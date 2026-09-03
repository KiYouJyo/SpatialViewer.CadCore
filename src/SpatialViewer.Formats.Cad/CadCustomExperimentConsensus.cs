using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

/// <summary>Structural custom-object identity retained in privacy-safe experiment evidence.</summary>
public sealed record CadCustomExperimentIdentity(
    string DxfName,
    string CppClassName,
    string ApplicationName);

/// <summary>One privacy-safe DXF A/B observation. Raw before/after values are never retained.</summary>
public sealed record CadDxfCustomExperimentObservation(
    CadCustomExperimentIdentity Identity,
    CadDxfCustomPayloadDiffStatus Status,
    string BeforeFingerprint,
    string AfterFingerprint,
    IReadOnlyList<CadDxfCustomPayloadValueChange> ValueChanges);

/// <summary>
/// Candidate DXF slots that changed in every comparable independent observation of the same custom-object schema.
/// A stable slot is evidence only and must not be named as a semantic field without independent verification.
/// </summary>
public sealed record CadDxfCustomExperimentConsensus(
    CadCustomExperimentIdentity Identity,
    string SchemaFingerprint,
    int ObservationCount,
    IReadOnlyList<CadDxfCustomPayloadValueChange> StableValueChanges)
{
    public bool HasStableCandidate => StableValueChanges.Count > 0;
}

/// <summary>One privacy-safe DWG A/B observation. Raw bytes and object-section offsets are never retained.</summary>
public sealed record CadDwgCustomExperimentObservation(
    CadCustomExperimentIdentity Identity,
    string CaptureMethod,
    CadDwgCustomObjectRecordDiffStatus Status,
    int ByteCount,
    IReadOnlyList<CadDwgCustomObjectChangedByteRange> ChangedRanges);

/// <summary>
/// Byte ranges that changed in every comparable independent observation of the same custom-object record profile.
/// Stable ranges remain framing/proprietary-stream evidence only and are not decoded Tianzheng Databits fields.
/// </summary>
public sealed record CadDwgCustomExperimentConsensus(
    CadCustomExperimentIdentity Identity,
    string CaptureMethod,
    int ByteCount,
    int ObservationCount,
    IReadOnlyList<CadDwgCustomObjectChangedByteRange> StableChangedRanges)
{
    public bool HasStableCandidate => StableChangedRanges.Count > 0;
}

/// <summary>
/// Builds repeatability evidence from controlled custom-object A/B experiments. At least two independent
/// observations are required so a one-off changed slot cannot be presented as repeatable evidence.
/// </summary>
public static class CadCustomExperimentAnalyzer
{
    private const int MaxObservations = 10_000;

    public static CadDxfCustomExperimentObservation ObserveDxf(
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var report = CadDxfCustomPayloadDiffer.Compare(before, after);
        return new CadDxfCustomExperimentObservation(
            Identity(before, after),
            report.Status,
            report.BeforeFingerprint,
            report.AfterFingerprint,
            new ReadOnlyCollection<CadDxfCustomPayloadValueChange>(report.ValueChanges.ToArray()));
    }

    public static CadDwgCustomExperimentObservation ObserveDwg(
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var report = CadDwgCustomObjectRecordDiffer.Compare(before, after);
        var record = before.RawDwgObjectRecord!;
        return new CadDwgCustomExperimentObservation(
            Identity(before, after),
            record.CaptureMethod,
            report.Status,
            report.BeforeByteCount,
            new ReadOnlyCollection<CadDwgCustomObjectChangedByteRange>(report.ChangedRanges.ToArray()));
    }

    public static CadDxfCustomExperimentConsensus BuildDxfConsensus(
        IEnumerable<CadDxfCustomExperimentObservation> observations)
    {
        var items = Materialize(observations);
        var first = items[0];
        if (first.Status != CadDxfCustomPayloadDiffStatus.Comparable
            || !string.Equals(first.BeforeFingerprint, first.AfterFingerprint, StringComparison.Ordinal))
        {
            throw new ArgumentException("DXF consensus requires comparable observations with one unchanged structural schema.", nameof(observations));
        }

        var stable = new HashSet<CadDxfCustomPayloadValueChange>(first.ValueChanges);
        foreach (var item in items.Skip(1))
        {
            if (!SameIdentity(first.Identity, item.Identity))
                throw new ArgumentException("DXF consensus observations must have the same custom-object identity.", nameof(observations));
            if (item.Status != CadDxfCustomPayloadDiffStatus.Comparable
                || !string.Equals(item.BeforeFingerprint, item.AfterFingerprint, StringComparison.Ordinal)
                || !string.Equals(first.BeforeFingerprint, item.BeforeFingerprint, StringComparison.Ordinal))
            {
                throw new ArgumentException("DXF consensus requires comparable observations from the same structural schema.", nameof(observations));
            }

            stable.IntersectWith(item.ValueChanges);
        }

        var ordered = stable
            .OrderBy(change => change.GroupIndex)
            .ThenBy(change => change.Code)
            .ThenBy(change => change.CodeOccurrence)
            .ToArray();
        return new CadDxfCustomExperimentConsensus(
            first.Identity,
            first.BeforeFingerprint,
            items.Count,
            new ReadOnlyCollection<CadDxfCustomPayloadValueChange>(ordered));
    }

