namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Allows controlled A/B experiments for an otherwise-unknown custom class only when that exact class
/// disappeared from a known-Xiangyuan native-vs-converted experiment. This is candidate-scoped evidence:
/// it never changes the global vendor classifier and never names proprietary fields.
/// </summary>
public static class CadXiangyuanCandidateExperimentAnalyzer
{
    private const int MaxObservations = 10_000;

    public static IReadOnlyList<CadXiangyuanConversionClassDelta> GetUnknownRemovedEntityCandidates(
        CadXiangyuanConversionDiffReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.Classes
            .Where(IsUnknownRemovedEntityCandidate)
            .OrderBy(candidate => candidate.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.CppClassName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static CadDxfCustomExperimentObservation ObserveDxf(
        CadXiangyuanConversionClassDelta candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ValidateCandidate(candidate);
        ValidateEntity(candidate, before, nameof(before));
        ValidateEntity(candidate, after, nameof(after));
        return CadCustomExperimentAnalyzer.ObserveDxf(before, after);
    }

    public static CadDwgCustomExperimentObservation ObserveDwg(
        CadXiangyuanConversionClassDelta candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ValidateCandidate(candidate);
        ValidateEntity(candidate, before, nameof(before));
        ValidateEntity(candidate, after, nameof(after));
        return CadCustomExperimentAnalyzer.ObserveDwg(before, after);
    }

    public static CadDxfCustomExperimentConsensus BuildDxfConsensus(
        CadXiangyuanConversionClassDelta candidate,
        IEnumerable<CadDxfCustomExperimentObservation> observations)
    {
        ValidateCandidate(candidate);
        var materialized = Materialize(observations);
        foreach (var observation in materialized) ValidateIdentity(candidate, observation.Identity, nameof(observations));
        return CadCustomExperimentAnalyzer.BuildDxfConsensus(materialized);
    }

    public static CadDwgCustomExperimentConsensus BuildDwgConsensus(
        CadXiangyuanConversionClassDelta candidate,
        IEnumerable<CadDwgCustomExperimentObservation> observations)
    {
        ValidateCandidate(candidate);
        var materialized = Materialize(observations);
        foreach (var observation in materialized) ValidateIdentity(candidate, observation.Identity, nameof(observations));
        return CadCustomExperimentAnalyzer.BuildDwgConsensus(materialized);
    }

    public static CadDxfCustomExperimentObservation ObserveDxf(
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ValidateRepeatedCandidate(candidate);
        ValidateEntity(candidate, before, nameof(before));
        ValidateEntity(candidate, after, nameof(after));
        return CadCustomExperimentAnalyzer.ObserveDxf(before, after);
    }

    public static CadDwgCustomExperimentObservation ObserveDwg(
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        ValidateRepeatedCandidate(candidate);
        ValidateEntity(candidate, before, nameof(before));
        ValidateEntity(candidate, after, nameof(after));
        return CadCustomExperimentAnalyzer.ObserveDwg(before, after);
    }

    public static CadDxfCustomExperimentConsensus BuildDxfConsensus(
        CadXiangyuanConversionClassConsensus candidate,
        IEnumerable<CadDxfCustomExperimentObservation> observations)
    {
        ValidateRepeatedCandidate(candidate);
        var materialized = Materialize(observations);
        foreach (var observation in materialized) ValidateIdentity(candidate, observation.Identity, nameof(observations));
        return CadCustomExperimentAnalyzer.BuildDxfConsensus(materialized);
    }

    public static CadDwgCustomExperimentConsensus BuildDwgConsensus(
        CadXiangyuanConversionClassConsensus candidate,
        IEnumerable<CadDwgCustomExperimentObservation> observations)
    {
        ValidateRepeatedCandidate(candidate);
        var materialized = Materialize(observations);
        foreach (var observation in materialized) ValidateIdentity(candidate, observation.Identity, nameof(observations));
        return CadCustomExperimentAnalyzer.BuildDwgConsensus(materialized);
    }

    private static bool IsUnknownRemovedEntityCandidate(CadXiangyuanConversionClassDelta candidate)
        => candidate.Status == CadXiangyuanConversionDiffStatus.RemovedAfterConversion
            && candidate.PresentInNative
            && !candidate.PresentInConverted
            && candidate.IsEntity
            && candidate.ClassifiedVendor == CadCustomObjectVendor.Unknown
            && CadCustomObjectClassifier.Classify(
                candidate.DxfName,
                candidate.CppClassName,
                candidate.ApplicationName) == CadCustomObjectVendor.Unknown;

    private static void ValidateRepeatedCandidate(CadXiangyuanConversionClassConsensus? candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidate.IsRepeatedRemovedUnknownEntityCandidate)
        {
            throw new ArgumentException(
                "Repeated candidate experiments require an unknown entity class removed in at least two independent controlled conversion pairs with no contradictory retained/added observation.",
                nameof(candidate));
        }
    }

    private static void ValidateCandidate(CadXiangyuanConversionClassDelta? candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!IsUnknownRemovedEntityCandidate(candidate))
        {
            throw new ArgumentException(
                "Candidate-scoped Xiangyuan experiments require an entity class that was removed by the controlled conversion and still classifies as Unknown.",
                nameof(candidate));
        }
    }

