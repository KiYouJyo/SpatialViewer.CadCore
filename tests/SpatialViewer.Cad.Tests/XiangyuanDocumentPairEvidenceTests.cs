using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanDocumentPairEvidenceTests
{
    private static readonly CadCustomClassDefinition XiangyuanClass = new(
        "XY_DOCUMENT_OBJECT",
        "XiangyuanDocumentObject",
        "LzxSoft Control Planning CAD",
        1901,
        1,
        true,
        "None",
        true);

    private static readonly CadCustomClassDefinition OtherXiangyuanClass = new(
        "XY_OTHER_OBJECT",
        "XiangyuanOtherObject",
        "LzxSoft Control Planning CAD",
        1902,
        1,
        true,
        "None",
        true);

    private static readonly CadCustomClassDefinition CandidateClass = new(
        "PRIVATE_DOCUMENT_OBJECT",
        "PrivateDocumentObject",
        "PrivatePlanningApp",
        1903,
        1,
        true,
        "EraseAllowed",
        true);

    [Fact]
    public void ExplicitWholeDocumentPairCollectsAllPrivacySafeEvidenceChannels()
    {
        const string sourceHandle = "SECRET_SOURCE_HANDLE";
        const string beforeRaw = "PRIVATE_FAR_BEFORE";
        const string afterRaw = "PRIVATE_FAR_AFTER";
        const string beforeTarget = "SECRET_TARGET_BEFORE";
        const string afterTarget = "SECRET_TARGET_AFTER";
        var before = EvidenceEntity(
            sourceHandle, XiangyuanClass, beforeRaw, beforeTarget, 123456.75, new byte[] { 1, 2, 3, 4 });
        var after = EvidenceEntity(
            sourceHandle, XiangyuanClass, afterRaw, afterTarget, 123457.75, new byte[] { 1, 9, 3, 4 });

        var report = CadXiangyuanDocumentPairEvidenceAnalyzer.AnalyzeExplicit(
            Document("PRIVATE_BEFORE_DRAWING.dwg", before),
            Document("PRIVATE_AFTER_DRAWING.dwg", after));
        var json = CadXiangyuanDocumentPairEvidenceAnalyzer.ToJson(report);

        Assert.Equal(1, report.BeforeEligibleEntityCount);
        Assert.Equal(1, report.AfterEligibleEntityCount);
        Assert.Equal(1, report.MatchedEntityCount);
        Assert.Equal(0, report.IdentityMismatchCount);
        Assert.Equal(1, report.DxfComparablePairCount);
        Assert.Equal(1, report.DxfChangedPairCount);
        Assert.Equal(1, report.DwgComparablePairCount);
        Assert.Equal(1, report.DwgChangedPairCount);
        Assert.Equal(1, report.GeometryComparablePairCount);
        Assert.Equal(1, report.GeometryChangedPairCount);
        Assert.Equal(1, report.ReferenceComparablePairCount);
        Assert.Equal(1, report.ReferenceChangedPairCount);
        Assert.Single(report.DxfChanges);
        Assert.Single(report.DwgChanges);
        Assert.Single(report.GeometryChanges);
        Assert.Single(report.ReferenceChanges);
        Assert.DoesNotContain(sourceHandle, json, StringComparison.Ordinal);
        Assert.DoesNotContain(beforeRaw, json, StringComparison.Ordinal);
        Assert.DoesNotContain(afterRaw, json, StringComparison.Ordinal);
        Assert.DoesNotContain(beforeTarget, json, StringComparison.Ordinal);
        Assert.DoesNotContain(afterTarget, json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_BEFORE_DRAWING", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_AFTER_DRAWING", json, StringComparison.Ordinal);
        Assert.DoesNotContain("123456.75", json, StringComparison.Ordinal);
        Assert.DoesNotContain("123457.75", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentHandlesAreNeverMatchedByGeometryOrContentSimilarity()
    {
        var before = EvidenceEntity(
            "100", XiangyuanClass, "same", "10", 5, new byte[] { 1, 2 });
        var after = EvidenceEntity(
            "200", XiangyuanClass, "same", "10", 5, new byte[] { 1, 2 });

        var report = CadXiangyuanDocumentPairEvidenceAnalyzer.AnalyzeExplicit(
            Document("before.dwg", before),
            Document("after.dwg", after));

        Assert.Equal(0, report.MatchedEntityCount);
        Assert.Equal(1, report.BeforeOnlyEntityCount);
        Assert.Equal(1, report.AfterOnlyEntityCount);
        Assert.Equal(0, report.IdentityMismatchCount);
        Assert.Empty(report.DxfChanges);
        Assert.Empty(report.GeometryChanges);
    }

    [Fact]
    public void SameHandleWithDifferentClassIdentityFailsClosedAsMismatch()
    {
        var before = EvidenceEntity(
            "300", XiangyuanClass, "1", "10", 0, new byte[] { 1, 2 });
        var after = EvidenceEntity(
            "300", OtherXiangyuanClass, "2", "20", 1, new byte[] { 1, 3 });

        var report = CadXiangyuanDocumentPairEvidenceAnalyzer.AnalyzeExplicit(
            Document("before.dwg", before),
            Document("after.dwg", after));

        Assert.Equal(0, report.MatchedEntityCount);
        Assert.Equal(1, report.IdentityMismatchCount);
        Assert.Equal(0, report.BeforeOnlyEntityCount);
        Assert.Empty(report.DxfChanges);
        Assert.Empty(report.DwgChanges);
        Assert.Empty(report.GeometryChanges);
        Assert.Empty(report.ReferenceChanges);
    }

    [Fact]
    public void RepeatedUnknownCandidateMatchesOnlyExactCandidateIdentity()
    {
        var candidate = RepeatedCandidate();
        var beforeCandidate = EvidenceEntity(
            "400", CandidateClass, "1", "40", 0, new byte[] { 1, 2 });
        var afterCandidate = EvidenceEntity(
            "400", CandidateClass, "2", "41", 1, new byte[] { 1, 3 });
        var unrelatedBefore = EvidenceEntity(
            "401", XiangyuanClass, "3", "42", 2, new byte[] { 1, 4 });
        var unrelatedAfter = EvidenceEntity(
            "401", XiangyuanClass, "4", "43", 3, new byte[] { 1, 5 });

        var report = CadXiangyuanDocumentPairEvidenceAnalyzer.AnalyzeCandidate(
            candidate,
            Document("before.dwg", beforeCandidate, unrelatedBefore),
            Document("after.dwg", afterCandidate, unrelatedAfter));

        Assert.Equal(CadXiangyuanDocumentPairProvenance.RepeatedConversionCandidate, report.Provenance);
        Assert.Equal(1, report.BeforeEligibleEntityCount);
        Assert.Equal(1, report.AfterEligibleEntityCount);
        Assert.Equal(1, report.MatchedEntityCount);
        Assert.Equal(1, report.DxfChangedPairCount);
        Assert.Equal(
            CadCustomObjectVendor.Unknown,
            CadCustomObjectClassifier.Classify(
                CandidateClass.DxfName,
                CandidateClass.CppClassName,
                CandidateClass.ApplicationName));
    }

    [Fact]
    public void DuplicateStableCustomHandleIsRejectedAcrossModelAndBlockSpace()
    {
        var model = EvidenceEntity(
            "500", XiangyuanClass, "1", "10", 0, new byte[] { 1, 2 });
        var blockEntity = EvidenceEntity(
            "500", OtherXiangyuanClass, "2", "20", 1, new byte[] { 1, 3 });
        var before = Document(
            "before.dwg",
            new[] { model },
            new[] { new CadBlockDefinition("PRIVATE_BLOCK", new Point2D(0, 0), new CadEntity[] { blockEntity }) });
        var after = Document("after.dwg", model);

        var exception = Assert.Throws<ArgumentException>(() =>
            CadXiangyuanDocumentPairEvidenceAnalyzer.AnalyzeExplicit(before, after));

        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PaperAndBlockCustomEntitiesParticipateInExactHandleMatching()
    {
        var beforeBlock = EvidenceEntity(
            "600", XiangyuanClass, "1", "10", 0, new byte[] { 1, 2 });
        var afterBlock = EvidenceEntity(
            "600", XiangyuanClass, "2", "11", 1, new byte[] { 1, 3 });
        var beforePaper = EvidenceEntity(
            "700", XiangyuanClass, "3", "12", 2, new byte[] { 1, 4 });
        var afterPaper = EvidenceEntity(
            "700", XiangyuanClass, "4", "13", 3, new byte[] { 1, 5 });
        var before = Document(
            "before.dwg",
            Array.Empty<CadEntity>(),
            new[] { new CadBlockDefinition("B", new Point2D(0, 0), new CadEntity[] { beforeBlock }) },
            new[] { PaperLayout(beforePaper) });
        var after = Document(
            "after.dwg",
            Array.Empty<CadEntity>(),
            new[] { new CadBlockDefinition("B", new Point2D(0, 0), new CadEntity[] { afterBlock }) },
            new[] { PaperLayout(afterPaper) });

        var report = CadXiangyuanDocumentPairEvidenceAnalyzer.AnalyzeExplicit(before, after);

        Assert.Equal(2, report.MatchedEntityCount);
        Assert.Equal(2, report.DxfChangedPairCount);
    }

    [Fact]
    public void JsonRoundTripPreservesOnlyFrozenPrivacySafeEvidence()
    {
        var before = EvidenceEntity(
            "800", XiangyuanClass, "PRIVATE_A", "80", 0, new byte[] { 1, 2 });
        var after = EvidenceEntity(
            "800", XiangyuanClass, "PRIVATE_B", "81", 1, new byte[] { 1, 3 });
        var report = CadXiangyuanDocumentPairEvidenceAnalyzer.AnalyzeExplicit(
            Document("before.dwg", before),
            Document("after.dwg", after));

        var json = CadXiangyuanDocumentPairEvidenceAnalyzer.ToJson(report);
        var roundTrip = CadXiangyuanDocumentPairEvidenceAnalyzer.FromJson(json);

        Assert.Equal(report.SchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(report.Provenance, roundTrip.Provenance);
        Assert.Equal(report.MatchedEntityCount, roundTrip.MatchedEntityCount);
        Assert.Equal(report.DxfChanges.ToArray(), roundTrip.DxfChanges.ToArray());
        Assert.Equal(report.DwgChanges.ToArray(), roundTrip.DwgChanges.ToArray());
        Assert.Equal(report.GeometryChanges.ToArray(), roundTrip.GeometryChanges.ToArray());
        Assert.Equal(report.ReferenceChanges.ToArray(), roundTrip.ReferenceChanges.ToArray());
        Assert.DoesNotContain("PRIVATE_A", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_B", json, StringComparison.Ordinal);
    }

    private static CadXiangyuanConversionClassConsensus RepeatedCandidate()
        => new(
            CandidateClass.DxfName,
            CandidateClass.CppClassName,
            CandidateClass.ApplicationName,
            CadCustomObjectVendor.Unknown,
            CandidateClass.IsEntity,
            CandidateClass.WasProxy,
            CandidateClass.ProxyFlags,
            2,
            2,
            0,
            0);

    private static CadCustomEntity EvidenceEntity(
        string handle,
        CadCustomClassDefinition definition,
        string rawValue,
        string targetHandle,
        double x,
        byte[] dwgBytes)
    {
        var payload = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, definition.CppClassName),
            new(40, rawValue)
        });
        return new CadCustomEntity(handle, definition.DxfName)
        {
            ClassDefinition = definition,
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload),
            RawDwgObjectRecord = new CadDwgCustomObjectRecord(
                dwgBytes,
                123456,
                false,
                "AcDbObjectsHandleMap"),
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline" },
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[]
                {
                    new Point2D(x, 0),
                    new Point2D(10, 0)
                })
            },
            HandleReferences = new[]
            {
                new CadCustomHandleReference(330, targetHandle)
            }
        };
    }

    private static CadLayoutDefinition PaperLayout(params CadEntity[] entities)
        => new(
            "Layout1",
            1,
            true,
            new Size2D(420, 297),
            new BoundingBox2D(0, 0, 420, 297),
            new BoundingBox2D(0, 0, 420, 297),
            entities,
            Array.Empty<CadViewportDefinition>());

    private static CadDocument Document(string name, params CadEntity[] modelSpace)
        => Document(name, modelSpace, Array.Empty<CadBlockDefinition>());

    private static CadDocument Document(
        string name,
        IReadOnlyList<CadEntity> modelSpace,
        IReadOnlyList<CadBlockDefinition> blocks,
        IReadOnlyList<CadLayoutDefinition>? layouts = null)
        => new(
            name,
            "DWG",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            blocks,
            modelSpace,
            layouts: layouts);
}
