using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;
using SpatialViewer.Rendering;

namespace SpatialViewer.Cad.Tests;

public sealed class EntityCoverageV060Tests
{
    [Fact]
    public void LayoutSceneProjectsModelContentAndClipsViewport()
    {
        var layers = new[]
        {
            new CadLayer("0", CadColor.FromAci(7)),
            new CadLayer("FROZEN", CadColor.FromAci(1))
        };
        var model = new CadEntity[]
        {
            new CadLineEntity("MODEL", new Point2D(-100, 0), new Point2D(100, 0)),
            new CadLineEntity("FROZEN-LINE", new Point2D(-20, 10), new Point2D(20, 10), LayerName: "FROZEN")
        };
        var viewport = new CadViewportDefinition(
            "VP2",
            new Point2D(100, 50),
            new Size2D(100, 50),
            Point2D.Origin,
            Point2D.Origin,
            50,
            FrozenLayers: new[] { "FROZEN" });
        var sheet = new CadLayoutDefinition(
            "Sheet1",
            1,
            true,
            new Size2D(200, 100),
            new BoundingBox2D(0, 0, 200, 100),
            new BoundingBox2D(0, 0, 200, 100),
            new CadEntity[] { new CadLineEntity("PAPER", new Point2D(10, 10), new Point2D(40, 10)) },
            new[] { viewport });
        var modelLayout = new CadLayoutDefinition("Model", 0, false, Size2D.Empty, BoundingBox2D.Empty, BoundingBox2D.Empty, Array.Empty<CadEntity>(), Array.Empty<CadViewportDefinition>());
        var document = new CadDocument("layout.dxf", "DXF", "AC1032", CadUnits.Millimetres, layers, Array.Empty<CadBlockDefinition>(), model, layouts: new[] { modelLayout, sheet });

        Assert.Same(document.Scene, document.GetLayoutScene("model"));
        var scene = document.GetLayoutScene("sheet1");
        var projected = Assert.Single(scene.GetItems().Where(item => item.Metadata.TryGetValue("Space", out var space) && space == "ModelThroughViewport"));
        Assert.Equal("VP2", projected.Metadata["ViewportHandle"]);
        Assert.Equal(viewport.PaperBounds, projected.ClipBounds);
        Assert.Equal(viewport.PaperBounds, projected.Bounds);
        Assert.DoesNotContain(scene.GetItems(), item => item.Layer.Name == "FROZEN" && item.Metadata.TryGetValue("Space", out var space) && space == "ModelThroughViewport");
        Assert.Contains(scene.GetItems(), item => item.Metadata.TryGetValue("Space", out var space) && space == "Paper");
        Assert.Contains(scene.GetItems(), item => item.Metadata.TryGetValue("Space", out var space) && space == "ViewportBoundary");

        var inside = HitTesting.HitTest(scene, new Point2D(100, 50), .5);
        Assert.NotNull(inside);
        Assert.Equal(projected.Id, inside.Value.Id);
        Assert.Null(HitTesting.HitTest(scene, new Point2D(170, 50), .5));

        var frame = RenderPreparation.Prepare(scene, new Camera2D(new Point2D(100, 50)));
        var command = Assert.Single(frame.Commands.Where(candidate => candidate.Metadata is not null && candidate.Metadata.TryGetValue("ViewportHandle", out var handle) && handle == "VP2" && candidate.Metadata.TryGetValue("Space", out var space) && space == "ModelThroughViewport"));
        Assert.Equal(viewport.PaperBounds, command.ClipBounds);
    }

    [Fact]
    public async Task ACadSharpWriterLayoutRoundTripsThroughReaderAndScene()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-layout-{Guid.NewGuid():N}.dxf");
        try
        {
            ACadSharpFixtureTranscoder.WriteLayoutDxf(path);
            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            Assert.True(result.IsSuccess);
            Assert.True(document.Layouts.Count >= 2);
            var layout = Assert.Single(document.Layouts.Where(candidate => candidate.Name == "SheetV060"));
            Assert.True(layout.IsPaperSpace);
            Assert.Equal(2, layout.TabOrder);
            Assert.Equal(200, layout.PaperSize.Width, 6);
            Assert.Equal(100, layout.PaperSize.Height, 6);
            Assert.Contains(layout.Entities, entity => entity is CadLineEntity);
            var viewport = Assert.Single(layout.Viewports.Where(candidate => !candidate.RepresentsPaper && candidate.IsOn));
            Assert.Equal(100, viewport.PaperSize.Width, 6);
            Assert.Equal(50, viewport.PaperSize.Height, 6);
            Assert.Equal(50, viewport.ViewHeight, 6);
            Assert.Equal(2, viewport.ScaleFactor, 6);

            var scene = document.GetLayoutScene("SheetV060");
            Assert.Contains(scene.GetItems(), item => item.Metadata.TryGetValue("Space", out var space) && space == "Paper");
            Assert.Contains(scene.GetItems(), item => item.Metadata.TryGetValue("Space", out var space) && space == "ModelThroughViewport" && item.ClipBounds is not null);
            Assert.Equal(document.Layouts.Count.ToString(), document.Metadata["LayoutCount"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
