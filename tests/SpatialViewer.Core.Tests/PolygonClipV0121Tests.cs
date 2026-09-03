using SpatialViewer.Core;

namespace SpatialViewer.Core.Tests;

public sealed class PolygonClipV0121Tests
{
    [Fact]
    public void LocalPolygonClipFollowsNodeTransformAndRestrictsHitTesting()
    {
        var id = ObjectId.New();
        var transform = Transform2D.Rotation(Math.PI / 4).Then(Transform2D.Translation(100, 50));
        var localClip = new[]
        {
            new Point2D(0, 0),
            new Point2D(10, 0),
            new Point2D(0, 10)
        };
        var node = new SceneNode(
            id,
            new RectangleGeometry(new BoundingBox2D(-100, -100, 100, 100)),
            transform)
        {
            LocalClipPolygon = localClip
        };
        var scene = new Scene2D(new[] { new SceneLayer(new Layer("clip", "Clip"), new[] { node }) });

        var item = Assert.Single(scene.GetItems());
        var worldClip = Assert.Single(item.ClipPolygons);
        var expected = localClip.Select(transform.Apply).ToArray();
        Assert.Equal(expected.Length, worldClip.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].X, worldClip[index].X, 12);
            Assert.Equal(expected[index].Y, worldClip[index].Y, 12);
        }

        var expectedBounds = BoundingBox2D.FromPoints(expected);
        Assert.Equal(expectedBounds.MinX, item.Bounds.MinX, 12);
        Assert.Equal(expectedBounds.MinY, item.Bounds.MinY, 12);
        Assert.Equal(expectedBounds.MaxX, item.Bounds.MaxX, 12);
        Assert.Equal(expectedBounds.MaxY, item.Bounds.MaxY, 12);

        var inside = transform.Apply(new Point2D(2, 2));
        var outsidePolygonButInsideBounds = transform.Apply(new Point2D(8, 8));
        Assert.Equal(id, HitTesting.HitTest(scene, inside, 0)?.Id);
        Assert.Null(HitTesting.HitTest(scene, outsidePolygonButInsideBounds, 0));
    }

    [Fact]
    public void NestedPolygonClipsRemainIndependentIntersectionStack()
    {
        var id = ObjectId.New();
        var child = new SceneNode(
            id,
            new RectangleGeometry(new BoundingBox2D(-100, -100, 100, 100)),
            Transform2D.Translation(5, 0))
        {
            LocalClipPolygon = new[]
            {
                new Point2D(0, -5),
                new Point2D(10, -5),
                new Point2D(0, 5)
            }
        };
        var parent = new SceneNode(
            ObjectId.New(),
            transform: Transform2D.Translation(100, 50),
            children: new[] { child })
        {
            LocalClipPolygon = new[]
            {
                new Point2D(0, -10),
                new Point2D(20, -10),
                new Point2D(20, 10),
                new Point2D(0, 10)
            }
        };
        var scene = new Scene2D(new[] { new SceneLayer(new Layer("clip", "Clip"), new[] { parent }) });

        var item = Assert.Single(scene.GetItems());
        Assert.Equal(2, item.ClipPolygons.Count);
        Assert.Equal(id, HitTesting.HitTest(scene, new Point2D(108, 50), 0)?.Id);
        Assert.Null(HitTesting.HitTest(scene, new Point2D(118, 54), 0));
    }

    [Fact]
    public void MalformedLocalPolygonClipFailsClosed()
    {
        var node = new SceneNode(
            ObjectId.New(),
            new RectangleGeometry(new BoundingBox2D(0, 0, 10, 10)))
        {
            LocalClipPolygon = new[] { new Point2D(0, 0), new Point2D(10, 0) }
        };
        var scene = new Scene2D(new[] { new SceneLayer(new Layer("clip", "Clip"), new[] { node }) });

        Assert.Empty(scene.GetItems());
        Assert.True(scene.GetBounds().IsEmpty);
    }
}
