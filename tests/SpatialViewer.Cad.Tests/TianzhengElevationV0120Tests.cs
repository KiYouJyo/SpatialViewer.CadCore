using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengElevationV0120Tests
{
    [Fact]
    public async Task PublishedElevationProfileSurvivesRealTextDxfReader()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tch-elevation.dxf");
            WriteDxfWithElevationClass(path);
            InjectElevation(path, "7FFFFC81", new (int, string)[]
            {
                (100, "TDbEntity"),
                (47, "50.0"),
                (100, "TDbSymbWithText"),
                (40, "3.5"),
                (7, "_TCH_DIM"),
                (100, "TDbSymbElevation"),
                (10, "84966.3"),
                (20, "37937.0"),
                (30, "0.0"),
                (1, "37.900")
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var elevation = Assert.IsType<CadTianzhengElevationSemantic>(custom.NativeSemantics);

            Assert.True(result.IsSuccess);
            Assert.Equal(CadTianzhengSemanticDecoder.ElevationTextDirectProfile, elevation.DecoderProfile);
            Assert.Equal(new Point2D(84966.3, 37937.0), elevation.InsertionPoint);
            Assert.Equal(0.0, elevation.InsertionZ);
            Assert.Equal("37.900", elevation.Text);
            Assert.Equal(50.0, elevation.PlotScale);
            Assert.Equal(CadCustomSemanticCoverage.Partial, elevation.Coverage);
            Assert.False(elevation.IsDrawable2D);
            Assert.Equal(bool.TrueString, custom.Metadata["NativeSemanticEvidenceDecoded"]);
            Assert.Equal(nameof(CadCustomSemanticCoverage.Partial), custom.Metadata["NativeSemanticCoverage"]);
            Assert.Equal(bool.FalseString, custom.Metadata["NativeSemanticDrawable2D"]);
            Assert.Equal(nameof(CadTianzhengElevationSemantic), custom.Metadata["NativeSemanticType"]);
            Assert.Empty(document.Scene.GetItems());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ElevationTextIsPreservedWithoutInventingSymbolGeometry()
    {
        var payload = Payload("+0.000", includeScale: false);

        var semantic = Assert.IsType<CadTianzhengElevationSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_ELEVATION", ElevationClass(), payload));

        Assert.Equal("+0.000", semantic.Text);
        Assert.Null(semantic.PlotScale);
        Assert.Equal(CadCustomSemanticCoverage.Partial, semantic.Coverage);
        Assert.False(semantic.IsDrawable2D);
        var properties = typeof(CadTianzhengElevationSemantic).GetProperties();
        Assert.DoesNotContain(properties, property => property.Name.Contains("Arrow", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Direction", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("TextHeight", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ElevationDecoderRequiresStrongSubclassAndMandatoryFields()
    {
        var missingSubclass = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(10, "10"),
            new(20, "20"),
            new(1, "1.500")
        });
        var missingText = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, "TDbSymbElevation"),
            new(10, "10"),
            new(20, "20")
        });
        var malformedPoint = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, "TDbSymbElevation"),
            new(10, "not-a-number"),
            new(20, "20"),
            new(1, "1.500")
        });
        var truncated = new CadDxfCustomPayload(Payload("1.500", includeScale: true).Groups, true);

        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_ELEVATION", ElevationClass(), missingSubclass));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_ELEVATION", ElevationClass(), missingText));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_ELEVATION", ElevationClass(), malformedPoint));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_ELEVATION", ElevationClass(), truncated));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("VENDOR_ELEVATION", VendorClass(), Payload("1.500", includeScale: true)));
    }

    [Fact]
    public void NonPositiveOrMalformedOptionalPlotScaleDoesNotBlockCoreElevationEvidence()
    {
        var zeroScale = new CadDxfCustomPayload(Payload("2.100", includeScale: false).Groups.Concat(new[] { new CadRawDxfGroup(47, "0") }).ToArray());
        var malformedScale = new CadDxfCustomPayload(Payload("2.100", includeScale: false).Groups.Concat(new[] { new CadRawDxfGroup(47, "bad") }).ToArray());

        var zero = Assert.IsType<CadTianzhengElevationSemantic>(CadTianzhengSemanticDecoder.Decode("TCH_ELEVATION", ElevationClass(), zeroScale));
        var malformed = Assert.IsType<CadTianzhengElevationSemantic>(CadTianzhengSemanticDecoder.Decode("TCH_ELEVATION", ElevationClass(), malformedScale));

        Assert.Null(zero.PlotScale);
        Assert.Null(malformed.PlotScale);
        Assert.Equal("2.100", zero.Text);
        Assert.Equal("2.100", malformed.Text);
    }

    private static CadDxfCustomPayload Payload(string text, bool includeScale)
    {
        var groups = new List<CadRawDxfGroup>
        {
            new(100, "TDbEntity"),
            new(100, "TDbSymbWithText"),
            new(100, "TDbSymbElevation"),
            new(10, "100"),
            new(20, "200"),
            new(30, "0"),
            new(1, text)
        };
        if (includeScale) groups.Add(new CadRawDxfGroup(47, "50"));
        return new CadDxfCustomPayload(groups);
    }

    private static CadCustomClassDefinition ElevationClass()
        => new("TCH_ELEVATION", "TDbSymbElevation", "Tianzheng Architecture", 603, 1, true, "None", false);

    private static CadCustomClassDefinition VendorClass()
        => new("VENDOR_ELEVATION", "TDbSymbElevation", "Vendor", 903, 1, true, "None", false);

    private static void WriteDxfWithElevationClass(string path)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = "TCH_ELEVATION",
            CppClassName = "TDbSymbElevation",
            ApplicationName = "Tianzheng Architecture",
            ClassNumber = 603,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectElevation(string path, string handle, IReadOnlyList<(int Code, string Value)> groups)
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
            "  0", "TCH_ELEVATION",
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
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tch-elevation-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
