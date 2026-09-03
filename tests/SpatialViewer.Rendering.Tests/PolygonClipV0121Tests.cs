using SpatialViewer.Core;
using SpatialViewer.Rendering;

namespace SpatialViewer.Rendering.Tests;

public sealed class PolygonClipV0121Tests
{
    [Fact]
    public void RenderPreparationCarriesWorldPolygonClipStackWithoutFlatteningIt()
    {
        var child = new SceneNode(
            ObjectId.New(),
            new RectangleGeometry(new BoundingBox2D(-20, -20, 20, 20)),
            Transform2D.Rotation(Math.PI / 2))
        {
            LocalClipPolygon = new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(0, 10)
            }
        };
        var parent = new SceneNode(
            ObjectId.New(),
            transform: Transform2D.Translation(100, 50),
            children: new[] { child },
            clipBounds: new BoundingBox2D(80, 40, 110, 80))
        {
            LocalClipPolygon = new[]
            {
                new Point2D(-20, -20),
                new Point2D(20, -20),
                new Point2D(20, 20),
                new Point2D(-20, 20)
            }
        };
        var scene = new Scene2D(new[] { new SceneLayer(new Layer("clip", "Clip"), new[] { parent }) });
        var camera = new Camera2D(new Point2D(100, 50));

        var command = Assert.Single(RenderPreparation.Prepare(scene, camera).Commands);

        Assert.Equal(new BoundingBox2D(80, 40, 110, 80), command.ClipBounds);
        Assert.Equal(2, command.ClipPolygons.Count);
        Assert.Equal(4, command.ClipPolygons[0].Count);
        Assert.Equal(3, command.ClipPolygons[1].Count);
        Assert.Contains(command.ClipPolygons[1], point => Math.Abs(point.X - 90) < 1e-12 && Math.Abs(point.Y - 50) < 1e-12);
        Assert.Contains(command.ClipPolygons[1], point => Math.Abs(point.X - 100) < 1e-12 && Math.Abs(point.Y - 50) < 1e-12);
        Assert.Contains(command.ClipPolygons[1], point => Math.Abs(point.X - 100) < 1e-12 && Math.Abs(point.Y - 60) < 1e-12);
    }
}
