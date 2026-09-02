using System.Text;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengSchemaCorpusV0120Tests
{
    private static readonly CadCustomClassDefinition WallClass = new(
        "TCH_WALL", "TDbWall", "Tianzheng Architecture", 601, 1, true, "None", false);

    [Fact]
    public void BuildClustersSameSchemaWithoutExportingDrawingContents()
    {
        var firstPayload = Payload("PRIVATE_ROOM_ALPHA", false);
        var secondPayload = Payload("PRIVATE_ROOM_BETA", true);
        var first = Wall("A1B2", firstPayload, new CadCustomHandleReference[] { new(330, "SECRET_TARGET_1") }) with
        {
            NativeSemantics = new CadTianzhengWallSemantic(
                new Point2D(123456.789, 987654.321),
                new Point2D(124456.789, 987654.321),
                100,
                100,
                0,
                3000,
                CadTianzhengSemanticDecoder.WallDirectProfile),
            RawDwgObjectRecord = new CadDwgCustomObjectRecord(
                Encoding.UTF8.GetBytes("DWG_PRIVATE_BYTES_ALPHA"),
                12345,
                false,
                "test"),
            Representation = CadCustomEntityRepresentation.ProxyGraphics
        };
        var second = Wall("C3D4", secondPayload, new CadCustomHandleReference[] { new(330, "SECRET_TARGET_2") });
        var unrelated = new CadCustomEntity("E5F6", "VENDOR_SECRET_OBJECT")
        {
            ClassDefinition = new CadCustomClassDefinition(
                "VENDOR_SECRET_OBJECT", "VendorPrivateType", "Vendor Private App", 900, 1, true, "None", false),
            RawDxfPayload = Payload("VENDOR_PRIVATE_VALUE", false)
        };
        var document = Document("private-project-file.dxf", first, second, unrelated);

        var report = CadTianzhengSchemaCorpus.Build(document);
        var json = CadTianzhengSchemaCorpus.ToJson(report);

        Assert.Equal(1, report.SampleCount);
        Assert.Equal(2, report.EntityCount);
        var entry = Assert.Single(report.Entries);
        Assert.Equal("TCH_WALL", entry.DxfName);
        Assert.Equal(2, entry.EntityCount);
        Assert.Equal(1, entry.SamplesContainingProfile);
        Assert.Equal(1, entry.TruncatedRawDxfEntityCount);
        Assert.Equal(1, entry.NativeSemanticEntityCount);
        Assert.Equal(1, entry.ProxyGraphicsEntityCount);
        Assert.Equal(1, entry.RawDwgEvidenceEntityCount);
        Assert.Equal("330x1", entry.ReferenceCodeSignature);
        Assert.DoesNotContain("PRIVATE_ROOM_ALPHA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_ROOM_BETA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_TARGET", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-project-file.dxf", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DWG_PRIVATE_BYTES_ALPHA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("123456.789", json, StringComparison.Ordinal);
        Assert.DoesNotContain("VENDOR_SECRET_OBJECT", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceMultiplicityCreatesSeparateStructuralClusters()
    {
        var payload = Payload("same-schema-values-do-not-matter", false);
        var oneReference = Wall(
            "100",
            payload,
            new CadCustomHandleReference[] { new(330, "200") });
        var twoReferences = Wall(
            "101",
            payload,
            new CadCustomHandleReference[] { new(330, "200"), new(330, "201") });

        var report = CadTianzhengSchemaCorpus.Build(Document("reference-shapes.dxf", oneReference, twoReferences));

        Assert.Equal(2, report.Entries.Count);
        Assert.Contains(report.Entries, entry => entry.ReferenceCodeSignature == "330x1");
        Assert.Contains(report.Entries, entry => entry.ReferenceCodeSignature == "330x2");
    }

    [Fact]
    public void MergeTracksSampleCoverageAndEntityCounts()
    {
        var payload = Payload("sample-specific-value", false);
        var first = CadTianzhengSchemaCorpus.Build(Document(
            "sample-one.dxf",
            Wall("100", payload, Array.Empty<CadCustomHandleReference>()),
            Wall("101", payload, Array.Empty<CadCustomHandleReference>())));
        var second = CadTianzhengSchemaCorpus.Build(Document(
            "sample-two.dxf",
            Wall("200", Payload("different-value-same-schema", false), Array.Empty<CadCustomHandleReference>())));

        var merged = CadTianzhengSchemaCorpus.Merge(new[] { first, second });

        Assert.Equal(2, merged.SampleCount);
        Assert.Equal(3, merged.EntityCount);
        var entry = Assert.Single(merged.Entries);
        Assert.Equal(3, entry.EntityCount);
        Assert.Equal(2, entry.SamplesContainingProfile);
    }

    [Fact]
    public void BuildIncludesModelBlockAndPaperSpaceEntities()
    {
        var payload = Payload("space-independent", false);
        var model = Wall("10", payload, Array.Empty<CadCustomHandleReference>());
        var blockWall = Wall("20", payload, Array.Empty<CadCustomHandleReference>());
        var paper = Wall("30", payload, Array.Empty<CadCustomHandleReference>());
        var block = new CadBlockDefinition("WallBlock", Point2D.Origin, new CadEntity[] { blockWall });
        var layout = new CadLayoutDefinition(
            "Sheet1",
            1,
            true,
            new Size2D(420, 297),
            new BoundingBox2D(0, 0, 420, 297),
            new BoundingBox2D(0, 0, 420, 297),
            new CadEntity[] { paper },
            Array.Empty<CadViewportDefinition>());
        var document = new CadDocument(
            "all-spaces.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            new[] { block },
            new CadEntity[] { model },
            layouts: new[] { layout });

        var report = CadTianzhengSchemaCorpus.Build(document);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(3, entry.EntityCount);
    }

    private static CadCustomEntity Wall(
        string handle,
        CadDxfCustomPayload payload,
        IReadOnlyList<CadCustomHandleReference> references)
        => new(handle, "TCH_WALL")
        {
            ClassDefinition = WallClass,
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload),
            HandleReferences = references
        };

    private static CadDxfCustomPayload Payload(string privateValue, bool truncated)
        => new(
            new CadRawDxfGroup[]
            {
                new(100, "TDbWall"),
                new(10, privateValue),
                new(20, "987654.321"),
                new(40, "200")
            },
            truncated);

    private static CadDocument Document(string displayName, params CadEntity[] entities)
        => new(
            displayName,
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            entities);
}
