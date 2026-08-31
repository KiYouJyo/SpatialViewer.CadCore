using SpatialViewer.Core;

namespace SpatialViewer.Core.Tests;

public sealed class CompoundPathTests
{
    [Fact]
    public void CompoundPathHitTestingUsesEvenOddHoleParity()
    {
        var outer = new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10) };
        var hole = new[] { new Point2D(3, 3), new Point2D(7, 3), new Point2D(7, 7), new Point2D(3, 7) };
        var node = new SceneNode(ObjectId.New(), new CompoundPathGeometry(new IReadOnlyList<Point2D>[] { outer, hole }), style: new SceneStyle(Fill: "#202020"));
        var scene = new Scene2D(new[] { new SceneLayer(new Layer("hatch", "Hatch"), new[] { node }) });

        Assert.NotNull(HitTesting.HitTest(scene, new(1, 1), .05));
        Assert.Null(HitTesting.HitTest(scene, new(5, 5), .05));
        Assert.NotNull(HitTesting.HitTest(scene, new(3, 5), .05));
    }
}
