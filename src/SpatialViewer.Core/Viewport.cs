namespace SpatialViewer.Core;

/// <summary>Testable 2D camera. Zoom means device-independent pixels per world unit.</summary>
public sealed class Camera2D
{
    public Camera2D(Point2D target, double zoom = 1) { Target = target; Zoom = zoom; }
    public Point2D Target { get; private set; }
    public double Zoom { get; private set; }
    public double MinimumZoom { get; init; } = 1e-9;
    public double MaximumZoom { get; init; } = 1e9;
    public void SetTarget(Point2D target) => Target = target;
    public void SetZoom(double zoom) { if (!double.IsFinite(zoom)) throw new ArgumentOutOfRangeException(nameof(zoom)); Zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom); }
    public void PanScreen(Vector2D screenDelta) => Target -= screenDelta * (1 / Zoom);
    public void ZoomAt(double multiplier, Point2D screen, Size2D viewport)
    { var anchored = ScreenToWorld(screen, viewport); SetZoom(Zoom * multiplier); var after = ScreenToWorld(screen, viewport); Target += anchored - after; }
    public Point2D WorldToScreen(Point2D world, Size2D viewport) => new(((world.X - Target.X) * Zoom) + viewport.Width / 2, ((Target.Y - world.Y) * Zoom) + viewport.Height / 2);
    public Point2D ScreenToWorld(Point2D screen, Size2D viewport) => new(((screen.X - viewport.Width / 2) / Zoom) + Target.X, Target.Y - ((screen.Y - viewport.Height / 2) / Zoom));
    public void Fit(BoundingBox2D bounds, Size2D viewport, double margin = .08)
    { if (bounds.IsEmpty || viewport.IsEmpty) return; SetTarget(bounds.Center); var usableWidth = viewport.Width * (1 - (2 * margin)); var usableHeight = viewport.Height * (1 - (2 * margin)); SetZoom(Math.Min(usableWidth / Math.Max(bounds.Width, 1e-12), usableHeight / Math.Max(bounds.Height, 1e-12))); }
}

public static class HitTesting
{
    public static SceneItem? HitTest(Scene2D scene, Point2D worldPoint, double tolerance)
    {
        foreach (var item in scene.GetItems().Reverse()) if (item.Bounds.Inflate(tolerance).Contains(worldPoint) && Hit(item, worldPoint, tolerance)) return item;
        return null;
    }
    private static bool Hit(SceneItem item, Point2D point, double tolerance)
    {
        if (!item.Transform.TryInvert(out var inverse)) return false; var local = inverse.Apply(point);
        return item.Geometry switch
        {
            PointGeometry p => p.Position.DistanceTo(local) <= tolerance,
            LineGeometry l => DistanceToSegment(local, l.Start, l.End) <= tolerance,
            PolylineGeometry p => HitSegments(p.Points, p.IsClosed, local, tolerance),
            PathGeometry p => HitSegments(p.Points, p.IsClosed, local, tolerance),
            PolygonGeometry p => PointInPolygon(p.Points, local) || HitSegments(p.Points, true, local, tolerance),
            RectangleGeometry r => r.Rectangle.Inflate(tolerance).Contains(local),
            CircleGeometry c => Math.Abs(c.Center.DistanceTo(local) - c.Radius) <= tolerance,
            ArcGeometry a => Math.Abs(a.Center.DistanceTo(local) - a.Radius) <= tolerance + 0.001,
            EllipseGeometry e => Math.Abs(((local.X - e.Center.X) * (local.X - e.Center.X) / (e.RadiusX * e.RadiusX)) + ((local.Y - e.Center.Y) * (local.Y - e.Center.Y) / (e.RadiusY * e.RadiusY)) - 1) <= tolerance / Math.Max(e.RadiusX, e.RadiusY),
            TextGeometry t => t.GetBounds().Inflate(tolerance).Contains(local), ImageGeometry i => i.GetBounds().Contains(local), _ => false
        };
    }
    private static bool HitSegments(IReadOnlyList<Point2D> points, bool closed, Point2D target, double tolerance) { for (var i = 1; i < points.Count; i++) if (DistanceToSegment(target, points[i - 1], points[i]) <= tolerance) return true; return closed && points.Count > 2 && DistanceToSegment(target, points[^1], points[0]) <= tolerance; }
    private static double DistanceToSegment(Point2D p, Point2D a, Point2D b) { var ab = b - a; var ap = p - a; var denominator = ab.LengthSquared; var t = denominator <= double.Epsilon ? 0 : Math.Clamp(((ap.X * ab.X) + (ap.Y * ab.Y)) / denominator, 0, 1); return p.DistanceTo(a + (ab * t)); }
    private static bool PointInPolygon(IReadOnlyList<Point2D> points, Point2D point) { var inside = false; for (var i = 0; i < points.Count; i++) { var j = (i + points.Count - 1) % points.Count; if (((points[i].Y > point.Y) != (points[j].Y > point.Y)) && point.X < ((points[j].X - points[i].X) * (point.Y - points[i].Y) / (points[j].Y - points[i].Y)) + points[i].X) inside = !inside; } return inside; }
}
