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
        ArgumentNullException.ThrowIfNull(scene);
        if (!double.IsFinite(tolerance)) throw new ArgumentOutOfRangeException(nameof(tolerance));
        var worldTolerance = Math.Abs(tolerance);
        var items = scene.Items;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            var item = items[index];
            if (!item.Layer.IsVisible) continue;
            if (item.Bounds.Inflate(worldTolerance).Contains(worldPoint) && Hit(item, worldPoint, worldTolerance)) return item;
        }
        return null;
    }

    private static bool Hit(SceneItem item, Point2D point, double worldTolerance)
    {
        if (!item.Transform.TryInvert(out var inverse)) return false;
        var local = inverse.Apply(point);
        var localTolerance = TransformTolerance(inverse, worldTolerance);
        return item.Geometry switch
        {
            PointGeometry p => p.Position.DistanceTo(local) <= localTolerance,
            LineGeometry l => DistanceToSegment(local, l.Start, l.End) <= localTolerance,
            PolylineGeometry p => HitSegments(p.Points, p.IsClosed, local, localTolerance),
            PathGeometry p => HitSegments(p.Points, p.IsClosed, local, localTolerance),
            PolygonGeometry p => PointInPolygon(p.Points, local) || HitSegments(p.Points, true, local, localTolerance),
            CompoundPathGeometry p => HitCompoundPath(p, local, localTolerance),
            RectangleGeometry r => r.Rectangle.Inflate(localTolerance).Contains(local),
            CircleGeometry c => Math.Abs(c.Center.DistanceTo(local) - c.Radius) <= localTolerance,
            ArcGeometry a => HitArc(a, local, localTolerance),
            EllipseGeometry e => HitEllipse(e, local, localTolerance),
            TextGeometry t => t.GetBounds().Inflate(localTolerance).Contains(local),
            ImageGeometry i => i.GetBounds().Inflate(localTolerance).Contains(local),
            _ => false
        };
    }

    private static bool HitCompoundPath(CompoundPathGeometry path, Point2D point, double tolerance)
    {
        var parity = false;
        foreach (var loop in path.Loops)
        {
            if (HitSegments(loop, true, point, tolerance)) return true;
            if (loop.Count >= 3 && PointInPolygon(loop, point)) parity = !parity;
        }
        return parity;
    }

    private static double TransformTolerance(Transform2D inverse, double worldTolerance)
    {
        if (worldTolerance <= double.Epsilon) return 0;
        var sumSquares = (inverse.M11 * inverse.M11) + (inverse.M12 * inverse.M12) + (inverse.M21 * inverse.M21) + (inverse.M22 * inverse.M22);
        var determinant = (inverse.M11 * inverse.M22) - (inverse.M12 * inverse.M21);
        var discriminant = Math.Max(0, (sumSquares * sumSquares) - (4 * determinant * determinant));
        var largestEigenvalue = (sumSquares + Math.Sqrt(discriminant)) / 2;
        return worldTolerance * Math.Sqrt(Math.Max(0, largestEigenvalue));
    }

    private static bool HitArc(ArcGeometry arc, Point2D point, double tolerance)
    {
        if (Math.Abs(arc.Center.DistanceTo(point) - arc.Radius) > tolerance) return false;
        var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
        if (arc.ContainsAngle(angle)) return true;
        return arc.At(arc.StartRadians).DistanceTo(point) <= tolerance || arc.At(arc.StartRadians + arc.SweepRadians).DistanceTo(point) <= tolerance;
    }

    private static bool HitEllipse(EllipseGeometry ellipse, Point2D point, double tolerance)
    {
        var radiusX = Math.Abs(ellipse.RadiusX);
        var radiusY = Math.Abs(ellipse.RadiusY);
        if (radiusX <= double.Epsilon && radiusY <= double.Epsilon) return ellipse.Center.DistanceTo(point) <= tolerance;
        if (radiusX <= double.Epsilon) return DistanceToSegment(point, new(ellipse.Center.X, ellipse.Center.Y - radiusY), new(ellipse.Center.X, ellipse.Center.Y + radiusY)) <= tolerance;
        if (radiusY <= double.Epsilon) return DistanceToSegment(point, new(ellipse.Center.X - radiusX, ellipse.Center.Y), new(ellipse.Center.X + radiusX, ellipse.Center.Y)) <= tolerance;
        var normalized = ((point.X - ellipse.Center.X) * (point.X - ellipse.Center.X) / (radiusX * radiusX)) + ((point.Y - ellipse.Center.Y) * (point.Y - ellipse.Center.Y) / (radiusY * radiusY));
        return Math.Abs(normalized - 1) <= tolerance / Math.Max(radiusX, radiusY);
    }

    private static bool HitSegments(IReadOnlyList<Point2D> points, bool closed, Point2D target, double tolerance)
    {
        for (var index = 1; index < points.Count; index++) if (DistanceToSegment(target, points[index - 1], points[index]) <= tolerance) return true;
        return closed && points.Count > 2 && DistanceToSegment(target, points[^1], points[0]) <= tolerance;
    }

    private static double DistanceToSegment(Point2D point, Point2D start, Point2D end)
    {
        var segment = end - start;
        var relative = point - start;
        var denominator = segment.LengthSquared;
        var t = denominator <= double.Epsilon ? 0 : Math.Clamp(((relative.X * segment.X) + (relative.Y * segment.Y)) / denominator, 0, 1);
        return point.DistanceTo(start + (segment * t));
    }

    private static bool PointInPolygon(IReadOnlyList<Point2D> points, Point2D point)
    {
        if (points.Count < 3) return false;
        var inside = false;
        for (var index = 0; index < points.Count; index++)
        {
            var previous = (index + points.Count - 1) % points.Count;
            if (((points[index].Y > point.Y) != (points[previous].Y > point.Y)) && point.X < ((points[previous].X - points[index].X) * (point.Y - points[index].Y) / (points[previous].Y - points[index].Y)) + points[index].X) inside = !inside;
        }
        return inside;
    }
}
