using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class EntityCoverageV040Tests
{
    private static readonly CadLayer Layer0 = new("0", CadColor.FromAci(7));

    [Fact]
    public void SplineRetainsSemanticDefinitionAndTranslatesToPath()
    {
        var definition = new CadSplineDefinition(
            3,
            new[] { new Point2D(0, 0), new Point2D(10, 20), new Point2D(20, -10), new Point2D(30, 0) },
            new[] { 0d, 0d, 0d, 0d, 1d, 1d, 1d, 1d },
            new[] { 1d, 1d, 1d, 1d },
            Array.Empty<Point2D>());
        var spline = new CadSplineEntity("S1", definition);
        var document = new CadDocument("spline", "DXF", "AC1027", CadUnits.Unitless, new[] { Layer0 }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { spline });

        Assert.Same(definition, spline.Spline);
        Assert.Equal(3, spline.Spline.Degree);
        Assert.Equal(4, spline.Spline.ControlPoints.Count);
        var item = Assert.Single(document.Scene.GetItems());
        var path = Assert.IsType<PathGeometry>(item.Geometry);
        Assert.True(path.Points.Count >= 64);
        Assert.Equal("3", item.Metadata["SplineDegree"]);
    }

    [Fact]
    public void SolidHatchUsesCompoundEvenOddFillAndKeepsHole()
    {
        static CadHatchPolylineEdge Loop(double min, double max) => new(
            new[] { new Point2D(min, min), new Point2D(max, min), new Point2D(max, max), new Point2D(min, max) },
            new[] { 0d, 0d, 0d, 0d });

        var hatch = new CadHatchEntity("H1", new[]
        {
            new CadHatchLoop(new CadHatchEdge[] { Loop(0, 10) }),
            new CadHatchLoop(new CadHatchEdge[] { Loop(3, 7) })
        }, Color: CadColor.FromAci(1));
        var document = new CadDocument("hatch", "DXF", "AC1027", CadUnits.Unitless, new[] { Layer0 }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { hatch });

        var item = Assert.Single(document.Scene.GetItems());
        var compound = Assert.IsType<CompoundPathGeometry>(item.Geometry);
        Assert.Equal(2, compound.Loops.Count);
        Assert.Equal("#FF0000", item.Style.Fill);
        Assert.NotNull(HitTesting.HitTest(document.Scene, new(1, 1), .05));
        Assert.Null(HitTesting.HitTest(document.Scene, new(5, 5), .05));
        Assert.Equal("2", item.Metadata["HatchLoopCount"]);
    }

    [Fact]
    public void PatternHatchRetainsPatternMetadataWithoutPretendingSolidFill()
    {
        var loop = new CadHatchLoop(new CadHatchEdge[]
        {
            new CadHatchLineEdge(new(0, 0), new(10, 0)),
            new CadHatchLineEdge(new(10, 0), new(10, 10)),
            new CadHatchLineEdge(new(10, 10), new(0, 10)),
            new CadHatchLineEdge(new(0, 10), new(0, 0))
        });
        var hatch = new CadHatchEntity("HP", new[] { loop }, false, "ANSI31", Math.PI / 4, 2);
        var document = new CadDocument("pattern", "DXF", "AC1027", CadUnits.Unitless, new[] { Layer0 }, Array.Empty<CadBlockDefinition>(), new CadEntity[] { hatch });

        var item = Assert.Single(document.Scene.GetItems());
        Assert.Null(item.Style.Fill);
        Assert.Equal("ANSI31", item.Metadata["HatchPattern"]);
        Assert.Equal("False", item.Metadata["HatchSolid"]);
    }

    [Fact]
    public void BlockAttributeOverridesVariableDefinitionWithoutDoubleTransform()
    {
        var variableDefinition = new CadAttributeEntity("D1", new(10, 5), "ROOM", "DEFAULT", 2, IsDefinition: true);
        var constantDefinition = new CadAttributeEntity("D2", new(0, 0), "CONST", "C", 2, IsDefinition: true, IsConstant: true);
        var block = new CadBlockDefinition("ROOMTAG", Point2D.Origin, new CadEntity[] { variableDefinition, constantDefinition });
        var instance = new CadAttributeEntity("A1", new(110, 205), "ROOM", "A-101", 2);
        var reference = new CadBlockReferenceEntity("I1", "ROOMTAG", new(100, 200)) { Attributes = new[] { instance } };
        var document = new CadDocument("attributes", "DXF", "AC1027", CadUnits.Unitless, new[] { Layer0 }, new[] { block }, new CadEntity[] { reference });

        var texts = document.Scene.GetItems().Where(item => item.Geometry is TextGeometry).ToArray();
        Assert.Equal(2, texts.Length);
        Assert.DoesNotContain(texts, item => ((TextGeometry)item.Geometry).Text == "DEFAULT");
        var instanceItem = Assert.Single(texts, item => ((TextGeometry)item.Geometry).Text == "A-101");
        var text = (TextGeometry)instanceItem.Geometry;
        var worldOrigin = instanceItem.Transform.Apply(text.Origin);
        Assert.Equal(110, worldOrigin.X, 8);
        Assert.Equal(205, worldOrigin.Y, 8);
        Assert.Equal("ROOM", instanceItem.Metadata["AttributeTag"]);
        Assert.Contains(texts, item => ((TextGeometry)item.Geometry).Text == "C");
    }
}
