using SpatialViewer.Core;
using SpatialViewer.Rendering;

namespace SpatialViewer.Rendering.Tests;

public sealed class TextFidelityV070Tests
{
    [Fact]
    public void DefaultTextPlacementRemainsBackwardCompatible()
    {
        var text = new TextGeometry(new Point2D(10, 20), "A", 8);
        var transform = Transform2D.Scale(2, 3).Then(Transform2D.Rotation(Math.PI / 4));
        var placement = TextScreenTransform.Resolve(text, transform.Apply);
        Assert.Equal(Math.PI / 4, placement.RotationRadians, 8);
        Assert.Equal(24, placement.FontSizePixels, 8);
        Assert.Equal(transform.Apply(text.Origin), placement.Origin);
    }

    [Fact]
    public void WidthFactorMirrorAndObliqueAreResolvedAfterFullMapping()
    {
        var text = new TextGeometry(Point2D.Origin, "AB", 10)
        {
            WidthFactor = .5,
            ObliqueAngleRadians = .2,
            IsBackward = true,
            IsUpsideDown = true
        };
        var placement = TextScreenTransform.Resolve(text, Transform2D.Scale(2, 4).Apply);
        Assert.Equal(-.25, placement.HorizontalScale, 8);
        Assert.Equal(-1, placement.VerticalScale, 8);
        Assert.Equal(Math.Tan(.2), placement.ObliqueShear, 8);
    }

    [Fact]
    public void BaselineCenteredTextMovesScreenOriginToAnchoredTopLeft()
    {
        var text = new TextGeometry(new Point2D(100, 50), "AB", 10)
        {
            HorizontalAlignment = TextHorizontalAlignment2D.Center,
            VerticalAlignment = TextVerticalAlignment2D.Baseline
        };
        var placement = TextScreenTransform.Resolve(text, point => point);
        var bounds = text.GetBounds();
        Assert.Equal(new Point2D(bounds.MinX, bounds.MaxY), placement.Origin);
        Assert.True(placement.Origin.X < text.Origin.X);
        Assert.True(placement.Origin.Y > text.Origin.Y);
    }
}
