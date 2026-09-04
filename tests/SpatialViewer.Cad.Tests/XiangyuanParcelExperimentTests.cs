using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanParcelExperimentTests
{
    private static readonly CadCustomClassDefinition ExplicitXiangyuanClass = new(
        "XY_PARCEL_SYNTHETIC",
        "XiangyuanParcelSynthetic",
        "LzxSoft Control Planning CAD",
        1201,
        1,
        true,
        "None",
        false);

    private static readonly CadCustomClassDefinition CandidateClass = new(
        "VENDOR_PRIVATE_PARCEL",
        "VendorPrivateParcel",
        "VendorPrivateApp",
        1202,
        1,
        true,
        "EraseAllowed",
        true);

    [Fact]
    public void CatalogSeparatesRawValueCasesFromGeometryRelationshipCases()
    {
        var all = CadXiangyuanParcelExperimentCases.All;
        var raw = CadXiangyuanParcelExperimentCases.RawPayloadValueCases;

        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.ParcelNumber);
        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.FarMin);
        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.FarMax);
        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.BuildingDensityMin);
        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.BuildingDensityMax);
        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.GreenRateMin);
        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.GreenRateMax);
        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.HeightMin);
        Assert.Contains(all, item => item.Id == CadXiangyuanParcelExperimentCases.HeightMax);
        Assert.DoesNotContain(raw, item => item.Id == CadXiangyuanParcelExperimentCases.Area);
        Assert.DoesNotContain(raw, item => item.Id == CadXiangyuanParcelExperimentCases.Boundary);
        Assert.DoesNotContain(raw, item => item.Id == CadXiangyuanParcelExperimentCases.ControlIndicatorRelationship);
    }

    [Fact]
    public void ExplicitXiangyuanConsensusKeepsOneDeclaredParcelIntent()
    {
        var experimentCase = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.FarMax);
        var first = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitDxf(
            experimentCase,
            ExplicitEntity("100", "2.0", "0"),
            ExplicitEntity("101", "2.5", "1"));
        var second = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitDxf(
            experimentCase,
            ExplicitEntity("200", "3.0", "0"),
            ExplicitEntity("201", "3.5", "0"));

        var consensus = CadXiangyuanParcelExperimentAnalyzer.BuildExplicitDxfConsensus(new[] { first, second });

        Assert.Equal(CadXiangyuanParcelExperimentCases.FarMax, consensus.ExperimentCase.Id);
        Assert.Equal(CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, consensus.Provenance);
        Assert.True(consensus.HasStableCandidate);
        var stable = Assert.Single(consensus.StructuralConsensus.StableValueChanges);
        Assert.Equal(40, stable.Code);
        Assert.Equal(1, stable.CodeOccurrence);
    }

    [Fact]
    public void RepeatedUnknownCandidateCanCollectCaseBoundParcelEvidenceWithoutVendorPromotion()
    {
        var experimentCase = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.GreenRateMax);
        var candidate = RepeatedCandidate();
        var first = CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateDxf(
            experimentCase,
            candidate,
            CandidateEntity("300", "30", "0"),
            CandidateEntity("301", "35", "1"));
        var second = CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateDxf(
            experimentCase,
            candidate,
            CandidateEntity("400", "40", "0"),
            CandidateEntity("401", "45", "0"));

        var consensus = CadXiangyuanParcelExperimentAnalyzer.BuildCandidateDxfConsensus(
            candidate,
            new[] { first, second });

        Assert.Equal(CadXiangyuanParcelExperimentCases.GreenRateMax, consensus.ExperimentCase.Id);
        Assert.Equal(CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate, consensus.Provenance);
        Assert.True(consensus.HasStableCandidate);
        Assert.Equal(
            CadCustomObjectVendor.Unknown,
            CadCustomObjectClassifier.Classify(
                CandidateClass.DxfName,
                CandidateClass.CppClassName,
                CandidateClass.ApplicationName));
        Assert.False(CandidateEntity("999", "1", "0").IsXiangyuan);
    }

    [Fact]
    public void ConsensusRejectsMixingFarAndGreenRateObservations()
    {
        var farCase = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.FarMax);
        var greenCase = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.GreenRateMax);
        var first = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitDxf(
            farCase,
            ExplicitEntity("500", "2.0", "0"),
            ExplicitEntity("501", "2.5", "0"));
        var second = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitDxf(
            greenCase,
            ExplicitEntity("600", "30", "0"),
            ExplicitEntity("601", "35", "0"));

        var exception = Assert.Throws<ArgumentException>(() =>
            CadXiangyuanParcelExperimentAnalyzer.BuildExplicitDxfConsensus(new[] { first, second }));

        Assert.Contains("different", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeometryRelationshipCasesCannotBeMisusedAsRawValueConsensus()
    {
        var boundary = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.Boundary);

        var exception = Assert.Throws<ArgumentException>(() =>
            CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitDxf(
                boundary,
                ExplicitEntity("700", "1", "0"),
                ExplicitEntity("701", "2", "0")));

        Assert.Contains("Geometry", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpoofedCaseMetadataAndMixedProvenanceAreRejected()
    {
        var spoofed = new CadXiangyuanParcelExperimentCase(
            CadXiangyuanParcelExperimentCases.FarMax,
            "actually green rate",
            CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue);
        Assert.Throws<ArgumentException>(() =>
            CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitDxf(
                spoofed,
                ExplicitEntity("800", "1", "0"),
                ExplicitEntity("801", "2", "0")));

        var canonical = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.FarMax);
        var explicitObservation = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitDxf(
            canonical,
            ExplicitEntity("810", "1", "0"),
            ExplicitEntity("811", "2", "0"));
        var candidateObservation = CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateDxf(
            canonical,
            RepeatedCandidate(),
            CandidateEntity("820", "1", "0"),
            CandidateEntity("821", "2", "0"));

        var exception = Assert.Throws<ArgumentException>(() =>
            CadXiangyuanParcelExperimentAnalyzer.BuildExplicitDxfConsensus(
                new[] { explicitObservation, candidateObservation }));

        Assert.Contains("provenance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveIsCaseInsensitiveButReturnsCanonicalRecord()
    {
        var resolved = CadXiangyuanParcelExperimentCases.Resolve("  far_max  ");

        Assert.Equal(CadXiangyuanParcelExperimentCases.FarMax, resolved.Id);
        Assert.Equal(CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue, resolved.EvidenceKind);
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

    private static CadCustomEntity ExplicitEntity(string handle, string primaryValue, string unrelatedValue)
        => Entity(handle, ExplicitXiangyuanClass, primaryValue, unrelatedValue);

    private static CadCustomEntity CandidateEntity(string handle, string primaryValue, string unrelatedValue)
        => Entity(handle, CandidateClass, primaryValue, unrelatedValue);

    private static CadCustomEntity Entity(
        string handle,
        CadCustomClassDefinition definition,
        string primaryValue,
        string unrelatedValue)
    {
        var payload = new CadDxfCustomPayload(
            new CadRawDxfGroup[]
            {
                new(100, definition.CppClassName),
                new(40, primaryValue),
                new(70, unrelatedValue)
            });
        return new CadCustomEntity(handle, definition.DxfName)
        {
            ClassDefinition = definition,
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload)
        };
    }
}
