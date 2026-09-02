using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class DimStyleV0100Tests
{
    [Fact]
    public void LinearDimensionAppliesExtensionOffsetExtensionAndSuppression()
    {
        var dimension = Dimension() with
        {
            Presentation = new CadDimensionPresentation(
                ExtensionLineOffset: 2,
                ExtensionLineExtension: 3,
                SuppressSecondExtensionLine: true)
        };

        var lines = Document(dimension).Scene.GetItems().Where(item => item.Geometry is LineGeometry).Select(item => (LineGeometry)item.Geometry).ToArray();

        Assert.Contains(lines, line => SameLine(line, new Point2D(0, 2), new Point2D(0, 23)));
        Assert.DoesNotContain(lines, line => SameLine(line, new Point2D(100, 2), new Point2D(100, 23)));
        Assert.Contains(lines, line => SameLine(line, new Point2D(0, 20), new Point2D(100, 20)));
    }

    [Fact]
    public void DimensionLineSuppressionSplitsAtTextProjectionAndHonorsLineExtension()
    {
        var dimension = Dimension() with
        {
            Presentation = new CadDimensionPresentation(
                DimensionLineExtension: 1,
                SuppressFirstDimensionLine: true)
        };

        var lines = Document(dimension).Scene.GetItems().Where(item => item.Geometry is LineGeometry).Select(item => (LineGeometry)item.Geometry).ToArray();

        Assert.Contains(lines, line => SameLine(line, new Point2D(50, 20), new Point2D(101, 20)));
        Assert.DoesNotContain(lines, line => SameLine(line, new Point2D(-1, 20), new Point2D(101, 20)));
    }

    [Fact]
    public void DimensionStyleFormattingAndCustomArrowIdentityReachSceneMetadata()
    {
        var dimension = Dimension() with
        {
            Presentation = new CadDimensionPresentation(
                DimensionLineGap: 0.75,
                ArrowBlockName: "ARCHTICK",
                DecimalPlaces: 3,
                DecimalSeparator: ',',
                Rounding: 0.25,
                Prefix: "~",
                Suffix: " mm",
                GenerateTolerances: true,
                PlusTolerance: 0.2,
                MinusTolerance: 0.1,
                ToleranceDecimalPlaces: 2,
                ToleranceScaleFactor: 0.75,
                AlternateUnitsEnabled: true,
                AlternateUnitScaleFactor: 0.0393700787,
                AlternateUnitDecimalPlaces: 2,
                AlternateUnitSuffix: " [in]",
                LinearUnitFormat: "Decimal")
        };

        var items = Document(dimension).Scene.GetItems().Where(item => item.Id == dimension.ObjectId).ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.Equal("0.75", item.Metadata["DimensionLineGap"]);
            Assert.Equal("ARCHTICK", item.Metadata["DimensionArrowBlock"]);
            Assert.Equal("3", item.Metadata["DimensionDecimalPlaces"]);
            Assert.Equal(",", item.Metadata["DimensionDecimalSeparator"]);
            Assert.Equal("0.25", item.Metadata["DimensionRounding"]);
            Assert.Equal("True", item.Metadata["DimensionTolerances"]);
            Assert.Equal("True", item.Metadata["DimensionAlternateUnits"]);
            Assert.Equal("True", item.Metadata["DimensionCustomArrowRequested"]);
            Assert.Equal("True", item.Metadata["DimensionCustomArrowFallbackApplied"]);
        });
        Assert.Contains(items, item => item.Metadata.TryGetValue("DimensionArrowRequestedBlock", out var block) && block == "ARCHTICK");
    }

    [Fact]
    public void BothDimensionLineSegmentsCanBeSuppressedWithoutSuppressingExtensionLines()
    {
        var dimension = Dimension() with
        {
            Presentation = new CadDimensionPresentation(
                SuppressFirstDimensionLine: true,
                SuppressSecondDimensionLine: true)
        };

        var lines = Document(dimension).Scene.GetItems().Where(item => item.Geometry is LineGeometry).Select(item => (LineGeometry)item.Geometry).ToArray();
        Assert.Contains(lines, line => SameLine(line, new Point2D(0, 0), new Point2D(0, 20)));
        Assert.Contains(lines, line => SameLine(line, new Point2D(100, 0), new Point2D(100, 20)));
        Assert.DoesNotContain(lines, line => SameLine(line, new Point2D(0, 20), new Point2D(100, 20)));
    }

    private static CadDimensionEntity Dimension() => new(
        "DIM100",
        CadDimensionKind.Linear,
        new Point2D(100, 20),
        new Point2D(50, 23),
        "100.00",
        100,
        0,
        2.5,
        2.5,
        "STANDARD",
        new Dictionary<string, Point2D>
        {
            ["FirstPoint"] = new(0, 0),
            ["SecondPoint"] = new(100, 0)
        });

    private static bool SameLine(LineGeometry line, Point2D first, Point2D second)
        => (line.Start.DistanceTo(first) < 1e-8 && line.End.DistanceTo(second) < 1e-8)
            || (line.Start.DistanceTo(second) < 1e-8 && line.End.DistanceTo(first) < 1e-8);

    private static CadDocument Document(CadEntity entity) => new(
        "dimstyle-v0100.dxf",
        "DXF",
        "AC1032",
        CadUnits.Unitless,
        new[] { new CadLayer("0", CadColor.FromAci(7)) },
        Array.Empty<CadBlockDefinition>(),
        new[] { entity });
}
