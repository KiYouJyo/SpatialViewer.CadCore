using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// A privacy-safe controlled experiment intent for one Tianzheng semantic investigation.
/// The case names the property intentionally changed by the researcher, but does not assign
/// any DXF group to that property. Research-only cases can collect comparable evidence without
/// becoming eligible to clear a release gate.
/// </summary>
public sealed record CadTianzhengProbeExperimentCase(
    string Id,
    string DxfName,
    string ParameterIntent,
    bool CanClearReleaseGate = true);

/// <summary>
/// Canonical v0.12 probe experiments. The release-gate catalog remains narrow while explicitly
/// separated research-only cases can investigate version/identity drift without semantic aliasing.
/// </summary>
public static class CadTianzhengProbeExperimentCases
{
    public const string AxisLabelText = "AXIS_LABEL_TEXT";
    public const string DrawingIndexText = "DRAWING_INDEX_TEXT";
    public const string IndexPointerText = "INDEX_POINTER_TEXT";
    public const string DimensionPlotScale = "DIMENSION_PLOT_SCALE";
    public const string ModernDimensionPlotScale = "DIMENSION_PLOT_SCALE_MODERN";

    private static readonly IReadOnlyDictionary<string, CadTianzhengProbeExperimentCase> GateCases =
        new ReadOnlyDictionary<string, CadTianzhengProbeExperimentCase>(
            new Dictionary<string, CadTianzhengProbeExperimentCase>(StringComparer.Ordinal)
            {
                [AxisLabelText] = new(AxisLabelText, "TCH_AXIS_LABEL", "displayed axis label text/number"),
                [DrawingIndexText] = new(DrawingIndexText, "TCH_DRAWINGINDEX", "displayed drawing-index text/number"),
                [IndexPointerText] = new(IndexPointerText, "TCH_INDEXPOINTER", "displayed index-pointer text/number"),
                [DimensionPlotScale] = new(DimensionPlotScale, "TCH_DIMENSION2", "dimension plot/output scale")
            });

    private static readonly IReadOnlyDictionary<string, CadTianzhengProbeExperimentCase> ResearchCases =
        new ReadOnlyDictionary<string, CadTianzhengProbeExperimentCase>(
            new Dictionary<string, CadTianzhengProbeExperimentCase>(StringComparer.Ordinal)
            {
                [ModernDimensionPlotScale] = new(
                    ModernDimensionPlotScale,
                    "TCH_DIMENSION",
                    "dimension plot/output scale on modern identity",
                    CanClearReleaseGate: false)
            });

    /// <summary>The four formal v0.12 release-gate experiments only.</summary>
    public static IReadOnlyCollection<CadTianzhengProbeExperimentCase> All
        => new ReadOnlyCollection<CadTianzhengProbeExperimentCase>(GateCases.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());

    /// <summary>Canonical research-only cases that can collect evidence but cannot clear release gates.</summary>
    public static IReadOnlyCollection<CadTianzhengProbeExperimentCase> ResearchOnly
        => new ReadOnlyCollection<CadTianzhengProbeExperimentCase>(ResearchCases.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());

    public static CadTianzhengProbeExperimentCase Resolve(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var normalized = id.Trim().ToUpperInvariant();
        if (GateCases.TryGetValue(normalized, out var gateCase)) return gateCase;
        if (ResearchCases.TryGetValue(normalized, out var researchCase)) return researchCase;
        throw new FormatException($"Unknown Tianzheng probe experiment case '{normalized}'.");
    }
}

/// <summary>One parsed TCHDIFF observation bound to its declared controlled experiment intent.</summary>
public sealed record CadTianzhengProbeExperimentObservation(
    CadTianzhengProbeExperimentCase ExperimentCase,
    CadTianzhengProbeDiff Diff);

/// <summary>
/// Consensus for exactly one experiment intent. Stable slots remain anonymous evidence and are not
/// promoted to a semantic mapping by this type.
/// </summary>
public sealed record CadTianzhengProbeExperimentConsensus(
    CadTianzhengProbeExperimentCase ExperimentCase,
    CadTianzhengProbeConsensus StructuralConsensus)
{
    public bool HasStableCandidate => StructuralConsensus.HasStableCandidate;
}

