namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Xiangyuan-specific guard around the generic privacy-safe custom-object A/B analyzer.
/// This type adds vendor identity requirements only; it does not assign semantic names to changed DXF slots
/// or DWG byte ranges.
/// </summary>
public static class CadXiangyuanExperimentAnalyzer
{
    private const int MaxObservations = 10_000;
    public static CadDxfCustomExperimentObservation ObserveDxf(
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ValidateXiangyuanPair(before, after);
        return CadCustomExperimentAnalyzer.ObserveDxf(before, after);
    }

    public static CadDwgCustomExperimentObservation ObserveDwg(
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ValidateXiangyuanPair(before, after);
        return CadCustomExperimentAnalyzer.ObserveDwg(before, after);
    }

    public static CadDxfCustomExperimentConsensus BuildDxfConsensus(
        IEnumerable<CadDxfCustomExperimentObservation> observations)
    {
        var materialized = Materialize(observations);
        ValidateXiangyuanIdentities(materialized.Select(observation => observation.Identity));
        return CadCustomExperimentAnalyzer.BuildDxfConsensus(materialized);
    }

    public static CadDwgCustomExperimentConsensus BuildDwgConsensus(
        IEnumerable<CadDwgCustomExperimentObservation> observations)
    {
        var materialized = Materialize(observations);
        ValidateXiangyuanIdentities(materialized.Select(observation => observation.Identity));
        return CadCustomExperimentAnalyzer.BuildDwgConsensus(materialized);
    }

    private static void ValidateXiangyuanPair(CadCustomEntity before, CadCustomEntity after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (!before.IsXiangyuan || !after.IsXiangyuan)
            throw new ArgumentException(
                "Xiangyuan A/B experiments require both entities to have explicit Xiangyuan vendor identity.",
                nameof(after));
    }

    private static void ValidateXiangyuanIdentities(IEnumerable<CadCustomExperimentIdentity> identities)
    {
        foreach (var identity in identities)
        {
            if (!CadCustomObjectClassifier.IsXiangyuan(
                    identity.DxfName,
                    identity.CppClassName,
                    identity.ApplicationName))
            {
                throw new ArgumentException(
                    "Xiangyuan consensus cannot include observations whose retained identity is not explicitly Xiangyuan.",
                    nameof(identities));
            }
        }
    }

    private static List<T> Materialize<T>(IEnumerable<T> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var materialized = observations.Take(MaxObservations + 1).ToList();
        if (materialized.Count > MaxObservations)
            throw new ArgumentException($"Xiangyuan experiment consensus supports at most {MaxObservations} observations.", nameof(observations));
        return materialized;
    }
}
