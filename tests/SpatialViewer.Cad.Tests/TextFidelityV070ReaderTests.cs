using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TextFidelityV070ReaderTests
{
    [Fact]
    public async Task GeneratedDxfPreservesTextStylesAlignmentAndMTextLayoutThroughRealReader()
    {
        var dxf = Path.Combine(Path.GetTempPath(), $"spatial-viewer-v070-{Guid.NewGuid():N}.dxf");
        try
        {
            ACadSharpFixtureTranscoder.WriteTextFidelityDxf(dxf);
            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dxf));
            var document = Assert.IsType<CadDocument>(result.Document);
            Assert.True(result.IsSuccess);

            var text = Assert.Single(document.ModelSpace.OfType<CadTextEntity>(), entity => !entity.IsMText);
            Assert.Equal("ROOM°", text.Text);
            Assert.Equal("CadCoreSHX", text.Presentation.StyleName);
            Assert.Equal("simplex.shx", text.Presentation.FontFileName, ignoreCase: true);
            Assert.Equal("Center", text.Presentation.HorizontalAlignment);
            Assert.Equal("Middle", text.Presentation.VerticalAlignment);
            Assert.Equal(new Point2D(100, 50), text.Presentation.AlignmentPoint);
            Assert.Equal(.72, text.Presentation.WidthFactor, 6);
            Assert.Equal(.2, text.Presentation.ObliqueAngleRadians, 6);
            Assert.True(text.Presentation.IsBackward);

            var mtext = Assert.Single(document.ModelSpace.OfType<CadTextEntity>(), entity => entity.IsMText);
            Assert.Equal("CadCoreTTF", mtext.Presentation.StyleName);
            Assert.Equal("simhei.ttf", mtext.Presentation.FontFileName, ignoreCase: true);
            Assert.Equal("BottomRight", mtext.Presentation.AttachmentPoint);
            Assert.Equal(42, mtext.Presentation.LayoutWidth, 6);
            Assert.Equal(1.5, mtext.Presentation.LineSpacingFactor, 6);
            Assert.Contains("第一行", mtext.Text, StringComparison.Ordinal);
            Assert.Contains("第二行", mtext.Text, StringComparison.Ordinal);

            var textItem = Assert.Single(document.Scene.GetItems(), item => item.Metadata.TryGetValue("TextStyle", out var style) && style == "CadCoreSHX");
            var textGeometry = Assert.IsType<TextGeometry>(textItem.Geometry);
            Assert.Equal(new Point2D(100, 50), textGeometry.Origin);
            Assert.Equal(TextHorizontalAlignment2D.Center, textGeometry.HorizontalAlignment);
            Assert.Equal(TextVerticalAlignment2D.Middle, textGeometry.VerticalAlignment);
            Assert.Equal("Shx", textItem.Metadata["FontKind"]);
            Assert.Equal(bool.TrueString, textItem.Metadata["FontFallbackApplied"]);

            var mtextItem = Assert.Single(document.Scene.GetItems(), item => item.Metadata.TryGetValue("TextStyle", out var style) && style == "CadCoreTTF");
            var mtextGeometry = Assert.IsType<TextGeometry>(mtextItem.Geometry);
            Assert.Equal(TextHorizontalAlignment2D.Right, mtextGeometry.HorizontalAlignment);
            Assert.Equal(TextVerticalAlignment2D.Bottom, mtextGeometry.VerticalAlignment);
            Assert.Equal(42, mtextGeometry.LayoutWidth, 6);
            Assert.Equal("SimHei", mtextGeometry.FontFamily);
        }
        finally
        {
            if (File.Exists(dxf)) File.Delete(dxf);
        }
    }
}
