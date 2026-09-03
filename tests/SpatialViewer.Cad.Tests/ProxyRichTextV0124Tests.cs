using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyRichTextV0124Tests
{
    [Fact]
    public void Text2PreservesShxBigFontAndUsesCadAwareCjkFallback()
    {
        var source = new IProxyGeometry[]
        {
            new ProxyText2
            {
                Normal = CSMath.XYZ.AxisZ,
                StartPoint = new CSMath.XYZ(10, 20, 0),
                TextDirection = new CSMath.XYZ(1, 0, 0),
                Text = "北京意铭创设咨询有限公司",
                Height = 5,
                WidthFactor = .75,
                ObliqueAngle = 0,
                TrackingPercentage = 82,
                FontFilename = "hztxt.shx",
                BigFontFilename = "hzfs.shx",
                IsBackwards = true
            }
        };

        var primitives = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out _);

        Assert.Equal(0, unsupported);
        var proxyText = Assert.IsType<CadProxyText>(Assert.Single(primitives));
        Assert.Equal(nameof(GraphicsType.Text2), proxyText.ProxyTextKind);
        Assert.Equal("hztxt.shx", proxyText.FontFileName);
        Assert.Equal("hzfs.shx", proxyText.BigFontFileName);
        Assert.Equal(82, proxyText.TrackingPercentage, 8);
        Assert.True(proxyText.IsBackward);

        var item = Assert.Single(Document(proxyText).Scene.GetItems());
        var geometry = Assert.IsType<TextGeometry>(item.Geometry);
        Assert.Equal("SimSun", geometry.FontFamily);
        Assert.Equal(.75, geometry.WidthFactor, 8);
        Assert.True(geometry.IsBackward);
        Assert.Equal("hztxt.shx", item.Metadata["ProxyTextFontFile"]);
        Assert.Equal("hzfs.shx", item.Metadata["ProxyTextBigFontFile"]);
        Assert.Equal("SimSun", item.Metadata["ProxyTextResolvedFontFamily"]);
    }

    [Fact]
    public void UnicodeText2PrefersEmbeddedTypefaceAndSurvivesModelTransform()
    {
        var source = new IProxyGeometry[]
        {
            new ProxyPushModelTransform
            {
                TransformationMatrix = CSMath.Matrix4.CreateTranslation(new CSMath.XYZ(100, 200, 0))
            },
            new ProxyUnicodeText2
            {
                Normal = CSMath.XYZ.AxisZ,
                StartPoint = new CSMath.XYZ(1, 2, 0),
                TextDirection = new CSMath.XYZ(1, 0, 0),
                Text = "修订日志",
                Height = 4,
                WidthFactor = .9,
                ObliqueAngle = 0,
                TrackingPercentage = 95,
                FontDescriptor = new TrueTypeFontDescriptor
                {
                    Typeface = "SimSun",
                    FontFilename = "simsun.ttc"
                },
                BigFontFilename = "",
                IsUpsideDown = true
            },
            new ProxyPopModelTransform()
        };

        var primitives = ACadSharpProxyGraphicsMapping.Map(source, out var unsupported, out var stateful);

        Assert.True(stateful);
        Assert.Equal(0, unsupported);
        var proxyText = Assert.IsType<CadProxyText>(Assert.Single(primitives));
        Assert.Equal(101, proxyText.Origin.X, 8);
        Assert.Equal(202, proxyText.Origin.Y, 8);
        Assert.Equal("SimSun", proxyText.Typeface);
        Assert.Equal("simsun.ttc", proxyText.FontFileName);
        Assert.True(proxyText.IsUpsideDown);

        var item = Assert.Single(Document(proxyText).Scene.GetItems());
        var geometry = Assert.IsType<TextGeometry>(item.Geometry);
        Assert.Equal("SimSun", geometry.FontFamily);
        Assert.True(geometry.IsUpsideDown);
        Assert.Equal("Typeface", item.Metadata["ProxyTextFontResolution"]);
    }

    private static CadDocument Document(CadProxyText text)
    {
        var custom = new CadCustomEntity("CUSTOM-TEXT", "TCH_PROXY_TEXT")
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyPrimitives = new CadProxyPrimitive[] { text },
            ProxyGraphicKinds = new[] { text.ProxyTextKind }
        };
        return new CadDocument(
            "proxy-text.dwg",
            "DWG",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });
    }
}
