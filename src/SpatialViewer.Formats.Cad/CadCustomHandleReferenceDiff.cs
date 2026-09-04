using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

public enum CadCustomHandleReferenceDiffStatus
{
    Comparable,
    LayoutMismatch,
    MissingReferenceEvidence
}

/// <summary>
/// One changed object-reference slot identified only by DXF group code and same-code occurrence.
/// Source/target handles are intentionally never exposed.
/// </summary>
public sealed record CadCustomHandleReferenceValueChange(
    int GroupCode,
    int CodeOccurrence);

/// <summary>
/// Privacy-safe comparison of two retained custom-object reference layouts. Signatures contain only
/// reference group-code order/occurrence; target handles are used in-memory for equality only.
/// </summary>
public sealed record CadCustomHandleReferenceDiffReport(
    CadCustomHandleReferenceDiffStatus Status,
    string BeforeLayoutSignature,
    string AfterLayoutSignature,
    IReadOnlyList<CadCustomHandleReferenceValueChange> ValueChanges)
{
    public bool IsComparable => Status == CadCustomHandleReferenceDiffStatus.Comparable;
    public int ChangedReferenceCount => ValueChanges.Count;
}

public static class CadCustomHandleReferenceDiffer
{
    public static CadCustomHandleReferenceDiffReport Compare(CadCustomEntity before, CadCustomEntity after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        CadDxfCustomPayloadDiffer.ValidateEntityIdentity(before, after);

        var beforeReferences = before.HandleReferences;
        var afterReferences = after.HandleReferences;
        var beforeSignature = LayoutSignature(beforeReferences);
        var afterSignature = LayoutSignature(afterReferences);

        if (beforeReferences.Count == 0 || afterReferences.Count == 0)
        {
            return Report(
                CadCustomHandleReferenceDiffStatus.MissingReferenceEvidence,
                beforeSignature,
                afterSignature,
                Array.Empty<CadCustomHandleReferenceValueChange>());
        }

        if (!SameLayout(beforeReferences, afterReferences))
        {
            return Report(
                CadCustomHandleReferenceDiffStatus.LayoutMismatch,
                beforeSignature,
                afterSignature,
                Array.Empty<CadCustomHandleReferenceValueChange>());
        }

        var occurrences = new Dictionary<int, int>();
        var changes = new List<CadCustomHandleReferenceValueChange>();
        for (var index = 0; index < beforeReferences.Count; index++)
        {
            var beforeReference = beforeReferences[index];
            occurrences.TryGetValue(beforeReference.GroupCode, out var occurrence);
            occurrence++;
            occurrences[beforeReference.GroupCode] = occurrence;
            if (!string.Equals(
                    CadDxfCustomPayloadProfiler.CanonicalHandle(beforeReference.TargetHandle),
                    CadDxfCustomPayloadProfiler.CanonicalHandle(afterReferences[index].TargetHandle),
                    StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new(beforeReference.GroupCode, occurrence));
            }
        }

        return Report(
            CadCustomHandleReferenceDiffStatus.Comparable,
            beforeSignature,
            afterSignature,
            changes);
    }

    private static bool SameLayout(
        IReadOnlyList<CadCustomHandleReference> before,
        IReadOnlyList<CadCustomHandleReference> after)
    {
        if (before.Count != after.Count) return false;
        for (var index = 0; index < before.Count; index++)
            if (before[index].GroupCode != after[index].GroupCode) return false;
        return true;
    }

    private static string LayoutSignature(IReadOnlyList<CadCustomHandleReference> references)
    {
        if (references.Count == 0) return "none";
        var occurrences = new Dictionary<int, int>();
        var slots = new string[references.Count];
        for (var index = 0; index < references.Count; index++)
        {
            var code = references[index].GroupCode;
            occurrences.TryGetValue(code, out var occurrence);
            occurrence++;
            occurrences[code] = occurrence;
            slots[index] = $"{code}#{occurrence}";
        }
        return string.Join(',', slots);
    }

    private static CadCustomHandleReferenceDiffReport Report(
        CadCustomHandleReferenceDiffStatus status,
        string beforeSignature,
        string afterSignature,
        IEnumerable<CadCustomHandleReferenceValueChange> changes)
        => new(
            status,
            beforeSignature,
            afterSignature,
            new ReadOnlyCollection<CadCustomHandleReferenceValueChange>(changes.ToArray()));
}

