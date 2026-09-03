using System.Text;
using System.Text.Json;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class DwgCustomObjectDiffV0120Tests
{
    private const string CaptureMethod = "acadsharp-r2004plus-object-section";

    [Fact]
    public void SameLengthRecordsReportOnlyContiguousChangedRanges()
    {
        var before = Record(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        var after = Record(0, 1, 20, 30, 4, 5, 60, 7, 80, 90);

        var report = CadDwgCustomObjectRecordDiffer.Compare(before, after);

        Assert.Equal(CadDwgCustomObjectRecordDiffStatus.Comparable, report.Status);
        Assert.True(report.IsByteComparable);
        Assert.Equal(10, report.BeforeByteCount);
        Assert.Equal(10, report.AfterByteCount);
        Assert.Equal(2, report.CommonPrefixByteCount);
        Assert.Equal(5, report.ChangedByteCount);
        Assert.Collection(
            report.ChangedRanges,
            range => Assert.Equal(new CadDwgCustomObjectChangedByteRange(2, 2), range),
            range => Assert.Equal(new CadDwgCustomObjectChangedByteRange(6, 1), range),
            range => Assert.Equal(new CadDwgCustomObjectChangedByteRange(8, 2), range));
    }

    [Fact]
    public void DifferentLengthRecordsStopAtStructuralLengthMismatch()
    {
        var before = Record(1, 2, 3, 4, 5);
        var after = Record(1, 2, 9, 3, 4, 5);

        var report = CadDwgCustomObjectRecordDiffer.Compare(before, after);

        Assert.Equal(CadDwgCustomObjectRecordDiffStatus.LengthMismatch, report.Status);
        Assert.False(report.IsByteComparable);
        Assert.Equal(2, report.CommonPrefixByteCount);
        Assert.Equal(5, report.BeforeByteCount);
        Assert.Equal(6, report.AfterByteCount);
        Assert.Empty(report.ChangedRanges);
        Assert.Equal(0, report.ChangedByteCount);
    }

    [Fact]
    public void TruncatedRecordFailsClosedWithoutChangedRanges()
    {
        var before = new CadDwgCustomObjectRecord(new byte[] { 1, 2, 3, 4 }, 100, true, CaptureMethod);
        var after = Record(1, 2, 9, 4);

        var report = CadDwgCustomObjectRecordDiffer.Compare(before, after);

        Assert.Equal(CadDwgCustomObjectRecordDiffStatus.TruncatedInput, report.Status);
        Assert.False(report.IsByteComparable);
        Assert.Equal(2, report.CommonPrefixByteCount);
        Assert.Empty(report.ChangedRanges);
    }

    [Fact]
    public void CaptureMethodMismatchFailsBeforeByteComparison()
    {
        var before = Record(1, 2, 3);
        var after = new CadDwgCustomObjectRecord(new byte[] { 1, 2, 9 }, 200, false, "other-capture-method");

        var exception = Assert.Throws<ArgumentException>(() => CadDwgCustomObjectRecordDiffer.Compare(before, after));

        Assert.Contains("different methods", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("200", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityOverloadUsesSharedCustomIdentityGate()
    {
        var before = Entity(
            "SECRET_HANDLE_BEFORE",
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            Record(1, 2, 3, 4));
        var after = Entity(
            "SECRET_HANDLE_AFTER",
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            Record(1, 2, 9, 4));

        var report = CadDwgCustomObjectRecordDiffer.Compare(before, after);

        Assert.Equal(CadDwgCustomObjectRecordDiffStatus.Comparable, report.Status);
        var range = Assert.Single(report.ChangedRanges);
        Assert.Equal(new CadDwgCustomObjectChangedByteRange(2, 1), range);
    }

    [Fact]
    public void EntityOverloadRejectsIdentityMismatchAndMissingEvidenceWithoutLeaks()
    {
        const string privateHandle = "SECRET_COLUMN_HANDLE";
        var column = Entity(
            privateHandle,
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            Record(1, 2, 3));
        var stair = Entity(
            "SECRET_STAIR_HANDLE",
            "TCH_RECTSTAIR",
            "TDbRectStair",
            "Tianzheng Architecture",
            Record(1, 2, 4));
        var missing = Entity(
            "SECRET_MISSING_HANDLE",
            "TCH_COLUMN",
            "TDbColumn",
            "Tianzheng Architecture",
            null);

        var identityException = Assert.Throws<ArgumentException>(() => CadDwgCustomObjectRecordDiffer.Compare(column, stair));
        var evidenceException = Assert.Throws<ArgumentException>(() => CadDwgCustomObjectRecordDiffer.Compare(column, missing));

        Assert.Contains("DXF identity", identityException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw DWG object-record", evidenceException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privateHandle, identityException.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(privateHandle, evidenceException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializedReportNeverContainsRawBytesOffsetsOrHandles()
    {
        const string privateBefore = "SECRET_DWG_PROJECT_ALPHA";
        const string privateAfter = "SECRET_DWG_PROJECT_BETA_";
        var beforeBytes = Encoding.UTF8.GetBytes(privateBefore);
        var afterBytes = Encoding.UTF8.GetBytes(privateAfter);
        Assert.Equal(beforeBytes.Length, afterBytes.Length);
        var before = new CadDwgCustomObjectRecord(beforeBytes, 1234567, false, CaptureMethod);
        var after = new CadDwgCustomObjectRecord(afterBytes, 7654321, false, CaptureMethod);

        var report = CadDwgCustomObjectRecordDiffer.Compare(before, after);
        var json = JsonSerializer.Serialize(report);

        Assert.Equal(CadDwgCustomObjectRecordDiffStatus.Comparable, report.Status);
        Assert.NotEmpty(report.ChangedRanges);
        Assert.DoesNotContain(privateBefore, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateAfter, json, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567", json, StringComparison.Ordinal);
        Assert.DoesNotContain("7654321", json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CadDwgCustomObjectRecord.Bytes), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CadDwgCustomObjectRecord.ObjectSectionOffset), json, StringComparison.Ordinal);
    }

    private static CadDwgCustomObjectRecord Record(params byte[] bytes)
        => new(bytes, 100, false, CaptureMethod);

    private static CadCustomEntity Entity(
        string handle,
        string dxfName,
        string cppClassName,
        string applicationName,
        CadDwgCustomObjectRecord? record)
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
            RawDwgObjectRecord = record
        };
}
