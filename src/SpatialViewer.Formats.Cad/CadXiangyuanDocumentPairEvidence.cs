using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace SpatialViewer.Formats.Cad;

public enum CadXiangyuanDocumentPairProvenance
{
    ExplicitXiangyuanIdentity,
    RepeatedConversionCandidate
}

/// <summary>
/// Privacy-safe whole-document A/B evidence. Entity handles are used only for exact local matching and are
/// never retained. No geometric nearest-neighbour or content heuristic is used to pair custom entities.
/// </summary>
public sealed record CadXiangyuanDocumentPairEvidenceReport(
    int SchemaVersion,
    CadXiangyuanDocumentPairProvenance Provenance,
    int BeforeEligibleEntityCount,
    int AfterEligibleEntityCount,
    int MatchedEntityCount,
    int BeforeOnlyEntityCount,
    int AfterOnlyEntityCount,
    int IdentityMismatchCount,
    int DxfComparablePairCount,
    int DxfChangedPairCount,
    int DwgComparablePairCount,
    int DwgChangedPairCount,
    int GeometryComparablePairCount,
    int GeometryChangedPairCount,
    int ReferenceComparablePairCount,
    int ReferenceChangedPairCount,
    IReadOnlyList<CadDxfCustomExperimentObservation> DxfChanges,
    IReadOnlyList<CadDwgCustomExperimentObservation> DwgChanges,
    IReadOnlyList<CadProxyGeometryExperimentObservation> GeometryChanges,
    IReadOnlyList<CadCustomHandleReferenceExperimentObservation> ReferenceChanges);

public static class CadXiangyuanDocumentPairEvidenceAnalyzer
{
    public const int CurrentSchemaVersion = 1;
    private const int MaxEntities = 1_000_000;
    private const int MaxJsonBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static CadXiangyuanDocumentPairEvidenceReport AnalyzeExplicit(
        CadDocument before,
        CadDocument after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return Analyze(
            before,
            after,
            CadXiangyuanDocumentPairProvenance.ExplicitXiangyuanIdentity,
            entity => entity.IsXiangyuan,
            candidate: null);
    }

    public static CadXiangyuanDocumentPairEvidenceReport AnalyzeCandidate(
        CadXiangyuanConversionClassConsensus candidate,
        CadDocument before,
        CadDocument after)
    {
        CadXiangyuanCandidateExperimentAnalyzer.ValidateRepeatedCandidate(candidate);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return Analyze(
            before,
            after,
            CadXiangyuanDocumentPairProvenance.RepeatedConversionCandidate,
            entity => MatchesCandidate(candidate, entity),
            candidate);
    }

    public static string ToJson(CadXiangyuanDocumentPairEvidenceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValidateReport(report, nameof(report));
        var json = JsonSerializer.Serialize(report, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new ArgumentException($"Xiangyuan document-pair evidence JSON exceeds the {MaxJsonBytes} byte safety limit.", nameof(report));
        return json;
    }

    public static CadXiangyuanDocumentPairEvidenceReport FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new FormatException($"Xiangyuan document-pair evidence JSON exceeds the {MaxJsonBytes} byte safety limit.");
        try
        {
            var report = JsonSerializer.Deserialize<CadXiangyuanDocumentPairEvidenceReport>(json, JsonOptions)
                ?? throw new FormatException("Xiangyuan document-pair evidence JSON did not contain a report.");
            ValidateReport(report, nameof(json));
            return Freeze(report);
        }
        catch (JsonException exception)
        {
            throw new FormatException("Invalid Xiangyuan document-pair evidence JSON.", exception);
        }
    }

