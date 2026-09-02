using System.Globalization;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class DwgRawObjectEvidenceV0120Tests
{
    [Fact]
    public void RawDwgObjectRecordCopiesSourceBytes()
    {
        var source = new byte[] { 1, 2, 3, 4 };
        var record = new CadDwgCustomObjectRecord(source, 42, false, "fixture");

        source[0] = 99;

        Assert.Equal(4, record.ByteCount);
        Assert.Equal(1, record.Bytes.Span[0]);
        Assert.Equal(42, record.ObjectSectionOffset);
        Assert.False(record.IsTruncated);
        Assert.Equal("fixture", record.CaptureMethod);
    }

    [Fact]
    public void ModernDwgReaderHookCapturesObjectRecordsByHandle()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "raw-object-evidence.dwg");
            var document = new global::ACadSharp.CadDocument();
            document.CreateDefaults();
            document.Entities.Add(new Line
            {
                StartPoint = new XYZ(10, 20, 0),
                EndPoint = new XYZ(110, 220, 0)
            });
            document.Entities.Add(new Line
            {
                StartPoint = new XYZ(-25, 5, 0),
                EndPoint = new XYZ(250, 75, 0)
            });
            using (var writer = new DwgWriter(path, document)) writer.Write();

            using var reader = new CadCoreDwgReader(path);
            reader.Configuration.KeepUnknownEntities = true;
            var source = reader.Read();
            var lines = source.Entities.OfType<Line>().ToArray();
            Assert.Equal(2, lines.Length);

            var firstHandle = lines[0].Handle.ToString(CultureInfo.InvariantCulture);
            var secondHandle = lines[1].Handle.ToString(CultureInfo.InvariantCulture);
            var first = Assert.IsType<CadDwgCustomObjectRecord>(ACadSharpCustomPayloadContext.FindDwgObjectRecord(firstHandle));
            var repeated = Assert.IsType<CadDwgCustomObjectRecord>(ACadSharpCustomPayloadContext.FindDwgObjectRecord(firstHandle));
            var second = Assert.IsType<CadDwgCustomObjectRecord>(ACadSharpCustomPayloadContext.FindDwgObjectRecord(secondHandle));
            var snapshot = Assert.IsType<DwgRawObjectCaptureSnapshot>(ACadSharpCustomPayloadContext.SnapshotDwg());

            Assert.Same(first, repeated);
            Assert.True(first.ByteCount > 8);
            Assert.True(second.ByteCount > 8);
            Assert.True(first.ObjectSectionOffset >= 0);
            Assert.True(second.ObjectSectionOffset >= 0);
            Assert.NotEqual(first.ObjectSectionOffset, second.ObjectSectionOffset);
            Assert.False(first.IsTruncated);
            Assert.False(second.IsTruncated);
            Assert.Equal(ACadSharpDwgRawObjectReader.CaptureMethod, first.CaptureMethod);
            Assert.True(snapshot.Supported);
            Assert.False(snapshot.CaptureFailed);
            Assert.False(snapshot.BudgetExhausted);
            Assert.Equal(2, snapshot.CapturedRecordCount);
            Assert.Equal(0, snapshot.TruncatedRecordCount);
            Assert.True(snapshot.CapturedByteCount >= first.ByteCount + second.ByteCount);
            Assert.Null(ACadSharpCustomPayloadContext.FindDwgObjectRecord("18446744073709551615"));
        }
        finally
        {
            ACadSharpCustomPayloadContext.Clear();
            Directory.Delete(root, true);
        }
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-dwg-evidence-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
