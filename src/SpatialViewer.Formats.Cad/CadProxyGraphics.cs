using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Reader-independent proxy graphic retained from an application-defined CAD object.
/// These primitives are display fallbacks only; they do not imply native Tianzheng semantics.
/// </summary>
public abstract record CadProxyPrimitive(string SourceKind);

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
/// Display-only text presentation supplied by a proxy-graphics stream. This retains only the
/// presentation fields explicitly exposed by the reader and is not a native custom-object semantic.
/// </summary>
public sealed record CadProxyText(
    Point2D Origin,
    string Text,
    double Height,
    double RotationRadians,
    double WidthFactor,
    double ObliqueAngleRadians,
    string ProxyTextKind)
    : CadProxyPrimitive(ProxyTextKind);