    private static CadXiangyuanDocumentPairEvidenceReport Analyze(
        CadDocument before,
        CadDocument after,
        CadXiangyuanDocumentPairProvenance provenance,
        Func<CadCustomEntity, bool> eligible,
        CadXiangyuanConversionClassConsensus? candidate)
    {
        var beforeAll = EnumerateCustomEntities(before).ToArray();
        var afterAll = EnumerateCustomEntities(after).ToArray();
        if (beforeAll.Length > MaxEntities || afterAll.Length > MaxEntities)
            throw new ArgumentException($"Document-pair evidence supports at most {MaxEntities} custom entities per document.");

        var beforeIndex = IndexByStableHandle(beforeAll, nameof(before));
        var afterIndex = IndexByStableHandle(afterAll, nameof(after));
        var beforeEligible = beforeAll.Where(eligible).ToArray();
        var afterEligible = afterAll.Where(eligible).ToArray();
        var consumedAfterHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var matched = 0;
        var beforeOnly = 0;
        var identityMismatch = 0;
        var dxfComparable = 0;
        var dxfChanged = 0;
        var dwgComparable = 0;
        var dwgChanged = 0;
        var geometryComparable = 0;
        var geometryChanged = 0;
        var referenceComparable = 0;
        var referenceChanged = 0;
        var dxfChanges = new List<CadDxfCustomExperimentObservation>();
        var dwgChanges = new List<CadDwgCustomExperimentObservation>();
        var geometryChanges = new List<CadProxyGeometryExperimentObservation>();
        var referenceChanges = new List<CadCustomHandleReferenceExperimentObservation>();

        foreach (var beforeEntity in beforeEligible)
        {
            if (string.IsNullOrWhiteSpace(beforeEntity.Handle))
            {
                beforeOnly++;
                continue;
            }

            if (!afterIndex.TryGetValue(beforeEntity.Handle, out var afterEntity))
            {
                beforeOnly++;
                continue;
            }

            consumedAfterHandles.Add(beforeEntity.Handle);
            if (!eligible(afterEntity) || !SameIdentity(beforeEntity, afterEntity))
            {
                identityMismatch++;
                continue;
            }

            matched++;
            if (beforeEntity.RawDxfPayload is not null && afterEntity.RawDxfPayload is not null)
            {
                var observation = provenance == CadXiangyuanDocumentPairProvenance.ExplicitXiangyuanIdentity
                    ? CadXiangyuanExperimentAnalyzer.ObserveDxf(beforeEntity, afterEntity)
                    : CadXiangyuanCandidateExperimentAnalyzer.ObserveDxf(candidate!, beforeEntity, afterEntity);
                if (observation.Status == CadDxfCustomPayloadDiffStatus.Comparable)
                {
                    dxfComparable++;
                    if (observation.ValueChanges.Count > 0)
                    {
                        dxfChanged++;
                        dxfChanges.Add(observation);
                    }
                }
            }

            if (beforeEntity.RawDwgObjectRecord is not null && afterEntity.RawDwgObjectRecord is not null)
            {
                var observation = provenance == CadXiangyuanDocumentPairProvenance.ExplicitXiangyuanIdentity
                    ? CadXiangyuanExperimentAnalyzer.ObserveDwg(beforeEntity, afterEntity)
                    : CadXiangyuanCandidateExperimentAnalyzer.ObserveDwg(candidate!, beforeEntity, afterEntity);
                if (observation.Status == CadDwgCustomObjectRecordDiffStatus.Comparable)
                {
                    dwgComparable++;
                    if (observation.ChangedRanges.Count > 0)
                    {
                        dwgChanged++;
                        dwgChanges.Add(observation);
                    }
                }
            }

            if (beforeEntity.ProxyPrimitives.Count > 0 && afterEntity.ProxyPrimitives.Count > 0)
            {
                ValidatePairProvenance(provenance, candidate, beforeEntity, afterEntity);
                var observation = CadProxyGeometryExperimentAnalyzer.Observe(beforeEntity, afterEntity);
                if (observation.Status == CadProxyGeometryDiffStatus.Comparable)
                {
                    geometryComparable++;
                    if (observation.ValueChanges.Count > 0)
                    {
                        geometryChanged++;
                        geometryChanges.Add(observation);
                    }
                }
            }

            if (beforeEntity.HandleReferences.Count > 0 && afterEntity.HandleReferences.Count > 0)
            {
                ValidatePairProvenance(provenance, candidate, beforeEntity, afterEntity);
                var observation = CadCustomHandleReferenceExperimentAnalyzer.Observe(beforeEntity, afterEntity);
                if (observation.Status == CadCustomHandleReferenceDiffStatus.Comparable)
                {
                    referenceComparable++;
                    if (observation.ValueChanges.Count > 0)
                    {
                        referenceChanged++;
                        referenceChanges.Add(observation);
                    }
                }
            }
        }

        var afterOnly = afterEligible.Count(entity =>
            IsAfterOnly(entity, consumedAfterHandles, beforeIndex, eligible));

        var report = new CadXiangyuanDocumentPairEvidenceReport(
            CurrentSchemaVersion,
            provenance,
            beforeEligible.Length,
            afterEligible.Length,
            matched,
            beforeOnly,
            afterOnly,
            identityMismatch,
            dxfComparable,
            dxfChanged,
            dwgComparable,
            dwgChanged,
            geometryComparable,
            geometryChanged,
            referenceComparable,
            referenceChanged,
            new ReadOnlyCollection<CadDxfCustomExperimentObservation>(dxfChanges),
            new ReadOnlyCollection<CadDwgCustomExperimentObservation>(dwgChanges),
            new ReadOnlyCollection<CadProxyGeometryExperimentObservation>(geometryChanges),
            new ReadOnlyCollection<CadCustomHandleReferenceExperimentObservation>(referenceChanges));
        ValidateReport(report, nameof(before));
        return report;
    }

