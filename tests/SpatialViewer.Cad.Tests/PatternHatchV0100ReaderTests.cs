using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class PatternHatchV0100ReaderTests
{
    [Fact]
    public async Task GeneratedDxfPreservesCustomPatternLineFamiliesThroughReaderAndScene()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatial-viewer-pattern-v0100-{Guid.NewGuid():N}.dxf");
        try
        {
            WriteFixture(path);
            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            var document = Assert.IsType<CadDocument>(result.Document);
            var hatch = Assert.Single(document.ModelSpace.OfType<CadHatchEntity>());

            Assert.False(hatch.IsSolid);
            Assert.Equal("CadCorePattern", hatch.PatternName);
            Assert.Equal(Math.PI / 4, hatch.PatternAngleRadians, 10);
            var line = Assert.Single(hatch.PatternLines);
            Assert.Equal(Math.PI / 3, line.AngleRadians, 10);
            Assert.Equal(new Point2D(0, 1), line.BasePoint);
            Assert.Equal(new Vector2D(0, 2), line.Offset);
            Assert.Equal(new[] { 2d, -1d }, line.DashLengths);

            var items = document.Scene.GetItems().Where(item => item.Id == hatch.ObjectId).ToArray();
            Assert.Contains(items, item => item.Geometry is LineGeometry);
            Assert.Contains(items, item => item.Metadata.TryGetValue("HatchPatternDefinitionLineCount", out var count) && count == "1");
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_UNSUPPORTED_ENTITY" && diagnostic.Message.Contains("HATCH", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void WriteFixture(string path)
    {
        var document = new global::ACadSharp.CadDocument();
        document.CreateDefaults();
        var hatch = new Hatch
        {
            IsSolid = false,
            Pattern = new HatchPattern("CadCorePattern"),
            PatternAngle = Math.PI / 4,
            PatternScale = 1
        };
        hatch.Pattern.Lines.Add(new HatchPattern.Line
        {
            Angle = Math.PI / 3,
            BasePoint = new XY(0, 1),
            Offset = new XY(0, 2),
            DashLengths = new List<double> { 2, -1 }
        });

        var boundary = new Hatch.BoundaryPath();
        boundary.Edges.Add(new Hatch.BoundaryPath.Line { Start = new XY(0, 0), End = new XY(10, 0) });
        boundary.Edges.Add(new Hatch.BoundaryPath.Line { Start = new XY(10, 0), End = new XY(10, 10) });
        boundary.Edges.Add(new Hatch.BoundaryPath.Line { Start = new XY(10, 10), End = new XY(0, 10) });
        boundary.Edges.Add(new Hatch.BoundaryPath.Line { Start = new XY(0, 10), End = new XY(0, 0) });
        hatch.Paths.Add(boundary);
        hatch.SeedPoints.Add(new XY(1, 1));
        document.Entities.Add(hatch);

        using var writer = new DxfWriter(path, document, false);
        writer.Write();
    }
}
