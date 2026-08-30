using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>Converts CAD-specific entities into the generic Stage 1 scene without exposing reader types.</summary>
public sealed class CadSceneTranslator
{
    public static Scene2D Translate(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var layers = document.CadLayers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        if (!layers.ContainsKey("0")) layers["0"] = new CadLayer("0", CadColor.FromAci(7));
        var blocks = document.Blocks.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var sceneLayers = layers.Values.Select((layer, index) => new SceneLayer(new Layer(layer.Name, layer.Name, index, layer.IsVisible, layer.IsLocked, new Dictionary<string, string> { ["CadColor"] = ResolveColor(layer.Color, layer.Color, null) }), document.ModelSpace.Where(entity => string.Equals(entity.LayerName, layer.Name, StringComparison.OrdinalIgnoreCase)).Select(entity => ToNode(entity, layers, blocks, layer.Color, new HashSet<string>(StringComparer.OrdinalIgnoreCase))).Where(x => x is not null).Cast<SceneNode>().ToArray())).ToArray();
        return new Scene2D(sceneLayers);
    }
    private static SceneNode? ToNode(CadEntity entity, IReadOnlyDictionary<string, CadLayer> layers, IReadOnlyDictionary<string, CadBlockDefinition> blocks, CadColor inheritedColor, HashSet<string> stack)
    {
        if (!entity.IsVisible || entity is CadUnsupportedEntity) return null;
        var layerColor = layers.TryGetValue(entity.LayerName, out var layer) ? layer.Color : CadColor.FromAci(7);
        var resolved = ResolveColor(entity.Color, layerColor, inheritedColor); var metadata = new Dictionary<string, string>(entity.Metadata, StringComparer.Ordinal) { ["SourceFormat"] = "CAD", ["CadType"] = entity.GetType().Name.Replace("Cad", string.Empty).Replace("Entity", string.Empty), ["Layer"] = entity.LayerName, ["Handle"] = entity.Handle, ["LineType"] = entity.LineTypeName };
        if (entity.LineWeight is { } weight) metadata["LineWeight"] = weight.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var style = new SceneStyle(resolved, Math.Max(1, (entity.LineWeight ?? 0) / 100d));
        return entity switch
        {
            CadPointEntity point => new SceneNode(entity.ObjectId, new PointGeometry(point.Position), style: style, metadata: metadata),
            CadLineEntity line => new SceneNode(entity.ObjectId, new LineGeometry(line.Start, line.End), style: style, metadata: metadata),
            CadCircleEntity circle => new SceneNode(entity.ObjectId, new CircleGeometry(circle.Center, circle.Radius), style: style, metadata: metadata),
            CadArcEntity arc => new SceneNode(entity.ObjectId, new ArcGeometry(arc.Center, arc.Radius, arc.StartRadians, arc.SweepRadians), style: style, metadata: metadata),
            CadEllipseEntity ellipse => new SceneNode(entity.ObjectId, new EllipseGeometry(ellipse.Center, ellipse.RadiusX, ellipse.RadiusY), Transform2D.Translation(ellipse.Center.X, ellipse.Center.Y).Then(Transform2D.Rotation(ellipse.RotationRadians)).Then(Transform2D.Translation(-ellipse.Center.X, -ellipse.Center.Y)), style, metadata: metadata),
            CadPolylineEntity polyline => new SceneNode(entity.ObjectId, polyline.IsClosed ? new PolygonGeometry(polyline.Vertices) : new PolylineGeometry(polyline.Vertices), style: style, metadata: metadata),
            CadTextEntity text => new SceneNode(entity.ObjectId, new TextGeometry(text.InsertionPoint, text.Text, text.Height), Transform2D.Translation(text.InsertionPoint.X, text.InsertionPoint.Y).Then(Transform2D.Rotation(text.RotationRadians)).Then(Transform2D.Translation(-text.InsertionPoint.X, -text.InsertionPoint.Y)), style, metadata: metadata),
            CadBlockReferenceEntity reference => BlockNode(reference, layers, blocks, resolved, stack, metadata),
            _ => null
        };
    }
    private static SceneNode? BlockNode(CadBlockReferenceEntity reference, IReadOnlyDictionary<string, CadLayer> layers, IReadOnlyDictionary<string, CadBlockDefinition> blocks, string color, HashSet<string> stack, IReadOnlyDictionary<string, string> metadata)
    {
        if (!blocks.TryGetValue(reference.BlockName, out var definition) || !stack.Add(reference.BlockName)) return null;
        var transform = Transform2D.Translation(-definition.BasePoint.X, -definition.BasePoint.Y).Then(Transform2D.Scale(reference.ScaleX, reference.ScaleY)).Then(Transform2D.Rotation(reference.RotationRadians)).Then(Transform2D.Translation(reference.InsertionPoint.X, reference.InsertionPoint.Y));
        var children = definition.Entities.Select(entity => ToNode(entity, layers, blocks, CadColor.FromRgb(Convert.ToByte(color[1..3], 16), Convert.ToByte(color[3..5], 16), Convert.ToByte(color[5..7], 16)), stack)).Where(x => x is not null).Cast<SceneNode>().ToArray(); stack.Remove(reference.BlockName);
        return new SceneNode(reference.ObjectId, null, transform, new SceneStyle(color), children, metadata);
    }
    public static string ResolveColor(CadColor color, CadColor layerColor, CadColor? blockColor)
    {
        var selected = color.Kind == CadColorKind.ByLayer ? layerColor : color.Kind == CadColorKind.ByBlock ? blockColor ?? layerColor : color;
        if (selected.Kind == CadColorKind.TrueColor) return $"#{selected.Red:X2}{selected.Green:X2}{selected.Blue:X2}";
        return selected.Index switch { 1 => "#FF0000", 2 => "#FFFF00", 3 => "#00FF00", 4 => "#00FFFF", 5 => "#0000FF", 6 => "#FF00FF", 7 => "#FFFFFF", _ => "#B0BEC5" };
    }
}
