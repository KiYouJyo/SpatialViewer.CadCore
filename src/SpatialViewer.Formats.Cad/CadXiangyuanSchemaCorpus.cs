using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// One privacy-safe Xiangyuan custom-entity schema cluster. The report contains structural identities
/// and aggregate compatibility coverage only; it never includes drawing paths, object handles,
/// coordinates, labels, raw DXF values, or raw DWG bytes.
/// </summary>
public sealed record CadXiangyuanSchemaCorpusEntry(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    string SchemaFingerprint,
    string GroupCodeSignature,
    string SubclassMarkerSignature,
    string ReferenceCodeSignature,
    string ProxyGraphicKindSignature,
    int EntityCount,
    int SamplesContainingProfile,
    int RawDxfEvidenceEntityCount,
    int TruncatedRawDxfEntityCount,
    int ProxyGraphicsEntityCount,
    int OpaqueEntityCount,
    int RawDwgEvidenceEntityCount,
    int ResolvedRelationshipEntityCount,
    int ResolvedRelationshipCount);

/// <summary>A mergeable anonymized Xiangyuan schema report for one or more CAD samples.</summary>
public sealed record CadXiangyuanSchemaCorpusReport(
    int SchemaVersion,
    int SampleCount,
    IReadOnlyList<CadXiangyuanSchemaCorpusEntry> Entries)
{
    public int EntityCount => Entries.Sum(entry => entry.EntityCount);
}

/// <summary>
/// Builds deterministic Xiangyuan compatibility corpora from already-imported CadCore documents.
/// This is a discovery/compatibility tool only; structural clusters do not imply native parcel,
/// atlas, utility, road, or control-index semantics.
/// </summary>
public static class CadXiangyuanSchemaCorpus
{
    public const int CurrentSchemaVersion = 1;
    private const string Unavailable = "unavailable";
    private const string None = "none";
    private const int MaxJsonBytes = 16 * 1024 * 1024;
    private const int MaxEntries = 100_000;
    private const int MaxIdentityLength = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static CadXiangyuanSchemaCorpusReport Build(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var relationshipsBySource = CadCustomRelationshipResolver.Resolve(document)
            .ToLookup(relationship => relationship.SourceHandle, StringComparer.OrdinalIgnoreCase);

        var entries = EnumerateCustomEntities(document)
            .Where(entity => entity.IsXiangyuan)
            .GroupBy(ProfileKey.Create)
            .Select(group => CreateEntry(group.Key, group, relationshipsBySource))
            .OrderBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SchemaFingerprint, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReferenceCodeSignature, StringComparer.Ordinal)
            .ThenBy(entry => entry.ProxyGraphicKindSignature, StringComparer.Ordinal)
            .ToArray();

