using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TextFidelityV0100Tests
{
    [Fact]
    public void MTextParserFlattensCommonInlineFormattingWithoutLeakingControlCodes()
    {
        var parsed = CadMTextParser.Parse("{\\fArial|b0;ROOM}\\P\\C1;A\\S1#2; \\U+4E2D \\{X\\} \\LUNDER\\l");

        Assert.Equal("ROOM\nA1/2 中 {X} UNDER", parsed.PlainText);
        Assert.True(parsed.HasInlineFormatting);
        Assert.True(parsed.HasStackedText);
        Assert.True(parsed.HasFontOverrides);
        Assert.True(parsed.HasColorOverrides);
        Assert.True(parsed.HasDecorations);
    }

    [Fact]
    public void MTextParserRetainsLiteralEscapesAndTracksSizePresentationOverrides()
    {
        var parsed = CadMTextParser.Parse("A\\\\B \\H1.5x;H \\W0.8;W \\Q15;Q \\T2;T");

        Assert.Equal("A\\B H W Q T", parsed.PlainText);
        Assert.True(parsed.HasHeightOverrides);
        Assert.True(parsed.HasWidthOverrides);
        Assert.True(parsed.HasObliqueOverrides);
        Assert.True(parsed.HasTrackingOverrides);
    }

    [Fact]
    public void SceneBuilderUsesRawMTextForStackedFractionsAndRetainsFormattingDiagnostics()
    {
        var text = new CadTextEntity("A10", new Point2D(20, 30), "A 1/2", 5, IsMText: true)
        {
            Presentation = new CadTextPresentation(
                FontFileName: "simhei.ttf",
                AttachmentPoint: "TopLeft",
                RawText: "{\\fSimHei;A} \\S1^2;")
        };

        var item = Assert.Single(Document(text).Scene.GetItems());
        var geometry = Assert.IsType<TextGeometry>(item.Geometry);

        Assert.Equal("A 1/2", geometry.Text);
        Assert.Equal(bool.TrueString, item.Metadata["MTextInlineFormatting"]);
        Assert.Equal(bool.TrueString, item.Metadata["MTextStackedText"]);
        Assert.Equal(bool.TrueString, item.Metadata["MTextFontOverrides"]);
    }

    private static CadDocument Document(CadEntity entity) => new(
        "text-v0100.dxf",
        "DXF",
        "AC1032",
        CadUnits.Unitless,
        new[] { new CadLayer("0", CadColor.FromAci(7)) },
        Array.Empty<CadBlockDefinition>(),
        new[] { entity });
}
