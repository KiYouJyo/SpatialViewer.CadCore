using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanExperimentAnalyzerTests
{
    private const string XiangyuanDxfName = "XY_SYNTHETIC_PARCEL";
    private const string XiangyuanCppClass = "XiangyuanSyntheticParcel";
    private const string XiangyuanApplication = "LzxSoft Control Planning CAD";

    [Fact]
    public void ObserveDxfRejectsNonXiangyuanEntitiesBeforeCandidateGeneration()
    {
        var before = DxfEntity(
            "100",
            Payload(new(100, "TDbColumn"), new(40, "400")),
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture");
        var after = DxfEntity(
            "101",
            Payload(new(100, "TDbColumn"), new(40, "500")),
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture");

        var exception = Assert.Throws<ArgumentException>(() => CadXiangyuanExperimentAnalyzer.ObserveDxf(before, after));

        Assert.Contains("explicit Xiangyuan", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DxfConsensusKeepsOnlyRepeatableXiangyuanSlotsWithoutRawValues()
    {
        const string privateBefore = "PRIVATE_FAR_BEFORE";
        const string privateAfter = "PRIVATE_FAR_AFTER";
        var first = CadXiangyuanExperimentAnalyzer.ObserveDxf(
            DxfEntity("200", Payload(new(100, "XiangyuanSyntheticParcel"), new(40, privateBefore), new(70, "0"))),
            DxfEntity("201", Payload(new(100, "XiangyuanSyntheticParcel"), new(40, privateAfter), new(70, "1"))));
        var second = CadXiangyuanExperimentAnalyzer.ObserveDxf(
            DxfEntity("300", Payload(new(100, "XiangyuanSyntheticParcel"), new(40, "PRIVATE_2A"), new(70, "0"))),
            DxfEntity("301", Payload(new(100, "XiangyuanSyntheticParcel"), new(40, "PRIVATE_2B"), new(70, "0"))));

        var observations = new List<CadDxfCustomExperimentObservation> { first, second };
        var consensus = CadXiangyuanExperimentAnalyzer.BuildDxfConsensus(observations);
        var json = JsonSerializer.Serialize(consensus);

        Assert.True(consensus.HasStableCandidate);
        Assert.Equal(2, consensus.ObservationCount);
        var stable = Assert.Single(consensus.StableValueChanges);
        Assert.Equal(1, stable.GroupIndex);
        Assert.Equal(40, stable.Code);
        Assert.Equal(1, stable.CodeOccurrence);
        Assert.Equal(XiangyuanDxfName, consensus.Identity.DxfName);
        Assert.DoesNotContain(privateBefore, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateAfter, json, StringComparison.Ordinal);
        Assert.DoesNotContain("200", json, StringComparison.Ordinal);
        Assert.DoesNotContain("201", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsensusRejectsObservationWhoseIdentityIsNotXiangyuan()
    {
        var xiangyuan = CadXiangyuanExperimentAnalyzer.ObserveDxf(
            DxfEntity("400", Payload(new(100, "XiangyuanSyntheticParcel"), new(40, "1"))),
            DxfEntity("401", Payload(new(100, "XiangyuanSyntheticParcel"), new(40, "2"))));
        var spoofed = xiangyuan with
        {
            Identity = new CadCustomExperimentIdentity("TCH_COLUMN", "TDbColumn", "Tianzheng Architecture")
        };

        var observations = new List<CadDxfCustomExperimentObservation> { xiangyuan, spoofed };

        Assert.Throws<ArgumentException>(() => CadXiangyuanExperimentAnalyzer.BuildDxfConsensus(observations));
    }

    [Fact]
    public void DwgConsensusUsesSameXiangyuanVendorGateAndRetainsOnlyRanges()
    {
        var first = CadXiangyuanExperimentAnalyzer.ObserveDwg(
            DwgEntity("500", Bytes(10), "AcDbObjectsHandleMap"),
            DwgEntity("501", Bytes(10, 2, 3, 8), "AcDbObjectsHandleMap"));
        var second = CadXiangyuanExperimentAnalyzer.ObserveDwg(
            DwgEntity("600", Bytes(10), "AcDbObjectsHandleMap"),
            DwgEntity("601", Bytes(10, 3, 4, 8), "AcDbObjectsHandleMap"));

        var observations = new List<CadDwgCustomExperimentObservation> { first, second };
        var consensus = CadXiangyuanExperimentAnalyzer.BuildDwgConsensus(observations);

        Assert.True(consensus.HasStableCandidate);
        Assert.Equal(2, consensus.ObservationCount);
        Assert.Collection(
            consensus.StableChangedRanges,
            range =>
            {
                Assert.Equal(3, range.Offset);
                Assert.Equal(1, range.Length);
            },
            range =>
            {
                Assert.Equal(8, range.Offset);
                Assert.Equal(1, range.Length);
            });
    }

    [Fact]
    public void ObserveDxfStillRejectsDifferentKnownXiangyuanClassIdentity()
    {
        var before = DxfEntity(
            "700",
            Payload(new(100, "XiangyuanSyntheticParcel"), new(40, "1")));
        var after = DxfEntity(
            "701",
            Payload(new(100, "XiangyuanOtherParcel"), new(40, "2")),
            XiangyuanDxfName,
            "XiangyuanOtherParcel",
            XiangyuanApplication);

        Assert.Throws<ArgumentException>(() => CadXiangyuanExperimentAnalyzer.ObserveDxf(before, after));
    }

    private static CadDxfCustomPayload Payload(params CadRawDxfGroup[] groups)
        => new(groups);

    private static CadCustomEntity DxfEntity(
        string handle,
        CadDxfCustomPayload payload,
        string dxfName = XiangyuanDxfName,
        string cppClassName = XiangyuanCppClass,
        string applicationName = XiangyuanApplication)
        => new(handle, dxfName)
        {
            ClassDefinition = Class(dxfName, cppClassName, applicationName),
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload)
        };

    private static CadCustomEntity DwgEntity(
        string handle,
        byte[] bytes,
        string captureMethod)
        => new(handle, XiangyuanDxfName)
        {
            ClassDefinition = Class(XiangyuanDxfName, XiangyuanCppClass, XiangyuanApplication),
            RawDwgObjectRecord = new CadDwgCustomObjectRecord(bytes, 987654321, false, captureMethod)
        };

    private static CadCustomClassDefinition Class(string dxfName, string cppClassName, string applicationName)
        => new(dxfName, cppClassName, applicationName, 800, 1, true, "None", false);

    private static byte[] Bytes(int count, params int[] changedOffsets)
    {
        var bytes = new byte[count];
        foreach (var offset in changedOffsets) bytes[offset] = 1;
        return bytes;
    }
}
