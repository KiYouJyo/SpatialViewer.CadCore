using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Display-only subentity trait overrides captured from an ObjectARX proxy-graphics stream.
/// Null values inherit the containing CAD custom entity's already-resolved presentation.
/// LayerIndex is retained as provenance until the source reader can prove an index-to-layer mapping.
/// </summary>
public readonly record struct CadProxyTraits(CadColor? Color = null, int? LineWeight = null, int? LayerIndex = null)
{
    public bool HasOverrides => Color is not null || LineWeight is not null || LayerIndex is not null;
}

/// <summary>
/// Reader-independent proxy graphic retained from an application-defined CAD object.
/// These primitives are display fallbacks only; they do not imply native Tianzheng semantics.
/// </summary>
public abstract record CadProxyPrimitive(string SourceKind)
{
    /// <summary>
    /// Evidence-backed display traits active when this primitive was emitted. These affect only
    /// fallback presentation and never promote proprietary object semantics.
    /// </summary>
    public CadProxyTraits Traits { get; init; }
}

public sealed record CadProxyPolyline(IReadOnlyList<Point2D> Points)
    : CadProxyPrimitive("Polyline");

/// <summary>Planar lightweight-polyline proxy retaining closed state and per-segment bulges.</summary>
public sealed record CadProxyLwPolyline(
    IReadOnlyList<Point2D> Points,
    IReadOnlyList<double> Bulges,
    bool IsClosed)
    : CadProxyPrimitive("LwPolyine");

public sealed record CadProxyPolygon(IReadOnlyList<Point2D> Points)
    : CadProxyPrimitive("Polygon");

public sealed record CadProxyCircle(Point2D Center, double Radius)
    : CadProxyPrimitive("Circle");

public sealed record CadProxyArc(Point2D Center, double Radius, double StartRadians, double SweepRadians)
    : CadProxyPrimitive("CircularArc");

/// <summary>
/// Scoped 2D proxy-graphics clip. The boundary and children are already expressed in the same
/// reader-independent object coordinate system. Nested groups preserve the original push/pop scope.
/// </summary>
public sealed record CadProxyClipGroup(
    IReadOnlyList<Point2D> ClipPolygon,
    IReadOnlyList<CadProxyPrimitive> Children,
    bool DrawBoundary = false)
    : CadProxyPrimitive("ClipGroup");

/// <summary>
/// Display-only text presentation supplied by a proxy-graphics stream. Text2/UnicodeText2 carry
/// explicit font and layout evidence; retain it here rather than flattening every custom-object label
/// to the same default UI font before scene translation.
/// </summary>
public sealed record CadProxyText(
    Point2D Origin,
    string Text,
    double Height,
    double RotationRadians,
    double WidthFactor,
    double ObliqueAngleRadians,
    string ProxyTextKind,
    string FontFileName = "",
    string BigFontFileName = "",
    string Typeface = "",
    double TrackingPercentage = 100,
    bool IsBackward = false,
    bool IsUpsideDown = false,
    bool IsVertical = false,
    bool IsRaw = false,
    bool IsUnderlined = false,
    bool IsOverlined = false)
    : CadProxyPrimitive(ProxyTextKind);

/// <summary>Helpers for reporting whether a proxy tree actually carries supported trait overrides.</summary>
public static class CadProxyTraitInspector
{
    public static bool HasOverrides(IEnumerable<CadProxyPrimitive> primitives)
    {
        ArgumentNullException.ThrowIfNull(primitives);
        return primitives.Any(HasOverrides);
    }

    private static bool HasOverrides(CadProxyPrimitive primitive)
        => primitive.Traits.HasOverrides
            || primitive is CadProxyClipGroup clip && clip.Children.Any(HasOverrides);
}
