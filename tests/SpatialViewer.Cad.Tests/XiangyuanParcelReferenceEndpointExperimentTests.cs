using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanParcelReferenceEndpointExperimentTests
{
    private static readonly CadCustomClassDefinition XiangyuanSourceClass = new(
        "XY_PARCEL_ENDPOINT",
        "XiangyuanParcelEndpoint",
        "LzxSoft Control Planning CAD",
        1801,
        1,
        true,
        "None",
        true);

    private static readonly CadCustomClassDefinition CandidateSourceClass = new(
        "PRIVATE_PARCEL_ENDPOINT",
        "PrivateParcelEndpoint",
        "PrivatePlanningApp",
        1802,
        1,
        true,
        "EraseAllowed",
        true);

    private static readonly CadCustomClassDefinition TargetClass = new(
        "PRIVATE_INDICATOR_TARGET",
        "PrivateIndicatorTarget",
        "PrivateTargetApp",
        1803,
        1,
        true,
        "None",
        false);

    private static readonly CadCustomHandleReferenceValueChange Slot330 = new(330, 1);

    [Fact]
    public void ExplicitRelationshipEndpointConsensusKeepsStableBlockTargetKind()
    {
        var first = ExplicitBlockObservation("1", "10", "2", "20");
        var second = ExplicitBlockObservation("3", "30", "4", "40");

        var consensus = CadXiangyuanParcelExperimentAnalyzer.BuildExplicitReferenceEndpointConsensus(
            new[] { first, second });

        Assert.Equal(CadXiangyuanParcelExperimentCases.ControlIndicatorRelationship, consensus.ExperimentCase.Id);
        Assert.Equal(CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, consensus.Provenance);
        Assert.Equal(2, consensus.StructuralConsensus.ObservationCount);
        Assert.Equal(CadCustomReferenceEndpointKind.BlockReference, consensus.StructuralConsensus.TargetDescriptor.Kind);
    }

    [Fact]
    public void RepeatedCandidateEndpointConsensusCanRetainCustomTargetIdentityWithoutPromotingSourceVendor()
    {
        var candidate = RepeatedCandidate();
        var first = CandidateCustomObservation(candidate, "5", "50", "6", "60");
        var second = CandidateCustomObservation(candidate, "7", "70", "8", "80");

        var consensus = CadXiangyuanParcelExperimentAnalyzer.BuildCandidateReferenceEndpointConsensus(
            candidate,
            new[] { first, second });
        var target = consensus.StructuralConsensus.TargetDescriptor;

        Assert.Equal(CadCustomReferenceEndpointKind.CustomEntity, target.Kind);
        Assert.Equal(TargetClass.DxfName, target.DxfName);
        Assert.Equal(TargetClass.CppClassName, target.CppClassName);
        Assert.Equal(TargetClass.ApplicationName, target.ApplicationName);
        Assert.Equal(
            CadCustomObjectVendor.Unknown,
            CadCustomObjectClassifier.Classify(
                CandidateSourceClass.DxfName,
                CandidateSourceClass.CppClassName,
                CandidateSourceClass.ApplicationName));
    }

    [Fact]
    public void EndpointCaseBindingRejectsBoundaryAndRawValueCases()
    {
        foreach (var caseId in new[]
        {
            CadXiangyuanParcelExperimentCases.Boundary,
            CadXiangyuanParcelExperimentCases.Area,
            CadXiangyuanParcelExperimentCases.FarMax
        })
        {
            var beforeSource = XiangyuanSource("90", "91");
            var afterSource = XiangyuanSource("92", "93");
            Assert.Throws<ArgumentException>(() =>
                CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitReferenceEndpoint(
                    CadXiangyuanParcelExperimentCases.Resolve(caseId),
                    Document("before.dwg", beforeSource, Block("91", "A")),
                    beforeSource,
                    Document("after.dwg", afterSource, Block("93", "B")),
                    afterSource,
                    Slot330));
        }
    }

    [Fact]
    public void EndpointConsensusRejectsMixedProvenance()
    {
        var explicitObservation = ExplicitBlockObservation("100", "101", "102", "103");
        var candidate = RepeatedCandidate();
        var candidateObservation = CandidateCustomObservation(candidate, "104", "105", "106", "107");

        var exception = Assert.Throws<ArgumentException>(() =>
            CadXiangyuanParcelExperimentAnalyzer.BuildExplicitReferenceEndpointConsensus(
                new[] { explicitObservation, candidateObservation }));

        Assert.Contains("provenance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EndpointObservationFailsClosedWhenTargetTypeChanges()
    {
        var relationship = RelationshipCase();
        var beforeSource = XiangyuanSource("110", "111");
        var afterSource = XiangyuanSource("112", "113");
        var observation = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitReferenceEndpoint(
            relationship,
            Document("before.dwg", beforeSource, Block("111", "A")),
            beforeSource,
            Document("after.dwg", afterSource, new CadTextEntity("113", new Point2D(0, 0), "PRIVATE", 1)),
            afterSource,
            Slot330);

        Assert.Equal(CadCustomReferenceEndpointObservationStatus.TargetStructureMismatch, observation.Observation.Status);
        Assert.Throws<ArgumentException>(() =>
            CadXiangyuanParcelExperimentAnalyzer.BuildExplicitReferenceEndpointConsensus(
                new[] { observation, observation }));
    }

    private static CadXiangyuanParcelReferenceEndpointExperimentObservation ExplicitBlockObservation(
        string beforeSourceHandle,
        string beforeTargetHandle,
        string afterSourceHandle,
        string afterTargetHandle)
    {
        var beforeSource = XiangyuanSource(beforeSourceHandle, beforeTargetHandle);
        var afterSource = XiangyuanSource(afterSourceHandle, afterTargetHandle);
        return CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitReferenceEndpoint(
            RelationshipCase(),
            Document("before.dwg", beforeSource, Block(beforeTargetHandle, "PRIVATE_A")),
            beforeSource,
            Document("after.dwg", afterSource, Block(afterTargetHandle, "PRIVATE_B")),
            afterSource,
            Slot330);
    }

    private static CadXiangyuanParcelReferenceEndpointExperimentObservation CandidateCustomObservation(
        CadXiangyuanConversionClassConsensus candidate,
        string beforeSourceHandle,
        string beforeTargetHandle,
        string afterSourceHandle,
        string afterTargetHandle)
    {
        var beforeSource = CandidateSource(beforeSourceHandle, beforeTargetHandle);
        var afterSource = CandidateSource(afterSourceHandle, afterTargetHandle);
        return CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateReferenceEndpoint(
            RelationshipCase(),
            candidate,
            Document("before.dwg", beforeSource, CustomTarget(beforeTargetHandle)),
            beforeSource,
            Document("after.dwg", afterSource, CustomTarget(afterTargetHandle)),
            afterSource,
            Slot330);
    }

    private static CadXiangyuanParcelExperimentCase RelationshipCase()
        => CadXiangyuanParcelExperimentCases.Resolve(
            CadXiangyuanParcelExperimentCases.ControlIndicatorRelationship);

    private static CadXiangyuanConversionClassConsensus RepeatedCandidate()
        => new(
            CandidateSourceClass.DxfName,
            CandidateSourceClass.CppClassName,
            CandidateSourceClass.ApplicationName,
            CadCustomObjectVendor.Unknown,
            CandidateSourceClass.IsEntity,
            CandidateSourceClass.WasProxy,
            CandidateSourceClass.ProxyFlags,
            2,
            2,
            0,
            0);

    private static CadCustomEntity XiangyuanSource(string handle, string targetHandle)
        => Source(handle, targetHandle, XiangyuanSourceClass);

    private static CadCustomEntity CandidateSource(string handle, string targetHandle)
        => Source(handle, targetHandle, CandidateSourceClass);

    private static CadCustomEntity Source(
        string handle,
        string targetHandle,
        CadCustomClassDefinition definition)
        => new(handle, definition.DxfName)
        {
            ClassDefinition = definition,
            HandleReferences = new[] { new CadCustomHandleReference(330, targetHandle) }
        };

    private static CadBlockReferenceEntity Block(string handle, string privateBlockName)
        => new(handle, privateBlockName, new Point2D(0, 0));

    private static CadCustomEntity CustomTarget(string handle)
        => new(handle, TargetClass.DxfName)
        {
            ClassDefinition = TargetClass
        };

    private static CadDocument Document(string name, params CadEntity[] entities)
        => new(
            name,
            "DWG",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            entities);
}
