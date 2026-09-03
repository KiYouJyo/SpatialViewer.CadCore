using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengDrawingNameV0120Tests
{
    [Fact]
    public async Task PublishedGroupOneDrawingNameSurvivesRealTextDxfReader()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tch-drawing-name.dxf");
            WriteDxfWithDrawingNameClass(path);
            InjectDrawingName(path, "7FFFFA71", new (int, string)[]
            {
                (1, "GROUND FLOOR PLAN")
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var drawingName = Assert.IsType<CadTianzhengDrawingNameSemantic>(custom.NativeSemantics);

            Assert.True(result.IsSuccess);
            Assert.Equal("GROUND FLOOR PLAN", drawingName.Text);
            Assert.Equal(CadTianzhengSemanticDecoder.DrawingNameTextDirectProfile, drawingName.DecoderProfile);
            Assert.Equal(CadCustomSemanticCoverage.Partial, drawingName.Coverage);
            Assert.False(drawingName.IsDrawable2D);
            Assert.Equal(bool.TrueString, custom.Metadata["NativeSemanticEvidenceDecoded"]);
            Assert.Equal(nameof(CadCustomSemanticCoverage.Partial), custom.Metadata["NativeSemanticCoverage"]);
            Assert.Equal(bool.FalseString, custom.Metadata["NativeSemanticDrawable2D"]);
            Assert.Equal(nameof(CadTianzhengDrawingNameSemantic), custom.Metadata["NativeSemanticType"]);
            Assert.Empty(document.Scene.GetItems());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DrawingNameDecoderRequiresExactTianzhengIdentityAndUsefulGroupOneText()
    {
        var valid = Payload("SECTION A-A");
        var blank = Payload("   ");
        var missing = new CadDxfCustomPayload(new CadRawDxfGroup[] { new(90, "1") });
        var truncated = new CadDxfCustomPayload(valid.Groups, true);

        var semantic = Assert.IsType<CadTianzhengDrawingNameSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_DRAWINGNAME", DrawingNameClass(), valid));

        Assert.Equal("SECTION A-A", semantic.Text);
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_DRAWINGNAME", DrawingNameClass(), blank));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_DRAWINGNAME", DrawingNameClass(), missing));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_DRAWINGNAME", DrawingNameClass(), truncated));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("VENDOR_DRAWINGNAME", VendorClass(), valid));
    }

    [Fact]
    public void DrawingNameSemanticDoesNotInventIndexOrGeometryFields()
    {
        var semantic = Assert.IsType<CadTianzhengDrawingNameSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_DRAWINGNAME", DrawingNameClass(), Payload("DETAIL 01")));

        var properties = typeof(CadTianzhengDrawingNameSemantic).GetProperties();
        Assert.Equal("DETAIL 01", semantic.Text);
        Assert.DoesNotContain(properties, property => property.Name.Contains("Point", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Scale", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Index", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Number", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Underline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnmappedDrawingNameGroupsRemainRawOnly()
    {
        var payload = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(1, "ROOF PLAN"),
            new(10, "1000"),
            new(20, "2000"),
            new(40, "3.5"),
            new(47, "100")
        });

        var semantic = Assert.IsType<CadTianzhengDrawingNameSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_DRAWINGNAME", DrawingNameClass(), payload));

        Assert.Equal("ROOF PLAN", semantic.Text);
        Assert.Equal(5, payload.Groups.Count);
        Assert.Single(typeof(CadTianzhengDrawingNameSemantic).GetProperties(), property => property.Name == nameof(CadTianzhengDrawingNameSemantic.Text));
    }

    private static CadDxfCustomPayload Payload(string text)
        => new(new CadRawDxfGroup[] { new(1, text) });

    private static CadCustomClassDefinition DrawingNameClass()
        => new("TCH_DRAWINGNAME", "OpaqueDrawingNameClass", "Tianzheng Architecture", 605, 1, true, "None", false);

    private static CadCustomClassDefinition VendorClass()
        => new("VENDOR_DRAWINGNAME", "OpaqueDrawingNameClass", "Vendor", 905, 1, true, "None", false);

    private static void WriteDxfWithDrawingNameClass(string path)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = "TCH_DRAWINGNAME",
            CppClassName = "OpaqueDrawingNameClass",
            ApplicationName = "Tianzheng Architecture",
            ClassNumber = 605,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectDrawingName(string path, string handle, IReadOnlyList<(int Code, string Value)> groups)
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
            "  0", "TCH_DRAWINGNAME",
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
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tch-drawingname-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
