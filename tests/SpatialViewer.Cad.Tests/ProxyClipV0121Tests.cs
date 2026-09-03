using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyClipV0121Tests
{
    [Fact]
    public void TwoPointClipMapsToRectangleAndRestrictsScene()
    {
        var graphics = new IProxyGeometry[]
        {
            Clip(new CSMath.XY(0, 0), new CSMath.XY(10, 10)),
            Line(-5, 5, 15, 5),
            new ProxyPopClip()
        };

        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out var stateful);
        Assert.True(stateful);
        Assert.Equal(0, unsupported);
        var group = Assert.IsType<CadProxyClipGroup>(Assert.Single(mapped));
        Assert.Equal(4, group.ClipPolygon.Count);
        Assert.IsType<CadProxyPolyline>(Assert.Single(group.Children));

        var custom = Custom(mapped);
        var scene = Document(custom).Scene;
        var item = Assert.Single(scene.GetItems());
        Assert.Single(item.ClipPolygons);
        Assert.Equal(0, item.Bounds.MinX, 12);
        Assert.Equal(10, item.Bounds.MaxX, 12);
        Assert.Equal(custom.ObjectId, HitTesting.HitTest(scene, new Point2D(5, 5), 0)?.Id);
        Assert.Null(HitTesting.HitTest(scene, new Point2D(12, 5), 0));
    }

    [Fact]
    public void ModelTransformAcceptsMatchingInverseAndClipOutlivesTransformScope()
    {
        var model = CSMath.Matrix4.CreateTranslation(new CSMath.XYZ(100, 50, 0));
        var inverse = CSMath.Matrix4.CreateTranslation(new CSMath.XYZ(-100, -50, 0));
        var graphics = new IProxyGeometry[]
        {
            new ProxyPushModelTransform { TransformationMatrix = model },
            BuildClip(new[] { new CSMath.XY(0, 0), new CSMath.XY(10, 10) }, inverse: inverse),
            Line(-5, 5, 15, 5),
            new ProxyPopModelTransform(),
            new ProxyPopClip()
        };

        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out _);
        Assert.Equal(0, unsupported);
        var group = Assert.IsType<CadProxyClipGroup>(Assert.Single(mapped));
        Assert.Equal(new Point2D(100, 50), group.ClipPolygon[0]);
        Assert.Equal(new Point2D(110, 60), group.ClipPolygon[2]);
        var line = Assert.IsType<CadProxyPolyline>(Assert.Single(group.Children));
        Assert.Equal(new Point2D(95, 55), line.Points[0]);
        Assert.Equal(new Point2D(115, 55), line.Points[1]);

        var scene = Document(Custom(mapped)).Scene;
        Assert.NotNull(HitTesting.HitTest(scene, new Point2D(105, 55), 0));
        Assert.Null(HitTesting.HitTest(scene, new Point2D(112, 55), 0));
    }

    [Fact]
    public void NestedClipScopesRemainNested()
    {
        var graphics = new IProxyGeometry[]
        {
            Clip(new CSMath.XY(0, 0), new CSMath.XY(20, 20)),
            Clip(new CSMath.XY(5, 5), new CSMath.XY(15, 5), new CSMath.XY(5, 15)),
            Line(0, 10, 20, 10),
            new ProxyPopClip(),
            new ProxyPopClip()
        };

        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out _);
        Assert.Equal(0, unsupported);
        var outer = Assert.IsType<CadProxyClipGroup>(Assert.Single(mapped));
        var inner = Assert.IsType<CadProxyClipGroup>(Assert.Single(outer.Children));
        Assert.Equal(3, inner.ClipPolygon.Count);
        var scene = Document(Custom(mapped)).Scene;
        var item = Assert.Single(scene.GetItems());
        Assert.Equal(2, item.ClipPolygons.Count);
        Assert.NotNull(HitTesting.HitTest(scene, new Point2D(7, 10), 0));
        Assert.Null(HitTesting.HitTest(scene, new Point2D(14, 10), 0));
    }

    [Fact]
    public void DrawBoundarySurvivesAsOutlineWithoutInventingFill()
    {
        var graphics = new IProxyGeometry[]
        {
            BuildClip(new[] { new CSMath.XY(0, 0), new CSMath.XY(10, 10) }, drawBoundary: true),
            new ProxyPopClip()
        };

        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out _);
        Assert.Equal(0, unsupported);
        var group = Assert.IsType<CadProxyClipGroup>(Assert.Single(mapped));
        Assert.True(group.DrawBoundary);
        Assert.Empty(group.Children);

        var item = Assert.Single(Document(Custom(mapped)).Scene.GetItems());
        var outline = Assert.IsType<PolylineGeometry>(item.Geometry);
        Assert.Equal(5, outline.Points.Count);
        Assert.Equal(outline.Points[0], outline.Points[^1]);
        Assert.Equal(bool.TrueString, item.Metadata["ProxyClipBoundary"]);
        Assert.Single(item.ClipPolygons);
    }

    [Fact]
    public void UnsupportedClipStateAndMatrixMismatchFailClosed()
    {
        var translatedClipSpace = CSMath.Matrix4.CreateTranslation(new CSMath.XYZ(1, 0, 0));
        var model = CSMath.Matrix4.CreateTranslation(new CSMath.XYZ(100, 50, 0));
        var wrongInverse = CSMath.Matrix4.CreateTranslation(new CSMath.XYZ(-99, -50, 0));

        AssertClosed(new IProxyGeometry[] { BuildClip(new[] { new CSMath.XY(0, 0), new CSMath.XY(10, 10) }, front: true), Line(0, 5, 10, 5), new ProxyPopClip() });
        AssertClosed(new IProxyGeometry[] { BuildClip(new[] { new CSMath.XY(0, 0), new CSMath.XY(10, 10) }, back: true), Line(0, 5, 10, 5), new ProxyPopClip() });
        AssertClosed(new IProxyGeometry[] { BuildClip(new[] { new CSMath.XY(0, 0), new CSMath.XY(10, 10) }, extrusion: new CSMath.XYZ(0, 0, -1)), Line(0, 5, 10, 5), new ProxyPopClip() });
        AssertClosed(new IProxyGeometry[] { BuildClip(new[] { new CSMath.XY(0, 0), new CSMath.XY(10, 10) }, origin: new CSMath.XYZ(1, 0, 0)), Line(0, 5, 10, 5), new ProxyPopClip() });
        AssertClosed(new IProxyGeometry[] { BuildClip(new[] { new CSMath.XY(0, 0), new CSMath.XY(10, 10) }, clipTransform: translatedClipSpace), Line(0, 5, 10, 5), new ProxyPopClip() });
        AssertClosed(new IProxyGeometry[] { new ProxyPushModelTransform { TransformationMatrix = model }, BuildClip(new[] { new CSMath.XY(0, 0), new CSMath.XY(10, 10) }, inverse: wrongInverse), Line(0, 5, 10, 5), new ProxyPopModelTransform(), new ProxyPopClip() });
        AssertClosed(new IProxyGeometry[] { Clip(new CSMath.XY(0, 0)), Line(0, 5, 10, 5), new ProxyPopClip() });
        AssertClosed(new IProxyGeometry[] { Clip(new CSMath.XY(0, 0), new CSMath.XY(10, 10)), Line(0, 5, 10, 5) });
        AssertClosed(new IProxyGeometry[] { new ProxyPopClip() });
    }

    [Fact]
    public void ClipFreeStreamStillUsesEstablishedProxyMapperBehavior()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxyCircle
            {
                Center = new CSMath.XYZ(5, 6, 0),
                Radius = 3,
                Normal = CSMath.XYZ.AxisZ
            }
        };

        var established = ACadSharpProxyGraphicsMapping.Map(graphics, out var establishedUnsupported, out var establishedStateful);
        var clipAware = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var clipUnsupported, out var clipStateful);
        Assert.Equal(establishedUnsupported, clipUnsupported);
        Assert.Equal(establishedStateful, clipStateful);
        Assert.Equal(established, clipAware);
    }

    private static void AssertClosed(IProxyGeometry[] graphics)
    {
        var mapped = ACadSharpProxyGraphicsClipMapping.Map(graphics, out var unsupported, out var stateful);
        Assert.True(stateful);
        Assert.Empty(mapped);
        Assert.Equal(graphics.Length, unsupported);
    }

    private static ProxyPushClip Clip(params CSMath.XY[] points) => BuildClip(points);

    private static ProxyPushClip BuildClip(
        CSMath.XY[] points,
        CSMath.Matrix4? inverse = null,
        CSMath.Matrix4? clipTransform = null,
        CSMath.XYZ? extrusion = null,
        CSMath.XYZ? origin = null,
        bool front = false,
        bool back = false,
        bool drawBoundary = false)
        => new()
        {
            Extrusion = extrusion ?? CSMath.XYZ.AxisZ,
            ClipBoundaryOrigin = origin ?? new CSMath.XYZ(0, 0, 0),
            ClipBoundary = points.ToList(),
            PointCount = points.Length,
            ClipBoundaryTransformMatrix = clipTransform ?? CSMath.Matrix4.Identity,
            InverseBlockTransformMatrix = inverse ?? CSMath.Matrix4.Identity,
            FrontClipOn = front,
            BackClipOn = back,
            FrontClip = 0,
            BackClip = 0,
            DrawBoundary = drawBoundary
        };

    private static ProxyPolyline Line(double x1, double y1, double x2, double y2)
        => new() { Points = new() { new CSMath.XYZ(x1, y1, 0), new CSMath.XYZ(x2, y2, 0) } };

    private static CadCustomEntity Custom(IReadOnlyList<CadProxyPrimitive> primitives)
        => new("PX-CLIP", "TCH_PROXY_CLIP") { Representation = CadCustomEntityRepresentation.ProxyGraphics, ProxyPrimitives = primitives };

    private static CadDocument Document(CadCustomEntity custom)
        => new("proxy-clip.dwg", "DWG", "AC1032", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(7)) }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { custom });
}