public sealed record CadCustomHandleReferenceExperimentObservation(
    CadCustomExperimentIdentity Identity,
    CadCustomHandleReferenceDiffStatus Status,
    string BeforeLayoutSignature,
    string AfterLayoutSignature,
    IReadOnlyList<CadCustomHandleReferenceValueChange> ValueChanges);

public sealed record CadCustomHandleReferenceExperimentConsensus(
    CadCustomExperimentIdentity Identity,
    string LayoutSignature,
    int ObservationCount,
    IReadOnlyList<CadCustomHandleReferenceValueChange> StableValueChanges)
{
    public bool HasStableCandidate => StableValueChanges.Count > 0;
}

public static class CadCustomHandleReferenceExperimentAnalyzer
{
    private const int MaxObservations = 10_000;

    public static CadCustomHandleReferenceExperimentObservation Observe(
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var diff = CadCustomHandleReferenceDiffer.Compare(before, after);
        return new(
            Identity(before, after),
            diff.Status,
            diff.BeforeLayoutSignature,
            diff.AfterLayoutSignature,
            new ReadOnlyCollection<CadCustomHandleReferenceValueChange>(diff.ValueChanges.ToArray()));
    }

    public static CadCustomHandleReferenceExperimentConsensus BuildConsensus(
        IEnumerable<CadCustomHandleReferenceExperimentObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent custom-reference observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"Custom-reference consensus supports at most {MaxObservations} observations.", nameof(observations));

        var first = items[0] ?? throw new ArgumentException("Custom-reference observation cannot be null.", nameof(observations));
        ValidateComparable(first, nameof(observations));
        var stable = new HashSet<CadCustomHandleReferenceValueChange>(first.ValueChanges);
        foreach (var item in items.Skip(1))
        {
            if (item is null) throw new ArgumentException("Custom-reference observation cannot be null.", nameof(observations));
            if (!SameIdentity(first.Identity, item.Identity))
                throw new ArgumentException("Custom-reference consensus observations must have the same custom-object identity.", nameof(observations));
            ValidateComparable(item, nameof(observations));
            if (!string.Equals(first.BeforeLayoutSignature, item.BeforeLayoutSignature, StringComparison.Ordinal))
                throw new ArgumentException("Custom-reference consensus requires one shared reference layout.", nameof(observations));
            stable.IntersectWith(item.ValueChanges);
        }

        var ordered = stable
            .OrderBy(change => change.GroupCode)
            .ThenBy(change => change.CodeOccurrence)
            .ToArray();
        return new(
            first.Identity,
            first.BeforeLayoutSignature,
            items.Count,
            new ReadOnlyCollection<CadCustomHandleReferenceValueChange>(ordered));
    }

    private static void ValidateComparable(
        CadCustomHandleReferenceExperimentObservation observation,
        string parameterName)
    {
        if (observation.Status != CadCustomHandleReferenceDiffStatus.Comparable
            || !string.Equals(observation.BeforeLayoutSignature, observation.AfterLayoutSignature, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Custom-reference consensus requires comparable observations with one unchanged reference layout.",
                parameterName);
        }
    }

    private static CadCustomExperimentIdentity Identity(CadCustomEntity before, CadCustomEntity after)
    {
        var dxfName = string.IsNullOrWhiteSpace(before.ClassDefinition?.DxfName)
            ? before.SourceEntityType
            : before.ClassDefinition.DxfName;
        var cpp = string.IsNullOrWhiteSpace(before.ClassDefinition?.CppClassName)
            ? after.ClassDefinition?.CppClassName ?? string.Empty
            : before.ClassDefinition.CppClassName;
        var application = string.IsNullOrWhiteSpace(before.ClassDefinition?.ApplicationName)
            ? after.ClassDefinition?.ApplicationName ?? string.Empty
            : before.ClassDefinition.ApplicationName;
        return new(dxfName, cpp, application);
    }

    private static bool SameIdentity(CadCustomExperimentIdentity left, CadCustomExperimentIdentity right)
        => string.Equals(left.DxfName, right.DxfName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.CppClassName, right.CppClassName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.ApplicationName, right.ApplicationName, StringComparison.OrdinalIgnoreCase);
}
