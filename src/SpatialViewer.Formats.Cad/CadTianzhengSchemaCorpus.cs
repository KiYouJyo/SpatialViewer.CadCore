using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// One privacy-safe Tianzheng custom-entity schema cluster. The report deliberately contains no handles,
/// coordinates, text values, raw DXF values, raw DWG bytes, document names, or source paths.
/// Relationship fields are aggregate coverage only and never participate in schema identity.
/// </summary>
public sealed record CadTianzhengSchemaCorpusEntry(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    string SchemaFingerprint,
    string GroupCodeSignature,
    string SubclassMarkerSignature,
    string ReferenceCodeSignature,
    int EntityCount,
    int SamplesContainingProfile,
    int TruncatedRawDxfEntityCount,
    int NativeSemanticEntityCount,
    int ProxyGraphicsEntityCount,
    int RawDwgEvidenceEntityCount,
    int ResolvedRelationshipEntityCount,
    int ResolvedRelationshipCount,
    int OpeningHostWallEntityCount,
    int OpeningHostWallRelationshipCount)
{
    /// <summary>Entities whose recovered native semantics remain evidence-backed but not safely drawable as native 2D geometry.</summary>
    public int PartialSemanticEntityCount { get; init; }

    /// <summary>Entities whose recovered native semantics are sufficient for native 2D Scene geometry.</summary>
    public int Drawable2DSemanticEntityCount { get; init; }
}

/// <summary>A mergeable anonymized schema report for one or more CAD samples.</summary>
public sealed record CadTianzhengSchemaCorpusReport(
    int SchemaVersion,
    int SampleCount,
    IReadOnlyList<CadTianzhengSchemaCorpusEntry> Entries)
{
    public int EntityCount => Entries.Sum(entry => entry.EntityCount);
}

