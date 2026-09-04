using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyPolygonFillV0128Tests
{
    [Fact]
    public void ProxyPolygonUsesInheritedEntityColorAsFill()
    {
        var custom = new CadCustomEntity("PX-POLYGON", "PRIVATE_PROXY", Color: CadColor.FromAci(3))
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                Polygon()
            }
        };

        var item = Assert.Single(Document(custom).Scene.GetItems().Where(item => item.Id == custom.ObjectId));

        Assert.IsType<PolygonGeometry>(item.Geometry);
        Assert.Equal("#00FF00", item.Style.Stroke);
        Assert.Equal("#00FF00", item.Style.Fill);
        Assert.Equal(bool.TrueString, item.Metadata["ProxyPolygonFilled"]);
        Assert.Equal("EffectiveProxyColor", item.Metadata["ProxyPolygonFillSource"]);
    }

    [Fact]
    public void ProxyPolygonUsesPrimitiveColorOverrideForStrokeAndFill()
    {
        var polygon = Polygon() with
        {
            Traits = new CadProxyTraits(CadColor.FromRgb(12, 34, 56))
        };
        var custom = new CadCustomEntity("PX-POLYGON-COLOR", "PRIVATE_PROXY", Color: CadColor.FromAci(7))
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[] { polygon }
        };

        var item = Assert.Single(Document(custom).Scene.GetItems().Where(item => item.Id == custom.ObjectId));

        Assert.Equal("#0C2238", item.Style.Stroke);
        Assert.Equal("#0C2238", item.Style.Fill);
        Assert.Equal(bool.TrueString, item.Metadata["ProxyColorOverride"]);
        Assert.Equal(bool.TrueString, item.Metadata["ProxyPolygonFilled"]);
    }

    [Fact]
    public void ProxyPolylineAndClosedLwPolylineRemainUnfilled()
    {
        var custom = new CadCustomEntity("PX-OUTLINES", "PRIVATE_PROXY", Color: CadColor.FromAci(1))
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[]
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 0)
                }),
                new CadProxyLwPolyline(
                    new[]
                    {
                        new Point2D(20, 0),
                        new Point2D(30, 0),
                        new Point2D(30, 10),
                        new Point2D(20, 10)
                    },
                    new[] { 0d, 0d, 0d, 0d },
                    true)
            }
        };

        var items = Document(custom).Scene.GetItems().Where(item => item.Id == custom.ObjectId).ToArray();

        Assert.Equal(2, items.Length);
        Assert.All(items, item => Assert.Null(item.Style.Fill));
    }

    private static CadProxyPolygon Polygon()
        => new(new[]
        {
            new Point2D(0, 0),
            new Point2D(10, 0),
            new Point2D(10, 10),
            new Point2D(0, 10)
        });

    private static CadDocument Document(CadCustomEntity custom)
        => new(
            "proxy-polygon-fill.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });
}
