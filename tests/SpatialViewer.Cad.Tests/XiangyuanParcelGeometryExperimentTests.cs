using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Cad.Tests;

public sealed class XiangyuanParcelGeometryExperimentTests
{
    private static readonly CadCustomClassDefinition XiangyuanClass = new(
        "XY_PARCEL_PROXY",
        "XiangyuanParcelProxy",
        "LzxSoft Control Planning CAD",
        1401,
        1,
        true,
        "None",
        true);

    private static readonly CadCustomClassDefinition CandidateClass = new(
        "PRIVATE_PARCEL_PROXY",
        "PrivateParcelProxy",
        "PrivatePlanningApp",
        1402,
        1,
        true,
        "EraseAllowed",
        true);

    [Fact]
    public void ExplicitBoundaryConsensusRetainsRepeatableAnonymousVertexSlot()
    {
        var boundary = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.Boundary);
        var first = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitGeometry(
            boundary,
            XiangyuanEntity("100", 0, 0),
            XiangyuanEntity("101", 1, 0));
        var second = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitGeometry(
            boundary,
            XiangyuanEntity("200", 20, 20),
            XiangyuanEntity("201", 21, 20));

        var consensus = CadXiangyuanParcelExperimentAnalyzer.BuildExplicitGeometryConsensus(new[] { first, second });

        Assert.Equal(CadXiangyuanParcelExperimentCases.Boundary, consensus.ExperimentCase.Id);
        Assert.Equal(CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, consensus.Provenance);
        Assert.True(consensus.HasStableCandidate);
        var stable = Assert.Single(consensus.StructuralConsensus.StableValueChanges);
        Assert.Equal(CadProxyGeometryField.PointX, stable.Field);
        Assert.Equal(0, stable.ElementIndex);
    }

    [Fact]
    public void RepeatedUnknownCandidateCanResearchAreaGeometryWithoutVendorPromotion()
    {
        var area = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.Area);
        var candidate = RepeatedCandidate();
        var first = CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateGeometry(
            area,
            candidate,
            CandidateEntity("300", 0, 0),
            CandidateEntity("301", 1, 0));
        var second = CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateGeometry(
            area,
            candidate,
            CandidateEntity("400", 10, 10),
            CandidateEntity("401", 11, 10));

        var consensus = CadXiangyuanParcelExperimentAnalyzer.BuildCandidateGeometryConsensus(
            candidate,
            new[] { first, second });

        Assert.Equal(CadXiangyuanParcelExperimentCases.Area, consensus.ExperimentCase.Id);
        Assert.True(consensus.HasStableCandidate);
        Assert.Equal(
            CadCustomObjectVendor.Unknown,
            CadCustomObjectClassifier.Classify(
                CandidateClass.DxfName,
                CandidateClass.CppClassName,
                CandidateClass.ApplicationName));
    }

    [Fact]
    public void GeometryAnalyzerRejectsRawValueAndRelationshipCases()
    {
        var far = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.FarMax);
        var relationship = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.ControlIndicatorRelationship);

        Assert.Throws<ArgumentException>(() => CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitGeometry(
            far, XiangyuanEntity("500", 0, 0), XiangyuanEntity("501", 1, 0)));
        Assert.Throws<ArgumentException>(() => CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitGeometry(
            relationship, XiangyuanEntity("510", 0, 0), XiangyuanEntity("511", 1, 0)));
    }

    [Fact]
    public void GeometryConsensusRejectsMixingAreaAndBoundaryIntent()
    {
        var boundary = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.Boundary);
        var area = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.Area);
        var first = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitGeometry(
            boundary, XiangyuanEntity("600", 0, 0), XiangyuanEntity("601", 1, 0));
        var second = CadXiangyuanParcelExperimentAnalyzer.ObserveExplicitGeometry(
            area, XiangyuanEntity("610", 10, 10), XiangyuanEntity("611", 11, 10));

        var exception = Assert.Throws<ArgumentException>(() =>
            CadXiangyuanParcelExperimentAnalyzer.BuildExplicitGeometryConsensus(new[] { first, second }));

        Assert.Contains("mix", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CandidateGeometryGateRejectsContradictoryConversionConsensus()
    {
        var boundary = CadXiangyuanParcelExperimentCases.Resolve(CadXiangyuanParcelExperimentCases.Boundary);
        var contradictory = RepeatedCandidate() with { RemovedPairCount = 1, RetainedPairCount = 1 };

        Assert.Throws<ArgumentException>(() => CadXiangyuanParcelExperimentAnalyzer.ObserveCandidateGeometry(
            boundary,
            contradictory,
            CandidateEntity("700", 0, 0),
            CandidateEntity("701", 1, 0)));
    }

    private static CadXiangyuanConversionClassConsensus RepeatedCandidate()
        => new(
            CandidateClass.DxfName,
            CandidateClass.CppClassName,
            CandidateClass.ApplicationName,
            CadCustomObjectVendor.Unknown,
            CandidateClass.IsEntity,
            CandidateClass.WasProxy,
            CandidateClass.ProxyFlags,
            2,
            2,
            0,
            0);

    private static CadCustomEntity XiangyuanEntity(string handle, double firstX, double firstY)
        => Entity(handle, XiangyuanClass, firstX, firstY);

    private static CadCustomEntity CandidateEntity(string handle, double firstX, double firstY)
        => Entity(handle, CandidateClass, firstX, firstY);

    private static CadCustomEntity Entity(
        string handle,
        CadCustomClassDefinition definition,
        double firstX,
        double firstY)
        => new(handle, definition.DxfName)
        {
            ClassDefinition = definition,
            Representation = CadCustomEntityRepresentation.ProxyGraphics,
            ProxyGraphicKinds = new[] { "Polyline" },
            ProxyPrimitives = new CadProxyPrimitive[]
            {
                new CadProxyPolyline(new[]
                {
                    new Point2D(firstX, firstY),
                    new Point2D(100, 0),
                    new Point2D(100, 100),
                    new Point2D(0, 100),
                    new Point2D(firstX, firstY)
                })
            }
        };
}
