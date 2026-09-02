using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengSchemaCorpusSemanticCoverageV0120Tests
{
    [Fact]
    public void BuildSeparatesPartialDrawableAndUndecodedSemanticCoverage()
    {
        var wall = new CadCustomEntity("100", "TCH_WALL")
        {
            ClassDefinition = new CadCustomClassDefinition("TCH_WALL", "TDbWall", "Tianzheng Architecture", 601, 1, true, "None", false),
            NativeSemantics = new CadTianzhengWallSemantic(
                new Point2D(0, 0),
                new Point2D(1000, 0),
                75,
                75,
                0,
                3000,
                CadTianzhengSemanticDecoder.WallDirectProfile)
        };
        var opening = new CadCustomEntity("200", "TCH_OPENING")
        {
            ClassDefinition = new CadCustomClassDefinition("TCH_OPENING", "TDbOpening", "Tianzheng Architecture", 602, 1, true, "None", false),
            NativeSemantics = new CadTianzhengOpeningAnchorSemantic(
                new Point2D(500, 0),
                0,
                CadTianzhengSemanticDecoder.OpeningAnchorDirectProfile)
        };
        var undecoded = new CadCustomEntity("300", "TCH_COLUMN")
        {
            ClassDefinition = new CadCustomClassDefinition("TCH_COLUMN", "TDbColumn", "Tianzheng Architecture", 603, 1, true, "None", false)
        };

        var report = CadTianzhengSchemaCorpus.Build(Document(wall, opening, undecoded));

        Assert.Equal(3, report.SchemaVersion);
        var wallEntry = Assert.Single(report.Entries, entry => entry.DxfName == "TCH_WALL");
        var openingEntry = Assert.Single(report.Entries, entry => entry.DxfName == "TCH_OPENING");
        var columnEntry = Assert.Single(report.Entries, entry => entry.DxfName == "TCH_COLUMN");

        Assert.Equal(1, wallEntry.NativeSemanticEntityCount);
        Assert.Equal(0, wallEntry.PartialSemanticEntityCount);
        Assert.Equal(1, wallEntry.Drawable2DSemanticEntityCount);

        Assert.Equal(1, openingEntry.NativeSemanticEntityCount);
        Assert.Equal(1, openingEntry.PartialSemanticEntityCount);
        Assert.Equal(0, openingEntry.Drawable2DSemanticEntityCount);

        Assert.Equal(0, columnEntry.NativeSemanticEntityCount);
        Assert.Equal(0, columnEntry.PartialSemanticEntityCount);
        Assert.Equal(0, columnEntry.Drawable2DSemanticEntityCount);
    }

    [Fact]
    public void MergeAccumulatesSemanticCoverageWithoutChangingSchemaIdentity()
    {
        var first = CadTianzhengSchemaCorpus.Build(Document(Opening("100")));
        var second = CadTianzhengSchemaCorpus.Build(Document(Opening("200")));

        var merged = CadTianzhengSchemaCorpus.Merge(new[] { first, second });

        var entry = Assert.Single(merged.Entries);
        Assert.Equal("TCH_OPENING", entry.DxfName);
        Assert.Equal(2, entry.EntityCount);
        Assert.Equal(2, entry.SamplesContainingProfile);
        Assert.Equal(2, entry.NativeSemanticEntityCount);
        Assert.Equal(2, entry.PartialSemanticEntityCount);
        Assert.Equal(0, entry.Drawable2DSemanticEntityCount);
    }

    private static CadCustomEntity Opening(string handle)
        => new(handle, "TCH_OPENING")
        {
            ClassDefinition = new CadCustomClassDefinition("TCH_OPENING", "TDbOpening", "Tianzheng Architecture", 602, 1, true, "None", false),
            NativeSemantics = new CadTianzhengOpeningAnchorSemantic(
                new Point2D(10, 20),
                0,
                CadTianzhengSemanticDecoder.OpeningAnchorDirectProfile)
        };

    private static CadDocument Document(params CadEntity[] entities)
        => new(
            "semantic-coverage.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            entities);
}
