using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class RotationPivotV0121Tests
{
    [Fact]
    public void GenericTextRotationKeepsInsertionPointFixed()
    {
        var text = new CadTextEntity(
            "TXT-PIVOT",
            new Point2D(10, 20),
            "A1",
            2,
            Math.PI / 2);
        var document = Document(text);

        var item = Assert.Single(document.Scene.GetItems(), candidate => candidate.Id == text.ObjectId && candidate.Geometry is TextGeometry);
        var geometry = Assert.IsType<TextGeometry>(item.Geometry);
        var pivot = item.Transform.Apply(geometry.Origin);
        var xAxis = item.Transform.Apply(new Point2D(geometry.Origin.X + 1, geometry.Origin.Y));

        Assert.Equal(10, pivot.X, 12);
        Assert.Equal(20, pivot.Y, 12);
        Assert.Equal(10, xAxis.X, 12);
        Assert.Equal(21, xAxis.Y, 12);
    }

    [Fact]
    public void DimensionTextRotationKeepsTextPositionFixed()
    {
        var dimension = new CadDimensionEntity(
            "DIM-PIVOT",
            CadDimensionKind.Unknown,
            new Point2D(0, 0),
            new Point2D(50, 60),
            "1000",
            1000,
            Math.PI / 2,
            2.5,
            2,
            "Standard",
            new Dictionary<string, Point2D>());
        var document = Document(dimension);

        var item = Assert.Single(document.Scene.GetItems(), candidate => candidate.Id == dimension.ObjectId && candidate.Geometry is TextGeometry);
        var geometry = Assert.IsType<TextGeometry>(item.Geometry);
        var pivot = item.Transform.Apply(geometry.Origin);
        var xAxis = item.Transform.Apply(new Point2D(geometry.Origin.X + 1, geometry.Origin.Y));

        Assert.Equal(50, pivot.X, 12);
        Assert.Equal(60, pivot.Y, 12);
        Assert.Equal(50, xAxis.X, 12);
        Assert.Equal(61, xAxis.Y, 12);
    }

    [Fact]
    public void RotatedEllipseKeepsCenterFixed()
    {
        var ellipse = new CadEllipseEntity(
            "ELLIPSE-PIVOT",
            new Point2D(30, 40),
            10,
            5,
            Math.PI / 3);
        var document = Document(ellipse);

        var item = Assert.Single(document.Scene.GetItems(), candidate => candidate.Id == ellipse.ObjectId);
        Assert.IsType<EllipseGeometry>(item.Geometry);
        var center = item.Transform.Apply(ellipse.Center);
        var xAxis = item.Transform.Apply(new Point2D(ellipse.Center.X + 1, ellipse.Center.Y));

        Assert.Equal(30, center.X, 12);
        Assert.Equal(40, center.Y, 12);
        Assert.Equal(30.5, xAxis.X, 12);
        Assert.Equal(40 + (Math.Sqrt(3) / 2), xAxis.Y, 12);
    }

    private static CadDocument Document(CadEntity entity)
        => new(
            "rotation-pivot.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new[] { entity });
}
