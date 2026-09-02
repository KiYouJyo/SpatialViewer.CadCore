using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengSpaceV0120Tests
{
    [Fact]
    public async Task PublishedSpaceProfileSurvivesRealTextDxfReader()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tch-space.dxf");
            WriteDxfWithSpaceClass(path);
            InjectSpace(path, "7FFFFB71", new (int, string)[]
            {
                (100, "TDbEntity"),
                (46, "0.0"),
                (47, "100.0"),
                (68, "0"),
                (100, "TDbSpace"),
                (70, "5"),
                (10, "1128560.0"),
                (20, "393884.0"),
                (30, "0.0"),
                (1, "Utility Room"),
                (2, "1033"),
                (7, "Arial"),
                (40, "3.5"),
                (41, "16.335"),
                (42, "16260.0"),
                (43, "120.0"),
                (50, "0.0"),
                (90, "0")
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var space = Assert.IsType<CadTianzhengSpaceSemantic>(custom.NativeSemantics);

            Assert.True(result.IsSuccess);
            Assert.Equal(CadTianzhengSemanticDecoder.SpaceNameNumberDirectProfile, space.DecoderProfile);
            Assert.Equal(new Point2D(1128560.0, 393884.0), space.InsertionPoint);
            Assert.Equal(0.0, space.InsertionZ);
            Assert.Equal("Utility Room", space.Name);
            Assert.Equal("1033", space.Number);
            Assert.Equal(CadCustomSemanticCoverage.Partial, space.Coverage);
            Assert.False(space.IsDrawable2D);
            Assert.Equal(bool.TrueString, custom.Metadata["NativeSemanticEvidenceDecoded"]);
            Assert.Equal(nameof(CadCustomSemanticCoverage.Partial), custom.Metadata["NativeSemanticCoverage"]);
            Assert.Equal(bool.FalseString, custom.Metadata["NativeSemanticDrawable2D"]);
            Assert.Equal(nameof(CadTianzhengSpaceSemantic), custom.Metadata["NativeSemanticType"]);
            Assert.Empty(document.Scene.GetItems());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SpaceDecoderKeepsOnlyEvidenceBackedRoomIdentityFields()
    {
        var semantic = Assert.IsType<CadTianzhengSpaceSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_SPACE", SpaceClass(), Payload("Meeting Room", "210")));

        Assert.Equal(new Point2D(100, 200), semantic.InsertionPoint);
        Assert.Equal(0, semantic.InsertionZ);
        Assert.Equal("Meeting Room", semantic.Name);
        Assert.Equal("210", semantic.Number);
        Assert.Equal(CadCustomSemanticCoverage.Partial, semantic.Coverage);
        Assert.False(semantic.IsDrawable2D);

        var properties = typeof(CadTianzhengSpaceSemantic).GetProperties();
        Assert.DoesNotContain(properties, property => property.Name.Contains("Area", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Volume", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Perimeter", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Skirting", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("WallArea", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SpaceDecoderRequiresStrongSubclassAndMandatoryNameNumberGroups()
    {
        var missingSubclass = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(10, "100"),
            new(20, "200"),
            new(1, "Room"),
            new(2, "101")
        });
        var missingName = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, "TDbSpace"),
            new(10, "100"),
            new(20, "200"),
            new(2, "101")
        });
        var missingNumber = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, "TDbSpace"),
            new(10, "100"),
            new(20, "200"),
            new(1, "Room")
        });
        var malformedPoint = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, "TDbSpace"),
            new(10, "bad"),
            new(20, "200"),
            new(1, "Room"),
            new(2, "101")
        });
        var truncated = new CadDxfCustomPayload(Payload("Room", "101").Groups, true);

        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_SPACE", SpaceClass(), missingSubclass));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_SPACE", SpaceClass(), missingName));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_SPACE", SpaceClass(), missingNumber));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_SPACE", SpaceClass(), malformedPoint));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_SPACE", SpaceClass(), truncated));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("VENDOR_SPACE", VendorClass(), Payload("Room", "101")));
    }

    [Fact]
    public void UnmappedNumericGroupsRemainRawEvidenceOnly()
    {
        var payload = new CadDxfCustomPayload(Payload("Office", "305").Groups.Concat(new CadRawDxfGroup[]
        {
            new(41, "21.50"),
            new(42, "18560"),
            new(43, "120")
        }).ToArray());

        var semantic = Assert.IsType<CadTianzhengSpaceSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_SPACE", SpaceClass(), payload));

        Assert.Equal("Office", semantic.Name);
        Assert.Equal("305", semantic.Number);
        Assert.Contains(payload.Groups, group => group.Code == 41 && group.RawValue == "21.50");
        Assert.Contains(payload.Groups, group => group.Code == 42 && group.RawValue == "18560");
        Assert.Contains(payload.Groups, group => group.Code == 43 && group.RawValue == "120");
    }

    private static CadDxfCustomPayload Payload(string name, string number)
        => new(new CadRawDxfGroup[]
        {
            new(100, "TDbEntity"),
            new(100, "TDbSpace"),
            new(10, "100"),
            new(20, "200"),
            new(30, "0"),
            new(1, name),
            new(2, number)
        });

    private static CadCustomClassDefinition SpaceClass()
        => new("TCH_SPACE", "TDbSpace", "Tianzheng Architecture", 604, 1, true, "None", false);

    private static CadCustomClassDefinition VendorClass()
        => new("VENDOR_SPACE", "TDbSpace", "Vendor", 904, 1, true, "None", false);

    private static void WriteDxfWithSpaceClass(string path)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = "TCH_SPACE",
            CppClassName = "TDbSpace",
            ApplicationName = "Tianzheng Architecture",
            ClassNumber = 604,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectSpace(string path, string handle, IReadOnlyList<(int Code, string Value)> groups)
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
            "  0", "TCH_SPACE",
            "  5", handle,
            "100", "AcDbEntity",
            "  8", "SPACE"
        };
        foreach (var (code, value) in groups)
        {
            lines.Add(code.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(3));
            lines.Add(value);
        }
        File.WriteAllText(path, source.Insert(endAt, string.Join("\n", lines)), Encoding.ASCII);
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tch-space-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
