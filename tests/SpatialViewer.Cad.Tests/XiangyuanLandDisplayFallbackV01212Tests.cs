using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanLandDisplayFallbackV01212Tests
{
    [Fact]
    public void LzxLandUsesResolvedYdLayerColorForDisplayFillAndKeepsProxyOutline()
    {
        var definition = new CadCustomClassDefinition(
            "LZX_LAND",
            "AcdbLzxLand",
            "LZXOBJ|湘源控规",
            700,
            1,
            true,
            "None",
            false);
        var boundary = new CadProxyLwPolyline(
            new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 8),
                new Point2D(0, 8)
            },
            new[] { 0d, 0d, 0d, 0d },
            true)
        {
            Traits = new CadProxyTraits(Color: CadColor.FromAci(7), FillOn: true, MarkerId: 17)
        };
        var custom = new CadCustomEntity("LZX-1", "LZX_LAND", "YD-A33")
        {
            ClassDefinition = definition,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "SubentColor", "SubentMarker", "SubentFillon", "LwPolyine" },
            ProxyPrimitives = new CadProxyPrimitive[] { boundary }
        };
        var document = Document(custom, new CadLayer("YD-A33", CadColor.FromAci(3)));

        var items = document.Scene.GetItems().Where(item => item.Id == custom.ObjectId).ToArray();
        var fill = Assert.Single(items, item => item.Style.Fill is not null);
        var outline = Assert.Single(items, item => item.Style.Fill is null);

        Assert.IsType<PolygonGeometry>(fill.Geometry);
        Assert.Equal("#00FF00", fill.Style.Fill);
        Assert.Equal(0, fill.Style.StrokeWidth);
        Assert.Equal(bool.TrueString, fill.Metadata["XiangyuanLandDisplayFallback"]);
        Assert.Equal("ResolvedCadLayerColor", fill.Metadata["XiangyuanLandFillSource"]);
        Assert.Equal("ClosedProxyLwPolyline", fill.Metadata["XiangyuanLandBoundarySource"]);
        Assert.Equal(bool.FalseString, fill.Metadata["XiangyuanLandSemanticClaim"]);

        Assert.IsType<PolygonGeometry>(outline.Geometry);
        Assert.Equal("#FFFFFF", outline.Style.Stroke);
        Assert.Null(outline.Style.Fill);
        Assert.Equal("17", outline.Metadata["ProxyMarkerId"]);
    }

    [Fact]
    public void GenericClosedProxyPolylineIsNotPromotedToLandFill()
    {
        var custom = new CadCustomEntity("GENERIC-1", "PRIVATE_PROXY", "YD-A33")
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyLwPolyline(
                    new[]
                    {
                        new Point2D(0, 0),
                        new Point2D(10, 0),
                        new Point2D(10, 8),
                        new Point2D(0, 8)
                    },
                    new[] { 0d, 0d, 0d, 0d },
                    true)
            }
        };
        var document = Document(custom, new CadLayer("YD-A33", CadColor.FromAci(3)));

        var items = document.Scene.GetItems().Where(item => item.Id == custom.ObjectId).ToArray();

        Assert.Single(items);
        Assert.All(items, item => Assert.Null(item.Style.Fill));
        Assert.DoesNotContain(items, item => item.Metadata.ContainsKey("XiangyuanLandDisplayFallback"));
    }

    [Fact]
    public void LzxLandOutsideYdLayerRemainsProxyOnly()
    {
        var definition = new CadCustomClassDefinition(
            "LZX_LAND",
            "AcdbLzxLand",
            "LZXOBJ|湘源控规",
            700,
            1,
            true,
            "None",
            false);
        var custom = new CadCustomEntity("LZX-2", "LZX_LAND", "0")
        {
            ClassDefinition = definition,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyLwPolyline(
                    new[]
                    {
                        new Point2D(0, 0),
                        new Point2D(10, 0),
                        new Point2D(10, 8),
                        new Point2D(0, 8)
                    },
                    new[] { 0d, 0d, 0d, 0d },
                    true)
            }
        };
        var document = new CadDocument(
            "not-yd-layer.dwg",
            "DWG",
            "AC1032",
            CadUnits.Metres,
            new[] { new CadLayer("0", CadColor.FromAci(3)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });

        var items = document.Scene.GetItems().Where(item => item.Id == custom.ObjectId).ToArray();

        Assert.Single(items);
        Assert.All(items, item => Assert.Null(item.Style.Fill));
    }

    private static CadDocument Document(CadCustomEntity custom, CadLayer layer)
        => new(
            "xiangyuan-land-display-fallback.dwg",
            "DWG",
            "AC1032",
            CadUnits.Metres,
            new[] { new CadLayer("0", CadColor.FromAci(7)), layer },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });
}