    private static void ValidatePairProvenance(
        CadXiangyuanDocumentPairProvenance provenance,
        CadXiangyuanConversionClassConsensus? candidate,
        CadCustomEntity before,
        CadCustomEntity after)
    {
        if (provenance == CadXiangyuanDocumentPairProvenance.ExplicitXiangyuanIdentity)
            CadXiangyuanExperimentAnalyzer.ValidateXiangyuanPair(before, after);
        else
        {
            CadXiangyuanCandidateExperimentAnalyzer.ValidateRepeatedCandidate(candidate);
            CadXiangyuanCandidateExperimentAnalyzer.ValidateEntity(candidate!, before, nameof(before));
            CadXiangyuanCandidateExperimentAnalyzer.ValidateEntity(candidate!, after, nameof(after));
        }
    }

    private static bool IsAfterOnly(
        CadCustomEntity entity,
        HashSet<string> consumedAfterHandles,
        Dictionary<string, CadCustomEntity> beforeIndex,
        Func<CadCustomEntity, bool> eligible)
    {
        if (string.IsNullOrWhiteSpace(entity.Handle)) return true;
        if (consumedAfterHandles.Contains(entity.Handle)) return false;
        return !beforeIndex.TryGetValue(entity.Handle, out var beforeEntity) || !eligible(beforeEntity);
    }

