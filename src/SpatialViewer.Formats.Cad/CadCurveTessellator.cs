using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>Scene-facing curve tessellation. Reader adapters retain analytic CAD semantics; approximation begins only at the translation/render boundary.</summary>
internal static class CadCurveTessellator
{
    public static IReadOnlyList<Point2D> Spline(CadSplineDefinition spline, int minimumSegments = 64)
    {
        ArgumentNullException.ThrowIfNull(spline);
        if (spline.ControlPoints.Count < 2)
        {
            var fallback = spline.FitPoints.Where(IsFinite).ToArray();
            return fallback.Length >= 2 ? fallback : spline.ControlPoints.Where(IsFinite).ToArray();
        }

        var control = spline.ControlPoints.Where(IsFinite).ToArray();
        if (control.Length < 2) return spline.FitPoints.Where(IsFinite).ToArray();
        var degree = Math.Clamp(spline.Degree, 1, Math.Min(10, control.Length - 1));
        var knots = ValidKnots(spline.Knots, control.Length, degree) ? spline.Knots.ToArray() : UniformClampedKnots(control.Length, degree);
        var weights = spline.Weights.Count == control.Length && spline.Weights.All(value => double.IsFinite(value) && value > 0)
            ? spline.Weights.ToArray()
            : Enumerable.Repeat(1d, control.Length).ToArray();
        var start = knots[degree];
        var end = knots[control.Length];
        if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start) return control;

