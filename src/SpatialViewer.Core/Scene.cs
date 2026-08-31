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
/// <summary>Multiple closed loops interpreted with an even-odd fill rule. This preserves holes without forcing CAD hatches into independent filled polygons.</summary>
public sealed record CompoundPathGeometry(IReadOnlyList<IReadOnlyList<Point2D>> Loops) : Geometry2D
{
    public override BoundingBox2D GetBounds()
    {
        var bounds = BoundingBox2D.Empty;
        foreach (var loop in Loops) bounds = bounds.Union(BoundingBox2D.FromPoints(loop));
        return bounds;
    }
}
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
    internal Point2D At(double angle) => new(Center.X + (Math.Cos(angle) * Radius), Center.Y + (Math.Sin(angle) * Radius));
    internal bool ContainsAngle(double angle)
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
    public SceneNode(ObjectId id, Geometry2D? geometry = null, Transform2D? transform = null, SceneStyle? style = null, IReadOnlyList<SceneNode>? children = null, IReadOnlyDictionary<string, string>? metadata = null, BoundingBox2D? clipBounds = null)
    {
        Id = id; Geometry = geometry; Transform = transform ?? Transform2D.Identity; Style = style ?? SceneStyle.Default;
        Children = children ?? Array.Empty<SceneNode>(); Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        ClipBounds = clipBounds;
    }
    public ObjectId Id { get; }
    public Geometry2D? Geometry { get; }
    public Transform2D Transform { get; }
    public SceneStyle Style { get; }
    public IReadOnlyList<SceneNode> Children { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    /// <summary>Optional axis-aligned clip rectangle in final scene/world coordinates. Parent clip rectangles are inherited by children.</summary>
    public BoundingBox2D? ClipBounds { get; }
    public BoundingBox2D GetBounds() => GetBounds(Transform2D.Identity, null);
    internal BoundingBox2D GetBounds(Transform2D parent, BoundingBox2D? parentClip)
    {
        var world = Transform.Then(parent);
        var clip = IntersectClips(parentClip, ClipBounds);
        var bounds = Geometry?.GetBounds().Transform(world) ?? BoundingBox2D.Empty;
        if (clip is { } rectangle) bounds = bounds.Intersection(rectangle);
        foreach (var child in Children) bounds = bounds.Union(child.GetBounds(world, clip));
        return bounds;
    }
    internal static BoundingBox2D? IntersectClips(BoundingBox2D? first, BoundingBox2D? second)
    {
        if (first is null) return second;
        if (second is null) return first;
        return first.Value.Intersection(second.Value);
    }
}

public sealed class SceneLayer
{
    public SceneLayer(Layer layer, IReadOnlyList<SceneNode> nodes) { Layer = layer; Nodes = nodes; }
    public Layer Layer { get; }
    public IReadOnlyList<SceneNode> Nodes { get; }
}

public readonly record struct SceneItem(ObjectId Id, Geometry2D Geometry, Transform2D Transform, SceneStyle Style, Layer Layer, BoundingBox2D Bounds, IReadOnlyDictionary<string, string> Metadata, BoundingBox2D? ClipBounds = null);

public sealed class Scene2D
{
    private readonly SceneItem[] _items;

    public Scene2D(IReadOnlyList<SceneLayer> layers)
    {
        Layers = layers.OrderBy(layer => layer.Layer.Order).ToArray();
        _items = Layers
            .SelectMany(layer => layer.Nodes.SelectMany(node => Flatten(node, Transform2D.Identity, layer.Layer, null)))
            .ToArray();
    }

    public IReadOnlyList<SceneLayer> Layers { get; }

    internal IReadOnlyList<SceneItem> Items => _items;

    public IEnumerable<SceneItem> GetItems(bool visibleOnly = true)
    {
        foreach (var item in _items)
        {
            if (!visibleOnly || item.Layer.IsVisible) yield return item;
        }
    }

    public BoundingBox2D GetBounds(bool visibleOnly = true)
    {
        var bounds = BoundingBox2D.Empty;
        foreach (var item in _items)
        {
            if (visibleOnly && !item.Layer.IsVisible) continue;
            bounds = bounds.Union(item.Bounds);
        }
        return bounds;
    }

    private static IEnumerable<SceneItem> Flatten(SceneNode node, Transform2D parent, Layer layer, BoundingBox2D? parentClip)
    {
        var transform = node.Transform.Then(parent);
        var clip = SceneNode.IntersectClips(parentClip, node.ClipBounds);
        if (node.Geometry is { } geometry)
        {
            var bounds = geometry.GetBounds().Transform(transform);
            if (clip is { } rectangle) bounds = bounds.Intersection(rectangle);
            if (!bounds.IsEmpty) yield return new(node.Id, geometry, transform, node.Style, layer, bounds, node.Metadata, clip);
        }
        foreach (var child in node.Children)
        {
            foreach (var item in Flatten(child, transform, layer, clip)) yield return item;
        }
    }
}
