using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class EntityCoverageV040FixtureTests
{
    [Fact]
    public async Task DxfReaderImportsSplineSolidHatchAndBlockAttributes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "cad", "dxf", "entity-coverage-v040.dxf");
        var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var document = Assert.IsType<CadDocument>(result.Document);

        var spline = Assert.Single(document.ModelSpace.OfType<CadSplineEntity>());
        Assert.Equal(3, spline.Spline.Degree);
        Assert.Equal(4, spline.Spline.ControlPoints.Count);
        Assert.Equal(8, spline.Spline.Knots.Count);

        var hatch = Assert.Single(document.ModelSpace.OfType<CadHatchEntity>());
        Assert.True(hatch.IsSolid);
        Assert.Equal(2, hatch.Loops.Count);
        var hatchItem = Assert.Single(document.Scene.GetItems(), item => item.Id == hatch.ObjectId);
        Assert.IsType<CompoundPathGeometry>(hatchItem.Geometry);
        Assert.NotNull(hatchItem.Style.Fill);
        Assert.Null(HitTesting.HitTest(document.Scene, new(50, 10), .05));
        Assert.NotNull(HitTesting.HitTest(document.Scene, new(42, 2), .05));

        var block = Assert.Single(document.Blocks, definition => definition.Name == "TAGBLOCK");
        Assert.Equal(2, block.Entities.OfType<CadAttributeEntity>().Count());
        var insert = Assert.Single(document.ModelSpace.OfType<CadBlockReferenceEntity>());
        var attribute = Assert.Single(insert.Attributes);
        Assert.Equal("ROOM", attribute.Tag);
        Assert.Equal("A-101", attribute.Value);
        var instanceText = Assert.Single(document.Scene.GetItems(), item => item.Metadata.TryGetValue("AttributeTag", out var tag) && tag == "ROOM" && item.Geometry is TextGeometry text && text.Text == "A-101");
        var textGeometry = (TextGeometry)instanceText.Geometry;
        var worldOrigin = instanceText.Transform.Apply(textGeometry.Origin);
        Assert.Equal(110, worldOrigin.X, 8);
        Assert.Equal(205, worldOrigin.Y, 8);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_UNSUPPORTED_ENTITY" && (diagnostic.Message.Contains("SPLINE", StringComparison.OrdinalIgnoreCase) || diagnostic.Message.Contains("HATCH", StringComparison.OrdinalIgnoreCase) || diagnostic.Message.Contains("ATTRIB", StringComparison.OrdinalIgnoreCase) || diagnostic.Message.Contains("ATTDEF", StringComparison.OrdinalIgnoreCase)));
    }
}