    private static Dictionary<string, CadCustomEntity> IndexByStableHandle(
        IReadOnlyList<CadCustomEntity> entities,
        string parameterName)
    {
        var index = new Dictionary<string, CadCustomEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Handle)) continue;
            if (!index.TryAdd(entity.Handle, entity))
                throw new ArgumentException("Document-pair evidence requires globally unique non-empty custom-entity handles.", parameterName);
        }
        return index;
    }

    private static IEnumerable<CadCustomEntity> EnumerateCustomEntities(CadDocument document)
    {
        foreach (var entity in document.ModelSpace.OfType<CadCustomEntity>()) yield return entity;
        foreach (var block in document.Blocks)
            foreach (var entity in block.Entities.OfType<CadCustomEntity>())
                yield return entity;
        foreach (var layout in document.Layouts.Where(layout => layout.IsPaperSpace))
            foreach (var entity in layout.Entities.OfType<CadCustomEntity>())
                yield return entity;
    }

    private static bool MatchesCandidate(
        CadXiangyuanConversionClassConsensus candidate,
        CadCustomEntity entity)
    {
        var definition = entity.ClassDefinition;
        return definition is not null
            && string.Equals(candidate.DxfName, definition.DxfName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.CppClassName, definition.CppClassName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.ApplicationName, definition.ApplicationName, StringComparison.OrdinalIgnoreCase)
            && candidate.IsEntity == definition.IsEntity
            && candidate.WasProxy == definition.WasProxy
            && string.Equals(candidate.ProxyFlags, definition.ProxyFlags, StringComparison.Ordinal);
    }

    private static bool SameIdentity(CadCustomEntity left, CadCustomEntity right)
    {
        static string Dxf(CadCustomEntity entity)
            => string.IsNullOrWhiteSpace(entity.ClassDefinition?.DxfName) ? entity.SourceEntityType : entity.ClassDefinition.DxfName;
        return string.Equals(Dxf(left), Dxf(right), StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.ClassDefinition?.CppClassName ?? string.Empty, right.ClassDefinition?.CppClassName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.ClassDefinition?.ApplicationName ?? string.Empty, right.ClassDefinition?.ApplicationName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateReport(CadXiangyuanDocumentPairEvidenceReport report, string parameterName)
    {
        if (report.SchemaVersion != CurrentSchemaVersion)
            throw new ArgumentException($"Unsupported Xiangyuan document-pair evidence version: {report.SchemaVersion}.", parameterName);
        if (!Enum.IsDefined(report.Provenance))
            throw new ArgumentException($"Unsupported Xiangyuan document-pair provenance: {(int)report.Provenance}.", parameterName);
        var counts = new[]
        {
            report.BeforeEligibleEntityCount, report.AfterEligibleEntityCount, report.MatchedEntityCount,
            report.BeforeOnlyEntityCount, report.AfterOnlyEntityCount, report.IdentityMismatchCount,
            report.DxfComparablePairCount, report.DxfChangedPairCount, report.DwgComparablePairCount, report.DwgChangedPairCount,
            report.GeometryComparablePairCount, report.GeometryChangedPairCount, report.ReferenceComparablePairCount, report.ReferenceChangedPairCount
        };
        if (counts.Any(count => count < 0))
            throw new ArgumentException("Xiangyuan document-pair evidence counts cannot be negative.", parameterName);
        if (report.MatchedEntityCount + report.BeforeOnlyEntityCount + report.IdentityMismatchCount != report.BeforeEligibleEntityCount)
            throw new ArgumentException("Before-side document-pair counts are inconsistent.", parameterName);
        if (report.MatchedEntityCount + report.AfterOnlyEntityCount > report.AfterEligibleEntityCount)
            throw new ArgumentException("After-side document-pair counts are inconsistent.", parameterName);
        if (report.DxfChangedPairCount > report.DxfComparablePairCount
            || report.DwgChangedPairCount > report.DwgComparablePairCount
            || report.GeometryChangedPairCount > report.GeometryComparablePairCount
            || report.ReferenceChangedPairCount > report.ReferenceComparablePairCount)
            throw new ArgumentException("Changed evidence counts cannot exceed comparable evidence counts.", parameterName);
        if (report.DxfChanges is null || report.DwgChanges is null || report.GeometryChanges is null || report.ReferenceChanges is null)
            throw new ArgumentException("Document-pair evidence change collections cannot be null.", parameterName);
        if (report.DxfComparablePairCount > report.MatchedEntityCount
            || report.DwgComparablePairCount > report.MatchedEntityCount
            || report.GeometryComparablePairCount > report.MatchedEntityCount
            || report.ReferenceComparablePairCount > report.MatchedEntityCount)
            throw new ArgumentException("Comparable evidence counts cannot exceed matched entity count.", parameterName);
        if (report.DxfChanges.Count != report.DxfChangedPairCount
            || report.DwgChanges.Count != report.DwgChangedPairCount
            || report.GeometryChanges.Count != report.GeometryChangedPairCount
            || report.ReferenceChanges.Count != report.ReferenceChangedPairCount)
            throw new ArgumentException("Document-pair changed evidence counts do not match their collections.", parameterName);

        foreach (var observation in report.DxfChanges)
        {
            ValidateObservationIdentity(report.Provenance, observation.Identity, parameterName);
            if (observation.Status != CadDxfCustomPayloadDiffStatus.Comparable
                || observation.ValueChanges.Count == 0
                || !string.Equals(observation.BeforeFingerprint, observation.AfterFingerprint, StringComparison.Ordinal))
                throw new ArgumentException("DXF document-pair evidence must contain only changed comparable same-schema observations.", parameterName);
        }
        foreach (var observation in report.DwgChanges)
        {
            ValidateObservationIdentity(report.Provenance, observation.Identity, parameterName);
            if (observation.Status != CadDwgCustomObjectRecordDiffStatus.Comparable
                || observation.ChangedRanges.Count == 0)
                throw new ArgumentException("DWG document-pair evidence must contain only changed comparable observations.", parameterName);
        }
        foreach (var observation in report.GeometryChanges)
        {
            ValidateObservationIdentity(report.Provenance, observation.Identity, parameterName);
            if (observation.Status != CadProxyGeometryDiffStatus.Comparable
                || observation.ValueChanges.Count == 0
                || !string.Equals(observation.BeforeLayoutFingerprint, observation.AfterLayoutFingerprint, StringComparison.Ordinal))
                throw new ArgumentException("Geometry document-pair evidence must contain only changed comparable same-layout observations.", parameterName);
        }
        foreach (var observation in report.ReferenceChanges)
        {
            ValidateObservationIdentity(report.Provenance, observation.Identity, parameterName);
            if (observation.Status != CadCustomHandleReferenceDiffStatus.Comparable
                || observation.ValueChanges.Count == 0
                || !string.Equals(observation.BeforeLayoutSignature, observation.AfterLayoutSignature, StringComparison.Ordinal))
                throw new ArgumentException("Reference document-pair evidence must contain only changed comparable same-layout observations.", parameterName);
        }
    }

    private static void ValidateObservationIdentity(
        CadXiangyuanDocumentPairProvenance provenance,
        CadCustomExperimentIdentity identity,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(identity, parameterName);
        var vendor = CadCustomObjectClassifier.Classify(
            identity.DxfName,
            identity.CppClassName,
            identity.ApplicationName);
        if (provenance == CadXiangyuanDocumentPairProvenance.ExplicitXiangyuanIdentity
            && vendor != CadCustomObjectVendor.Xiangyuan)
            throw new ArgumentException("Explicit Xiangyuan document-pair evidence contains a non-Xiangyuan identity.", parameterName);
        if (provenance == CadXiangyuanDocumentPairProvenance.RepeatedConversionCandidate
            && vendor != CadCustomObjectVendor.Unknown)
            throw new ArgumentException("Repeated-candidate document-pair evidence must retain Unknown vendor identity.", parameterName);
    }

    private static CadXiangyuanDocumentPairEvidenceReport Freeze(CadXiangyuanDocumentPairEvidenceReport report)
        => report with
        {
            DxfChanges = new ReadOnlyCollection<CadDxfCustomExperimentObservation>(report.DxfChanges.ToArray()),
            DwgChanges = new ReadOnlyCollection<CadDwgCustomExperimentObservation>(report.DwgChanges.ToArray()),
            GeometryChanges = new ReadOnlyCollection<CadProxyGeometryExperimentObservation>(report.GeometryChanges.ToArray()),
            ReferenceChanges = new ReadOnlyCollection<CadCustomHandleReferenceExperimentObservation>(report.ReferenceChanges.ToArray())
        };
}
