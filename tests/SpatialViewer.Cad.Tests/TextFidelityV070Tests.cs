using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TextFidelityV070Tests
{
    [Fact]
    public void CadTextNormalizerDecodesCommonAutocadSymbolsAndLineBreaks()
    {
        var normalized = CadTextNormalizer.Normalize("A%%d B%%p C%%c\\P\\U+4E2D\\~X%%uU%%u");
        Assert.Equal("A° B± C⌀\n中 XU", normalized);
    }

    [Fact]
    public void ShxResolverRetainsStrokeFontKindAndChoosesScriptAwareFallback()
    {
        var latin = CadFontResolver.Resolve("simplex.shx", "ROOM");
        var chinese = CadFontResolver.Resolve("hztxt.shx", "房间");
        Assert.Equal(CadFontKind.Shx, latin.Kind);
        Assert.Equal("Segoe UI", latin.Family);
        Assert.True(latin.UsesFallback);
        Assert.Equal("SimSun", chinese.Family);
        Assert.True(chinese.UsesFallback);
    }

    [Fact]
    public void CenterMiddleTextUsesAlignmentPointWidthFactorAndShxMetadata()
    {
        var text = new CadTextEntity("700", new Point2D(10, 20), "ROOM", 10)
        {
            Presentation = new CadTextPresentation(
                FontFileName: "simplex.shx",
                WidthFactor: .8,
                ObliqueAngleRadians: .1,
                HorizontalAlignment: "Center",
                VerticalAlignment: "Middle",
                AlignmentPoint: new Point2D(100, 50))
        };
        var document = Document(text);
        var item = Assert.Single(document.Scene.GetItems());
        var geometry = Assert.IsType<TextGeometry>(item.Geometry);
        Assert.Equal(new Point2D(100, 50), geometry.Origin);
        Assert.Equal(TextHorizontalAlignment2D.Center, geometry.HorizontalAlignment);
        Assert.Equal(TextVerticalAlignment2D.Middle, geometry.VerticalAlignment);
        Assert.Equal(.8, geometry.WidthFactor, 8);
        Assert.Equal("Segoe UI", geometry.FontFamily);
        Assert.Equal("Shx", item.Metadata["FontKind"]);
        Assert.Equal(bool.TrueString, item.Metadata["FontFallbackApplied"]);
        Assert.True(geometry.GetBounds().MinX < geometry.Origin.X);
        Assert.True(geometry.GetBounds().MaxX > geometry.Origin.X);
    }

    [Fact]
    public void MTextAttachmentAndLayoutWidthReachGenericTextGeometry()
    {
        var text = new CadTextEntity("701", new Point2D(30, 40), "第一行\n第二行", 5, IsMText: true)
        {
            Presentation = new CadTextPresentation(
                FontFileName: "simhei.ttf",
                AttachmentPoint: "BottomRight",
                LayoutWidth: 42,
                LineSpacingFactor: 1.5,
                RawText: "{\\fSimHei;第一行}\\P第二行")
        };
        var geometry = Assert.IsType<TextGeometry>(Assert.Single(Document(text).Scene.GetItems()).Geometry);
        Assert.Equal(TextHorizontalAlignment2D.Right, geometry.HorizontalAlignment);
        Assert.Equal(TextVerticalAlignment2D.Bottom, geometry.VerticalAlignment);
        Assert.Equal(42, geometry.LayoutWidth, 8);
        Assert.Equal(1.5, geometry.LineSpacingFactor, 8);
        Assert.True(geometry.IsMultiline);
        Assert.Equal("SimHei", geometry.FontFamily);
        Assert.Equal(42, geometry.GetBounds().Width, 8);
    }

    private static CadDocument Document(CadEntity entity) => new(
        "text.dxf",
        "DXF",
        "AC1032",
        CadUnits.Unitless,
        new[] { new CadLayer("0", CadColor.FromAci(7)) },
        Array.Empty<CadBlockDefinition>(),
        new[] { entity });
}