        return new CadXiangyuanSchemaCorpusReport(
            CurrentSchemaVersion,
            1,
            new ReadOnlyCollection<CadXiangyuanSchemaCorpusEntry>(entries));
    }

    public static CadXiangyuanSchemaCorpusReport Build(IEnumerable<CadDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return Merge(documents.Select(Build));
    }

    public static CadXiangyuanSchemaCorpusReport Merge(IEnumerable<CadXiangyuanSchemaCorpusReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var materialized = reports.ToArray();
        foreach (var report in materialized) ValidateReport(report, nameof(reports));

        var entries = materialized
            .SelectMany(report => report.Entries)
            .GroupBy(ProfileKey.Create)
            .Select(group => new CadXiangyuanSchemaCorpusEntry(
                group.Key.DxfName,
                group.Key.CppClassName,
                group.Key.ApplicationName,
                group.Key.SchemaFingerprint,
                group.Key.GroupCodeSignature,
                group.Key.SubclassMarkerSignature,
                group.Key.ReferenceCodeSignature,
                group.Key.ProxyGraphicKindSignature,
                group.Sum(entry => entry.EntityCount),
                group.Sum(entry => entry.SamplesContainingProfile),
                group.Sum(entry => entry.RawDxfEvidenceEntityCount),
                group.Sum(entry => entry.TruncatedRawDxfEntityCount),
                group.Sum(entry => entry.ProxyGraphicsEntityCount),
                group.Sum(entry => entry.OpaqueEntityCount),
                group.Sum(entry => entry.RawDwgEvidenceEntityCount),
                group.Sum(entry => entry.ResolvedRelationshipEntityCount),
                group.Sum(entry => entry.ResolvedRelationshipCount)))
            .OrderBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SchemaFingerprint, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReferenceCodeSignature, StringComparer.Ordinal)
            .ThenBy(entry => entry.ProxyGraphicKindSignature, StringComparer.Ordinal)
            .ToArray();

        var merged = new CadXiangyuanSchemaCorpusReport(
            CurrentSchemaVersion,
            materialized.Sum(report => report.SampleCount),
            new ReadOnlyCollection<CadXiangyuanSchemaCorpusEntry>(entries));
        ValidateReport(merged, nameof(reports));
        return merged;
    }

    public static CadXiangyuanSchemaCorpusReport FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new FormatException($"Xiangyuan schema corpus JSON exceeds the {MaxJsonBytes} byte safety limit.");

        try
        {
            var parsed = JsonSerializer.Deserialize<CadXiangyuanSchemaCorpusReport>(json, JsonOptions)
                ?? throw new FormatException("Xiangyuan schema corpus JSON did not contain a report.");
            ValidateReport(parsed, nameof(json));
            return Freeze(parsed);
        }
        catch (JsonException exception)
        {
            throw new FormatException("Invalid Xiangyuan schema corpus JSON.", exception);
        }
    }

    public static CadXiangyuanSchemaCorpusReport MergeJson(IEnumerable<string> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        return Merge(reports.Select(FromJson));
    }

    public static string ToJson(CadXiangyuanSchemaCorpusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValidateReport(report, nameof(report));
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static CadXiangyuanSchemaCorpusReport Freeze(CadXiangyuanSchemaCorpusReport report)
        => new(
            report.SchemaVersion,
            report.SampleCount,
            new ReadOnlyCollection<CadXiangyuanSchemaCorpusEntry>(report.Entries.ToArray()));

    private static void ValidateReport(CadXiangyuanSchemaCorpusReport? report, string parameterName)
    {
        if (report is null) throw new ArgumentException("Xiangyuan schema corpus report cannot be null.", parameterName);
        if (report.SchemaVersion != CurrentSchemaVersion)
            throw new ArgumentException($"Unsupported Xiangyuan schema corpus version: {report.SchemaVersion}.", parameterName);
        if (report.SampleCount < 0)
            throw new ArgumentException("Xiangyuan schema corpus sample count cannot be negative.", parameterName);
        if (report.Entries is null)
            throw new ArgumentException("Xiangyuan schema corpus entries cannot be null.", parameterName);
        if (report.Entries.Count > MaxEntries)
            throw new ArgumentException($"Xiangyuan schema corpus contains more than {MaxEntries} entries.", parameterName);
        if (report.Entries.Count > 0 && report.SampleCount == 0)
            throw new ArgumentException("A non-empty Xiangyuan schema corpus must represent at least one sample.", parameterName);

        foreach (var entry in report.Entries)
        {
            if (entry is null) throw new ArgumentException("Xiangyuan schema corpus cannot contain null entries.", parameterName);
            ValidateIdentity(entry.DxfName, nameof(entry.DxfName), parameterName, required: true);
            ValidateIdentity(entry.CppClassName, nameof(entry.CppClassName), parameterName, required: false);
            ValidateIdentity(entry.ApplicationName, nameof(entry.ApplicationName), parameterName, required: false);
            ValidateIdentity(entry.SchemaFingerprint, nameof(entry.SchemaFingerprint), parameterName, required: true);
            ValidateIdentity(entry.GroupCodeSignature, nameof(entry.GroupCodeSignature), parameterName, required: true);
            ValidateIdentity(entry.SubclassMarkerSignature, nameof(entry.SubclassMarkerSignature), parameterName, required: true);
            ValidateIdentity(entry.ReferenceCodeSignature, nameof(entry.ReferenceCodeSignature), parameterName, required: false);
            ValidateIdentity(entry.ProxyGraphicKindSignature, nameof(entry.ProxyGraphicKindSignature), parameterName, required: true);

            if (entry.EntityCount <= 0)
                throw new ArgumentException("Xiangyuan schema corpus entity counts must be positive.", parameterName);
            if (entry.SamplesContainingProfile <= 0 || entry.SamplesContainingProfile > report.SampleCount)
                throw new ArgumentException("Xiangyuan schema corpus profile sample coverage is inconsistent with the report sample count.", parameterName);

            ValidateEntityCoverage(entry.RawDxfEvidenceEntityCount, entry.EntityCount, nameof(entry.RawDxfEvidenceEntityCount), parameterName);
            ValidateEntityCoverage(entry.TruncatedRawDxfEntityCount, entry.EntityCount, nameof(entry.TruncatedRawDxfEntityCount), parameterName);
            ValidateEntityCoverage(entry.ProxyGraphicsEntityCount, entry.EntityCount, nameof(entry.ProxyGraphicsEntityCount), parameterName);
            ValidateEntityCoverage(entry.OpaqueEntityCount, entry.EntityCount, nameof(entry.OpaqueEntityCount), parameterName);
            ValidateEntityCoverage(entry.RawDwgEvidenceEntityCount, entry.EntityCount, nameof(entry.RawDwgEvidenceEntityCount), parameterName);
            ValidateEntityCoverage(entry.ResolvedRelationshipEntityCount, entry.EntityCount, nameof(entry.ResolvedRelationshipEntityCount), parameterName);

            if (entry.TruncatedRawDxfEntityCount > entry.RawDxfEvidenceEntityCount)
                throw new ArgumentException("Truncated raw-DXF coverage cannot exceed raw-DXF evidence coverage.", parameterName);
            if (entry.ProxyGraphicsEntityCount + entry.OpaqueEntityCount != entry.EntityCount)
                throw new ArgumentException("Proxy and opaque coverage must exactly equal Xiangyuan entity coverage.", parameterName);
            if (entry.ResolvedRelationshipCount < 0)
                throw new ArgumentException("Resolved relationship count cannot be negative.", parameterName);
        }
    }

    private static void ValidateIdentity(string? value, string fieldName, string parameterName, bool required)
    {
        if (required && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Xiangyuan schema corpus field {fieldName} cannot be empty.", parameterName);
        if (value?.Length > MaxIdentityLength)
            throw new ArgumentException($"Xiangyuan schema corpus field {fieldName} exceeds {MaxIdentityLength} characters.", parameterName);
    }

    private static void ValidateEntityCoverage(int value, int entityCount, string fieldName, string parameterName)
    {
        if (value < 0 || value > entityCount)
            throw new ArgumentException($"Xiangyuan schema corpus field {fieldName} must be between zero and the profile entity count.", parameterName);
    }

    private static CadXiangyuanSchemaCorpusEntry CreateEntry(
        ProfileKey key,
        IEnumerable<CadCustomEntity> entities,
        ILookup<string, CadCustomRelationship> relationshipsBySource)
    {
        var materialized = entities.ToArray();
        var relationships = materialized
            .SelectMany(entity => relationshipsBySource[entity.Handle])
            .ToArray();
        var resolvedSourceHandles = relationships
            .Select(relationship => relationship.SourceHandle)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new CadXiangyuanSchemaCorpusEntry(
            key.DxfName,
            key.CppClassName,
            key.ApplicationName,
            key.SchemaFingerprint,
            key.GroupCodeSignature,
            key.SubclassMarkerSignature,
            key.ReferenceCodeSignature,
            key.ProxyGraphicKindSignature,
            materialized.Length,
            1,
            materialized.Count(entity => entity.RawDxfPayload is not null),
            materialized.Count(entity => entity.RawDxfPayload?.IsTruncated == true),
            materialized.Count(entity => entity.Representation == CadCustomEntityRepresentation.ProxyGraphics),
            materialized.Count(entity => entity.Representation == CadCustomEntityRepresentation.Opaque),
            materialized.Count(entity => entity.RawDwgObjectRecord is not null),
            resolvedSourceHandles.Count,
            relationships.Length);
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

    private static string ReferenceSignature(CadCustomEntity entity)
        => string.Join(',', entity.HandleReferences
            .GroupBy(reference => reference.GroupCode)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}x{group.Count()}"));

    private static string ProxyGraphicKindSignature(CadCustomEntity entity)
    {
        var kinds = entity.ProxyGraphicKinds
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToArray();
        return kinds.Length == 0 ? None : string.Join(',', kinds);
    }

    private readonly record struct ProfileKey(
        string DxfName,
        string CppClassName,
        string ApplicationName,
        string SchemaFingerprint,
        string GroupCodeSignature,
        string SubclassMarkerSignature,
        string ReferenceCodeSignature,
        string ProxyGraphicKindSignature)
    {
        public static ProfileKey Create(CadCustomEntity entity)
        {
            var definition = entity.ClassDefinition;
            var profile = entity.RawDxfProfile;
            return new ProfileKey(
                FirstNonEmpty(definition?.DxfName, entity.SourceEntityType),
                definition?.CppClassName ?? string.Empty,
                definition?.ApplicationName ?? string.Empty,
                profile?.Fingerprint ?? Unavailable,
                profile?.GroupCodeSignature ?? Unavailable,
                profile is null ? Unavailable : string.Join('>', profile.SubclassMarkers),
                ReferenceSignature(entity),
                CadXiangyuanSchemaCorpus.ProxyGraphicKindSignature(entity));
        }

        public static ProfileKey Create(CadXiangyuanSchemaCorpusEntry entry)
            => new(
                entry.DxfName,
                entry.CppClassName,
                entry.ApplicationName,
                entry.SchemaFingerprint,
                entry.GroupCodeSignature,
                entry.SubclassMarkerSignature,
                entry.ReferenceCodeSignature,
                entry.ProxyGraphicKindSignature);

        private static string FirstNonEmpty(string? first, string second)
            => string.IsNullOrWhiteSpace(first) ? second : first;
    }
}
