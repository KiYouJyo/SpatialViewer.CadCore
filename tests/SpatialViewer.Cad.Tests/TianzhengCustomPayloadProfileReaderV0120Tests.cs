using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengCustomPayloadProfileReaderV0120Tests
{
    [Fact]
    public async Task RealReaderAttachesAnonymizedSchemaProfileToCustomEntity()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cadcore-tch-profile-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "profile-wall.dxf");
            var source = new global::ACadSharp.CadDocument();
            source.CreateDefaults();
            source.Classes.Add(new DxfClass
            {
                DxfName = "TCH_WALL",
                CppClassName = "TDbWall",
                ApplicationName = "Tianzheng Architecture",
                ClassNumber = 612,
                InstanceCount = 1,
                IsAnEntity = true
            });
            using (var writer = new DxfWriter(path, source, false)) writer.Write();
            Inject(path);

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var profile = Assert.IsType<CadDxfCustomPayloadProfile>(custom.RawDxfProfile);

            Assert.True(result.IsSuccess);
            Assert.Equal(64, profile.Fingerprint.Length);
            Assert.Equal(profile.Fingerprint, custom.Metadata["RawDxfSchemaFingerprint"]);
            Assert.Equal(profile.GroupCodeSignature, custom.Metadata["RawDxfGroupCodeSignature"]);
            Assert.Contains("TDbWall", profile.SubclassMarkers);
            Assert.Contains("TDbWall", custom.Metadata["RawDxfSubclassMarkers"], StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE-WALL-LABEL", profile.GroupCodeSignature, StringComparison.Ordinal);
            Assert.Empty(custom.HandleReferences);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void Inject(string path)
    {
        var text = File.ReadAllText(path, Encoding.ASCII).Replace("\r\n", "\n", StringComparison.Ordinal);
        var marker = "\n  0\nSECTION\n  2\nENTITIES";
        var at = text.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            marker = "\n0\nSECTION\n2\nENTITIES";
            at = text.IndexOf(marker, StringComparison.Ordinal);
        }
        Assert.True(at >= 0);
        var end = text.IndexOf("\n  0\nENDSEC", at, StringComparison.Ordinal);
        if (end < 0) end = text.IndexOf("\n0\nENDSEC", at, StringComparison.Ordinal);
        Assert.True(end >= 0);
        var entity = string.Join("\n",
            string.Empty,
            "  0", "TCH_WALL",
            "  5", "7FFF1201",
            "100", "AcDbEntity",
            "  8", "0",
            "100", "TDbCurveEntity",
            "100", "TDbWall",
            " 90", "202612",
            "  1", "PRIVATE-WALL-LABEL");
        File.WriteAllText(path, text.Insert(end, entity), Encoding.ASCII);
    }
}
