using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class DimensionAxisFidelityV0122Tests
{
    [Theory]
    [InlineData("_Oblique")]
    [InlineData("ARCHTICK")]
    [InlineData("_ArchTick")]
    public void ArchitecturalTickUsesOneObliqueStrokePerDimensionEnd(string arrowBlock)
    {
        var dimension = Dimension(0) with
        {
            Presentation = new CadDimensionPresentation(ArrowBlockName: arrowBlock)
        };

        var ticks = ArrowLines(Document(dimension), arrowBlock);

        Assert.Equal(2, ticks.Length);
        Assert.All(ticks, item =>
        {
            Assert.Equal("ArchitecturalTick", item.Metadata["DimensionArrowResolvedKind"]);
            Assert.Equal("False", item.Metadata["DimensionArrowFallbackApplied"]);
            var line = Assert.IsType<LineGeometry>(item.Geometry);
            var dx = Math.Abs(line.End.X - line.Start.X);
            var dy = Math.Abs(line.End.Y - line.Start.Y);
            Assert.True(dx > 0);
            Assert.InRange(dy / dx, 0.999999, 1.000001);
        });
    }

    [Fact]
    public void UnknownCustomArrowKeepsConservativeGenericFallback()
    {
        const string custom = "MY_CUSTOM_ARROW";
        var dimension = Dimension(0) with
        {
            Presentation = new CadDimensionPresentation(ArrowBlockName: custom)
        };

        var arrows = ArrowLines(Document(dimension), custom);

        Assert.Equal(4, arrows.Length);
        Assert.All(arrows, item => Assert.False(item.Metadata.ContainsKey("DimensionArrowResolvedKind")));
    }

    [Fact]
    public void SeparateArrowBlocksCanMixArchitecturalTickAndFallback()
    {
        var dimension = Dimension(0) with
        {
            Presentation = new CadDimensionPresentation(
                FirstArrowBlockName: "_Oblique",
                SecondArrowBlockName: "CUSTOM_SECOND",
                SeparateArrowBlocks: true)
        };

        var document = Document(dimension);
        Assert.Single(ArrowLines(document, "_Oblique"));
        Assert.Equal(2, ArrowLines(document, "CUSTOM_SECOND").Length);
    }

    [Theory]
    [InlineData(3.141592653589793, 0)]
    [InlineData(4.71238898038469, -1.5707963267948966)]
    [InlineData(-3.141592653589793, 0)]
    [InlineData(1.5707963267948966, 1.5707963267948966)]
    public void DimensionTextRotationIsNormalizedToReadableHalfPlane(double sourceRotation, double expectedRotation)
    {
        var dimension = Dimension(sourceRotation);

        var text = Document(dimension).Scene.GetItems()
            .Single(item => item.Id == dimension.ObjectId && item.Geometry is TextGeometry);
        var actualRotation = Math.Atan2(text.Transform.M12, text.Transform.M11);

        Assert.InRange(actualRotation, expectedRotation - 1e-9, expectedRotation + 1e-9);
        if (Math.Abs(sourceRotation - expectedRotation) > 1e-9)
        {
            Assert.Equal("True", text.Metadata["DimensionTextReadableRotationApplied"]);
            Assert.True(text.Metadata.ContainsKey("DimensionTextSourceRotation"));
            Assert.True(text.Metadata.ContainsKey("DimensionTextResolvedRotation"));
        }
    }

    private static SceneItem[] ArrowLines(CadDocument document, string requested)
        => document.Scene.GetItems()
            .Where(item => item.Geometry is LineGeometry
                && item.Metadata.TryGetValue("DimensionArrowRequestedBlock", out var block)
                && string.Equals(block, requested, StringComparison.Ordinal))
            .ToArray();

    private static CadDimensionEntity Dimension(double rotation) => new(
        "DIM-V0122",
        CadDimensionKind.Linear,
        new Point2D(100, 20),
        new Point2D(50, 23),
        "2250",
        100,
        rotation,
        2.5,
        2.5,
        "ARCH",
        new Dictionary<string, Point2D>
        {
            ["FirstPoint"] = new(0, 0),
            ["SecondPoint"] = new(100, 0)
        });

    private static CadDocument Document(CadEntity entity) => new(
        "axis-dimension-v0122.dxf",
        "DXF",
        "AC1032",
        CadUnits.Millimetres,
        new[] { new CadLayer("0", CadColor.FromAci(2)) },
        Array.Empty<CadBlockDefinition>(),
        new[] { entity });
}
