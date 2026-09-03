using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyTraitsV0121Tests
{
    [Fact]
    public void ExplicitAciTrueColorAndLineWeightAreSnapshottedUntilReset()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxySubentColor { ColorIndex = 1 },
            new ProxySubentLineWeight { LineWeight = global::ACadSharp.LineWeightType.W200 },
            Line(0, 0, 10, 0),
            new ProxySubentTrueColor
            {
                ColorMethod = ProxyColorMethod.ByColor,
                Color = new global::ACadSharp.Color(12, 34, 56)
            },
            Line(0, 1, 10, 1),
            new ProxySubentColor { ColorIndex = 256 },
            new ProxySubentLineWeight { LineWeight = global::ACadSharp.LineWeightType.ByLayer },
            Line(0, 2, 10, 2)
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(graphics, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(0, unsupported);
        Assert.Equal(3, mapped.Count);

        Assert.Equal(CadColor.FromAci(1), mapped[0].Traits.Color);
        Assert.Equal(200, mapped[0].Traits.LineWeight);

        Assert.Equal(CadColor.FromRgb(12, 34, 56), mapped[1].Traits.Color);
        Assert.Equal(200, mapped[1].Traits.LineWeight);

        Assert.False(mapped[2].Traits.HasOverrides);
    }

    [Fact]
    public void UnsupportedTraitStateClearsStaleOverrideInsteadOfGuessing()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxySubentColor { ColorIndex = 3 },
            Line(0, 0, 10, 0),
            new ProxySubentTrueColor
            {
                ColorMethod = ProxyColorMethod.Foreground,
                Color = global::ACadSharp.Color.Default
            },
            new ProxySubentLineWeight { LineWeight = global::ACadSharp.LineWeightType.ByDIPs },
            Line(0, 1, 10, 1)
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(graphics, out var unsupported, out _);

        Assert.Equal(2, unsupported);
        Assert.Equal(2, mapped.Count);
        Assert.Equal(CadColor.FromAci(3), mapped[0].Traits.Color);
        Assert.False(mapped[1].Traits.HasOverrides);
    }

    [Fact]
    public void ClipAwareMapperCarriesTraitStateIntoScopedChildren()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxySubentColor { ColorIndex = 5 },
            new ProxySubentLineWeight { LineWeight = global::ACadSharp.LineWeightType.W100 },
            Clip(new CSMath.XY(0, 0), new CSMath.XY(10, 10)),
            Line(-5, 5, 15, 5),
            new ProxyPopClip()
        };

        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out var stateful);

        Assert.True(stateful);
        Assert.Equal(0, unsupported);
        var group = Assert.IsType<CadProxyClipGroup>(Assert.Single(mapped));
        var line = Assert.IsType<CadProxyPolyline>(Assert.Single(group.Children));
        Assert.Equal(CadColor.FromAci(5), line.Traits.Color);
        Assert.Equal(100, line.Traits.LineWeight);
        Assert.True(CadProxyTraitInspector.HasOverrides(mapped));
    }

    [Fact]
    public void ProxyTraitsActuallyOverrideSceneStrokeAndWidth()
    {
        var custom = new CadCustomEntity("PX-TRAITS", "TCH_PROXY_TRAITS", Color: CadColor.FromAci(7))
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[] { new Point2D(0, 0), new Point2D(10, 0) })
                {
                    Traits = new CadProxyTraits(CadColor.FromRgb(12, 34, 56), 200)
                },
                new CadProxyPolyline(new[] { new Point2D(0, 5), new Point2D(10, 5) })
            }
        };
        var document = new CadDocument(
            "proxy-traits.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });

        var items = document.Scene.GetItems().Where(item => item.Id == custom.ObjectId).ToArray();
        Assert.Equal(2, items.Length);

        var styled = Assert.Single(items, item => item.Metadata.ContainsKey("ProxyColorOverride"));
        var inherited = Assert.Single(items, item => !item.Metadata.ContainsKey("ProxyColorOverride"));
        Assert.Equal("#0C2238", styled.Style.Stroke);
        Assert.Equal(2d, styled.Style.StrokeWidth, 12);
        Assert.Equal(bool.TrueString, styled.Metadata["ProxyPrimitiveTraitsApplied"]);
        Assert.Equal(bool.TrueString, styled.Metadata["ProxyLineWeightOverride"]);
        Assert.Equal(bool.TrueString, styled.Metadata["ProxyTraitsApplied"]);
        Assert.Equal("#FFFFFF", inherited.Style.Stroke);
        Assert.Equal(1d, inherited.Style.StrokeWidth, 12);
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