    private static void ValidateEntity(
        CadXiangyuanConversionClassDelta candidate,
        CadCustomEntity entity,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(entity, parameterName);
        var definition = entity.ClassDefinition
            ?? throw new ArgumentException("Candidate A/B entities must retain their CLASSES-table definition.", parameterName);
        if (!Same(candidate.DxfName, FirstNonEmpty(definition.DxfName, entity.SourceEntityType))
            || !Same(candidate.CppClassName, definition.CppClassName)
            || !Same(candidate.ApplicationName, definition.ApplicationName))
        {
            throw new ArgumentException("Candidate A/B entity identity does not match the controlled conversion candidate.", parameterName);
        }
    }

    private static void ValidateEntity(
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomEntity entity,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(entity, parameterName);
        var definition = entity.ClassDefinition
            ?? throw new ArgumentException("Repeated-candidate A/B entities must retain their CLASSES-table definition.", parameterName);
        if (!Same(candidate.DxfName, FirstNonEmpty(definition.DxfName, entity.SourceEntityType))
            || !Same(candidate.CppClassName, definition.CppClassName)
            || !Same(candidate.ApplicationName, definition.ApplicationName)
            || candidate.IsEntity != definition.IsEntity
            || candidate.WasProxy != definition.WasProxy
            || !string.Equals(candidate.ProxyFlags, definition.ProxyFlags, StringComparison.Ordinal))
        {
            throw new ArgumentException("Repeated-candidate A/B entity structure does not match the conversion consensus candidate.", parameterName);
        }
    }

    private static void ValidateIdentity(
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomExperimentIdentity identity,
        string parameterName)
    {
        if (!Same(candidate.DxfName, identity.DxfName)
            || !Same(candidate.CppClassName, identity.CppClassName)
            || !Same(candidate.ApplicationName, identity.ApplicationName))
        {
            throw new ArgumentException("Repeated-candidate consensus observation identity does not match the conversion consensus candidate.", parameterName);
        }
    }

    private static void ValidateIdentity(
        CadXiangyuanConversionClassDelta candidate,
        CadCustomExperimentIdentity identity,
        string parameterName)
    {
        if (!Same(candidate.DxfName, identity.DxfName)
            || !Same(candidate.CppClassName, identity.CppClassName)
            || !Same(candidate.ApplicationName, identity.ApplicationName))
        {
            throw new ArgumentException("Candidate consensus observation identity does not match the controlled conversion candidate.", parameterName);
        }
    }

    private static List<T> Materialize<T>(IEnumerable<T> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var materialized = observations.Take(MaxObservations + 1).ToList();
        if (materialized.Count > MaxObservations)
            throw new ArgumentException($"Candidate experiment consensus supports at most {MaxObservations} observations.", nameof(observations));
        return materialized;
    }

    private static bool Same(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(string? first, string second)
        => string.IsNullOrWhiteSpace(first) ? second : first;
}
