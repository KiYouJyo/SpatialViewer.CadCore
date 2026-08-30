using SpatialViewer.Core;

namespace SpatialViewer.Rendering;

/// <summary>Adaptive screen-space tessellation for circular arcs with a bounded pixel error.</summary>
public static class AdaptiveArcTessellator
{
    public const double DefaultTolerancePixels = 0.25;
    private const int DefaultMaxDepth = 12;

    public static IReadOnlyList<Point2D> Tessellate(ArcGeometry arc, Func<Point2D, Point2D> worldToScreen, double tolerancePixels = DefaultTolerancePixels, int maxDepth = DefaultMaxDepth)
    {
        ArgumentNullException.ThrowIfNull(arc);
        ArgumentNullException.ThrowIfNull(worldToScreen);
        if (!double.IsFinite(tolerancePixels) || tolerancePixels <= 0) throw new ArgumentOutOfRangeException(nameof(tolerancePixels));
        if (maxDepth is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        if (!double.IsFinite(arc.Radius) || arc.Radius <= 0 || !double.IsFinite(arc.StartRadians) || !double.IsFinite(arc.SweepRadians)) return Array.Empty<Point2D>();

        var sweep = arc.SweepRadians;
        if (Math.Abs(sweep) <= double.Epsilon) return new[] { worldToScreen(PointAt(arc, arc.StartRadians)) };

        var initialSegments = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2)));
        var points = new List<Point2D>(initialSegments * 4 + 1);
        for (var segment = 0; segment < initialSegments; segment++)
        {
            var start = arc.StartRadians + (sweep * segment / initialSegments);
            var end = arc.StartRadians + (sweep * (segment + 1) / initialSegments);
            var p0 = worldToScreen(PointAt(arc, start));
            var p1 = worldToScreen(PointAt(arc, end));
            if (points.Count == 0) points.Add(p0);
            Subdivide(arc, worldToScreen, start, end, p0, p1, tolerancePixels * tolerancePixels, maxDepth, 0, points);
        }
        return points;
    }

    private static void Subdivide(ArcGeometry arc, Func<Point2D, Point2D> map, double start, double end, Point2D p0, Point2D p1, double toleranceSquared, int maxDepth, int depth, List<Point2D> output)
    {
        var middle = (start + end) / 2;
        var actualMiddle = map(PointAt(arc, middle));
        var chordMiddle = new Point2D((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2);
        var dx = actualMiddle.X - chordMiddle.X;
        var dy = actualMiddle.Y - chordMiddle.Y;
        if (depth >= maxDepth || (dx * dx) + (dy * dy) <= toleranceSquared)
        {
            output.Add(p1);
            return;
        }

        Subdivide(arc, map, start, middle, p0, actualMiddle, toleranceSquared, maxDepth, depth + 1, output);
        Subdivide(arc, map, middle, end, actualMiddle, p1, toleranceSquared, maxDepth, depth + 1, output);
    }

    private static Point2D PointAt(ArcGeometry arc, double angle) => new(arc.Center.X + (Math.Cos(angle) * arc.Radius), arc.Center.Y + (Math.Sin(angle) * arc.Radius));
}
