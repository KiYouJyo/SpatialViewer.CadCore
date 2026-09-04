using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanConversionConsensusTests
{
    private const string DxfName = "VENDOR_PRIVATE_PARCEL";
    private const string CppClassName = "VendorPrivateParcel";
    private const string ApplicationName = "VendorPrivateApp";
    private static readonly IReadOnlyList<CadXiangyuanConversionProfileDelta> NoProfiles = Array.Empty<CadXiangyuanConversionProfileDelta>();

    [Fact]
    public void RepeatedRemovalProducesUnknownEntityCandidateWithoutVendorPromotion()
    {
        var first = Report(RemovedUnknown());
        var second = Report(RemovedUnknown());

        var consensus = CadXiangyuanConversionConsensus.Build(new[] { first, second });

        Assert.Equal(2, consensus.PairCount);
        Assert.Equal(1, consensus.RepeatedRemovedUnknownEntityCandidateCount);
        var item = Assert.Single(consensus.Classes);
        Assert.Equal(2, item.ObservedPairCount);
        Assert.Equal(2, item.RemovedPairCount);
        Assert.Equal(0, item.RetainedPairCount);
        Assert.True(item.IsRepeatedRemovedUnknownEntityCandidate);
        Assert.Equal(CadCustomObjectVendor.Unknown, CadCustomObjectClassifier.Classify(DxfName, CppClassName, ApplicationName));

        var extracted = CadXiangyuanConversionConsensus.GetRepeatedRemovedUnknownEntityCandidates(consensus);
        Assert.Single(extracted);
    }

    [Fact]
    public void OneRetainedObservationPreventsStableRemovalCandidate()
    {
        var first = Report(RemovedUnknown());
        var retained = RemovedUnknown() with
        {
            PresentInConverted = true,
            ConvertedDeclaredInstanceCount = 1,
            Status = CadXiangyuanConversionDiffStatus.RetainedAfterConversion
        };
        var second = Report(retained);

        var consensus = CadXiangyuanConversionConsensus.Build(new[] { first, second });
        var item = Assert.Single(consensus.Classes);

        Assert.Equal(1, item.RemovedPairCount);
        Assert.Equal(1, item.RetainedPairCount);
        Assert.False(item.IsRepeatedRemovedUnknownEntityCandidate);
        Assert.Empty(CadXiangyuanConversionConsensus.GetRepeatedRemovedUnknownEntityCandidates(consensus));
    }

    [Fact]
    public void KnownVendorRemovalNeverBecomesUnknownCandidate()
    {
        var known = new CadXiangyuanConversionClassDelta(
            "TCH_WALL",
            "TDbWall",
            "Tianzheng Architecture",
            CadCustomObjectVendor.Tianzheng,
            true,
            false,
            "None",
            true,
            false,
            1,
            0,
            CadXiangyuanConversionDiffStatus.RemovedAfterConversion);

        var consensus = CadXiangyuanConversionConsensus.Build(new[] { Report(known), Report(known) });
        var item = Assert.Single(consensus.Classes);

        Assert.Equal(2, item.RemovedPairCount);
        Assert.False(item.IsRepeatedRemovedUnknownEntityCandidate);
        Assert.Equal(0, consensus.RepeatedRemovedUnknownEntityCandidateCount);
    }

    [Fact]
    public void RepeatedProfileRemovalIsTrackedSeparatelyFromClassIdentity()
    {
        var profile = RemovedProfile();
        var first = Report(RemovedUnknown(), profile);
        var second = Report(RemovedUnknown(), profile);

        var consensus = CadXiangyuanConversionConsensus.Build(new[] { first, second });

        Assert.Equal(1, consensus.RepeatedRemovedUnknownProfileCandidateCount);
        var item = Assert.Single(consensus.Profiles);
        Assert.Equal(2, item.RemovedPairCount);
        Assert.True(item.IsRepeatedRemovedUnknownProfileCandidate);
    }

    [Fact]
    public void JsonRoundTripPreservesOnlyAggregatedStructuralEvidence()
    {
        var consensus = CadXiangyuanConversionConsensus.Build(new[]
        {
            Report(RemovedUnknown(), RemovedProfile()),
            Report(RemovedUnknown(), RemovedProfile())
        });

        var json = CadXiangyuanConversionConsensus.ToJson(consensus);
        var roundTrip = CadXiangyuanConversionConsensus.FromJson(json);

        Assert.Equal(consensus.SchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(consensus.PairCount, roundTrip.PairCount);
        Assert.Equal(consensus.Classes.ToArray(), roundTrip.Classes.ToArray());
        Assert.Equal(consensus.Profiles.ToArray(), roundTrip.Profiles.ToArray());
        Assert.DoesNotContain("native", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("converted", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsensusRejectsSinglePairAndSpoofedVendorMetadata()
    {
        Assert.Throws<ArgumentException>(() => CadXiangyuanConversionConsensus.Build(new[] { Report(RemovedUnknown()) }));

        var spoofed = new CadXiangyuanConversionConsensusReport(
            CadXiangyuanConversionConsensus.CurrentSchemaVersion,
            2,
            new[]
            {
                new CadXiangyuanConversionClassConsensus(
                    "TCH_WALL",
                    "TDbWall",
                    "Tianzheng Architecture",
                    CadCustomObjectVendor.Unknown,
                    true,
                    false,
                    "None",
                    2,
                    2,
                    0,
                    0)
            },
            Array.Empty<CadXiangyuanConversionProfileConsensus>());

        var exception = Assert.Throws<ArgumentException>(() => CadXiangyuanConversionConsensus.ToJson(spoofed));
        Assert.Contains("classifier", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CadXiangyuanConversionClassDelta RemovedUnknown()
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

    private static CadXiangyuanConversionProfileDelta RemovedProfile()
        => new(
            DxfName,
            CppClassName,
            ApplicationName,
            CadCustomObjectVendor.Unknown,
            "schema-fingerprint",
            "100,10,20,40",
            "AcDbEntity>VendorPrivateParcel",
            "330x1",
            "Polyline,Text2",
            1,
            0,
            CadXiangyuanConversionDiffStatus.RemovedAfterConversion);

    private static CadXiangyuanConversionDiffReport Report(
        CadXiangyuanConversionClassDelta item,
        CadXiangyuanConversionProfileDelta? profile = null)
        => new(
            CadXiangyuanConversionDiffer.CurrentSchemaVersion,
            1,
            1,
            new[] { item },
            profile is null ? NoProfiles : new[] { profile });
}
