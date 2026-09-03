using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengProbeOutputV0120Tests
{
    [Fact]
    public void SignatureParserRetainsOnlyStructuralProtocolFields()
    {
        const string secret = "PRIVATE-PROJECT-VALUE-12345";
        var text = string.Join('\n',
            "Command: TCHSIG",
            "[TCHSIG] Object type=TCH_AXIS_LABEL",
            "[TCHSIG] Entry count=7",
            "[TCHSIG] Subclass marker count=2",
            "[UNTRUSTED] " + secret,
            "[TCHSIG] code-signature=0,100,8,100,10,20,1");

        var signature = CadTianzhengProbeOutputParser.ParseSignature(text);
        var json = JsonSerializer.Serialize(signature);

        Assert.Equal("TCH_AXIS_LABEL", signature.DxfName);
        Assert.Equal(7, signature.EntryCount);
        Assert.Equal(2, signature.SubclassMarkerCount);
        Assert.Equal(7, signature.GroupCodes.Count);
        Assert.Equal(0, signature.GroupCodes[0]);
        Assert.Equal(1, signature.GroupCodes[6]);
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        Assert.DoesNotContain("UNTRUSTED", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffParserReadsOnlyChangedSlotIdentity()
    {
        var text = string.Join('\n',
            "[TCHDIFF] Object type=TCH_RECTSTAIR",
            "[TCHDIFF] changed slot=4 code=40 occurrence=1",
            "[TCHDIFF] changed slot=8 code=40 occurrence=2",
            "[TCHDIFF] Changed candidate count=2");

        var diff = CadTianzhengProbeOutputParser.ParseDiff(text);

        Assert.Equal("TCH_RECTSTAIR", diff.DxfName);
        Assert.Equal(2, diff.ValueChanges.Count);
        Assert.Equal(new CadDxfCustomPayloadValueChange(4, 40, 1), diff.ValueChanges[0]);
        Assert.Equal(new CadDxfCustomPayloadValueChange(8, 40, 2), diff.ValueChanges[1]);
    }

    [Fact]
    public void ConsensusKeepsOnlySlotsRepeatedAcrossIndependentExperiments()
    {
        var signature = new CadTianzhengProbeSignature(
            "TCH_DIMENSION2",
            9,
            2,
            new[] { 0, 100, 8, 100, 40, 70, 40, 47, 90 });
        var first = new CadTianzhengProbeDiff(
            "TCH_DIMENSION2",
            new CadDxfCustomPayloadValueChange[]
            {
                new(4, 40, 1),
                new(7, 47, 1)
            });
        var second = new CadTianzhengProbeDiff(
            "tch_dimension2",
            new CadDxfCustomPayloadValueChange[]
            {
                new(6, 40, 2),
                new(7, 47, 1)
            });

        var consensus = CadTianzhengProbeOutputParser.BuildConsensus(signature, new[] { first, second });

        Assert.Equal(2, consensus.ObservationCount);
        var stable = Assert.Single(consensus.StableValueChanges);
        Assert.Equal(new CadDxfCustomPayloadValueChange(7, 47, 1), stable);
        Assert.True(consensus.HasStableCandidate);
    }

    [Theory]
    [InlineData("TCH_AXIS_LABEL", 4, 41, 1)]
    [InlineData("TCH_AXIS_LABEL", 9, 40, 1)]
    [InlineData("TCH_AXIS_LABEL", 4, 40, 2)]
    [InlineData("TCH_DRAWINGINDEX", 4, 40, 1)]
    public void ConsensusRejectsDiffThatDoesNotMatchSignature(
        string dxfName,
        int slot,
        int code,
        int occurrence)
    {
        var signature = new CadTianzhengProbeSignature(
            "TCH_AXIS_LABEL",
            6,
            2,
            new[] { 0, 100, 8, 100, 40, 1 });
        var invalid = new CadTianzhengProbeDiff(
            dxfName,
            new[] { new CadDxfCustomPayloadValueChange(slot, code, occurrence) });
        var valid = new CadTianzhengProbeDiff(
            "TCH_AXIS_LABEL",
            new[] { new CadDxfCustomPayloadValueChange(4, 40, 1) });

        Assert.Throws<ArgumentException>(() =>
            CadTianzhengProbeOutputParser.BuildConsensus(signature, new[] { invalid, valid }));
    }

    [Fact]
    public void ParsersFailClosedForMalformedOrNonTianzhengProtocol()
    {
        Assert.Throws<FormatException>(() => CadTianzhengProbeOutputParser.ParseSignature(
            "[TCHSIG] Object type=VENDOR_AXIS\n[TCHSIG] Entry count=1\n[TCHSIG] Subclass marker count=0\n[TCHSIG] code-signature=0"));
        Assert.Throws<FormatException>(() => CadTianzhengProbeOutputParser.ParseSignature(
            "[TCHSIG] Object type=TCH_AXIS_LABEL\n[TCHSIG] Entry count=2\n[TCHSIG] Subclass marker count=0\n[TCHSIG] code-signature=0"));
        Assert.Throws<FormatException>(() => CadTianzhengProbeOutputParser.ParseDiff(
            "[TCHDIFF] Object type=TCH_AXIS_LABEL\n[TCHDIFF] changed slot=4 code=40 occurrence=0"));
        Assert.Throws<FormatException>(() => CadTianzhengProbeOutputParser.ParseDiff(
            "[TCHDIFF] changed slot=4 code=40 occurrence=1"));
    }

    [Fact]
    public void ConsensusRequiresAtLeastTwoIndependentObservations()
    {
        var signature = new CadTianzhengProbeSignature(
            "TCH_INDEXPOINTER",
            5,
            1,
            new[] { 0, 100, 8, 1, 40 });
        var observation = new CadTianzhengProbeDiff(
            "TCH_INDEXPOINTER",
            new[] { new CadDxfCustomPayloadValueChange(4, 40, 1) });

        Assert.Throws<ArgumentException>(() =>
            CadTianzhengProbeOutputParser.BuildConsensus(signature, new[] { observation }));
    }
}