        var segments = Math.Max(minimumSegments, control.Length * 12);
        var points = new List<Point2D>(segments + 1);
        for (var step = 0; step <= segments; step++)
        {
            var u = step == segments ? Math.BitDecrement(end) : start + ((end - start) * step / segments);
            var point = Evaluate(control, weights, knots, degree, u);
            if (IsFinite(point) && (points.Count == 0 || points[^1].DistanceTo(point) > 1e-12)) points.Add(point);
        }
        if (!spline.IsClosed && !spline.IsPeriodic && points.Count > 0 && points[^1].DistanceTo(control[^1]) > 1e-9) points.Add(control[^1]);
        return points;
    }

    public static IReadOnlyList<Point2D> HatchLoop(CadHatchLoop loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        var points = new List<Point2D>();
        foreach (var edge in loop.Edges)
        {
            var segment = edge switch
            {
                CadHatchLineEdge line => new[] { line.Start, line.End },
                CadHatchArcEdge arc => Arc(arc.Center, arc.Radius, arc.StartRadians, arc.SweepRadians),
                CadHatchEllipseEdge ellipse => Ellipse(ellipse),
                CadHatchPolylineEdge polyline => Polyline(polyline),
                CadHatchSplineEdge spline => Spline(spline.Spline).ToArray(),
                _ => Array.Empty<Point2D>()
            };
            Append(points, segment);
        }
        if (points.Count > 1 && points[^1].DistanceTo(points[0]) <= 1e-9) points.RemoveAt(points.Count - 1);
        return points;
    }

    private static Point2D[] Arc(Point2D center, double radius, double start, double sweep)
    {
        if (!double.IsFinite(radius) || radius <= 0 || !double.IsFinite(start) || !double.IsFinite(sweep)) return Array.Empty<Point2D>();
        var segments = Math.Max(8, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 24)));
        return Enumerable.Range(0, segments + 1)
            .Select(index => start + (sweep * index / segments))
            .Select(angle => new Point2D(center.X + (Math.Cos(angle) * radius), center.Y + (Math.Sin(angle) * radius)))
            .ToArray();
    }

    private static Point2D[] Ellipse(CadHatchEllipseEdge ellipse)
    {
        var axis = ellipse.MajorAxisEndPoint;
        var rx = Math.Sqrt((axis.X * axis.X) + (axis.Y * axis.Y));
        var ry = rx * Math.Abs(ellipse.RadiusRatio);
        if (rx <= double.Epsilon || ry <= double.Epsilon) return Array.Empty<Point2D>();
        var rotation = Math.Atan2(axis.Y, axis.X);
        var segments = Math.Max(12, (int)Math.Ceiling(Math.Abs(ellipse.SweepRadians) / (Math.PI / 24)));
        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);
        return Enumerable.Range(0, segments + 1).Select(index =>
        {
            var angle = ellipse.StartRadians + (ellipse.SweepRadians * index / segments);
            var x = Math.Cos(angle) * rx;
            var y = Math.Sin(angle) * ry;
            return new Point2D(ellipse.Center.X + (x * cos) - (y * sin), ellipse.Center.Y + (x * sin) + (y * cos));
        }).ToArray();
    }

    private static Point2D[] Polyline(CadHatchPolylineEdge polyline)
    {
        if (polyline.Vertices.Count < 2) return polyline.Vertices.ToArray();
        var points = new List<Point2D>();
        var segmentCount = polyline.IsClosed ? polyline.Vertices.Count : polyline.Vertices.Count - 1;
        for (var index = 0; index < segmentCount; index++)
        {
            var start = polyline.Vertices[index];
            var end = polyline.Vertices[(index + 1) % polyline.Vertices.Count];
            var bulge = index < polyline.Bulges.Count ? polyline.Bulges[index] : 0;
            Append(points, Bulge(start, end, bulge));
        }
        return points.ToArray();
    }

    private static Point2D[] Bulge(Point2D start, Point2D end, double bulge)
    {
        var chord = start.DistanceTo(end);
        if (!double.IsFinite(bulge) || Math.Abs(bulge) <= 1e-12 || chord <= double.Epsilon) return new[] { start, end };
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var midpoint = new Point2D((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        var offset = chord * (1 - (bulge * bulge)) / (4 * bulge);
        var center = new Point2D(midpoint.X - ((dy / chord) * offset), midpoint.Y + ((dx / chord) * offset));
        var radius = center.DistanceTo(start);
        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        return Arc(center, radius, startAngle, 4 * Math.Atan(bulge));
    }

    private static Point2D Evaluate(Point2D[] control, double[] weights, double[] knots, int degree, double u)
    {
        double x = 0, y = 0, denominator = 0;
        for (var index = 0; index < control.Length; index++)
        {
            var basis = Basis(index, degree, u, knots);
            if (basis == 0) continue;
            var weighted = basis * weights[index];
            x += control[index].X * weighted;
            y += control[index].Y * weighted;
            denominator += weighted;
        }
        return Math.Abs(denominator) <= double.Epsilon ? control[0] : new Point2D(x / denominator, y / denominator);
    }

    private static double Basis(int index, int degree, double u, IReadOnlyList<double> knots)
    {
        if (degree == 0) return u >= knots[index] && u < knots[index + 1] ? 1 : 0;
        var leftDenominator = knots[index + degree] - knots[index];
        var rightDenominator = knots[index + degree + 1] - knots[index + 1];
        var left = Math.Abs(leftDenominator) <= double.Epsilon ? 0 : ((u - knots[index]) / leftDenominator) * Basis(index, degree - 1, u, knots);
        var right = Math.Abs(rightDenominator) <= double.Epsilon ? 0 : ((knots[index + degree + 1] - u) / rightDenominator) * Basis(index + 1, degree - 1, u, knots);
        return left + right;
    }

    private static bool ValidKnots(IReadOnlyList<double> knots, int controlCount, int degree)
    {
        if (knots.Count != controlCount + degree + 1 || knots.Any(value => !double.IsFinite(value))) return false;
        for (var index = 1; index < knots.Count; index++) if (knots[index] < knots[index - 1]) return false;
        return true;
    }

    private static double[] UniformClampedKnots(int controlCount, int degree)
    {
        var count = controlCount + degree + 1;
        var knots = new double[count];
        var interior = controlCount - degree - 1;
        for (var index = 0; index < count; index++)
        {
            if (index <= degree) knots[index] = 0;
            else if (index >= controlCount) knots[index] = 1;
            else knots[index] = (double)(index - degree) / (interior + 1);
        }
        return knots;
    }

    private static void Append(List<Point2D> target, IEnumerable<Point2D> source)
    {
        foreach (var point in source.Where(IsFinite))
        {
            if (target.Count == 0 || target[^1].DistanceTo(point) > 1e-9) target.Add(point);
        }
    }

    private static bool IsFinite(Point2D point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
