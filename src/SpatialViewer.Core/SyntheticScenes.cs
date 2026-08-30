namespace SpatialViewer.Core;

/// <summary>Deterministic scenes used by the Debug Host, automated tests, and performance baselines.</summary>
public static class SyntheticScenes
{
    public static SyntheticDocument BasicPrimitives()
    {
        var drafting = new Layer("drafting", "Drafting", 0); var annotation = new Layer("annotation", "Annotation", 1);
        var nodes = new SceneNode[]
        {
            Node(new LineGeometry(new(0, 0), new(100, 0)), "#1976D2"), Node(new PolylineGeometry(new[] { new Point2D(0, 20), new(30, 55), new(90, 25) }), "#00897B"),
            Node(new PolygonGeometry(new[] { new Point2D(10, 80), new(55, 120), new(90, 75) }), "#6A1B9A", "#E1BEE7"), Node(new CircleGeometry(new(160, 40), 28), "#D84315"),
            Node(new ArcGeometry(new(160, 105), 30, 0, Math.PI * 1.25), "#5D4037"), Node(new EllipseGeometry(new(240, 50), 45, 22), "#3949AB"),
            Node(new RectangleGeometry(new(210, 90, 285, 140)), "#455A64", "#CFD8DC"), Node(new PathGeometry(new[] { new Point2D(310, 30), new(345, 65), new(320, 95), new(365, 135) }), "#C2185B")
        };
        var transformed = new SceneNode(ObjectId.New(), null, Transform2D.Translation(320, 20).Then(Transform2D.Rotation(.35)), children: new[] { Node(new CircleGeometry(Point2D.Origin, 22), "#FF8F00", "#FFE0B2"), Node(new LineGeometry(new(-30, 0), new(30, 0)), "#FF8F00") });
        return new SyntheticDocument("Basic primitives", new Scene2D(new[] { new SceneLayer(drafting, nodes.Append(transformed).ToArray()), new SceneLayer(annotation, new[] { Node(new TextGeometry(new(12, 165), "Spatial Viewer · Scene A", 14), "#202020") }) }));
    }
    public static SyntheticDocument NestedTransforms()
    {
        var layer = new Layer("transforms", "Nested transforms");
        var leaf = Node(new RectangleGeometry(new(-20, -10, 20, 10)), "#1565C0", "#BBDEFB");
        var middle = new SceneNode(ObjectId.New(), null, Transform2D.Scale(2, 1.5).Then(Transform2D.Rotation(Math.PI / 6)), children: new[] { leaf });
        var root = new SceneNode(ObjectId.New(), null, Transform2D.Translation(120, 75), children: new[] { middle });
        return new SyntheticDocument("Nested transforms", new Scene2D(new[] { new SceneLayer(layer, new[] { root }) }));
    }
    public static SyntheticDocument LargeCoordinates()
    {
        var layer = new Layer("survey", "Survey", 0); const double x = 500000, y = 3400000;
        var nodes = new[] { Node(new RectangleGeometry(new(x, y, x + 1200, y + 700)), "#00695C"), Node(new CircleGeometry(new(x + 600, y + 350), 175), "#D84315"), Node(new PolylineGeometry(new[] { new Point2D(x, y), new(x + 1200, y + 700) }), "#1565C0"), Node(new TextGeometry(new(x + 50, y + 650), "500000 / 3400000", 35), "#202020") };
        return new SyntheticDocument("Large coordinates", new Scene2D(new[] { new SceneLayer(layer, nodes) }));
    }
    public static SyntheticDocument Stress(int primitiveCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(primitiveCount);
        var layer = new Layer("stress", $"Stress {primitiveCount:N0}"); var nodes = new SceneNode[primitiveCount];
        for (var i = 0; i < primitiveCount; i++) { var x = (i % 1000) * 10d; var y = (i / 1000) * 10d; nodes[i] = Node(new LineGeometry(new(x, y), new(x + 8, y + 8)), i % 3 == 0 ? "#1E88E5" : "#546E7A"); }
        return new SyntheticDocument($"Stress {primitiveCount:N0}", new Scene2D(new[] { new SceneLayer(layer, nodes) }));
    }
    private static SceneNode Node(Geometry2D geometry, string stroke, string? fill = null) => new(ObjectId.New(), geometry, style: new SceneStyle(stroke, 1, fill));
}
