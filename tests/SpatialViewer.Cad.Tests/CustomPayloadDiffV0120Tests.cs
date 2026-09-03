using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class CustomPayloadDiffV0120Tests
{
    [Fact]
    public void SameLayoutReportsOnlyChangedStructuralSlots()
    {
        var before = Payload(
            new(100, "TDbColumn"),
            new(10, "PRIVATE_X_1000"),
            new(20, "PRIVATE_Y_2000"),
            new(40, "400"),
            new(40, "500"));
        var after = Payload(
            new(100, "TDbColumn"),
            new(10, "PRIVATE_X_1000"),
            new(20, "PRIVATE_Y_2000"),
            new(40, "450"),
            new(40, "550"));

        var report = CadDxfCustomPayloadDiffer.Compare(before, after);

        Assert.Equal(CadDxfCustomPayloadDiffStatus.Comparable, report.Status);
        Assert.True(report.IsComparableLayout);
        Assert.Equal(report.BeforeFingerprint, report.AfterFingerprint);
        Assert.Equal(2, report.ChangedValueCount);
        Assert.Empty(report.CodeCountDeltas);
        Assert.Collection(
            report.ValueChanges,
            change =>
            {
                Assert.Equal(3, change.GroupIndex);
                Assert.Equal(40, change.Code);
                Assert.Equal(1, change.CodeOccurrence);
            },
            change =>
            {
                Assert.Equal(4, change.GroupIndex);
                Assert.Equal(40, change.Code);
                Assert.Equal(2, change.CodeOccurrence);
            });
    }

    [Fact]
    public void StructuralMismatchDoesNotAttemptValueAlignment()
    {
        var before = Payload(
            new(100, "TDbColumn"),
            new(10, "100"),
            new(20, "200"));
        var after = Payload(
            new(100, "TDbColumn"),
            new(10, "100"),
            new(40, "500"),
            new(20, "200"));

        var report = CadDxfCustomPayloadDiffer.Compare(before, after);

        Assert.Equal(CadDxfCustomPayloadDiffStatus.LayoutMismatch, report.Status);
        Assert.False(report.IsComparableLayout);
        Assert.Equal(2, report.FirstLayoutMismatchIndex);
        Assert.Empty(report.ValueChanges);
        var delta = Assert.Single(report.CodeCountDeltas);
        Assert.Equal(40, delta.Code);
        Assert.Equal(0, delta.BeforeCount);
        Assert.Equal(1, delta.AfterCount);
        Assert.NotEqual(report.BeforeFingerprint, report.AfterFingerprint);
    }

    [Fact]
    public void TruncatedInputFailsClosedWithoutDiffingValues()
    {
        var before = new CadDxfCustomPayload(
            new CadRawDxfGroup[]
            {
                new(100, "TDbColumn"),
                new(40, "PRIVATE_BEFORE")
            },
            true);
        var after = Payload(
            new(100, "TDbColumn"),
            new(40, "PRIVATE_AFTER"));

        var report = CadDxfCustomPayloadDiffer.Compare(before, after);

        Assert.Equal(CadDxfCustomPayloadDiffStatus.TruncatedInput, report.Status);
        Assert.False(report.IsComparableLayout);
        Assert.Empty(report.ValueChanges);
        Assert.Empty(report.CodeCountDeltas);
        Assert.Null(report.FirstLayoutMismatchIndex);
    }

    [Fact]
    public void UnchangedPayloadProducesComparableEmptyDiff()
    {
        var before = Payload(
            new(100, "TDbColumn"),
            new(10, "100"),
            new(20, "200"),
            new(40, "400"));
        var after = Payload(
            new(100, "TDbColumn"),
            new(10, "100"),
            new(20, "200"),
            new(40, "400"));

        var report = CadDxfCustomPayloadDiffer.Compare(before, after);

        Assert.Equal(CadDxfCustomPayloadDiffStatus.Comparable, report.Status);
        Assert.Equal(0, report.ChangedValueCount);
        Assert.Empty(report.ValueChanges);
        Assert.Empty(report.CodeCountDeltas);
    }

    [Fact]
    public void SerializedDiffNeverContainsComparedRawValues()
    {
        const string privateBefore = "SECRET_PROJECT_ROOM_ALPHA";
        const string privateAfter = "SECRET_PROJECT_ROOM_BETA";
        var before = Payload(
            new(100, "TDbSpace"),
            new(1, privateBefore),
            new(2, "PRIVATE_NUMBER_101"));
        var after = Payload(
            new(100, "TDbSpace"),
            new(1, privateAfter),
            new(2, "PRIVATE_NUMBER_101"));

        var report = CadDxfCustomPayloadDiffer.Compare(before, after);
        var json = JsonSerializer.Serialize(report);

        Assert.Equal(1, report.ChangedValueCount);
        var change = Assert.Single(report.ValueChanges);
        Assert.Equal(1, change.GroupIndex);
        Assert.Equal(1, change.Code);
        Assert.DoesNotContain(privateBefore, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateAfter, json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_NUMBER_101", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityOverloadComparesMatchingCustomIdentity()
    {
        var before = Entity(
            "SECRET_HANDLE_BEFORE",
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            Payload(new(100, "TDbColumn"), new(40, "400")));
        var after = Entity(
            "SECRET_HANDLE_AFTER",
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            Payload(new(100, "TDbColumn"), new(40, "500")));

        var report = CadDxfCustomPayloadDiffer.Compare(before, after);

        Assert.Equal(CadDxfCustomPayloadDiffStatus.Comparable, report.Status);
        var change = Assert.Single(report.ValueChanges);
        Assert.Equal(40, change.Code);
        Assert.Equal(1, change.CodeOccurrence);
    }

    [Fact]
    public void EntityOverloadRejectsDifferentDxfIdentityWithoutLeakingHandlesOrValues()
    {
        const string privateHandle = "SECRET_COLUMN_HANDLE";
        const string privateValue = "SECRET_COLUMN_WIDTH";
        var before = Entity(
            privateHandle,
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            Payload(new(40, privateValue)));
        var after = Entity(
            "SECRET_STAIR_HANDLE",
            "TCH_RECTSTAIR",
            "TDbRectStair",
            "Tianzheng Architecture",
            Payload(new(40, "PRIVATE_STAIR_VALUE")));

        var exception = Assert.Throws<ArgumentException>(() => CadDxfCustomPayloadDiffer.Compare(before, after));

        Assert.Contains("DXF identity", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privateHandle, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(privateValue, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityOverloadRejectsDifferentKnownCppOrApplicationIdentity()
    {
        var before = Entity(
            "100",
            "TCH_COLUMN",
            "TDbColumnV1",
            "Tianzheng Architecture",
            Payload(new(40, "400")));
        var cppMismatch = Entity(
            "101",
            "TCH_COLUMN",
            "TDbColumnV2",
            "Tianzheng Architecture",
            Payload(new(40, "500")));
        var applicationMismatch = Entity(
            "102",
            "TCH_COLUMN",
            "TDbColumnV1",
            "Other Architecture",
            Payload(new(40, "500")));

        var cppException = Assert.Throws<ArgumentException>(() => CadDxfCustomPayloadDiffer.Compare(before, cppMismatch));
        var applicationException = Assert.Throws<ArgumentException>(() => CadDxfCustomPayloadDiffer.Compare(before, applicationMismatch));

        Assert.Contains("C++ class", cppException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("applications", applicationException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EntityOverloadRejectsMissingRawDxfPayload()
    {
        var before = Entity(
            "100",
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            null);
        var after = Entity(
            "101",
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            Payload(new(40, "500")));

        var exception = Assert.Throws<ArgumentException>(() => CadDxfCustomPayloadDiffer.Compare(before, after));

        Assert.Contains("raw DXF payload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CadDxfCustomPayload Payload(params CadRawDxfGroup[] groups)
        => new(groups);

    private static CadCustomEntity Entity(
        string handle,
        string dxfName,
        string cppClassName,
        string applicationName,
        CadDxfCustomPayload? payload)
        => new(handle, dxfName)
        {
            ClassDefinition = new CadCustomClassDefinition(
                dxfName,
                cppClassName,
                applicationName,
                700,
                1,
                true,
                "None",
                false),
            RawDxfPayload = payload,
            RawDxfProfile = CadDxfCustomPayloadProfiler.Create(payload)
        };
}
