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
        var item = Assert.Single(document.Scene.GetItems()); Assert.Equal("#FF0000", item.Style.Stroke); Assert.Equal("LINE", item.Geometry is LineGeometry ? "LINE" : string.Empty); Assert.Equal("ROAD", item.Metadata["Layer"]); Assert.Equal("A", item.Metadata["Handle"]); Assert.Equal("1", item.Metadata["CadColorIndex"]);
    }

    [Theory]
    [InlineData(1, "#FF0000")]
    [InlineData(7, "#FFFFFF")]
    [InlineData(8, "#808080")]
    [InlineData(9, "#C0C0C0")]
    [InlineData(30, "#FF7F00")]
    [InlineData(113, "#52A57C")]
    [InlineData(250, "#000000")]
    [InlineData(254, "#CCCCCC")]
    [InlineData(255, "#FFFFFF")]
    public void AciPaletteMatchesRepresentativeAutoCadColors(int index, string expected) => Assert.Equal(expected, CadColorPalette.GetHex(index));

    [Fact]
    public void AciPaletteCoversEveryValidIndexDeterministically()
    {
        for (var index = 1; index <= 255; index++)
        {
            var color = CadColorPalette.GetHex(index);
            Assert.StartsWith("#", color, StringComparison.Ordinal);
            Assert.Equal(7, color.Length);
            Assert.True(uint.TryParse(color[1..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _));
        }
    }

    [Fact]
    public void TranslatorPreservesTrueColorExactly()
    {
        var document = new CadDocument("true-colour", "DXF", "AC1015", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(7)) }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { new CadLineEntity("RGB", new(0, 0), new(1, 0), Color: CadColor.FromRgb(12, 34, 56)) });
        var item = Assert.Single(document.Scene.GetItems());
        Assert.Equal("#0C2238", item.Style.Stroke);
        Assert.Equal("TrueColor", item.Metadata["CadColorKind"]);
        Assert.Equal("#0C2238", item.Metadata["CadTrueColor"]);
    }

    [Fact]
    public void TopLevelByBlockFallsBackToAdaptiveAci7RatherThanLayerColor()
    {
        var document = new CadDocument("byblock", "DXF", "AC1015", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(1)) }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { new CadLineEntity("B", new(0, 0), new(1, 0), Color: CadColor.ByBlock) });
        var item = Assert.Single(document.Scene.GetItems());
        Assert.Equal("#FFFFFF", item.Style.Stroke);
        Assert.Equal("7", item.Metadata["CadColorIndex"]);
        Assert.Equal(bool.TrueString, item.Metadata["BackgroundAdaptiveStroke"]);
    }

    [Fact]
    public void NestedByBlockColorInheritancePreservesAciSemantics()
    {
        var leaf = new CadBlockDefinition("LEAF", Point2D.Origin, new CadEntity[] { new CadLineEntity("L1", new(0, 0), new(10, 0), Color: CadColor.ByBlock) });
        var middle = new CadBlockDefinition("MIDDLE", Point2D.Origin, new CadEntity[] { new CadBlockReferenceEntity("M1", "LEAF", Point2D.Origin, Color: CadColor.ByBlock) });
        var document = new CadDocument("nested-colour", "DXF", "AC1015", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(7)) }, new[] { leaf, middle }, new CadEntity[] { new CadBlockReferenceEntity("I1", "MIDDLE", Point2D.Origin, Color: CadColor.FromAci(30)) });
        var item = Assert.Single(document.Scene.GetItems());
        Assert.Equal("#FF7F00", item.Style.Stroke);
        Assert.Equal("Aci", item.Metadata["CadColorKind"]);
        Assert.Equal("30", item.Metadata["CadColorIndex"]);
        Assert.False(item.Metadata.ContainsKey("BackgroundAdaptiveStroke"));
    }

    [Fact]
    public void TranslatorPreservesArcAsArcGeometry()
    {
        var document = new CadDocument("arc", "DXF", "AC1015", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(7)) }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { new CadArcEntity("ARC1", new(0, 0), 50, 0, Math.PI / 2) });
        Assert.IsType<ArcGeometry>(Assert.Single(document.Scene.GetItems()).Geometry);
    }

    [Fact]
    public void NestedTransformedArcRemainsAnalyticArcGeometry()
    {
        var arc = new CadBlockDefinition("ARC", Point2D.Origin, new CadEntity[] { new CadArcEntity("A1", new(0, 0), 20, 0, Math.PI * 1.5, Color: CadColor.ByBlock) });
        var nested = new CadBlockDefinition("NEST", Point2D.Origin, new CadEntity[] { new CadBlockReferenceEntity("N1", "ARC", new(5, 3), Math.PI / 5, 2, .5, Color: CadColor.ByBlock) });
        var document = new CadDocument("nested-arc", "DXF", "AC1015", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(7)) }, new[] { arc, nested }, new CadEntity[] { new CadBlockReferenceEntity("I1", "NEST", new(100, 200), Math.PI / 7, 1.5, .75, Color: CadColor.FromAci(4)) });
        var item = Assert.Single(document.Scene.GetItems());
        Assert.IsType<ArcGeometry>(item.Geometry);
        Assert.NotEqual(Transform2D.Identity, item.Transform);
        Assert.Equal("#00FFFF", item.Style.Stroke);
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
