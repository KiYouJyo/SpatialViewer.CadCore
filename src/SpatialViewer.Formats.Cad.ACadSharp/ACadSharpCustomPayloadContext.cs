using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Per-import raw custom-payload context. AsyncLocal keeps parallel imports isolated while allowing the
/// reader factory and the later entity adapter to share evidence without threading file-path concerns through every mapper.
/// </summary>
internal static class ACadSharpCustomPayloadContext
{
    private static readonly AsyncLocal<DxfCustomPayloadScanResult?> CurrentScan = new();

    public static void Initialize(string filePath)
        => CurrentScan.Value = ACadSharpDxfCustomPayloadReader.Scan(filePath);

    public static CadDxfCustomPayload? FindDxfPayload(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle)) return null;
        return CurrentScan.Value?.Payloads.TryGetValue(handle, out var payload) == true ? payload : null;
    }

    public static DxfCustomPayloadScanResult? Snapshot() => CurrentScan.Value;
}
