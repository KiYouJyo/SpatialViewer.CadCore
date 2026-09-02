using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

public enum CadUnits { Unitless, Millimetres, Centimetres, Metres, Inches, Feet }
public enum CadColorKind { ByLayer, ByBlock, Aci, TrueColor }
public readonly record struct CadColor(CadColorKind Kind, int Index = 256, byte Red = 0, byte Green = 0, byte Blue = 0)
{
    public static CadColor ByLayer { get; } = new(CadColorKind.ByLayer, 256);
    public static CadColor ByBlock { get; } = new(CadColorKind.ByBlock, 0);
    public static CadColor FromAci(int index) => new(CadColorKind.Aci, index);
    public static CadColor FromRgb(byte red, byte green, byte blue) => new(CadColorKind.TrueColor, 0, red, green, blue);
}
public sealed record CadLayer(string Name, CadColor Color, bool IsVisible = true, bool IsLocked = false, string LineTypeName = "Continuous", int? LineWeight = null);
public abstract record CadEntity(string Handle, string LayerName, CadColor Color, bool IsVisible, string LineTypeName, int? LineWeight, IReadOnlyDictionary<string, string> Metadata)
{
    public ObjectId ObjectId => CadIds.ToObjectId(Handle);
}
public sealed record CadPointEntity(string Handle, Point2D Position, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
public sealed record CadLineEntity(string Handle, Point2D Start, Point2D End, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
public sealed record CadCircleEntity(string Handle, Point2D Center, double Radius, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
public sealed record CadArcEntity(string Handle, Point2D Center, double Radius, double StartRadians, double SweepRadians, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
public sealed record CadEllipseEntity(string Handle, Point2D Center, double RadiusX, double RadiusY, double RotationRadians = 0, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
public sealed record CadPolylineEntity(string Handle, IReadOnlyList<Point2D> Vertices, bool IsClosed = false, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value)
{
    /// <summary>Per-vertex AutoCAD bulge. Each value describes the segment starting at the matching vertex; the final value applies to the closing segment when <see cref="IsClosed"/> is true.</summary>
    public IReadOnlyList<double> Bulges { get; init; } = Array.Empty<double>();
}

/// <summary>Reader-independent NURBS/fit-point definition retained without permanently flattening the source spline.</summary>
public sealed record CadSplineDefinition(int Degree, IReadOnlyList<Point2D> ControlPoints, IReadOnlyList<double> Knots, IReadOnlyList<double> Weights, IReadOnlyList<Point2D> FitPoints, bool IsClosed = false, bool IsPeriodic = false);
public sealed record CadSplineEntity(string Handle, CadSplineDefinition Spline, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);

public abstract record CadHatchEdge;
public sealed record CadHatchLineEdge(Point2D Start, Point2D End) : CadHatchEdge;
public sealed record CadHatchArcEdge(Point2D Center, double Radius, double StartRadians, double SweepRadians) : CadHatchEdge;
public sealed record CadHatchEllipseEdge(Point2D Center, Point2D MajorAxisEndPoint, double RadiusRatio, double StartRadians, double SweepRadians) : CadHatchEdge;
public sealed record CadHatchPolylineEdge(IReadOnlyList<Point2D> Vertices, IReadOnlyList<double> Bulges, bool IsClosed = true) : CadHatchEdge;
public sealed record CadHatchSplineEdge(CadSplineDefinition Spline) : CadHatchEdge;
public sealed record CadHatchLoop(IReadOnlyList<CadHatchEdge> Edges, string Flags = "");
/// <summary>One reader-independent line family from a CAD hatch pattern definition.</summary>
public sealed record CadHatchPatternLine(double AngleRadians, Point2D BasePoint, Vector2D Offset, IReadOnlyList<double> DashLengths);
public sealed record CadHatchEntity(string Handle, IReadOnlyList<CadHatchLoop> Loops, bool IsSolid = true, string PatternName = "SOLID", double PatternAngleRadians = 0, double PatternScale = 1, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value)
{
    /// <summary>Source PAT/DWG/DXF line families. Empty means the reader did not provide a drawable pattern definition.</summary>
    public IReadOnlyList<CadHatchPatternLine> PatternLines { get; init; } = Array.Empty<CadHatchPatternLine>();
}

public sealed record CadTextEntity(string Handle, Point2D InsertionPoint, string Text, double Height, double RotationRadians = 0, double Width = 0, bool IsMText = false, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value)
{
    public CadTextPresentation Presentation { get; init; } = new();
}
public sealed record CadAttributeEntity(string Handle, Point2D InsertionPoint, string Tag, string Value, double Height, double RotationRadians = 0, bool IsDefinition = false, string Prompt = "", bool IsConstant = false, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value)
{
    public CadTextPresentation Presentation { get; init; } = new();
}
public sealed record CadBlockReferenceEntity(string Handle, string BlockName, Point2D InsertionPoint, double RotationRadians = 0, double ScaleX = 1, double ScaleY = 1, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value)
{
    /// <summary>Instance attributes are stored alongside the INSERT but retain their own handles and world-space text placement.</summary>
    public IReadOnlyList<CadAttributeEntity> Attributes { get; init; } = Array.Empty<CadAttributeEntity>();
}
public sealed record CadUnsupportedEntity(string Handle, string EntityType, string LayerName = "0", IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, CadColor.ByLayer, true, "Continuous", null, Metadata ?? EmptyMetadata.Value);
public sealed record CadBlockDefinition(string Name, Point2D BasePoint, IReadOnlyList<CadEntity> Entities);

/// <summary>CAD-specific document whose primary scene is model space; paper layouts are available independently through <see cref="GetLayoutScene"/>.</summary>
public sealed class CadDocument : IDocument
{
    public CadDocument(string displayName, string sourceFormat, string version, CadUnits units, IReadOnlyList<CadLayer> layers, IReadOnlyList<CadBlockDefinition> blocks, IReadOnlyList<CadEntity> modelSpace, IReadOnlyList<Diagnostic>? diagnostics = null, IReadOnlyDictionary<string, string>? metadata = null, IReadOnlyList<CadLayoutDefinition>? layouts = null)
    {
        DocumentId = Guid.NewGuid(); DisplayName = displayName; SourceFormat = sourceFormat; Version = version; Units = units; CadLayers = layers; Blocks = blocks; ModelSpace = modelSpace; Diagnostics = diagnostics ?? Array.Empty<Diagnostic>(); Layouts = layouts ?? Array.Empty<CadLayoutDefinition>();
        Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata ?? new Dictionary<string, string>()) { ["SourceFormat"] = sourceFormat, ["CadVersion"] = version, ["Units"] = units.ToString(), ["LayoutCount"] = Layouts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        Scene = CadSceneTranslator.Translate(this);
        Layers = Scene.Layers.Select(x => x.Layer).ToArray();
    }
    public Guid DocumentId { get; }
    public DocumentKind Kind => DocumentKind.Cad;
    public string DisplayName { get; }
    public string SourceFormat { get; }
    public string Version { get; }
    public CadUnits Units { get; }
    public IReadOnlyList<CadLayer> CadLayers { get; }
    public IReadOnlyList<CadBlockDefinition> Blocks { get; }
    public IReadOnlyList<CadEntity> ModelSpace { get; }
    public IReadOnlyList<CadLayoutDefinition> Layouts { get; }
    public IReadOnlyList<CadCustomClassDefinition> CustomClasses { get; init; } = Array.Empty<CadCustomClassDefinition>();
    public BoundingBox2D Bounds => Scene.GetBounds();
    public IReadOnlyList<Layer> Layers { get; }
    public Scene2D Scene { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public Scene2D GetLayoutScene(string layoutName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutName);
        var layout = Layouts.FirstOrDefault(candidate => string.Equals(candidate.Name, layoutName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"CAD layout was not found: {layoutName}");
        return layout.IsPaperSpace ? CadLayoutSceneTranslator.Translate(this, layout) : Scene;
    }
}
internal static class EmptyMetadata { public static readonly IReadOnlyDictionary<string, string> Value = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()); }
internal static class CadIds { public static ObjectId ToObjectId(string value) { var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value)); return new ObjectId(new Guid(hash.AsSpan(0, 16))); } }
