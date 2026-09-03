using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class LayoutViewportOverlayV0124Tests
{
    [Fact]
    public void SyntheticViewportBoundaryDoesNotRenderOrPolluteVisibleExtents()
    {
        var document = new CadDocument(
            "sheet.dwg",
            "DWG",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { new CadLineEntity("MODEL", new Point2D(0, 0), new Point2D(100, 0)) });
        var layout = new CadLayoutDefinition(
            "Layout1",
            1,
            true,
            new Size2D(420, 297),
            new BoundingBox2D(0, 0, 420, 297),
            new BoundingBox2D(0, 0, 420, 297),
            new CadEntity[] { new CadLineEntity("PAPER", new Point2D(10, 10), new Point2D(20, 10)) },
            new[]
            {
                new CadViewportDefinition(
                    "VP-HUGE",
                    new Point2D(500000, 500000),
                    new Size2D(1000000, 1000000),
                    Point2D.Origin,
                    Point2D.Origin,
                    100)
            });

        var scene = CadLayoutSceneTranslator.Translate(document, layout);
        var visible = scene.GetItems().ToArray();
        var all = scene.GetItems(false).ToArray();

        Assert.DoesNotContain(visible, item => item.Metadata.TryGetValue("Space", out var space) && space == "ViewportBoundary");
        var overlay = Assert.Single(all, item => item.Metadata.TryGetValue("Space", out var space) && space == "ViewportBoundary");
        Assert.Equal("True", overlay.Metadata["SyntheticViewportOverlay"]);
        Assert.Equal("True", overlay.Metadata["ExcludedFromVisibleExtents"]);
        Assert.False(overlay.Layer.IsVisible);

        var visibleBounds = scene.GetBounds();
        Assert.True(visibleBounds.MaxX < 1000);
        Assert.True(visibleBounds.MaxY < 1000);
    }
}
