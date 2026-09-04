using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class CadCustomHandleReferenceDiffTests
{
    private static readonly CadCustomClassDefinition CustomClass = new(
        "PRIVATE_REFERENCE_OBJECT",
        "PrivateReferenceObject",
        "PrivateApplication",
        1501,
        1,
        true,
        "None",
        true);

    [Fact]
    public void ComparableReferenceDiffDoesNotExposeTargetHandles()
    {
        const string beforeTarget = "SECRET_TARGET_ABC";
        const string afterTarget = "SECRET_TARGET_XYZ";
        var before = Entity("SECRET_SOURCE_1", new(330, beforeTarget), new(340, "UNCHANGED_TARGET"));
        var after = Entity("SECRET_SOURCE_2", new(330, afterTarget), new(340, "UNCHANGED_TARGET"));

        var report = CadCustomHandleReferenceDiffer.Compare(before, after);
        var json = JsonSerializer.Serialize(report);

        Assert.Equal(CadCustomHandleReferenceDiffStatus.Comparable, report.Status);
        Assert.Equal("330#1,340#1", report.BeforeLayoutSignature);
        Assert.Equal(report.BeforeLayoutSignature, report.AfterLayoutSignature);
        var change = Assert.Single(report.ValueChanges);
        Assert.Equal(330, change.GroupCode);
        Assert.Equal(1, change.CodeOccurrence);
        Assert.DoesNotContain(beforeTarget, json, StringComparison.Ordinal);
        Assert.DoesNotContain(afterTarget, json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_SOURCE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("UNCHANGED_TARGET", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SameCodeOccurrencesRemainDistinctWithoutHandles()
    {
        var before = Entity("100", new(330, "A"), new(330, "B"));
        var after = Entity("101", new(330, "A"), new(330, "C"));

        var report = CadCustomHandleReferenceDiffer.Compare(before, after);

        Assert.Equal("330#1,330#2", report.BeforeLayoutSignature);
        var change = Assert.Single(report.ValueChanges);
        Assert.Equal(330, change.GroupCode);
        Assert.Equal(2, change.CodeOccurrence);
    }

    [Fact]
    public void ReferenceCodeLayoutChangeFailsClosed()
    {
        var before = Entity("200", new(330, "A"), new(340, "B"));
        var after = Entity("201", new(330, "A"), new(350, "B"));

        var report = CadCustomHandleReferenceDiffer.Compare(before, after);

        Assert.Equal(CadCustomHandleReferenceDiffStatus.LayoutMismatch, report.Status);
        Assert.Empty(report.ValueChanges);
    }

    [Fact]
    public void MissingReferencesCannotProduceReferenceEvidence()
    {
        var before = Entity("300");
        var after = Entity("301", new(330, "A"));

        var report = CadCustomHandleReferenceDiffer.Compare(before, after);

        Assert.Equal(CadCustomHandleReferenceDiffStatus.MissingReferenceEvidence, report.Status);
        Assert.Empty(report.ValueChanges);
    }

    [Fact]
    public void ConsensusKeepsOnlyRepeatableReferenceSlot()
    {
        var first = CadCustomHandleReferenceExperimentAnalyzer.Observe(
            Entity("400", new(330, "A"), new(340, "B")),
            Entity("401", new(330, "X"), new(340, "Y")));
        var second = CadCustomHandleReferenceExperimentAnalyzer.Observe(
            Entity("500", new(330, "C"), new(340, "D")),
            Entity("501", new(330, "Z"), new(340, "D")));

        var consensus = CadCustomHandleReferenceExperimentAnalyzer.BuildConsensus(new[] { first, second });

        Assert.True(consensus.HasStableCandidate);
        var stable = Assert.Single(consensus.StableValueChanges);
        Assert.Equal(330, stable.GroupCode);
        Assert.Equal(1, stable.CodeOccurrence);
    }

    [Fact]
    public void DifferentCustomObjectIdentityIsRejectedBeforeHandleComparison()
    {
        var before = Entity("600", new CadCustomHandleReference(330, "A"));
        var other = CustomClass with { DxfName = "OTHER_REFERENCE_OBJECT" };
        var after = new CadCustomEntity("601", other.DxfName)
        {
            ClassDefinition = other,
            HandleReferences = new[] { new CadCustomHandleReference(330, "B") }
        };

        Assert.Throws<ArgumentException>(() => CadCustomHandleReferenceDiffer.Compare(before, after));
    }

    private static CadCustomEntity Entity(string handle, params CadCustomHandleReference[] references)
        => new(handle, CustomClass.DxfName)
        {
            ClassDefinition = CustomClass,
            HandleReferences = references
        };
}
