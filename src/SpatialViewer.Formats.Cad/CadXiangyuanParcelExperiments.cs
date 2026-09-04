using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

public enum CadXiangyuanParcelExperimentEvidenceKind
{
    RawPayloadValue,
    GeometryOrRelationship
}

public enum CadXiangyuanParcelExperimentProvenance
{
    ExplicitXiangyuanIdentity,
    RepeatedConversionCandidate
}

/// <summary>
/// Canonical single-variable research intent for one Xiangyuan parcel experiment.
/// The case names what the researcher intentionally changed; it never assigns a DXF slot,
/// DWG byte range, proxy primitive, or relationship to that semantic.
/// </summary>
public sealed record CadXiangyuanParcelExperimentCase(
    string Id,
    string ParameterIntent,
    CadXiangyuanParcelExperimentEvidenceKind EvidenceKind);

public static class CadXiangyuanParcelExperimentCases
{
    public const string ParcelNumber = "PARCEL_NUMBER";
    public const string LandUseCode = "LAND_USE_CODE";
    public const string LandUseNature = "LAND_USE_NATURE";
    public const string Area = "AREA";
    public const string FarMin = "FAR_MIN";
    public const string FarMax = "FAR_MAX";
    public const string BuildingDensityMin = "BUILDING_DENSITY_MIN";
    public const string BuildingDensityMax = "BUILDING_DENSITY_MAX";
    public const string GreenRateMin = "GREEN_RATE_MIN";
    public const string GreenRateMax = "GREEN_RATE_MAX";
    public const string HeightMin = "HEIGHT_MIN";
    public const string HeightMax = "HEIGHT_MAX";
    public const string Boundary = "BOUNDARY";
    public const string ControlIndicatorRelationship = "CONTROL_INDICATOR_RELATIONSHIP";

