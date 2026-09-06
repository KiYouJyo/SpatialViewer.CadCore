using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanLandCodeOverlayV01214Tests
{
    private static readonly double[] ZeroBulges = { 0d, 0d, 0d, 0d };

    [Fact]
    public void YdCodeSceneLayerRendersAfterOpaqueParcelLayers()
    {
        var custom = Land("LZX-A33", "YD-A33");
        var document = Document(custom);

        var codeLayer = Assert.Single(document.Scene.Layers, layer => layer.Layer.Name == "YD-CODE");
        Assert.Equal(int.MaxValue, codeLayer.Layer.Order);
        Assert.True(codeLayer.Layer.Metadata.TryGetValue("XiangyuanCodeOverlayLayer", out var overlay));
        Assert.Equal(bool.TrueString, overlay);

        var ordered = document.Scene.Layers.ToArray();
        Assert.Equal("YD-CODE", ordered[^1].Layer.Name);
    }

    [Fact]
    public void GeneratedCodeUsesLegibleParcelScaledHeight()
    {
        var custom = Land("LZX-A33", "YD-A33");
        var document = Document(custom);

        var item = Assert.Single(document.Scene.GetItems(), candidate =>
            candidate.Layer.Name == "YD-CODE"
            && candidate.Metadata.ContainsKey("XiangyuanLandCodeDisplayFallback"));
        var text = Assert.IsType<TextGeometry>(item.Geometry);

        Assert.Equal("A33", text.Text);
        Assert.InRange(text.Height, 1.43, 1.45);
        Assert.Equal(bool.TrueString, item.Metadata["XiangyuanLandCodeOverlay"]);
    }

    [Fact]
    public void HidingYdCodeStillHidesOverlayDespiteRenderOrderOverride()
    {
        var custom = Land("LZX-B1", "YD-B1");
        var document = Document(custom, codeVisible: false);

        Assert.DoesNotContain(document.Scene.GetItems(), item =>
            item.Metadata.ContainsKey("XiangyuanLandCodeDisplayFallback"));
        Assert.Contains(document.Scene.GetItems(visibleOnly: false), item =>
            item.Metadata.ContainsKey("XiangyuanLandCodeDisplayFallback"));
    }

    private static CadCustomEntity Land(string handle, string layer)
        => new(handle, "LZX_LAND", layer)
        {
            ClassDefinition = new CadCustomClassDefinition(
                "LZX_LAND",
                "AcdbLzxLand",
                "LZXOBJ|湘源控规",
                700,
                1,
                true,
                "None",
                false),
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
                    ZeroBulges,
                    true)
            }
        };

    private static CadDocument Document(CadCustomEntity custom, bool codeVisible = true)
        => new(
            "xiangyuan-code-overlay.dwg",
            "DWG",
            "AC1032",
            CadUnits.Metres,
            new[]
            {
                new CadLayer("0", CadColor.FromAci(7)),
                new CadLayer("YD-CODE", CadColor.FromAci(7), codeVisible),
                new CadLayer(custom.LayerName, CadColor.FromAci(3))
            },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });
}
