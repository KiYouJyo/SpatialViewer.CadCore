using SpatialViewer.Core;
using SpatialViewer.Rendering;

namespace SpatialViewer.Rendering.Tests;

public sealed class SpatialCullingTests
{
    [Fact]
    public void ViewportPreparationCullsOffscreenItemsAndPreservesDrawOrder()
    {
        var layer = new Layer("lines", "Lines");
        var nodes = Enumerable.Range(0, 100)
            .Select(index => new SceneNode(ObjectId.New(), new LineGeometry(new(index, 0), new(index + .5, 0))))
            .ToArray();
        var scene = new Scene2D(new[] { new SceneLayer(layer, nodes) });
        var camera = new Camera2D(new(50, 0), 10);
        var viewport = new Size2D(100, 100);
        var worldBounds = camera.GetVisibleWorldBounds(viewport);

        var frame = RenderPreparation.Prepare(scene, camera, viewport);
        var expected = scene.GetItems().Where(item => item.Bounds.Intersects(worldBounds)).Select(item => item.Id).ToArray();

        Assert.Equal(expected, frame.Commands.Select(command => command.ObjectId));
        Assert.True(frame.Commands.Count < nodes.Length / 4);
        Assert.Equal(camera.Target, frame.LocalOrigin);
    }

    [Fact]
    public void ViewportPreparationHonorsOverscanInScreenPixels()
    {
        var layer = new Layer("overscan", "Overscan");
        var justOutside = new SceneNode(ObjectId.New(), new PointGeometry(new(5.5, 0)));
        var scene = new Scene2D(new[] { new SceneLayer(layer, new[] { justOutside }) });
        var camera = new Camera2D(Point2D.Origin, 10);
        var viewport = new Size2D(100, 100);

        Assert.Empty(RenderPreparation.Prepare(scene, camera, viewport).Commands);
        Assert.Single(RenderPreparation.Prepare(scene, camera, viewport, overscanPixels: 6).Commands);
    }

    [Fact]
    public void EmptyViewportProducesNoCommands()
    {
        var document = SyntheticScenes.BasicPrimitives();
        var camera = new Camera2D(document.Bounds.Center);

        Assert.Empty(RenderPreparation.Prepare(document.Scene, camera, new Size2D(0, 100)).Commands);
    }
}
