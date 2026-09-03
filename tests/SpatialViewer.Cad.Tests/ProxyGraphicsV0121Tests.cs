using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyGraphicsV0121Tests
{
    [Fact]
    public void ReaderIndependentProxyTextReachesSceneWithPresentationFields()
    {
        var definition = new CadCustomClassDefinition("TCH_AXIS_LABEL", "TDbAxisLabel", "Tianzheng Architecture", 501, 1, true, "None", false);
        var custom = new CadCustomEntity("PX-TEXT", "TCH_AXIS_LABEL")
        {
            ClassDefinition = definition,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "UnicodeText" },
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyText(new Point2D(10, 20), "轴1", 2.5, Math.PI / 2, 0.8, 0.1, "UnicodeText")
            }
        };
        var document = new CadDocument(
            "proxy-text.dwg",
            "DWG",
            "AC1032",
            CadUnits.Unitless,
            new[] { new CadLayer("0", CadColor.FromAci(7)) },
            Array.Empty<CadBlockDefinition>(),
            new CadEntity[] { custom });

        var item = Assert.Single(document.Scene.GetItems().Where(item => item.Id == custom.ObjectId));
        var text = Assert.IsType<TextGeometry>(item.Geometry);

        Assert.Equal("轴1", text.Text);
        Assert.Equal(2.5, text.Height, 12);
        Assert.Equal(0.8, text.WidthFactor, 12);
        Assert.Equal(0.1, text.ObliqueAngleRadians, 12);
        Assert.Equal(TextVerticalAlignment2D.Baseline, text.VerticalAlignment);
        Assert.Equal("UnicodeText", item.Metadata["ProxySourceKind"]);
        Assert.Equal(bool.TrueString, item.Metadata["CustomProxyFallback"]);
        Assert.Equal(bool.FalseString, item.Metadata["NativeSemanticsDecoded"]);
        Assert.Equal(new Point2D(10, 20), item.Transform.Apply(new Point2D(10, 20)));
        var rotated = item.Transform.Apply(new Point2D(11, 20));
        Assert.Equal(10, rotated.X, 12);
        Assert.Equal(21, rotated.Y, 12);
    }

    [Fact]
    public void ACadSharpMappingRetainsPlainAndUnicodeProxyText()
    {
        var graphics = new IProxyGeometry[]
        {
            new ProxyText
            {
                Normal = CSMath.XYZ.AxisZ,
                StartPoint = new CSMath.XYZ(1, 2, 0),
                Text = "A1",
                Height = 2,
                TextDirection = new CSMath.XYZ(1, 0, 0),
                WidthFactor = 0.75,
                ObliqueAngle = 0.05
            },
            new ProxyUnicodeText
            {
                Normal = CSMath.XYZ.AxisZ,
                StartPoint = new CSMath.XYZ(4, 5, 0),
                Text = "轴2",
                Height = 3,
                TextDirection = new CSMath.XYZ(0, 1, 0),
                WidthFactor = 1,
                ObliqueAngle = 0
            }
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(graphics, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(0, unsupported);
        Assert.Equal(2, mapped.Count);
        var plain = Assert.IsType<CadProxyText>(mapped[0]);
        var unicode = Assert.IsType<CadProxyText>(mapped[1]);
        Assert.Equal("A1", plain.Text);
        Assert.Equal("Text", plain.SourceKind);
        Assert.Equal(0, plain.RotationRadians, 12);
        Assert.Equal("轴2", unicode.Text);
        Assert.Equal("UnicodeText", unicode.SourceKind);
        Assert.Equal(Math.PI / 2, unicode.RotationRadians, 12);
    }

    [Fact]
    public void PolylineWithNormalRequiresPlanarNormalInsteadOfFallingThroughBaseType()
    {
        var planar = new ProxyPolylineWithNormal
        {
            Normal = CSMath.XYZ.AxisZ,
            Points = new() { new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0) }
        };
        var tilted = new ProxyPolylineWithNormal
        {
            Normal = new CSMath.XYZ(0, 0.5, Math.Sqrt(0.75)),
            Points = new() { new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0) }
        };

        var planarMapped = ACadSharpProxyGraphicsMapping.Map(new IProxyGeometry[] { planar }, out var planarUnsupported, out var planarStateful);
        var tiltedMapped = ACadSharpProxyGraphicsMapping.Map(new IProxyGeometry[] { tilted }, out var tiltedUnsupported, out var tiltedStateful);

        Assert.False(planarStateful);
        Assert.Equal(0, planarUnsupported);
        Assert.IsType<CadProxyPolyline>(Assert.Single(planarMapped));
        Assert.False(tiltedStateful);
        Assert.Equal(1, tiltedUnsupported);
        Assert.Empty(tiltedMapped);
    }

    [Fact]
    public void UnsafeProxyTextFailsClosed()
    {
        var negativeNormal = new ProxyUnicodeText
        {
            Normal = new CSMath.XYZ(0, 0, -1),
            StartPoint = new CSMath.XYZ(1, 2, 0),
            Text = "轴3",
            Height = 2,
            TextDirection = new CSMath.XYZ(1, 0, 0),
            WidthFactor = 1
        };
        var zeroDirection = new ProxyText
        {
            Normal = CSMath.XYZ.AxisZ,
            StartPoint = new CSMath.XYZ(1, 2, 0),
            Text = "A",
            Height = 2,
            TextDirection = new CSMath.XYZ(0, 0, 0),
            WidthFactor = 1
        };
        var invalidHeight = new ProxyText
        {
            Normal = CSMath.XYZ.AxisZ,
            StartPoint = new CSMath.XYZ(1, 2, 0),
            Text = "A",
            Height = 0,
            TextDirection = new CSMath.XYZ(1, 0, 0),
            WidthFactor = 1
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(
            new IProxyGeometry[] { negativeNormal, zeroDirection, invalidHeight },
            out var unsupported,
            out var stateful);

        Assert.False(stateful);
        Assert.Equal(3, unsupported);
        Assert.Empty(mapped);
    }
}
