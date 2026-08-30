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
public sealed record CadPolylineEntity(string Handle, IReadOnlyList<Point2D> Vertices, bool IsClosed = false, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
public sealed record CadTextEntity(string Handle, Point2D InsertionPoint, string Text, double Height, double RotationRadians = 0, double Width = 0, bool IsMText = false, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
public sealed record CadBlockReferenceEntity(string Handle, string BlockName, Point2D InsertionPoint, double RotationRadians = 0, double ScaleX = 1, double ScaleY = 1, string LayerName = "0", CadColor Color = default, bool IsVisible = true, string LineTypeName = "Continuous", int? LineWeight = null, IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
public sealed record CadUnsupportedEntity(string Handle, string EntityType, string LayerName = "0", IReadOnlyDictionary<string, string>? Metadata = null) : CadEntity(Handle, LayerName, CadColor.ByLayer, true, "Continuous", null, Metadata ?? EmptyMetadata.Value);
public sealed record CadBlockDefinition(string Name, Point2D BasePoint, IReadOnlyList<CadEntity> Entities);

/// <summary>CAD-specific document whose public scene is generated by the CAD-to-Scene translator.</summary>
public sealed class CadDocument : IDocument
{
    public CadDocument(string displayName, string sourceFormat, string version, CadUnits units, IReadOnlyList<CadLayer> layers, IReadOnlyList<CadBlockDefinition> blocks, IReadOnlyList<CadEntity> modelSpace, IReadOnlyList<Diagnostic>? diagnostics = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        DocumentId = Guid.NewGuid(); DisplayName = displayName; SourceFormat = sourceFormat; Version = version; Units = units; CadLayers = layers; Blocks = blocks; ModelSpace = modelSpace; Diagnostics = diagnostics ?? Array.Empty<Diagnostic>();
        Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata ?? new Dictionary<string, string>()) { ["SourceFormat"] = sourceFormat, ["CadVersion"] = version, ["Units"] = units.ToString() });
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
    public BoundingBox2D Bounds => Scene.GetBounds();
    public IReadOnlyList<Layer> Layers { get; }
    public Scene2D Scene { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
internal static class EmptyMetadata { public static readonly IReadOnlyDictionary<string, string> Value = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()); }
internal static class CadIds { public static ObjectId ToObjectId(string value) { var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value)); return new ObjectId(new Guid(hash.AsSpan(0, 16))); } }
