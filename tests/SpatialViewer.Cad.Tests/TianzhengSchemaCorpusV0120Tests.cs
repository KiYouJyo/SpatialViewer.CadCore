using System.Text;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengSchemaCorpusV0120Tests
{
    private static readonly CadCustomClassDefinition WallClass = new(
        "TCH_WALL", "TDbWall", "Tianzheng Architecture", 601, 1, true, "None", false);
    private static readonly CadCustomClassDefinition OpeningClass = new(
        "TCH_OPENING", "TDbOpening", "Tianzheng Architecture", 602, 1, true, "None", false);

    [Fact]
    public void BuildClustersSameSchemaWithoutExportingDrawingContents()
    {
        var firstPayload = Payload("PRIVATE_ROOM_ALPHA", false);
        var secondPayload = Payload("PRIVATE_ROOM_BETA", false);
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

        Assert.Equal(2, report.SchemaVersion);
        Assert.Equal(1, report.SampleCount);
        Assert.Equal(2, report.EntityCount);
        var entry = Assert.Single(report.Entries);
        Assert.Equal("TCH_WALL", entry.DxfName);
        Assert.Equal(2, entry.EntityCount);
        Assert.Equal(1, entry.SamplesContainingProfile);
        Assert.Equal(0, entry.TruncatedRawDxfEntityCount);
        Assert.Equal(1, entry.NativeSemanticEntityCount);
        Assert.Equal(1, entry.ProxyGraphicsEntityCount);
        Assert.Equal(1, entry.RawDwgEvidenceEntityCount);
        Assert.Equal(0, entry.ResolvedRelationshipEntityCount);
        Assert.Equal(0, entry.ResolvedRelationshipCount);
        Assert.Equal(0, entry.OpeningHostWallEntityCount);
        Assert.Equal(0, entry.OpeningHostWallRelationshipCount);
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
    public void TruncatedPayloadRemainsSeparateFromCompleteSchema()
    {
        var complete = Wall(
            "100",
            Payload("complete-value", false),
            Array.Empty<CadCustomHandleReference>());
        var truncated = Wall(
            "101",
            Payload("truncated-value", true),
            Array.Empty<CadCustomHandleReference>());

        var report = CadTianzhengSchemaCorpus.Build(Document("truncation.dxf", complete, truncated));

        Assert.Equal(2, report.Entries.Count);
        var truncatedEntry = Assert.Single(report.Entries, entry => entry.TruncatedRawDxfEntityCount == 1);
        var completeEntry = Assert.Single(report.Entries, entry => entry.TruncatedRawDxfEntityCount == 0);
        Assert.NotEqual(completeEntry.SchemaFingerprint, truncatedEntry.SchemaFingerprint);
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
    public void RelationshipResolutionChangesCoverageWithoutSplittingOpeningSchema()
    {
        const string hostHandle = "ABC123";
        const string missingHandle = "DEAD99";
        var wall = Wall(hostHandle, Payload("host-wall", false), Array.Empty<CadCustomHandleReference>());
        var resolved = Opening(
            "200",
            OpeningPayload("PRIVATE_OPENING_ALPHA"),
            new CadCustomHandleReference[] { new(330, hostHandle) });
        var unresolved = Opening(
            "201",
            OpeningPayload("PRIVATE_OPENING_BETA"),
            new CadCustomHandleReference[] { new(330, missingHandle) });

        var report = CadTianzhengSchemaCorpus.Build(Document("opening-coverage.dxf", wall, resolved, unresolved));
        var openingEntry = Assert.Single(report.Entries, entry => entry.DxfName == "TCH_OPENING");
        var json = CadTianzhengSchemaCorpus.ToJson(report);

        Assert.Equal(2, openingEntry.EntityCount);
        Assert.Equal("330x1", openingEntry.ReferenceCodeSignature);
        Assert.Equal(1, openingEntry.ResolvedRelationshipEntityCount);
        Assert.Equal(1, openingEntry.ResolvedRelationshipCount);
        Assert.Equal(1, openingEntry.OpeningHostWallEntityCount);
        Assert.Equal(1, openingEntry.OpeningHostWallRelationshipCount);
        Assert.DoesNotContain(hostHandle, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(missingHandle, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_OPENING_ALPHA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_OPENING_BETA", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeTracksSampleCoverageEntityCountsAndResolvedRelationships()
    {
        const string hostHandle = "700";
        var first = CadTianzhengSchemaCorpus.Build(Document(
            "sample-one.dxf",
            Wall(hostHandle, Payload("host", false), Array.Empty<CadCustomHandleReference>()),
            Opening("701", OpeningPayload("sample-one"), new CadCustomHandleReference[] { new(330, hostHandle) })));
        var second = CadTianzhengSchemaCorpus.Build(Document(
            "sample-two.dxf",
            Opening("801", OpeningPayload("sample-two"), new CadCustomHandleReference[] { new(330, "missing") })));

        var merged = CadTianzhengSchemaCorpus.Merge(new[] { first, second });
        var openingEntry = Assert.Single(merged.Entries, entry => entry.DxfName == "TCH_OPENING");

        Assert.Equal(2, merged.SampleCount);
        Assert.Equal(3, merged.EntityCount);
        Assert.Equal(2, openingEntry.EntityCount);
        Assert.Equal(2, openingEntry.SamplesContainingProfile);
        Assert.Equal(1, openingEntry.ResolvedRelationshipEntityCount);
        Assert.Equal(1, openingEntry.ResolvedRelationshipCount);
        Assert.Equal(1, openingEntry.OpeningHostWallEntityCount);
        Assert.Equal(1, openingEntry.OpeningHostWallRelationshipCount);
    }

    [Fact]
    public void MergeRejectsOlderCorpusSchema()
    {
        var older = new CadTianzhengSchemaCorpusReport(
            1,
            1,
            Array.Empty<CadTianzhengSchemaCorpusEntry>());

        var exception = Assert.Throws<ArgumentException>(() => CadTianzhengSchemaCorpus.Merge(new[] { older }));

        Assert.Contains("version: 1", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static CadCustomEntity Opening(
        string handle,
        CadDxfCustomPayload payload,
        IReadOnlyList<CadCustomHandleReference> references)
        => new(handle, "TCH_OPENING")
        {
            ClassDefinition = OpeningClass,
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

    private static CadDxfCustomPayload OpeningPayload(string privateValue)
        => new(
            new CadRawDxfGroup[]
            {
                new(100, "TDbOpening"),
                new(10, privateValue),
                new(20, "654321.987")
            });

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
