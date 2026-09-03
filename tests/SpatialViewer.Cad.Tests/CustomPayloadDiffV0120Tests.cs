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

    private static CadDxfCustomPayload Payload(params CadRawDxfGroup[] groups)
        => new(groups);
}
