using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

public sealed record CadHatchPatternTessellation(IReadOnlyList<Geometry2D> Geometries, bool Truncated, int CandidateLineCount);

/// <summary>
/// Expands reader-independent CAD hatch pattern line families and clips them against the hatch's
/// tessellated compound boundary with an even-odd rule. Output uses generic scene geometry so the
/// renderer remains CAD-format agnostic.
/// </summary>
public static class CadHatchPatternTessellator
{
    private const int MaxCandidateLines = 20_000;
    private const int MaxOutputGeometries = 100_000;
    private const double Epsilon = 1e-9;

    public static CadHatchPatternTessellation Tessellate(CadHatchEntity hatch, IReadOnlyList<IReadOnlyList<Point2D>> loops)
    {
        ArgumentNullException.ThrowIfNull(hatch);
        ArgumentNullException.ThrowIfNull(loops);
        if (hatch.IsSolid || hatch.PatternLines.Count == 0 || loops.Count == 0) return new(Array.Empty<Geometry2D>(), false, 0);

        var validLoops = loops.Where(loop => loop.Count >= 3).ToArray();
        if (validLoops.Length == 0) return new(Array.Empty<Geometry2D>(), false, 0);
        var bounds = BoundingBox2D.Empty;
        foreach (var loop in validLoops) bounds = bounds.Union(BoundingBox2D.FromPoints(loop));
        if (bounds.IsEmpty) return new(Array.Empty<Geometry2D>(), false, 0);

        var output = new List<Geometry2D>();
        var candidateCount = 0;
        var truncated = false;
        var scale = double.IsFinite(hatch.PatternScale) && Math.Abs(hatch.PatternScale) > Epsilon ? Math.Abs(hatch.PatternScale) : 1;
        var globalAngle = double.IsFinite(hatch.PatternAngleRadians) ? hatch.PatternAngleRadians : 0;

        foreach (var source in hatch.PatternLines)
        {
            var angle = (double.IsFinite(source.AngleRadians) ? source.AngleRadians : 0) + globalAngle;
            var direction = new Vector2D(Math.Cos(angle), Math.Sin(angle));
            var normal = new Vector2D(-direction.Y, direction.X);
            var basePoint = RotateScale(source.BasePoint, scale, globalAngle);
            var offset = RotateScale(source.Offset, scale, globalAngle);
            var spacing = Dot(offset, normal);
            if (!double.IsFinite(spacing) || Math.Abs(spacing) <= Epsilon) continue;

            var projections = BoundsCorners(bounds).Select(point => Dot(new Vector2D(point.X, point.Y), normal)).ToArray();
            var minProjection = projections.Min();
            var maxProjection = projections.Max();
            var baseProjection = Dot(new Vector2D(basePoint.X, basePoint.Y), normal);
            var firstRatio = (minProjection - baseProjection) / spacing;
            var secondRatio = (maxProjection - baseProjection) / spacing;
            var first = (long)Math.Floor(Math.Min(firstRatio, secondRatio)) - 1;
            var last = (long)Math.Ceiling(Math.Max(firstRatio, secondRatio)) + 1;

            if (last - first + 1 > MaxCandidateLines)
            {
                truncated = true;
                var middle = (first + last) / 2;
                first = middle - (MaxCandidateLines / 2);
                last = first + MaxCandidateLines - 1;
            }

            var dashes = source.DashLengths
                .Where(double.IsFinite)
                .Select(value => value * scale)
                .ToArray();

            for (var familyIndex = first; familyIndex <= last; familyIndex++)
            {
                candidateCount++;
                if (candidateCount > MaxCandidateLines)
                {
                    truncated = true;
                    break;
                }

                var origin = basePoint + (offset * familyIndex);
                var intersections = Intersections(origin, direction, validLoops);
                if (intersections.Count < 2) continue;

                for (var index = 0; index + 1 < intersections.Count; index++)
                {
                    var start = intersections[index];
                    var end = intersections[index + 1];
                    if (end - start <= Epsilon) continue;
                    var middle = PointAt(origin, direction, (start + end) / 2);
                    if (!InsideCompound(middle, validLoops)) continue;
                    AddPatternInterval(output, origin, direction, start, end, dashes, ref truncated);
                    if (output.Count >= MaxOutputGeometries) break;
                }

                if (output.Count >= MaxOutputGeometries)
                {
                    truncated = true;
                    break;
                }
            }

            if (candidateCount > MaxCandidateLines || output.Count >= MaxOutputGeometries) break;
        }

        return new(output, truncated, Math.Min(candidateCount, MaxCandidateLines));
    }

