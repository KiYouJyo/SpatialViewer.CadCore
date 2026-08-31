using System.Globalization;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>Reader-independent paper-space viewport. Model geometry remains semantic and is projected only when a layout scene is requested.</summary>
public sealed record CadViewportDefinition(
    string Handle,
    Point2D PaperCenter,
    Size2D PaperSize,
    Point2D ViewCenter,
    Point2D ViewTarget,
    double ViewHeight,
    double TwistRadians = 0,
    bool IsOn = true,
    bool RepresentsPaper = false,
    IReadOnlyList<string>? FrozenLayers = null,
    string? BoundaryHandle = null,
    IReadOnlyList<Point2D>? ClipBoundary = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public IReadOnlyList<string> FrozenLayerNames { get; } = FrozenLayers ?? Array.Empty<string>();
    public IReadOnlyList<Point2D> BoundaryPoints { get; } = ClipBoundary ?? Array.Empty<Point2D>();
    public IReadOnlyDictionary<string, string> Properties { get; } = Metadata ?? EmptyMetadata.Value;
    public double ViewWidth => ViewHeight <= double.Epsilon || PaperSize.Height <= double.Epsilon ? 0 : ViewHeight * (PaperSize.Width / PaperSize.Height);
    public double ScaleFactor => ViewHeight <= double.Epsilon ? 0 : PaperSize.Height / ViewHeight;
    public BoundingBox2D PaperBounds => PaperSize.IsEmpty
        ? BoundingBox2D.Empty
        : new(PaperCenter.X - (PaperSize.Width / 2), PaperCenter.Y - (PaperSize.Height / 2), PaperCenter.X + (PaperSize.Width / 2), PaperCenter.Y + (PaperSize.Height / 2));
    public Transform2D ModelToPaper
    {
        get
        {
            var scale = ScaleFactor;
            if (!double.IsFinite(scale) || scale <= double.Epsilon) return Transform2D.Identity;
            return Transform2D.Translation(-ViewCenter.X, -ViewCenter.Y)
                .Then(Transform2D.Rotation(-TwistRadians))
                .Then(Transform2D.Scale(scale, scale))
                .Then(Transform2D.Translation(PaperCenter.X, PaperCenter.Y));
        }
    }
}

/// <summary>Reader-independent CAD layout including paper-space entities and its model-space viewports.</summary>
public sealed record CadLayoutDefinition(
    string Name,
    int TabOrder,
    bool IsPaperSpace,
    Size2D PaperSize,
    BoundingBox2D Limits,
    BoundingBox2D Extents,
    IReadOnlyList<CadEntity> Entities,
    IReadOnlyList<CadViewportDefinition> Viewports,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public IReadOnlyDictionary<string, string> Properties { get; } = Metadata ?? EmptyMetadata.Value;
}

/// <summary>Builds a paper-space scene while keeping the source model-space scene untouched.</summary>
public static class CadLayoutSceneTranslator
{
    private const string ViewportLayerName = "__VIEWPORTS__";

    public static Scene2D Translate(CadDocument document, CadLayoutDefinition layout)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        if (!layout.IsPaperSpace) return document.Scene;

