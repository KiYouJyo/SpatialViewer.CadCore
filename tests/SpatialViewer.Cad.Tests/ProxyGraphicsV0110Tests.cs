using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyGraphicsV0110Tests
{
    [Fact]
    public void ReaderIndependentProxyPrimitivesReachSceneAsFallbackGeometry()
    {
        var definition = new CadCustomClassDefinition("TCH_WALL", "TDbWall", "Tianzheng Architecture", 501, 1, true, "None", false);
        var custom = new CadCustomEntity("PX01", "TCH_WALL")
        {
            ClassDefinition = definition,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline", "Polygon", "Circle", "CircularArc" },
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 5) }),
                new CadProxyPolygon(new[] { new Point2D(20, 0), new Point2D(25, 0), new Point2D(25, 5), new Point2D(20, 5) }),
                new CadProxyCircle(new Point2D(40, 5), 3),
                new CadProxyArc(new Point2D(55, 5), 4, 0, Math.PI / 2)
            }
        };
        var document = new CadDocument(
            "proxy-fallback.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });

        var items = document.Scene.GetItems().Where(item => item.Id == custom.ObjectId).ToArray();

        Assert.Equal(4, items.Length);
        Assert.Contains(items, item => item.Geometry is PolylineGeometry);
        Assert.Contains(items, item => item.Geometry is PolygonGeometry);
        Assert.Contains(items, item => item.Geometry is CircleGeometry);
        Assert.Contains(items, item => item.Geometry is ArcGeometry);
        Assert.All(items, item =>
        {
            Assert.Equal(bool.TrueString, item.Metadata["CustomProxyFallback"]);
            Assert.Equal(bool.FalseString, item.Metadata["NativeSemanticsDecoded"]);
            Assert.Equal(bool.FalseString, item.Metadata["ProxyTraitsApplied"]);
        });
    }

    [Fact]
    public void ACadSharpMappingCopiesSafePlanarProxyGeometry()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxyPolyline { Points = new() { new CSMath.XYZ(1, 2, 0), new CSMath.XYZ(4, 6, 0) } },
            new ProxyPolygon { Points = new() { new CSMath.XYZ(10, 10, 0), new CSMath.XYZ(12, 10, 0), new CSMath.XYZ(12, 12, 0) } },
            new ProxyCircle { Center = new CSMath.XYZ(20, 5, 0), Radius = 2, Normal = CSMath.XYZ.AxisZ },
            new ProxyCircularArc
            {
                Center = new CSMath.XYZ(30, 5, 0),
                Radius = 3,
                Normal = CSMath.XYZ.AxisZ,
                StartVectorDirection = new CSMath.XYZ(1, 0, 0),
                SweepAngle = Math.PI / 2
            }
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(graphics, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(0, unsupported);
        Assert.Collection(
            mapped,
            primitive => Assert.IsType<CadProxyPolyline>(primitive),
            primitive => Assert.IsType<CadProxyPolygon>(primitive),
            primitive => Assert.IsType<CadProxyCircle>(primitive),
            primitive => Assert.IsType<CadProxyArc>(primitive));
        var arc = Assert.IsType<CadProxyArc>(mapped[3]);
        Assert.Equal(0, arc.StartRadians, 12);
        Assert.Equal(Math.PI / 2, arc.SweepRadians, 12);
    }

    [Fact]
    public void StatefulTransformCommandsBlockPotentiallyMisplacedFallbackGeometry()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxyPushModelTransform(),
            new ProxyPolyline { Points = new() { new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0) } }
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(graphics, out var unsupported, out var stateful);

        Assert.True(stateful);
        Assert.Empty(mapped);
        Assert.Equal(graphics.Length, unsupported);
    }
}
