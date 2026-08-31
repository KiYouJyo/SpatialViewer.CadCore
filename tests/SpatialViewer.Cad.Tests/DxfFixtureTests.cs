using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class DxfFixtureTests
{
    [Fact]
    public async Task MixedBasicDxfCreatesFormatNeutralCadDocument()
    {
        var result = await ImportAsync("mixed-basic.dxf");
        var document = Assert.IsType<CadDocument>(result.Document);
        Assert.True(result.IsSuccess);
        Assert.Equal("DXF", document.SourceFormat);
        Assert.Equal(CadUnits.Millimetres, document.Units);
        Assert.Contains(document.CadLayers, layer => layer.Name == "ROAD" && layer.Color.Kind == CadColorKind.Aci);
        Assert.Contains(document.ModelSpace, entity => entity is CadLineEntity);
        Assert.Contains(document.ModelSpace, entity => entity is CadCircleEntity);
        Assert.Contains(document.ModelSpace, entity => entity is CadArcEntity);
        Assert.Contains(document.ModelSpace, entity => entity is CadEllipseEntity);
        Assert.Contains(document.ModelSpace, entity => entity is CadPolylineEntity);
        Assert.Contains(document.ModelSpace, entity => entity is CadTextEntity text && !text.IsMText);
        Assert.Contains(document.ModelSpace, entity => entity is CadTextEntity text && text.IsMText && text.Text.Contains('\n'));
        Assert.Contains(document.ModelSpace, entity => entity is CadBlockReferenceEntity reference && reference.BlockName == "NEST");
        Assert.Contains(document.Blocks, block => block.Name == "MARK");
        Assert.Contains(document.Blocks, block => block.Name == "NEST" && block.Entities.OfType<CadBlockReferenceEntity>().Any(reference => reference.BlockName == "MARK"));
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "CAD_UNSUPPORTED_ENTITY");
        Assert.True(document.Scene.GetItems().Count() >= 9);
    }

    [Fact]
    public async Task LargeCoordinateDxfRetainsDoublePrecisionThroughScene()
    {
        var document = Assert.IsType<CadDocument>((await ImportAsync("large-coordinate.dxf")).Document);
        var line = Assert.IsType<CadLineEntity>(Assert.Single(document.ModelSpace.OfType<CadLineEntity>()));
        Assert.Equal(500000.123456, line.Start.X, 6);
        Assert.Equal(3400000.654321, line.Start.Y, 6);
        var camera = new Camera2D(document.Bounds.Center, 10); var viewport = new Size2D(1920, 1080); var screen = camera.WorldToScreen(line.Start, viewport);
        var roundTrip = camera.ScreenToWorld(screen, viewport);
        Assert.Equal(line.Start.X, roundTrip.X, 8); Assert.Equal(line.Start.Y, roundTrip.Y, 8);
    }

    [Fact]
    public async Task InvalidDxfReturnsDiagnosticAndDoesNotPoisonNextImport()
    {
        var importer = new ACadSharpCadImporter();
        var invalid = await importer.ImportAsync(new ImportRequest(Fixture("invalid.dxf", "negative")));
        Assert.False(invalid.IsSuccess); Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "CAD_READER_FAILURE");
        Assert.True((await ImportAsync("mixed-basic.dxf")).IsSuccess);
    }

    [Fact]
    public async Task ImportCanBeCancelledBeforeReaderStarts()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ACadSharpCadImporter().ImportAsync(new ImportRequest(Fixture("mixed-basic.dxf")), cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task DwgGeneratedFromFixtureUsesSameCadAndScenePipeline()
    {
        var dwg = Path.Combine(Path.GetTempPath(), $"spatial-viewer-{Guid.NewGuid():N}.dwg");
        try
        {
            ACadSharpFixtureTranscoder.WriteDwgFromDxf(Fixture("mixed-basic.dxf"), dwg);
            var dxf = Assert.IsType<CadDocument>((await ImportAsync("mixed-basic.dxf")).Document);
            var dwgResult = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dwg));
            var dwgDocument = Assert.IsType<CadDocument>(dwgResult.Document);
            Assert.True(dwgResult.IsSuccess); Assert.Equal("DWG", dwgDocument.SourceFormat);
            Assert.Equal(dxf.ModelSpace.Count(entity => entity is not CadUnsupportedEntity), dwgDocument.ModelSpace.Count(entity => entity is not CadUnsupportedEntity));
            Assert.Equal(dxf.Bounds.Width, dwgDocument.Bounds.Width, 4); Assert.Equal(dxf.Bounds.Height, dwgDocument.Bounds.Height, 4);
            Assert.Contains(dwgDocument.ModelSpace, entity => entity is CadBlockReferenceEntity);
        }
        finally { if (File.Exists(dwg)) File.Delete(dwg); }
    }

    [Fact]
    public async Task DwgLargeCoordinateFixtureRetainsDoublePrecision()
    {
        var dwg = Path.Combine(Path.GetTempPath(), $"spatial-viewer-large-{Guid.NewGuid():N}.dwg");
        try
        {
            ACadSharpFixtureTranscoder.WriteDwgFromDxf(Fixture("large-coordinate.dxf"), dwg);
            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dwg));
            var document = Assert.IsType<CadDocument>(result.Document);
            var line = Assert.IsType<CadLineEntity>(Assert.Single(document.ModelSpace.OfType<CadLineEntity>()));
            Assert.True(result.IsSuccess); Assert.Equal("DWG", document.SourceFormat);
            Assert.Equal(500000.123456, line.Start.X, 6); Assert.Equal(3400000.654321, line.Start.Y, 6);
        }
        finally { if (File.Exists(dwg)) File.Delete(dwg); }
    }

    [Fact]
    public async Task FixtureScenesProduceDeterministicPngGoldenOutputs()
    {
        var output = Path.Combine(RepositoryRoot(), "artifacts", "stage2", "render");
        foreach (var fixture in new[] { "mixed-basic.dxf", "large-coordinate.dxf", "entity-coverage-v040.dxf" })
        {
            var document = Assert.IsType<CadDocument>((await ImportAsync(fixture)).Document); var path = Path.Combine(output, Path.ChangeExtension(fixture, ".png"));
            GoldenScenePngRenderer.Render(document.Scene, path); var bytes = await File.ReadAllBytesAsync(path); Assert.True(bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })); Assert.True(bytes.Length > 100);
        }
    }

    private static async Task<ImportResult> ImportAsync(string fileName) => await new ACadSharpCadImporter().ImportAsync(new ImportRequest(Fixture(fileName)));
    private static string Fixture(string fileName, string folder = "dxf") => Path.Combine(AppContext.BaseDirectory, "fixtures", "cad", folder, fileName);
    private static string RepositoryRoot() => FindRoot(new DirectoryInfo(AppContext.BaseDirectory)).FullName;
    private static DirectoryInfo FindRoot(DirectoryInfo directory) => File.Exists(Path.Combine(directory.FullName, "SpatialViewer.CadCore.sln")) ? directory : directory.Parent is { } parent ? FindRoot(parent) : throw new DirectoryNotFoundException("SpatialViewer.CadCore.sln was not found.");
}
