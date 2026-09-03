using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengGateEvidenceV0120Tests
{
    [Fact]
    public void ParameterExistenceEvidenceCannotNameAStableRawField()
    {
        var consensus = AxisConsensus();
        var claims = new[]
        {
            new CadTianzhengExternalEvidenceClaim(
                "PUBLIC-AXIS-PROPERTY-DOC",
                CadTianzhengProbeExperimentCases.AxisLabelText,
                CadTianzhengExternalEvidenceStrength.ParameterExistence)
        };

        var result = CadTianzhengSemanticEvidenceAssessor.Assess(consensus, claims);

        Assert.True(result.HasParameterExistenceEvidence);
        Assert.False(result.HasMatchingRawFieldEvidence);
        Assert.False(result.ReadyForSemanticImplementation);
        Assert.Null(result.ReadyCandidate);
    }

    [Fact]
    public void MatchingRawFieldEvidencePromotesExactlyOneConsensusCandidateToImplementationReady()
    {
        var consensus = AxisConsensus();
        var claims = new[]
        {
            new CadTianzhengExternalEvidenceClaim(
                "PUBLIC-AXIS-RAW-MAPPING",
                CadTianzhengProbeExperimentCases.AxisLabelText,
                CadTianzhengExternalEvidenceStrength.RawFieldMapping,
                GroupCode: 40,
                CodeOccurrence: 1)
        };

        var result = CadTianzhengSemanticEvidenceAssessor.Assess(consensus, claims);
        var ready = Assert.IsType<CadDxfCustomPayloadValueChange>(result.ReadyCandidate);

        Assert.True(result.HasParameterExistenceEvidence);
        Assert.True(result.HasMatchingRawFieldEvidence);
        Assert.True(result.ReadyForSemanticImplementation);
        Assert.Equal(2, ready.GroupIndex);
        Assert.Equal(40, ready.Code);
        Assert.Equal(1, ready.CodeOccurrence);
    }

    [Fact]
    public void RawFieldEvidenceThatDoesNotMatchRepeatableProbeCandidateCannotClearGate()
    {
        var consensus = AxisConsensus();
        var claims = new[]
        {
            new CadTianzhengExternalEvidenceClaim(
                "PUBLIC-AXIS-OTHER-GROUP",
                CadTianzhengProbeExperimentCases.AxisLabelText,
                CadTianzhengExternalEvidenceStrength.RawFieldMapping,
                GroupCode: 41,
                CodeOccurrence: 1)
        };

        var result = CadTianzhengSemanticEvidenceAssessor.Assess(consensus, claims);

        Assert.True(result.HasParameterExistenceEvidence);
        Assert.False(result.HasMatchingRawFieldEvidence);
        Assert.False(result.ReadyForSemanticImplementation);
    }

    [Fact]
    public void ConflictingPublicRawMappingsFailClosedInsteadOfChoosingOne()
    {
        var consensus = AxisConsensus();
        var claims = new[]
        {
            new CadTianzhengExternalEvidenceClaim(
                "PUBLIC-MAPPING-A",
                CadTianzhengProbeExperimentCases.AxisLabelText,
                CadTianzhengExternalEvidenceStrength.RawFieldMapping,
                40,
                1),
            new CadTianzhengExternalEvidenceClaim(
                "PUBLIC-MAPPING-B",
                CadTianzhengProbeExperimentCases.AxisLabelText,
                CadTianzhengExternalEvidenceStrength.RawFieldMapping,
                41,
                1)
        };

        Assert.Throws<ArgumentException>(() => CadTianzhengSemanticEvidenceAssessor.Assess(consensus, claims));
    }

    [Fact]
    public void ParameterExistenceClaimCannotSmuggleAGroupMapping()
    {
        var consensus = AxisConsensus();
        var claims = new[]
        {
            new CadTianzhengExternalEvidenceClaim(
                "PUBLIC-PROPERTY-ONLY",
                CadTianzhengProbeExperimentCases.AxisLabelText,
                CadTianzhengExternalEvidenceStrength.ParameterExistence,
                40,
                1)
        };

        Assert.Throws<ArgumentException>(() => CadTianzhengSemanticEvidenceAssessor.Assess(consensus, claims));
    }

    [Fact]
    public void EvidenceFromAnotherGateCannotBeReusedForAxisConsensus()
    {
        var consensus = AxisConsensus();
        var claims = new[]
        {
            new CadTianzhengExternalEvidenceClaim(
                "PUBLIC-DIMENSION-EVIDENCE",
                CadTianzhengProbeExperimentCases.DimensionPlotScale,
                CadTianzhengExternalEvidenceStrength.ParameterExistence)
        };

        Assert.Throws<ArgumentException>(() => CadTianzhengSemanticEvidenceAssessor.Assess(consensus, claims));
    }

    [Fact]
    public void DuplicateCitationKeyCannotPretendToBeIndependentEvidence()
    {
        var consensus = AxisConsensus();
        var claims = new[]
        {
            new CadTianzhengExternalEvidenceClaim(
                "SAME-SOURCE",
                CadTianzhengProbeExperimentCases.AxisLabelText,
                CadTianzhengExternalEvidenceStrength.ParameterExistence),
            new CadTianzhengExternalEvidenceClaim(
                "SAME-SOURCE",
                CadTianzhengProbeExperimentCases.AxisLabelText,
                CadTianzhengExternalEvidenceStrength.RawFieldMapping,
                40,
                1)
        };

        Assert.Throws<ArgumentException>(() => CadTianzhengSemanticEvidenceAssessor.Assess(consensus, claims));
    }

    [Fact]
    public void FabricatedConsensusCaseIsRejectedBeforeExternalEvidenceIsConsidered()
    {
        var canonicalConsensus = AxisConsensus();
        var fakeCase = new CadTianzhengProbeExperimentCase(
            CadTianzhengProbeExperimentCases.AxisLabelText,
            "TCH_AXIS_LABEL",
            "different intent");
        var fabricated = new CadTianzhengProbeExperimentConsensus(fakeCase, canonicalConsensus.StructuralConsensus);

        Assert.Throws<ArgumentException>(() =>
            CadTianzhengSemanticEvidenceAssessor.Assess(fabricated, Array.Empty<CadTianzhengExternalEvidenceClaim>()));
    }

    private static CadTianzhengProbeExperimentConsensus AxisConsensus()
    {
        var signature = CadTianzhengProbeOutputParser.ParseSignature("""
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=5
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40,41,40
            """);
        var first = CadTianzhengProbeExperimentParser.Parse("""
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            [TCHDIFF] changed slot=3 code=41 occurrence=1
            """);
        var second = CadTianzhengProbeExperimentParser.Parse("""
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            [TCHDIFF] changed slot=4 code=40 occurrence=2
            """);

        return CadTianzhengProbeExperimentParser.BuildConsensus(signature, new[] { first, second });
    }
}
