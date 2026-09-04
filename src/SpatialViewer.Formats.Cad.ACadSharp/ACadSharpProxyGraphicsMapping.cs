using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Converts the safe 2D subset of ACadSharp proxy graphics into reader-independent CadCore primitives.
/// Balanced planar model-transform stacks are applied for translation, rotation and uniform scaling.
/// Supported ObjectARX subentity color/true-color/line-weight states are snapshotted onto each emitted
/// primitive. Clip commands, malformed stacks and non-planar/non-similarity transforms remain fail-closed
/// so a fallback is never drawn at a plausible-but-wrong location.
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

        // Clip state changes visibility rather than just coordinates. The clip-aware mapper owns that
        // state machine; this legacy entry point remains fail-closed when clip commands are present.
        if (source.Any(graphic => graphic.GraphicsType is GraphicsType.PushClip or GraphicsType.PopClip))
            return FailClosed(source, out unsupportedCount);

        var result = new List<CadProxyPrimitive>();
        var stack = new Stack<ProxyTransformState>();
        var current = ProxyTransformState.Identity;
        var traits = default(CadProxyTraits);
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

            if (IsHandledTraitCommand(graphic))
            {
                if (!TryApplyTraitCommand(graphic, ref traits)) unsupportedCount++;
                continue;
            }

            var mapped = MapOne(graphic, traits);
            if (mapped is null)
            {
                unsupportedCount++;
                continue;
            }

            var transformed = current.IsIdentity ? mapped : ApplyTransform(mapped, current);
            result.Add(transformed with { Traits = traits });
        }

        if (stack.Count != 0)
            return FailClosed(source, out unsupportedCount);

        return result;
    }

    internal static bool IsHandledTraitCommand(IProxyGeometry graphic)
        => graphic is ProxySubentColor or ProxySubentTrueColor or ProxySubentLineWeight or ProxySubentFillon;

    internal static bool TryApplyTraitCommand(IProxyGeometry graphic, ref CadProxyTraits traits)
    {
        switch (graphic)
        {
            case ProxySubentColor color:
                if (color.ColorIndex is 0 or 256)
                {
                    traits = traits with { Color = null };
                    return true;
                }
                if (color.ColorIndex is >= 1 and <= 255)
                {
                    traits = traits with { Color = CadColor.FromAci(color.ColorIndex) };
                    return true;
                }
                // An unknown color state must not leave a stale previous override active.
                traits = traits with { Color = null };
                return false;

            case ProxySubentTrueColor trueColor:
                switch (trueColor.ColorMethod)
                {
                    case ProxyColorMethod.ByLayer:
                    case ProxyColorMethod.ByBlock:
                        traits = traits with { Color = null };
                        return true;
                    case ProxyColorMethod.ByColor when trueColor.Color.IsTrueColor:
                        traits = traits with { Color = CadColor.FromRgb(trueColor.Color.R, trueColor.Color.G, trueColor.Color.B) };
                        return true;
                    case ProxyColorMethod.ByACI when trueColor.Color.Index is >= 1 and <= 255:
                        traits = traits with { Color = CadColor.FromAci(trueColor.Color.Index) };
                        return true;
                    default:
                        // Foreground/None/unknown methods are not assigned guessed CAD colors.
                        traits = traits with { Color = null };
                        return false;
                }

            case ProxySubentFillon fill:
                traits = traits with { FillOn = fill.IsOn };
                return true;

            case ProxySubentLineWeight lineWeight:
                switch (lineWeight.LineWeight)
                {
                    case global::ACadSharp.LineWeightType.ByLayer:
                    case global::ACadSharp.LineWeightType.ByBlock:
                    case global::ACadSharp.LineWeightType.Default:
                        traits = traits with { LineWeight = null };
                        return true;
                    case global::ACadSharp.LineWeightType.ByDIPs:
                        traits = traits with { LineWeight = null };
                        return false;
                    default:
                        var value = (int)lineWeight.LineWeight;
                        if (value >= 0 && Enum.IsDefined(typeof(global::ACadSharp.LineWeightType), lineWeight.LineWeight))
                        {
                            traits = traits with { LineWeight = value };
                            return true;
                        }
                        traits = traits with { LineWeight = null };
                        return false;
                }

            default:
                return false;
        }
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
    {
        var transformed = primitive switch
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
            CadProxyEdgeSet edgeSet => new CadProxyEdgeSet(
                edgeSet.Edges.Select(edge => edge with
                {
                    Start = state.Transform.Apply(edge.Start),
                    End = state.Transform.Apply(edge.End)
                }).ToArray(),
                edgeSet.ProxyEdgeKind),
            CadProxySurfaceSet surface => new CadProxySurfaceSet(
                surface.Faces.Select(face => face with
                {
                    Points = face.Points.Select(state.Transform.Apply).ToArray()
                }).ToArray(),
                surface.Edges.Select(edge => edge with
                {
                    Start = state.Transform.Apply(edge.Start),
                    End = state.Transform.Apply(edge.End)
                }).ToArray(),
                surface.ProxySurfaceKind),
            CadProxyText text => new CadProxyText(
                state.Transform.Apply(text.Origin),
                text.Text,
                text.Height * state.Scale,
                NormalizeAngle(text.RotationRadians + state.RotationRadians),
                text.WidthFactor,
                text.ObliqueAngleRadians,
                text.ProxyTextKind,
                text.FontFileName,
                text.BigFontFileName,
                text.Typeface,
                text.TrackingPercentage,
                text.IsBackward,
                text.IsUpsideDown,
                text.IsVertical,
                text.IsRaw,
                text.IsUnderlined,
                text.IsOverlined),
            _ => primitive
        };
        return transformed with { Traits = primitive.Traits };
    }

    internal static CadProxyPrimitive? MapOne(IProxyGeometry graphic, CadProxyTraits traits)
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
            ProxyMesh mesh when traits.FillOn == true && TryMeshSurface(mesh, out var meshSurface) => meshSurface,
            ProxyMesh mesh when TryMeshEdges(mesh, out var meshEdges) => meshEdges,
            ProxyShell shell when traits.FillOn == true && TryShellSurface(shell, out var shellSurface) => shellSurface,
            ProxyShell shell when TryShellEdges(shell, out var shellEdges) => shellEdges,
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
            ProxyText2 text when TryProxyText(
                text.Normal,
                text.StartPoint,
                text.TextDirection,
                text.Text,
                text.Height,
                text.WidthFactor,
                text.ObliqueAngle,
                nameof(GraphicsType.Text2),
                out var mappedText) => mappedText with
                {
                    FontFileName = text.FontFilename ?? string.Empty,
                    BigFontFileName = text.BigFontFilename ?? string.Empty,
                    TrackingPercentage = text.TrackingPercentage,
                    IsBackward = text.IsBackwards,
                    IsUpsideDown = text.IsUpsideDown,
                    IsVertical = text.IsVertical,
                    IsRaw = text.IsRaw,
                    IsUnderlined = text.IsUnderlined,
                    IsOverlined = text.IsOverlined
                },
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
            ProxyUnicodeText2 text when TryProxyText(
                text.Normal,
                text.StartPoint,
                text.TextDirection,
                text.Text,
                text.Height,
                text.WidthFactor,
                text.ObliqueAngle,
                nameof(GraphicsType.UnicodeText2),
                out var mappedText) => mappedText with
                {
                    FontFileName = text.FontDescriptor?.FontFilename ?? string.Empty,
                    BigFontFileName = text.BigFontFilename ?? string.Empty,
                    Typeface = text.FontDescriptor?.Typeface ?? string.Empty,
                    TrackingPercentage = text.TrackingPercentage,
                    IsBackward = text.IsBackwards,
                    IsUpsideDown = text.IsUpsideDown,
                    IsVertical = text.IsVertical,
                    IsRaw = text.IsRaw,
                    IsUnderlined = text.IsUnderlined,
                    IsOverlined = text.IsOverlined
                },
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

    private static bool TryMeshSurface(ProxyMesh mesh, out CadProxySurfaceSet mapped)
    {
        mapped = null!;
        if (!TryMeshEdges(mesh, out var edgeSet)) return false;
        if (mesh.RowCount < 2 || mesh.ColumnCount < 2) return false;
        if (!TryCoplanarVertices(mesh.Vertices, out var vertices)) return false;

        var faceCountLong = (long)(mesh.RowCount - 1) * (mesh.ColumnCount - 1);
        if (faceCountLong <= 0 || faceCountLong > int.MaxValue) return false;
        var faceCount = (int)faceCountLong;
        if (!TryFaceEvidence(mesh.FaceTraits, faceCount, out var evidence)) return false;

        var faces = new List<CadProxyFace>(faceCount);
        var faceIndex = 0;
        for (var row = 0; row < mesh.RowCount - 1; row++)
        {
            var rowStart = row * mesh.ColumnCount;
            for (var column = 0; column < mesh.ColumnCount - 1; column++)
            {
                var lowerLeft = rowStart + column;
                var lowerRight = lowerLeft + 1;
                var upperLeft = lowerLeft + mesh.ColumnCount;
                var upperRight = upperLeft + 1;
                var points = new[]
                {
                    vertices[lowerLeft],
                    vertices[lowerRight],
                    vertices[upperRight],
                    vertices[upperLeft]
                };
                if (!TryAddFace(points, evidence[faceIndex++], faces)) return false;
            }
        }

        if (faceIndex != faceCount || faces.Count == 0) return false;
        mapped = new CadProxySurfaceSet(faces, edgeSet.Edges, "MeshSurface");
        return true;
    }

    private static bool TryShellSurface(ProxyShell shell, out CadProxySurfaceSet mapped)
    {
        mapped = null!;
        if (!TryShellEdges(shell, out var edgeSet)) return false;
        if (!TryCoplanarVertices(shell.Vertices, out var vertices)) return false;
        if (shell.Faces.Count == 0) return false;
        if (!TryFaceEvidence(shell.FaceTraits, shell.Faces.Count, out var evidence)) return false;

        var faces = new List<CadProxyFace>(shell.Faces.Count);
        for (var faceIndex = 0; faceIndex < shell.Faces.Count; faceIndex++)
        {
            var sourceFace = shell.Faces[faceIndex];
            if (sourceFace is null || sourceFace.Length < 3) return false;
            var points = new Point2D[sourceFace.Length];
            for (var index = 0; index < sourceFace.Length; index++)
            {
                var vertexIndex = sourceFace[index];
                if (vertexIndex < 0 || vertexIndex >= vertices.Count) return false;
                points[index] = vertices[vertexIndex];
            }
            if (!TryAddFace(points, evidence[faceIndex], faces)) return false;
        }

        if (faces.Count == 0) return false;
        mapped = new CadProxySurfaceSet(faces, edgeSet.Edges, "ShellSurface");
        return true;
    }

    private static bool TryFaceEvidence(
        FaceTraits? traits,
        int faceCount,
        out IReadOnlyList<CadProxyFaceEvidence> evidence)
    {
        var result = new CadProxyFaceEvidence[faceCount];
        evidence = result;
        if (traits is null) return true;

        if (!TraitCountMatches(traits.Colors.Count, faceCount)
            || !TraitCountMatches(traits.LayerHandles.Count, faceCount)
            || !TraitCountMatches(traits.MakerIds.Count, faceCount)
            || !TraitCountMatches(traits.VisibilityIndicators.Count, faceCount)
            || !TraitCountMatches(traits.Normals.Count, faceCount))
            return false;

        for (var index = 0; index < faceCount; index++)
        {
            var rawColor = traits.Colors.Count == 0 ? (int?)null : traits.Colors[index];
            CadColor? color = rawColor is >= 1 and <= 255 ? CadColor.FromAci(rawColor.Value) : null;
            ulong? layerReference = traits.LayerHandles.Count == 0 ? null : traits.LayerHandles[index];
            if (layerReference > uint.MaxValue) return false;

            var visibility = traits.VisibilityIndicators.Count == 0 ? (int?)null : traits.VisibilityIndicators[index];
            if (visibility is not null && visibility is not (0 or 1 or 2)) return false;

            if (traits.Normals.Count > 0)
            {
                var normal = traits.Normals[index];
                if (!double.IsFinite(normal.X)
                    || !double.IsFinite(normal.Y)
                    || !double.IsFinite(normal.Z)
                    || Math.Abs(normal.X) > Epsilon
                    || Math.Abs(normal.Y) > Epsilon
                    || Math.Abs(Math.Abs(normal.Z) - 1d) > Epsilon)
                    return false;
            }

            result[index] = new CadProxyFaceEvidence(
                rawColor,
                color,
                layerReference,
                traits.MakerIds.Count == 0 ? null : traits.MakerIds[index],
                visibility);
        }

        return true;
    }

    private static bool TryAddFace(
        IReadOnlyList<Point2D> points,
        CadProxyFaceEvidence evidence,
        List<CadProxyFace> faces)
    {
        if (points.Count < 3
            || points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            return false;

        var twiceArea = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var next = (index + 1) % points.Count;
            twiceArea += (points[index].X * points[next].Y) - (points[next].X * points[index].Y);
        }
        if (!double.IsFinite(twiceArea) || Math.Abs(twiceArea) <= Epsilon) return false;

        faces.Add(new CadProxyFace(points.ToArray(), evidence));
        return true;
    }

    private static bool TryMeshEdges(ProxyMesh mesh, out CadProxyEdgeSet mapped)
    {
        mapped = null!;
        if (mesh.RowCount <= 0 || mesh.ColumnCount <= 0) return false;

        var expectedVertexCount = (long)mesh.RowCount * mesh.ColumnCount;
        if (expectedVertexCount != mesh.Vertices.Count || expectedVertexCount > int.MaxValue) return false;
        if (!TryCoplanarVertices(mesh.Vertices, out var vertices)) return false;

        var rowEdgeCount = (long)mesh.RowCount * Math.Max(0, mesh.ColumnCount - 1);
        var columnEdgeCount = (long)Math.Max(0, mesh.RowCount - 1) * mesh.ColumnCount;
        var expectedEdgeCountLong = rowEdgeCount + columnEdgeCount;
        if (expectedEdgeCountLong <= 0 || expectedEdgeCountLong > int.MaxValue) return false;
        var expectedEdgeCount = (int)expectedEdgeCountLong;

        if (!TryEdgeEvidence(mesh.EdgeTraits, expectedEdgeCount, out var evidence)) return false;
        var edges = new List<CadProxyEdgeSegment>(expectedEdgeCount);
        var edgeIndex = 0;

        // AcGiGeometry::mesh defines edge-data order as all row edges first, then all column edges.
        for (var row = 0; row < mesh.RowCount; row++)
        {
            var rowStart = row * mesh.ColumnCount;
            for (var column = 0; column < mesh.ColumnCount - 1; column++)
            {
                var first = rowStart + column;
                var second = first + 1;
                if (!TryAddEdge(vertices[first], vertices[second], evidence[edgeIndex++], edges)) return false;
            }
        }
        for (var row = 0; row < mesh.RowCount - 1; row++)
        {
            var rowStart = row * mesh.ColumnCount;
            for (var column = 0; column < mesh.ColumnCount; column++)
            {
                var first = rowStart + column;
                var second = first + mesh.ColumnCount;
                if (!TryAddEdge(vertices[first], vertices[second], evidence[edgeIndex++], edges)) return false;
            }
        }

        if (edgeIndex != expectedEdgeCount || edges.Count == 0) return false;
        mapped = new CadProxyEdgeSet(edges, "MeshEdges");
        return true;
    }

    private static bool TryShellEdges(ProxyShell shell, out CadProxyEdgeSet mapped)
    {
        mapped = null!;
        if (shell.Vertices.Count < 2 || shell.Faces.Count == 0) return false;
        if (!TryCoplanarVertices(shell.Vertices, out var vertices)) return false;

        long expectedEdgeCountLong = 0;
        foreach (var face in shell.Faces)
        {
            if (face is null || face.Length < 2) return false;
            expectedEdgeCountLong += face.Length;
            if (expectedEdgeCountLong > int.MaxValue) return false;
            foreach (var vertexIndex in face)
                if (vertexIndex < 0 || vertexIndex >= vertices.Count) return false;
        }
        if (expectedEdgeCountLong <= 0) return false;
        var expectedEdgeCount = (int)expectedEdgeCountLong;
        if (!TryEdgeEvidence(shell.EdgeTraits, expectedEdgeCount, out var evidence)) return false;

        var edges = new List<CadProxyEdgeSegment>(expectedEdgeCount);
        var edgeIndex = 0;
        // Shell edge data follows face-list traversal. ACadSharp retains one edge-trait slot for each
        // face boundary entry, including shared boundaries, so preserve that order rather than dedupe.
        foreach (var face in shell.Faces)
        {
            for (var index = 0; index < face.Length; index++)
            {
                var next = (index + 1) % face.Length;
                if (!TryAddEdge(vertices[face[index]], vertices[face[next]], evidence[edgeIndex++], edges)) return false;
            }
        }

        if (edgeIndex != expectedEdgeCount || edges.Count == 0) return false;
        mapped = new CadProxyEdgeSet(edges, "ShellEdges");
        return true;
    }

    private static bool TryCoplanarVertices(
        List<CSMath.XYZ> source,
        out IReadOnlyList<Point2D> vertices)
    {
        vertices = Array.Empty<Point2D>();
        if (source.Count == 0) return false;

        var first = source[0];
        if (!TryFinitePoint(first)) return false;
        var zScale = Math.Max(1d, Math.Abs(first.Z));
        for (var index = 1; index < source.Count; index++)
        {
            var point = source[index];
            if (!TryFinitePoint(point)) return false;
            zScale = Math.Max(zScale, Math.Abs(point.Z));
        }

        var zTolerance = Epsilon * zScale;
        var mapped = new Point2D[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            var point = source[index];
            if (Math.Abs(point.Z - first.Z) > zTolerance) return false;
            mapped[index] = new Point2D(point.X, point.Y);
        }
        vertices = mapped;
        return true;
    }

    private static bool TryEdgeEvidence(
        EdgeTraits? traits,
        int edgeCount,
        out IReadOnlyList<CadProxyEdgeEvidence> evidence)
    {
        var result = new CadProxyEdgeEvidence[edgeCount];
        evidence = result;
        if (traits is null) return true;

        if (!TraitCountMatches(traits.Colors.Count, edgeCount)
            || !TraitCountMatches(traits.LayerHandles.Count, edgeCount)
            || !TraitCountMatches(traits.LineTypeHandles.Count, edgeCount)
            || !TraitCountMatches(traits.MakerIds.Count, edgeCount)
            || !TraitCountMatches(traits.VisibilityIndicators.Count, edgeCount))
            return false;

        for (var index = 0; index < edgeCount; index++)
        {
            var rawColor = traits.Colors.Count == 0 ? (int?)null : traits.Colors[index];
            CadColor? color = rawColor is >= 1 and <= 255 ? CadColor.FromAci(rawColor.Value) : null;

            ulong? layerReference = traits.LayerHandles.Count == 0 ? null : traits.LayerHandles[index];
            ulong? lineTypeReference = traits.LineTypeHandles.Count == 0 ? null : traits.LineTypeHandles[index];
            if (layerReference > uint.MaxValue || lineTypeReference > uint.MaxValue) return false;

            var visibility = traits.VisibilityIndicators.Count == 0 ? (int?)null : traits.VisibilityIndicators[index];
            if (visibility is not null && visibility is not (0 or 1 or 2)) return false;

            result[index] = new CadProxyEdgeEvidence(
                rawColor,
                color,
                layerReference,
                lineTypeReference,
                traits.MakerIds.Count == 0 ? null : traits.MakerIds[index],
                visibility);
        }
        return true;
    }

    private static bool TraitCountMatches(int count, int edgeCount) => count == 0 || count == edgeCount;

    private static bool TryAddEdge(
        Point2D start,
        Point2D end,
        CadProxyEdgeEvidence evidence,
        List<CadProxyEdgeSegment> edges)
    {
        if (!double.IsFinite(start.X)
            || !double.IsFinite(start.Y)
            || !double.IsFinite(end.X)
            || !double.IsFinite(end.Y)
            || start.DistanceTo(end) <= Epsilon)
            return false;
        edges.Add(new CadProxyEdgeSegment(start, end, evidence));
        return true;
    }

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
