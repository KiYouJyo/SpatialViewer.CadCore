using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class FidelityV030Tests
{
    [Fact]
    public async Task BulgedLwPolylinePreservesBulgeAndProducesAnalyticArc()
    {
        var document = await ImportAsync();
        var polyline = Assert.Single(document.ModelSpace.OfType<CadPolylineEntity>());
        Assert.Equal(2, polyline.Bulges.Count);
        Assert.Equal(1d, polyline.Bulges[0], 12);
        Assert.Equal(0d, polyline.Bulges[1], 12);
        var item = Assert.Single(document.Scene.GetItems(), candidate => candidate.Id == polyline.ObjectId);
        var arc = Assert.IsType<ArcGeometry>(item.Geometry);
        Assert.Equal(10, arc.Center.X, 8);
        Assert.Equal(0, arc.Center.Y, 8);
        Assert.Equal(10, arc.Radius, 8);
        Assert.Equal(Math.PI, arc.SweepRadians, 8);
    }

    [Fact]
    public async Task ResolvedLineTypePatternAndScalesReachSceneMetadata()
    {
        var document = await ImportAsync();
        var line = Assert.Single(document.ModelSpace.OfType<CadLineEntity>());
        Assert.Equal("DASHED", line.LineTypeName);
        Assert.Equal("6;-4", line.Metadata["LineTypePattern"]);
        Assert.Equal("1.5", line.Metadata["LineTypeScale"]);
        Assert.Equal("2", line.Metadata["GlobalLineTypeScale"]);
        var item = Assert.Single(document.Scene.GetItems(), candidate => candidate.Id == line.ObjectId);
        Assert.Equal("6;-4", item.Metadata["LineTypePattern"]);
    }

    [Fact]
    public async Task RotatedEllipseRemainsEllipseWithNonIdentityTransform()
    {
        var document = await ImportAsync();
        var ellipse = Assert.Single(document.ModelSpace.OfType<CadEllipseEntity>());
        Assert.InRange(ellipse.RotationRadians, (Math.PI / 4) - 1e-8, (Math.PI / 4) + 1e-8);
        var item = Assert.Single(document.Scene.GetItems(), candidate => candidate.Id == ellipse.ObjectId);
        Assert.IsType<EllipseGeometry>(item.Geometry);
        Assert.NotEqual(Transform2D.Identity, item.Transform);
    }

    [Fact]
    public async Task TextRotationAndBlockScaleRemainInWorldTransform()
    {
        var document = await ImportAsync();
        var textItems = document.Scene.GetItems().Where(item => item.Geometry is TextGeometry).ToArray();
        Assert.Equal(2, textItems.Length);
        Assert.All(textItems, item => Assert.NotEqual(Transform2D.Identity, item.Transform));
        var blockText = Assert.Single(textItems, item => ((TextGeometry)item.Geometry).Text == "Scaled text");
        var text = (TextGeometry)blockText.Geometry;
        var origin = blockText.Transform.Apply(text.Origin);
        var x = blockText.Transform.Apply(new Point2D(text.Origin.X + 1, text.Origin.Y));
        Assert.Equal(2, origin.DistanceTo(x), 8);
    }

    private static async Task<CadDocument> ImportAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "cad", "dxf", "fidelity-v030.dxf");
        var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
        Assert.True(result.IsSuccess);
        return Assert.IsType<CadDocument>(result.Document);
    }
}
