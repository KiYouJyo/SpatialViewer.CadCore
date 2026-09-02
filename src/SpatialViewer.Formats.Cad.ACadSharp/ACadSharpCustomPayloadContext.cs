using ACadSharp.IO;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Per-import raw custom-payload context. AsyncLocal keeps parallel imports isolated while allowing the
/// reader factory and the later entity adapter to share evidence without threading file-path/reader concerns through every mapper.
/// </summary>
internal static class ACadSharpCustomPayloadContext
{
    private static readonly AsyncLocal<DxfCustomPayloadScanResult?> CurrentDxfScan = new();
    private static readonly AsyncLocal<DwgRawObjectCaptureState?> CurrentDwgCapture = new();

    public static void Initialize(string filePath)
    {
        CurrentDxfScan.Value = ACadSharpDxfCustomPayloadReader.Scan(filePath);
        CurrentDwgCapture.Value = null;
    }

    public static void InitializeDwg(DwgReader reader, global::ACadSharp.CadDocument document)
        => CurrentDwgCapture.Value = ACadSharpDwgRawObjectReader.Initialize(reader, document);

    public static CadDxfCustomPayload? FindDxfPayload(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle)) return null;
        return CurrentDxfScan.Value?.Payloads.TryGetValue(handle, out var payload) == true ? payload : null;
    }

    public static CadDwgCustomObjectRecord? FindDwgObjectRecord(string handle)
        => CurrentDwgCapture.Value?.Find(handle);

    public static DxfCustomPayloadScanResult? Snapshot() => CurrentDxfScan.Value;

    public static DwgRawObjectCaptureSnapshot? SnapshotDwg() => CurrentDwgCapture.Value?.Snapshot();

    public static void Clear()
    {
        CurrentDxfScan.Value = null;
        CurrentDwgCapture.Value = null;
    }
}
