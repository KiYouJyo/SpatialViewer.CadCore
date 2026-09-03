using System.Reflection;
using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyLayerProvenanceV0125Tests
{
    [Fact]
    public void SubentityLayerStateSurvivesByLayerColorWithoutGuessingItsMeaning()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxySubentLayer { LayerIndex = 37 },
            Line(0, 0, 10, 0),
            new ProxySubentColor { ColorIndex = 1 },
            Line(0, 1, 10, 1),
            new ProxySubentColor { ColorIndex = 256 },
            Line(0, 2, 10, 2)
        };
        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out _);
        Assert.Equal(1, unsupported);

        var (rewritten, handled) = ApplyLayerProvenance(graphics, mapped);

        Assert.Equal(1, handled);
        Assert.Equal(3, rewritten.Count);
        Assert.All(rewritten, primitive => Assert.Equal(37, primitive.Traits.LayerIndex));
        Assert.Null(rewritten[0].Traits.Color);
        Assert.Equal(CadColor.FromAci(1), rewritten[1].Traits.Color);
        Assert.Null(rewritten[2].Traits.Color);
    }

    [Fact]
    public void LayerStateFollowsProxyClipScopesAndPrimitiveOrder()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxySubentLayer { LayerIndex = 11 },
            Clip(new CSMath.XY(0, 0), new CSMath.XY(10, 10)),
            Line(-5, 5, 15, 5),
            new ProxyPopClip(),
            new ProxySubentLayer { LayerIndex = 23 },
            Line(0, 20, 10, 20)
        };
        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out _);
        Assert.Equal(2, unsupported);

        var (rewritten, handled) = ApplyLayerProvenance(graphics, mapped);

        Assert.Equal(2, handled);
        Assert.Equal(2, rewritten.Count);
        var group = Assert.IsType<CadProxyClipGroup>(rewritten[0]);
        var clipped = Assert.IsType<CadProxyPolyline>(Assert.Single(group.Children));
        var outside = Assert.IsType<CadProxyPolyline>(rewritten[1]);
        Assert.Equal(11, clipped.Traits.LayerIndex);
        Assert.Equal(23, outside.Traits.LayerIndex);
    }

    [Fact]
    public void ProvenanceFailsClosedWhenSourceAndMappedPrimitiveCountsDoNotReconcile()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxySubentLayer { LayerIndex = 7 },
            Line(0, 0, 10, 0),
            Line(0, 1, 10, 1)
        };
        IReadOnlyList<CadProxyPrimitive> incomplete = new CadProxyPrimitive[]
        {
            new CadProxyPolyline(new[] { new SpatialViewer.Core.Point2D(0, 0), new SpatialViewer.Core.Point2D(10, 0) })
        };

        var (rewritten, handled) = ApplyLayerProvenance(graphics, incomplete);

        Assert.Equal(0, handled);
        Assert.Same(incomplete, rewritten);
        Assert.Null(rewritten[0].Traits.LayerIndex);
    }

    private static (IReadOnlyList<CadProxyPrimitive> Primitives, int Handled) ApplyLayerProvenance(
        IProxyGeometry[] graphics,
        IReadOnlyList<CadProxyPrimitive> mapped)
    {
        var type = typeof(ACadSharpCadImporter).Assembly.GetType(
            "SpatialViewer.Formats.Cad.ACadSharp.ACadSharpProxyLayerProvenance",
            throwOnError: true)!;
        var method = type.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!;
        var arguments = new object?[] { graphics, mapped, 0 };
        var result = (IReadOnlyList<CadProxyPrimitive>)method.Invoke(null, arguments)!;
        return (result, (int)arguments[2]!);
    }

    private static ProxyPolyline Line(double x1, double y1, double x2, double y2)
        => new() { Points = new() { new CSMath.XYZ(x1, y1, 0), new CSMath.XYZ(x2, y2, 0) } };

    private static ProxyPushClip Clip(params CSMath.XY[] points)
        => new()
        {
            Extrusion = CSMath.XYZ.AxisZ,
            ClipBoundaryOrigin = new CSMath.XYZ(0, 0, 0),
            ClipBoundary = points.ToList(),
            PointCount = points.Length,
            ClipBoundaryTransformMatrix = CSMath.Matrix4.Identity,
            InverseBlockTransformMatrix = CSMath.Matrix4.Identity,
            FrontClipOn = false,
            BackClipOn = false,
            FrontClip = 0,
            BackClip = 0,
            DrawBoundary = false
        };
}
