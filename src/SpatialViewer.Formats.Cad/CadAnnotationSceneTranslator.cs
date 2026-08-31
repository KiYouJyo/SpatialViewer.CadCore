using System.Globalization;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

public sealed partial class CadSceneTranslator
{
    private static SceneNode DimensionNode(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["DimensionKind"] = dimension.Kind.ToString(),
            ["DimensionMeasurement"] = dimension.Measurement.ToString("R", CultureInfo.InvariantCulture),
            ["DimensionStyle"] = dimension.StyleName,
            ["DimensionSemantic"] = bool.TrueString
        };
        var children = new List<SceneNode>();
        switch (dimension.Kind)
        {
            case CadDimensionKind.Linear:
            case CadDimensionKind.Aligned:
                AddLinearDimension(dimension, style, enriched, children);
                break;
            case CadDimensionKind.Radius:
            case CadDimensionKind.Diameter:
                AddRadialDimension(dimension, style, enriched, children);
                break;
            case CadDimensionKind.Angular2Line:
            case CadDimensionKind.Angular3Point:
                AddAngularDimension(dimension, style, enriched, children);
                break;
            case CadDimensionKind.Ordinate:
                AddOrdinateDimension(dimension, style, enriched, children);
                break;
            default:
                AddReferencePath(dimension, style, enriched, children);
                break;
        }
        if (!string.IsNullOrWhiteSpace(dimension.Text) && dimension.TextHeight > double.Epsilon)
        {
            children.Add(TextNode(dimension.ObjectId, dimension.TextPosition, dimension.Text, dimension.TextHeight, dimension.RotationRadians, style, enriched));
        }
        return new SceneNode(dimension.ObjectId, style: style, children: children, metadata: enriched);
    }

    private static void AddLinearDimension(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        if (!TryReference(dimension, "FirstPoint", out var first) || !TryReference(dimension, "SecondPoint", out var second))
        {
            AddReferencePath(dimension, style, metadata, children);
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

        children.Add(LineNode(dimension.ObjectId, first, dimFirst, style, metadata));
        children.Add(LineNode(dimension.ObjectId, second, dimSecond, style, metadata));
        children.Add(LineNode(dimension.ObjectId, dimFirst, dimSecond, style, metadata));
        AddArrow(children, dimension.ObjectId, dimFirst, dimSecond, dimension.ArrowSize, style, metadata);
        AddArrow(children, dimension.ObjectId, dimSecond, dimFirst, dimension.ArrowSize, style, metadata);
    }

    private static void AddRadialDimension(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        if (!TryReference(dimension, "AngleVertex", out var center))
        {
            AddReferencePath(dimension, style, metadata, children);
            return;
        }
        var edge = dimension.DefinitionPoint;
        if (dimension.Kind == CadDimensionKind.Diameter)
        {
            var opposite = new Point2D((2 * center.X) - edge.X, (2 * center.Y) - edge.Y);
            children.Add(LineNode(dimension.ObjectId, opposite, edge, style, metadata));
            AddArrow(children, dimension.ObjectId, opposite, center, dimension.ArrowSize, style, metadata);
        }
        else
        {
            children.Add(LineNode(dimension.ObjectId, center, edge, style, metadata));
        }
        AddArrow(children, dimension.ObjectId, edge, center, dimension.ArrowSize, style, metadata);
    }

    private static void AddAngularDimension(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        Point2D center;
        Point2D first;
        Point2D second;
        Point2D arcPoint;
        if (dimension.Kind == CadDimensionKind.Angular3Point)
        {
            if (!TryReference(dimension, "AngleVertex", out center) || !TryReference(dimension, "FirstPoint", out first) || !TryReference(dimension, "SecondPoint", out second))
            {
                AddReferencePath(dimension, style, metadata, children);
                return;
            }
            arcPoint = dimension.DefinitionPoint;
        }
        else
        {
            if (!TryReference(dimension, "Center", out center) || !TryReference(dimension, "SecondPoint", out second))
            {
                AddReferencePath(dimension, style, metadata, children);
                return;
            }
            first = dimension.DefinitionPoint;
            arcPoint = TryReference(dimension, "DimensionArc", out var candidate) ? candidate : dimension.DefinitionPoint;
        }

        children.Add(LineNode(dimension.ObjectId, center, first, style, metadata));
        children.Add(LineNode(dimension.ObjectId, center, second, style, metadata));
        var radius = center.DistanceTo(arcPoint);
        if (radius <= double.Epsilon) return;
        var start = Math.Atan2(first.Y - center.Y, first.X - center.X);
        var end = Math.Atan2(second.Y - center.Y, second.X - center.X);
        var through = Math.Atan2(arcPoint.Y - center.Y, arcPoint.X - center.X);
        var sweep = SweepContaining(start, end, through);
        children.Add(new SceneNode(dimension.ObjectId, new ArcGeometry(center, radius, start, sweep), style: style, metadata: metadata));
    }

    private static void AddOrdinateDimension(CadDimensionEntity dimension, SceneStyle style, IReadOnlyDictionary<string, string> metadata, List<SceneNode> children)
    {
        if (!TryReference(dimension, "FeatureLocation", out var feature))
        {
            AddReferencePath(dimension, style, metadata, children);
            return;
        }
        var endpoint = TryReference(dimension, "LeaderEndpoint", out var leaderEndpoint)
            ? leaderEndpoint
            : TryReference(dimension, "LeaderEndPoint", out leaderEndpoint) ? leaderEndpoint : dimension.DefinitionPoint;
        children.Add(LineNode(dimension.ObjectId, feature, endpoint, style, metadata));
        if (endpoint.DistanceTo(dimension.DefinitionPoint) > 1e-9) children.Add(LineNode(dimension.ObjectId, endpoint, dimension.DefinitionPoint, style, metadata));
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
}
