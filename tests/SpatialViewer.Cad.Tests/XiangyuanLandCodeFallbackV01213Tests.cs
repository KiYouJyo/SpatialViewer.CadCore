using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanLandCodeFallbackV01213Tests
{
    private static readonly double[] ZeroBulges = { 0d, 0d, 0d, 0d };

    [Fact]
    public void LzxLandPublishesLayerSuffixAsCodeOnYdCodeLayer()
    {
        var custom = Land("LZX-A33", "YD-A33");
        var document = Document(custom, codeLayerVisible: true, CadColor.FromAci(7));

        var label = Assert.Single(document.Scene.GetItems(), item =>
            item.Layer.Name == "YD-CODE"
            && item.Metadata.TryGetValue("XiangyuanLandCodeDisplayFallback", out var value)
            && value == bool.TrueString);

        var text = Assert.IsType<TextGeometry>(label.Geometry);
        Assert.Equal("A33", text.Text);
        Assert.Equal(TextHorizontalAlignment2D.Center, text.HorizontalAlignment);
        Assert.Equal(TextVerticalAlignment2D.Middle, text.VerticalAlignment);
        Assert.Equal("#FFFFFF", label.Style.Stroke);
        Assert.Equal("SourceLayerSuffix", label.Metadata["XiangyuanLandCodeSource"]);
        Assert.Equal("YD-A33", label.Metadata["XiangyuanLandCodeSourceLayer"]);
        Assert.Equal(bool.FalseString, label.Metadata["XiangyuanLandSemanticClaim"]);
        Assert.InRange(text.Origin.X, 4.9, 5.1);
        Assert.InRange(text.Origin.Y, 3.9, 4.1);
    }

    [Fact]
    public void YdCodeLayerVisibilityControlsGeneratedLabels()
    {
        var custom = Land("LZX-B1", "YD-B1");
        var document = Document(custom, codeLayerVisible: false, CadColor.FromAci(2));

        Assert.DoesNotContain(document.Scene.GetItems(), item =>
            item.Metadata.ContainsKey("XiangyuanLandCodeDisplayFallback"));

        var label = Assert.Single(document.Scene.GetItems(visibleOnly: false), item =>
            item.Metadata.ContainsKey("XiangyuanLandCodeDisplayFallback"));
        Assert.Equal("YD-CODE", label.Layer.Name);
        Assert.Equal("#FFFF00", label.Style.Stroke);
    }

    [Fact]
    public void NonXiangyuanOrNonYdLayerDoesNotInventCodeLabel()
    {
        var generic = new CadCustomEntity("GENERIC", "PRIVATE_PROXY", "YD-A33")
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[] { Boundary() }
        };
        var lzxOnZero = Land("LZX-ZERO", "0");
        var document = new CadDocument(
            "xiangyuan-code-negative.dwg",
            "DWG",
            "AC1032",
            CadUnits.Metres,
            new[]
            {
                new CadLayer("0", CadColor.FromAci(7)),
                new CadLayer("YD-A33", CadColor.FromAci(3)),
                new CadLayer("YD-CODE", CadColor.FromAci(7))
            },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { generic, lzxOnZero });

        Assert.DoesNotContain(document.Scene.GetItems(false), item =>
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
            ProxyPrimitives = new CadProxyPrimitive[] { Boundary() }
        };

    private static CadProxyLwPolyline Boundary()
        => new(
            new[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 8),
                new Point2D(0, 8)
            },
            ZeroBulges,
            true);

    private static CadDocument Document(CadCustomEntity custom, bool codeLayerVisible, CadColor codeColor)
        => new(
            "xiangyuan-code-fallback.dwg",
            "DWG",
            "AC1032",
            CadUnits.Metres,
            new[]
            {
                new CadLayer("0", CadColor.FromAci(7)),
                new CadLayer(custom.LayerName, CadColor.FromAci(3)),
                new CadLayer("YD-CODE", codeColor, codeLayerVisible)
            },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });
}
