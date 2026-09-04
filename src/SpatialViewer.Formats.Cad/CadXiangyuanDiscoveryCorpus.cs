using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// One CLASSES-table identity observed in a drawing that the operator already knows was produced by Xiangyuan.
/// ClassifiedVendor is the normal CadCore classifier result and may remain Unknown; inclusion here is discovery
/// evidence only and never promotes an unknown class to Xiangyuan.
/// </summary>
public sealed record CadXiangyuanDiscoveryClassEntry(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    CadCustomObjectVendor ClassifiedVendor,
    bool IsEntity,
    bool WasProxy,
    string ProxyFlags,
    int DeclaredInstanceCount,
    int SamplesContainingClass);

/// <summary>
/// One structural custom-entity profile from a known-Xiangyuan sample. No raw values, drawing paths,
/// handles, coordinates, labels, or raw DWG bytes are retained.
/// </summary>
public sealed record CadXiangyuanDiscoveryProfileEntry(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    CadCustomObjectVendor ClassifiedVendor,
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

/// <summary>
/// Privacy-safe inventory used only when the caller already knows the source drawings came from Xiangyuan.
/// Unknown vendor identities remain Unknown so the report can reveal candidates without manufacturing support claims.
/// </summary>
public sealed record CadXiangyuanDiscoveryReport(
    int SchemaVersion,
    int SampleCount,
    IReadOnlyList<CadXiangyuanDiscoveryClassEntry> Classes,
    IReadOnlyList<CadXiangyuanDiscoveryProfileEntry> Profiles)
{
    public int CustomEntityCount => Profiles.Sum(profile => profile.EntityCount);
    public int KnownXiangyuanEntityCount => Profiles
        .Where(profile => profile.ClassifiedVendor == CadCustomObjectVendor.Xiangyuan)
        .Sum(profile => profile.EntityCount);
    public int UnknownVendorEntityCount => Profiles
        .Where(profile => profile.ClassifiedVendor == CadCustomObjectVendor.Unknown)
        .Sum(profile => profile.EntityCount);
}

public static class CadXiangyuanDiscoveryCorpus
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

    public static CadXiangyuanDiscoveryReport Build(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var relationshipsBySource = CadCustomRelationshipResolver.Resolve(document)
            .ToLookup(relationship => relationship.SourceHandle, StringComparer.OrdinalIgnoreCase);

        var classes = document.CustomClasses
            .GroupBy(ClassKey.Create)
            .Select(group => new CadXiangyuanDiscoveryClassEntry(
                group.Key.DxfName,
                group.Key.CppClassName,
                group.Key.ApplicationName,
                group.Key.ClassifiedVendor,
                group.Key.IsEntity,
                group.Key.WasProxy,
                group.Key.ProxyFlags,
                group.Sum(item => Math.Max(0, item.InstanceCount)),
                1))
            .OrderBy(entry => entry.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.CppClassName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var profiles = EnumerateCustomEntities(document)
            .GroupBy(ProfileKey.Create)
            .Select(group => CreateProfileEntry(group.Key, group, relationshipsBySource))
            .OrderBy(entry => entry.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SchemaFingerprint, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReferenceCodeSignature, StringComparer.Ordinal)
            .ThenBy(entry => entry.ProxyGraphicKindSignature, StringComparer.Ordinal)
            .ToArray();

        return new CadXiangyuanDiscoveryReport(
            CurrentSchemaVersion,
            1,
            new ReadOnlyCollection<CadXiangyuanDiscoveryClassEntry>(classes),
            new ReadOnlyCollection<CadXiangyuanDiscoveryProfileEntry>(profiles));
    }

    public static CadXiangyuanDiscoveryReport Build(IEnumerable<CadDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return Merge(documents.Select(Build));
    }

    public static CadXiangyuanDiscoveryReport Merge(IEnumerable<CadXiangyuanDiscoveryReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var materialized = reports.ToArray();
        foreach (var report in materialized) ValidateReport(report, nameof(reports));

        var classes = materialized
            .SelectMany(report => report.Classes)
            .GroupBy(ClassKey.Create)
            .Select(group => new CadXiangyuanDiscoveryClassEntry(
                group.Key.DxfName,
                group.Key.CppClassName,
                group.Key.ApplicationName,
                group.Key.ClassifiedVendor,
                group.Key.IsEntity,
                group.Key.WasProxy,
                group.Key.ProxyFlags,
                group.Sum(entry => entry.DeclaredInstanceCount),
                group.Sum(entry => entry.SamplesContainingClass)))
            .OrderBy(entry => entry.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.CppClassName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var profiles = materialized
            .SelectMany(report => report.Profiles)
            .GroupBy(ProfileKey.Create)
            .Select(group => new CadXiangyuanDiscoveryProfileEntry(
                group.Key.DxfName,
                group.Key.CppClassName,
                group.Key.ApplicationName,
                group.Key.ClassifiedVendor,
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
            .OrderBy(entry => entry.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SchemaFingerprint, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReferenceCodeSignature, StringComparer.Ordinal)
            .ThenBy(entry => entry.ProxyGraphicKindSignature, StringComparer.Ordinal)
            .ToArray();

        var merged = new CadXiangyuanDiscoveryReport(
            CurrentSchemaVersion,
            materialized.Sum(report => report.SampleCount),
            new ReadOnlyCollection<CadXiangyuanDiscoveryClassEntry>(classes),
            new ReadOnlyCollection<CadXiangyuanDiscoveryProfileEntry>(profiles));
        ValidateReport(merged, nameof(reports));
        return merged;
    }

    public static CadXiangyuanDiscoveryReport FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new FormatException($"Xiangyuan discovery JSON exceeds the {MaxJsonBytes} byte safety limit.");

        try
        {
            var parsed = JsonSerializer.Deserialize<CadXiangyuanDiscoveryReport>(json, JsonOptions)
                ?? throw new FormatException("Xiangyuan discovery JSON did not contain a report.");
            ValidateReport(parsed, nameof(json));
            return Freeze(parsed);
        }
        catch (JsonException exception)
        {
            throw new FormatException("Invalid Xiangyuan discovery JSON.", exception);
        }
    }

    public static CadXiangyuanDiscoveryReport MergeJson(IEnumerable<string> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        return Merge(reports.Select(FromJson));
    }

    public static string ToJson(CadXiangyuanDiscoveryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValidateReport(report, nameof(report));
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static CadXiangyuanDiscoveryReport Freeze(CadXiangyuanDiscoveryReport report)
        => new(
            report.SchemaVersion,
            report.SampleCount,
            new ReadOnlyCollection<CadXiangyuanDiscoveryClassEntry>(report.Classes.ToArray()),
            new ReadOnlyCollection<CadXiangyuanDiscoveryProfileEntry>(report.Profiles.ToArray()));

    private static void ValidateReport(CadXiangyuanDiscoveryReport? report, string parameterName)
    {
        if (report is null) throw new ArgumentException("Xiangyuan discovery report cannot be null.", parameterName);
        if (report.SchemaVersion != CurrentSchemaVersion)
            throw new ArgumentException($"Unsupported Xiangyuan discovery version: {report.SchemaVersion}.", parameterName);
        if (report.SampleCount < 0)
            throw new ArgumentException("Xiangyuan discovery sample count cannot be negative.", parameterName);
        if (report.Classes is null || report.Profiles is null)
            throw new ArgumentException("Xiangyuan discovery classes/profiles cannot be null.", parameterName);
        if (report.Classes.Count > MaxEntries || report.Profiles.Count > MaxEntries)
            throw new ArgumentException($"Xiangyuan discovery contains more than {MaxEntries} entries.", parameterName);
        if ((report.Classes.Count > 0 || report.Profiles.Count > 0) && report.SampleCount == 0)
            throw new ArgumentException("A non-empty Xiangyuan discovery report must represent at least one sample.", parameterName);

        foreach (var entry in report.Classes)
        {
            ValidateIdentity(entry.DxfName, nameof(entry.DxfName), parameterName, required: true);
            ValidateIdentity(entry.CppClassName, nameof(entry.CppClassName), parameterName, required: false);
            ValidateIdentity(entry.ApplicationName, nameof(entry.ApplicationName), parameterName, required: false);
            ValidateIdentity(entry.ProxyFlags, nameof(entry.ProxyFlags), parameterName, required: false);
            ValidateVendor(entry.ClassifiedVendor, parameterName);
            if (entry.DeclaredInstanceCount < 0)
                throw new ArgumentException("Xiangyuan discovery declared instance count cannot be negative.", parameterName);
            if (entry.SamplesContainingClass <= 0 || entry.SamplesContainingClass > report.SampleCount)
                throw new ArgumentException("Xiangyuan discovery class sample coverage is inconsistent with the report sample count.", parameterName);
        }

        foreach (var entry in report.Profiles)
        {
            ValidateIdentity(entry.DxfName, nameof(entry.DxfName), parameterName, required: true);
            ValidateIdentity(entry.CppClassName, nameof(entry.CppClassName), parameterName, required: false);
            ValidateIdentity(entry.ApplicationName, nameof(entry.ApplicationName), parameterName, required: false);
            ValidateIdentity(entry.SchemaFingerprint, nameof(entry.SchemaFingerprint), parameterName, required: true);
            ValidateIdentity(entry.GroupCodeSignature, nameof(entry.GroupCodeSignature), parameterName, required: true);
            ValidateIdentity(entry.SubclassMarkerSignature, nameof(entry.SubclassMarkerSignature), parameterName, required: true);
            ValidateIdentity(entry.ReferenceCodeSignature, nameof(entry.ReferenceCodeSignature), parameterName, required: false);
            ValidateIdentity(entry.ProxyGraphicKindSignature, nameof(entry.ProxyGraphicKindSignature), parameterName, required: true);
            ValidateVendor(entry.ClassifiedVendor, parameterName);
            if (entry.EntityCount <= 0)
                throw new ArgumentException("Xiangyuan discovery profile entity count must be positive.", parameterName);
            if (entry.SamplesContainingProfile <= 0 || entry.SamplesContainingProfile > report.SampleCount)
                throw new ArgumentException("Xiangyuan discovery profile sample coverage is inconsistent with the report sample count.", parameterName);
            ValidateCoverage(entry.RawDxfEvidenceEntityCount, entry.EntityCount, nameof(entry.RawDxfEvidenceEntityCount), parameterName);
            ValidateCoverage(entry.TruncatedRawDxfEntityCount, entry.EntityCount, nameof(entry.TruncatedRawDxfEntityCount), parameterName);
            ValidateCoverage(entry.ProxyGraphicsEntityCount, entry.EntityCount, nameof(entry.ProxyGraphicsEntityCount), parameterName);
            ValidateCoverage(entry.OpaqueEntityCount, entry.EntityCount, nameof(entry.OpaqueEntityCount), parameterName);
            ValidateCoverage(entry.RawDwgEvidenceEntityCount, entry.EntityCount, nameof(entry.RawDwgEvidenceEntityCount), parameterName);
            ValidateCoverage(entry.ResolvedRelationshipEntityCount, entry.EntityCount, nameof(entry.ResolvedRelationshipEntityCount), parameterName);
            if (entry.TruncatedRawDxfEntityCount > entry.RawDxfEvidenceEntityCount)
                throw new ArgumentException("Truncated raw-DXF coverage cannot exceed raw-DXF evidence coverage.", parameterName);
            if (entry.ProxyGraphicsEntityCount + entry.OpaqueEntityCount != entry.EntityCount)
                throw new ArgumentException("Proxy and opaque coverage must exactly equal custom-entity coverage.", parameterName);
            if (entry.ResolvedRelationshipCount < 0)
                throw new ArgumentException("Resolved relationship count cannot be negative.", parameterName);
        }
    }

    private static void ValidateIdentity(string? value, string fieldName, string parameterName, bool required)
    {
        if (required && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Xiangyuan discovery field {fieldName} cannot be empty.", parameterName);
        if (value?.Length > MaxIdentityLength)
            throw new ArgumentException($"Xiangyuan discovery field {fieldName} exceeds {MaxIdentityLength} characters.", parameterName);
    }

    private static void ValidateVendor(CadCustomObjectVendor vendor, string parameterName)
    {
        if (!Enum.IsDefined(vendor))
            throw new ArgumentException($"Unsupported custom-object vendor value: {(int)vendor}.", parameterName);
    }

    private static void ValidateCoverage(int value, int entityCount, string fieldName, string parameterName)
    {
        if (value < 0 || value > entityCount)
            throw new ArgumentException($"Xiangyuan discovery field {fieldName} must be between zero and the profile entity count.", parameterName);
    }

    private static CadXiangyuanDiscoveryProfileEntry CreateProfileEntry(
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

        return new CadXiangyuanDiscoveryProfileEntry(
            key.DxfName,
            key.CppClassName,
            key.ApplicationName,
            key.ClassifiedVendor,
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

    private readonly record struct ClassKey(
        string DxfName,
        string CppClassName,
        string ApplicationName,
        CadCustomObjectVendor ClassifiedVendor,
        bool IsEntity,
        bool WasProxy,
        string ProxyFlags)
    {
        public static ClassKey Create(CadCustomClassDefinition definition)
            => new(
                definition.DxfName,
                definition.CppClassName,
                definition.ApplicationName,
                definition.Vendor,
                definition.IsEntity,
                definition.WasProxy,
                definition.ProxyFlags);

        public static ClassKey Create(CadXiangyuanDiscoveryClassEntry entry)
            => new(
                entry.DxfName,
                entry.CppClassName,
                entry.ApplicationName,
                entry.ClassifiedVendor,
                entry.IsEntity,
                entry.WasProxy,
                entry.ProxyFlags);
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
        public static ProfileKey Create(CadCustomEntity entity)
        {
            var definition = entity.ClassDefinition;
            var profile = entity.RawDxfProfile;
            return new ProfileKey(
                FirstNonEmpty(definition?.DxfName, entity.SourceEntityType),
                definition?.CppClassName ?? string.Empty,
                definition?.ApplicationName ?? string.Empty,
                entity.Vendor,
                profile?.Fingerprint ?? Unavailable,
                profile?.GroupCodeSignature ?? Unavailable,
                profile is null ? Unavailable : string.Join('>', profile.SubclassMarkers),
                ReferenceSignature(entity),
                CadXiangyuanDiscoveryCorpus.ProxyGraphicKindSignature(entity));
        }

        public static ProfileKey Create(CadXiangyuanDiscoveryProfileEntry entry)
            => new(
                entry.DxfName,
                entry.CppClassName,
                entry.ApplicationName,
                entry.ClassifiedVendor,
                entry.SchemaFingerprint,
                entry.GroupCodeSignature,
                entry.SubclassMarkerSignature,
                entry.ReferenceCodeSignature,
                entry.ProxyGraphicKindSignature);

        private static string FirstNonEmpty(string? first, string second)
            => string.IsNullOrWhiteSpace(first) ? second : first;
    }
}
