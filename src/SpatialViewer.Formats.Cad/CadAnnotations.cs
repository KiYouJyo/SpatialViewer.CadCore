using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>Reader-independent dimension families retained as CAD semantics rather than anonymous dimension-picture blocks.</summary>
public enum CadDimensionKind
{
    Unknown,
    Linear,
    Aligned,
    Angular2Line,
    Angular3Point,
    Radius,
    Diameter,
    Ordinate,
    ArcLength
}

/// <summary>Semantic dimension record. Reference points keep subtype-specific definition points by stable names.</summary>
public sealed record CadDimensionEntity(
    string Handle,
    CadDimensionKind Kind,
    Point2D DefinitionPoint,
    Point2D TextPosition,
    string Text,
    double Measurement,
    double RotationRadians,
    double TextHeight,
    double ArrowSize,
    string StyleName,
    IReadOnlyDictionary<string, Point2D> ReferencePoints,
    string LayerName = "0",
    CadColor Color = default,
    bool IsVisible = true,
    string LineTypeName = "Continuous",
    int? LineWeight = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
    : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);

/// <summary>One semantic leader path, used by both MLEADER and future compound annotation entities.</summary>
public sealed record CadLeaderPath(
    IReadOnlyList<Point2D> Points,
    bool IsSpline = false,
    double ArrowSize = 0,
    Point2D? ConnectionPoint = null,
    Point2D? LandingEndPoint = null);

/// <summary>Classic LEADER semantic data, including the linked annotation identity without flattening it into the path.</summary>
public sealed record CadLeaderEntity(
    string Handle,
    IReadOnlyList<Point2D> Vertices,
    bool ArrowHeadEnabled,
    bool IsSpline,
    string AnnotationHandle,
    string AnnotationType,
    string AnnotationText,
    Point2D? AnnotationPoint,
    double TextHeight,
    string StyleName,
    string LayerName = "0",
    CadColor Color = default,
    bool IsVisible = true,
    string LineTypeName = "Continuous",
    int? LineWeight = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
    : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);

/// <summary>MLEADER semantic data. Multiple raw leader paths and embedded content survive reader adaptation independently of rendering.</summary>
public sealed record CadMultiLeaderEntity(
    string Handle,
    IReadOnlyList<CadLeaderPath> Paths,
    string Text,
    Point2D TextLocation,
    double TextHeight,
    double TextRotationRadians,
    string ContentType,
    bool EnableDogleg,
    double LandingDistance,
    double ArrowSize,
    string StyleName,
    string LayerName = "0",
    CadColor Color = default,
    bool IsVisible = true,
    string LineTypeName = "Continuous",
    int? LineWeight = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
    : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value);
