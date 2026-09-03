using System.Globalization;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

public sealed partial class CadSceneTranslator
{
    private static SceneNode DimensionNode(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var presentation = dimension.Presentation;
        var dimensionLineStyle = DimensionComponentStyle(dimension.DimensionLineColor, style);
        var extensionLineStyle = DimensionComponentStyle(dimension.ExtensionLineColor, style);
        var textStyle = DimensionComponentStyle(dimension.TextColor, style);
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["DimensionKind"] = dimension.Kind.ToString(),
            ["DimensionMeasurement"] = dimension.Measurement.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionStyle"] = dimension.StyleName,
            ["DimensionSemantic"] = bool.TrueString,
            ["DimensionExtensionLineOffset"] = presentation.ExtensionLineOffset.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionExtensionLineExtension"] = presentation.ExtensionLineExtension.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionLineExtension"] = presentation.DimensionLineExtension.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionLineGap"] = presentation.DimensionLineGap.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionSuppressFirstExtensionLine"] = presentation.SuppressFirstExtensionLine.ToString(),
            ["DimensionSuppressSecondExtensionLine"] = presentation.SuppressSecondExtensionLine.ToString(),
            ["DimensionSuppressFirstDimensionLine"] = presentation.SuppressFirstDimensionLine.ToString(),
            ["DimensionSuppressSecondDimensionLine"] = presentation.SuppressSecondDimensionLine.ToString(),
            ["DimensionArrowBlock"] = presentation.ArrowBlockName,
            ["DimensionFirstArrowBlock"] = presentation.FirstArrowBlockName,
            ["DimensionSecondArrowBlock"] = presentation.SecondArrowBlockName,
            ["DimensionSeparateArrowBlocks"] = presentation.SeparateArrowBlocks.ToString(),
            ["DimensionDecimalPlaces"] = presentation.DecimalPlaces.ToString(CultureInfo.InvariantCulture),
            ["DimensionDecimalSeparator"] = presentation.DecimalSeparator.ToString(),
            ["DimensionRounding"] = presentation.Rounding.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionPrefix"] = presentation.Prefix,
            ["DimensionSuffix"] = presentation.Suffix,
            ["DimensionTolerances"] = presentation.GenerateTolerances.ToString(),
            ["DimensionLimits"] = presentation.LimitsGeneration.ToString(),
            ["DimensionPlusTolerance"] = presentation.PlusTolerance.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionMinusTolerance"] = presentation.MinusTolerance.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionToleranceDecimalPlaces"] = presentation.ToleranceDecimalPlaces.ToString(CultureInfo.InvariantCulture),
            ["DimensionToleranceScaleFactor"] = presentation.ToleranceScaleFactor.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionAlternateUnits"] = presentation.AlternateUnitsEnabled.ToString(),
            ["DimensionAlternateUnitScale"] = presentation.AlternateUnitScaleFactor.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionAlternateUnitDecimalPlaces"] = presentation.AlternateUnitDecimalPlaces.ToString(CultureInfo.InvariantCulture),
            ["DimensionAlternateUnitSuffix"] = presentation.AlternateUnitSuffix,
            ["DimensionLinearUnitFormat"] = presentation.LinearUnitFormat,
            ["DimensionAngularUnitFormat"] = presentation.AngularUnitFormat,
            ["DimensionLineResolvedStroke"] = dimensionLineStyle.Stroke,
            ["DimensionExtensionLineResolvedStroke"] = extensionLineStyle.Stroke,
            ["DimensionTextResolvedStroke"] = textStyle.Stroke
        };
        var customArrowRequested = presentation.SeparateArrowBlocks
            ? !string.IsNullOrWhiteSpace(presentation.FirstArrowBlockName) || !string.IsNullOrWhiteSpace(presentation.SecondArrowBlockName) || !string.IsNullOrWhiteSpace(presentation.ArrowBlockName)
            : !string.IsNullOrWhiteSpace(presentation.ArrowBlockName);
        enriched["DimensionCustomArrowRequested"] = customArrowRequested.ToString();
        enriched["DimensionCustomArrowFallbackApplied"] = customArrowRequested.ToString();

        var children = new List<SceneNode>();
        switch (dimension.Kind)
        {
            case CadDimensionKind.Linear:
            case CadDimensionKind.Aligned:
                AddLinearDimension(dimension, dimensionLineStyle, extensionLineStyle, enriched, children);
                break;
            case CadDimensionKind.Radius:
            case CadDimensionKind.Diameter:
                AddRadialDimension(dimension, dimensionLineStyle, enriched, children);
                break;
            case CadDimensionKind.Angular2Line:
            case CadDimensionKind.Angular3Point:
                AddAngularDimension(dimension, dimensionLineStyle, extensionLineStyle, enriched, children);
                break;
            case CadDimensionKind.Ordinate:
                AddOrdinateDimension(dimension, dimensionLineStyle, enriched, children);
                break;
            default:
                AddReferencePath(dimension, dimensionLineStyle, enriched, children);
                break;
        }
        if (!string.IsNullOrWhiteSpace(dimension.Text) && dimension.TextHeight > double.Epsilon)
        {
            children.Add(TextNode(dimension.ObjectId, dimension.TextPosition, dimension.Text, dimension.TextHeight, dimension.RotationRadians, textStyle, enriched));
        }
        return new SceneNode(dimension.ObjectId, style: dimensionLineStyle, children: children, metadata: enriched);
    }

    private static SceneStyle DimensionComponentStyle(CadColor? componentColor, SceneStyle inherited)
    {
        if (componentColor is not { } color || color.Kind is CadColorKind.ByLayer or CadColorKind.ByBlock) return inherited;
        return inherited with { Stroke = ToHex(color) };
    }

    private static void AddLinearDimension(CadDimensionEntity dimension, SceneStyle dimensionLineStyle, SceneStyle extensionLineStyle, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        if (!TryReference(dimension, "FirstPoint", out var first) || !TryReference(dimension, "SecondPoint", out var second))
        {
            AddReferencePath(dimension, dimensionLineStyle, metadata, children);
            return;
        }

        Point2D dimFirst;
        Point2D dimSecond;
        if (dimension.Kind == CadDimensionKind.Linear)
        {
            var axis = new Point2D(Math.Cos(dimension.RotationRadians), Math.Sin(dimension.RotationRadians));
            var delta = new Point2D(first.X - second.X, first.Y - second.Y);
            var along = Dot(delta, axis);
            dimSecond = dimension.DefinitionPoint;
            dimFirst = new Point2D(dimSecond.X + (axis.X * along), dimSecond.Y + (axis.Y * along));
        }
        else
        {
            var direction = Normalize(new Point2D(second.X - first.X, second.Y - first.Y));
            var perpendicular = new Point2D(-direction.Y, direction.X);
            var offset = Dot(new Point2D(dimension.DefinitionPoint.X - second.X, dimension.DefinitionPoint.Y - second.Y), perpendicular);
            dimFirst = new Point2D(first.X + (perpendicular.X * offset), first.Y + (perpendicular.Y * offset));
            dimSecond = new Point2D(second.X + (perpendicular.X * offset), second.Y + (perpendicular.Y * offset));
        }

        var presentation = dimension.Presentation;
        AddExtensionLine(children, dimension.ObjectId, first, dimFirst, presentation.ExtensionLineOffset, presentation.ExtensionLineExtension, presentation.SuppressFirstExtensionLine, extensionLineStyle, metadata);
        AddExtensionLine(children, dimension.ObjectId, second, dimSecond, presentation.ExtensionLineOffset, presentation.ExtensionLineExtension, presentation.SuppressSecondExtensionLine, extensionLineStyle, metadata);

        var dimensionDirection = Normalize(new Point2D(dimSecond.X - dimFirst.X, dimSecond.Y - dimFirst.Y));
        var lineStart = new Point2D(dimFirst.X - (dimensionDirection.X * presentation.DimensionLineExtension), dimFirst.Y - (dimensionDirection.Y * presentation.DimensionLineExtension));
        var lineEnd = new Point2D(dimSecond.X + (dimensionDirection.X * presentation.DimensionLineExtension), dimSecond.Y + (dimensionDirection.Y * presentation.DimensionLineExtension));
        AddDimensionLine(children, dimension.ObjectId, lineStart, lineEnd, dimension.TextPosition, presentation.SuppressFirstDimensionLine, presentation.SuppressSecondDimensionLine, dimensionLineStyle, metadata);

        var firstArrowMetadata = ArrowMetadata(metadata, presentation, first: true);
        var secondArrowMetadata = ArrowMetadata(metadata, presentation, first: false);
        AddArrow(children, dimension.ObjectId, dimFirst, dimSecond, dimension.ArrowSize, dimensionLineStyle, firstArrowMetadata);
        AddArrow(children, dimension.ObjectId, dimSecond, dimFirst, dimension.ArrowSize, dimensionLineStyle, secondArrowMetadata);
    }

    private static void AddExtensionLine(List<SceneNode> children, ObjectId id, Point2D origin, Point2D dimensionPoint, double offset, double extension, bool suppressed, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        if (suppressed) return;
        var direction = Normalize(new Point2D(dimensionPoint.X - origin.X, dimensionPoint.Y - origin.Y));
        if (direction == Point2D.Origin) return;
        var safeOffset = double.IsFinite(offset) ? Math.Max(0, offset) : 0;
        var safeExtension = double.IsFinite(extension) ? Math.Max(0, extension) : 0;
        var start = new Point2D(origin.X + (direction.X * safeOffset), origin.Y + (direction.Y * safeOffset));
        var end = new Point2D(dimensionPoint.X + (direction.X * safeExtension), dimensionPoint.Y + (direction.Y * safeExtension));
        children.Add(LineNode(id, start, end, style, metadata));
    }

    private static void AddDimensionLine(List<SceneNode> children, ObjectId id, Point2D first, Point2D second, Point2D textPosition, bool suppressFirst, bool suppressSecond, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        if (suppressFirst && suppressSecond) return;
        if (!suppressFirst && !suppressSecond)
        {
            children.Add(LineNode(id, first, second, style, metadata));
            return;
        }

        var split = ProjectToSegment(textPosition, first, second);
        if (!suppressFirst && first.DistanceTo(split) > 1e-9) children.Add(LineNode(id, first, split, style, metadata));
        if (!suppressSecond && split.DistanceTo(second) > 1e-9) children.Add(LineNode(id, split, second, style, metadata));
    }

    private static Point2D ProjectToSegment(Point2D point, Point2D start, Point2D end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= double.Epsilon) return start;
        var t = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        return new Point2D(start.X + (dx * t), start.Y + (dy * t));
    }

    private static Dictionary<string, string> ArrowMetadata(IReadOnlyDictionary<string, string> metadata, CadDimensionPresentation presentation, bool first)
    {
        var result = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["DimensionArrowEnd"] = first ? "First" : "Second"
        };
        var requested = presentation.SeparateArrowBlocks
            ? first ? presentation.FirstArrowBlockName : presentation.SecondArrowBlockName
            : presentation.ArrowBlockName;
        if (string.IsNullOrWhiteSpace(requested)) requested = presentation.ArrowBlockName;
        result["DimensionArrowRequestedBlock"] = requested;
        result["DimensionArrowFallbackApplied"] = (!string.IsNullOrWhiteSpace(requested)).ToString();
        return result;
    }

    private static void AddRadialDimension(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        if (!TryReference(dimension, "AngleVertex", out var center))
        {
            AddReferencePath(dimension, style, metadata, children);
            return;
        }
        var edge = dimension.DefinitionPoint;
        var presentation = dimension.Presentation;
        if (!presentation.SuppressFirstDimensionLine && !presentation.SuppressSecondDimensionLine)
        {
            if (dimension.Kind == CadDimensionKind.Diameter)
            {
                var opposite = new Point2D((2 * center.X) - edge.X, (2 * center.Y) - edge.Y);
                children.Add(LineNode(dimension.ObjectId, opposite, edge, style, metadata));
                AddArrow(children, dimension.ObjectId, opposite, center, dimension.ArrowSize, style, ArrowMetadata(metadata, presentation, true));
            }
            else
            {
                children.Add(LineNode(dimension.ObjectId, center, edge, style, metadata));
            }
            AddArrow(children, dimension.ObjectId, edge, center, dimension.ArrowSize, style, ArrowMetadata(metadata, presentation, false));
        }
    }

    private static void AddAngularDimension(CadDimensionEntity dimension, SceneStyle dimensionLineStyle, SceneStyle extensionLineStyle, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        Point2D center;
        Point2D first;
        Point2D second;
        Point2D arcPoint;
        if (dimension.Kind == CadDimensionKind.Angular3Point)
        {
            if (!TryReference(dimension, "AngleVertex", out center) || !TryReference(dimension, "FirstPoint", out first) || !TryReference(dimension, "SecondPoint", out second))
            {
                AddReferencePath(dimension, dimensionLineStyle, metadata, children);
                return;
            }
            arcPoint = dimension.DefinitionPoint;
        }
        else
        {
            if (!TryReference(dimension, "Center", out center) || !TryReference(dimension, "SecondPoint", out second))
            {
                AddReferencePath(dimension, dimensionLineStyle, metadata, children);
                return;
            }
            first = dimension.DefinitionPoint;
            arcPoint = TryReference(dimension, "DimensionArc", out var candidate) ? candidate : dimension.DefinitionPoint;
        }

        if (!dimension.Presentation.SuppressFirstExtensionLine) children.Add(LineNode(dimension.ObjectId, center, first, extensionLineStyle, metadata));
        if (!dimension.Presentation.SuppressSecondExtensionLine) children.Add(LineNode(dimension.ObjectId, center, second, extensionLineStyle, metadata));
        if (dimension.Presentation.SuppressFirstDimensionLine && dimension.Presentation.SuppressSecondDimensionLine) return;
        var radius = center.DistanceTo(arcPoint);
        if (radius <= double.Epsilon) return;
        var start = Math.Atan2(first.Y - center.Y, first.X - center.X);
        var end = Math.Atan2(second.Y - center.Y, second.X - center.X);
        var through = Math.Atan2(arcPoint.Y - center.Y, arcPoint.X - center.X);
        var sweep = SweepContaining(start, end, through);
        children.Add(new SceneNode(dimension.ObjectId, new ArcGeometry(center, radius, start, sweep), style: dimensionLineStyle, metadata: metadata));
    }

    private static void AddOrdinateDimension(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        if (!TryReference(dimension, "FeatureLocation", out var feature) || !TryReference(dimension, "LeaderEndpoint", out var endpoint))
        {
            AddReferencePath(dimension, style, metadata, children);
            return;
        }

        var horizontalDirection = MetadataDouble(metadata, "DimensionHorizontalDirection", 0);
        var isOrdinateTypeX = MetadataBool(metadata, "DimensionOrdinateTypeX");
        var rotation = horizontalDirection + (isOrdinateTypeX ? Math.PI / 2 : 0);
        var refDelta = new Point2D(endpoint.X - feature.X, endpoint.Y - feature.Y);
        var local = Rotate(refDelta, -rotation);
        var minimumOffset = 2 * Math.Max(dimension.ArrowSize, double.Epsilon);

        Point2D localJog1;
        Point2D localJog2;
        if (local.X >= 0)
        {
            if (local.X >= 2 * minimumOffset)
            {
                localJog1 = new Point2D(local.X - minimumOffset, 0);
                localJog2 = new Point2D(local.X - minimumOffset, local.Y);
            }
            else
            {
                localJog1 = new Point2D(minimumOffset, 0);
                localJog2 = new Point2D(local.X - minimumOffset, local.Y);
            }
        }
        else if (local.X <= -2 * minimumOffset)
        {
            localJog1 = new Point2D(local.X + minimumOffset, 0);
            localJog2 = new Point2D(local.X + minimumOffset, local.Y);
        }
        else
        {
            localJog1 = new Point2D(-minimumOffset, 0);
            localJog2 = new Point2D(local.X + minimumOffset, local.Y);
        }

        var jog1Delta = Rotate(localJog1, rotation);
        var jog2Delta = Rotate(localJog2, rotation);
        var jog1 = new Point2D(feature.X + jog1Delta.X, feature.Y + jog1Delta.Y);
        var jog2 = new Point2D(feature.X + jog2Delta.X, feature.Y + jog2Delta.Y);
        var axis = new Point2D(Math.Cos(rotation), Math.Sin(rotation));
        var extensionOffset = Math.Max(0, double.IsFinite(dimension.Presentation.ExtensionLineOffset) ? dimension.Presentation.ExtensionLineOffset : 0);
        var start = new Point2D(feature.X + (axis.X * extensionOffset), feature.Y + (axis.Y * extensionOffset));
        var ordinateMetadata = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["DimensionOrdinateJogReconstructed"] = bool.TrueString,
            ["DimensionOrdinateResolvedRotation"] = rotation.ToString("R", CultureInfo.InvariantCulture)
        };

        if (start.DistanceTo(jog1) > 1e-9) children.Add(LineNode(dimension.ObjectId, start, jog1, style, ordinateMetadata));
        if (jog1.DistanceTo(jog2) > 1e-9) children.Add(LineNode(dimension.ObjectId, jog1, jog2, style, ordinateMetadata));
        if (jog2.DistanceTo(endpoint) > 1e-9) children.Add(LineNode(dimension.ObjectId, jog2, endpoint, style, ordinateMetadata));
    }

    private static void AddReferencePath(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        var points = dimension.ReferencePoints.Values.ToList();
        if (points.Count == 0) points.Add(dimension.DefinitionPoint);
        else if (points[^1].DistanceTo(dimension.DefinitionPoint) > 1e-9) points.Add(dimension.DefinitionPoint);
        if (points.Count == 1) children.Add(new SceneNode(dimension.ObjectId, new PointGeometry(points[0]), style: style, metadata: metadata));
        else children.Add(new SceneNode(dimension.ObjectId, new PolylineGeometry(points), style: style, metadata: metadata));
    }

    private static SceneNode LeaderNode(CadLeaderEntity leader, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["LeaderStyle"] = leader.StyleName,
            ["LeaderSpline"] = leader.IsSpline.ToString(),
            ["LeaderAssociatedHandle"] = leader.AnnotationHandle,
            ["LeaderAssociatedType"] = leader.AnnotationType,
            ["LeaderSemantic"] = bool.TrueString
        };
        if (!string.IsNullOrEmpty(leader.AnnotationText)) enriched["LeaderAnnotationText"] = leader.AnnotationText;
        var children = new List<SceneNode>();
        if (leader.Vertices.Count == 1) children.Add(new SceneNode(leader.ObjectId, new PointGeometry(leader.Vertices[0]), style: style, metadata: enriched));
        else if (leader.Vertices.Count > 1) children.Add(new SceneNode(leader.ObjectId, leader.IsSpline ? new PathGeometry(leader.Vertices, false) : new PolylineGeometry(leader.Vertices), style: style, metadata: enriched));
        if (leader.ArrowHeadEnabled && leader.Vertices.Count > 1) AddArrow(children, leader.ObjectId, leader.Vertices[0], leader.Vertices[1], Math.Max(leader.TextHeight, 1), style, enriched);
        return new SceneNode(leader.ObjectId, style: style, children: children, metadata: enriched);
    }

    private static SceneNode MultiLeaderNode(CadMultiLeaderEntity multiLeader, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["MultiLeaderStyle"] = multiLeader.StyleName,
            ["MultiLeaderContentType"] = multiLeader.ContentType,
            ["MultiLeaderPathCount"] = multiLeader.Paths.Count.ToString(CultureInfo.InvariantCulture),
            ["MultiLeaderDogleg"] = multiLeader.EnableDogleg.ToString(),
            ["MultiLeaderSemantic"] = bool.TrueString
        };
        var children = new List<SceneNode>();
        for (var index = 0; index < multiLeader.Paths.Count; index++)
        {
            var path = multiLeader.Paths[index];
            if (path.Points.Count == 0) continue;
            var pathMetadata = new Dictionary<string, string>(enriched, StringComparer.Ordinal)
            {
                ["MultiLeaderPathIndex"] = index.ToString(CultureInfo.InvariantCulture),
                ["MultiLeaderPathSpline"] = path.IsSpline.ToString()
            };
            if (path.Points.Count == 1) children.Add(new SceneNode(multiLeader.ObjectId, new PointGeometry(path.Points[0]), style: style, metadata: pathMetadata));
            else
            {
                children.Add(new SceneNode(multiLeader.ObjectId, path.IsSpline ? new PathGeometry(path.Points, false) : new PolylineGeometry(path.Points), style: style, metadata: pathMetadata));
                AddArrow(children, multiLeader.ObjectId, path.Points[0], path.Points[1], path.ArrowSize > double.Epsilon ? path.ArrowSize : multiLeader.ArrowSize, style, pathMetadata);
            }
        }
        if (!string.IsNullOrWhiteSpace(multiLeader.Text) && multiLeader.TextHeight > double.Epsilon)
        {
            children.Add(TextNode(multiLeader.ObjectId, multiLeader.TextLocation, multiLeader.Text, multiLeader.TextHeight, multiLeader.TextRotationRadians, style, enriched));
        }
        return new SceneNode(multiLeader.ObjectId, style: style, children: children, metadata: enriched);
    }

    private static SceneNode LineNode(ObjectId id, Point2D start, Point2D end, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
        => new(id, new LineGeometry(start, end), style: style, metadata: metadata);

    private static void AddArrow(List<SceneNode> children, ObjectId id, Point2D tip, Point2D toward, double size, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var direction = Normalize(new Point2D(toward.X - tip.X, toward.Y - tip.Y));
        if (Math.Abs(direction.X) <= double.Epsilon && Math.Abs(direction.Y) <= double.Epsilon) return;
        var arrowSize = size > double.Epsilon ? size : 2.5;
        var back = new Point2D(-direction.X, -direction.Y);
        const double spread = 0.42;
        var leftDirection = Rotate(back, spread);
        var rightDirection = Rotate(back, -spread);
        var left = new Point2D(tip.X + (leftDirection.X * arrowSize), tip.Y + (leftDirection.Y * arrowSize));
        var right = new Point2D(tip.X + (rightDirection.X * arrowSize), tip.Y + (rightDirection.Y * arrowSize));
        children.Add(LineNode(id, tip, left, style, metadata));
        children.Add(LineNode(id, tip, right, style, metadata));
    }

    private static bool TryReference(CadDimensionEntity dimension, string name, out Point2D point) => dimension.ReferencePoints.TryGetValue(name, out point);
    private static double Dot(Point2D a, Point2D b) => (a.X * b.X) + (a.Y * b.Y);
    private static Point2D Normalize(Point2D vector)
    {
        var length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y));
        return length <= double.Epsilon ? Point2D.Origin : new Point2D(vector.X / length, vector.Y / length);
    }
    private static Point2D Rotate(Point2D vector, double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new Point2D((vector.X * cosine) - (vector.Y * sine), (vector.X * sine) + (vector.Y * cosine));
    }
    private static double SweepContaining(double start, double end, double through)
    {
        var positive = NormalizeAngle(end - start);
        var throughPositive = NormalizeAngle(through - start);
        return throughPositive <= positive + 1e-9 ? positive : positive - (Math.PI * 2);
    }
    private static double NormalizeAngle(double angle)
    {
        var value = angle % (Math.PI * 2);
        return value < 0 ? value + (Math.PI * 2) : value;
    }
    private static double MetadataDouble(IReadOnlyDictionary<string, string> metadata, string key, double fallback)
        => metadata.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed) ? parsed : fallback;
    private static bool MetadataBool(IReadOnlyDictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;
}
