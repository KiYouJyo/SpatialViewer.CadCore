using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class PatternHatchV0100Tests
{
    [Fact]
    public void ContinuousPatternLinesAreClippedInsideRectangle()
    {
        var hatch = Hatch(Rectangle(0, 0, 10, 10), new CadHatchPatternLine(0, new Point2D(0, 1), new Vector2D(0, 2), Array.Empty<double>()));
        var result = CadHatchPatternTessellator.Tessellate(hatch, Loops(hatch));
        var lines = result.Geometries.OfType<LineGeometry>().OrderBy(line => line.Start.Y).ToArray();

        Assert.False(result.Truncated);
        Assert.Equal(5, lines.Length);
        Assert.Equal(new[] { 1d, 3d, 5d, 7d, 9d }, lines.Select(line => line.Start.Y).ToArray());
        Assert.All(lines, line =>
        {
            Assert.Equal(0, Math.Min(line.Start.X, line.End.X), 8);
            Assert.Equal(10, Math.Max(line.Start.X, line.End.X), 8);
        });
    }

    [Fact]
    public void EvenOddClippingPreservesHoleInPatternHatch()
    {
        var hatch = Hatch(
            new[] { Rectangle(0, 0, 10, 10), Rectangle(4, 4, 6, 6) },
            new CadHatchPatternLine(0, new Point2D(0, 1), new Vector2D(0, 2), Array.Empty<double>()));
        var result = CadHatchPatternTessellator.Tessellate(hatch, Loops(hatch));
        var middle = result.Geometries.OfType<LineGeometry>().Where(line => Math.Abs(line.Start.Y - 5) < 1e-8).OrderBy(line => line.Start.X).ToArray();

        Assert.Equal(2, middle.Length);
        Assert.Equal(0, middle[0].Start.X, 8);
        Assert.Equal(4, middle[0].End.X, 8);
        Assert.Equal(6, middle[1].Start.X, 8);
        Assert.Equal(10, middle[1].End.X, 8);
        Assert.DoesNotContain(result.Geometries.OfType<LineGeometry>(), line => line.Start.Y <= 5 && line.End.Y >= 5 && Math.Min(line.Start.X, line.End.X) < 5 && Math.Max(line.Start.X, line.End.X) > 5);
    }

    [Fact]
    public void DashPatternIsExpandedBeforeRendering()
    {
        var hatch = Hatch(Rectangle(0, 0, 10, 2), new CadHatchPatternLine(0, new Point2D(0, 1), new Vector2D(0, 5), new[] { 2d, -1d }));
        var result = CadHatchPatternTessellator.Tessellate(hatch, Loops(hatch));
        var lines = result.Geometries.OfType<LineGeometry>().OrderBy(line => line.Start.X).ToArray();

        Assert.Equal(4, lines.Length);
        Assert.Equal((0d, 2d), (lines[0].Start.X, lines[0].End.X));
        Assert.Equal((3d, 5d), (lines[1].Start.X, lines[1].End.X));
        Assert.Equal((6d, 8d), (lines[2].Start.X, lines[2].End.X));
        Assert.Equal((9d, 10d), (lines[3].Start.X, lines[3].End.X));
    }

    [Fact]
    public void PatternAngleAndScaleTransformLineFamily()
    {
        var hatch = Hatch(Rectangle(0, 0, 10, 10), new CadHatchPatternLine(0, new Point2D(1, 0), new Vector2D(2, 0), Array.Empty<double>()), patternAngle: Math.PI / 2, patternScale: 2);
        var result = CadHatchPatternTessellator.Tessellate(hatch, Loops(hatch));
        var lines = result.Geometries.OfType<LineGeometry>().ToArray();

        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.True(Math.Abs(line.Start.X - line.End.X) < 1e-7));
    }

    [Fact]
    public void SceneContainsPatternGeometryAndExplicitDiagnostics()
    {
        var hatch = Hatch(Rectangle(0, 0, 10, 10), new CadHatchPatternLine(0, new Point2D(0, 1), new Vector2D(0, 2), Array.Empty<double>()));
        var document = Document(hatch);
        var items = document.Scene.GetItems().Where(item => item.Id == hatch.ObjectId).ToArray();

        Assert.Contains(items, item => item.Geometry is CompoundPathGeometry);
        Assert.Equal(5, items.Count(item => item.Geometry is LineGeometry));
        Assert.All(items, item => Assert.Equal("1", item.Metadata["HatchPatternDefinitionLineCount"]));
        Assert.Contains(items, item => item.Metadata.TryGetValue("HatchPatternGeometryCount", out var count) && count == "5");
        Assert.DoesNotContain(items.Where(item => item.Geometry is LineGeometry), item => item.Metadata.ContainsKey("LineTypePattern"));
    }

    [Fact]
    public void SolidHatchRemainsCompoundFillWithoutPatternExpansion()
    {
        var loops = new[] { Loop(Rectangle(0, 0, 5, 5)) };
        var hatch = new CadHatchEntity("SOLID10", loops, IsSolid: true, PatternName: "SOLID")
        {
            PatternLines = new[] { new CadHatchPatternLine(0, Point2D.Origin, new Vector2D(0, 1), Array.Empty<double>()) }
        };
        var item = Assert.Single(Document(hatch).Scene.GetItems(), candidate => candidate.Id == hatch.ObjectId);

        Assert.IsType<CompoundPathGeometry>(item.Geometry);
        Assert.NotNull(item.Style.Fill);
    }

    private static CadHatchEntity Hatch(IReadOnlyList<Point2D> rectangle, CadHatchPatternLine line, double patternAngle = 0, double patternScale = 1)
        => Hatch(new[] { rectangle }, line, patternAngle, patternScale);

    private static CadHatchEntity Hatch(IReadOnlyList<IReadOnlyList<Point2D>> boundaries, CadHatchPatternLine line, double patternAngle = 0, double patternScale = 1)
        => new("PAT10", boundaries.Select(Loop).ToArray(), IsSolid: false, PatternName: "CUSTOM", PatternAngleRadians: patternAngle, PatternScale: patternScale)
        {
            PatternLines = new[] { line }
        };

    private static IReadOnlyList<IReadOnlyList<Point2D>> Loops(CadHatchEntity hatch)
        => hatch.Loops.Select(CadCurveTessellator.HatchLoop).Where(loop => loop.Count >= 3).ToArray();

    private static CadHatchLoop Loop(IReadOnlyList<Point2D> points)
        => new(new CadHatchEdge[] { new CadHatchPolylineEdge(points, Enumerable.Repeat(0d, points.Count).ToArray(), true) });

    private static IReadOnlyList<Point2D> Rectangle(double minX, double minY, double maxX, double maxY)
        => new[] { new Point2D(minX, minY), new Point2D(maxX, minY), new Point2D(maxX, maxY), new Point2D(minX, maxY) };

    private static CadDocument Document(CadEntity entity) => new(
        "pattern-v0100.dxf",
        "DXF",
        "AC1032",
        CadUnits.Unitless,
        new[] { new CadLayer("0", CadColor.FromAci(7)) },
        Array.Empty<CadBlockDefinition>(),
        new[] { entity });
}
