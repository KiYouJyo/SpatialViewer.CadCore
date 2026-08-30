using SpatialViewer.Core;
using SpatialViewer.Rendering;

namespace SpatialViewer.Rendering.Tests;

public sealed class AdaptiveArcTessellatorTests
{
    [Fact]
    public void TessellationAddsDetailAsScreenScaleIncreases()
    {
        var arc = new ArcGeometry(Point2D.Origin, 10, 0, Math.PI / 2);
        var normal = AdaptiveArcTessellator.Tessellate(arc, point => point);
        var zoomed = AdaptiveArcTessellator.Tessellate(arc, point => new Point2D(point.X * 100, point.Y * 100));
        Assert.True(zoomed.Count > normal.Count);
    }

    [Fact]
    public void TessellationRespectsPixelErrorForCircularProjection()
    {
        const double radius = 800;
        const double tolerance = .25;
        var arc = new ArcGeometry(Point2D.Origin, radius, 0, Math.PI * 1.75);
        var points = AdaptiveArcTessellator.Tessellate(arc, point => point, tolerance);
        Assert.True(points.Count > 16);
        for (var index = 1; index < points.Count; index++)
        {
            var chord = points[index - 1].DistanceTo(points[index]);
            var halfChord = Math.Min(radius, chord / 2);
            var sagitta = radius - Math.Sqrt(Math.Max(0, (radius * radius) - (halfChord * halfChord)));
            Assert.True(sagitta <= tolerance + 1e-6, $"Segment {index} sagitta {sagitta} exceeded {tolerance} pixels.");
        }
    }

    [Fact]
    public void FullSweepClosesWithoutFlatteningGeometrySemantics()
    {
        var arc = new ArcGeometry(new Point2D(10, -20), 50, .4, Math.PI * 2);
        var points = AdaptiveArcTessellator.Tessellate(arc, point => point);
        Assert.True(points.Count > 16);
        Assert.InRange(points[0].DistanceTo(points[^1]), 0, 1e-9);
    }

    [Fact]
    public void NonUniformTransformedArcAdaptsInScreenSpace()
    {
        var arc = new ArcGeometry(new Point2D(4, 7), 30, -.2, Math.PI * 1.2);
        Point2D Map(Point2D point) => new((point.X * 40) + (point.Y * 7) + 300, (point.X * 3) + (point.Y * 12) - 90);
        var points = AdaptiveArcTessellator.Tessellate(arc, Map);
        Assert.True(points.Count > 16);
        Assert.All(points, point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Fact]
    public void LargeCoordinatesCanRemainCameraRelativeUntilScreenMapping()
    {
        var center = new Point2D(500_000_000, 3_400_000_000);
        var arc = new ArcGeometry(center, 10_000, 0, Math.PI / 2);
        var points = AdaptiveArcTessellator.Tessellate(arc, point => new Point2D((point.X - center.X) * .1, (point.Y - center.Y) * .1));
        Assert.All(points, point => { Assert.InRange(point.X, -1, 1001); Assert.InRange(point.Y, -1, 1001); });
    }

    [Fact]
    public void TighterToleranceProducesAtLeastAsMuchDetail()
    {
        var arc = new ArcGeometry(Point2D.Origin, 500, 0, Math.PI);
        var loose = AdaptiveArcTessellator.Tessellate(arc, point => point, 2);
        var tight = AdaptiveArcTessellator.Tessellate(arc, point => point, .1);
        Assert.True(tight.Count > loose.Count);
    }
}

public sealed class RenderColorPolicyTests
{
    private static readonly IReadOnlyDictionary<string, string> Adaptive = new Dictionary<string, string> { [RenderColorPolicy.BackgroundAdaptiveStrokeKey] = bool.TrueString };

    [Fact]
    public void AdaptiveStrokeIsBlackOnLightCanvas() => Assert.Equal("#000000", RenderColorPolicy.ResolveStroke(new SceneStyle("#FFFFFF"), Adaptive, "#FAFAFA"));

    [Fact]
    public void AdaptiveStrokeIsWhiteOnDarkCanvas() => Assert.Equal("#FFFFFF", RenderColorPolicy.ResolveStroke(new SceneStyle("#FFFFFF"), Adaptive, "#202830"));

    [Fact]
    public void NonAdaptiveStrokeIsNeverRewritten() => Assert.Equal("#FF7F00", RenderColorPolicy.ResolveStroke(new SceneStyle("#FF7F00"), null, "#FAFAFA"));

    [Fact]
    public void InvalidCanvasColorKeepsSourceStroke() => Assert.Equal("#FFFFFF", RenderColorPolicy.ResolveStroke(new SceneStyle("#FFFFFF"), Adaptive, "not-a-color"));
}
