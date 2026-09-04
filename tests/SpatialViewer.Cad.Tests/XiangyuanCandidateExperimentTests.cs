using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanCandidateExperimentTests
{
    private const string DxfName = "VENDOR_PRIVATE_PARCEL";
    private const string CppClassName = "VendorPrivateParcel";
    private const string ApplicationName = "VendorPrivateApp";
    private static readonly CadCustomClassDefinition CandidateClass = new(
        DxfName,
        CppClassName,
        ApplicationName,
        1101,
        1,
        true,
        "EraseAllowed",
        true);

    [Fact]
    public void CandidateExtractionKeepsOnlyRemovedUnknownEntityClasses()
    {
        var candidate = Candidate();
        var retained = candidate with
        {
            PresentInConverted = true,
            ConvertedDeclaredInstanceCount = 1,
            Status = CadXiangyuanConversionDiffStatus.RetainedAfterConversion
        };
        var known = candidate with
        {
            DxfName = "TCH_WALL",
            CppClassName = "TDbWall",
            ApplicationName = "Tianzheng Architecture",
            ClassifiedVendor = CadCustomObjectVendor.Tianzheng
        };
        var nonEntity = candidate with { DxfName = "VENDOR_OBJECT", IsEntity = false };
        var report = new CadXiangyuanConversionDiffReport(
            CadXiangyuanConversionDiffer.CurrentSchemaVersion,
            1,
            1,
            new[] { candidate, retained, known, nonEntity },
            Array.Empty<CadXiangyuanConversionProfileDelta>());

        var extracted = CadXiangyuanCandidateExperimentAnalyzer.GetUnknownRemovedEntityCandidates(report);

        var item = Assert.Single(extracted);
        Assert.Equal(DxfName, item.DxfName);
        Assert.Equal(CadCustomObjectVendor.Unknown, item.ClassifiedVendor);
    }

    [Fact]
    public void DxfConsensusAllowsUnknownConversionCandidateWithoutPromotingVendor()
    {
        const string privateBefore = "PRIVATE_FAR_1";
        const string privateAfter = "PRIVATE_FAR_2";
        var candidate = Candidate();
        var first = CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(
            candidate,
            DxfEntity("100", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new(40, privateBefore), new(70, "0"))),
            DxfEntity("101", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new(40, privateAfter), new(70, "1"))));
        var second = CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(
            candidate,
            DxfEntity("200", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new(40, "PRIVATE_3"), new(70, "0"))),
            DxfEntity("201", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new(40, "PRIVATE_4"), new(70, "0"))));
        var observations = new List<CadDxfCustomExperimentObservation> { first, second };

        var consensus = CadXiangyuanCandidateExperimentAnalyzer.BuildDxfConsensus(candidate, observations);
        var json = JsonSerializer.Serialize(consensus);

        Assert.True(consensus.HasStableCandidate);
        var stable = Assert.Single(consensus.StableValueChanges);
        Assert.Equal(1, stable.GroupIndex);
        Assert.Equal(40, stable.Code);
        Assert.Equal(1, stable.CodeOccurrence);
        Assert.Equal(CadCustomObjectVendor.Unknown, CadCustomObjectClassifier.Classify(DxfName, CppClassName, ApplicationName));
        Assert.False(DxfEntity("300", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"))).IsXiangyuan);
        Assert.DoesNotContain(privateBefore, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateAfter, json, StringComparison.Ordinal);
    }

    [Fact]
    public void CandidateGateRejectsRetainedOrKnownVendorClasses()
    {
        var retained = Candidate() with
        {
            PresentInConverted = true,
            ConvertedDeclaredInstanceCount = 1,
            Status = CadXiangyuanConversionDiffStatus.RetainedAfterConversion
        };
        var spoofedKnown = Candidate() with
        {
            DxfName = "TCH_WALL",
            CppClassName = "TDbWall",
            ApplicationName = "Tianzheng Architecture",
            ClassifiedVendor = CadCustomObjectVendor.Unknown
        };
        var before = DxfEntity("400", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new(40, "1")));
        var after = DxfEntity("401", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new(40, "2")));

        Assert.Throws<ArgumentException>(() => CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(retained, before, after));
        Assert.Throws<ArgumentException>(() => CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(spoofedKnown, before, after));
    }

    [Fact]
    public void CandidateGateRejectsEntityIdentityMismatch()
    {
        var candidate = Candidate();
        var before = DxfEntity("500", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new(40, "1")));
        var otherClass = new CadCustomClassDefinition(
            "OTHER_PRIVATE_CLASS",
            "OtherPrivateClass",
            ApplicationName,
            1102,
            1,
            true,
            "None",
            true);
        var after = DxfEntity("501", Payload(new CadRawDxfGroup(100, "OtherPrivateClass"), new(40, "2")), otherClass);

        var exception = Assert.Throws<ArgumentException>(() => CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(candidate, before, after));

        Assert.Contains("identity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepeatedConversionCandidateCanDriveSamePrivacySafeDxfExperiment()
    {
        var repeated = new CadXiangyuanConversionClassConsensus(
            DxfName,
            CppClassName,
            ApplicationName,
            CadCustomObjectVendor.Unknown,
            true,
            true,
            "EraseAllowed",
            2,
            2,
            0,
            0);
        var first = CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(
            repeated,
            DxfEntity("550", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new CadRawDxfGroup(40, "1"))),
            DxfEntity("551", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new CadRawDxfGroup(40, "2"))));
        var second = CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(
            repeated,
            DxfEntity("552", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new CadRawDxfGroup(40, "3"))),
            DxfEntity("553", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new CadRawDxfGroup(40, "4"))));
        var observations = new List<CadDxfCustomExperimentObservation> { first, second };

        var consensus = CadXiangyuanCandidateExperimentAnalyzer.BuildDxfConsensus(repeated, observations);

        Assert.True(consensus.HasStableCandidate);
        var stable = Assert.Single(consensus.StableValueChanges);
        Assert.Equal(40, stable.Code);
        Assert.Equal(CadCustomObjectVendor.Unknown, CadCustomObjectClassifier.Classify(DxfName, CppClassName, ApplicationName));
    }

    [Fact]
    public void RepeatedCandidateGateRejectsContradictoryConversionEvidence()
    {
        var contradictory = new CadXiangyuanConversionClassConsensus(
            DxfName,
            CppClassName,
            ApplicationName,
            CadCustomObjectVendor.Unknown,
            true,
            true,
            "EraseAllowed",
            2,
            1,
            1,
            0);
        var before = DxfEntity("560", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new CadRawDxfGroup(40, "1")));
        var after = DxfEntity("561", Payload(new CadRawDxfGroup(100, "VendorPrivateParcel"), new CadRawDxfGroup(40, "2")));

        Assert.Throws<ArgumentException>(() => CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(contradictory, before, after));
    }

    [Fact]
    public void DwgConsensusUsesSameCandidateProvenanceGate()
    {
        var candidate = Candidate();
        var first = CadXiangyuanCandidateExperimentAnalyzer.ObserveDwg(
            candidate,
            DwgEntity("600", Bytes(10)),
            DwgEntity("601", Bytes(10, 2, 3, 8)));
        var second = CadXiangyuanCandidateExperimentAnalyzer.ObserveDwg(
            candidate,
            DwgEntity("700", Bytes(10)),
            DwgEntity("701", Bytes(10, 3, 4, 8)));
        var observations = new List<CadDwgCustomExperimentObservation> { first, second };

        var consensus = CadXiangyuanCandidateExperimentAnalyzer.BuildDwgConsensus(candidate, observations);

        Assert.True(consensus.HasStableCandidate);
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

    private static CadXiangyuanConversionClassDelta Candidate()
        => new(
            DxfName,
            CppClassName,
            ApplicationName,
            CadCustomObjectVendor.Unknown,
            true,
            true,
            "EraseAllowed",
            true,
            false,
            1,
            0,
            CadXiangyuanConversionDiffStatus.RemovedAfterConversion);

    private static CadDxfCustomPayload Payload(params CadRawDxfGroup[] groups)
        => new(groups);

    private static CadCustomEntity DxfEntity(
        string handle,
        CadDxfCustomPayload payload,
        CadCustomClassDefinition? definition = null)
        => new(handle, (definition ?? CandidateClass).DxfName)
        {
            ClassDefinition = definition ?? CandidateClass,
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload)
        };

    private static CadCustomEntity DwgEntity(string handle, byte[] bytes)
        => new(handle, DxfName)
        {
            ClassDefinition = CandidateClass,
            RawDwgObjectRecord = new CadDwgCustomObjectRecord(bytes, 999999, false, "AcDbObjectsHandleMap")
        };

    private static byte[] Bytes(int count, params int[] changedOffsets)
    {
        var bytes = new byte[count];
        foreach (var offset in changedOffsets) bytes[offset] = 1;
        return bytes;
    }
}
