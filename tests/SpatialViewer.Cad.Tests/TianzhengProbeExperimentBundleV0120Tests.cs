using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengProbeExperimentBundleV0120Tests
{
    [Fact]
    public void AtomicBundleBindsCaseSignatureAndDiffWithoutRetainingConsoleSecrets()
    {
        const string secret = "PROJECT-AXIS-A17";
        var bundle = CadTianzhengProbeExperimentBundleParser.Parse($"""
            diagnostic noise {secret}
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=5
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40,41,40
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """);
        var json = JsonSerializer.Serialize(bundle);

        Assert.Equal(CadTianzhengProbeExperimentCases.AxisLabelText, bundle.Observation.ExperimentCase.Id);
        Assert.Equal("TCH_AXIS_LABEL", bundle.Signature.DxfName);
        Assert.Single(bundle.Observation.Diff.ValueChanges);
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
    }

    [Fact]
    public void BundleRejectsSignatureFromAnotherObjectEvenWhenDiffCaseIsValid()
    {
        const string text = """
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHSIG] Object type=TCH_DRAWINGINDEX
            [TCHSIG] Entry count=3
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """;

        Assert.Throws<FormatException>(() => CadTianzhengProbeExperimentBundleParser.Parse(text));
    }

    [Fact]
    public void BundleRejectsChangedSlotWhoseCodeDoesNotMatchItsOwnSignature()
    {
        const string text = """
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=4
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,41,40
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            """;

        Assert.Throws<FormatException>(() => CadTianzhengProbeExperimentBundleParser.Parse(text));
    }

    [Fact]
    public void BundleRejectsChangedSlotWithWrongRepeatedGroupOccurrence()
    {
        const string text = """
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=5
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40,41,40
            [TCHDIFF] changed slot=4 code=40 occurrence=1
            """;

        Assert.Throws<FormatException>(() => CadTianzhengProbeExperimentBundleParser.Parse(text));
    }

    [Fact]
    public void BundleRejectsMissingCaseOrIncompleteSignature()
    {
        const string missingCase = """
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=3
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40
            """;
        const string incompleteSignature = """
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=3
            [TCHSIG] Subclass marker count=1
            """;

        Assert.Throws<FormatException>(() => CadTianzhengProbeExperimentBundleParser.Parse(missingCase));
        Assert.Throws<FormatException>(() => CadTianzhengProbeExperimentBundleParser.Parse(incompleteSignature));
    }

    [Fact]
    public void TwoAtomicBundlesCanFeedExistingCaseBoundConsensusDirectly()
    {
        var first = CadTianzhengProbeExperimentBundleParser.Parse("""
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=5
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40,41,40
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            [TCHDIFF] changed slot=3 code=41 occurrence=1
            """);
        var second = CadTianzhengProbeExperimentBundleParser.Parse("""
            [TCHDIFF] Case=AXIS_LABEL_TEXT
            [TCHDIFF] Object type=TCH_AXIS_LABEL
            [TCHSIG] Object type=TCH_AXIS_LABEL
            [TCHSIG] Entry count=5
            [TCHSIG] Subclass marker count=1
            [TCHSIG] code-signature=0,100,40,41,40
            [TCHDIFF] changed slot=2 code=40 occurrence=1
            [TCHDIFF] changed slot=4 code=40 occurrence=2
            """);

        Assert.Equal(first.Signature, second.Signature);
        var consensus = CadTianzhengProbeExperimentParser.BuildConsensus(
            first.Signature,
            new[] { first.Observation, second.Observation });
        var stable = Assert.Single(consensus.StructuralConsensus.StableValueChanges);

        Assert.Equal(2, stable.GroupIndex);
        Assert.Equal(40, stable.Code);
        Assert.Equal(1, stable.CodeOccurrence);
    }
}
