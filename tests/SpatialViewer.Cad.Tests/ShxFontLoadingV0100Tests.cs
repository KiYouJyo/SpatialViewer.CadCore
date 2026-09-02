using System.Text;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ShxFontLoadingV0100Tests
{
    [Fact]
    public async Task DrawingDirectoryShxIsLoadedIntoVectorText()
    {
        var root = TemporaryDirectory();
        try
        {
            var dxf = Path.Combine(root, "drawing.dxf");
            var shx = Path.Combine(root, "fixture.shx");
            await File.WriteAllBytesAsync(shx, BuildShapeFont());
            WriteTextDxf(dxf, "fixture.shx");

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dxf));
            var document = Assert.IsType<CadDocument>(result.Document);
            var text = Assert.Single(document.ModelSpace.OfType<CadTextEntity>());

            Assert.True(result.IsSuccess);
            Assert.NotNull(text.Presentation.VectorFont);
            Assert.Equal("1", document.Metadata["ShxLoadedFontCount"]);
            Assert.All(document.Scene.GetItems().Where(item => item.Id == text.ObjectId), item => Assert.IsType<PolylineGeometry>(item.Geometry));
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("CAD_SHX_FONT_", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task HostProvidedSupportDirectoryIsSearchedAfterDrawingDirectory()
    {
        var root = TemporaryDirectory();
        var support = TemporaryDirectory();
        try
        {
            var dxf = Path.Combine(root, "drawing.dxf");
            await File.WriteAllBytesAsync(Path.Combine(support, "fixture.shx"), BuildShapeFont());
            WriteTextDxf(dxf, "fixture.shx");
            var options = new ImportOptions(Metadata: new Dictionary<string, string>
            {
                [CadFontImportMetadata.ShxSearchPaths] = support
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dxf, options));
            var document = Assert.IsType<CadDocument>(result.Document);
            var text = Assert.Single(document.ModelSpace.OfType<CadTextEntity>());

            Assert.True(result.IsSuccess);
            Assert.NotNull(text.Presentation.VectorFont);
            Assert.Equal("2", document.Metadata["ShxSearchDirectoryCount"]);
            Assert.Equal("1", document.Metadata["ShxLoadedFontCount"]);
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(support, true);
        }
    }

    [Fact]
    public async Task StaleAbsoluteShxReferenceRelocatesByBasenameIntoSupportDirectory()
    {
        var root = TemporaryDirectory();
        var support = TemporaryDirectory();
        try
        {
            var dxf = Path.Combine(root, "drawing.dxf");
            var stale = Path.Combine(root, "old-machine-support", "fixture.shx");
            await File.WriteAllBytesAsync(Path.Combine(support, "fixture.shx"), BuildShapeFont());
            WriteTextDxf(dxf, stale);
            var options = new ImportOptions(Metadata: new Dictionary<string, string>
            {
                [CadFontImportMetadata.ShxSearchPaths] = support
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dxf, options));
            var document = Assert.IsType<CadDocument>(result.Document);
            var text = Assert.Single(document.ModelSpace.OfType<CadTextEntity>());

            Assert.True(result.IsSuccess);
            Assert.NotNull(text.Presentation.VectorFont);
            Assert.Equal("1", document.Metadata["ShxLoadedFontCount"]);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_SHX_FONT_NOT_FOUND");
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(support, true);
        }
    }

    [Fact]
    public async Task MissingShxFallsBackWithoutFailingImportAndReportsOnce()
    {
        var root = TemporaryDirectory();
        try
        {
            var dxf = Path.Combine(root, "drawing.dxf");
            WriteTextDxf(dxf, "missing.shx", 2);

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dxf));
            var document = Assert.IsType<CadDocument>(result.Document);

            Assert.True(result.IsSuccess);
            Assert.All(document.ModelSpace.OfType<CadTextEntity>(), text => Assert.Null(text.Presentation.VectorFont));
            Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_SHX_FONT_NOT_FOUND");
            Assert.Equal("1", document.Metadata["ShxRequestedFontCount"]);
            Assert.Equal("0", document.Metadata["ShxLoadedFontCount"]);
            Assert.Contains(document.Scene.GetItems(), item => item.Geometry is TextGeometry && item.Metadata["FontFallbackApplied"] == bool.TrueString);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task InvalidShxFallsBackWithoutFailingImport()
    {
        var root = TemporaryDirectory();
        try
        {
            var dxf = Path.Combine(root, "drawing.dxf");
            await File.WriteAllTextAsync(Path.Combine(root, "broken.shx"), "not-a-compiled-shx", Encoding.ASCII);
            WriteTextDxf(dxf, "broken.shx");

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dxf));
            var document = Assert.IsType<CadDocument>(result.Document);
            var text = Assert.Single(document.ModelSpace.OfType<CadTextEntity>());

            Assert.True(result.IsSuccess);
            Assert.Null(text.Presentation.VectorFont);
            Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_SHX_FONT_INVALID");
            Assert.Contains(document.Scene.GetItems(), item => item.Id == text.ObjectId && item.Geometry is TextGeometry);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void WriteTextDxf(string path, string fontFileName, int entityCount = 1)
    {
        var document = new global::ACadSharp.CadDocument();
        document.CreateDefaults();
        var style = new TextStyle("CadCoreShxLoading") { Filename = fontFileName };
        document.TextStyles.Add(style);
        for (var index = 0; index < entityCount; index++)
        {
            document.Entities.Add(new TextEntity
            {
                Value = "AA",
                InsertPoint = new XYZ(index * 30, 0, 0),
                Height = 10,
                Style = style
            });
        }
        using var writer = new DxfWriter(path, document, false);
        writer.Write();
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

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-shx-v0100-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