/// <summary>
/// Parses the optional case-bound protocol emitted by the v0.12 probe. Only a fixed case ID is
/// retained in addition to the existing privacy-safe structural TCHDIFF output; raw values remain ignored.
/// </summary>
public static class CadTianzhengProbeExperimentParser
{
    private const string CasePrefix = "[TCHDIFF] Case=";
    private const int MaxInputChars = 1_048_576;
    private const int MaxObservations = 10_000;

    public static CadTianzhengProbeExperimentObservation Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaxInputChars)
            throw new ArgumentException("Probe output exceeds the 1 MiB input limit.", nameof(text));

        string? caseId = null;
        foreach (var rawLine in Lines(text))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(CasePrefix, StringComparison.Ordinal)) continue;
            if (caseId is not null) throw new FormatException("Duplicate TCHDIFF experiment case.");
            caseId = line[CasePrefix.Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(caseId))
            throw new FormatException("Case-bound TCHDIFF output is missing its experiment case.");

        var experimentCase = CadTianzhengProbeExperimentCases.Resolve(caseId);
        var diff = CadTianzhengProbeOutputParser.ParseDiff(text);
        if (!string.Equals(experimentCase.DxfName, diff.DxfName, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("TCHDIFF experiment case does not match the selected object type.");

        return new CadTianzhengProbeExperimentObservation(experimentCase, diff);
    }

    public static CadTianzhengProbeExperimentConsensus BuildConsensus(
        CadTianzhengProbeSignature signature,
        IEnumerable<CadTianzhengProbeExperimentObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent case-bound observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"At most {MaxObservations} case-bound observations are supported.", nameof(observations));

        var firstObservation = items[0]
            ?? throw new ArgumentException("Case-bound observation cannot be null.", nameof(observations));
        var firstDeclaredCase = firstObservation.ExperimentCase
            ?? throw new ArgumentException("Experiment case cannot be null.", nameof(observations));
        CadTianzhengProbeExperimentCase canonicalCase;
        try
        {
            canonicalCase = CadTianzhengProbeExperimentCases.Resolve(firstDeclaredCase.Id);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new ArgumentException("Experiment case is not a canonical v0.12 probe case.", nameof(observations), exception);
        }

        if (!string.Equals(canonicalCase.DxfName, firstDeclaredCase.DxfName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Declared experiment case object identity is not canonical.", nameof(observations));
        if (canonicalCase.CanClearReleaseGate != firstDeclaredCase.CanClearReleaseGate)
            throw new ArgumentException("Declared experiment case release-gate scope is not canonical.", nameof(observations));
        if (!string.Equals(canonicalCase.DxfName, signature.DxfName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Experiment case object type does not match the TCHSIG signature.", nameof(signature));

        foreach (var observation in items)
        {
            if (observation is null)
                throw new ArgumentException("Case-bound observation cannot be null.", nameof(observations));
            if (observation.ExperimentCase is null)
                throw new ArgumentException("Experiment case cannot be null.", nameof(observations));
            if (observation.Diff is null)
                throw new ArgumentException("Case-bound TCHDIFF observation cannot be null.", nameof(observations));
            if (!string.Equals(canonicalCase.Id, observation.ExperimentCase.Id, StringComparison.Ordinal))
                throw new ArgumentException("Cannot mix different Tianzheng experiment cases in one consensus.", nameof(observations));
            if (!string.Equals(canonicalCase.DxfName, observation.ExperimentCase.DxfName, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Experiment case object identity is not canonical.", nameof(observations));
            if (canonicalCase.CanClearReleaseGate != observation.ExperimentCase.CanClearReleaseGate)
                throw new ArgumentException("Experiment case release-gate scope is not canonical.", nameof(observations));
        }

        var structural = CadTianzhengProbeOutputParser.BuildConsensus(signature, items.Select(item => item.Diff));
        return new CadTianzhengProbeExperimentConsensus(canonicalCase, structural);
    }

    private static string[] Lines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
}
