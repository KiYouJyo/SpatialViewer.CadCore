using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Converts the safe 2D subset of ACadSharp proxy graphics into reader-independent CadCore primitives.
/// Geometry is deliberately withheld when model transforms or clipping commands are present until those
/// stateful commands are implemented, preventing a fallback from being drawn at a plausible-but-wrong location.
/// </summary>
public static class ACadSharpProxyGraphicsMapping
{
    private const double Epsilon = 1e-9;

    public static IReadOnlyList<CadProxyPrimitive> Map(
        IEnumerable<IProxyGeometry> graphics,
        out int unsupportedCount,
        out bool statefulGeometryCommandsPresent)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        var source = graphics.ToArray();
        statefulGeometryCommandsPresent = source.Any(graphic => IsStatefulGeometryCommand(graphic.GraphicsType));
        if (statefulGeometryCommandsPresent)
        {
            unsupportedCount = source.Length;
            return Array.Empty<CadProxyPrimitive>();
        }

        var result = new List<CadProxyPrimitive>();
        unsupportedCount = 0;
        foreach (var graphic in source)
        {
            var mapped = MapOne(graphic);
            if (mapped is null)
            {
                unsupportedCount++;
                continue;
            }

            result.Add(mapped);
        }

        return result;
    }

    private static CadProxyPrimitive? MapOne(IProxyGeometry graphic)
        => graphic switch
        {
            // ProxyPolylineWithNormal derives from ProxyPolyline. Handle it first and do not let a
            // rejected non-planar instance fall through to the unguarded base-class arm.
            ProxyPolylineWithNormal polyline => IsPlanar(polyline.Normal) && TryPoints(polyline.Points, 2, out var points)
                ? new CadProxyPolyline(points)
                : null,
            ProxyPolyline polyline when TryPoints(polyline.Points, 2, out var points) => new CadProxyPolyline(points),
            ProxyPolygon polygon when TryPoints(polygon.Points, 3, out var points) => new CadProxyPolygon(points),
            ProxyCircle circle when IsPlanar(circle.Normal) && IsPositiveFinite(circle.Radius) && TryPoint(circle.Center, out var center)
                => new CadProxyCircle(center, circle.Radius),
            ProxyCircularArc arc when IsPlanar(arc.Normal) && IsPositiveFinite(arc.Radius) && TryPoint(arc.Center, out var center) && TryDirection(arc.StartVectorDirection, out var startRadians) && double.IsFinite(arc.SweepAngle)
                => new CadProxyArc(center, arc.Radius, startRadians, arc.Normal.Z < 0 ? -arc.SweepAngle : arc.SweepAngle),
            ProxyText text when TryProxyText(
                text.Normal,
                text.StartPoint,
                text.TextDirection,
                text.Text,
                text.Height,
                text.WidthFactor,
                text.ObliqueAngle,
                nameof(GraphicsType.Text),
                out var mappedText) => mappedText,
            ProxyUnicodeText text when TryProxyText(
                text.Normal,
                text.StartPoint,
                text.TextDirection,
                text.Text,
                text.Height,
                text.WidthFactor,
                text.ObliqueAngle,
                nameof(GraphicsType.UnicodeText),
                out var mappedText) => mappedText,
            _ => null
        };

    private static bool IsStatefulGeometryCommand(GraphicsType type)
        => type is GraphicsType.PushModelTransform
            or GraphicsType.PushModelTransform2
            or GraphicsType.PopModelTransform
            or GraphicsType.PushClip
            or GraphicsType.PopClip;

    private static bool IsPlanar(CSMath.XYZ normal)
        => double.IsFinite(normal.X)
            && double.IsFinite(normal.Y)
            && double.IsFinite(normal.Z)
            && Math.Abs(normal.X) <= Epsilon
            && Math.Abs(normal.Y) <= Epsilon
            && Math.Abs(Math.Abs(normal.Z) - 1d) <= Epsilon;

    private static bool IsPositiveFacingPlanar(CSMath.XYZ normal)
        => IsPlanar(normal) && normal.Z > 0;

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > Epsilon;

    private static bool TryProxyText(
        CSMath.XYZ normal,
        CSMath.XYZ origin,
        CSMath.XYZ direction,
        string? text,
        double height,
        double widthFactor,
        double obliqueAngle,
        string proxyTextKind,
        out CadProxyText mapped)
    {
        mapped = null!;
        // Negative-Z text needs an OCS/mirroring transform. Withhold it rather than silently flipping
        // labels; ordinary Tianzheng plan annotations use the positive-Z planar case.
        if (!IsPositiveFacingPlanar(normal)
            || string.IsNullOrEmpty(text)
            || !IsPositiveFinite(height)
            || !double.IsFinite(widthFactor)
            || !double.IsFinite(obliqueAngle)
            || !TryPoint(origin, out var mappedOrigin)
            || !TryDirection(direction, out var rotationRadians))
            return false;

        var normalizedWidthFactor = Math.Abs(widthFactor) > Epsilon ? Math.Abs(widthFactor) : 1d;
        mapped = new CadProxyText(
            mappedOrigin,
            text,
            height,
            rotationRadians,
            normalizedWidthFactor,
            obliqueAngle,
            proxyTextKind);
        return true;
    }

    private static bool TryDirection(CSMath.XYZ direction, out double radians)
    {
        radians = 0;
        if (!double.IsFinite(direction.X) || !double.IsFinite(direction.Y)) return false;
        if (Math.Abs(direction.X) <= Epsilon && Math.Abs(direction.Y) <= Epsilon) return false;
        radians = Math.Atan2(direction.Y, direction.X);
        return true;
    }

    private static bool TryPoint(CSMath.XYZ point, out Point2D mapped)
    {
        mapped = Point2D.Origin;
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
        mapped = new Point2D(point.X, point.Y);
        return true;
    }

    private static bool TryPoints(IEnumerable<CSMath.XYZ>? points, int minimumCount, out IReadOnlyList<Point2D> mapped)
    {
        mapped = Array.Empty<Point2D>();
        if (points is null) return false;
        var result = new List<Point2D>();
        foreach (var point in points)
        {
            if (!TryPoint(point, out var converted)) return false;
            result.Add(converted);
        }

        if (result.Count < minimumCount) return false;
        mapped = result;
        return true;
    }
}
