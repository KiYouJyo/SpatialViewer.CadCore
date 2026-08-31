using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>Converts CAD-specific entities into the generic Stage 1 scene without exposing reader types.</summary>
public sealed partial class CadSceneTranslator
{
    private const string BackgroundAdaptiveStrokeKey = "BackgroundAdaptiveStroke";

    public static Scene2D Translate(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var layers = document.CadLayers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        if (!layers.ContainsKey("0")) layers["0"] = new CadLayer("0", CadColor.FromAci(7));
        var blocks = document.Blocks.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var sceneLayers = layers.Values.Select((layer, index) =>
        {
            var effectiveLayerColor = ResolveCadColor(layer.Color, CadColor.FromAci(7), null);
            var layerMetadata = new Dictionary<string, string> { ["CadColor"] = ToHex(effectiveLayerColor) };
            AddColorMetadata(layerMetadata, effectiveLayerColor);
            var nodes = document.ModelSpace
                .Where(entity => string.Equals(entity.LayerName, layer.Name, StringComparison.OrdinalIgnoreCase))
                .Select(entity => ToNode(entity, layers, blocks, null, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .Where(node => node is not null)
                .Cast<SceneNode>()
                .ToArray();
            return new SceneLayer(new Layer(layer.Name, layer.Name, index, layer.IsVisible, layer.IsLocked, layerMetadata), nodes);
        }).ToArray();
        return new Scene2D(sceneLayers);
    }

    private static SceneNode? ToNode(CadEntity entity, IReadOnlyDictionary<string, CadLayer> layers, IReadOnlyDictionary<string, CadBlockDefinition> blocks, CadColor? inheritedColor, CadColor? inheritedLayerColor, HashSet<string> stack)
    {
        if (!entity.IsVisible || entity is CadUnsupportedEntity) return null;
        var sourceLayerColor = layers.TryGetValue(entity.LayerName, out var layer) ? layer.Color : CadColor.FromAci(7);
        var layerColor = string.Equals(entity.LayerName, "0", StringComparison.OrdinalIgnoreCase) && inheritedLayerColor is { } inheritedLayer
            ? inheritedLayer
            : ResolveCadColor(sourceLayerColor, CadColor.FromAci(7), inheritedColor);
        var effectiveColor = ResolveCadColor(entity.Color, layerColor, inheritedColor);
        var metadata = new Dictionary<string, string>(entity.Metadata, StringComparer.Ordinal)
        {
            ["SourceFormat"] = "CAD",
            ["CadType"] = entity.GetType().Name.Replace("Cad", string.Empty).Replace("Entity", string.Empty),
            ["Layer"] = entity.LayerName,
            ["Handle"] = entity.Handle,
            ["LineType"] = entity.LineTypeName
        };
        AddColorMetadata(metadata, effectiveColor);
        if (entity.LineWeight is { } weight) metadata["LineWeight"] = weight.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var style = new SceneStyle(ToHex(effectiveColor), Math.Max(1, (entity.LineWeight ?? 0) / 100d));
        return entity switch
        {
            CadPointEntity point => new SceneNode(entity.ObjectId, new PointGeometry(point.Position), style: style, metadata: metadata),
            CadLineEntity line => new SceneNode(entity.ObjectId, new LineGeometry(line.Start, line.End), style: style, metadata: metadata),
            CadCircleEntity circle => new SceneNode(entity.ObjectId, new CircleGeometry(circle.Center, circle.Radius), style: style, metadata: metadata),
            CadArcEntity arc => new SceneNode(entity.ObjectId, new ArcGeometry(arc.Center, arc.Radius, arc.StartRadians, arc.SweepRadians), style: style, metadata: metadata),
            CadEllipseEntity ellipse => new SceneNode(entity.ObjectId, new EllipseGeometry(ellipse.Center, ellipse.RadiusX, ellipse.RadiusY), Transform2D.Translation(ellipse.Center.X, ellipse.Center.Y).Then(Transform2D.Rotation(ellipse.RotationRadians)).Then(Transform2D.Translation(-ellipse.Center.X, -ellipse.Center.Y)), style, metadata: metadata),
            CadPolylineEntity polyline => PolylineNode(polyline, style, metadata),
            CadSplineEntity spline => SplineNode(spline, style, metadata),
            CadHatchEntity hatch => HatchNode(hatch, effectiveColor, style, metadata),
            CadDimensionEntity dimension => DimensionNode(dimension, style, metadata),
            CadLeaderEntity leader => LeaderNode(leader, style, metadata),
            CadMultiLeaderEntity multiLeader => MultiLeaderNode(multiLeader, style, metadata),
            CadTextEntity text => TextNode(entity.ObjectId, text.InsertionPoint, text.Text, text.Height, text.RotationRadians, style, metadata),
            CadAttributeEntity attribute => AttributeNode(attribute, style, metadata),
            CadBlockReferenceEntity reference => BlockNode(reference, layers, blocks, effectiveColor, layerColor, stack, metadata),
            _ => null
        };
    }

    private static SceneNode SplineNode(CadSplineEntity spline, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var points = CadCurveTessellator.Spline(spline.Spline);
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["SplineDegree"] = spline.Spline.Degree.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SplineClosed"] = spline.Spline.IsClosed.ToString(),
            ["SplinePeriodic"] = spline.Spline.IsPeriodic.ToString(),
            ["SplineControlPointCount"] = spline.Spline.ControlPoints.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return new SceneNode(spline.ObjectId, new PathGeometry(points, spline.Spline.IsClosed || spline.Spline.IsPeriodic), style: style, metadata: enriched);
    }

    private static SceneNode? HatchNode(CadHatchEntity hatch, CadColor effectiveColor, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var loops = hatch.Loops.Select(CadCurveTessellator.HatchLoop).Where(loop => loop.Count >= 3).ToArray();
        if (loops.Length == 0) return null;
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["HatchSolid"] = hatch.IsSolid.ToString(),
            ["HatchPattern"] = hatch.PatternName,
            ["HatchPatternAngle"] = hatch.PatternAngleRadians.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            ["HatchPatternScale"] = hatch.PatternScale.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            ["HatchLoopCount"] = loops.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var hatchStyle = hatch.IsSolid ? style with { Fill = ToHex(effectiveColor) } : style;
        return new SceneNode(hatch.ObjectId, new CompoundPathGeometry(loops), style: hatchStyle, metadata: enriched);
    }

    private static SceneNode AttributeNode(CadAttributeEntity attribute, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var enriched = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["AttributeTag"] = attribute.Tag,
            ["AttributeDefinition"] = attribute.IsDefinition.ToString(),
            ["AttributeConstant"] = attribute.IsConstant.ToString()
        };
        if (!string.IsNullOrEmpty(attribute.Prompt)) enriched["AttributePrompt"] = attribute.Prompt;
        return TextNode(attribute.ObjectId, attribute.InsertionPoint, attribute.Value, attribute.Height, attribute.RotationRadians, style, enriched);
    }

    private static SceneNode TextNode(ObjectId id, Point2D insertionPoint, string text, double height, double rotationRadians, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
        => new(id, new TextGeometry(insertionPoint, text, height), Transform2D.Translation(insertionPoint.X, insertionPoint.Y).Then(Transform2D.Rotation(rotationRadians)).Then(Transform2D.Translation(-insertionPoint.X, -insertionPoint.Y)), style, metadata: metadata);

    private static SceneNode PolylineNode(CadPolylineEntity polyline, SceneStyle style, IReadOnlyDictionary<string, string> metadata)
    {
        var hasBulges = polyline.Bulges.Any(value => Math.Abs(value) > 1e-12);
        if (!hasBulges) return new SceneNode(polyline.ObjectId, polyline.IsClosed ? new PolygonGeometry(polyline.Vertices) : new PolylineGeometry(polyline.Vertices), style: style, metadata: metadata);
        var children = new List<SceneNode>();
        var segmentCount = polyline.IsClosed ? polyline.Vertices.Count : Math.Max(0, polyline.Vertices.Count - 1);
        for (var index = 0; index < segmentCount; index++)
        {
            var start = polyline.Vertices[index];
            var end = polyline.Vertices[(index + 1) % polyline.Vertices.Count];
            var bulge = index < polyline.Bulges.Count ? polyline.Bulges[index] : 0;
            children.Add(new SceneNode(polyline.ObjectId, BulgeSegment(start, end, bulge), style: style, metadata: metadata));
        }
        return new SceneNode(polyline.ObjectId, children: children, style: style, metadata: metadata);
    }

    private static Geometry2D BulgeSegment(Point2D start, Point2D end, double bulge)
    {
        var chord = start.DistanceTo(end);
        if (!double.IsFinite(bulge) || Math.Abs(bulge) <= 1e-12 || chord <= double.Epsilon) return new LineGeometry(start, end);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var midpoint = new Point2D((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        var offset = chord * (1 - (bulge * bulge)) / (4 * bulge);
        var center = new Point2D(midpoint.X - ((dy / chord) * offset), midpoint.Y + ((dx / chord) * offset));
        var radius = center.DistanceTo(start);
        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        return new ArcGeometry(center, radius, startAngle, 4 * Math.Atan(bulge));
    }

    private static SceneNode? BlockNode(CadBlockReferenceEntity reference, IReadOnlyDictionary<string, CadLayer> layers, IReadOnlyDictionary<string, CadBlockDefinition> blocks, CadColor effectiveColor, CadColor effectiveLayerColor, HashSet<string> stack, IReadOnlyDictionary<string, string> metadata)
    {
        if (!blocks.TryGetValue(reference.BlockName, out var definition) || !stack.Add(reference.BlockName)) return null;
        var transform = Transform2D.Translation(-definition.BasePoint.X, -definition.BasePoint.Y)
            .Then(Transform2D.Scale(reference.ScaleX, reference.ScaleY))
            .Then(Transform2D.Rotation(reference.RotationRadians))
            .Then(Transform2D.Translation(reference.InsertionPoint.X, reference.InsertionPoint.Y));
        var instanceTags = new HashSet<string>(reference.Attributes.Select(attribute => attribute.Tag), StringComparer.OrdinalIgnoreCase);
        var blockChildren = definition.Entities
            .Where(entity => entity is not CadAttributeEntity { IsDefinition: true, IsConstant: false } definitionAttribute || !instanceTags.Contains(definitionAttribute.Tag))
            .Select(entity => ToNode(entity, layers, blocks, effectiveColor, effectiveLayerColor, stack))
            .Where(node => node is not null)
            .Cast<SceneNode>()
            .ToArray();
        stack.Remove(reference.BlockName);

        var children = new List<SceneNode>();
        if (blockChildren.Length > 0) children.Add(new SceneNode(reference.ObjectId, transform: transform, style: new SceneStyle(ToHex(effectiveColor)), children: blockChildren, metadata: metadata));
        foreach (var attribute in reference.Attributes)
        {
            var attributeNode = ToNode(attribute, layers, blocks, effectiveColor, effectiveLayerColor, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (attributeNode is not null) children.Add(attributeNode);
        }
        return new SceneNode(reference.ObjectId, style: new SceneStyle(ToHex(effectiveColor)), children: children, metadata: metadata);
    }

    public static string ResolveColor(CadColor color, CadColor layerColor, CadColor? blockColor) => ToHex(ResolveCadColor(color, layerColor, blockColor));

    private static CadColor ResolveCadColor(CadColor color, CadColor layerColor, CadColor? blockColor)
    {
        return color.Kind switch
        {
            CadColorKind.ByLayer => NormalizeInherited(layerColor, blockColor),
            CadColorKind.ByBlock => blockColor is { } inherited ? NormalizeInherited(inherited, null) : CadColor.FromAci(7),
            _ => color
        };
    }

    private static CadColor NormalizeInherited(CadColor color, CadColor? blockColor)
    {
        return color.Kind switch
        {
            CadColorKind.ByLayer => CadColor.FromAci(7),
            CadColorKind.ByBlock => blockColor is { } inherited ? NormalizeInherited(inherited, null) : CadColor.FromAci(7),
            _ => color
        };
    }

    private static string ToHex(CadColor color) => color.Kind == CadColorKind.TrueColor
        ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
        : CadColorPalette.GetHex(color.Index);

    private static void AddColorMetadata(Dictionary<string, string> metadata, CadColor color)
    {
        metadata["CadColorKind"] = color.Kind.ToString();
        if (color.Kind == CadColorKind.Aci)
        {
            metadata["CadColorIndex"] = color.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (CadColorPalette.IsBackgroundAdaptive(color.Index)) metadata[BackgroundAdaptiveStrokeKey] = bool.TrueString;
        }
        else if (color.Kind == CadColorKind.TrueColor)
        {
            metadata["CadTrueColor"] = ToHex(color);
        }
    }
}
