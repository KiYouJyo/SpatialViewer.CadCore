using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// Strength of public, non-project evidence for one controlled Tianzheng gate experiment.
/// ParameterExistence confirms only that the named property exists on the object type.
/// RawFieldMapping additionally identifies the DXF group code/occurrence carrying that property.
/// </summary>
public enum CadTianzhengExternalEvidenceStrength
{
    ParameterExistence,
    RawFieldMapping
}

/// <summary>
/// Privacy-safe external evidence claim. It intentionally stores no drawing value, coordinate, handle,
/// file path, project text, or copied source text. SourceId is only a bounded citation key maintained by
/// the research ledger.
/// </summary>
public sealed record CadTianzhengExternalEvidenceClaim(
    string SourceId,
    string ExperimentCaseId,
    CadTianzhengExternalEvidenceStrength Strength,
    int? GroupCode = null,
    int? CodeOccurrence = null);

/// <summary>
/// Assessment of whether a case-bound probe consensus has enough independent public evidence to begin a
/// named semantic implementation. "Ready" does not itself implement or release the semantic; a real Reader
/// regression and fail-closed decoder are still required by the v0.12 gate.
/// </summary>
public sealed record CadTianzhengSemanticReadiness(
    CadTianzhengProbeExperimentCase ExperimentCase,
    int ObservationCount,
    IReadOnlyList<CadDxfCustomPayloadValueChange> StableCandidates,
    bool HasParameterExistenceEvidence,
    bool HasMatchingRawFieldEvidence,
    CadDxfCustomPayloadValueChange? ReadyCandidate)
{
    public bool ReadyForSemanticImplementation => ReadyCandidate is not null && HasMatchingRawFieldEvidence;
}

/// <summary>
/// Combines anonymous repeatability consensus with independently maintained public evidence without guessing.
/// Parameter-existence material can strengthen a research hypothesis but can never name a raw field. A raw
/// mapping must agree with exactly one stable probe candidate by group code and occurrence. Research-only
/// experiment cases are deliberately rejected here even if they have repeatable raw evidence.
/// </summary>
public static class CadTianzhengSemanticEvidenceAssessor
{
    private const int MaxClaims = 1_000;
    private const int MaxSourceIdLength = 160;

    public static CadTianzhengSemanticReadiness Assess(
        CadTianzhengProbeExperimentConsensus consensus,
        IEnumerable<CadTianzhengExternalEvidenceClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(consensus);
        ArgumentNullException.ThrowIfNull(claims);

        var canonical = CadTianzhengProbeExperimentCases.Resolve(consensus.ExperimentCase.Id);
        if (!canonical.CanClearReleaseGate)
            throw new ArgumentException("Research-only Tianzheng experiment cases cannot clear a release semantic gate.", nameof(consensus));
        if (!string.Equals(canonical.DxfName, consensus.ExperimentCase.DxfName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Experiment consensus is not bound to the canonical object identity.", nameof(consensus));
        if (!string.Equals(canonical.ParameterIntent, consensus.ExperimentCase.ParameterIntent, StringComparison.Ordinal))
            throw new ArgumentException("Experiment consensus is not bound to the canonical parameter intent.", nameof(consensus));
        if (canonical.CanClearReleaseGate != consensus.ExperimentCase.CanClearReleaseGate)
            throw new ArgumentException("Experiment consensus release-gate scope is not canonical.", nameof(consensus));
        if (!string.Equals(canonical.DxfName, consensus.StructuralConsensus.Signature.DxfName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Experiment consensus signature does not match the canonical object identity.", nameof(consensus));
        if (consensus.StructuralConsensus.ObservationCount < 2)
            throw new ArgumentException("Semantic evidence assessment requires at least two independent probe observations.", nameof(consensus));

        var items = claims.Take(MaxClaims + 1).ToList();
        if (items.Count > MaxClaims)
            throw new ArgumentException($"At most {MaxClaims} external evidence claims are supported.", nameof(claims));

        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var rawMappings = new HashSet<(int Code, int Occurrence)>();
        var hasParameterEvidence = false;

        foreach (var claim in items)
        {
            ArgumentNullException.ThrowIfNull(claim);
            var sourceId = ValidateSourceId(claim.SourceId);
            if (!sourceIds.Add(sourceId))
                throw new ArgumentException("External evidence contains a duplicate source id.", nameof(claims));

            var claimCase = CadTianzhengProbeExperimentCases.Resolve(claim.ExperimentCaseId);
            if (!claimCase.CanClearReleaseGate)
                throw new ArgumentException("Research-only Tianzheng evidence cannot be reused as release-gate evidence.", nameof(claims));
            if (!string.Equals(canonical.Id, claimCase.Id, StringComparison.Ordinal))
                throw new ArgumentException("External evidence belongs to a different Tianzheng experiment case.", nameof(claims));

            switch (claim.Strength)
            {
                case CadTianzhengExternalEvidenceStrength.ParameterExistence:
                    if (claim.GroupCode is not null || claim.CodeOccurrence is not null)
                        throw new ArgumentException("Parameter-existence evidence must not pretend to identify a raw DXF field.", nameof(claims));
                    hasParameterEvidence = true;
                    break;

                case CadTianzhengExternalEvidenceStrength.RawFieldMapping:
                    if (claim.GroupCode is null || claim.CodeOccurrence is null)
                        throw new ArgumentException("Raw-field evidence must identify both group code and occurrence.", nameof(claims));
                    ValidateGroup(claim.GroupCode.Value, claim.CodeOccurrence.Value, nameof(claims));
                    hasParameterEvidence = true;
                    rawMappings.Add((claim.GroupCode.Value, claim.CodeOccurrence.Value));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(claims), "Unsupported external evidence strength.");
            }
        }

        if (rawMappings.Count > 1)
            throw new ArgumentException("External raw-field evidence conflicts on group code or occurrence.", nameof(claims));

        var stable = consensus.StructuralConsensus.StableValueChanges
            .OrderBy(item => item.GroupIndex)
            .ThenBy(item => item.Code)
            .ThenBy(item => item.CodeOccurrence)
            .ToArray();

        CadDxfCustomPayloadValueChange? ready = null;
        if (rawMappings.Count == 1)
        {
            var mapping = rawMappings.Single();
            var matches = stable
                .Where(candidate => candidate.Code == mapping.Code && candidate.CodeOccurrence == mapping.Occurrence)
                .ToArray();
            if (matches.Length > 1)
                throw new ArgumentException("Probe consensus contains ambiguous duplicate candidates for one raw mapping.", nameof(consensus));
            if (matches.Length == 1)
                ready = matches[0];
        }

        return new CadTianzhengSemanticReadiness(
            canonical,
            consensus.StructuralConsensus.ObservationCount,
            new ReadOnlyCollection<CadDxfCustomPayloadValueChange>(stable),
            hasParameterEvidence,
            ready is not null,
            ready);
    }

    private static string ValidateSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed.Length > MaxSourceIdLength)
            throw new ArgumentException($"External evidence source id exceeds {MaxSourceIdLength} characters.", nameof(value));
        if (trimmed.Any(character => char.IsControl(character)))
            throw new ArgumentException("External evidence source id contains control characters.", nameof(value));
        return trimmed;
    }

    private static void ValidateGroup(int code, int occurrence, string parameterName)
    {
        if (code is < -5 or > 1071)
            throw new ArgumentException("External evidence group code is outside the DXF range.", parameterName);
        if (occurrence <= 0 || occurrence > 65_536)
            throw new ArgumentException("External evidence group occurrence is out of range.", parameterName);
    }
}
