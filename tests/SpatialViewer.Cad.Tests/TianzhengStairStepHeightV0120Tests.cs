using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengStairStepHeightV0120Tests
{
    [Theory]
    [InlineData("TCH_LINESTAIR", "OpaqueLineStairClass", 175.0, CadTianzhengStairSemanticDecoder.LineStairStepHeightProfile)]
    [InlineData("TCH_RECTSTAIR", "OpaqueRectStairClass", 165.5, CadTianzhengStairSemanticDecoder.RectStairStepHeightProfile)]
    public async Task PublishedGroupFortyStepHeightSurvivesRealTextDxfReader(
        string dxfName,
        string cppClassName,
        double expectedHeight,
        string expectedProfile)
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, $"{dxfName.ToLowerInvariant()}-step-height.dxf");
            WriteDxfWithStairClass(path, dxfName, cppClassName);
            InjectStair(path, "7FFFFA81", dxfName, new (int, string)[]
            {
                (40, expectedHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (41, "280"),
                (70, "12")
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var stair = Assert.IsType<CadTianzhengStairStepSemantic>(custom.NativeSemantics);

            Assert.True(result.IsSuccess);
            Assert.Equal(dxfName, stair.StairEntityType);
            Assert.Equal(expectedHeight, stair.StepHeight);
            Assert.Equal(expectedProfile, stair.DecoderProfile);
            Assert.Equal(CadCustomSemanticCoverage.Partial, stair.Coverage);
            Assert.False(stair.IsDrawable2D);
            Assert.Equal(bool.TrueString, custom.Metadata["NativeSemanticEvidenceDecoded"]);
            Assert.Equal(nameof(CadCustomSemanticCoverage.Partial), custom.Metadata["NativeSemanticCoverage"]);
            Assert.Equal(bool.FalseString, custom.Metadata["NativeSemanticDrawable2D"]);
            Assert.Equal(nameof(CadTianzhengStairStepSemantic), custom.Metadata["NativeSemanticType"]);
            Assert.Empty(document.Scene.GetItems());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void StairDecoderRequiresExactConsistentIdentityAndOnePositiveFiniteGroupForty()
    {
        var valid = Payload(new CadRawDxfGroup(40, "175"));
        var duplicate = Payload(new CadRawDxfGroup(40, "175"), new CadRawDxfGroup(40, "180"));
        var malformed = Payload(new CadRawDxfGroup(40, "not-a-number"));
        var zero = Payload(new CadRawDxfGroup(40, "0"));
        var negative = Payload(new CadRawDxfGroup(40, "-175"));
        var truncated = new CadDxfCustomPayload(valid.Groups, true);

        var line = Assert.IsType<CadTianzhengStairStepSemantic>(
            CadTianzhengStairSemanticDecoder.Decode("TCH_LINESTAIR", StairClass("TCH_LINESTAIR"), valid));
        var rect = Assert.IsType<CadTianzhengStairStepSemantic>(
            CadTianzhengStairSemanticDecoder.Decode("TCH_RECTSTAIR", StairClass("TCH_RECTSTAIR"), valid));

        Assert.Equal(175, line.StepHeight);
        Assert.Equal(175, rect.StepHeight);
        Assert.Null(CadTianzhengStairSemanticDecoder.Decode("TCH_LINESTAIR", StairClass("TCH_RECTSTAIR"), valid));
        Assert.Null(CadTianzhengStairSemanticDecoder.Decode("VENDOR_STAIR", VendorClass(), valid));
        Assert.Null(CadTianzhengStairSemanticDecoder.Decode("TCH_LINESTAIR", StairClass("TCH_LINESTAIR"), duplicate));
        Assert.Null(CadTianzhengStairSemanticDecoder.Decode("TCH_LINESTAIR", StairClass("TCH_LINESTAIR"), malformed));
        Assert.Null(CadTianzhengStairSemanticDecoder.Decode("TCH_LINESTAIR", StairClass("TCH_LINESTAIR"), zero));
        Assert.Null(CadTianzhengStairSemanticDecoder.Decode("TCH_LINESTAIR", StairClass("TCH_LINESTAIR"), negative));
        Assert.Null(CadTianzhengStairSemanticDecoder.Decode("TCH_LINESTAIR", StairClass("TCH_LINESTAIR"), truncated));
    }

    [Fact]
    public void StairStepSemanticDoesNotPromoteOtherPublishedTableFieldsWithoutIndependentValidation()
    {
        var payload = Payload(
            new CadRawDxfGroup(40, "175"),
            new CadRawDxfGroup(41, "280"),
            new CadRawDxfGroup(42, "1200"),
            new CadRawDxfGroup(43, "280"),
            new CadRawDxfGroup(44, "1100"),
            new CadRawDxfGroup(50, "1.57079632679"),
            new CadRawDxfGroup(70, "12"),
            new CadRawDxfGroup(71, "10"),
            new CadRawDxfGroup(72, "9"));

        var semantic = Assert.IsType<CadTianzhengStairStepSemantic>(
            CadTianzhengStairSemanticDecoder.Decode("TCH_RECTSTAIR", StairClass("TCH_RECTSTAIR"), payload));
        var properties = typeof(CadTianzhengStairStepSemantic).GetProperties();

        Assert.Equal(175, semantic.StepHeight);
        Assert.DoesNotContain(properties, property => property.Name.Contains("Width", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Count", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Rotation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Platform", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Flight", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(9, payload.Groups.Count);
    }

    [Fact]
    public void PartialStairSemanticKeepsProxyGraphicsAsDisplayFallback()
    {
        var semantic = new CadTianzhengStairStepSemantic(
            "TCH_LINESTAIR",
            175,
            CadTianzhengStairSemanticDecoder.LineStairStepHeightProfile);
        var custom = new CadCustomEntity("CA81", "TCH_LINESTAIR")
        {
            ClassDefinition = StairClass("TCH_LINESTAIR"),
            NativeSemantics = semantic,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline" },
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[]
                {
                    new Point2D(0, 0),
                    new Point2D(1200, 3000)
                })
            }
        };
        var document = Document(custom);

        var item = Assert.Single(document.Scene.GetItems());

        Assert.IsType<PolylineGeometry>(item.Geometry);
        Assert.Equal(new BoundingBox2D(0, 0, 1200, 3000), item.Bounds);
        Assert.Equal(bool.TrueString, item.Metadata["CustomProxyFallback"]);
        Assert.Equal(bool.FalseString, item.Metadata["NativeSemanticsDecoded"]);
        Assert.Equal(CadCustomSemanticCoverage.Partial, semantic.Coverage);
        Assert.False(semantic.IsDrawable2D);
    }

    private static CadDxfCustomPayload Payload(params CadRawDxfGroup[] groups)
        => new(groups);

    private static CadCustomClassDefinition StairClass(string dxfName)
        => new(dxfName, "OpaqueStairClass", "Tianzheng Architecture", 607, 1, true, "None", false);

    private static CadCustomClassDefinition VendorClass()
        => new("VENDOR_STAIR", "OpaqueStairClass", "Vendor", 907, 1, true, "None", false);

    private static CadDocument Document(params CadEntity[] entities)
        => new(
            "stair-step-height.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            entities);

    private static void WriteDxfWithStairClass(string path, string dxfName, string cppClassName)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = dxfName,
            CppClassName = cppClassName,
            ApplicationName = "Tianzheng Architecture",
            ClassNumber = 607,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectStair(
        string path,
        string handle,
        string dxfName,
        IReadOnlyList<(int Code, string Value)> groups)
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
        foreach (var (code, value) in groups)
        {
            lines.Add(code.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(3));
            lines.Add(value);
        }
        File.WriteAllText(path, source.Insert(endAt, string.Join("\n", lines)), Encoding.ASCII);
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tch-stair-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
