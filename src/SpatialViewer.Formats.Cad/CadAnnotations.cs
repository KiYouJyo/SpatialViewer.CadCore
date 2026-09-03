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

/// <summary>
/// Viewer-relevant DIMSTYLE semantics copied out of the source reader. Geometry fields are stored
/// after the source style scale has been applied so scene construction does not depend on reader types.
/// </summary>
public sealed record CadDimensionPresentation(
    double ExtensionLineOffset = 0,
    double ExtensionLineExtension = 0,
    double DimensionLineExtension = 0,
    double DimensionLineGap = 0,
    bool SuppressFirstExtensionLine = false,
    bool SuppressSecondExtensionLine = false,
    bool SuppressFirstDimensionLine = false,
    bool SuppressSecondDimensionLine = false,
    string ArrowBlockName = "",
    string FirstArrowBlockName = "",
    string SecondArrowBlockName = "",
    bool SeparateArrowBlocks = false,
    int DecimalPlaces = 2,
    char DecimalSeparator = '.',
    double Rounding = 0,
    string Prefix = "",
    string Suffix = "",
    bool GenerateTolerances = false,
    bool LimitsGeneration = false,
    double PlusTolerance = 0,
    double MinusTolerance = 0,
    int ToleranceDecimalPlaces = 2,
    double ToleranceScaleFactor = 1,
    bool AlternateUnitsEnabled = false,
    double AlternateUnitScaleFactor = 25.4,
    int AlternateUnitDecimalPlaces = 3,
    string AlternateUnitPrefix = "",
    string AlternateUnitSuffix = "",
    string LinearUnitFormat = "Decimal",
    string AngularUnitFormat = "DecimalDegrees");

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
    : CadEntity(Handle, LayerName, Color == default ? CadColor.ByLayer : Color, IsVisible, LineTypeName, LineWeight, Metadata ?? EmptyMetadata.Value)
{
    public CadDimensionPresentation Presentation { get; init; } = new();

    /// <summary>Explicit DIMCLRD component color. Null means the reader did not expose a separate DIMSTYLE color.</summary>
    public CadColor? DimensionLineColor { get; init; }

    /// <summary>Explicit DIMCLRE component color. Null means extension lines inherit the semantic entity style.</summary>
    public CadColor? ExtensionLineColor { get; init; }

    /// <summary>Explicit DIMCLRT component color. Null means dimension text inherits the semantic entity style.</summary>
    public CadColor? TextColor { get; init; }
}

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
