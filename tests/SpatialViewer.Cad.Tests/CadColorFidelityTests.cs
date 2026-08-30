using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;
using SpatialViewer.Rendering;

namespace SpatialViewer.Cad.Tests;

public sealed class CadColorFidelityTests
{
    [Fact]
    public void LayerZeroByLayerInheritsInsertLayerAcrossNestedBlocks()
    {
        var leaf = new CadBlockDefinition("LEAF", Point2D.Origin, new CadEntity[] { new CadLineEntity("L1", Point2D.Origin, new(10, 0), "0", CadColor.ByLayer) });
        var nested = new CadBlockDefinition("NEST", Point2D.Origin, new CadEntity[] { new CadBlockReferenceEntity("N1", "LEAF", Point2D.Origin, LayerName: "0", Color: CadColor.ByLayer) });
        var layers = new[] { new CadLayer("0", CadColor.FromAci(7)), new CadLayer("RED", CadColor.FromAci(1)) };
        var document = new CadDocument("layer-zero", "DXF", "AC1015", CadUnits.Unitless, layers, new[] { leaf, nested }, new CadEntity[] { new CadBlockReferenceEntity("I1", "NEST", Point2D.Origin, LayerName: "RED", Color: CadColor.ByLayer) });
        var item = Assert.Single(document.Scene.GetItems());
        Assert.Equal("#FF0000", item.Style.Stroke);
        Assert.Equal("1", item.Metadata["CadColorIndex"]);
    }

    [Fact]
    public void AdaptiveAciMetadataSurvivesRenderPreparation()
    {
        var document = new CadDocument("aci7", "DXF", "AC1015", CadUnits.Unitless, new[] { new CadLayer("0", CadColor.FromAci(7)) }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { new CadLineEntity("W", Point2D.Origin, new(10, 0), Color: CadColor.FromAci(7)) });
        var camera = new Camera2D(document.Bounds.Center);
        var command = Assert.Single(RenderPreparation.Prepare(document.Scene, camera).Commands);
        Assert.NotNull(command.Metadata);
        Assert.Equal(bool.TrueString, command.Metadata![RenderColorPolicy.BackgroundAdaptiveStrokeKey]);
    }

    [Fact]
    public async Task ColorFidelityDxfExercisesReaderAndScenePipeline()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "cad", "dxf", "color-fidelity.dxf");
        var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
        var document = Assert.IsType<CadDocument>(result.Document);
        Assert.True(result.IsSuccess);

        Assert.Contains(document.ModelSpace.OfType<CadLineEntity>(), line => line.Color.Kind == CadColorKind.Aci && line.Color.Index == 8);
        Assert.Contains(document.ModelSpace.OfType<CadLineEntity>(), line => line.Color.Kind == CadColorKind.Aci && line.Color.Index == 30);
        Assert.Contains(document.ModelSpace.OfType<CadLineEntity>(), line => line.Color.Kind == CadColorKind.Aci && line.Color.Index == 113);
        Assert.Contains(document.ModelSpace.OfType<CadLineEntity>(), line => line.Color.Kind == CadColorKind.Aci && line.Color.Index == 254);
        Assert.Contains(document.ModelSpace.OfType<CadLineEntity>(), line => line.Color.Kind == CadColorKind.TrueColor && line.Color.Red == 12 && line.Color.Green == 34 && line.Color.Blue == 56);

        var items = document.Scene.GetItems().ToArray();
        Assert.Contains(items, item => item.Style.Stroke == "#808080");
        Assert.Contains(items, item => item.Style.Stroke == "#FF7F00");
        Assert.Contains(items, item => item.Style.Stroke == "#52A57C");
        Assert.Contains(items, item => item.Style.Stroke == "#CCCCCC");
        Assert.Contains(items, item => item.Style.Stroke == "#0C2238");

        var nestedByBlock = Assert.Single(items.Where(item => item.Bounds.MinX >= 19 && Math.Abs(item.Bounds.Center.Y - 20) < .01));
        Assert.Equal("#FF7F00", nestedByBlock.Style.Stroke);
        Assert.Equal("30", nestedByBlock.Metadata["CadColorIndex"]);

        var nestedByLayer = Assert.Single(items.Where(item => item.Bounds.MinX >= 19 && Math.Abs(item.Bounds.Center.Y - 40) < .01));
        Assert.Equal("#FF0000", nestedByLayer.Style.Stroke);
        Assert.Equal("1", nestedByLayer.Metadata["CadColorIndex"]);
    }
}
