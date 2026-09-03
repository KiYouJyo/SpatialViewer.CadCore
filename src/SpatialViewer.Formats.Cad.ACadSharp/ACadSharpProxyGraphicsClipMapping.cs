using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Adds the evidence-backed 2D subset of ObjectARX clip-boundary semantics on top of the existing
/// proxy primitive mapper. Streams without clip commands delegate directly to the established mapper.
/// Clip streams remain fail-closed unless every state transition and clip transform can be represented
/// exactly by CadCore's 2D scene contract.
/// </summary>
public static class ACadSharpProxyGraphicsClipMapping
{
    private const double Epsilon = 1e-9;

    public static IReadOnlyList<CadProxyPrimitive> Map(
        IEnumerable<IProxyGeometry> graphics,
        out int unsupportedCount,
        out bool statefulGeometryCommandsPresent)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        var source = graphics.ToArray();
        if (!source.Any(graphic => graphic.GraphicsType is GraphicsType.PushClip or GraphicsType.PopClip))
            return ACadSharpProxyGraphicsMapping.Map(source, out unsupportedCount, out statefulGeometryCommandsPresent);

        statefulGeometryCommandsPresent = source.Any(graphic => IsStatefulGeometryCommand(graphic.GraphicsType));
        unsupportedCount = 0;
        var result = new List<CadProxyPrimitive>();
        var transformStack = new Stack<ProxyTransformState>();
        var clipStack = new Stack<ClipFrame>();
        var current = ProxyTransformState.Identity;

        foreach (var graphic in source)
        {
            switch (graphic)
            {
                case ProxyPushModelTransform push:
                    if (!TryPushTransform(push.TransformationMatrix, transformStack, ref current))
                        return FailClosed(source, out unsupportedCount);
                    continue;
                case ProxyPushModelTransform2 push:
                    if (!TryPushTransform(push.TransformationMatrix, transformStack, ref current))
                        return FailClosed(source, out unsupportedCount);
                    continue;
                case ProxyPopModelTransform:
                    if (transformStack.Count == 0)
                        return FailClosed(source, out unsupportedCount);
                    current = transformStack.Pop();
                    continue;
                case ProxyPushClip pushClip:
                    if (!TryCreateClipFrame(pushClip, current, out var frame))
                        return FailClosed(source, out unsupportedCount);
                    clipStack.Push(frame);
                    continue;
                case ProxyPopClip:
                    if (clipStack.Count == 0)
                        return FailClosed(source, out unsupportedCount);
                    var completed = clipStack.Pop();
                    if (completed.Children.Count > 0 || completed.DrawBoundary)
                    {
                        Append(
                            result,
                            clipStack,
                            new CadProxyClipGroup(completed.Polygon, completed.Children.ToArray(), completed.DrawBoundary));
                    }
                    continue;
            }

            var mapped = ACadSharpProxyGraphicsMapping.Map(
                new[] { graphic },
                out var primitiveUnsupported,
                out _);
            unsupportedCount += primitiveUnsupported;
            foreach (var primitive in mapped)
                Append(result, clipStack, current.IsIdentity ? primitive : ApplyTransform(primitive, current));
        }

        if (transformStack.Count != 0 || clipStack.Count != 0)
            return FailClosed(source, out unsupportedCount);

