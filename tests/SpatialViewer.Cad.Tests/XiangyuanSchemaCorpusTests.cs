using System.Text;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanSchemaCorpusTests
{
    private static readonly CadCustomClassDefinition XiangyuanClass = new(
        "XY_TEST_PARCEL",
        "XiangyuanParcelObject",
        "LzxSoft Control Planning CAD",
        701,
        1,
        true,
        "None",
        false);

    [Fact]
    public void BuildClustersSameSchemaWithoutExportingDrawingContents()
    {
        var firstPayload = Payload("PRIVATE_PARCEL_ALPHA", false);
        var secondPayload = Payload("PRIVATE_PARCEL_BETA", false);
        var first = Xiangyuan(
            "A100",
            firstPayload,
            new CadCustomHandleReference[] { new(330, "SECRET_TARGET_1") }) with
        {
            RawDwgObjectRecord = new CadDwgCustomObjectRecord(
                Encoding.UTF8.GetBytes("PRIVATE_DWG_BYTES"),
                45678,
                false,
                "test"),
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline", "Text2", "Polyline" }
        };
        var second = Xiangyuan(
            "A101",
            secondPayload,
            new CadCustomHandleReference[] { new(330, "SECRET_TARGET_2") }) with
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Text2", "Polyline" }
        };
        var tianzheng = new CadCustomEntity("T100", "TCH_WALL")
        {
            ClassDefinition = new CadCustomClassDefinition(
                "TCH_WALL", "TDbWall", "Tianzheng Architecture", 501, 1, true, "None", false),
            RawDxfPayload = Payload("TIANZHENG_PRIVATE", false)
        };
        var unrelated = new CadCustomEntity("U100", "VENDOR_PRIVATE")
        {
            ClassDefinition = new CadCustomClassDefinition(
                "VENDOR_PRIVATE", "PrivateObject", "Other Vendor", 900, 1, true, "None", false),
            RawDxfPayload = Payload("OTHER_PRIVATE", false)
        };

        var report = CadXiangyuanSchemaCorpus.Build(Document("private-xiangyuan-project.dxf", first, second, tianzheng, unrelated));
        var json = CadXiangyuanSchemaCorpus.ToJson(report);

        Assert.Equal(1, report.SchemaVersion);
        Assert.Equal(1, report.SampleCount);
        Assert.Equal(2, report.EntityCount);
        var entry = Assert.Single(report.Entries);
        Assert.Equal("XY_TEST_PARCEL", entry.DxfName);
        Assert.Equal("XiangyuanParcelObject", entry.CppClassName);
        Assert.Equal(2, entry.EntityCount);
        Assert.Equal(1, entry.SamplesContainingProfile);
        Assert.Equal(2, entry.RawDxfEvidenceEntityCount);
        Assert.Equal(0, entry.TruncatedRawDxfEntityCount);
        Assert.Equal(2, entry.ProxyGraphicsEntityCount);
        Assert.Equal(0, entry.OpaqueEntityCount);
        Assert.Equal(1, entry.RawDwgEvidenceEntityCount);
        Assert.Equal("330x1", entry.ReferenceCodeSignature);
        Assert.Equal("Polyline,Text2", entry.ProxyGraphicKindSignature);

        Assert.DoesNotContain("PRIVATE_PARCEL_ALPHA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_PARCEL_BETA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_TARGET", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-xiangyuan-project.dxf", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_DWG_BYTES", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TIANZHENG_PRIVATE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("OTHER_PRIVATE", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedPayloadAndProxyShapeCreateSeparateCompatibilityClusters()
    {
        var complete = Xiangyuan("100", Payload("same", false), Array.Empty<CadCustomHandleReference>()) with
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline" }
        };
        var truncated = Xiangyuan("101", Payload("same", true), Array.Empty<CadCustomHandleReference>()) with
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline" }
        };
        var differentProxy = Xiangyuan("102", Payload("same", false), Array.Empty<CadCustomHandleReference>()) with
        {
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline", "Text2" }
        };

        var report = CadXiangyuanSchemaCorpus.Build(Document("clusters.dxf", complete, truncated, differentProxy));

        Assert.Equal(3, report.Entries.Count);
        Assert.Contains(report.Entries, entry => entry.TruncatedRawDxfEntityCount == 1);
        Assert.Contains(report.Entries, entry => entry.ProxyGraphicKindSignature == "Polyline");
        Assert.Contains(report.Entries, entry => entry.ProxyGraphicKindSignature == "Polyline,Text2");
    }

    [Fact]
    public void GenericResolvedReferencesAreAggregatedWithoutExportingHandles()
    {
        var target = new CadLineEntity("200", Point2D.Origin, new Point2D(10, 0));
        var resolved = Xiangyuan(
            "201",
            Payload("resolved", false),
            new CadCustomHandleReference[] { new(330, "200") });
        var unresolved = Xiangyuan(
            "202",
            Payload("unresolved", false),
            new CadCustomHandleReference[] { new(330, "DEAD") });

        var report = CadXiangyuanSchemaCorpus.Build(Document("relationships.dxf", target, resolved, unresolved));
        var entry = Assert.Single(report.Entries);
        var json = CadXiangyuanSchemaCorpus.ToJson(report);

        Assert.Equal(2, entry.EntityCount);
        Assert.Equal(1, entry.ResolvedRelationshipEntityCount);
        Assert.Equal(1, entry.ResolvedRelationshipCount);
        Assert.DoesNotContain("DEAD", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(""200"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeTracksSampleCoverageAndRepresentationCounts()
    {
        var first = CadXiangyuanSchemaCorpus.Build(Document(
            "sample-one.dxf",
            Xiangyuan("301", Payload("one", false), Array.Empty<CadCustomHandleReference>()) with
            {
                Representation = CadCustomEntityRepresentation.ProxyGraphics,
                ProxyGraphicKinds = new[] { "Polyline" }
            }));
        var second = CadXiangyuanSchemaCorpus.Build(Document(
            "sample-two.dxf",
            Xiangyuan("401", Payload("two", false), Array.Empty<CadCustomHandleReference>()) with
            {
                Representation = CadCustomEntityRepresentation.ProxyGraphics,
                ProxyGraphicKinds = new[] { "Polyline" }
            }));

        var merged = CadXiangyuanSchemaCorpus.Merge(new[] { first, second });
        var entry = Assert.Single(merged.Entries);

        Assert.Equal(2, merged.SampleCount);
        Assert.Equal(2, merged.EntityCount);
        Assert.Equal(2, entry.SamplesContainingProfile);
        Assert.Equal(2, entry.ProxyGraphicsEntityCount);
        Assert.Equal(0, entry.OpaqueEntityCount);

        var roundTrip = CadXiangyuanSchemaCorpus.FromJson(CadXiangyuanSchemaCorpus.ToJson(merged));
        Assert.Equal(merged, roundTrip);
    }

    [Fact]
    public void MergeRejectsUnsupportedCorpusSchema()
    {
        var older = new CadXiangyuanSchemaCorpusReport(
            0,
            1,
            Array.Empty<CadXiangyuanSchemaCorpusEntry>());

        var exception = Assert.Throws<ArgumentException>(() => CadXiangyuanSchemaCorpus.Merge(new[] { older }));

        Assert.Contains("version: 0", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationRejectsInconsistentRepresentationCoverage()
    {
        var invalid = new CadXiangyuanSchemaCorpusReport(
            CadXiangyuanSchemaCorpus.CurrentSchemaVersion,
            1,
            new[]
            {
                new CadXiangyuanSchemaCorpusEntry(
                    "XY_TEST",
                    "XiangyuanObject",
                    "LzxSoft",
                    "fingerprint",
                    "0,5,100",
                    "AcDbEntity",
                    "",
                    "none",
                    2,
                    1,
                    0,
                    0,
                    0,
                    1,
                    0,
                    0,
                    0)
            });

        var exception = Assert.Throws<ArgumentException>(() => CadXiangyuanSchemaCorpus.ToJson(invalid));

        Assert.Contains("exactly equal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildIncludesModelBlockAndPaperSpaceXiangyuanEntities()
    {
        var payload = Payload("space-independent", false);
        var model = Xiangyuan("10", payload, Array.Empty<CadCustomHandleReference>());
        var blockEntity = Xiangyuan("20", payload, Array.Empty<CadCustomHandleReference>());
        var paper = Xiangyuan("30", payload, Array.Empty<CadCustomHandleReference>());
        var block = new CadBlockDefinition("XiangyuanBlock", Point2D.Origin, new CadEntity[] { blockEntity });
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

        var report = CadXiangyuanSchemaCorpus.Build(document);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(3, entry.EntityCount);
        Assert.Equal(3, entry.OpaqueEntityCount);
    }

    private static CadCustomEntity Xiangyuan(
        string handle,
        CadDxfCustomPayload payload,
        IReadOnlyList<CadCustomHandleReference> references)
        => new(handle, "XY_TEST_PARCEL")
        {
            ClassDefinition = XiangyuanClass,
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload),
            HandleReferences = references
        };

    private static CadDxfCustomPayload Payload(string privateValue, bool truncated)
        => new(
            new CadRawDxfGroup[]
            {
                new(100, "AcDbEntity"),
                new(100, "XiangyuanSyntheticObject"),
                new(10, privateValue),
                new(20, "987654.321"),
                new(40, "2.5")
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
