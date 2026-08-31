using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class EntityCoverageV050ReaderTests
{
    [Fact]
    public async Task GeneratedDxfImportsDimensionLeaderAndMultiLeaderThroughRealReader()
    {
        var dxf = Path.Combine(Path.GetTempPath(), $"spatial-viewer-v050-{Guid.NewGuid():N}.dxf");
        var png = Path.Combine(Path.GetTempPath(), $"spatial-viewer-v050-{Guid.NewGuid():N}.png");
        try
        {
            ACadSharpFixtureTranscoder.WriteAnnotationDxf(dxf);
            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dxf));
            var document = Assert.IsType<CadDocument>(result.Document);

            Assert.True(result.IsSuccess);
            var dimension = Assert.Single(document.ModelSpace.OfType<CadDimensionEntity>());
            Assert.Equal(CadDimensionKind.Linear, dimension.Kind);
            Assert.Equal(100, dimension.Measurement, 6);
            Assert.True(dimension.ReferencePoints.ContainsKey("FirstPoint"));
            Assert.True(dimension.ReferencePoints.ContainsKey("SecondPoint"));
            Assert.False(string.IsNullOrWhiteSpace(dimension.Text));

            var leader = Assert.Single(document.ModelSpace.OfType<CadLeaderEntity>());
            Assert.Equal(3, leader.Vertices.Count);
            Assert.True(leader.ArrowHeadEnabled);
            Assert.False(leader.IsSpline);

            var multiLeader = Assert.Single(document.ModelSpace.OfType<CadMultiLeaderEntity>());
            Assert.NotEmpty(multiLeader.Paths);
            Assert.Equal("CadCore MLeader", multiLeader.Text);
            Assert.True(multiLeader.EnableDogleg);
            Assert.Contains(multiLeader.Paths, path => path.Points.Count >= 3);

            Assert.Contains(document.Scene.GetItems(), item => item.Metadata.TryGetValue("DimensionSemantic", out var value) && value == "True");
            Assert.Contains(document.Scene.GetItems(), item => item.Metadata.TryGetValue("LeaderSemantic", out var value) && value == "True");
            Assert.Contains(document.Scene.GetItems(), item => item.Metadata.TryGetValue("MultiLeaderSemantic", out var value) && value == "True");
            Assert.NotNull(HitTesting.HitTest(document.Scene, new(50, 20), .2));
            Assert.NotNull(HitTesting.HitTest(document.Scene, new(20, 50), .2));
            Assert.NotNull(HitTesting.HitTest(document.Scene, new(65, 70), .2));

            GoldenScenePngRenderer.Render(document.Scene, png);
            var bytes = await File.ReadAllBytesAsync(png);
            Assert.True(bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
            Assert.True(bytes.Length > 100);
        }
        finally
        {
            if (File.Exists(dxf)) File.Delete(dxf);
            if (File.Exists(png)) File.Delete(png);
        }
    }

    [Fact]
    public async Task GeneratedAnnotationDwgUsesTheSameSemanticPipeline()
    {
        var dxf = Path.Combine(Path.GetTempPath(), $"spatial-viewer-v050-{Guid.NewGuid():N}.dxf");
        var dwg = Path.Combine(Path.GetTempPath(), $"spatial-viewer-v050-{Guid.NewGuid():N}.dwg");
        try
        {
            ACadSharpFixtureTranscoder.WriteAnnotationDxf(dxf);
            ACadSharpFixtureTranscoder.WriteDwgFromDxf(dxf, dwg);
            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(dwg));
            var document = Assert.IsType<CadDocument>(result.Document);

            Assert.True(result.IsSuccess);
            Assert.Single(document.ModelSpace.OfType<CadDimensionEntity>());
            Assert.Single(document.ModelSpace.OfType<CadLeaderEntity>());
            var multiLeaders = document.ModelSpace.OfType<CadMultiLeaderEntity>().ToArray();
            var diagnostics = string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
            var entities = string.Join(", ", document.ModelSpace.Select(entity => entity.GetType().Name));
            Assert.True(multiLeaders.Length == 1, $"Expected one MLEADER after ACadSharp DXF->DWG self-roundtrip. Entities: {entities}. Diagnostics: {diagnostics}");
            Assert.Contains(document.Scene.GetItems(), item => item.Geometry is TextGeometry text && text.Text.Contains("MLeader", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(dxf)) File.Delete(dxf);
            if (File.Exists(dwg)) File.Delete(dwg);
        }
    }
}
