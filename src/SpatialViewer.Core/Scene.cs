using System.Collections.ObjectModel;

namespace SpatialViewer.Core;

public readonly record struct ObjectId(Guid Value)
{
    public static ObjectId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public sealed class Layer
{
    public Layer(string id, string name, int order = 0, bool isVisible = true, bool isLocked = false, IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id; Name = name; Order = order; IsVisible = isVisible; IsLocked = isLocked;
        Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    }
    public string Id { get; }
    public string Name { get; }
    public int Order { get; }
    public bool IsVisible { get; set; }
    public bool IsLocked { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public sealed record SceneStyle(string Stroke = "#202020", double StrokeWidth = 1, string? Fill = null, double Opacity = 1)
{
    public static SceneStyle Default { get; } = new();
}

public abstract record Geometry2D
{
    public abstract BoundingBox2D GetBounds();
}
public sealed record PointGeometry(Point2D Position) : Geometry2D { public override BoundingBox2D GetBounds() => BoundingBox2D.Empty.Include(Position); }
public sealed record LineGeometry(Point2D Start, Point2D End) : Geometry2D { public override BoundingBox2D GetBounds() => BoundingBox2D.Empty.Include(Start).Include(End); }
public sealed record PolylineGeometry(IReadOnlyList<Point2D> Points, bool IsClosed = false) : Geometry2D { public override BoundingBox2D GetBounds() => BoundingBox2D.FromPoints(Points); }
public sealed record PolygonGeometry(IReadOnlyList<Point2D> Points) : Geometry2D { public override BoundingBox2D GetBounds() => BoundingBox2D.FromPoints(Points); }
public sealed record RectangleGeometry(BoundingBox2D Rectangle) : Geometry2D { public override BoundingBox2D GetBounds() => Rectangle; }
public sealed record CircleGeometry(Point2D Center, double Radius) : Geometry2D { public override BoundingBox2D GetBounds() => new(Center.X - Radius, Center.Y - Radius, Center.X + Radius, Center.Y + Radius); }
public sealed record ArcGeometry(Point2D Center, double Radius, double StartRadians, double SweepRadians) : Geometry2D
{
    public override BoundingBox2D GetBounds()
    {
        var points = new List<Point2D> { At(StartRadians), At(StartRadians + SweepRadians) };
        foreach (var angle in new[] { 0d, Math.PI / 2, Math.PI, Math.PI * 1.5 }) if (ContainsAngle(angle)) points.Add(At(angle));
        return BoundingBox2D.FromPoints(points);
    }
    private Point2D At(double angle) => new(Center.X + (Math.Cos(angle) * Radius), Center.Y + (Math.Sin(angle) * Radius));
    private bool ContainsAngle(double angle)
    {
        var sweep = SweepRadians; var start = StartRadians;
        if (sweep < 0) { start += sweep; sweep = -sweep; }
        if (sweep >= Math.PI * 2) return true;
        var normalized = ((angle - start) % (Math.PI * 2) + (Math.PI * 2)) % (Math.PI * 2);
        return normalized <= sweep;
    }
}
public sealed record EllipseGeometry(Point2D Center, double RadiusX, double RadiusY) : Geometry2D { public override BoundingBox2D GetBounds() => new(Center.X - RadiusX, Center.Y - RadiusY, Center.X + RadiusX, Center.Y + RadiusY); }
public sealed record PathGeometry(IReadOnlyList<Point2D> Points, bool IsClosed = false) : Geometry2D { public override BoundingBox2D GetBounds() => BoundingBox2D.FromPoints(Points); }
public sealed record TextGeometry(Point2D Origin, string Text, double Height = 10) : Geometry2D { public override BoundingBox2D GetBounds() => new(Origin.X, Origin.Y - Height, Origin.X + (Math.Max(1, Text.Length) * Height * .6), Origin.Y); }
public sealed record ImageGeometry(Point2D Origin, Size2D Size, string Source) : Geometry2D { public override BoundingBox2D GetBounds() => new(Origin.X, Origin.Y, Origin.X + Size.Width, Origin.Y + Size.Height); }

public sealed class SceneNode
{
    public SceneNode(ObjectId id, Geometry2D? geometry = null, Transform2D? transform = null, SceneStyle? style = null, IReadOnlyList<SceneNode>? children = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = id; Geometry = geometry; Transform = transform ?? Transform2D.Identity; Style = style ?? SceneStyle.Default;
        Children = children ?? Array.Empty<SceneNode>(); Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    }
    public ObjectId Id { get; }
    public Geometry2D? Geometry { get; }
    public Transform2D Transform { get; }
    public SceneStyle Style { get; }
    public IReadOnlyList<SceneNode> Children { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public BoundingBox2D GetBounds() => GetBounds(Transform2D.Identity);
    internal BoundingBox2D GetBounds(Transform2D parent) { var world = Transform.Then(parent); var bounds = Geometry?.GetBounds().Transform(world) ?? BoundingBox2D.Empty; foreach (var child in Children) bounds = bounds.Union(child.GetBounds(world)); return bounds; }
}

public sealed class SceneLayer
{
    public SceneLayer(Layer layer, IReadOnlyList<SceneNode> nodes) { Layer = layer; Nodes = nodes; }
    public Layer Layer { get; }
    public IReadOnlyList<SceneNode> Nodes { get; }
}

public readonly record struct SceneItem(ObjectId Id, Geometry2D Geometry, Transform2D Transform, SceneStyle Style, Layer Layer, BoundingBox2D Bounds, IReadOnlyDictionary<string, string> Metadata);

public sealed class Scene2D
{
    public Scene2D(IReadOnlyList<SceneLayer> layers) { Layers = layers.OrderBy(layer => layer.Layer.Order).ToArray(); }
    public IReadOnlyList<SceneLayer> Layers { get; }
    public IEnumerable<SceneItem> GetItems(bool visibleOnly = true)
    {
        foreach (var layer in Layers) { if (visibleOnly && !layer.Layer.IsVisible) continue; foreach (var node in layer.Nodes) foreach (var item in Flatten(node, Transform2D.Identity, layer.Layer)) yield return item; }
    }
    public BoundingBox2D GetBounds(bool visibleOnly = true) { var bounds = BoundingBox2D.Empty; foreach (var item in GetItems(visibleOnly)) bounds = bounds.Union(item.Bounds); return bounds; }
    private static IEnumerable<SceneItem> Flatten(SceneNode node, Transform2D parent, Layer layer)
    { var transform = node.Transform.Then(parent); if (node.Geometry is { } geometry) yield return new(node.Id, geometry, transform, node.Style, layer, geometry.GetBounds().Transform(transform), node.Metadata); foreach (var child in node.Children) foreach (var item in Flatten(child, transform, layer)) yield return item; }
}
