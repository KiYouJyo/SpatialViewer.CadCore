using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

/// <summary>Why two retained DWG custom-object records can or cannot be compared byte-by-byte.</summary>
public enum CadDwgCustomObjectRecordDiffStatus
{
    Comparable,
    LengthMismatch,
    TruncatedInput
}

/// <summary>One contiguous changed byte range. The compared byte values are intentionally not exposed.</summary>
public sealed record CadDwgCustomObjectChangedByteRange(int Offset, int Length);

/// <summary>
/// Privacy-safe report for two retained DWG custom-object records. It contains only byte counts, an exact
/// common-prefix length, and changed ranges for equal-length records; no object-section offset, hash, handle,
/// file name, path, or raw byte value is exported.
/// </summary>
public sealed record CadDwgCustomObjectRecordDiffReport(
    CadDwgCustomObjectRecordDiffStatus Status,
    int BeforeByteCount,
    int AfterByteCount,
    int CommonPrefixByteCount,
    IReadOnlyList<CadDwgCustomObjectChangedByteRange> ChangedRanges)
{
    public bool IsByteComparable => Status == CadDwgCustomObjectRecordDiffStatus.Comparable;
    public int ChangedByteCount => ChangedRanges.Sum(range => range.Length);
}

/// <summary>
/// Compares bounded raw DWG custom-object records without returning the underlying bytes. Equal-length records
/// are compared at identical byte offsets. Different-length records are not aligned heuristically because one
/// inserted field can shift the remainder of a proprietary object stream and create plausible but false matches.
/// </summary>
public static class CadDwgCustomObjectRecordDiffer
{
    /// <summary>
    /// Preferred object-oriented A/B entry point. Custom identity and DWG capture method must match, and both
    /// entities must contain retained raw DWG object records before any byte-difference candidates are produced.
    /// </summary>
    public static CadDwgCustomObjectRecordDiffReport Compare(
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        CadDxfCustomPayloadDiffer.ValidateEntityIdentity(before, after);
        var beforeRecord = before.RawDwgObjectRecord
            ?? throw new ArgumentException("The before custom entity does not contain retained raw DWG object-record evidence.", nameof(before));
        var afterRecord = after.RawDwgObjectRecord
            ?? throw new ArgumentException("The after custom entity does not contain retained raw DWG object-record evidence.", nameof(after));
        return Compare(beforeRecord, afterRecord);
    }

    /// <summary>
    /// Low-level record comparison. Capture methods must match because byte offsets from different extraction
    /// paths are not assumed to have the same framing.
    /// </summary>
    public static CadDwgCustomObjectRecordDiffReport Compare(
        CadDwgCustomObjectRecord before,
        CadDwgCustomObjectRecord after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (!string.Equals(before.CaptureMethod, after.CaptureMethod, StringComparison.Ordinal))
            throw new ArgumentException("DWG custom-object records captured by different methods cannot be compared byte-by-byte.", nameof(after));

        var beforeBytes = before.Bytes.Span;
        var afterBytes = after.Bytes.Span;
        var commonPrefix = CommonPrefixLength(beforeBytes, afterBytes);
        if (before.IsTruncated || after.IsTruncated)
        {
            return Report(
                CadDwgCustomObjectRecordDiffStatus.TruncatedInput,
                beforeBytes.Length,
                afterBytes.Length,
                commonPrefix,
                Array.Empty<CadDwgCustomObjectChangedByteRange>());
        }

        if (beforeBytes.Length != afterBytes.Length)
        {
            return Report(
                CadDwgCustomObjectRecordDiffStatus.LengthMismatch,
                beforeBytes.Length,
                afterBytes.Length,
                commonPrefix,
                Array.Empty<CadDwgCustomObjectChangedByteRange>());
        }

        var ranges = ChangedRanges(beforeBytes, afterBytes);
        return Report(
            CadDwgCustomObjectRecordDiffStatus.Comparable,
            beforeBytes.Length,
            afterBytes.Length,
            commonPrefix,
            ranges);
    }

    private static int CommonPrefixLength(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        var commonLength = Math.Min(before.Length, after.Length);
        var index = 0;
        while (index < commonLength && before[index] == after[index]) index++;
        return index;
    }

    private static CadDwgCustomObjectChangedByteRange[] ChangedRanges(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        var ranges = new List<CadDwgCustomObjectChangedByteRange>();
        var index = 0;
        while (index < before.Length)
        {
            if (before[index] == after[index])
            {
                index++;
                continue;
            }

            var start = index;
            while (index < before.Length && before[index] != after[index]) index++;
            ranges.Add(new CadDwgCustomObjectChangedByteRange(start, index - start));
        }

        return ranges.ToArray();
    }

    private static CadDwgCustomObjectRecordDiffReport Report(
        CadDwgCustomObjectRecordDiffStatus status,
        int beforeByteCount,
        int afterByteCount,
        int commonPrefixByteCount,
        IEnumerable<CadDwgCustomObjectChangedByteRange> changedRanges)
        => new(
            status,
            beforeByteCount,
            afterByteCount,
            commonPrefixByteCount,
            new ReadOnlyCollection<CadDwgCustomObjectChangedByteRange>(changedRanges.ToArray()));
}
