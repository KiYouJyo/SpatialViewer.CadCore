using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class RealDrawingFidelityV0123Tests
{
    [Fact]
    public void OrdinateDimensionReconstructsCadJogWithoutConnectingDefinitionDatum()
    {
        var dimension = new CadDimensionEntity(
            "ORD-V0123",
            CadDimensionKind.Ordinate,
            new Point2D(-10000, -10000),
            new Point2D(305, 200),
            "200",
            200,
            0,
            2.5,
            2.5,
            "ORD",
            new Dictionary<string, Point2D>
            {
                ["FeatureLocation"] = new(100, 100),
                ["LeaderEndpoint"] = new(300, 200)
            },
            Metadata: new Dictionary<string, string>
            {
                ["DimensionHorizontalDirection"] = "0",
                ["DimensionOrdinateTypeX"] = "False"
            });

        var lines = Document(dimension).Scene.GetItems()
            .Where(item => item.Id == dimension.ObjectId && item.Geometry is LineGeometry)
            .ToArray();

        Assert.Equal(3, lines.Length);
        Assert.All(lines, item => Assert.Equal("True", item.Metadata["DimensionOrdinateJogReconstructed"]));
        var bounds = lines.Select(item => item.Geometry!.GetBounds()).Aggregate(BoundingBox2D.Empty, static (current, next) => current.Union(next));
        Assert.InRange(bounds.MinX, 99.999999, 100.000001);
        Assert.InRange(bounds.MinY, 99.999999, 100.000001);
        Assert.InRange(bounds.MaxX, 299.999999, 300.000001);
        Assert.InRange(bounds.MaxY, 199.999999, 200.000001);
        Assert.DoesNotContain(lines, item =>
        {
            var line = Assert.IsType<LineGeometry>(item.Geometry);
            return line.Start.DistanceTo(dimension.DefinitionPoint) <= 1e-9 || line.End.DistanceTo(dimension.DefinitionPoint) <= 1e-9;
        });
    }

    [Fact]
    public void SeparateArrowSlotsFallBackToSharedArchitecturalTick()
    {
        var dimension = LinearDimension() with
        {
            Presentation = new CadDimensionPresentation(
                ArrowBlockName: "_ARCHTICK",
                FirstArrowBlockName: "",
                SecondArrowBlockName: "",
                SeparateArrowBlocks: true)
        };

        var arrows = Document(dimension).Scene.GetItems()
            .Where(item => item.Id == dimension.ObjectId
                && item.Geometry is LineGeometry
                && item.Metadata.TryGetValue("DimensionArrowResolvedKind", out var kind)
                && string.Equals(kind, "ArchitecturalTick", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, arrows.Length);
        Assert.All(arrows, item => Assert.Equal("_ARCHTICK", item.Metadata["DimensionArrowRequestedBlock"]));
    }

    [Fact]
    public void DimensionTextMiddlePointIsRenderedAsMiddleCenterAnchor()
    {
        var dimension = LinearDimension();

        var textNode = Document(dimension).Scene.GetItems()
            .Single(item => item.Id == dimension.ObjectId && item.Geometry is TextGeometry);
        var geometry = Assert.IsType<TextGeometry>(textNode.Geometry);

        Assert.Equal(TextHorizontalAlignment2D.Center, geometry.HorizontalAlignment);
        Assert.Equal(TextVerticalAlignment2D.Middle, geometry.VerticalAlignment);
        Assert.Equal("MiddleCenter", textNode.Metadata["DimensionTextAnchor"]);
        var bounds = geometry.GetBounds();
        Assert.InRange((bounds.MinX + bounds.MaxX) / 2, dimension.TextPosition.X - 1e-9, dimension.TextPosition.X + 1e-9);
        Assert.InRange((bounds.MinY + bounds.MaxY) / 2, dimension.TextPosition.Y - 1e-9, dimension.TextPosition.Y + 1e-9);
    }

    [Fact]
    public void DimStyleComponentColorsRemainIndependent()
    {
        var dimension = LinearDimension() with
        {
            DimensionLineColor = CadColor.FromAci(8),
            ExtensionLineColor = CadColor.FromAci(2),
            TextColor = CadColor.FromAci(1)
        };

        var items = Document(dimension).Scene.GetItems()
            .Where(item => item.Id == dimension.ObjectId)
            .ToArray();
        var lines = items.Where(item => item.Geometry is LineGeometry).ToArray();
        var text = Assert.Single(items, item => item.Geometry is TextGeometry);

        Assert.Contains(lines, item => item.Style.Stroke == "#808080");
        Assert.Contains(lines, item => item.Style.Stroke == "#FFFF00");
        Assert.Equal("#FF0000", text.Style.Stroke);
        Assert.Equal("#808080", text.Metadata["DimensionLineResolvedStroke"]);
        Assert.Equal("#FFFF00", text.Metadata["DimensionExtensionLineResolvedStroke"]);
        Assert.Equal("#FF0000", text.Metadata["DimensionTextResolvedStroke"]);
    }

    [Fact]
    public void LegacyShxCjkFallbackUsesPrintOrientedCadMetrics()
    {
        var resolution = CadFontResolver.Resolve("hztxt.shx", "北京意铭创设咨询有限公司");

        Assert.Equal(CadFontKind.Shx, resolution.Kind);
        Assert.Equal("SimSun", resolution.Family);
        Assert.True(resolution.UsesFallback);
    }

    private static CadDimensionEntity LinearDimension() => new(
        "DIM-V0123",
        CadDimensionKind.Linear,
        new Point2D(100, 20),
        new Point2D(50, 23),
        "2250",
        100,
        0,
        2.5,
        2.5,
        "ARCH",
        new Dictionary<string, Point2D>
        {
            ["FirstPoint"] = new(0, 0),
            ["SecondPoint"] = new(100, 0)
        });

    private static CadDocument Document(CadEntity entity) => new(
        "real-drawing-v0123.dwg",
        "DWG",
        "AC1032",
        CadUnits.Millimetres,
        new[] { new CadLayer("0", CadColor.FromAci(2)) },
        Array.Empty<CadBlockDefinition>(),
        new[] { entity });
}