    public static CadDwgCustomExperimentConsensus BuildDwgConsensus(
        IEnumerable<CadDwgCustomExperimentObservation> observations)
    {
        var items = Materialize(observations);
        var first = items[0];
        if (first.Status != CadDwgCustomObjectRecordDiffStatus.Comparable)
            throw new ArgumentException("DWG consensus requires byte-comparable observations.", nameof(observations));

        var stable = first.ChangedRanges.ToArray();
        foreach (var item in items.Skip(1))
        {
            if (!SameIdentity(first.Identity, item.Identity))
                throw new ArgumentException("DWG consensus observations must have the same custom-object identity.", nameof(observations));
            if (item.Status != CadDwgCustomObjectRecordDiffStatus.Comparable
                || item.ByteCount != first.ByteCount
                || !string.Equals(item.CaptureMethod, first.CaptureMethod, StringComparison.Ordinal))
            {
                throw new ArgumentException("DWG consensus requires comparable records with the same byte count and capture method.", nameof(observations));
            }

            stable = IntersectRanges(stable, item.ChangedRanges);
        }

        return new CadDwgCustomExperimentConsensus(
            first.Identity,
            first.CaptureMethod,
            first.ByteCount,
            items.Count,
            new ReadOnlyCollection<CadDwgCustomObjectChangedByteRange>(stable));
    }

    private static List<T> Materialize<T>(IEnumerable<T> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent observations are required for repeatability consensus.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"Experiment consensus supports at most {MaxObservations} observations.", nameof(observations));
        return items;
    }

    private static CadCustomExperimentIdentity Identity(CadCustomEntity before, CadCustomEntity after)
    {
        static string Known(string? primary, string? fallback)
            => string.IsNullOrWhiteSpace(primary) ? fallback ?? string.Empty : primary;

        var dxfName = string.IsNullOrWhiteSpace(before.ClassDefinition?.DxfName)
            ? before.SourceEntityType
            : before.ClassDefinition.DxfName;
        return new CadCustomExperimentIdentity(
            dxfName,
            Known(before.ClassDefinition?.CppClassName, after.ClassDefinition?.CppClassName),
            Known(before.ClassDefinition?.ApplicationName, after.ClassDefinition?.ApplicationName));
    }

    private static bool SameIdentity(CadCustomExperimentIdentity left, CadCustomExperimentIdentity right)
        => string.Equals(left.DxfName, right.DxfName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.CppClassName, right.CppClassName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.ApplicationName, right.ApplicationName, StringComparison.OrdinalIgnoreCase);

    private static CadDwgCustomObjectChangedByteRange[] IntersectRanges(
        IReadOnlyList<CadDwgCustomObjectChangedByteRange> left,
        IReadOnlyList<CadDwgCustomObjectChangedByteRange> right)
    {
        var result = new List<CadDwgCustomObjectChangedByteRange>();
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Count && rightIndex < right.Count)
        {
            var a = left[leftIndex];
            var b = right[rightIndex];
            var aEnd = checked(a.Offset + a.Length);
            var bEnd = checked(b.Offset + b.Length);
            var start = Math.Max(a.Offset, b.Offset);
            var end = Math.Min(aEnd, bEnd);
            if (start < end) AddRange(result, start, end - start);

            if (aEnd <= bEnd) leftIndex++;
            if (bEnd <= aEnd) rightIndex++;
        }

        return result.ToArray();
    }

    private static void AddRange(List<CadDwgCustomObjectChangedByteRange> ranges, int offset, int length)
    {
        if (ranges.Count > 0)
        {
            var previous = ranges[^1];
            if (previous.Offset + previous.Length == offset)
            {
                ranges[^1] = new CadDwgCustomObjectChangedByteRange(previous.Offset, previous.Length + length);
                return;
            }
        }

        ranges.Add(new CadDwgCustomObjectChangedByteRange(offset, length));
    }
}
