using SpatialViewer.Core;

namespace SpatialViewer.Rendering;

/// <summary>Backend-neutral prepared drawing command. Coordinates remain doubles until a backend deliberately localizes them.</summary>
public readonly record struct RenderCommand(ObjectId ObjectId, Geometry2D Geometry, Transform2D WorldTransform, SceneStyle Style, BoundingBox2D Bounds, IReadOnlyDictionary<string, string>? Metadata = null);
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
    /// <summary>Flattens only visible layers. Local origin is camera target to preserve float precision in GPU-facing backends.</summary>
    public static RenderFrame Prepare(Scene2D scene, Camera2D camera) => new(scene.GetItems().Select(item => new RenderCommand(item.Id, item.Geometry, item.Transform, item.Style, item.Bounds, item.Metadata)).ToArray(), camera.Target);
}
