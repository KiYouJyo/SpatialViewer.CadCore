using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Repeatability evidence for one exact custom-class structural identity across independent
/// known-Xiangyuan native-vs-converted drawing pairs.
/// </summary>
public sealed record CadXiangyuanConversionClassConsensus(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    CadCustomObjectVendor ClassifiedVendor,
    bool IsEntity,
    bool WasProxy,
    string ProxyFlags,
    int ObservedPairCount,
    int RemovedPairCount,
    int RetainedPairCount,
    int AddedPairCount)
{
    /// <summary>
    /// A repeatable discovery candidate only. This is not a Xiangyuan classification and not a parcel-semantic claim.
    /// </summary>
    public bool IsRepeatedRemovedUnknownEntityCandidate
        => ObservedPairCount >= 2
            && RemovedPairCount == ObservedPairCount
            && RetainedPairCount == 0
            && AddedPairCount == 0
            && IsEntity
            && ClassifiedVendor == CadCustomObjectVendor.Unknown
            && CadCustomObjectClassifier.Classify(DxfName, CppClassName, ApplicationName) == CadCustomObjectVendor.Unknown;
}

/// <summary>Repeatability evidence for one exact custom-entity structural profile.</summary>
public sealed record CadXiangyuanConversionProfileConsensus(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    CadCustomObjectVendor ClassifiedVendor,
    string SchemaFingerprint,
    string GroupCodeSignature,
    string SubclassMarkerSignature,
    string ReferenceCodeSignature,
    string ProxyGraphicKindSignature,
    int ObservedPairCount,
    int RemovedPairCount,
    int RetainedPairCount,
    int AddedPairCount)
{
    public bool IsRepeatedRemovedUnknownProfileCandidate
        => ObservedPairCount >= 2
            && RemovedPairCount == ObservedPairCount
            && RetainedPairCount == 0
            && AddedPairCount == 0
            && ClassifiedVendor == CadCustomObjectVendor.Unknown
            && CadCustomObjectClassifier.Classify(DxfName, CppClassName, ApplicationName) == CadCustomObjectVendor.Unknown;
}

/// <summary>Aggregated privacy-safe evidence from two or more independent conversion pairs.</summary>
public sealed record CadXiangyuanConversionConsensusReport(
    int SchemaVersion,
    int PairCount,
    IReadOnlyList<CadXiangyuanConversionClassConsensus> Classes,
    IReadOnlyList<CadXiangyuanConversionProfileConsensus> Profiles)
{
    public int RepeatedRemovedUnknownEntityCandidateCount
        => Classes.Count(item => item.IsRepeatedRemovedUnknownEntityCandidate);
    public int RepeatedRemovedUnknownProfileCandidateCount
        => Profiles.Count(item => item.IsRepeatedRemovedUnknownProfileCandidate);
}

public static class CadXiangyuanConversionConsensus
{
    public const int CurrentSchemaVersion = 1;
    private const int MaxPairs = 10_000;

    public static CadXiangyuanConversionConsensusReport Build(
        IEnumerable<CadXiangyuanConversionDiffReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var materialized = reports.Take(MaxPairs + 1).ToArray();
        if (materialized.Length < 2)
            throw new ArgumentException("At least two independent Xiangyuan conversion pairs are required.", nameof(reports));
        if (materialized.Length > MaxPairs)
            throw new ArgumentException($"At most {MaxPairs} Xiangyuan conversion pairs are supported.", nameof(reports));

        foreach (var report in materialized)
            CadXiangyuanConversionDiffer.ValidateReport(report, nameof(reports));

        var classObservations = materialized
            .SelectMany((report, pairIndex) => report.Classes.Select(item => new IndexedClass(pairIndex, item)))
            .GroupBy(item => ClassKey.Create(item.Delta))
            .Select(group => BuildClassConsensus(group.Key, group))
            .OrderBy(item => item.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CppClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProxyFlags, StringComparer.Ordinal)
            .ToArray();

        var profileObservations = materialized
            .SelectMany((report, pairIndex) => report.Profiles.Select(item => new IndexedProfile(pairIndex, item)))
            .GroupBy(item => ProfileKey.Create(item.Delta))
            .Select(group => BuildProfileConsensus(group.Key, group))
            .OrderBy(item => item.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SchemaFingerprint, StringComparer.Ordinal)
            .ThenBy(item => item.ReferenceCodeSignature, StringComparer.Ordinal)
            .ThenBy(item => item.ProxyGraphicKindSignature, StringComparer.Ordinal)
            .ToArray();

        return new CadXiangyuanConversionConsensusReport(
            CurrentSchemaVersion,
            materialized.Length,
            new ReadOnlyCollection<CadXiangyuanConversionClassConsensus>(classObservations),
            new ReadOnlyCollection<CadXiangyuanConversionProfileConsensus>(profileObservations));
    }