    private static List<double> Intersections(Point2D origin, Vector2D direction, IReadOnlyList<IReadOnlyList<Point2D>> loops)
    {
        var values = new List<double>();
        foreach (var loop in loops)
        {
            for (var index = 0; index < loop.Count; index++)
            {
                var first = loop[index];
                var second = loop[(index + 1) % loop.Count];
                var edge = second - first;
                var denominator = Cross(direction, edge);
                if (Math.Abs(denominator) <= Epsilon) continue;
                var delta = first - origin;
                var t = Cross(delta, edge) / denominator;
                var u = Cross(delta, direction) / denominator;
                if (u >= -Epsilon && u <= 1 + Epsilon && double.IsFinite(t)) values.Add(t);
            }
        }

        values.Sort();
        var unique = new List<double>(values.Count);
        foreach (var value in values)
        {
            if (unique.Count == 0 || Math.Abs(value - unique[^1]) > 1e-7) unique.Add(value);
        }
        return unique;
    }

    private static void AddPatternInterval(List<Geometry2D> output, Point2D origin, Vector2D direction, double start, double end, IReadOnlyList<double> dashes, ref bool truncated)
    {
        if (output.Count >= MaxOutputGeometries) { truncated = true; return; }
        if (dashes.Count == 0)
        {
            output.Add(new LineGeometry(PointAt(origin, direction, start), PointAt(origin, direction, end)));
            return;
        }

        var cycle = dashes.Where(value => Math.Abs(value) > Epsilon).Sum(Math.Abs);
        if (!double.IsFinite(cycle) || cycle <= Epsilon)
        {
            output.Add(new PointGeometry(PointAt(origin, direction, (start + end) / 2)));
            return;
        }

        var cycleStart = Math.Floor(start / cycle) * cycle;
        while (cycleStart <= end + Epsilon)
        {
            var cursor = cycleStart;
            foreach (var dash in dashes)
            {
                if (output.Count >= MaxOutputGeometries) { truncated = true; return; }
                var length = Math.Abs(dash);
                if (length <= Epsilon)
                {
                    if (cursor >= start - Epsilon && cursor <= end + Epsilon) output.Add(new PointGeometry(PointAt(origin, direction, cursor)));
                    continue;
                }

                var next = cursor + length;
                if (dash > 0)
                {
                    var visibleStart = Math.Max(start, cursor);
                    var visibleEnd = Math.Min(end, next);
                    if (visibleEnd - visibleStart > Epsilon)
                    {
                        output.Add(new LineGeometry(PointAt(origin, direction, visibleStart), PointAt(origin, direction, visibleEnd)));
                    }
                }
                cursor = next;
            }
            cycleStart += cycle;
        }
    }

    private static bool InsideCompound(Point2D point, IReadOnlyList<IReadOnlyList<Point2D>> loops)
    {
        var inside = false;
        foreach (var loop in loops) if (InsideLoop(point, loop)) inside = !inside;
        return inside;
    }

    private static bool InsideLoop(Point2D point, IReadOnlyList<Point2D> loop)
    {
        var inside = false;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            var crosses = (a.Y > point.Y) != (b.Y > point.Y);
            if (!crosses) continue;
            var x = a.X + ((point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
            if (x > point.X) inside = !inside;
        }
        return inside;
    }

    private static Point2D RotateScale(Point2D point, double scale, double angle)
    {
        var cosine = Math.Cos(angle); var sine = Math.Sin(angle);
        var x = point.X * scale; var y = point.Y * scale;
        return new Point2D((x * cosine) - (y * sine), (x * sine) + (y * cosine));
    }

    private static Vector2D RotateScale(Vector2D vector, double scale, double angle)
    {
        var cosine = Math.Cos(angle); var sine = Math.Sin(angle);
        var x = vector.X * scale; var y = vector.Y * scale;
        return new Vector2D((x * cosine) - (y * sine), (x * sine) + (y * cosine));
    }

    private static IEnumerable<Point2D> BoundsCorners(BoundingBox2D bounds)
    {
        yield return new(bounds.MinX, bounds.MinY);
        yield return new(bounds.MaxX, bounds.MinY);
        yield return new(bounds.MaxX, bounds.MaxY);
        yield return new(bounds.MinX, bounds.MaxY);
    }

    private static Point2D PointAt(Point2D origin, Vector2D direction, double t) => origin + (direction * t);
    private static double Dot(Vector2D first, Vector2D second) => (first.X * second.X) + (first.Y * second.Y);
    private static double Cross(Vector2D first, Vector2D second) => (first.X * second.Y) - (first.Y * second.X);
}
