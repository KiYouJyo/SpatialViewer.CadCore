using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengProbeExperimentCasesV0120Tests
{
    [Fact]
    public void CanonicalGateCasesRemainNarrowAndBoundToOneObjectIdentity()
    {
        var cases = CadTianzhengProbeExperimentCases.All.ToDictionary(item => item.Id, StringComparer.Ordinal);

        Assert.Equal(4, cases.Count);
        Assert.Equal("TCH_AXIS_LABEL", cases[CadTianzhengProbeExperimentCases.AxisLabelText].DxfName);
        Assert.Equal("TCH_DRAWINGINDEX", cases[CadTianzhengProbeExperimentCases.DrawingIndexText].DxfName);
        Assert.Equal("TCH_INDEXPOINTER", cases[CadTianzhengProbeExperimentCases.IndexPointerText].DxfName);
        Assert.Equal("TCH_DIMENSION2", cases[CadTianzhengProbeExperimentCases.DimensionPlotScale].DxfName);
    }

    [Fact]
    public void ParserBindsDeclaredCaseWithoutRetainingRawConsoleNoise()
    {
        const string secret = "PROJECT-SECRET-AXIS-A17";
        var observation = CadTianzhengProbeExperimentParser.Parse($"""
            diagnostic noise {secret}
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """);
        var json = JsonSerializer.Serialize(observation);

        Assert.Equal(CadTianzhengProbeExperimentCases.AxisLabelText, observation.ExperimentCase.Id);
        Assert.Equal("TCH_AXIS_LABEL", observation.Diff.DxfName);
        Assert.Single(observation.Diff.ValueChanges);
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("UNKNOWN_CASE", "TCH_AXIS_LABEL")]
    [InlineData("AXIS_LABEL_TEXT", "TCH_DRAWINGINDEX")]
    [InlineData("DIMENSION_PLOT_SCALE", "TCH_AXIS_LABEL")]
    [InlineData("DIMENSION_PLOT_SCALE", "TCH_DIMENSION")]
    public void ParserFailsClosedForUnknownOrWrongObjectCase(string caseId, string dxfName)
    {
        var text = $"""
            [TCHDIFF] Case={caseId}
            [TCHDIFF] Object type={dxfName}
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """;

        Assert.Throws<FormatException>(() => CadTianzhengProbeExperimentParser.Parse(text));
    }

    [Fact]
    public void ParserRejectsMissingOrDuplicateCaseTags()
    {
        const string untagged = """
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """;
        const string duplicate = """
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """;

        Assert.Throws<FormatException>(() => CadTianzhengProbeExperimentParser.Parse(untagged));
        Assert.Throws<FormatException>(() => CadTianzhengProbeExperimentParser.Parse(duplicate));
    }

    [Fact]
    public void CaseBoundConsensusKeepsOnlyStableSlotsAcrossIndependentPairs()
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

        var consensus = CadTianzhengProbeExperimentParser.BuildConsensus(signature, new[] { first, second });
        var stable = Assert.Single(consensus.StructuralConsensus.StableValueChanges);

        Assert.Equal(CadTianzhengProbeExperimentCases.AxisLabelText, consensus.ExperimentCase.Id);
        Assert.Equal(2, consensus.StructuralConsensus.ObservationCount);
        Assert.Equal(2, stable.GroupIndex);
        Assert.Equal(40, stable.Code);
        Assert.Equal(1, stable.CodeOccurrence);
        Assert.True(consensus.HasStableCandidate);
    }

    [Fact]
    public void ConsensusRejectsMixedExperimentIntentEvenWhenRawStructureLooksCompatible()
    {
        var signature = CadTianzhengProbeOutputParser.ParseSignature("""
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=3
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40
            """);
        var diff = CadTianzhengProbeOutputParser.ParseDiff("""
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """);
        var axisCase = CadTianzhengProbeExperimentCases.Resolve(CadTianzhengProbeExperimentCases.AxisLabelText);
        var wrongIntent = new CadTianzhengProbeExperimentCase("OTHER_AXIS_PROPERTY", "TCH_AXIS_LABEL", "different controlled property");
        var observations = new[]
        {
            new CadTianzhengProbeExperimentObservation(axisCase, diff),
            new CadTianzhengProbeExperimentObservation(wrongIntent, diff)
        };

        Assert.Throws<ArgumentException>(() => CadTianzhengProbeExperimentParser.BuildConsensus(signature, observations));
    }

    [Fact]
    public void ConsensusRejectsFabricatedNonCanonicalCaseEvenWhenEveryObservationUsesIt()
    {
        var signature = CadTianzhengProbeOutputParser.ParseSignature("""
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=3
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40
            """);
        var diff = CadTianzhengProbeOutputParser.ParseDiff("""
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """);
        var fabricated = new CadTianzhengProbeExperimentCase(
            "FAKE_AXIS_CASE",
            "TCH_AXIS_LABEL",
            "fabricated test intent");
        var observation = new CadTianzhengProbeExperimentObservation(fabricated, diff);

        Assert.Throws<ArgumentException>(() =>
            CadTianzhengProbeExperimentParser.BuildConsensus(signature, new[] { observation, observation }));
    }

    [Fact]
    public void ConsensusRequiresCaseObjectTypeToMatchSignature()
    {
        var signature = CadTianzhengProbeOutputParser.ParseSignature("""
            [TCHSIG] Object type=TCH_DRAWINGINDEX
            [TCHSIG] Entry count=3
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40
            """);
        var observation = CadTianzhengProbeExperimentParser.Parse("""
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """);

        Assert.Throws<ArgumentException>(() =>
            CadTianzhengProbeExperimentParser.BuildConsensus(signature, new[] { observation, observation }));
    }
}
