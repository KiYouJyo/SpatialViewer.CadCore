using System.Text.Json;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class CadCustomReferenceEndpointEvidenceTests
{
    private static readonly CadCustomClassDefinition SourceClass = new(
        "PRIVATE_SOURCE_OBJECT",
        "PrivateSourceObject",
        "PrivateApplication",
        1701,
        1,
        true,
        "None",
        true);

    private static readonly CadCustomClassDefinition TargetCustomClass = new(
        "TARGET_CUSTOM_CLASS",
        "TargetCustomClass",
        "TargetApplication",
        1702,
        1,
        true,
        "None",
        false);

    private static readonly CadCustomHandleReferenceValueChange Slot330 = new(330, 1);

    [Fact]
    public void BlockEndpointEvidenceDoesNotExposeHandlesOrBlockNames()
    {
        const string privateBeforeBlock = "SECRET_INDICATOR_BLOCK_A";
        const string privateAfterBlock = "SECRET_INDICATOR_BLOCK_B";
        var beforeSource = Source("1", "10");
        var afterSource = Source("2", "20");
        var beforeTarget = new CadBlockReferenceEntity("10", privateBeforeBlock, new Point2D(1, 2));
        var afterTarget = new CadBlockReferenceEntity("20", privateAfterBlock, new Point2D(3, 4));

        var observation = CadCustomReferenceEndpointExperimentAnalyzer.Observe(
            Document("PRIVATE_BEFORE.dwg", beforeSource, beforeTarget),
            beforeSource,
            Document("PRIVATE_AFTER.dwg", afterSource, afterTarget),
            afterSource,
            Slot330);
        var json = JsonSerializer.Serialize(observation);

        Assert.Equal(CadCustomReferenceEndpointObservationStatus.Comparable, observation.Status);
        Assert.Equal(CadCustomReferenceEndpointKind.BlockReference, observation.TargetDescriptor!.Kind);
        Assert.Equal(CadCustomObjectVendor.Unknown, observation.TargetDescriptor.Vendor);
        Assert.DoesNotContain(privateBeforeBlock, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateAfterBlock, json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_BEFORE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_AFTER", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"10\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"20\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomEndpointRetainsOnlyStructuralClassIdentity()
    {
        var beforeSource = Source("30", "40");
        var afterSource = Source("31", "50");
        var beforeTarget = CustomTarget("40");
        var afterTarget = CustomTarget("50");

        var observation = CadCustomReferenceEndpointExperimentAnalyzer.Observe(
            Document("before.dwg", beforeSource, beforeTarget),
            beforeSource,
            Document("after.dwg", afterSource, afterTarget),
            afterSource,
            Slot330);

        var descriptor = Assert.IsType<CadCustomReferenceEndpointDescriptor>(observation.TargetDescriptor);
        Assert.Equal(CadCustomReferenceEndpointKind.CustomEntity, descriptor.Kind);
        Assert.Equal(TargetCustomClass.DxfName, descriptor.DxfName);
        Assert.Equal(TargetCustomClass.CppClassName, descriptor.CppClassName);
        Assert.Equal(TargetCustomClass.ApplicationName, descriptor.ApplicationName);
        Assert.Equal(CadCustomObjectVendor.Unknown, descriptor.Vendor);
    }

    [Fact]
    public void TargetStructureMismatchFailsClosed()
    {
        var beforeSource = Source("60", "70");
        var afterSource = Source("61", "80");
        var beforeTarget = new CadBlockReferenceEntity("70", "PRIVATE_BLOCK", new Point2D(0, 0));
        var afterTarget = new CadTextEntity("80", new Point2D(0, 0), "PRIVATE_TEXT", 2);

        var observation = CadCustomReferenceEndpointExperimentAnalyzer.Observe(
            Document("before.dwg", beforeSource, beforeTarget),
            beforeSource,
            Document("after.dwg", afterSource, afterTarget),
            afterSource,
            Slot330);

        Assert.Equal(CadCustomReferenceEndpointObservationStatus.TargetStructureMismatch, observation.Status);
        Assert.Null(observation.TargetDescriptor);
    }

    [Fact]
    public void UnresolvedTargetAndUnchangedSlotCannotProduceEndpointEvidence()
    {
        var unresolvedBefore = Source("90", "999");
        var unresolvedAfter = Source("91", "998");
        var unresolved = CadCustomReferenceEndpointExperimentAnalyzer.Observe(
            Document("before.dwg", unresolvedBefore),
            unresolvedBefore,
            Document("after.dwg", unresolvedAfter),
            unresolvedAfter,
            Slot330);
        Assert.Equal(CadCustomReferenceEndpointObservationStatus.TargetUnresolved, unresolved.Status);

        var unchangedBefore = Source("92", "100");
        var unchangedAfter = Source("93", "100");
        var targetBefore = new CadLineEntity("100", new Point2D(0, 0), new Point2D(1, 0));
        var targetAfter = new CadLineEntity("100", new Point2D(0, 0), new Point2D(2, 0));
        var unchanged = CadCustomReferenceEndpointExperimentAnalyzer.Observe(
            Document("before2.dwg", unchangedBefore, targetBefore),
            unchangedBefore,
            Document("after2.dwg", unchangedAfter, targetAfter),
            unchangedAfter,
            Slot330);
        Assert.Equal(CadCustomReferenceEndpointObservationStatus.SlotNotChanged, unchanged.Status);
    }

    [Fact]
    public void ConsensusRequiresTwoStableObservationsOfSameSlotAndTargetStructure()
    {
        var first = BlockObservation("110", "120", "111", "121");
        var second = BlockObservation("130", "140", "131", "141");

        var consensus = CadCustomReferenceEndpointExperimentAnalyzer.BuildConsensus(new[] { first, second });

        Assert.Equal(2, consensus.ObservationCount);
        Assert.Equal(Slot330, consensus.Slot);
        Assert.Equal(CadCustomReferenceEndpointKind.BlockReference, consensus.TargetDescriptor.Kind);
    }

    [Fact]
    public void ConsensusRejectsNonComparableOrDifferentEndpointEvidence()
    {
        var comparable = BlockObservation("150", "160", "151", "161");
        var different = comparable with
        {
            TargetDescriptor = new CadCustomReferenceEndpointDescriptor(
                CadCustomReferenceEndpointKind.Text,
                string.Empty,
                string.Empty,
                string.Empty,
                CadCustomObjectVendor.Unknown)
        };
        Assert.Throws<ArgumentException>(() =>
            CadCustomReferenceEndpointExperimentAnalyzer.BuildConsensus(new[] { comparable, different }));

        var invalid = comparable with
        {
            Status = CadCustomReferenceEndpointObservationStatus.TargetUnresolved,
            TargetDescriptor = null
        };
        Assert.Throws<ArgumentException>(() =>
            CadCustomReferenceEndpointExperimentAnalyzer.BuildConsensus(new[] { comparable, invalid }));
    }

    private static CadCustomReferenceEndpointExperimentObservation BlockObservation(
        string beforeSourceHandle,
        string beforeTargetHandle,
        string afterSourceHandle,
        string afterTargetHandle)
    {
        var beforeSource = Source(beforeSourceHandle, beforeTargetHandle);
        var afterSource = Source(afterSourceHandle, afterTargetHandle);
        var beforeTarget = new CadBlockReferenceEntity(beforeTargetHandle, "PRIVATE_A", new Point2D(0, 0));
        var afterTarget = new CadBlockReferenceEntity(afterTargetHandle, "PRIVATE_B", new Point2D(0, 0));
        return CadCustomReferenceEndpointExperimentAnalyzer.Observe(
            Document("before.dwg", beforeSource, beforeTarget),
            beforeSource,
            Document("after.dwg", afterSource, afterTarget),
            afterSource,
            Slot330);
    }

    private static CadCustomEntity Source(string handle, string targetHandle)
        => new(handle, SourceClass.DxfName)
        {
            ClassDefinition = SourceClass,
            HandleReferences = new[] { new CadCustomHandleReference(330, targetHandle) }
        };

    private static CadCustomEntity CustomTarget(string handle)
        => new(handle, TargetCustomClass.DxfName)
        {
            ClassDefinition = TargetCustomClass
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
