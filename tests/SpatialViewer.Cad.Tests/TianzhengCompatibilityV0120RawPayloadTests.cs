using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengCompatibilityV0120RawPayloadTests
{
    [Fact]
    public async Task TextDxfCustomEntityRetainsRawProprietaryGroupsAcrossReaderBoundary()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tianzheng-raw-payload.dxf");
            WriteDxfWithCustomClass(path, "TCH_WALL", "TDbWall", "Tianzheng Architecture");
            InjectCustomEntity(path, "TCH_WALL", "7FFFFF21", new[]
            {
                (90, "202612"),
                (40, "240.5"),
                (70, "3"),
                (1, "RAW-TIANZHENG-WALL"),
                (310, "DEADBEEF00112233")
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var payload = Assert.IsType<CadDxfCustomPayload>(custom.RawDxfPayload);

            Assert.True(result.IsSuccess);
            Assert.Equal("TCH_WALL", custom.SourceEntityType);
            Assert.False(payload.IsTruncated);
            Assert.Equal("ISO-8859-1", payload.ByteProjection);
            Assert.Contains(payload.Groups, group => group.Code == 90 && group.RawValue == "202612");
            Assert.Contains(payload.Groups, group => group.Code == 40 && group.RawValue == "240.5");
            Assert.Contains(payload.Groups, group => group.Code == 70 && group.RawValue == "3");
            Assert.Contains(payload.Groups, group => group.Code == 1 && group.RawValue == "RAW-TIANZHENG-WALL");
            Assert.Contains(payload.Groups, group => group.Code == 310 && group.RawValue == "DEADBEEF00112233");
            Assert.Equal(bool.TrueString, custom.Metadata["RawDxfPayloadAvailable"]);
            Assert.Equal(bool.FalseString, custom.Metadata["RawDxfPayloadTruncated"]);
            Assert.Equal("ISO-8859-1", custom.Metadata["RawDxfByteProjection"]);
            Assert.Equal("1", document.Metadata["RawDxfCapturedCustomRecordCount"]);
            Assert.Equal("0", document.Metadata["RawDxfTruncatedCustomRecordCount"]);
            Assert.Equal(bool.FalseString, document.Metadata["RawDxfScanBinary"]);
            Assert.Equal(bool.FalseString, document.Metadata["RawDxfScanFailed"]);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_CUSTOM_RAW_DXF_SCAN_FAILED");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ClassesTableRegistrationAllowsNonTchCustomPayloadCaptureWithoutSemanticGuessing()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "registered-custom-payload.dxf");
            WriteDxfWithCustomClass(path, "VENDOR_WALL", "VendorWall", "Vendor Application");
            InjectCustomEntity(path, "VENDOR_WALL", "7FFFFF31", new[] { (90, "777") });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var payload = Assert.IsType<CadDxfCustomPayload>(custom.RawDxfPayload);

            Assert.True(result.IsSuccess);
            Assert.False(custom.IsTianzheng);
            Assert.Equal("VENDOR_WALL", custom.ClassDefinition?.DxfName);
            Assert.Contains(payload.Groups, group => group.Code == 90 && group.RawValue == "777");
            Assert.Equal("1", document.Metadata["RawDxfCapturedCustomRecordCount"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ParallelImportsKeepRawCustomPayloadsIsolatedByImportContext()
    {
        var root = TemporaryDirectory();
        try
        {
            var firstPath = Path.Combine(root, "first-wall.dxf");
            var secondPath = Path.Combine(root, "second-wall.dxf");
            WriteDxfWithCustomClass(firstPath, "TCH_WALL", "TDbWall", "Tianzheng Architecture");
            WriteDxfWithCustomClass(secondPath, "TCH_WALL", "TDbWall", "Tianzheng Architecture");
            InjectCustomEntity(firstPath, "TCH_WALL", "7FFFFF41", new[] { (90, "111") });
            InjectCustomEntity(secondPath, "TCH_WALL", "7FFFFF42", new[] { (90, "222") });

            var importer = new ACadSharpCadImporter();
            var firstTask = importer.ImportAsync(new ImportRequest(firstPath));
            var secondTask = importer.ImportAsync(new ImportRequest(secondPath));
            var results = await Task.WhenAll(firstTask, secondTask);

            var first = Assert.IsType<CadDocument>(results[0].Document);
            var second = Assert.IsType<CadDocument>(results[1].Document);
            var firstPayload = Assert.IsType<CadDxfCustomPayload>(Assert.Single(first.ModelSpace.OfType<CadCustomEntity>()).RawDxfPayload);
            var secondPayload = Assert.IsType<CadDxfCustomPayload>(Assert.Single(second.ModelSpace.OfType<CadCustomEntity>()).RawDxfPayload);

            Assert.Contains(firstPayload.Groups, group => group.Code == 90 && group.RawValue == "111");
            Assert.DoesNotContain(firstPayload.Groups, group => group.Code == 90 && group.RawValue == "222");
            Assert.Contains(secondPayload.Groups, group => group.Code == 90 && group.RawValue == "222");
            Assert.DoesNotContain(secondPayload.Groups, group => group.Code == 90 && group.RawValue == "111");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void WriteDxfWithCustomClass(string path, string dxfName, string cppClassName, string applicationName)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = dxfName,
            CppClassName = cppClassName,
            ApplicationName = applicationName,
            ClassNumber = 601,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectCustomEntity(
        string path,
        string dxfName,
        string handle,
        IReadOnlyList<(int Code, string Value)> proprietaryGroups)
    {
        var source = File.ReadAllText(path, Encoding.ASCII).Replace("\r\n", "\n", StringComparison.Ordinal);
        var entitiesMarker = "\n  0\nSECTION\n  2\nENTITIES";
        var entitiesAt = source.IndexOf(entitiesMarker, StringComparison.Ordinal);
        if (entitiesAt < 0)
        {
            entitiesMarker = "\n0\nSECTION\n2\nENTITIES";
            entitiesAt = source.IndexOf(entitiesMarker, StringComparison.Ordinal);
        }
        Assert.True(entitiesAt >= 0, "Generated DXF did not contain an ENTITIES section.");

        var endAt = source.IndexOf("\n  0\nENDSEC", entitiesAt, StringComparison.Ordinal);
        if (endAt < 0) endAt = source.IndexOf("\n0\nENDSEC", entitiesAt, StringComparison.Ordinal);
        Assert.True(endAt >= 0, "Generated DXF did not contain the ENTITIES section terminator.");

        var lines = new List<string>
        {
            string.Empty,
            "  0", dxfName,
            "  5", handle,
            "100", "AcDbEntity",
            "  8", "0"
        };
        foreach (var (code, value) in proprietaryGroups)
        {
            lines.Add(code.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(3));
            lines.Add(value);
        }

        File.WriteAllText(path, source.Insert(endAt, string.Join("\n", lines)), Encoding.ASCII);
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tianzheng-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
