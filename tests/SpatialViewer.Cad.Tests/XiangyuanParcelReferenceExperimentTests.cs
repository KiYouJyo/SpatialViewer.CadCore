using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanParcelReferenceExperimentTests
{
    private static readonly CadCustomClassDefinition XiangyuanClass = new(
        "XY_PARCEL_REFERENCE",
        "XiangyuanParcelReference",
        "LzxSoft Control Planning CAD",
        1601,
        1,
        true,
        "None",
        true);

    private static readonly CadCustomClassDefinition CandidateClass = new(
        "PRIVATE_PARCEL_REFERENCE",
        "PrivateParcelReference",
        "PrivatePlanningApp",
        1602,
        1,
        true,
        "EraseAllowed",
        true);

    [Fact]
    public void ExplicitRelationshipConsensusRetainsAnonymousReferenceSlotOnly()
    {
        var relationship = RelationshipCase();
        var first = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitReference(
            relationship,
            XiangyuanEntity("100", "PRIVATE_TARGET_A", "UNCHANGED_A"),
            XiangyuanEntity("101", "PRIVATE_TARGET_B", "UNCHANGED_A"));
        var second = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitReference(
            relationship,
            XiangyuanEntity("200", "PRIVATE_TARGET_C", "UNCHANGED_B"),
            XiangyuanEntity("201", "PRIVATE_TARGET_D", "UNCHANGED_B"));

        var consensus = CadXiangyuanParcelExperimentAnalyzer.BuildExplicitReferenceConsensus(
            new[] { first, second });

        Assert.Equal(CadXiangyuanParcelExperimentCases.ControlIndicatorRelationship, consensus.ExperimentCase.Id);
        Assert.Equal(CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, consensus.Provenance);
        Assert.True(consensus.HasStableCandidate);
        var stable = Assert.Single(consensus.StructuralConsensus.StableValueChanges);
        Assert.Equal(330, stable.GroupCode);
        Assert.Equal(1, stable.CodeOccurrence);
    }

    [Fact]
    public void RepeatedUnknownCandidateCanResearchRelationshipWithoutVendorPromotion()
    {
        var relationship = RelationshipCase();
        var candidate = RepeatedCandidate();
        var first = CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateReference(
            relationship,
            candidate,
            CandidateEntity("300", "A", "SAME_1"),
            CandidateEntity("301", "B", "SAME_1"));
        var second = CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateReference(
            relationship,
            candidate,
            CandidateEntity("400", "C", "SAME_2"),
            CandidateEntity("401", "D", "SAME_2"));

        var consensus = CadXiangyuanParcelExperimentAnalyzer.BuildCandidateReferenceConsensus(
            candidate,
            new[] { first, second });

        Assert.True(consensus.HasStableCandidate);
        Assert.Equal(CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate, consensus.Provenance);
        Assert.Equal(
            CadCustomObjectVendor.Unknown,
            CadCustomObjectClassifier.Classify(
                CandidateClass.DxfName,
                CandidateClass.CppClassName,
                CandidateClass.ApplicationName));
    }

    [Fact]
    public void ReferenceAnalyzerRejectsNonRelationshipParcelCases()
    {
        foreach (var caseId in new[]
        {
            CadXiangyuanParcelExperimentCases.FarMax,
            CadXiangyuanParcelExperimentCases.Area,
            CadXiangyuanParcelExperimentCases.Boundary
        })
        {
            var experimentCase = CadXiangyuanParcelExperimentCases.Resolve(caseId);
            Assert.Throws<ArgumentException>(() => CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitReference(
                experimentCase,
                XiangyuanEntity("500", "A", "C"),
                XiangyuanEntity("501", "B", "C")));
        }
    }

    [Fact]
    public void CandidateReferenceGateRejectsContradictoryConversionEvidence()
    {
        var contradictory = RepeatedCandidate() with
        {
            RemovedPairCount = 1,
            RetainedPairCount = 1
        };

        Assert.Throws<ArgumentException>(() => CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateReference(
            RelationshipCase(),
            contradictory,
            CandidateEntity("600", "A", "C"),
            CandidateEntity("601", "B", "C")));
    }

    [Fact]
    public void ReferenceConsensusRejectsMixedProvenance()
    {
        var relationship = RelationshipCase();
        var explicitObservation = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitReference(
            relationship,
            XiangyuanEntity("700", "A", "C"),
            XiangyuanEntity("701", "B", "C"));
        var candidateObservation = CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateReference(
            relationship,
            RepeatedCandidate(),
            CandidateEntity("710", "A", "C"),
            CandidateEntity("711", "B", "C"));

        var exception = Assert.Throws<ArgumentException>(() =>
            CadXiangyuanParcelExperimentAnalyzer.BuildExplicitReferenceConsensus(
                new[] { explicitObservation, candidateObservation }));

        Assert.Contains("provenance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CadXiangyuanParcelExperimentCase RelationshipCase()
        => CadXiangyuanParcelExperimentCases.Resolve(
            CadXiangyuanParcelExperimentCases.ControlIndicatorRelationship);

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

    private static CadCustomEntity XiangyuanEntity(
        string handle,
        string changedTarget,
        string unchangedTarget)
        => Entity(handle, XiangyuanClass, changedTarget, unchangedTarget);

    private static CadCustomEntity CandidateEntity(
        string handle,
        string changedTarget,
        string unchangedTarget)
        => Entity(handle, CandidateClass, changedTarget, unchangedTarget);

    private static CadCustomEntity Entity(
        string handle,
        CadCustomClassDefinition definition,
        string changedTarget,
        string unchangedTarget)
        => new(handle, definition.DxfName)
        {
            ClassDefinition = definition,
            HandleReferences = new[]
            {
                new CadCustomHandleReference(330, changedTarget),
                new CadCustomHandleReference(340, unchangedTarget)
            }
        };
}
