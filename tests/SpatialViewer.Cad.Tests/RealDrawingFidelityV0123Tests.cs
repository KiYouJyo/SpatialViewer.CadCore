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
