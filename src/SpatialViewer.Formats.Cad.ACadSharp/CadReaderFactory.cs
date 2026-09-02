using ACadSharp.IO;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Creates ACadSharp readers with compatibility-preserving options enabled.
/// Application-defined entities must survive the reader boundary so CadCore can
/// classify Tianzheng/custom objects instead of silently losing them.
/// </summary>
internal static class CadReaderFactory
{
    public static ICadReader CreateReader(string filePath)
    {
        ACadSharpCustomPayloadContext.Initialize(filePath);
        var reader = global::ACadSharp.IO.CadReaderFactory.CreateReader(filePath);
        switch (reader)
        {
            case DxfReader dxfReader:
                dxfReader.Configuration.KeepUnknownEntities = true;
                break;
            case DwgReader dwgReader:
                dwgReader.Configuration.KeepUnknownEntities = true;
                break;
        }

        return reader;
    }
}
