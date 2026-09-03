using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengModernDimensionResearchV0120Tests
{
    [Fact]
    public void AtomicModernDimensionBundleParsesAsResearchOnlyEvidence()
    {
        var bundle = CadTianzhengProbeExperimentBundleParser.Parse("""
            [TCHDIFF] Case=DIMENSION_PLOT_SCALE_MODERN
            [TCHDIFF] Object type=TCH_DIMENSION
            [TCHSIG] Object type=TCH_DIMENSION
            [TCHSIG] Entry count=4
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,47,40
            [TCHDIFF] changed slot=2 code=47 occurrence=1
            """);

        Assert.Equal(CadTianzhengProbeExperimentCases.ModernDimensionPlotScale, bundle.Observation.ExperimentCase.Id);
        Assert.Equal("TCH_DIMENSION", bundle.Signature.DxfName);
        Assert.False(bundle.Observation.ExperimentCase.CanClearReleaseGate);
        Assert.Single(bundle.Observation.Diff.ValueChanges);
    }

    [Fact]
    public void ResearchOnlyModernDimensionConsensusCannotBecomeSemanticReadyEvenWithRawMapping()
    {
        var first = CadTianzhengProbeExperimentBundleParser.Parse("""
            [TCHDIFF] Case=DIMENSION_PLOT_SCALE_MODERN
            [TCHDIFF] Object type=TCH_DIMENSION
            [TCHSIG] Object type=TCH_DIMENSION
            [TCHSIG] Entry count=4
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,47,40
            [TCHDIFF] changed slot=2 code=47 occurrence=1
            """);
        var second = CadTianzhengProbeExperimentBundleParser.Parse("""
            [TCHDIFF] Case=DIMENSION_PLOT_SCALE_MODERN
            [TCHDIFF] Object type=TCH_DIMENSION
            [TCHSIG] Object type=TCH_DIMENSION
            [TCHSIG] Entry count=4
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,47,40
            [TCHDIFF] changed slot=2 code=47 occurrence=1
            """);
        var consensus = CadTianzhengProbeExperimentParser.BuildConsensus(
            first.Signature,
            new[] { first.Observation, second.Observation });
        var claims = new[]
        {
            new CadTianzhengExternalEvidenceClaim(
                "RESEARCH-MODERN-DIMENSION-RAW-MAPPING",
                CadTianzhengProbeExperimentCases.ModernDimensionPlotScale,
                CadTianzhengExternalEvidenceStrength.RawFieldMapping,
                GroupCode: 47,
                CodeOccurrence: 1)
        };

        Assert.False(consensus.ExperimentCase.CanClearReleaseGate);
        Assert.Throws<ArgumentException>(() => CadTianzhengSemanticEvidenceAssessor.Assess(consensus, claims));
    }
}
