using System.Text;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class ShxVectorV0100Tests
{
    [Fact]
    public void ShapeFontParserReadsMetricsAndVectorAdvance()
    {
        var font = CadShxFont.Parse(BuildShapeFont(), "fixture.shx");

        Assert.Equal(CadShxFontType.Shapes, font.FontType);
        Assert.Equal("fixture.shx", font.FileName);
        Assert.Equal("TEST", font.Info);
        Assert.Equal(8, font.BaseUp);
        Assert.Equal(2, font.BaseDown);
        Assert.Equal(10, font.DesignHeight);
        Assert.True(font.CanLayoutUnicodeText);
        Assert.True(font.TryGetGlyph(new Rune('A'), out var glyph));
        Assert.Single(glyph.Strokes);
        Assert.Equal(new Point2D(0, 0), glyph.Strokes[0][0]);
        Assert.Equal(new Point2D(8, 0), glyph.Strokes[0][1]);
        Assert.Equal(new Point2D(10, 0), glyph.Advance);
        Assert.True(glyph.HasExplicitAdvance);
    }

    [Fact]
    public void ShapeFontLayoutPlacesRepeatedGlyphsByCompiledAdvance()
    {
        var font = CadShxFont.Parse(BuildShapeFont());
        var layout = font.LayoutText("AA", 10);

        Assert.True(layout.Complete);
        Assert.Equal(0, layout.MissingGlyphCount);
        Assert.Equal(2, layout.Strokes.Count);
        Assert.Equal(new Point2D(0, 0), layout.Strokes[0][0]);
        Assert.Equal(new Point2D(10, 0), layout.Strokes[1][0]);
        Assert.Equal(20, layout.AdvanceWidth, 8);
    }

    [Fact]
    public void SceneBuilderUsesResolvedShxVectorsInsteadOfFallbackText()
    {
        var font = CadShxFont.Parse(BuildShapeFont(), "fixture.shx");
        var text = new CadTextEntity("SHX1", new Point2D(100, 50), "AA", 10)
        {
            Presentation = new CadTextPresentation(FontFileName: "fixture.shx", VectorFont: font)
        };

        var items = Document(text).Scene.GetItems().ToArray();

        Assert.Equal(2, items.Length);
        Assert.All(items, item => Assert.IsType<PolylineGeometry>(item.Geometry));
        Assert.All(items, item => Assert.Equal(bool.FalseString, item.Metadata["FontFallbackApplied"]));
        Assert.All(items, item => Assert.Equal(bool.TrueString, item.Metadata["ShxVectorGlyphComplete"]));
        Assert.Equal(new Point2D(100, 50), ((PolylineGeometry)items[0].Geometry).Points[0]);
        Assert.Equal(new Point2D(110, 50), ((PolylineGeometry)items[1].Geometry).Points[0]);
    }

    [Fact]
    public void MissingShxGlyphFallsBackToGenericTextWithoutLosingDiagnostics()
    {
        var font = CadShxFont.Parse(BuildShapeFont(), "fixture.shx");
        var text = new CadTextEntity("SHX2", Point2D.Origin, "AZ", 10)
        {
            Presentation = new CadTextPresentation(FontFileName: "fixture.shx", VectorFont: font)
        };

        var item = Assert.Single(Document(text).Scene.GetItems());

        Assert.IsType<TextGeometry>(item.Geometry);
        Assert.Equal(bool.TrueString, item.Metadata["FontFallbackApplied"]);
        Assert.Equal(bool.FalseString, item.Metadata["ShxVectorGlyphComplete"]);
        Assert.Equal("1", item.Metadata["ShxMissingGlyphCount"]);
        Assert.Equal("MissingGlyph", item.Metadata["ShxFallbackReason"]);
    }

    private static byte[] BuildShapeFont()
    {
        var header = Encoding.ASCII.GetBytes("AutoCAD-86 shapes 1.0\r\n\u001A");
        var info = Encoding.ASCII.GetBytes("TEST").Concat(new byte[] { 0, 8, 2, 0 }).ToArray();
        var a = new byte[] { 0, 0x80, 2, 8, 2, 0, 0 };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(header);
        writer.Write(new byte[4]);
        writer.Write((short)2);
        writer.Write((ushort)0);
        writer.Write((ushort)info.Length);
        writer.Write((ushort)'A');
        writer.Write((ushort)a.Length);
        writer.Write(info);
        writer.Write(a);
        writer.Flush();
        return stream.ToArray();
    }

    private static CadDocument Document(CadEntity entity) => new(
        "shx-v0100.dxf",
        "DXF",
        "AC1032",
        CadUnits.Unitless,
        new[] { new CadLayer("0", CadColor.FromAci(7)) },
        Array.Empty<CadBlockDefinition>(),
        new[] { entity });
}
