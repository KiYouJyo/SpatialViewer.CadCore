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
                [Area] = new(Area, "parcel area value while parcel boundary is held constant", CadXiangyuanParcelExperimentEvidenceKind.RawPayloadValue),
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
