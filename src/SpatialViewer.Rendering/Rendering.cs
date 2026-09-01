using SpatialViewer.Core;

namespace SpatialViewer.Rendering;

/// <summary>Backend-neutral prepared drawing command. Coordinates remain doubles until a backend deliberately localizes them.</summary>
public readonly record struct RenderCommand(ObjectId ObjectId, Geometry2D Geometry, Transform2D WorldTransform, SceneStyle Style, BoundingBox2D Bounds, IReadOnlyDictionary<string, string>? Metadata = null, BoundingBox2D? ClipBounds = null);
public sealed class RenderFrame
{
    public RenderFrame(IReadOnlyList<RenderCommand> commands, Point2D localOrigin) { Commands = commands; LocalOrigin = localOrigin; }
    public IReadOnlyList<RenderCommand> Commands { get; }
    public Point2D LocalOrigin { get; }
}
public interface ISceneRenderer : IDisposable
{
    void Render(RenderFrame frame, Camera2D camera, Size2D viewport, ObjectId? selectedObject);
    void RecreateResources();
}
public static class RenderPreparation
{
    /// <summary>Flattens all items on visible layers. Local origin is camera target to preserve float precision in GPU-facing backends.</summary>
    public static RenderFrame Prepare(Scene2D scene, Camera2D camera) => CreateFrame(scene.GetItems(), camera.Target);

    /// <summary>Uses the scene spatial index to prepare only items intersecting the current viewport, preserving original draw order.</summary>
    public static RenderFrame Prepare(Scene2D scene, Camera2D camera, Size2D viewport, double overscanPixels = 0)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);
        if (!double.IsFinite(overscanPixels)) throw new ArgumentOutOfRangeException(nameof(overscanPixels));
        if (viewport.IsEmpty) return new(Array.Empty<RenderCommand>(), camera.Target);

        var visibleBounds = camera.GetVisibleWorldBounds(viewport);
        var zoom = Math.Abs(camera.Zoom);
        if (zoom > double.Epsilon && Math.Abs(overscanPixels) > double.Epsilon) visibleBounds = visibleBounds.Inflate(Math.Abs(overscanPixels) / zoom);
        return CreateFrame(scene.QueryItems(visibleBounds), camera.Target);
    }

    private static RenderFrame CreateFrame(IEnumerable<SceneItem> items, Point2D localOrigin) => new(
        items.Select(item => new RenderCommand(item.Id, item.Geometry, item.Transform, item.Style, item.Bounds, item.Metadata, item.ClipBounds)).ToArray(),
        localOrigin);
}
