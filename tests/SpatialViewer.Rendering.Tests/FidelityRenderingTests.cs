using SpatialViewer.Core;
using SpatialViewer.Rendering;

namespace SpatialViewer.Rendering.Tests;

public sealed class FidelityRenderingTests
{
    [Fact]
    public void EllipseTessellationRefinesAtHigherScreenScale()
    {
        var ellipse = new EllipseGeometry(Point2D.Origin, 20, 5);
        var low = AdaptiveEllipseTessellator.Tessellate(ellipse, point => point);
        var high = AdaptiveEllipseTessellator.Tessellate(ellipse, point => new Point2D(point.X * 100, point.Y * 100));
        Assert.True(high.Count > low.Count);
        Assert.True(high.Count > 32);
    }

    [Fact]
    public void EllipseTessellationHonorsFullRotatedNonUniformMapping()
    {
        var ellipse = new EllipseGeometry(Point2D.Origin, 10, 2);
        var transform = Transform2D.Scale(2, .5).Then(Transform2D.Rotation(Math.PI / 4));
        var points = AdaptiveEllipseTessellator.Tessellate(ellipse, transform.Apply);
        Assert.Contains(points, point => Math.Abs(point.X) > 10 && Math.Abs(point.Y) > 10);
    }

    [Fact]
    public void StrokePatternCombinesEntityGlobalAndScreenScale()
    {
        var metadata = new Dictionary<string, string>
        {
            [RenderStrokePattern.PatternKey] = "6;-4",
            [RenderStrokePattern.EntityScaleKey] = "1.5",
            [RenderStrokePattern.GlobalScaleKey] = "2"
        };
        var pattern = RenderStrokePattern.ResolvePixels(metadata, 2);
        Assert.Equal(new[] { 36d, -24d }, pattern);
    }

    [Fact]
    public void TextPlacementExtractsRotationAndScaleFromCompleteTransform()
    {
        var text = new TextGeometry(new Point2D(10, 20), "A", 8);
        var transform = Transform2D.Scale(2, 3).Then(Transform2D.Rotation(Math.PI / 4));
        var placement = TextScreenTransform.Resolve(text, transform.Apply);
        Assert.Equal(Math.PI / 4, placement.RotationRadians, 8);
        Assert.Equal(24, placement.FontSizePixels, 8);
        Assert.Equal(transform.Apply(text.Origin), placement.Origin);
    }
}
