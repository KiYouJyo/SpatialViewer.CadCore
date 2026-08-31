using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class EntityCoverageV050Tests
{
    private static readonly CadLayer Layer0 = new("0", CadColor.FromAci(7));

    [Fact]
    public void LinearDimensionRetainsSemanticsAndProducesPickableAnnotationGeometry()
    {
        var dimension = new CadDimensionEntity(
            "D50-L",
            CadDimensionKind.Linear,
            new(100, 20),
            new(50, 23),
            "100.00",
            100,
            0,
            2.5,
            2.5,
            "STANDARD",
            new Dictionary<string, Point2D>
            {
                ["FirstPoint"] = new(0, 0),
                ["SecondPoint"] = new(100, 0)
            });
        var document = Document(dimension);

        Assert.Equal(CadDimensionKind.Linear, dimension.Kind);
        Assert.Equal(100, dimension.Measurement, 8);
        Assert.Equal(2, dimension.ReferencePoints.Count);
        Assert.Contains(document.Scene.GetItems(), item => item.Geometry is TextGeometry text && text.Text == "100.00");
        Assert.True(document.Scene.GetItems().Count(item => item.Geometry is LineGeometry) >= 7);
        var hit = HitTesting.HitTest(document.Scene, new(50, 20), .05);
        Assert.NotNull(hit);
        Assert.Equal(dimension.ObjectId, hit!.Value);
        Assert.Contains(document.Scene.GetItems(), item => item.Metadata.TryGetValue("DimensionSemantic", out var semantic) && semantic == "True");
    }

    [Fact]
    public void AngularDimensionUsesAnalyticArcAndPreservesReferencePoints()
    {
        var dimension = new CadDimensionEntity(
            "D50-A",
            CadDimensionKind.Angular3Point,
            new(Math.Sqrt(50), Math.Sqrt(50)),
            new(7, 7),
            "90°",
            Math.PI / 2,
            0,
            2.5,
            2,
            "STANDARD",
            new Dictionary<string, Point2D>
            {
                ["AngleVertex"] = new(0, 0),
                ["FirstPoint"] = new(10, 0),
                ["SecondPoint"] = new(0, 10)
            });
        var document = Document(dimension);

        var arc = Assert.Single(document.Scene.GetItems(), item => item.Geometry is ArcGeometry);
        var geometry = Assert.IsType<ArcGeometry>(arc.Geometry);
        Assert.Equal(Math.PI / 2, Math.Abs(geometry.SweepRadians), 8);
        Assert.NotNull(HitTesting.HitTest(document.Scene, new(Math.Sqrt(50), Math.Sqrt(50)), .1));
        Assert.Equal("Angular3Point", arc.Metadata["DimensionKind"]);
    }

    [Fact]
    public void ClassicLeaderKeepsLinkedAnnotationIdentityAndRemainsPickable()
    {
        var leader = new CadLeaderEntity(
            "L50",
            new[] { new Point2D(0, 0), new Point2D(10, 5), new Point2D(20, 5) },
            true,
            false,
            "A50",
            "MTEXT",
            "Room 101",
            new Point2D(22, 5),
            2.5,
            "STANDARD");
        var document = Document(leader);

        var path = Assert.Single(document.Scene.GetItems(), item => item.Geometry is PolylineGeometry);
        Assert.Equal("A50", path.Metadata["LeaderAssociatedHandle"]);
        Assert.Equal("MTEXT", path.Metadata["LeaderAssociatedType"]);
        Assert.Equal("Room 101", path.Metadata["LeaderAnnotationText"]);
        Assert.NotNull(HitTesting.HitTest(document.Scene, new(10, 5), .1));
        Assert.True(document.Scene.GetItems().Count(item => item.Geometry is LineGeometry) >= 2);
    }

    [Fact]
    public void MultiLeaderPreservesMultiplePathsDoglegsAndEmbeddedText()
    {
        var paths = new[]
        {
            new CadLeaderPath(new[] { new Point2D(0, 0), new Point2D(10, 5), new Point2D(20, 5), new Point2D(25, 5) }, false, 2, new Point2D(20, 5), new Point2D(25, 5)),
            new CadLeaderPath(new[] { new Point2D(0, 10), new Point2D(10, 7), new Point2D(20, 5), new Point2D(25, 5) }, true, 2, new Point2D(20, 5), new Point2D(25, 5))
        };
        var multiLeader = new CadMultiLeaderEntity(
            "ML50",
            paths,
            "Two leaders",
            new(27, 5),
            2.5,
            0,
            "MTextContent",
            true,
            5,
            2,
            "MLEADER-STD");
        var document = Document(multiLeader);

        Assert.Equal(2, multiLeader.Paths.Count);
        Assert.Contains(document.Scene.GetItems(), item => item.Geometry is PolylineGeometry && item.Metadata["MultiLeaderPathIndex"] == "0");
        Assert.Contains(document.Scene.GetItems(), item => item.Geometry is PathGeometry && item.Metadata["MultiLeaderPathIndex"] == "1");
        Assert.Contains(document.Scene.GetItems(), item => item.Geometry is TextGeometry text && text.Text == "Two leaders");
        Assert.Contains(document.Scene.GetItems(), item => item.Metadata.TryGetValue("MultiLeaderDogleg", out var dogleg) && dogleg == "True");
        Assert.NotNull(HitTesting.HitTest(document.Scene, new(20, 5), .1));
    }

    private static CadDocument Document(params CadEntity[] entities) => new(
        "annotations",
        "DXF",
        "AC1027",
        CadUnits.Unitless,
        new[] { Layer0 },
        Array.Empty<CadBlockDefinition>(),
        entities);
}
