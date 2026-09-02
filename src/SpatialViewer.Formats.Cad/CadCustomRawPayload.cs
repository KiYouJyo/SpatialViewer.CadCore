namespace SpatialViewer.Formats.Cad;

/// <summary>
/// One untyped DXF group retained from an application-defined entity.
/// <see cref="RawValue"/> uses a byte-preserving projection so the original value-line bytes can be reconstructed later without guessing a Tianzheng schema or code page.
/// </summary>
public sealed record CadRawDxfGroup(int Code, string RawValue);

/// <summary>
/// Raw DXF payload for a custom CAD entity. This is an evidence layer for later native Tianzheng decoding; retaining data here does not imply that CadCore understands the fields yet.
/// </summary>
public sealed record CadDxfCustomPayload(
    IReadOnlyList<CadRawDxfGroup> Groups,
    bool IsTruncated = false)
{
    /// <summary>Byte-to-character projection used while preserving raw text-DXF value lines.</summary>
    public string ByteProjection { get; init; } = "ISO-8859-1";
}

/// <summary>
/// Bounded byte copy of one complete custom-object record from the decompressed DWG AcDbObjects section.
/// The record still contains DWG common/object framing and is intentionally not mislabeled as decoded proprietary entity data.
/// </summary>
public sealed record CadDwgCustomObjectRecord
{
    public CadDwgCustomObjectRecord(ReadOnlyMemory<byte> bytes, long objectSectionOffset, bool isTruncated, string captureMethod)
    {
        Bytes = bytes.ToArray();
        ObjectSectionOffset = objectSectionOffset;
        IsTruncated = isTruncated;
        CaptureMethod = captureMethod ?? string.Empty;
    }

    public ReadOnlyMemory<byte> Bytes { get; }
    public long ObjectSectionOffset { get; }
    public bool IsTruncated { get; }
    public string CaptureMethod { get; }
    public int ByteCount => Bytes.Length;
}
