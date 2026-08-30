using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class CadSceneTests
{
    [Fact]
    public void TranslatorResolvesByLayerColorAndPreservesSourceMetadata()
    {
        var road = new CadLayer("ROAD", CadColor.FromAci(1));
        var document = new CadDocument("colour", "DXF", "AC1015", CadUnits.Millimetres, new[] { road }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { new CadLineEntity("A", new(0, 0), new(10, 0), "ROAD") });
        var item = Assert.Single(document.Scene.GetItems()); Assert.Equal("#FF0000", item.Style.Stroke); Assert.Equal("LINE", item.Geometry is LineGeometry ? "LINE" : string.Empty); Assert.Equal("ROAD", item.Metadata["Layer"]); Assert.Equal("A", item.Metadata["Handle"]);
    }

    [Fact]
    public void TranslatorPreservesArcAsArcGeometry()
    {
        var document = new CadDocument("arc", "DXF", "AC1015", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(7)) }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { new CadArcEntity("ARC1", new(0, 0), 50, 0, Math.PI / 2) });
        Assert.IsType<ArcGeometry>(Assert.Single(document.Scene.GetItems()).Geometry);
    }

    [Fact]
    public void TranslatorSupportsNestedNonUniformBlockTransforms()
    {
        var mark = new CadBlockDefinition("MARK", Point2D.Origin, new CadEntity[] { new CadLineEntity("M1", new(0, 0), new(10, 0)) });
        var nested = new CadBlockDefinition("NEST", Point2D.Origin, new CadEntity[] { new CadBlockReferenceEntity("N1", "MARK", new(5, 0), Math.PI / 2, 2, 1) });
        var document = new CadDocument("blocks", "DXF", "AC1015", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(7)) }, new[] { mark, nested }, new CadEntity[] { new CadBlockReferenceEntity("I1", "NEST", new(100, 200)) });
        var item = Assert.Single(document.Scene.GetItems()); Assert.Equal(105, item.Bounds.Center.X, 5); Assert.Equal(210, item.Bounds.Center.Y, 5); Assert.InRange(item.Bounds.Height, 19.9, 20.1);
    }

    [Fact]
    public async Task UnsupportedExtensionReportsFailureWithoutReaderInvocation()
    {
        var file = Path.Combine(Path.GetTempPath(), $"spatial-viewer-{Guid.NewGuid():N}.ifc");
        try { await File.WriteAllTextAsync(file, "not CAD"); var result = await new SpatialViewer.Formats.Cad.ACadSharp.ACadSharpCadImporter().ImportAsync(new ImportRequest(file)); Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, x => x.Code == "CAD_UNSUPPORTED_EXTENSION"); }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public async Task MalformedBlockReferenceIsDiagnosedAndSkippedSafely()
    {
        var result = await new SpatialViewer.Formats.Cad.ACadSharp.ACadSharpCadImporter().ImportAsync(new ImportRequest(Path.Combine(AppContext.BaseDirectory, "fixtures", "cad", "negative", "missing-block.dxf")));
        var document = Assert.IsType<CadDocument>(result.Document); Assert.True(result.IsSuccess); Assert.Contains(document.Diagnostics, x => x.Code == "CAD_INVALID_BLOCK_REFERENCE"); Assert.Empty(document.Scene.GetItems());
    }
}
