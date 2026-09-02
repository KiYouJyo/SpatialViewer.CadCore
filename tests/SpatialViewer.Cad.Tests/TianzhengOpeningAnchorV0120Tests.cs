using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengOpeningAnchorV0120Tests
{
    [Fact]
    public async Task PublishedOpeningAnchorProfileSurvivesRealTextDxfReader()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tch-opening-anchor.dxf");
            WriteDxfWithOpeningClass(path);
            InjectOpening(path, "7FFFFD91", new (int, string)[]
            {
                (100, "TDbOpening"),
                (10, "124819.3754"),
                (20, "-80530.6856"),
                (30, "600.0"),
                (90, "2020")
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var opening = Assert.IsType<CadTianzhengOpeningAnchorSemantic>(custom.NativeSemantics);

            Assert.True(result.IsSuccess);
            Assert.Equal(CadTianzhengSemanticDecoder.OpeningAnchorDirectProfile, opening.DecoderProfile);
            Assert.Equal(new Point2D(124819.3754, -80530.6856), opening.InsertionPoint);
            Assert.Equal(600.0, opening.Elevation);
            Assert.Equal(bool.TrueString, custom.Metadata["NativeSemanticsDecoded"]);
            Assert.Equal(nameof(CadTianzhengOpeningAnchorSemantic), custom.Metadata["NativeSemanticType"]);
            Assert.Equal(CadTianzhengSemanticDecoder.OpeningAnchorDirectProfile, custom.Metadata["NativeDecoderProfile"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void OpeningAnchorAndResolvedHostWallRemainSeparateEvidenceLayers()
    {
        var wall = new CadCustomEntity("100", "TCH_WALL")
        {
            ClassDefinition = WallClass(),
            NativeSemantics = new CadTianzhengWallSemantic(
                new Point2D(0, 0),
                new Point2D(1000, 0),
                100,
                100,
                0,
                3000,
                CadTianzhengSemanticDecoder.WallDirectProfile)
        };
        var openingSemantic = Assert.IsType<CadTianzhengOpeningAnchorSemantic>(
            CadTianzhengSemanticDecoder.Decode(
                "TCH_OPENING",
                OpeningClass(),
                OpeningPayload("500", "0", "0")));
        var opening = new CadCustomEntity("200", "TCH_OPENING")
        {
            ClassDefinition = OpeningClass(),
            NativeSemantics = openingSemantic,
            HandleReferences = new CadCustomHandleReference[] { new(330, "100") }
        };
        var document = Document(wall, opening);

        var relationship = Assert.Single(
            CadCustomRelationshipResolver.Resolve(document),
            candidate => candidate.Kind == CadCustomRelationshipKind.TianzhengOpeningHostWall);

        Assert.Equal(new Point2D(500, 0), openingSemantic.InsertionPoint);
        Assert.Equal("200", relationship.SourceHandle);
        Assert.Equal("100", relationship.TargetHandle);
    }

    [Fact]
    public void PartialOpeningSemanticsKeepProxyGraphicsAsDisplayFallback()
    {
        var semantic = new CadTianzhengOpeningAnchorSemantic(
            new Point2D(100, 200),
            0,
            CadTianzhengSemanticDecoder.OpeningAnchorDirectProfile);
        var custom = new CadCustomEntity("CA11", "TCH_OPENING")
        {
            ClassDefinition = OpeningClass(),
            NativeSemantics = semantic,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline" },
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[]
                {
                    new Point2D(90, 190),
                    new Point2D(110, 210)
                })
            }
        };
        var document = Document(custom);

        var item = Assert.Single(document.Scene.GetItems());

        Assert.Null(item.Geometry);
        var child = Assert.Single(item.Children);
        Assert.IsType<PolylineGeometry>(child.Geometry);
        Assert.Equal(new BoundingBox2D(90, 190, 110, 210), item.Bounds);
        Assert.Equal(bool.TrueString, item.Metadata["CustomProxyFallback"]);
        Assert.Equal(bool.FalseString, item.Metadata["NativeSemanticsDecoded"]);
        Assert.Equal(CadTianzhengSemanticDecoder.OpeningAnchorDirectProfile, custom.NativeSemantics?.DecoderProfile);
    }

    [Fact]
    public void OpeningAnchorDecoderRejectsTruncatedMalformedAndUnrelatedPayloads()
    {
        var validGroups = new CadRawDxfGroup[]
        {
            new(100, "TDbOpening"),
            new(10, "10.5"),
            new(20, "20.5"),
            new(30, "30.5")
        };
        var truncated = new CadDxfCustomPayload(validGroups, true);
        var malformed = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, "TDbOpening"),
            new(10, "not-a-number"),
            new(20, "20.5")
        });
        var unrelated = new CadDxfCustomPayload(validGroups);

        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_OPENING", OpeningClass(), truncated));
        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_OPENING", OpeningClass(), malformed));
        Assert.Null(CadTianzhengSemanticDecoder.Decode(
            "VENDOR_OPENING",
            new CadCustomClassDefinition("VENDOR_OPENING", "VendorOpening", "Vendor", 900, 1, true, "None", false),
            unrelated));
    }

    [Fact]
    public void AnchorSemanticDoesNotInventOpeningDimensions()
    {
        var semantic = Assert.IsType<CadTianzhengOpeningAnchorSemantic>(
            CadTianzhengSemanticDecoder.Decode(
                "TCH_OPENING",
                OpeningClass(),
                OpeningPayload("100", "200", "300")));

        Assert.Equal(new Point2D(100, 200), semantic.InsertionPoint);
        Assert.Equal(300, semantic.Elevation);
        Assert.DoesNotContain(
            typeof(CadTianzhengOpeningAnchorSemantic).GetProperties(),
            property => property.Name.Contains("Width", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Height", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Sill", StringComparison.OrdinalIgnoreCase));
    }

    private static CadDxfCustomPayload OpeningPayload(string x, string y, string z)
        => new(new CadRawDxfGroup[]
        {
            new(100, "TDbOpening"),
            new(10, x),
            new(20, y),
            new(30, z)
        });

    private static CadCustomClassDefinition OpeningClass()
        => new("TCH_OPENING", "TDbOpening", "Tianzheng Architecture", 602, 1, true, "None", false);

    private static CadCustomClassDefinition WallClass()
        => new("TCH_WALL", "TDbWall", "Tianzheng Architecture", 601, 1, true, "None", false);

    private static CadDocument Document(params CadEntity[] entities)
        => new(
            "opening-anchor.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            entities);

    private static void WriteDxfWithOpeningClass(string path)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = "TCH_OPENING",
            CppClassName = "TDbOpening",
            ApplicationName = "Tianzheng Architecture",
            ClassNumber = 602,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectOpening(string path, string handle, IReadOnlyList<(int Code, string Value)> groups)
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
            "  0", "TCH_OPENING",
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
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tch-opening-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
