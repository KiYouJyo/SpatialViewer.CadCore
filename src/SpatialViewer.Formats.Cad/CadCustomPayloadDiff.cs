using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

/// <summary>Why two retained custom-object DXF payloads can or cannot be compared value-by-value.</summary>
public enum CadDxfCustomPayloadDiffStatus
{
    Comparable,
    LayoutMismatch,
    TruncatedInput
}

/// <summary>
/// One changed raw value identified only by structural position. The original before/after values are
/// intentionally not exposed, allowing controlled A/B samples to be compared without exporting drawing data.
/// </summary>
public sealed record CadDxfCustomPayloadValueChange(
    int GroupIndex,
    int Code,
    int CodeOccurrence);

/// <summary>Aggregate change in the number of occurrences of one DXF group code.</summary>
public sealed record CadDxfCustomPayloadCodeCountDelta(
    int Code,
    int BeforeCount,
    int AfterCount);

/// <summary>
/// Privacy-safe structural/value-change report for two custom-object DXF payloads. Fingerprints and group-code
/// information are structural evidence only; raw values, coordinates, text, handles, paths, and drawing names
/// are never included.
/// </summary>
public sealed record CadDxfCustomPayloadDiffReport(
    CadDxfCustomPayloadDiffStatus Status,
    string BeforeFingerprint,
    string AfterFingerprint,
    int BeforeGroupCount,
    int AfterGroupCount,
    int? FirstLayoutMismatchIndex,
    IReadOnlyList<CadDxfCustomPayloadValueChange> ValueChanges,
    IReadOnlyList<CadDxfCustomPayloadCodeCountDelta> CodeCountDeltas)
{
    public bool IsComparableLayout => Status == CadDxfCustomPayloadDiffStatus.Comparable;
    public int ChangedValueCount => ValueChanges.Count;
}

/// <summary>
/// Compares retained custom-object DXF payloads without returning the original values. This is intended for
/// controlled reverse-engineering pairs such as "column width 400" versus "column width 500": if the raw
/// group-code layout is identical, the report identifies only which structural slots changed.
/// </summary>
public static class CadDxfCustomPayloadDiffer
{
    /// <summary>
    /// Compare two custom entities only after their application-defined identities are compatible and both
    /// sides contain retained raw DXF evidence. This is the preferred entry point for controlled object A/B
    /// experiments because it prevents accidental comparison of unrelated custom classes.
    /// </summary>
    public static CadDxfCustomPayloadDiffReport Compare(
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ValidateEntityIdentity(before, after);
        var beforePayload = before.RawDxfPayload
            ?? throw new ArgumentException("The before custom entity does not contain retained raw DXF payload evidence.", nameof(before));
        var afterPayload = after.RawDxfPayload
            ?? throw new ArgumentException("The after custom entity does not contain retained raw DXF payload evidence.", nameof(after));
        return Compare(beforePayload, afterPayload);
    }

    /// <summary>
    /// Low-level payload comparison. Callers are responsible for establishing that both payloads belong to
    /// the same logical custom-object class; object-oriented experiments should prefer the entity overload.
    /// </summary>
    public static CadDxfCustomPayloadDiffReport Compare(
        CadDxfCustomPayload before,
        CadDxfCustomPayload after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var beforeProfile = CadDxfCustomPayloadProfiler.Create(before)!;
        var afterProfile = CadDxfCustomPayloadProfiler.Create(after)!;
        if (before.IsTruncated || after.IsTruncated)
        {
            return Report(
                CadDxfCustomPayloadDiffStatus.TruncatedInput,
                beforeProfile.Fingerprint,
                afterProfile.Fingerprint,
                before.Groups.Count,
                after.Groups.Count,
                null,
                Array.Empty<CadDxfCustomPayloadValueChange>(),
                Array.Empty<CadDxfCustomPayloadCodeCountDelta>());
        }

        var firstLayoutMismatch = FirstLayoutMismatch(before.Groups, after.Groups);
        if (firstLayoutMismatch is not null)
        {
            return Report(
                CadDxfCustomPayloadDiffStatus.LayoutMismatch,
                beforeProfile.Fingerprint,
                afterProfile.Fingerprint,
                before.Groups.Count,
                after.Groups.Count,
                firstLayoutMismatch,
                Array.Empty<CadDxfCustomPayloadValueChange>(),
                CodeCountDeltas(before.Groups, after.Groups));
        }

        var occurrences = new Dictionary<int, int>();
        var changes = new List<CadDxfCustomPayloadValueChange>();
        for (var index = 0; index < before.Groups.Count; index++)
        {
            var beforeGroup = before.Groups[index];
            occurrences.TryGetValue(beforeGroup.Code, out var occurrence);
            occurrence++;
            occurrences[beforeGroup.Code] = occurrence;
            if (!string.Equals(beforeGroup.RawValue, after.Groups[index].RawValue, StringComparison.Ordinal))
                changes.Add(new CadDxfCustomPayloadValueChange(index, beforeGroup.Code, occurrence));
        }

        return Report(
            CadDxfCustomPayloadDiffStatus.Comparable,
            beforeProfile.Fingerprint,
            afterProfile.Fingerprint,
            before.Groups.Count,
            after.Groups.Count,
            null,
            changes,
            Array.Empty<CadDxfCustomPayloadCodeCountDelta>());
    }

