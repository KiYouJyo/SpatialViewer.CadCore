using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanCompatibilityFoundationTests
{
    [Theory]
    [InlineData("CUSTOM", "LzxParcelObject", "OtherVendor")]
    [InlineData("CUSTOM", "SomeClass", "LzxSoft Control Planning CAD")]
    [InlineData("CUSTOM", "SomeClass", "Xiangyuan Control Planning")]
    [InlineData("CUSTOM", "SomeClass", "湘源控规")]
    public void ClassifierRecognizesExplicitXiangyuanApplicationIdentities(string dxfName, string cppClass, string application)
    {
        Assert.True(CadCustomObjectClassifier.IsXiangyuan(dxfName, cppClass, application));
        Assert.Equal(CadCustomObjectVendor.Xiangyuan, CadCustomObjectClassifier.Classify(dxfName, cppClass, application));
        Assert.False(CadCustomObjectClassifier.IsTianzheng(dxfName, cppClass, application));
    }

    [Theory]
    [InlineData("LZX_PARCEL", "SomeClass", "OtherVendor")]
    [InlineData("CUSTOM", "LandscapeObject", "OtherVendor")]
    [InlineData("TCH_WALL", "TDbWall", "Tianzheng Architecture")]
    public void ClassifierDoesNotGuessXiangyuanFromUnprovenDxfNameOrOtherVendors(string dxfName, string cppClass, string application)
    {
        Assert.False(CadCustomObjectClassifier.IsXiangyuan(dxfName, cppClass, application));
    }

    [Fact]
    public void CustomClassAndEntityExposeXiangyuanVendorWithoutChangingConstructorShape()
    {
        var definition = new CadCustomClassDefinition(
            "XY_TEST_CUSTOM",
            "LzxParcelObject",
            "LzxSoft Control Planning CAD",
            601,
            2,
            true,
            "EraseAllowed, TransformAllowed",
            true);
        var entity = new CadCustomEntity("6001", "XY_TEST_CUSTOM")
        {
            ClassDefinition = definition,
            Representation = CadCustomEntityRepresentation.Opaque
        };

        Assert.Equal(CadCustomObjectVendor.Xiangyuan, definition.Vendor);
        Assert.True(definition.IsXiangyuan);
        Assert.False(definition.IsTianzheng);
        Assert.Equal(CadCustomObjectVendor.Xiangyuan, entity.Vendor);
        Assert.True(entity.IsXiangyuan);
        Assert.False(entity.IsTianzheng);
    }

    [Fact]
    public async Task ReaderPreservesXiangyuanCustomClassAndEntityWithVendorDiagnostics()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "xiangyuan-custom-entity.dxf");
            WriteDxfWithXiangyuanClass(path);
            InjectCustomEntity(path, "XY_TEST_CUSTOM");

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var preservedClass = Assert.Single(document.CustomClasses, candidate => candidate.DxfName == "XY_TEST_CUSTOM");
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());

            Assert.True(result.IsSuccess);
            Assert.True(preservedClass.IsXiangyuan);
            Assert.True(custom.IsXiangyuan);
            Assert.Equal(CadCustomObjectVendor.Xiangyuan, custom.Vendor);
            Assert.Equal("XY_TEST_CUSTOM", custom.ClassDefinition?.DxfName);
            Assert.Equal("LzxParcelObject", custom.ClassDefinition?.CppClassName);
            Assert.Equal("LzxSoft Control Planning CAD", custom.ClassDefinition?.ApplicationName);
            Assert.Equal(CadCustomEntityRepresentation.Opaque, custom.Representation);
            Assert.Equal(bool.TrueString, document.Metadata["XiangyuanDetected"]);
            Assert.Equal("1", document.Metadata["XiangyuanClassCount"]);
            Assert.Equal("1", document.Metadata["XiangyuanEntityCount"]);
            Assert.Equal(CadCustomObjectVendor.Xiangyuan.ToString(), custom.Metadata["CustomVendor"]);
            Assert.Equal(bool.TrueString, custom.Metadata["XiangyuanObject"]);
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

    private static void WriteDxfWithXiangyuanClass(string path)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = "XY_TEST_CUSTOM",
            CppClassName = "LzxParcelObject",
            ApplicationName = "LzxSoft Control Planning CAD",
            ClassNumber = 601,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectCustomEntity(string path, string dxfName)
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

        var customEntity = string.Join("\n",
            string.Empty,
            "  0", dxfName,
            "  5", "7FFFFFFD",
            "100", "AcDbEntity",
            "  8", "0");

        File.WriteAllText(path, source.Insert(endAt, customEntity), Encoding.ASCII);
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-xiangyuan-foundation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
