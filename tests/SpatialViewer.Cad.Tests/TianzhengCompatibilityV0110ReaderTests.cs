using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengCompatibilityV0110ReaderTests
{
    [Fact]
    public async Task WriterRoundTripPreservesTianzhengClassIdentity()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tianzheng-class.dxf");
            WriteDxfWithTianzhengClass(path);

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var preservedClass = Assert.Single(document.CustomClasses, candidate => candidate.DxfName == "TCH_WALL");

            Assert.True(result.IsSuccess);
            Assert.True(preservedClass.IsTianzheng);
            Assert.Equal("TDbWall", preservedClass.CppClassName);
            Assert.Equal("Tianzheng Architecture", preservedClass.ApplicationName);
            Assert.Equal(bool.TrueString, document.Metadata["TianzhengDetected"]);
            Assert.Equal("1", document.Metadata["TianzhengClassCount"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RawAcadProxyEntityIsPreservedAsTianzhengCustomEntity()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tianzheng-proxy.dxf");
            WriteDxfWithTianzhengClass(path);
            InjectProxyEntity(path, 501);

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var preservedClass = Assert.Single(document.CustomClasses, candidate => candidate.DxfName == "TCH_WALL");
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());

            Assert.True(result.IsSuccess);
            Assert.True(preservedClass.IsTianzheng);
            Assert.True(custom.IsTianzheng);
            Assert.Equal("TCH_WALL", custom.ClassDefinition?.DxfName);
            Assert.Equal("TDbWall", custom.ClassDefinition?.CppClassName);
            Assert.Equal("Tianzheng Architecture", custom.ClassDefinition?.ApplicationName);
            Assert.Equal(CadCustomEntityRepresentation.Opaque, custom.Representation);
            Assert.Equal(bool.TrueString, document.Metadata["TianzhengDetected"]);
            Assert.Equal("1", document.Metadata["TianzhengEntityCount"]);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CAD_CUSTOM_ENTITY_PRESERVED");
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code == "CAD_UNSUPPORTED_ENTITY" &&
                diagnostic.Context?.TryGetValue("Handle", out var handle) == true &&
                handle == custom.Handle);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void WriteDxfWithTianzhengClass(string path)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = "TCH_WALL",
            CppClassName = "TDbWall",
            ApplicationName = "Tianzheng Architecture",
            ClassNumber = 501,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectProxyEntity(string path, int classNumber)
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

        var proxy = string.Join("\n",
            string.Empty,
            "  0", "ACAD_PROXY_ENTITY",
            "  5", "7FFFFFFE",
            "100", "AcDbEntity",
            "  8", "0",
            "100", "AcDbProxyEntity",
            " 90", "498",
            " 91", classNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            " 70", "1",
            " 95", "0");

        File.WriteAllText(path, source.Insert(endAt, proxy), Encoding.ASCII);
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tianzheng-v0110-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
