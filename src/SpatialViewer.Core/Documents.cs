namespace SpatialViewer.Core;

public enum DocumentKind { Synthetic, Cad, Gis, Bim, Rhino }
public enum DiagnosticSeverity { Info, Warning, Error, Fatal }
public sealed record Diagnostic(DiagnosticSeverity Severity, string Code, string Message, IReadOnlyDictionary<string, string>? Context = null, Exception? Exception = null);
public interface IDocument
{
    Guid DocumentId { get; }
    DocumentKind Kind { get; }
    string DisplayName { get; }
    BoundingBox2D Bounds { get; }
    IReadOnlyList<Layer> Layers { get; }
    Scene2D Scene { get; }
    IReadOnlyDictionary<string, string> Metadata { get; }
    IReadOnlyList<Diagnostic> Diagnostics { get; }
}
public sealed class SyntheticDocument : IDocument
{
    public SyntheticDocument(string displayName, Scene2D scene, IReadOnlyList<Diagnostic>? diagnostics = null)
    { DocumentId = Guid.NewGuid(); DisplayName = displayName; Scene = scene; Diagnostics = diagnostics ?? Array.Empty<Diagnostic>(); Layers = scene.Layers.Select(x => x.Layer).ToArray(); }
    public Guid DocumentId { get; }
    public DocumentKind Kind => DocumentKind.Synthetic;
    public string DisplayName { get; }
    public BoundingBox2D Bounds => Scene.GetBounds();
    public IReadOnlyList<Layer> Layers { get; }
    public Scene2D Scene { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
