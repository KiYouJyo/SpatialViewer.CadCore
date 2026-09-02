using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Reader-independent proxy graphic retained from an application-defined CAD object.
/// These primitives are display fallbacks only; they do not imply native Tianzheng semantics.
/// </summary>
public abstract record CadProxyPrimitive(string SourceKind);

public sealed record CadProxyPolyline(IReadOnlyList<Point2D> Points)
    : CadProxyPrimitive("Polyline");

public sealed record CadProxyPolygon(IReadOnlyList<Point2D> Points)
    : CadProxyPrimitive("Polygon");

public sealed record CadProxyCircle(Point2D Center, double Radius)
    : CadProxyPrimitive("Circle");

public sealed record CadProxyArc(Point2D Center, double Radius, double StartRadians, double SweepRadians)
    : CadProxyPrimitive("CircularArc");