        var paperDocument = new CadDocument(
            document.DisplayName,
            document.SourceFormat,
            document.Version,
            document.Units,
            document.CadLayers,
            document.Blocks,
            layout.Entities,
            document.Diagnostics,
            document.Metadata);
        var paperItems = paperDocument.Scene.GetItems(false).ToArray();
        var modelItems = document.Scene.GetItems(false).ToArray();
        var nodesByLayer = new Dictionary<string, List<SceneNode>>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in document.CadLayers) nodesByLayer[layer.Name] = new List<SceneNode>();
        if (!nodesByLayer.ContainsKey("0")) nodesByLayer["0"] = new List<SceneNode>();

        foreach (var item in paperItems)
        {
            var metadata = new Dictionary<string, string>(item.Metadata, StringComparer.Ordinal)
            {
                ["Layout"] = layout.Name,
                ["Space"] = "Paper"
            };
            GetLayer(nodesByLayer, item.Layer.Name).Add(new SceneNode(item.Id, item.Geometry, item.Transform, item.Style, metadata: metadata, clipBounds: item.ClipBounds));
        }

        foreach (var viewport in layout.Viewports.Where(viewport => viewport.IsOn && !viewport.RepresentsPaper && !viewport.PaperBounds.IsEmpty && viewport.ScaleFactor > double.Epsilon))
        {
            var frozen = new HashSet<string>(viewport.FrozenLayerNames, StringComparer.OrdinalIgnoreCase);
            var transform = viewport.ModelToPaper;
            var paperBounds = viewport.PaperBounds;
            foreach (var item in modelItems)
            {
                if (frozen.Contains(item.Layer.Name)) continue;
                var projectedBounds = item.Bounds.Transform(transform);
                if (!projectedBounds.Intersects(paperBounds)) continue;
                var metadata = new Dictionary<string, string>(item.Metadata, StringComparer.Ordinal)
                {
                    ["Layout"] = layout.Name,
                    ["Space"] = "ModelThroughViewport",
                    ["ViewportHandle"] = viewport.Handle,
                    ["ViewportScale"] = viewport.ScaleFactor.ToString("R", CultureInfo.InvariantCulture)
                };
                GetLayer(nodesByLayer, item.Layer.Name).Add(new SceneNode(
                    item.Id,
                    item.Geometry,
                    item.Transform.Then(transform),
                    item.Style,
                    metadata: metadata,
                    clipBounds: paperBounds));
            }
        }

        var sceneLayers = new List<SceneLayer>();
        var order = 0;
        foreach (var layer in document.CadLayers)
        {
            var effectiveColor = CadSceneTranslator.ResolveColor(layer.Color, CadColor.FromAci(7), null);
            sceneLayers.Add(new SceneLayer(
                new Layer(layer.Name, layer.Name, order++, layer.IsVisible, layer.IsLocked, new Dictionary<string, string> { ["CadColor"] = effectiveColor, ["Layout"] = layout.Name }),
                GetLayer(nodesByLayer, layer.Name).ToArray()));
        }
        if (!document.CadLayers.Any(layer => string.Equals(layer.Name, "0", StringComparison.OrdinalIgnoreCase)))
            sceneLayers.Add(new SceneLayer(new Layer("0", "0", order++), GetLayer(nodesByLayer, "0").ToArray()));

        var viewportNodes = layout.Viewports
            .Where(viewport => viewport.IsOn && !viewport.RepresentsPaper && !viewport.PaperBounds.IsEmpty)
            .Select(viewport =>
            {
                var metadata = new Dictionary<string, string>(viewport.Properties, StringComparer.Ordinal)
                {
                    ["Layout"] = layout.Name,
                    ["Space"] = "ViewportBoundary",
                    ["ViewportHandle"] = viewport.Handle,
                    ["ViewportScale"] = viewport.ScaleFactor.ToString("R", CultureInfo.InvariantCulture),
                    ["ViewportFrozenLayerCount"] = viewport.FrozenLayerNames.Count.ToString(CultureInfo.InvariantCulture)
                };
                if (!string.IsNullOrWhiteSpace(viewport.BoundaryHandle)) metadata["ViewportBoundaryHandle"] = viewport.BoundaryHandle!;
                if (viewport.BoundaryPoints.Count > 0) metadata["ViewportNonRectangularBoundaryPreserved"] = bool.TrueString;
                return new SceneNode(CadIds.ToObjectId($"viewport:{layout.Name}:{viewport.Handle}"), new RectangleGeometry(viewport.PaperBounds), style: new SceneStyle("#808080", 1), metadata: metadata);
            })
            .ToArray();
        sceneLayers.Add(new SceneLayer(new Layer(ViewportLayerName, "Viewports", order, true, false, new Dictionary<string, string> { ["Layout"] = layout.Name }), viewportNodes));
        return new Scene2D(sceneLayers);
    }

    private static List<SceneNode> GetLayer(Dictionary<string, List<SceneNode>> layers, string name)
    {
        if (!layers.TryGetValue(name, out var nodes)) layers[name] = nodes = new List<SceneNode>();
        return nodes;
    }
}