    private static void ValidateEntityIdentity(CadCustomEntity before, CadCustomEntity after)
    {
        if (!string.Equals(EntityDxfIdentity(before), EntityDxfIdentity(after), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Custom entities must have the same DXF identity before payload comparison.", nameof(after));

        var beforeCpp = before.ClassDefinition?.CppClassName;
        var afterCpp = after.ClassDefinition?.CppClassName;
        if (!string.IsNullOrWhiteSpace(beforeCpp)
            && !string.IsNullOrWhiteSpace(afterCpp)
            && !string.Equals(beforeCpp, afterCpp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Custom entities with different known C++ class identities cannot be compared as one A/B object profile.", nameof(after));
        }

        var beforeApplication = before.ClassDefinition?.ApplicationName;
        var afterApplication = after.ClassDefinition?.ApplicationName;
        if (!string.IsNullOrWhiteSpace(beforeApplication)
            && !string.IsNullOrWhiteSpace(afterApplication)
            && !string.Equals(beforeApplication, afterApplication, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Custom entities from different known applications cannot be compared as one A/B object profile.", nameof(after));
        }
    }

    private static string EntityDxfIdentity(CadCustomEntity entity)
        => string.IsNullOrWhiteSpace(entity.ClassDefinition?.DxfName)
            ? entity.SourceEntityType
            : entity.ClassDefinition.DxfName;

    private static int? FirstLayoutMismatch(
        IReadOnlyList<CadRawDxfGroup> before,
        IReadOnlyList<CadRawDxfGroup> after)
    {
        var commonCount = Math.Min(before.Count, after.Count);
        for (var index = 0; index < commonCount; index++)
        {
            if (before[index].Code != after[index].Code) return index;
        }

        return before.Count == after.Count ? null : commonCount;
    }

    private static CadDxfCustomPayloadCodeCountDelta[] CodeCountDeltas(
        IReadOnlyList<CadRawDxfGroup> before,
        IReadOnlyList<CadRawDxfGroup> after)
    {
        var beforeCounts = before.GroupBy(group => group.Code).ToDictionary(group => group.Key, group => group.Count());
        var afterCounts = after.GroupBy(group => group.Code).ToDictionary(group => group.Key, group => group.Count());
        var codes = beforeCounts.Keys.Concat(afterCounts.Keys).Distinct().OrderBy(code => code);
        return codes
            .Select(code => new CadDxfCustomPayloadCodeCountDelta(
                code,
                beforeCounts.GetValueOrDefault(code),
                afterCounts.GetValueOrDefault(code)))
            .Where(delta => delta.BeforeCount != delta.AfterCount)
            .ToArray();
    }

    private static CadDxfCustomPayloadDiffReport Report(
        CadDxfCustomPayloadDiffStatus status,
        string beforeFingerprint,
        string afterFingerprint,
        int beforeGroupCount,
        int afterGroupCount,
        int? firstLayoutMismatchIndex,
        IEnumerable<CadDxfCustomPayloadValueChange> valueChanges,
        IEnumerable<CadDxfCustomPayloadCodeCountDelta> codeCountDeltas)
        => new(
            status,
            beforeFingerprint,
            afterFingerprint,
            beforeGroupCount,
            afterGroupCount,
            firstLayoutMismatchIndex,
            new ReadOnlyCollection<CadDxfCustomPayloadValueChange>(valueChanges.ToArray()),
            new ReadOnlyCollection<CadDxfCustomPayloadCodeCountDelta>(codeCountDeltas.ToArray()));
}
