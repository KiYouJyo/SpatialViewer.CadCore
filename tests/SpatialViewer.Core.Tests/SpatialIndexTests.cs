using SpatialViewer.Core;

namespace SpatialViewer.Core.Tests;

public sealed class SpatialIndexTests
{
    [Fact]
    public void QueryItemsPreservesSceneOrderAndReducesCandidates()
    {
        var layer = new Layer("grid", "Grid");
        var nodes = Enumerable.Range(0, 4096)
            .Select(index =>
            {
                var x = index % 64;
                var y = index / 64;
                return new SceneNode(ObjectId.New(), new LineGeometry(new(x, y), new(x + .5, y + .5)));
            })
            .ToArray();
        var scene = new Scene2D(new[] { new SceneLayer(layer, nodes) });

        var query = new BoundingBox2D(20, 20, 24, 24);
        var candidates = scene.QueryItems(query).ToArray();
        var expected = scene.GetItems().Where(item => item.Bounds.Intersects(query)).ToArray();

        Assert.Equal(expected.Select(item => item.Id), candidates.Select(item => item.Id));
        Assert.True(candidates.Length < nodes.Length / 16);
        var statistics = scene.GetSpatialIndexStatistics();
        Assert.Equal(nodes.Length, statistics.ItemCount);
        Assert.Equal(nodes.Length, statistics.IndexedItemCount);
        Assert.Equal(0, statistics.FallbackItemCount);
        Assert.True(statistics.NodeCount > 1);
    }

    [Fact]
    public void QueryItemsStillHonorsLiveLayerVisibility()
    {
        var visible = new Layer("visible", "Visible");
        var hidden = new Layer("hidden", "Hidden");
        var query = new BoundingBox2D(-1, -1, 2, 2);
        var first = new SceneNode(ObjectId.New(), new LineGeometry(new(0, 0), new(1, 0)));
        var second = new SceneNode(ObjectId.New(), new LineGeometry(new(0, 1), new(1, 1)));
        var scene = new Scene2D(new[] { new SceneLayer(visible, new[] { first }), new SceneLayer(hidden, new[] { second }) });

        Assert.Equal(2, scene.QueryItems(query).Count());
        hidden.IsVisible = false;
        Assert.Single(scene.QueryItems(query));
        Assert.Equal(2, scene.QueryItems(query, visibleOnly: false).Count());
    }

    [Fact]
    public void IndexedHitTestPreservesTopmostScenePriority()
    {
        var layer = new Layer("overlap", "Overlap");
        var bottom = new SceneNode(ObjectId.New(), new RectangleGeometry(new(-10, -10, 10, 10)));
        var top = new SceneNode(ObjectId.New(), new RectangleGeometry(new(-5, -5, 5, 5)));
        var scene = new Scene2D(new[] { new SceneLayer(layer, new[] { bottom, top }) });

        Assert.Equal(top.Id, HitTesting.HitTest(scene, Point2D.Origin, 0)?.Id);
    }

    [Fact]
    public void NonFiniteBoundsRemainConservativeFallbackCandidates()
    {
        var layer = new Layer("fallback", "Fallback");
        var id = ObjectId.New();
        var node = new SceneNode(id, new LineGeometry(new(double.NegativeInfinity, 0), new(double.PositiveInfinity, 0)));
        var scene = new Scene2D(new[] { new SceneLayer(layer, new[] { node }) });

        var statistics = scene.GetSpatialIndexStatistics();
        Assert.Equal(0, statistics.IndexedItemCount);
        Assert.Equal(1, statistics.FallbackItemCount);
        Assert.Contains(scene.QueryItems(new BoundingBox2D(100, 100, 101, 101), visibleOnly: false), item => item.Id == id);
    }

    [Fact]
    public void ClipAdjustedBoundsDriveSpatialQueries()
    {
        var layer = new Layer("clip", "Clip");
        var node = new SceneNode(
            ObjectId.New(),
            new LineGeometry(new(-100, 0), new(100, 0)),
            clipBounds: new BoundingBox2D(-10, -10, 10, 10));
        var scene = new Scene2D(new[] { new SceneLayer(layer, new[] { node }) });

        Assert.Empty(scene.QueryItems(new BoundingBox2D(50, -1, 60, 1)));
        Assert.Single(scene.QueryItems(new BoundingBox2D(0, -1, 1, 1)));
    }
}
