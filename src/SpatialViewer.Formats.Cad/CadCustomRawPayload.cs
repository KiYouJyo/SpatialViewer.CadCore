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
