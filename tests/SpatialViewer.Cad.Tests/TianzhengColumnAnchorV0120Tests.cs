using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengColumnAnchorV0120Tests
{
    [Fact]
    public async Task PublishedColumnPointElevenSurvivesRealTextDxfReader()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tch-column-anchor.dxf");
            WriteDxfWithColumnClass(path);
            InjectColumn(path, "7FFFFA51", new (int, string)[]
            {
                (11, "12500.25"),
                (21, "8600.75"),
                (31, "300.0"),
                (40, "500"),
                (41, "700"),
                (50, "0.785398")
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var column = Assert.IsType<CadTianzhengColumnAnchorSemantic>(custom.NativeSemantics);

            Assert.True(result.IsSuccess);
            Assert.Equal(CadTianzhengSemanticDecoder.ColumnAnchorDirectProfile, column.DecoderProfile);
            Assert.Equal(new Point2D(12500.25, 8600.75), column.InsertionPoint);
            Assert.Equal(300.0, column.Elevation);
            Assert.Equal(CadCustomSemanticCoverage.Partial, column.Coverage);
            Assert.False(column.IsDrawable2D);
            Assert.Equal(bool.TrueString, custom.Metadata["NativeSemanticEvidenceDecoded"]);
            Assert.Equal(nameof(CadCustomSemanticCoverage.Partial), custom.Metadata["NativeSemanticCoverage"]);
            Assert.Equal(bool.FalseString, custom.Metadata["NativeSemanticDrawable2D"]);
            Assert.Equal(nameof(CadTianzhengColumnAnchorSemantic), custom.Metadata["NativeSemanticType"]);
            Assert.Empty(document.Scene.GetItems());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ColumnAnchorDecoderRequiresExactIdentityAndFinitePointEleven()
    {
        var valid = Payload("100", "200", "300");
        var malformedX = Payload("not-a-number", "200", "300");
        var missingY = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(11, "100"),
            new(31, "300")
        });
        var truncated = new CadDxfCustomPayload(valid.Groups, true);

        var semantic = Assert.IsType<CadTianzhengColumnAnchorSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_COLUMN", ColumnClass(), valid));

        Assert.Equal(new Point2D(100, 200), semantic.InsertionPoint);
        Assert.Equal(300, semantic.Elevation);
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_COLUMN", ColumnClass(), malformedX));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_COLUMN", ColumnClass(), missingY));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_COLUMN", ColumnClass(), truncated));
        Assert.Null(CadTianzhengSemanticDecoder.Decode(
            "VENDOR_COLUMN",
            new CadCustomClassDefinition("VENDOR_COLUMN", "OpaqueColumnClass", "Vendor", 950, 1, true, "None", false),
            valid));
    }

    [Fact]
    public void ColumnAnchorDoesNotInventSectionRotationOrHeightFields()
    {
        var payload = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(11, "100"),
            new(21, "200"),
            new(31, "0"),
            new(40, "500"),
            new(41, "700"),
            new(50, "0.785398"),
            new(39, "3000")
        });

        var semantic = Assert.IsType<CadTianzhengColumnAnchorSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_COLUMN", ColumnClass(), payload));
        var properties = typeof(CadTianzhengColumnAnchorSemantic).GetProperties();

        Assert.Equal(new Point2D(100, 200), semantic.InsertionPoint);
        Assert.Equal(CadCustomSemanticCoverage.Partial, semantic.Coverage);
        Assert.False(semantic.IsDrawable2D);
        Assert.DoesNotContain(properties, property => property.Name.Contains("Width", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Height", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Rotation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Angle", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Shape", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Section", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(7, payload.Groups.Count);
    }

    [Fact]
    public void PartialColumnSemanticKeepsProxyGraphicsAsDisplayFallback()
    {
        var semantic = new CadTianzhengColumnAnchorSemantic(
            new Point2D(100, 200),
            0,
            CadTianzhengSemanticDecoder.ColumnAnchorDirectProfile);
        var custom = new CadCustomEntity("C011", "TCH_COLUMN")
        {
            ClassDefinition = ColumnClass(),
            NativeSemantics = semantic,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline" },
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[]
                {
                    new Point2D(50, 150),
                    new Point2D(150, 250)
                })
            }
        };
        var document = Document(custom);

        var item = Assert.Single(document.Scene.GetItems());

        Assert.IsType<PolylineGeometry>(item.Geometry);
        Assert.Equal(new BoundingBox2D(50, 150, 150, 250), item.Bounds);
        Assert.Equal(bool.TrueString, item.Metadata["CustomProxyFallback"]);
        Assert.Equal(bool.FalseString, item.Metadata["NativeSemanticsDecoded"]);
        Assert.Equal(CadCustomSemanticCoverage.Partial, semantic.Coverage);
        Assert.False(semantic.IsDrawable2D);
    }

    private static CadDxfCustomPayload Payload(string x, string y, string z)
        => new(new CadRawDxfGroup[]
        {
            new(11, x),
            new(21, y),
            new(31, z)
        });

    private static CadCustomClassDefinition ColumnClass()
        => new("TCH_COLUMN", "OpaqueColumnClass", "Tianzheng Architecture", 606, 1, true, "None", false);

    private static CadDocument Document(params CadEntity[] entities)
        => new(
            "column-anchor.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            entities);

    private static void WriteDxfWithColumnClass(string path)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = "TCH_COLUMN",
            CppClassName = "OpaqueColumnClass",
            ApplicationName = "Tianzheng Architecture",
            ClassNumber = 606,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectColumn(string path, string handle, IReadOnlyList<(int Code, string Value)> groups)
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
            "  0", "TCH_COLUMN",
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
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tch-column-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