        return result;
    }

    private static void Append(List<CadProxyPrimitive> root, Stack<ClipFrame> clips, CadProxyPrimitive primitive)
    {
        if (clips.Count == 0) root.Add(primitive);
        else clips.Peek().Children.Add(primitive);
    }

    private static CadProxyPrimitive[] FailClosed(IProxyGeometry[] source, out int unsupportedCount)
    {
        unsupportedCount = source.Length;
        return Array.Empty<CadProxyPrimitive>();
    }

    private static bool TryCreateClipFrame(ProxyPushClip clip, ProxyTransformState current, out ClipFrame frame)
    {
        frame = null!;
        if (clip.FrontClipOn
            || clip.BackClipOn
            || !double.IsFinite(clip.FrontClip)
            || !double.IsFinite(clip.BackClip)
            || !IsPositiveFacingPlanar(clip.Extrusion)
            || !IsOrigin(clip.ClipBoundaryOrigin)
            || !IsIdentityMatrix(clip.ClipBoundaryTransformMatrix)
            || !InverseBlockTransformMatches(clip.InverseBlockTransformMatrix, current)
            || !TryBoundary(clip.ClipBoundary, out var localBoundary))
            return false;

        var transformed = new Point2D[localBoundary.Count];
        for (var index = 0; index < localBoundary.Count; index++)
        {
            transformed[index] = current.Transform.Apply(localBoundary[index]);
            if (!double.IsFinite(transformed[index].X) || !double.IsFinite(transformed[index].Y)) return false;
        }
        if (!IsUsefulPolygon(transformed)) return false;

        frame = new ClipFrame(transformed, clip.DrawBoundary);
        return true;
    }

    private static bool TryBoundary(List<CSMath.XY>? source, out IReadOnlyList<Point2D> boundary)
    {
        boundary = Array.Empty<Point2D>();
        if (source is null || source.Count < 2) return false;
        var points = new List<Point2D>(Math.Max(4, source.Count));
        foreach (var point in source)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
            points.Add(new Point2D(point.X, point.Y));
        }

        if (points.Count == 2)
        {
            var first = points[0];
            var second = points[1];
            var minX = Math.Min(first.X, second.X);
            var maxX = Math.Max(first.X, second.X);
            var minY = Math.Min(first.Y, second.Y);
            var maxY = Math.Max(first.Y, second.Y);
            if (maxX - minX <= Epsilon || maxY - minY <= Epsilon) return false;
            boundary = new[]
            {
                new Point2D(minX, minY),
                new Point2D(maxX, minY),
                new Point2D(maxX, maxY),
                new Point2D(minX, maxY)
            };
            return true;
        }

        if (points.Count > 3 && NearlySame(points[0], points[^1])) points.RemoveAt(points.Count - 1);
        if (!IsUsefulPolygon(points)) return false;
        boundary = points;
        return true;
    }

    private static bool IsUsefulPolygon(IReadOnlyList<Point2D> points)
    {
        if (points.Count < 3) return false;
        var bounds = BoundingBox2D.FromPoints(points);
        if (bounds.IsEmpty || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)) return false;
        var scale = Math.Max(1d, Math.Max(bounds.Width, bounds.Height));
        if (bounds.Width <= Epsilon * scale || bounds.Height <= Epsilon * scale) return false;

        var twiceArea = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var next = (index + 1) % points.Count;
            if (NearlySame(points[index], points[next])) return false;
            twiceArea += (points[index].X * points[next].Y) - (points[next].X * points[index].Y);
        }
        return double.IsFinite(twiceArea) && Math.Abs(twiceArea) > Epsilon * scale * scale;
    }

    private static bool NearlySame(Point2D first, Point2D second)
        => first.DistanceTo(second) <= Epsilon * Math.Max(1d, Math.Max(Math.Abs(first.X) + Math.Abs(first.Y), Math.Abs(second.X) + Math.Abs(second.Y)));

    private static bool IsOrigin(CSMath.XYZ point)
        => double.IsFinite(point.X)
            && double.IsFinite(point.Y)
            && double.IsFinite(point.Z)
            && Math.Abs(point.X) <= Epsilon
            && Math.Abs(point.Y) <= Epsilon
            && Math.Abs(point.Z) <= Epsilon;

    private static bool IsPositiveFacingPlanar(CSMath.XYZ normal)
        => double.IsFinite(normal.X)
            && double.IsFinite(normal.Y)
            && double.IsFinite(normal.Z)
            && Math.Abs(normal.X) <= Epsilon
            && Math.Abs(normal.Y) <= Epsilon
            && Math.Abs(normal.Z - 1d) <= Epsilon;

    private static bool InverseBlockTransformMatches(CSMath.Matrix4 matrix, ProxyTransformState current)
    {
        if (!TryPlanarSimilarity(matrix, out var mapped)) return false;
        if (!current.Transform.TryInvert(out var expected)) return false;
        return SameTransform(mapped.Transform, expected);
    }

    private static bool SameTransform(Transform2D first, Transform2D second)
    {
        var scale = Math.Max(1d, new[]
        {
            Math.Abs(first.M11), Math.Abs(first.M12), Math.Abs(first.M21), Math.Abs(first.M22), Math.Abs(first.Dx), Math.Abs(first.Dy),
            Math.Abs(second.M11), Math.Abs(second.M12), Math.Abs(second.M21), Math.Abs(second.M22), Math.Abs(second.Dx), Math.Abs(second.Dy)
        }.Max());
        var tolerance = Epsilon * scale;
        return Math.Abs(first.M11 - second.M11) <= tolerance
            && Math.Abs(first.M12 - second.M12) <= tolerance
            && Math.Abs(first.M21 - second.M21) <= tolerance
            && Math.Abs(first.M22 - second.M22) <= tolerance
            && Math.Abs(first.Dx - second.Dx) <= tolerance
            && Math.Abs(first.Dy - second.Dy) <= tolerance;
    }

    private static bool IsIdentityMatrix(CSMath.Matrix4 matrix)
    {
        var expected = CSMath.Matrix4.Identity;
        return MatrixElementNear(matrix.M00, expected.M00)
            && MatrixElementNear(matrix.M01, expected.M01)
            && MatrixElementNear(matrix.M02, expected.M02)
            && MatrixElementNear(matrix.M03, expected.M03)
            && MatrixElementNear(matrix.M10, expected.M10)
            && MatrixElementNear(matrix.M11, expected.M11)
            && MatrixElementNear(matrix.M12, expected.M12)
            && MatrixElementNear(matrix.M13, expected.M13)
            && MatrixElementNear(matrix.M20, expected.M20)
            && MatrixElementNear(matrix.M21, expected.M21)
            && MatrixElementNear(matrix.M22, expected.M22)
            && MatrixElementNear(matrix.M23, expected.M23)
            && MatrixElementNear(matrix.M30, expected.M30)
            && MatrixElementNear(matrix.M31, expected.M31)
            && MatrixElementNear(matrix.M32, expected.M32)
            && MatrixElementNear(matrix.M33, expected.M33);
    }

    private static bool MatrixElementNear(double value, double expected)
        => double.IsFinite(value) && Math.Abs(value - expected) <= Epsilon;

    private static bool TryPushTransform(CSMath.Matrix4 matrix, Stack<ProxyTransformState> stack, ref ProxyTransformState current)
    {
        if (!TryPlanarSimilarity(matrix, out var pushed)) return false;
        stack.Push(current);
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
        if (determinant <= Epsilon) return false;

        var transform = new Transform2D(matrix.M00, matrix.M01, matrix.M10, matrix.M11, matrix.M30, matrix.M31);
        state = new ProxyTransformState(transform, xLength, Math.Atan2(matrix.M01, matrix.M00));
        return true;
    }

    private static CadProxyPrimitive ApplyTransform(CadProxyPrimitive primitive, ProxyTransformState state)
        => primitive switch
        {
            CadProxyPolyline polyline => new CadProxyPolyline(polyline.Points.Select(state.Transform.Apply).ToArray()),
            CadProxyLwPolyline polyline => new CadProxyLwPolyline(polyline.Points.Select(state.Transform.Apply).ToArray(), polyline.Bulges.ToArray(), polyline.IsClosed),
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

    private static bool IsStatefulGeometryCommand(GraphicsType type)
        => type is GraphicsType.PushModelTransform
            or GraphicsType.PushModelTransform2
            or GraphicsType.PopModelTransform
            or GraphicsType.PushClip
            or GraphicsType.PopClip;

    private static double NormalizeAngle(double radians)
    {
        var twoPi = Math.PI * 2;
        var normalized = radians % twoPi;
        return normalized < 0 ? normalized + twoPi : normalized;
    }

    private sealed class ClipFrame
    {
        public ClipFrame(IReadOnlyList<Point2D> polygon, bool drawBoundary)
        {
            Polygon = polygon;
            DrawBoundary = drawBoundary;
        }

        public IReadOnlyList<Point2D> Polygon { get; }
        public bool DrawBoundary { get; }
        public List<CadProxyPrimitive> Children { get; } = new();
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
