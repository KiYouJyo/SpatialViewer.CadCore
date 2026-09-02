using ACadSharp.Classes;
using ACadSharp.Entities;
using ACadSharp.IO;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengCompatibilityV0110ReaderTests
{
    [Fact]
    public async Task ProxyClassRoundTripIsPreservedAsTianzhengCustomEntity()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cadcore-tianzheng-v0110-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "tianzheng-proxy.dxf");
            var source = new global::ACadSharp.CadDocument();
            source.CreateDefaults();
            var dxfClass = new DxfClass
            {
                DxfName = "TCH_WALL",
                CppClassName = "TDbWall",
                ApplicationName = "Tianzheng Architecture",
                ClassNumber = 501,
                InstanceCount = 1,
                IsAnEntity = true
            };
            source.Classes.Add(dxfClass);
            source.Entities.Add(new ProxyEntity
            {
                DxfClass = dxfClass,
                Version = source.Header.Version
            });

            using (var writer = new DxfWriter(path, source, false))
            {
                writer.Write();
            }

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var preservedClass = Assert.Single(document.CustomClasses.Where(candidate => candidate.DxfName == "TCH_WALL"));

            Assert.True(result.IsSuccess);
            Assert.True(preservedClass.IsTianzheng);
            Assert.True(custom.IsTianzheng);
            Assert.Equal("TCH_WALL", custom.ClassDefinition?.DxfName);
            Assert.Equal("TDbWall", custom.ClassDefinition?.CppClassName);
            Assert.Equal("Tianzheng Architecture", custom.ClassDefinition?.ApplicationName);
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
}