    public static IReadOnlyList<CadXiangyuanConversionClassConsensus> GetRepeatedRemovedUnknownEntityCandidates(
        CadXiangyuanConversionConsensusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValidateConsensusReport(report, nameof(report));
        return report.Classes
            .Where(item => item.IsRepeatedRemovedUnknownEntityCandidate)
            .OrderByDescending(item => item.ObservedPairCount)
            .ThenBy(item => item.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CppClassName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static void ValidateConsensusReport(
        CadXiangyuanConversionConsensusReport report,
        string parameterName)
    {
        if (report.SchemaVersion != CurrentSchemaVersion)
            throw new ArgumentException($"Unsupported Xiangyuan conversion-consensus version: {report.SchemaVersion}.", parameterName);
        if (report.PairCount < 2 || report.PairCount > MaxPairs)
            throw new ArgumentException($"Xiangyuan conversion consensus must represent 2..{MaxPairs} pairs.", parameterName);
        if (report.Classes is null || report.Profiles is null)
            throw new ArgumentException("Xiangyuan conversion consensus classes/profiles cannot be null.", parameterName);
        foreach (var item in report.Classes)
            ValidateCounts(item.ObservedPairCount, item.RemovedPairCount, item.RetainedPairCount, item.AddedPairCount, report.PairCount, parameterName);
        foreach (var item in report.Profiles)
            ValidateCounts(item.ObservedPairCount, item.RemovedPairCount, item.RetainedPairCount, item.AddedPairCount, report.PairCount, parameterName);
    }

    private static CadXiangyuanConversionClassConsensus BuildClassConsensus(
        ClassKey key,
        IEnumerable<IndexedClass> observations)
    {
        var items = observations.ToArray();
        EnsureOneObservationPerPair(items.Select(item => item.PairIndex), "class");
        return new CadXiangyuanConversionClassConsensus(
            key.DxfName,
            key.CppClassName,
            key.ApplicationName,
            key.ClassifiedVendor,
            key.IsEntity,
            key.WasProxy,
            key.ProxyFlags,
            items.Length,
            items.Count(item => item.Delta.Status == CadXiangyuanConversionDiffStatus.RemovedAfterConversion),
            items.Count(item => item.Delta.Status == CadXiangyuanConversionDiffStatus.RetainedAfterConversion),
            items.Count(item => item.Delta.Status == CadXiangyuanConversionDiffStatus.AddedAfterConversion));
    }

    private static CadXiangyuanConversionProfileConsensus BuildProfileConsensus(
        ProfileKey key,
        IEnumerable<IndexedProfile> observations)
    {
        var items = observations.ToArray();
        EnsureOneObservationPerPair(items.Select(item => item.PairIndex), "profile");
        return new CadXiangyuanConversionProfileConsensus(
            key.DxfName,
            key.CppClassName,
            key.ApplicationName,
            key.ClassifiedVendor,
            key.SchemaFingerprint,
            key.GroupCodeSignature,
            key.SubclassMarkerSignature,
            key.ReferenceCodeSignature,
            key.ProxyGraphicKindSignature,
            items.Length,
            items.Count(item => item.Delta.Status == CadXiangyuanConversionDiffStatus.RemovedAfterConversion),
            items.Count(item => item.Delta.Status == CadXiangyuanConversionDiffStatus.RetainedAfterConversion),
            items.Count(item => item.Delta.Status == CadXiangyuanConversionDiffStatus.AddedAfterConversion));
    }

    private static void EnsureOneObservationPerPair(IEnumerable<int> pairIndices, string kind)
    {
        var indices = pairIndices.ToArray();
        if (indices.Distinct().Count() != indices.Length)
            throw new ArgumentException($"A Xiangyuan conversion pair contains duplicate {kind} structural keys.");
    }

    private static void ValidateCounts(
        int observed,
        int removed,
        int retained,
        int added,
        int pairCount,
        string parameterName)
    {
        if (observed <= 0 || observed > pairCount)
            throw new ArgumentException("Conversion-consensus observed pair count is outside the report pair count.", parameterName);
        if (removed < 0 || retained < 0 || added < 0 || removed + retained + added != observed)
            throw new ArgumentException("Conversion-consensus status counts must exactly equal observed pair count.", parameterName);
    }

    private sealed record IndexedClass(int PairIndex, CadXiangyuanConversionClassDelta Delta);
    private sealed record IndexedProfile(int PairIndex, CadXiangyuanConversionProfileDelta Delta);

    private readonly record struct ClassKey(
        string DxfName,
        string CppClassName,
        string ApplicationName,
        CadCustomObjectVendor ClassifiedVendor,
        bool IsEntity,
        bool WasProxy,
        string ProxyFlags)
    {
        public static ClassKey Create(CadXiangyuanConversionClassDelta item)
            => new(item.DxfName, item.CppClassName, item.ApplicationName, item.ClassifiedVendor, item.IsEntity, item.WasProxy, item.ProxyFlags);
    }

    private readonly record struct ProfileKey(
        string DxfName,
        string CppClassName,
        string ApplicationName,
        CadCustomObjectVendor ClassifiedVendor,
        string SchemaFingerprint,
        string GroupCodeSignature,
        string SubclassMarkerSignature,
        string ReferenceCodeSignature,
        string ProxyGraphicKindSignature)
    {
        public static ProfileKey Create(CadXiangyuanConversionProfileDelta item)
            => new(
                item.DxfName,
                item.CppClassName,
                item.ApplicationName,
                item.ClassifiedVendor,
                item.SchemaFingerprint,
                item.GroupCodeSignature,
                item.SubclassMarkerSignature,
                item.ReferenceCodeSignature,
                item.ProxyGraphicKindSignature);
    }
}
