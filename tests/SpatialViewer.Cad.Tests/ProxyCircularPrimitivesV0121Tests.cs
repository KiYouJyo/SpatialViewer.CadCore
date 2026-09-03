using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Cad.Tests;

public sealed class ProxyCircularPrimitivesV0121Tests
{
    [Fact]
    public void ThreePointCircleMapsToStableAnalyticCircle()
    {
        var proxy = new ProxyCirclePt3
        {
            Point1 = new CSMath.XYZ(0, 10, 125),
            Point2 = new CSMath.XYZ(10, 0, 125),
            Point3 = new CSMath.XYZ(0, -10, 125)
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(new IProxyGeometry[] { proxy }, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(0, unsupported);
        var circle = Assert.IsType<CadProxyCircle>(Assert.Single(mapped));
        Assert.Equal(0, circle.Center.X, 12);
        Assert.Equal(0, circle.Center.Y, 12);
        Assert.Equal(10, circle.Radius, 12);
    }

    [Fact]
    public void ThreePointSimpleArcUsesMiddlePointToChooseCounterClockwiseSweep()
    {
        var proxy = new ProxyCircularArc3Pt
        {
            Point1 = new CSMath.XYZ(10, 0, 20),
            Point2 = new CSMath.XYZ(0, 10, 20),
            Point3 = new CSMath.XYZ(-10, 0, 20),
            ArcType = 0
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(new IProxyGeometry[] { proxy }, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(0, unsupported);
        var arc = Assert.IsType<CadProxyArc>(Assert.Single(mapped));
        Assert.Equal(0, arc.Center.X, 12);
        Assert.Equal(0, arc.Center.Y, 12);
        Assert.Equal(10, arc.Radius, 12);
        Assert.Equal(0, arc.StartRadians, 12);
        Assert.Equal(Math.PI, arc.SweepRadians, 12);
    }

    [Fact]
    public void ThreePointSimpleArcUsesMiddlePointToChooseClockwiseSweep()
    {
        var proxy = new ProxyCircularArc3Pt
        {
            Point1 = new CSMath.XYZ(10, 0, -15),
            Point2 = new CSMath.XYZ(0, -10, -15),
            Point3 = new CSMath.XYZ(-10, 0, -15),
            ArcType = 0
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(new IProxyGeometry[] { proxy }, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(0, unsupported);
        var arc = Assert.IsType<CadProxyArc>(Assert.Single(mapped));
        Assert.Equal(0, arc.StartRadians, 12);
        Assert.Equal(-Math.PI, arc.SweepRadians, 12);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(3)]
    public void ThreePointArcRejectsUnrepresentableOrUnknownArcTypes(int arcType)
    {
        var proxy = new ProxyCircularArc3Pt
        {
            Point1 = new CSMath.XYZ(10, 0, 0),
            Point2 = new CSMath.XYZ(0, 10, 0),
            Point3 = new CSMath.XYZ(-10, 0, 0),
            ArcType = arcType
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(new IProxyGeometry[] { proxy }, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(1, unsupported);
        Assert.Empty(mapped);
    }

    [Fact]
    public void ThreePointCircularPrimitivesRejectNonPlanarDegenerateAndNonFiniteInput()
    {
        var nonPlanar = new ProxyCirclePt3
        {
            Point1 = new CSMath.XYZ(0, 0, 0),
            Point2 = new CSMath.XYZ(10, 0, 1),
            Point3 = new CSMath.XYZ(0, 10, 0)
        };
        var collinear = new ProxyCirclePt3
        {
            Point1 = new CSMath.XYZ(0, 0, 0),
            Point2 = new CSMath.XYZ(10, 0, 0),
            Point3 = new CSMath.XYZ(20, 0, 0)
        };
        var duplicate = new ProxyCircularArc3Pt
        {
            Point1 = new CSMath.XYZ(0, 0, 0),
            Point2 = new CSMath.XYZ(0, 0, 0),
            Point3 = new CSMath.XYZ(10, 10, 0),
            ArcType = 0
        };
        var nonFinite = new ProxyCircularArc3Pt
        {
            Point1 = new CSMath.XYZ(double.NaN, 0, 0),
            Point2 = new CSMath.XYZ(0, 10, 0),
            Point3 = new CSMath.XYZ(10, 0, 0),
            ArcType = 0
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(
            new IProxyGeometry[] { nonPlanar, collinear, duplicate, nonFinite },
            out var unsupported,
            out var stateful);

        Assert.False(stateful);
        Assert.Equal(4, unsupported);
        Assert.Empty(mapped);
    }

    [Fact]
    public void NearCollinearLargeCoordinateCircleFailsClosedInsteadOfCreatingHugeRadius()
    {
        var proxy = new ProxyCirclePt3
        {
            Point1 = new CSMath.XYZ(1_000_000_000, 1_000_000_000, 0),
            Point2 = new CSMath.XYZ(1_000_001_000, 1_000_000_000, 0),
            Point3 = new CSMath.XYZ(1_000_002_000, 1_000_000_000.0000001, 0)
        };

        var mapped = ACadSharpProxyGraphicsMapping.Map(new IProxyGeometry[] { proxy }, out var unsupported, out var stateful);

        Assert.False(stateful);
        Assert.Equal(1, unsupported);
        Assert.Empty(mapped);
    }
}
