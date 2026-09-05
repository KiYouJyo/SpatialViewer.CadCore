using ACadSharp.IO;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Thin ACadSharp reader hook: native ACadSharp performs the DWG read first, then CadCore opens a read-only
/// evidence view over its already-decompressed object section. No DWG parsing behavior is replaced here.
/// </summary>
internal sealed class CadCoreDwgReader : DwgReader
{
    public CadCoreDwgReader(string filePath) : base(filePath)
    {
    }

    public override global::ACadSharp.CadDocument Read()
    {
        var document = base.Read();
        ACadSharpCustomPayloadContext.InitializeProxyGraphicsCommands(this);
        ACadSharpCustomPayloadContext.InitializeDwg(this, document);
        return document;
    }
}
