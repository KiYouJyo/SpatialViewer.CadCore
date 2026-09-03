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

public enum TextHorizontalAlignment2D { Left, Center, Right }
public enum TextVerticalAlignment2D { Top, Middle, Bottom, Baseline }

/// <summary>Backend-neutral text geometry. The primary constructor remains compatible with Stage 1 while optional layout properties retain richer source semantics.</summary>
public sealed record TextGeometry(Point2D Origin, string Text, double Height = 10) : Geometry2D
{
    public string FontFamily { get; init; } = string.Empty;
    public double WidthFactor { get; init; } = 1;
    public double ObliqueAngleRadians { get; init; }
    public double LayoutWidth { get; init; }
    public double LineSpacingFactor { get; init; } = 1;
    public TextHorizontalAlignment2D HorizontalAlignment { get; init; } = TextHorizontalAlignment2D.Left;
    public TextVerticalAlignment2D VerticalAlignment { get; init; } = TextVerticalAlignment2D.Top;
    public bool IsBackward { get; init; }
    public bool IsUpsideDown { get; init; }
    public bool IsMultiline { get; init; }

    public double EstimatedWidth
    {
        get
        {
            if (double.IsFinite(LayoutWidth) && LayoutWidth > double.Epsilon) return LayoutWidth;
            var factor = double.IsFinite(WidthFactor) && Math.Abs(WidthFactor) > double.Epsilon ? Math.Abs(WidthFactor) : 1;
            var lines = Lines();
            var units = lines.Length == 0 ? .6 : lines.Max(VisualUnits);
            return Math.Max(Height * .6, units * Height * factor);
        }
    }

    public double EstimatedHeight
    {
        get
        {
            var lines = Math.Max(1, Lines().Length);
            var spacing = double.IsFinite(LineSpacingFactor) && LineSpacingFactor > double.Epsilon ? LineSpacingFactor : 1;
            return Height * (1 + ((lines - 1) * spacing));
        }
    }

    public override BoundingBox2D GetBounds()
    {
        var width = Math.Max(0, EstimatedWidth);
        var height = Math.Max(0, EstimatedHeight);
        var (minX, maxX) = HorizontalAlignment switch
        {
            TextHorizontalAlignment2D.Center => (-width / 2, width / 2),
            TextHorizontalAlignment2D.Right => (-width, 0),
            _ => (0d, width)
        };
        var (minY, maxY) = VerticalAlignment switch
        {
            TextVerticalAlignment2D.Middle => (-height / 2, height / 2),
            TextVerticalAlignment2D.Bottom or TextVerticalAlignment2D.Baseline => (0d, height),
            _ => (-height, 0d)
        };
        return new BoundingBox2D(Origin.X + minX, Origin.Y + minY, Origin.X + maxX, Origin.Y + maxY);
    }

