using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengSchemaCorpusJsonV0120Tests
{
    [Fact]
    public void JsonRoundTripReturnsValidatedFrozenReport()
    {
        var source = CadTianzhengSchemaCorpus.Build(Document("100"));
        var json = CadTianzhengSchemaCorpus.ToJson(source);

        var parsed = CadTianzhengSchemaCorpus.FromJson(json);

        Assert.Equal(source.SchemaVersion, parsed.SchemaVersion);
        Assert.Equal(source.SampleCount, parsed.SampleCount);
        Assert.Equal(source.EntityCount, parsed.EntityCount);
        Assert.IsType<ReadOnlyCollection<CadTianzhengSchemaCorpusEntry>>(parsed.Entries);
        var expected = Assert.Single(source.Entries);
        var actual = Assert.Single(parsed.Entries);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MergeJsonCombinesIndependentReports()
    {
        var first = CadTianzhengSchemaCorpus.ToJson(CadTianzhengSchemaCorpus.Build(Document("100")));
        var second = CadTianzhengSchemaCorpus.ToJson(CadTianzhengSchemaCorpus.Build(Document("200")));

        var merged = CadTianzhengSchemaCorpus.MergeJson(new[] { first, second });

        Assert.Equal(2, merged.SampleCount);
        Assert.Equal(2, merged.EntityCount);
        var entry = Assert.Single(merged.Entries);
        Assert.Equal(2, entry.EntityCount);
        Assert.Equal(2, entry.SamplesContainingProfile);
    }

    [Fact]
    public void FromJsonRejectsMalformedAndWrongSchemaReports()
    {
        Assert.Throws<FormatException>(() => CadTianzhengSchemaCorpus.FromJson("{not-json}"));

        const string oldSchema = """
            {
              "schemaVersion": 1,
              "sampleCount": 0,
              "entries": []
            }
            """;
        var exception = Assert.Throws<ArgumentException>(() => CadTianzhengSchemaCorpus.FromJson(oldSchema));
        Assert.Contains("version: 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromJsonRejectsCoverageCountsThatExceedEntityCount()
    {
        var json = CadTianzhengSchemaCorpus.ToJson(CadTianzhengSchemaCorpus.Build(Document("100")));
        var root = JsonNode.Parse(json)!.AsObject();
        var entry = root["entries"]!.AsArray()[0]!.AsObject();
        entry["resolvedRelationshipEntityCount"] = 2;

        var exception = Assert.Throws<ArgumentException>(
            () => CadTianzhengSchemaCorpus.FromJson(root.ToJsonString()));

        Assert.Contains("ResolvedRelationshipEntityCount", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToJsonAlsoRejectsManuallyConstructedInvalidReports()
    {
        var valid = Assert.Single(CadTianzhengSchemaCorpus.Build(Document("100")).Entries);
        var invalidEntry = valid with
        {
            EntityCount = 1,
            SamplesContainingProfile = 2
        };
        var invalidReport = new CadTianzhengSchemaCorpusReport(
            CadTianzhengSchemaCorpus.CurrentSchemaVersion,
            1,
            new[] { invalidEntry });

        var exception = Assert.Throws<ArgumentException>(() => CadTianzhengSchemaCorpus.ToJson(invalidReport));

        Assert.Contains("sample coverage", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CadDocument Document(string handle)
    {
        var wall = new CadCustomEntity(handle, "TCH_WALL")
        {
            ClassDefinition = new CadCustomClassDefinition(
                "TCH_WALL",
                "TDbWall",
                "Tianzheng Architecture",
                601,
                1,
                true,
                "None",
                false)
        };
        return new CadDocument(
            "private-file-name.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { wall });
    }
}
