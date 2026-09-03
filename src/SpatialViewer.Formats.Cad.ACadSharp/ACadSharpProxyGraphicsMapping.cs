using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Converts the safe 2D subset of ACadSharp proxy graphics into reader-independent CadCore primitives.
/// Balanced planar model-transform stacks are applied for translation, rotation and uniform scaling.
/// Clip commands, malformed stacks and non-planar/non-similarity transforms remain fail-closed so a
/// fallback is never drawn at a plausible-but-wrong location.
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

        // Clip state changes visibility rather than just coordinates. Until clip boundaries are represented
        // in the reader-independent proxy model, withholding the whole stream is safer than drawing through it.
        if (source.Any(graphic => graphic.GraphicsType is GraphicsType.PushClip or GraphicsType.PopClip))
            return FailClosed(source, out unsupportedCount);

        var result = new List<CadProxyPrimitive>();
        var stack = new Stack<ProxyTransformState>();
        var current = ProxyTransformState.Identity;
        unsupportedCount = 0;

        foreach (var graphic in source)
        {
            switch (graphic)
            {
                case ProxyPushModelTransform push:
                    if (!TryPushTransform(push.TransformationMatrix, stack, ref current))
                        return FailClosed(source, out unsupportedCount);
                    continue;
                case ProxyPushModelTransform2 push:
                    if (!TryPushTransform(push.TransformationMatrix, stack, ref current))
                        return FailClosed(source, out unsupportedCount);
                    continue;
                case ProxyPopModelTransform:
                    if (stack.Count == 0)
                        return FailClosed(source, out unsupportedCount);
                    current = stack.Pop();
                    continue;
            }

            var mapped = MapOne(graphic);
            if (mapped is null)
            {
                unsupportedCount++;
                continue;
            }

            result.Add(current.IsIdentity ? mapped : ApplyTransform(mapped, current));
        }

        if (stack.Count != 0)
            return FailClosed(source, out unsupportedCount);

        return result;
    }

    private static CadProxyPrimitive[] FailClosed(IProxyGeometry[] source, out int unsupportedCount)
    {
        unsupportedCount = source.Length;
        return Array.Empty<CadProxyPrimitive>();
    }

    private static bool TryPushTransform(CSMath.Matrix4 matrix, Stack<ProxyTransformState> stack, ref ProxyTransformState current)
    {
        if (!TryPlanarSimilarity(matrix, out var pushed)) return false;
        stack.Push(current);
        // ObjectARX model-transform stack semantics are previous * matrix. With Transform2D's
        // first.Then(second) convention, that is pushed.Then(previous): local geometry sees the
        // newly pushed transform first, then the transform that was already active.
        current = new ProxyTransformState(
            pushed.Transform.Then(current.Transform),
            pushed.Scale * current.Scale,
            NormalizeAngle(pushed.RotationRadians + current.RotationRadians));
        return true;
    }

    private static bool TryPlanarSimilarity(CSMath.Matrix4 matrix, out ProxyTransformState state)
    {
        state = ProxyTransformState.Identity;
        var values = new[]
        {
            matrix.M00, matrix.M01, matrix.M02, matrix.M03,
            matrix.M10, matrix.M11, matrix.M12, matrix.M13,
            matrix.M20, matrix.M21, matrix.M22, matrix.M23,
            matrix.M30, matrix.M31, matrix.M32, matrix.M33
        };
        if (values.Any(value => !double.IsFinite(value))) return false;

        // Reject perspective and XY/Z coupling. Z-only scale/translation is harmless to the projected
        // plan geometry, but XY must remain a strict affine 2D transform.
        if (Math.Abs(matrix.M03) > Epsilon
            || Math.Abs(matrix.M13) > Epsilon
            || Math.Abs(matrix.M23) > Epsilon
            || Math.Abs(matrix.M33 - 1d) > Epsilon
            || Math.Abs(matrix.M02) > Epsilon
            || Math.Abs(matrix.M12) > Epsilon
            || Math.Abs(matrix.M20) > Epsilon
            || Math.Abs(matrix.M21) > Epsilon)
            return false;

        var xLength = Math.Sqrt((matrix.M00 * matrix.M00) + (matrix.M01 * matrix.M01));
        var yLength = Math.Sqrt((matrix.M10 * matrix.M10) + (matrix.M11 * matrix.M11));
        if (xLength <= Epsilon || yLength <= Epsilon) return false;
        var tolerance = Epsilon * Math.Max(1d, Math.Max(xLength, yLength));
        if (Math.Abs(xLength - yLength) > tolerance) return false;

        var dot = (matrix.M00 * matrix.M10) + (matrix.M01 * matrix.M11);
        if (Math.Abs(dot) > tolerance * Math.Max(xLength, yLength)) return false;

        var determinant = (matrix.M00 * matrix.M11) - (matrix.M10 * matrix.M01);
        // Reflection changes text handedness and bulge orientation. Keep the first implementation to
        // proper rotations only rather than silently approximating mirrored custom graphics.
        if (determinant <= Epsilon) return false;

        var transform = new Transform2D(
            matrix.M00,
            matrix.M01,
            matrix.M10,
            matrix.M11,
            matrix.M30,
            matrix.M31);
        state = new ProxyTransformState(transform, xLength, Math.Atan2(matrix.M01, matrix.M00));
        return true;
    }

    private static CadProxyPrimitive ApplyTransform(CadProxyPrimitive primitive, ProxyTransformState state)
        => primitive switch
        {
            CadProxyPolyline polyline => new CadProxyPolyline(polyline.Points.Select(state.Transform.Apply).ToArray()),
            CadProxyLwPolyline polyline => new CadProxyLwPolyline(
                polyline.Points.Select(state.Transform.Apply).ToArray(),
                polyline.Bulges.ToArray(),
                polyline.IsClosed),
            CadProxyPolygon polygon => new CadProxyPolygon(polygon.Points.Select(state.Transform.Apply).ToArray()),
            CadProxyCircle circle => new CadProxyCircle(state.Transform.Apply(circle.Center), circle.Radius * state.Scale),
            CadProxyArc arc => new CadProxyArc(
                state.Transform.Apply(arc.Center),
                arc.Radius * state.Scale,
                NormalizeAngle(arc.StartRadians + state.RotationRadians),
                arc.SweepRadians),
            CadProxyText text => new CadProxyText(
                state.Transform.Apply(text.Origin),
                text.Text,
                text.Height * state.Scale,
                NormalizeAngle(text.RotationRadians + state.RotationRadians),
                text.WidthFactor,
                text.ObliqueAngleRadians,
                text.ProxyTextKind),
            _ => primitive
        };

    private static CadProxyPrimitive? MapOne(IProxyGeometry graphic)
        => graphic switch
        {
            // ProxyPolylineWithNormal derives from ProxyPolyline. Handle it first and do not let a
            // rejected non-planar instance fall through to the unguarded base-class arm.
            ProxyPolylineWithNormal polyline => IsPlanar(polyline.Normal) && TryPoints(polyline.Points, 2, out var points)
                ? new CadProxyPolyline(points)
                : null,
            ProxyPolyline polyline when TryPoints(polyline.Points, 2, out var points) => new CadProxyPolyline(points),
            ProxyLwPolyine polyline when TryLwPolyline(polyline, out var mappedPolyline) => mappedPolyline,
            ProxyPolygon polygon when TryPoints(polygon.Points, 3, out var points) => new CadProxyPolygon(points),
            ProxyCircle circle when IsPlanar(circle.Normal) && IsPositiveFinite(circle.Radius) && TryPoint(circle.Center, out var center)
                => new CadProxyCircle(center, circle.Radius),
            ProxyCirclePt3 circle when TryThreePointCircle(circle.Point1, circle.Point2, circle.Point3, out var center, out var radius)
                => new CadProxyCircle(center, radius),
            ProxyCircularArc arc when IsPlanar(arc.Normal) && IsPositiveFinite(arc.Radius) && TryPoint(arc.Center, out var center) && TryDirection(arc.StartVectorDirection, out var startRadians) && double.IsFinite(arc.SweepAngle)
                => new CadProxyArc(center, arc.Radius, startRadians, arc.Normal.Z < 0 ? -arc.SweepAngle : arc.SweepAngle),
            // Autodesk's 3-point primitive is start / point-on-arc / end. ArcType controls whether the
            // primitive is a simple arc, sector or chord. CadCore can losslessly represent only the
            // simple arc today, so sector/chord remain unsupported rather than silently losing fill.
            ProxyCircularArc3Pt arc when arc.ArcType == 0 && TryThreePointArc(
                arc.Point1,
                arc.Point2,
                arc.Point3,
                out var center,
                out var radius,
                out var startRadians,
                out var sweepRadians)
                => new CadProxyArc(center, radius, startRadians, sweepRadians),
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

    private static bool TryLwPolyline(ProxyLwPolyine proxy, out CadProxyLwPolyline mapped)
    {
        mapped = null!;
        var entity = proxy.Entity;
        if (entity is null
            || !IsPositiveFacingPlanar(entity.Normal)
            || entity.Vertices.Count < 2
            || !double.IsFinite(entity.Elevation)
            || !double.IsFinite(entity.ConstantWidth)
            || Math.Abs(entity.ConstantWidth) > Epsilon
            || !double.IsFinite(entity.Thickness)
            || Math.Abs(entity.Thickness) > Epsilon)
            return false;

        var points = new List<Point2D>(entity.Vertices.Count);
        var bulges = new List<double>(entity.Vertices.Count);
        foreach (var vertex in entity.Vertices)
        {
            if (!double.IsFinite(vertex.Location.X)
                || !double.IsFinite(vertex.Location.Y)
                || !double.IsFinite(vertex.Bulge)
                || !double.IsFinite(vertex.StartWidth)
                || !double.IsFinite(vertex.EndWidth)
                || Math.Abs(vertex.StartWidth) > Epsilon
                || Math.Abs(vertex.EndWidth) > Epsilon)
                return false;

            points.Add(new Point2D(vertex.Location.X, vertex.Location.Y));
            bulges.Add(vertex.Bulge);
        }

        mapped = new CadProxyLwPolyline(points, bulges, entity.IsClosed);
        return true;
    }

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

    private static bool TryThreePointCircle(
        CSMath.XYZ first,
        CSMath.XYZ second,
        CSMath.XYZ third,
        out Point2D center,
        out double radius)
    {
        center = Point2D.Origin;
        radius = 0;
        if (!TryHorizontalTriple(first, second, third, out var a, out var b, out var c)) return false;

        // Work in coordinates relative to the first point to reduce cancellation for large drawing
        // coordinates. Reject a scale-relative near-collinear triple instead of producing an unstable
        // huge-radius fallback.
        var ux = b.X - a.X;
        var uy = b.Y - a.Y;
        var vx = c.X - a.X;
        var vy = c.Y - a.Y;
        var u2 = (ux * ux) + (uy * uy);
        var v2 = (vx * vx) + (vy * vy);
        var cross = (ux * vy) - (uy * vx);
        if (!double.IsFinite(u2) || !double.IsFinite(v2) || !double.IsFinite(cross)) return false;

        var scale = Math.Sqrt(u2 * v2);
        if (!double.IsFinite(scale) || scale <= Epsilon || Math.Abs(cross) <= Epsilon * Math.Max(1d, scale)) return false;

        var denominator = 2d * cross;
        var offsetX = ((vy * u2) - (uy * v2)) / denominator;
        var offsetY = ((ux * v2) - (vx * u2)) / denominator;
        var centerX = a.X + offsetX;
        var centerY = a.Y + offsetY;
        if (!double.IsFinite(centerX) || !double.IsFinite(centerY)) return false;

        var computedRadius = Math.Sqrt((offsetX * offsetX) + (offsetY * offsetY));
        if (!IsPositiveFinite(computedRadius)) return false;

        center = new Point2D(centerX, centerY);
        radius = computedRadius;
        return true;
    }

    private static bool TryThreePointArc(
        CSMath.XYZ start,
        CSMath.XYZ pointOnArc,
        CSMath.XYZ end,
        out Point2D center,
        out double radius,
        out double startRadians,
        out double sweepRadians)
    {
        center = Point2D.Origin;
        radius = 0;
        startRadians = 0;
        sweepRadians = 0;
        if (!TryThreePointCircle(start, pointOnArc, end, out center, out radius)) return false;

        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        var middleAngle = Math.Atan2(pointOnArc.Y - center.Y, pointOnArc.X - center.X);
        var endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
        if (!double.IsFinite(startAngle) || !double.IsFinite(middleAngle) || !double.IsFinite(endAngle)) return false;

        var ccwToEnd = PositiveAngleDelta(startAngle, endAngle);
        var ccwToMiddle = PositiveAngleDelta(startAngle, middleAngle);
        var twoPi = Math.PI * 2d;
        if (ccwToEnd <= Epsilon
            || twoPi - ccwToEnd <= Epsilon
            || ccwToMiddle <= Epsilon
            || Math.Abs(ccwToMiddle - ccwToEnd) <= Epsilon)
            return false;

        // Exactly one directed arc from start to end contains the middle point. Select that sweep;
        // this preserves the ordering explicitly defined by Autodesk's 3-point circularArc contract.
        var sweep = ccwToMiddle < ccwToEnd ? ccwToEnd : -(twoPi - ccwToEnd);
        if (!double.IsFinite(sweep) || Math.Abs(sweep) <= Epsilon || Math.Abs(sweep) >= twoPi - Epsilon) return false;

        startRadians = NormalizeAngle(startAngle);
        sweepRadians = sweep;
        return true;
    }

    private static bool TryHorizontalTriple(
        CSMath.XYZ first,
        CSMath.XYZ second,
        CSMath.XYZ third,
        out Point2D a,
        out Point2D b,
        out Point2D c)
    {
        a = Point2D.Origin;
        b = Point2D.Origin;
        c = Point2D.Origin;
        if (!TryFinitePoint(first) || !TryFinitePoint(second) || !TryFinitePoint(third)) return false;

        var zScale = Math.Max(1d, Math.Max(Math.Abs(first.Z), Math.Max(Math.Abs(second.Z), Math.Abs(third.Z))));
        var zTolerance = Epsilon * zScale;
        if (Math.Abs(first.Z - second.Z) > zTolerance || Math.Abs(first.Z - third.Z) > zTolerance) return false;

        a = new Point2D(first.X, first.Y);
        b = new Point2D(second.X, second.Y);
        c = new Point2D(third.X, third.Y);
        return true;
    }

    private static bool TryFinitePoint(CSMath.XYZ point)
        => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

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

    private static double PositiveAngleDelta(double startRadians, double endRadians)
        => NormalizeAngle(endRadians - startRadians);

    private static double NormalizeAngle(double radians)
    {
        var twoPi = Math.PI * 2;
        var normalized = radians % twoPi;
        return normalized < 0 ? normalized + twoPi : normalized;
    }

    private readonly record struct ProxyTransformState(Transform2D Transform, double Scale, double RotationRadians)
    {
        public static ProxyTransformState Identity => new(Transform2D.Identity, 1d, 0d);

        public bool IsIdentity
            => Math.Abs(Transform.M11 - 1d) <= Epsilon
                && Math.Abs(Transform.M12) <= Epsilon
                && Math.Abs(Transform.M21) <= Epsilon
                && Math.Abs(Transform.M22 - 1d) <= Epsilon
                && Math.Abs(Transform.Dx) <= Epsilon
                && Math.Abs(Transform.Dy) <= Epsilon;
    }
}
