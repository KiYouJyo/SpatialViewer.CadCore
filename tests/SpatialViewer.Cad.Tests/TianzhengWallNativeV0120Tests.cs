using System.Text;
using ACadSharp.Classes;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class TianzhengWallNativeV0120Tests
{
    private const string PublishedPackedWall = "MwA3ADkAOAA4ADIALAAzADgAMwA0ADUANAAsAC0AMgA3ADYANQA1ADAALAAtADIANwA2ADUANQAwACwAMAAsADAALAA3ADUALAA3ADUA";

    [Fact]
    public async Task LegacyDirectWallProfileSurvivesRealReaderAndCreatesNativeOutline()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "tch-wall-direct.dxf");
            WriteDxfWithWallClass(path);
            InjectWall(path, "7FFFFE82", new (int, string)[]
            {
                (100, "TDbCurveEntity"),
                (46, "0.0"),
                (47, "100.0"),
                (68, "0"),
                (100, "TDbWall"),
                (38, "0.0"),
                (39, "3000.0"),
                (10, "11496.0"),
                (20, "14750.0"),
                (30, "0.0"),
                (11, "17227.3"),
                (21, "16381.5"),
                (31, "0.0"),
                (40, "100.0"),
                (41, "100.0"),
                (42, "80.0")
            });

            var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(path));
            var document = Assert.IsType<CadDocument>(result.Document);
            var custom = Assert.Single(document.ModelSpace.OfType<CadCustomEntity>());
            var wall = Assert.IsType<CadTianzhengWallSemantic>(custom.NativeSemantics);

            Assert.True(result.IsSuccess);
            Assert.Equal(CadTianzhengSemanticDecoder.WallDirectProfile, wall.DecoderProfile);
            Assert.Equal(new Point2D(11496.0, 14750.0), wall.Start);
            Assert.Equal(new Point2D(17227.3, 16381.5), wall.End);
            Assert.Equal(100.0, wall.LeftWidth, 6);
            Assert.Equal(100.0, wall.RightWidth, 6);
            Assert.Equal(3000.0, wall.Height);
            Assert.Equal(bool.TrueString, custom.Metadata["NativeSemanticsDecoded"]);

            var item = Assert.Single(document.Scene.GetItems());
            Assert.Equal(custom.ObjectId, item.Id);
            Assert.IsType<PolygonGeometry>(item.Geometry);
            Assert.Equal(bool.TrueString, item.Metadata["NativeSemanticsDecoded"]);
            Assert.Equal(CadTianzhengSemanticDecoder.WallDirectProfile, item.Metadata["NativeDecoderProfile"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PackedGroup300ProfileDecodesPublishedWallEvidence()
    {
        var payload = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, "AcDbEntity"),
            new(100, "TDbCurveEntity"),
            new(100, "TDbWall"),
            new(38, "0.0"),
            new(39, "3000.0"),
            new(300, PublishedPackedWall)
        });
        var definition = WallClass();

        var wall = Assert.IsType<CadTianzhengWallSemantic>(
            CadTianzhengSemanticDecoder.Decode("TCH_WALL", definition, payload));

        Assert.Equal(CadTianzhengSemanticDecoder.WallPacked300Profile, wall.DecoderProfile);
        Assert.Equal(new Point2D(379882, -276550), wall.Start);
        Assert.Equal(new Point2D(383454, -276550), wall.End);
        Assert.Equal(75, wall.LeftWidth, 6);
        Assert.Equal(75, wall.RightWidth, 6);
        Assert.Equal(150, wall.TotalWidth, 6);
        Assert.Equal(0, wall.BaseElevation);
        Assert.Equal(3000, wall.Height);
    }

    [Fact]
    public void MalformedPackedScientificNotationIsRejectedInsteadOfGuessed()
    {
        var malformed = "3.09328%+006,3.09888e+006,1.15625e+006,1.15625%+006,0,0,100,100";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(malformed));
        var payload = new CadDxfCustomPayload(new CadRawDxfGroup[]
        {
            new(100, "TDbWall"),
            new(300, encoded)
        });

        Assert.Null(CadTianzhengSemanticDecoder.Decode("TCH_WALL", WallClass(), payload));
    }

    [Fact]
    public void NativeWallGeometrySuppressesProxyFallbackWithoutDiscardingProxyEvidence()
    {
        var wall = new CadTianzhengWallSemantic(
            new Point2D(0, 0),
            new Point2D(1000, 0),
            75,
            125,
            0,
            3000,
            CadTianzhengSemanticDecoder.WallDirectProfile);
        var custom = new CadCustomEntity("CAFE", "TCH_WALL")
        {
            ClassDefinition = WallClass(),
            NativeSemantics = wall,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline" },
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[] { new Point2D(0, 0), new Point2D(9999, 9999) })
            }
        };
        var document = new CadDocument(
            "native-wall.dxf",
            "DXF",
            "AC1032",
            CadUnits.Millimetres,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });

        var item = Assert.Single(document.Scene.GetItems());
        var polygon = Assert.IsType<PolygonGeometry>(item.Geometry);
        Assert.Equal(4, polygon.Points.Count);
        Assert.Equal(new BoundingBox2D(0, -125, 1000, 75), item.Bounds);
        Assert.Equal(bool.TrueString, item.Metadata["NativeSemanticsDecoded"]);
        Assert.Equal(bool.FalseString, item.Metadata["CustomProxyFallback"]);
        Assert.Equal(bool.TrueString, item.Metadata["ProxyFallbackSuppressedByNativeSemantics"]);
        Assert.DoesNotContain(document.Scene.GetItems(), candidate => candidate.Bounds.MaxX > 1000);
        Assert.Single(custom.ProxyPrimitives);
    }

    private static CadCustomClassDefinition WallClass()
        => new("TCH_WALL", "TDbWall", "Tianzheng Architecture", 601, 1, true, "None", false);

    private static void WriteDxfWithWallClass(string path)
    {
        var source = new global::ACadSharp.CadDocument();
        source.CreateDefaults();
        source.Classes.Add(new DxfClass
        {
            DxfName = "TCH_WALL",
            CppClassName = "TDbWall",
            ApplicationName = "Tianzheng Architecture",
            ClassNumber = 601,
            InstanceCount = 1,
            IsAnEntity = true
        });

        using var writer = new DxfWriter(path, source, false);
        writer.Write();
    }

    private static void InjectWall(string path, string handle, IReadOnlyList<(int Code, string Value)> groups)
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
            "  0", "TCH_WALL",
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
        var path = Path.Combine(Path.GetTempPath(), $"cadcore-tch-wall-v0120-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