/// <summary>
/// Builds deterministic Tianzheng schema corpora from already-imported CadCore documents. Only structural
/// evidence and aggregate relationship/semantic coverage are retained, allowing samples from different
/// Tianzheng generations to be compared without exporting drawing contents.
/// </summary>
public static class CadTianzhengSchemaCorpus
{
    public const int CurrentSchemaVersion = 3;
    private const string Unavailable = "unavailable";
    private const int MaxJsonBytes = 16 * 1024 * 1024;
    private const int MaxEntries = 100_000;
    private const int MaxIdentityLength = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static CadTianzhengSchemaCorpusReport Build(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var relationshipsBySource = CadCustomRelationshipResolver.Resolve(document)
            .ToLookup(relationship => relationship.SourceHandle, StringComparer.OrdinalIgnoreCase);
        var entries = EnumerateCustomEntities(document)
            .Where(entity => entity.IsTianzheng)
            .GroupBy(ProfileKey.Create)
            .Select(group => CreateEntry(group.Key, group, relationshipsBySource))
            .OrderBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SchemaFingerprint, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReferenceCodeSignature, StringComparer.Ordinal)
            .ToArray();
        return new CadTianzhengSchemaCorpusReport(
            CurrentSchemaVersion,
            1,
            new ReadOnlyCollection<CadTianzhengSchemaCorpusEntry>(entries));
    }

    public static CadTianzhengSchemaCorpusReport Build(IEnumerable<CadDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return Merge(documents.Select(Build));
    }

    public static CadTianzhengSchemaCorpusReport Merge(IEnumerable<CadTianzhengSchemaCorpusReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var materialized = reports.ToArray();
        foreach (var report in materialized) ValidateReport(report, nameof(reports));

        var entries = materialized
            .SelectMany(report => report.Entries)
            .GroupBy(ProfileKey.Create)
            .Select(group => new CadTianzhengSchemaCorpusEntry(
                group.Key.DxfName,
                group.Key.CppClassName,
                group.Key.ApplicationName,
                group.Key.SchemaFingerprint,
                group.Key.GroupCodeSignature,
                group.Key.SubclassMarkerSignature,
                group.Key.ReferenceCodeSignature,
                group.Sum(entry => entry.EntityCount),
                group.Sum(entry => entry.SamplesContainingProfile),
                group.Sum(entry => entry.TruncatedRawDxfEntityCount),
                group.Sum(entry => entry.NativeSemanticEntityCount),
                group.Sum(entry => entry.ProxyGraphicsEntityCount),
                group.Sum(entry => entry.RawDwgEvidenceEntityCount),
                group.Sum(entry => entry.ResolvedRelationshipEntityCount),
                group.Sum(entry => entry.ResolvedRelationshipCount),
                group.Sum(entry => entry.OpeningHostWallEntityCount),
                group.Sum(entry => entry.OpeningHostWallRelationshipCount))
            {
                PartialSemanticEntityCount = group.Sum(entry => entry.PartialSemanticEntityCount),
                Drawable2DSemanticEntityCount = group.Sum(entry => entry.Drawable2DSemanticEntityCount)
            })
            .OrderBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SchemaFingerprint, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReferenceCodeSignature, StringComparer.Ordinal)
            .ToArray();

        var merged = new CadTianzhengSchemaCorpusReport(
            CurrentSchemaVersion,
            materialized.Sum(report => report.SampleCount),
            new ReadOnlyCollection<CadTianzhengSchemaCorpusEntry>(entries));
        ValidateReport(merged, nameof(reports));
        return merged;
    }

    /// <summary>Deserialize and validate one externally supplied corpus report.</summary>
    public static CadTianzhengSchemaCorpusReport FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new FormatException($"Tianzheng schema corpus JSON exceeds the {MaxJsonBytes} byte safety limit.");

        try
        {
            var parsed = JsonSerializer.Deserialize<CadTianzhengSchemaCorpusReport>(json, JsonOptions)
                ?? throw new FormatException("Tianzheng schema corpus JSON did not contain a report.");
            ValidateReport(parsed, nameof(json));
            return Freeze(parsed);
        }
        catch (JsonException exception)
        {
            throw new FormatException("Invalid Tianzheng schema corpus JSON.", exception);
        }
    }

    /// <summary>Deserialize, validate, and merge independently generated corpus JSON reports.</summary>
    public static CadTianzhengSchemaCorpusReport MergeJson(IEnumerable<string> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        return Merge(reports.Select(FromJson));
    }

    public static string ToJson(CadTianzhengSchemaCorpusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValidateReport(report, nameof(report));
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static CadTianzhengSchemaCorpusReport Freeze(CadTianzhengSchemaCorpusReport report)
        => new(
            report.SchemaVersion,
            report.SampleCount,
            new ReadOnlyCollection<CadTianzhengSchemaCorpusEntry>(report.Entries.ToArray()));

    private static void ValidateReport(CadTianzhengSchemaCorpusReport? report, string parameterName)
    {
        if (report is null) throw new ArgumentException("Tianzheng schema corpus report cannot be null.", parameterName);
        if (report.SchemaVersion != CurrentSchemaVersion)
            throw new ArgumentException($"Unsupported Tianzheng schema corpus version: {report.SchemaVersion}.", parameterName);
        if (report.SampleCount < 0)
            throw new ArgumentException("Tianzheng schema corpus sample count cannot be negative.", parameterName);
        if (report.Entries is null)
            throw new ArgumentException("Tianzheng schema corpus entries cannot be null.", parameterName);
        if (report.Entries.Count > MaxEntries)
            throw new ArgumentException($"Tianzheng schema corpus contains more than {MaxEntries} entries.", parameterName);
        if (report.Entries.Count > 0 && report.SampleCount == 0)
            throw new ArgumentException("A non-empty Tianzheng schema corpus must represent at least one sample.", parameterName);

        foreach (var entry in report.Entries)
        {
            if (entry is null) throw new ArgumentException("Tianzheng schema corpus cannot contain null entries.", parameterName);
            ValidateIdentity(entry.DxfName, nameof(entry.DxfName), parameterName, required: true);
            ValidateIdentity(entry.CppClassName, nameof(entry.CppClassName), parameterName, required: false);
            ValidateIdentity(entry.ApplicationName, nameof(entry.ApplicationName), parameterName, required: false);
            ValidateIdentity(entry.SchemaFingerprint, nameof(entry.SchemaFingerprint), parameterName, required: true);
            ValidateIdentity(entry.GroupCodeSignature, nameof(entry.GroupCodeSignature), parameterName, required: true);
            ValidateIdentity(entry.SubclassMarkerSignature, nameof(entry.SubclassMarkerSignature), parameterName, required: true);
            ValidateIdentity(entry.ReferenceCodeSignature, nameof(entry.ReferenceCodeSignature), parameterName, required: false);

            if (entry.EntityCount <= 0)
                throw new ArgumentException("Tianzheng schema corpus entity counts must be positive.", parameterName);
            if (entry.SamplesContainingProfile <= 0 || entry.SamplesContainingProfile > report.SampleCount)
                throw new ArgumentException("Tianzheng schema corpus profile sample coverage is inconsistent with the report sample count.", parameterName);

            ValidateEntityCoverage(entry.TruncatedRawDxfEntityCount, entry.EntityCount, nameof(entry.TruncatedRawDxfEntityCount), parameterName);
            ValidateEntityCoverage(entry.NativeSemanticEntityCount, entry.EntityCount, nameof(entry.NativeSemanticEntityCount), parameterName);
            ValidateEntityCoverage(entry.PartialSemanticEntityCount, entry.EntityCount, nameof(entry.PartialSemanticEntityCount), parameterName);
            ValidateEntityCoverage(entry.Drawable2DSemanticEntityCount, entry.EntityCount, nameof(entry.Drawable2DSemanticEntityCount), parameterName);
            ValidateEntityCoverage(entry.ProxyGraphicsEntityCount, entry.EntityCount, nameof(entry.ProxyGraphicsEntityCount), parameterName);
            ValidateEntityCoverage(entry.RawDwgEvidenceEntityCount, entry.EntityCount, nameof(entry.RawDwgEvidenceEntityCount), parameterName);
            ValidateEntityCoverage(entry.ResolvedRelationshipEntityCount, entry.EntityCount, nameof(entry.ResolvedRelationshipEntityCount), parameterName);
            ValidateEntityCoverage(entry.OpeningHostWallEntityCount, entry.EntityCount, nameof(entry.OpeningHostWallEntityCount), parameterName);

            if (entry.PartialSemanticEntityCount + entry.Drawable2DSemanticEntityCount != entry.NativeSemanticEntityCount)
                throw new ArgumentException("Partial and drawable semantic coverage must exactly equal native semantic coverage.", parameterName);
            if (entry.ResolvedRelationshipCount < 0)
                throw new ArgumentException("Resolved relationship count cannot be negative.", parameterName);
            if (entry.OpeningHostWallRelationshipCount < 0 || entry.OpeningHostWallRelationshipCount > entry.ResolvedRelationshipCount)
                throw new ArgumentException("Opening-host-wall relationship count is inconsistent with resolved relationship count.", parameterName);
            if (entry.OpeningHostWallEntityCount > entry.ResolvedRelationshipEntityCount)
                throw new ArgumentException("Opening-host-wall entity coverage cannot exceed resolved relationship entity coverage.", parameterName);
        }
    }

    private static void ValidateIdentity(string? value, string fieldName, string parameterName, bool required)
    {
        if (required && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Tianzheng schema corpus field {fieldName} cannot be empty.", parameterName);
        if (value?.Length > MaxIdentityLength)
            throw new ArgumentException($"Tianzheng schema corpus field {fieldName} exceeds {MaxIdentityLength} characters.", parameterName);
    }

    private static void ValidateEntityCoverage(int value, int entityCount, string fieldName, string parameterName)
    {
        if (value < 0 || value > entityCount)
            throw new ArgumentException($"Tianzheng schema corpus field {fieldName} must be between zero and the profile entity count.", parameterName);
    }

    private static CadTianzhengSchemaCorpusEntry CreateEntry(
        ProfileKey key,
        IEnumerable<CadCustomEntity> entities,
        ILookup<string, CadCustomRelationship> relationshipsBySource)
    {
        var materialized = entities.ToArray();
        var relationships = materialized
            .SelectMany(entity => relationshipsBySource[entity.Handle])
            .ToArray();
        var openingHostWallRelationships = relationships
            .Where(relationship => relationship.Kind == CadCustomRelationshipKind.TianzhengOpeningHostWall)
            .ToArray();
        var resolvedSourceHandles = relationships
            .Select(relationship => relationship.SourceHandle)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var openingHostSourceHandles = openingHostWallRelationships
            .Select(relationship => relationship.SourceHandle)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nativeSemanticEntityCount = materialized.Count(entity => entity.NativeSemantics is not null);
        var partialSemanticEntityCount = materialized.Count(entity => entity.NativeSemantics?.Coverage == CadCustomSemanticCoverage.Partial);
        var drawable2DSemanticEntityCount = materialized.Count(entity => entity.NativeSemantics?.Coverage == CadCustomSemanticCoverage.Drawable2D);

        return new CadTianzhengSchemaCorpusEntry(
            key.DxfName,
            key.CppClassName,
            key.ApplicationName,
            key.SchemaFingerprint,
            key.GroupCodeSignature,
            key.SubclassMarkerSignature,
            key.ReferenceCodeSignature,
            materialized.Length,
            1,
            materialized.Count(entity => entity.RawDxfPayload?.IsTruncated == true),
            nativeSemanticEntityCount,
            materialized.Count(entity => entity.Representation == CadCustomEntityRepresentation.ProxyGraphics),
            materialized.Count(entity => entity.RawDwgObjectRecord is not null),
            resolvedSourceHandles.Count,
            relationships.Length,
            openingHostSourceHandles.Count,
            openingHostWallRelationships.Length)
        {
            PartialSemanticEntityCount = partialSemanticEntityCount,
            Drawable2DSemanticEntityCount = drawable2DSemanticEntityCount
        };
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

    private readonly record struct ProfileKey(
        string DxfName,
        string CppClassName,
        string ApplicationName,
        string SchemaFingerprint,
        string GroupCodeSignature,
        string SubclassMarkerSignature,
        string ReferenceCodeSignature)
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
                ReferenceSignature(entity));
        }

        public static ProfileKey Create(CadTianzhengSchemaCorpusEntry entry)
            => new(
                entry.DxfName,
                entry.CppClassName,
                entry.ApplicationName,
                entry.SchemaFingerprint,
                entry.GroupCodeSignature,
                entry.SubclassMarkerSignature,
                entry.ReferenceCodeSignature);

        private static string FirstNonEmpty(string? first, string second)
            => string.IsNullOrWhiteSpace(first) ? second : first;
    }
}
