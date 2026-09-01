using System.Globalization;
using SpatialViewer.Core;

namespace SpatialViewer.Rendering;

/// <summary>Adaptive screen-space tessellation for ellipses after the complete world transform has been applied.</summary>
public static class AdaptiveEllipseTessellator
{
    public const double DefaultTolerancePixels = 0.25;
    private const int DefaultMaxDepth = 12;

    public static IReadOnlyList<Point2D> Tessellate(EllipseGeometry ellipse, Func<Point2D, Point2D> worldToScreen, double tolerancePixels = DefaultTolerancePixels, int maxDepth = DefaultMaxDepth)
    {
        ArgumentNullException.ThrowIfNull(ellipse);
        ArgumentNullException.ThrowIfNull(worldToScreen);
        if (!double.IsFinite(tolerancePixels) || tolerancePixels <= 0) throw new ArgumentOutOfRangeException(nameof(tolerancePixels));
        if (maxDepth is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        if (!double.IsFinite(ellipse.RadiusX) || !double.IsFinite(ellipse.RadiusY) || ellipse.RadiusX <= 0 || ellipse.RadiusY <= 0) return Array.Empty<Point2D>();

        var points = new List<Point2D>(65);
        for (var quarter = 0; quarter < 4; quarter++)
        {
            var start = quarter * Math.PI / 2;
            var end = (quarter + 1) * Math.PI / 2;
            var p0 = worldToScreen(PointAt(ellipse, start));
            var p1 = worldToScreen(PointAt(ellipse, end));
            if (points.Count == 0) points.Add(p0);
            Subdivide(ellipse, worldToScreen, start, end, p0, p1, tolerancePixels * tolerancePixels, maxDepth, 0, points);
        }
        return points;
    }

    private static void Subdivide(EllipseGeometry ellipse, Func<Point2D, Point2D> map, double start, double end, Point2D p0, Point2D p1, double toleranceSquared, int maxDepth, int depth, List<Point2D> output)
    {
        var middle = (start + end) / 2;
        var actualMiddle = map(PointAt(ellipse, middle));
        var chordMiddle = new Point2D((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2);
        var dx = actualMiddle.X - chordMiddle.X;
        var dy = actualMiddle.Y - chordMiddle.Y;
        if (depth >= maxDepth || (dx * dx) + (dy * dy) <= toleranceSquared)
        {
            output.Add(p1);
            return;
        }
        Subdivide(ellipse, map, start, middle, p0, actualMiddle, toleranceSquared, maxDepth, depth + 1, output);
        Subdivide(ellipse, map, middle, end, actualMiddle, p1, toleranceSquared, maxDepth, depth + 1, output);
    }

    private static Point2D PointAt(EllipseGeometry ellipse, double angle) => new(ellipse.Center.X + (Math.Cos(angle) * ellipse.RadiusX), ellipse.Center.Y + (Math.Sin(angle) * ellipse.RadiusY));
}

/// <summary>Resolves CAD-style signed line pattern metadata into screen-space dash/gap lengths.</summary>
public static class RenderStrokePattern
{
    public const string PatternKey = "LineTypePattern";
    public const string EntityScaleKey = "LineTypeScale";
    public const string GlobalScaleKey = "GlobalLineTypeScale";

    public static IReadOnlyList<double> ResolvePixels(IReadOnlyDictionary<string, string>? metadata, double screenPixelsPerLocalUnit)
    {
        if (metadata is null || !metadata.TryGetValue(PatternKey, out var text) || string.IsNullOrWhiteSpace(text) || !double.IsFinite(screenPixelsPerLocalUnit) || screenPixelsPerLocalUnit <= 0) return Array.Empty<double>();
        var entityScale = PositiveScale(metadata, EntityScaleKey);
        var globalScale = PositiveScale(metadata, GlobalScaleKey);
        var scale = screenPixelsPerLocalUnit * entityScale * globalScale;
        var result = new List<double>();
        foreach (var token in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) continue;
            if (Math.Abs(value) <= double.Epsilon) result.Add(0);
            else result.Add(Math.CopySign(Math.Max(0.5, Math.Abs(value) * scale), value));
        }
        return result.Any(value => value > 0) ? result : Array.Empty<double>();
    }

    private static double PositiveScale(IReadOnlyDictionary<string, string> metadata, string key) => metadata.TryGetValue(key, out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value > 0 ? value : 1;
}

public readonly record struct TextScreenPlacement(Point2D Origin, double RotationRadians, double FontSizePixels, double HorizontalScale = 1, double VerticalScale = 1, double ObliqueShear = 0);

/// <summary>Extracts text rotation, scale, anchoring, width factor, mirror and oblique presentation from the complete local-to-screen mapping.</summary>
public static class TextScreenTransform
{
    public static TextScreenPlacement Resolve(TextGeometry text, Func<Point2D, Point2D> localToScreen)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(localToScreen);
        var anchor = localToScreen(text.Origin);
        var x = localToScreen(new Point2D(text.Origin.X + 1, text.Origin.Y));
        var y = localToScreen(new Point2D(text.Origin.X, text.Origin.Y + 1));
        var rotation = Math.Atan2(x.Y - anchor.Y, x.X - anchor.X);
        var xScale = anchor.DistanceTo(x);
        var yScale = anchor.DistanceTo(y);
        var bounds = text.GetBounds();
        var origin = localToScreen(new Point2D(bounds.MinX, bounds.MaxY));
        var widthFactor = double.IsFinite(text.WidthFactor) && Math.Abs(text.WidthFactor) > double.Epsilon ? Math.Abs(text.WidthFactor) : 1;
        var horizontalScale = yScale <= double.Epsilon ? widthFactor : widthFactor * xScale / yScale;
        if (text.IsBackward) horizontalScale = -horizontalScale;
        var verticalScale = text.IsUpsideDown ? -1d : 1d;
        var shear = double.IsFinite(text.ObliqueAngleRadians) ? Math.Tan(text.ObliqueAngleRadians) : 0;
        return new TextScreenPlacement(origin, rotation, Math.Max(1, text.Height * yScale), horizontalScale, verticalScale, shear);
    }
}