    private string[] Lines() => Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static double VisualUnits(string line)
    {
        if (line.Length == 0) return .6;
        var units = 0d;
        foreach (var character in line)
        {
            units += character switch
            {
                '\t' => 2.4,
                >= '\u2E80' => 1,
                _ => .6
            };
        }
        return Math.Max(.6, units);
    }
}

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
    /// <summary>
    /// Optional polygon clip expressed in this node's local coordinates. Unlike <see cref="ClipBounds"/>,
    /// this polygon follows the node and all parent transforms. Nested polygons are retained as a clip
    /// stack so renderers can apply exact intersection semantics without approximating them as bounds.
    /// </summary>
    public IReadOnlyList<Point2D> LocalClipPolygon { get; init; } = Array.Empty<Point2D>();
    public BoundingBox2D GetBounds() => GetBounds(Transform2D.Identity, null, Array.Empty<IReadOnlyList<Point2D>>());
    internal BoundingBox2D GetBounds(Transform2D parent, BoundingBox2D? parentClip)
        => GetBounds(parent, parentClip, Array.Empty<IReadOnlyList<Point2D>>());
    private BoundingBox2D GetBounds(Transform2D parent, BoundingBox2D? parentClip, IReadOnlyList<IReadOnlyList<Point2D>> parentClipPolygons)
    {
        var world = Transform.Then(parent);
        var clip = IntersectClips(parentClip, ClipBounds);
        if (!TryAppendLocalClip(parentClipPolygons, LocalClipPolygon, world, out var clipPolygons)) return BoundingBox2D.Empty;
        var bounds = Geometry?.GetBounds().Transform(world) ?? BoundingBox2D.Empty;
        bounds = ApplyClipBounds(bounds, clip, clipPolygons);
        foreach (var child in Children) bounds = bounds.Union(child.GetBounds(world, clip, clipPolygons));
        return bounds;
    }
    internal static BoundingBox2D? IntersectClips(BoundingBox2D? first, BoundingBox2D? second)
    {
        if (first is null) return second;
        if (second is null) return first;
        return first.Value.Intersection(second.Value);
    }
    internal static BoundingBox2D ApplyClipBounds(BoundingBox2D bounds, BoundingBox2D? rectangle, IReadOnlyList<IReadOnlyList<Point2D>> polygons)
    {
        if (bounds.IsEmpty) return bounds;
        if (rectangle is { } clip) bounds = bounds.Intersection(clip);
        foreach (var polygon in polygons)
        {
            if (polygon.Count < 3) return BoundingBox2D.Empty;
            bounds = bounds.Intersection(BoundingBox2D.FromPoints(polygon));
            if (bounds.IsEmpty) break;
        }
        return bounds;
    }
    internal static bool TryAppendLocalClip(
        IReadOnlyList<IReadOnlyList<Point2D>> parent,
        IReadOnlyList<Point2D> local,
        Transform2D world,
        out IReadOnlyList<IReadOnlyList<Point2D>> result)
    {
        result = parent;
        if (local.Count == 0) return true;
        if (local.Count < 3) return false;
        var transformed = new Point2D[local.Count];
        for (var index = 0; index < local.Count; index++)
        {
            var point = local[index];
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
            transformed[index] = world.Apply(point);
            if (!double.IsFinite(transformed[index].X) || !double.IsFinite(transformed[index].Y)) return false;
        }
        var combined = new IReadOnlyList<Point2D>[parent.Count + 1];
        for (var index = 0; index < parent.Count; index++) combined[index] = parent[index];
        combined[^1] = transformed;
        result = combined;
        return true;
    }
}

public sealed class SceneLayer
{
    public SceneLayer(Layer layer, IReadOnlyList<SceneNode> nodes) { Layer = layer; Nodes = nodes; }
    public Layer Layer { get; }
    public IReadOnlyList<SceneNode> Nodes { get; }
}

public readonly record struct SceneItem(ObjectId Id, Geometry2D Geometry, Transform2D Transform, SceneStyle Style, Layer Layer, BoundingBox2D Bounds, IReadOnlyDictionary<string, string> Metadata, BoundingBox2D? ClipBounds = null)
{
    /// <summary>Final-world polygon clips inherited from transform-aware local clip nodes.</summary>
    public IReadOnlyList<IReadOnlyList<Point2D>> ClipPolygons { get; init; } = Array.Empty<IReadOnlyList<Point2D>>();
}

public sealed class Scene2D
{
    private readonly SceneItem[] _items;

    public Scene2D(IReadOnlyList<SceneLayer> layers)
    {
        Layers = layers.OrderBy(layer => layer.Layer.Order).ToArray();
        _items = Layers
            .SelectMany(layer => layer.Nodes.SelectMany(node => Flatten(node, Transform2D.Identity, layer.Layer, null, Array.Empty<IReadOnlyList<Point2D>>())))
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

    private static IEnumerable<SceneItem> Flatten(
        SceneNode node,
        Transform2D parent,
        Layer layer,
        BoundingBox2D? parentClip,
        IReadOnlyList<IReadOnlyList<Point2D>> parentClipPolygons)
    {
        var transform = node.Transform.Then(parent);
        var clip = SceneNode.IntersectClips(parentClip, node.ClipBounds);
        if (!SceneNode.TryAppendLocalClip(parentClipPolygons, node.LocalClipPolygon, transform, out var clipPolygons)) yield break;
        if (node.Geometry is { } geometry)
        {
            var bounds = SceneNode.ApplyClipBounds(geometry.GetBounds().Transform(transform), clip, clipPolygons);
            if (!bounds.IsEmpty)
            {
                yield return new SceneItem(node.Id, geometry, transform, node.Style, layer, bounds, node.Metadata, clip)
                {
                    ClipPolygons = clipPolygons
                };
            }
        }
        foreach (var child in node.Children)
        {
            foreach (var item in Flatten(child, transform, layer, clip, clipPolygons)) yield return item;
        }
    }
}
