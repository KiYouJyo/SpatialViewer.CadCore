using ACadSharp.IO;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>Test-only helper that produces a legal deterministic DWG fixture from an in-repository DXF fixture.</summary>
public static class ACadSharpFixtureTranscoder
{
    public static void WriteDwgFromDxf(string dxfPath, string dwgPath)
    {
        using var reader = new DxfReader(dxfPath);
        var document = reader.Read();
        document.CreateDefaults();
        using var writer = new DwgWriter(dwgPath, document);
        writer.Write();
    }
}
