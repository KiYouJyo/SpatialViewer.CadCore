using SpatialViewer.Core;
using SpatialViewer.Rendering;

namespace SpatialViewer.Rendering.Tests;

public sealed class RenderPreparationTests
{
    [Fact] public void PreparationTraversesNestedTransforms() { var document = SyntheticScenes.NestedTransforms(); var camera = new Camera2D(document.Bounds.Center); var command = Assert.Single(RenderPreparation.Prepare(document.Scene, camera).Commands); Assert.Equal(new Point2D(120, 75), command.Bounds.Center); Assert.InRange(command.Bounds.Width, 84.2, 84.4); Assert.InRange(command.Bounds.Height, 65.9, 66.1); }
    [Fact] public void PreparationExcludesInvisibleLayers() { var document = SyntheticScenes.BasicPrimitives(); var camera = new Camera2D(document.Bounds.Center); var all = RenderPreparation.Prepare(document.Scene, camera).Commands.Count; document.Layers[1].IsVisible = false; Assert.True(RenderPreparation.Prepare(document.Scene, camera).Commands.Count < all); }
    [Fact] public void PreparationUsesCameraAsLocalOriginForLargeCoordinates() { var document = SyntheticScenes.LargeCoordinates(); var camera = new Camera2D(new(500600, 3400350)); var frame = RenderPreparation.Prepare(document.Scene, camera); Assert.Equal(camera.Target, frame.LocalOrigin); Assert.All(frame.Commands, command => Assert.True(command.Bounds.MinX > 499000)); }
}
