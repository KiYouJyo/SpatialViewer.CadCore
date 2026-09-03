using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class AnonymousBlockV0121Tests
{
    [Fact]
    public async Task ReferencedAnonymousBlockDefinitionSurvivesReaderAndReachesScene()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "cad", "dxf", "anonymous-block-v0121.dxf");
        var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));

        Assert.True(result.IsSuccess);
        var document = Assert.IsType<CadDocument>(result.Document);
        var block = Assert.Single(document.Blocks, candidate => candidate.Name == "*U123");
        Assert.Single(block.Entities);
        var reference = Assert.Single(document.ModelSpace.OfType<CadBlockReferenceEntity>());
        Assert.Equal("*U123", reference.BlockName);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_INVALID_BLOCK_REFERENCE");

        var lineItem = Assert.Single(document.Scene.GetItems(), candidate => candidate.Id != reference.ObjectId && candidate.Geometry is LineGeometry);
        var line = Assert.IsType<LineGeometry>(lineItem.Geometry);
        var start = lineItem.Transform.Apply(line.Start);
        var end = lineItem.Transform.Apply(line.End);
        Assert.Equal(100, start.X, 12);
        Assert.Equal(200, start.Y, 12);
        Assert.Equal(100, end.X, 12);
        Assert.Equal(210, end.Y, 12);
    }
}
