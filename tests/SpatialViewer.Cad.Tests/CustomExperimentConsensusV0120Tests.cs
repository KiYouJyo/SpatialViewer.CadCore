using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class CustomExperimentConsensusV0120Tests
{
    [Fact]
    public void DxfConsensusKeepsOnlySlotsChangedInEveryIndependentObservation()
    {
        var first = CadCustomExperimentAnalyzer.ObserveDxf(
            DxfEntity("100", Payload(new(100, "TDbColumn"), new(40, "400"), new(70, "0"), new(90, "1"))),
            DxfEntity("101", Payload(new(100, "TDbColumn"), new(40, "500"), new(70, "1"), new(90, "1"))));
        var second = CadCustomExperimentAnalyzer.ObserveDxf(
            DxfEntity("200", Payload(new(100, "TDbColumn"), new(40, "600"), new(70, "0"), new(90, "2"))),
            DxfEntity("201", Payload(new(100, "TDbColumn"), new(40, "700"), new(70, "0"), new(90, "3"))));

        var consensus = CadCustomExperimentAnalyzer.BuildDxfConsensus(new[] { first, second });

        Assert.Equal(2, consensus.ObservationCount);
        Assert.True(consensus.HasStableCandidate);
        var stable = Assert.Single(consensus.StableValueChanges);
        Assert.Equal(1, stable.GroupIndex);
        Assert.Equal(40, stable.Code);
        Assert.Equal(1, stable.CodeOccurrence);
        Assert.Equal(first.BeforeFingerprint, consensus.SchemaFingerprint);
    }

    [Fact]
    public void DxfConsensusRejectsSingleObservationMixedIdentityOrLayoutMismatch()
    {
        var comparable = CadCustomExperimentAnalyzer.ObserveDxf(
            DxfEntity("100", Payload(new(100, "TDbColumn"), new(40, "400"))),
            DxfEntity("101", Payload(new(100, "TDbColumn"), new(40, "500"))));
        var otherIdentity = CadCustomExperimentAnalyzer.ObserveDxf(
            DxfEntity("200", Payload(new(100, "TDbColumn"), new(40, "600")), "TCH_OTHER", "TDbOther"),
            DxfEntity("201", Payload(new(100, "TDbColumn"), new(40, "700")), "TCH_OTHER", "TDbOther"));
        var layoutMismatch = CadCustomExperimentAnalyzer.ObserveDxf(
            DxfEntity("300", Payload(new(100, "TDbColumn"), new(40, "800"))),
            DxfEntity("301", Payload(new(100, "TDbColumn"), new(41, "900"))));
        var weakIdentity = new CadDxfCustomExperimentObservation(
            new CadCustomExperimentIdentity("TCH_COLUMN", string.Empty, string.Empty),
            comparable.Status,
            comparable.BeforeFingerprint,
            comparable.AfterFingerprint,
            comparable.ValueChanges);

        Assert.Throws<ArgumentException>(() => CadCustomExperimentAnalyzer.BuildDxfConsensus(new[] { comparable }));
        Assert.Throws<ArgumentException>(() => CadCustomExperimentAnalyzer.BuildDxfConsensus(new[] { comparable, otherIdentity }));
        Assert.Throws<ArgumentException>(() => CadCustomExperimentAnalyzer.BuildDxfConsensus(new[] { comparable, layoutMismatch }));
        Assert.Throws<ArgumentException>(() => CadCustomExperimentAnalyzer.BuildDxfConsensus(new[] { comparable, weakIdentity }));
    }

    [Fact]
    public void DwgConsensusIntersectsChangedByteRangesWithoutHeuristicAlignment()
    {
        var first = CadCustomExperimentAnalyzer.ObserveDwg(
            DwgEntity("100", Bytes(10), "AcDbObjectsHandleMap"),
            DwgEntity("101", Bytes(10, 2, 3, 4, 8), "AcDbObjectsHandleMap"));
        var second = CadCustomExperimentAnalyzer.ObserveDwg(
            DwgEntity("200", Bytes(10), "AcDbObjectsHandleMap"),
            DwgEntity("201", Bytes(10, 3, 4, 5, 8), "AcDbObjectsHandleMap"));

        var consensus = CadCustomExperimentAnalyzer.BuildDwgConsensus(new[] { first, second });

        Assert.Equal(2, consensus.ObservationCount);
        Assert.True(consensus.HasStableCandidate);
        Assert.Collection(
            consensus.StableChangedRanges,
            range =>
            {
                Assert.Equal(3, range.Offset);
                Assert.Equal(2, range.Length);
            },
            range =>
            {
                Assert.Equal(8, range.Offset);
                Assert.Equal(1, range.Length);
            });
    }

    [Fact]
    public void DwgConsensusRejectsNonComparableCountOrCaptureMethod()
    {
        var comparable = CadCustomExperimentAnalyzer.ObserveDwg(
            DwgEntity("100", Bytes(8), "AcDbObjectsHandleMap"),
            DwgEntity("101", Bytes(8, 2), "AcDbObjectsHandleMap"));
        var differentCount = CadCustomExperimentAnalyzer.ObserveDwg(
            DwgEntity("200", Bytes(9), "AcDbObjectsHandleMap"),
            DwgEntity("201", Bytes(9, 2), "AcDbObjectsHandleMap"));
        var lengthMismatch = CadCustomExperimentAnalyzer.ObserveDwg(
            DwgEntity("300", Bytes(8), "AcDbObjectsHandleMap"),
            DwgEntity("301", Bytes(9), "AcDbObjectsHandleMap"));

        Assert.Throws<ArgumentException>(() => CadCustomExperimentAnalyzer.BuildDwgConsensus(new[] { comparable }));
        Assert.Throws<ArgumentException>(() => CadCustomExperimentAnalyzer.BuildDwgConsensus(new[] { comparable, differentCount }));
        Assert.Throws<ArgumentException>(() => CadCustomExperimentAnalyzer.BuildDwgConsensus(new[] { comparable, lengthMismatch }));

        var differentCapture = new CadDwgCustomExperimentObservation(
            comparable.Identity,
            "OtherCaptureMethod",
            CadDwgCustomObjectRecordDiffStatus.Comparable,
            comparable.ByteCount,
            comparable.ChangedRanges);
        Assert.Throws<ArgumentException>(() => CadCustomExperimentAnalyzer.BuildDwgConsensus(new[] { comparable, differentCapture }));
    }

    [Fact]
    public void ConsensusSerializationDoesNotExposeHandlesRawValuesOrDwgBytes()
    {
        const string privateRawBefore = "SECRET_COLUMN_WIDTH_BEFORE";
        const string privateRawAfter = "SECRET_COLUMN_WIDTH_AFTER";
        const string privateHandle = "SECRET_HANDLE";
        var first = CadCustomExperimentAnalyzer.ObserveDxf(
            DxfEntity(privateHandle, Payload(new(100, "TDbColumn"), new(40, privateRawBefore))),
            DxfEntity("OTHER_SECRET_HANDLE", Payload(new(100, "TDbColumn"), new(40, privateRawAfter))));
        var second = CadCustomExperimentAnalyzer.ObserveDxf(
            DxfEntity("300", Payload(new(100, "TDbColumn"), new(40, "ANOTHER_PRIVATE_VALUE"))),
            DxfEntity("301", Payload(new(100, "TDbColumn"), new(40, "YET_ANOTHER_PRIVATE_VALUE"))));
        var dxfConsensus = CadCustomExperimentAnalyzer.BuildDxfConsensus(new[] { first, second });

        var privateBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0, 0 };
        var dwgFirst = CadCustomExperimentAnalyzer.ObserveDwg(
            DwgEntity("400", privateBytes, "AcDbObjectsHandleMap"),
            DwgEntity("401", new byte[] { 0xDE, 0x11, 0xBE, 0xEF, 0, 0 }, "AcDbObjectsHandleMap"));
        var dwgSecond = CadCustomExperimentAnalyzer.ObserveDwg(
            DwgEntity("500", new byte[] { 1, 2, 3, 4, 5, 6 }, "AcDbObjectsHandleMap"),
            DwgEntity("501", new byte[] { 1, 9, 3, 4, 5, 6 }, "AcDbObjectsHandleMap"));
        var dwgConsensus = CadCustomExperimentAnalyzer.BuildDwgConsensus(new[] { dwgFirst, dwgSecond });

        var json = JsonSerializer.Serialize(new { dxfConsensus, dwgConsensus });

        Assert.DoesNotContain(privateRawBefore, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateRawAfter, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateHandle, json, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(privateBytes), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectSectionOffset", json, StringComparison.Ordinal);
    }

    private static CadDxfCustomPayload Payload(params CadRawDxfGroup[] groups)
        => new(groups);

    private static CadCustomEntity DxfEntity(
        string handle,
        CadDxfCustomPayload payload,
        string dxfName = "TCH_COLUMN",
        string cppClassName = "TDbColumn")
        => new(handle, dxfName)
        {
            ClassDefinition = Class(dxfName, cppClassName),
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload)
        };

    private static CadCustomEntity DwgEntity(
        string handle,
        byte[] bytes,
        string captureMethod)
        => new(handle, "TCH_COLUMN")
        {
            ClassDefinition = Class("TCH_COLUMN", "TDbColumn"),
            RawDwgObjectRecord = new CadDwgCustomObjectRecord(bytes, 123456789, false, captureMethod)
        };

    private static CadCustomClassDefinition Class(string dxfName, string cppClassName)
        => new(dxfName, cppClassName, "Tianzheng Architecture", 700, 1, true, "None", false);

    private static byte[] Bytes(int count, params int[] changedOffsets)
    {
        var bytes = new byte[count];
        foreach (var offset in changedOffsets) bytes[offset] = 1;
        return bytes;
    }
}