    private static readonly IReadOnlyDictionary<string, CadXiangyuanParcelExperimentCase> Cases =
        new ReadOnlyDictionary<string, CadXiangyuanParcelExperimentCase>(
            new Dictionary<string, CadXiangyuanParcelExperimentCase>(StringComparer.Ordinal)
            {
                [ParcelNumber] = new(ParcelNumber, "displayed parcel number / parcel identifier", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [LandUseCode] = new(LandUseCode, "land-use classification code", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [LandUseNature] = new(LandUseNature, "land-use nature / displayed land-use designation", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [Area] = new(Area, "parcel area / derived area output from parcel geometry", CadXiangyuanParcelExperimentEvidenceKind.GeometryOrRelationship),
                [FarMin] = new(FarMin, "minimum floor-area ratio bound", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [FarMax] = new(FarMax, "maximum floor-area ratio bound", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [BuildingDensityMin] = new(BuildingDensityMin, "minimum building-density bound", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [BuildingDensityMax] = new(BuildingDensityMax, "maximum building-density bound", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [GreenRateMin] = new(GreenRateMin, "minimum green-rate bound", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [GreenRateMax] = new(GreenRateMax, "maximum green-rate bound", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [HeightMin] = new(HeightMin, "minimum building-height bound", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [HeightMax] = new(HeightMax, "maximum building-height bound", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
                [Boundary] = new(Boundary, "parcel boundary geometry while non-geometric attributes are held constant", CadXiangyuanParcelExperimentEvidenceKind.GeometryOrRelationship),
                [ControlIndicatorRelationship] = new(ControlIndicatorRelationship, "parcel-to-control-indicator object relationship", CadXiangyuanParcelExperimentEvidenceKind.GeometryOrRelationship)
            });

    public static IReadOnlyCollection<CadXiangyuanParcelExperimentCase> All
        => new ReadOnlyCollection<CadXiangyuanParcelExperimentCase>(
            Cases.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());

    public static IReadOnlyCollection<CadXiangyuanParcelExperimentCase> RawPayloadValueCases
        => new ReadOnlyCollection<CadXiangyuanParcelExperimentCase>(
            Cases.Values
                .Where(item => item.EvidenceKind == CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue)
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToArray());

    public static CadXiangyuanParcelExperimentCase Resolve(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var normalized = id.Trim().ToUpperInvariant();
        if (Cases.TryGetValue(normalized, out var experimentCase)) return experimentCase;
        throw new FormatException($"Unknown Xiangyuan parcel experiment case '{normalized}'.");
    }

    internal static CadXiangyuanParcelExperimentCase ValidateCanonical(
        CadXiangyuanParcelExperimentCase experimentCase,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(experimentCase, parameterName);
        CadXiangyuanParcelExperimentCase canonical;
        try
        {
            canonical = Resolve(experimentCase.Id);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new ArgumentException("Parcel experiment case is not canonical.", parameterName, exception);
        }

        if (canonical != experimentCase)
            throw new ArgumentException("Parcel experiment case metadata is not canonical.", parameterName);
        return canonical;
    }
}

public sealed record CadXiangyuanParcelDxfExperimentObservation(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadDxfCustomExperimentObservation Observation);

public sealed record CadXiangyuanParcelDwgExperimentObservation(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadDwgCustomExperimentObservation Observation);

public sealed record CadXiangyuanParcelDxfExperimentConsensus(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadDxfCustomExperimentConsensus StructuralConsensus)
{
    public bool HasStableCandidate => StructuralConsensus.HasStableCandidate;
}

public sealed record CadXiangyuanParcelDwgExperimentConsensus(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadDwgCustomExperimentConsensus StructuralConsensus)
{
    public bool HasStableCandidate => StructuralConsensus.HasStableCandidate;
}

public sealed record CadXiangyuanParcelGeometryExperimentObservation(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadProxyGeometryExperimentObservation Observation);

public sealed record CadXiangyuanParcelGeometryExperimentConsensus(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadProxyGeometryExperimentConsensus StructuralConsensus)
{
    public bool HasStableCandidate => StructuralConsensus.HasStableCandidate;
}

public sealed record CadXiangyuanParcelReferenceExperimentObservation(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadCustomHandleReferenceExperimentObservation Observation);

public sealed record CadXiangyuanParcelReferenceExperimentConsensus(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadCustomHandleReferenceExperimentConsensus StructuralConsensus)
{
    public bool HasStableCandidate => StructuralConsensus.HasStableCandidate;
}

public sealed record CadXiangyuanParcelReferenceEndpointExperimentObservation(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadCustomReferenceEndpointExperimentObservation Observation);

public sealed record CadXiangyuanParcelReferenceEndpointExperimentConsensus(
    CadXiangyuanParcelExperimentCase ExperimentCase,
    CadXiangyuanParcelExperimentProvenance Provenance,
    CadCustomReferenceEndpointExperimentConsensus StructuralConsensus);

/// <summary>
/// Case-bound Xiangyuan parcel A/B research. This layer prevents observations from different intentionally
/// changed parcel properties from being mixed into one consensus. Stable slots/ranges remain anonymous.
/// </summary>
public static class CadXiangyuanParcelExperimentAnalyzer
{
    private const int MaxObservations = 10_000;

    public static CadXiangyuanParcelDxfExperimentObservation ObserveExplicitDxf(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var canonical = ValidateRawPayloadCase(experimentCase, nameof(experimentCase));
        var observation = CadXiangyuanExperimentAnalyzer.ObserveDxf(before, after);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, observation);
    }

    public static CadXiangyuanParcelDwgExperimentObservation ObserveExplicitDwg(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var canonical = ValidateRawPayloadCase(experimentCase, nameof(experimentCase));
        var observation = CadXiangyuanExperimentAnalyzer.ObserveDwg(before, after);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, observation);
    }

    public static CadXiangyuanParcelDxfExperimentObservation ObserveCandidateDxf(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var canonical = ValidateRawPayloadCase(experimentCase, nameof(experimentCase));
        var observation = CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(candidate, before, after);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate, observation);
    }

    public static CadXiangyuanParcelDwgExperimentObservation ObserveCandidateDwg(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var canonical = ValidateRawPayloadCase(experimentCase, nameof(experimentCase));
        var observation = CadXiangyuanCandidateExperimentAnalyzer.ObserveDwg(candidate, before, after);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate, observation);
    }

    public static CadXiangyuanParcelDxfExperimentConsensus BuildExplicitDxfConsensus(
        IEnumerable<CadXiangyuanParcelDxfExperimentObservation> observations)
    {
        var items = Materialize(
            observations,
            CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity);
        var structural = CadXiangyuanExperimentAnalyzer.BuildDxfConsensus(items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelDwgExperimentConsensus BuildExplicitDwgConsensus(
        IEnumerable<CadXiangyuanParcelDwgExperimentObservation> observations)
    {
        var items = Materialize(
            observations,
            CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity);
        var structural = CadXiangyuanExperimentAnalyzer.BuildDwgConsensus(items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelDxfExperimentConsensus BuildCandidateDxfConsensus(
        CadXiangyuanConversionClassConsensus candidate,
        IEnumerable<CadXiangyuanParcelDxfExperimentObservation> observations)
    {
        var items = Materialize(
            observations,
            CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate);
        var structural = CadXiangyuanCandidateExperimentAnalyzer.BuildDxfConsensus(
            candidate,
            items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelDwgExperimentConsensus BuildCandidateDwgConsensus(
        CadXiangyuanConversionClassConsensus candidate,
        IEnumerable<CadXiangyuanParcelDwgExperimentObservation> observations)
    {
        var items = Materialize(
            observations,
            CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate);
        var structural = CadXiangyuanCandidateExperimentAnalyzer.BuildDwgConsensus(
            candidate,
            items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelGeometryExperimentObservation ObserveExplicitGeometry(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var canonical = ValidateProxyGeometryCase(experimentCase, nameof(experimentCase));
        CadXiangyuanExperimentAnalyzer.ValidateXiangyuanPair(before, after);
        var observation = CadProxyGeometryExperimentAnalyzer.Observe(before, after);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, observation);
    }

    public static CadXiangyuanParcelGeometryExperimentObservation ObserveCandidateGeometry(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var canonical = ValidateProxyGeometryCase(experimentCase, nameof(experimentCase));
        CadXiangyuanCandidateExperimentAnalyzer.ValidateRepeatedCandidate(candidate);
        CadXiangyuanCandidateExperimentAnalyzer.ValidateEntity(candidate, before, nameof(before));
        CadXiangyuanCandidateExperimentAnalyzer.ValidateEntity(candidate, after, nameof(after));
        var observation = CadProxyGeometryExperimentAnalyzer.Observe(before, after);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate, observation);
    }

    public static CadXiangyuanParcelGeometryExperimentConsensus BuildExplicitGeometryConsensus(
        IEnumerable<CadXiangyuanParcelGeometryExperimentObservation> observations)
    {
        var items = MaterializeGeometry(
            observations,
            CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity);
        CadXiangyuanExperimentAnalyzer.ValidateXiangyuanIdentities(
            items.Select(item => item.Observation.Identity));
        var structural = CadProxyGeometryExperimentAnalyzer.BuildConsensus(
            items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelGeometryExperimentConsensus BuildCandidateGeometryConsensus(
        CadXiangyuanConversionClassConsensus candidate,
        IEnumerable<CadXiangyuanParcelGeometryExperimentObservation> observations)
    {
        CadXiangyuanCandidateExperimentAnalyzer.ValidateRepeatedCandidate(candidate);
        var items = MaterializeGeometry(
            observations,
            CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate);
        foreach (var item in items)
            CadXiangyuanCandidateExperimentAnalyzer.ValidateIdentity(
                candidate,
                item.Observation.Identity,
                nameof(observations));
        var structural = CadProxyGeometryExperimentAnalyzer.BuildConsensus(
            items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelReferenceExperimentObservation ObserveExplicitReference(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var canonical = ValidateReferenceCase(experimentCase, nameof(experimentCase));
        CadXiangyuanExperimentAnalyzer.ValidateXiangyuanPair(before, after);
        var observation = CadCustomHandleReferenceExperimentAnalyzer.Observe(before, after);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, observation);
    }

    public static CadXiangyuanParcelReferenceExperimentObservation ObserveCandidateReference(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        var canonical = ValidateReferenceCase(experimentCase, nameof(experimentCase));
        CadXiangyuanCandidateExperimentAnalyzer.ValidateRepeatedCandidate(candidate);
        CadXiangyuanCandidateExperimentAnalyzer.ValidateEntity(candidate, before, nameof(before));
        CadXiangyuanCandidateExperimentAnalyzer.ValidateEntity(candidate, after, nameof(after));
        var observation = CadCustomHandleReferenceExperimentAnalyzer.Observe(before, after);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate, observation);
    }

    public static CadXiangyuanParcelReferenceExperimentConsensus BuildExplicitReferenceConsensus(
        IEnumerable<CadXiangyuanParcelReferenceExperimentObservation> observations)
    {
        var items = MaterializeReference(
            observations,
            CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity);
        CadXiangyuanExperimentAnalyzer.ValidateXiangyuanIdentities(
            items.Select(item => item.Observation.Identity));
        var structural = CadCustomHandleReferenceExperimentAnalyzer.BuildConsensus(
            items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelReferenceExperimentConsensus BuildCandidateReferenceConsensus(
        CadXiangyuanConversionClassConsensus candidate,
        IEnumerable<CadXiangyuanParcelReferenceExperimentObservation> observations)
    {
        CadXiangyuanCandidateExperimentAnalyzer.ValidateRepeatedCandidate(candidate);
        var items = MaterializeReference(
            observations,
            CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate);
        foreach (var item in items)
            CadXiangyuanCandidateExperimentAnalyzer.ValidateIdentity(
                candidate,
                item.Observation.Identity,
                nameof(observations));
        var structural = CadCustomHandleReferenceExperimentAnalyzer.BuildConsensus(
            items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelReferenceEndpointExperimentObservation ObserveExplicitReferenceEndpoint(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadDocument beforeDocument,
        CadCustomEntity before,
        CadDocument afterDocument,
        CadCustomEntity after,
        CadCustomHandleReferenceValueChange slot)
    {
        var canonical = ValidateReferenceCase(experimentCase, nameof(experimentCase));
        CadXiangyuanExperimentAnalyzer.ValidateXiangyuanPair(before, after);
        var observation = CadCustomReferenceEndpointExperimentAnalyzer.Observe(
            beforeDocument,
            before,
            afterDocument,
            after,
            slot);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity, observation);
    }

    public static CadXiangyuanParcelReferenceEndpointExperimentObservation ObserveCandidateReferenceEndpoint(
        CadXiangyuanParcelExperimentCase experimentCase,
        CadXiangyuanConversionClassConsensus candidate,
        CadDocument beforeDocument,
        CadCustomEntity before,
        CadDocument afterDocument,
        CadCustomEntity after,
        CadCustomHandleReferenceValueChange slot)
    {
        var canonical = ValidateReferenceCase(experimentCase, nameof(experimentCase));
        CadXiangyuanCandidateExperimentAnalyzer.ValidateRepeatedCandidate(candidate);
        CadXiangyuanCandidateExperimentAnalyzer.ValidateEntity(candidate, before, nameof(before));
        CadXiangyuanCandidateExperimentAnalyzer.ValidateEntity(candidate, after, nameof(after));
        var observation = CadCustomReferenceEndpointExperimentAnalyzer.Observe(
            beforeDocument,
            before,
            afterDocument,
            after,
            slot);
        return new(canonical, CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate, observation);
    }

    public static CadXiangyuanParcelReferenceEndpointExperimentConsensus BuildExplicitReferenceEndpointConsensus(
        IEnumerable<CadXiangyuanParcelReferenceEndpointExperimentObservation> observations)
    {
        var items = MaterializeReferenceEndpoint(
            observations,
            CadXiangyuanParcelExperimentProvenance.ExplicitXiangyuanIdentity);
        CadXiangyuanExperimentAnalyzer.ValidateXiangyuanIdentities(
            items.Select(item => item.Observation.SourceIdentity));
        var structural = CadCustomReferenceEndpointExperimentAnalyzer.BuildConsensus(
            items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    public static CadXiangyuanParcelReferenceEndpointExperimentConsensus BuildCandidateReferenceEndpointConsensus(
        CadXiangyuanConversionClassConsensus candidate,
        IEnumerable<CadXiangyuanParcelReferenceEndpointExperimentObservation> observations)
    {
        CadXiangyuanCandidateExperimentAnalyzer.ValidateRepeatedCandidate(candidate);
        var items = MaterializeReferenceEndpoint(
            observations,
            CadXiangyuanParcelExperimentProvenance.RepeatedConversionCandidate);
        foreach (var item in items)
            CadXiangyuanCandidateExperimentAnalyzer.ValidateIdentity(
                candidate,
                item.Observation.SourceIdentity,
                nameof(observations));
        var structural = CadCustomReferenceEndpointExperimentAnalyzer.BuildConsensus(
            items.Select(item => item.Observation));
        return new(items[0].ExperimentCase, items[0].Provenance, structural);
    }

    private static List<CadXiangyuanParcelReferenceEndpointExperimentObservation> MaterializeReferenceEndpoint(
        IEnumerable<CadXiangyuanParcelReferenceEndpointExperimentObservation> observations,
        CadXiangyuanParcelExperimentProvenance expectedProvenance)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent case-bound parcel reference-endpoint observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"Parcel reference-endpoint consensus supports at most {MaxObservations} observations.", nameof(observations));

        foreach (var item in items)
        {
            if (item is null || item.Observation is null)
                throw new ArgumentException("Parcel reference-endpoint observation cannot be null.", nameof(observations));
            ValidateReferenceCase(item.ExperimentCase, nameof(observations));
            if (item.Provenance != expectedProvenance)
                throw new ArgumentException("Cannot mix parcel reference-endpoint provenance modes in one consensus.", nameof(observations));
        }
        return items;
    }

    private static CadXiangyuanParcelExperimentCase ValidateReferenceCase(
        CadXiangyuanParcelExperimentCase experimentCase,
        string parameterName)
    {
        var canonical = CadXiangyuanParcelExperimentCases.ValidateCanonical(experimentCase, parameterName);
        if (!string.Equals(
                canonical.Id,
                CadXiangyuanParcelExperimentCases.ControlIndicatorRelationship,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Custom-reference parcel experiments are restricted to CONTROL_INDICATOR_RELATIONSHIP.",
                parameterName);
        }
        return canonical;
    }

    private static List<CadXiangyuanParcelReferenceExperimentObservation> MaterializeReference(
        IEnumerable<CadXiangyuanParcelReferenceExperimentObservation> observations,
        CadXiangyuanParcelExperimentProvenance expectedProvenance)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent case-bound parcel reference observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"Parcel reference consensus supports at most {MaxObservations} observations.", nameof(observations));

        foreach (var item in items)
        {
            if (item is null || item.Observation is null)
                throw new ArgumentException("Parcel reference observation cannot be null.", nameof(observations));
            ValidateReferenceCase(item.ExperimentCase, nameof(observations));
            if (item.Provenance != expectedProvenance)
                throw new ArgumentException("Cannot mix parcel reference experiment provenance modes in one consensus.", nameof(observations));
        }
        return items;
    }

    private static CadXiangyuanParcelExperimentCase ValidateProxyGeometryCase(
        CadXiangyuanParcelExperimentCase experimentCase,
        string parameterName)
    {
        var canonical = CadXiangyuanParcelExperimentCases.ValidateCanonical(experimentCase, parameterName);
        if (!string.Equals(canonical.Id, CadXiangyuanParcelExperimentCases.Area, StringComparison.Ordinal)
            && !string.Equals(canonical.Id, CadXiangyuanParcelExperimentCases.Boundary, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Proxy-geometry parcel experiments are restricted to AREA or BOUNDARY; object relationships require reference evidence.",
                parameterName);
        }
        return canonical;
    }

    private static List<CadXiangyuanParcelGeometryExperimentObservation> MaterializeGeometry(
        IEnumerable<CadXiangyuanParcelGeometryExperimentObservation> observations,
        CadXiangyuanParcelExperimentProvenance expectedProvenance)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent case-bound parcel geometry observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"Parcel geometry consensus supports at most {MaxObservations} observations.", nameof(observations));

        CadXiangyuanParcelExperimentCase? firstCase = null;
        foreach (var item in items)
        {
            if (item is null || item.Observation is null)
                throw new ArgumentException("Parcel geometry observation cannot be null.", nameof(observations));
            var canonical = ValidateProxyGeometryCase(item.ExperimentCase, nameof(observations));
            if (item.Provenance != expectedProvenance)
                throw new ArgumentException("Cannot mix parcel geometry experiment provenance modes in one consensus.", nameof(observations));
            firstCase ??= canonical;
            if (!string.Equals(firstCase.Id, canonical.Id, StringComparison.Ordinal))
                throw new ArgumentException("Cannot mix AREA and BOUNDARY parcel geometry cases in one consensus.", nameof(observations));
        }
        return items;
    }

    private static CadXiangyuanParcelExperimentCase ValidateRawPayloadCase(
        CadXiangyuanParcelExperimentCase experimentCase,
        string parameterName)
    {
        var canonical = CadXiangyuanParcelExperimentCases.ValidateCanonical(experimentCase, parameterName);
        if (canonical.EvidenceKind != CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue)
        {
            throw new ArgumentException(
                "Geometry/relationship parcel cases cannot be passed through raw-payload value consensus.",
                parameterName);
        }
        return canonical;
    }

    private static List<T> Materialize<T>(
        IEnumerable<T> observations,
        CadXiangyuanParcelExperimentProvenance expectedProvenance)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent case-bound parcel observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"Parcel experiment consensus supports at most {MaxObservations} observations.", nameof(observations));

        CadXiangyuanParcelExperimentCase? firstCase = null;
        foreach (var item in items)
        {
            if (item is null)
                throw new ArgumentException("Parcel experiment observation cannot be null.", nameof(observations));

            CadXiangyuanParcelExperimentCase experimentCase;
            CadXiangyuanParcelExperimentProvenance provenance;
            switch (item)
            {
                case CadXiangyuanParcelDxfExperimentObservation dxf:
                    experimentCase = dxf.ExperimentCase;
                    provenance = dxf.Provenance;
                    if (dxf.Observation is null)
                        throw new ArgumentException("Parcel DXF observation cannot be null.", nameof(observations));
                    break;
                case CadXiangyuanParcelDwgExperimentObservation dwg:
                    experimentCase = dwg.ExperimentCase;
                    provenance = dwg.Provenance;
                    if (dwg.Observation is null)
                        throw new ArgumentException("Parcel DWG observation cannot be null.", nameof(observations));
                    break;
                default:
                    throw new ArgumentException("Unsupported parcel experiment observation type.", nameof(observations));
            }

            var canonical = ValidateRawPayloadCase(experimentCase, nameof(observations));
            if (provenance != expectedProvenance)
                throw new ArgumentException("Cannot mix parcel experiment provenance modes in one consensus.", nameof(observations));
            firstCase ??= canonical;
            if (!string.Equals(firstCase.Id, canonical.Id, StringComparison.Ordinal))
                throw new ArgumentException("Cannot mix different Xiangyuan parcel experiment cases in one consensus.", nameof(observations));
        }

        return items;
    }
}
