using System.Collections.ObjectModel;
using System.Text.Json;

namespace SpatialViewer.Formats.Cad;

/// <summary>
/// One privacy-safe Tianzheng custom-entity schema cluster. The report deliberately contains no handles,
/// coordinates, text values, raw DXF values, raw DWG bytes, document names, or source paths.
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
    int RawDwgEvidenceEntityCount);

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
/// evidence is aggregated, allowing samples from different Tianzheng generations to be compared without
/// exporting drawing contents.
/// </summary>
public static class CadTianzhengSchemaCorpus
{
    public const int CurrentSchemaVersion = 1;
    private const string Unavailable = "unavailable";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static CadTianzhengSchemaCorpusReport Build(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var entries = EnumerateCustomEntities(document)
            .Where(entity => entity.IsTianzheng)
            .GroupBy(ProfileKey.Create)
            .Select(group => CreateEntry(group.Key, group))
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
        foreach (var report in materialized)
        {
            ArgumentNullException.ThrowIfNull(report);
            if (report.SchemaVersion != CurrentSchemaVersion)
                throw new ArgumentException($"Unsupported Tianzheng schema corpus version: {report.SchemaVersion}.", nameof(reports));
        }

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
                group.Sum(entry => entry.RawDwgEvidenceEntityCount)))
            .OrderBy(entry => entry.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SchemaFingerprint, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReferenceCodeSignature, StringComparer.Ordinal)
            .ToArray();

        return new CadTianzhengSchemaCorpusReport(
            CurrentSchemaVersion,
            materialized.Sum(report => report.SampleCount),
            new ReadOnlyCollection<CadTianzhengSchemaCorpusEntry>(entries));
    }

    public static string ToJson(CadTianzhengSchemaCorpusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static CadTianzhengSchemaCorpusEntry CreateEntry(
        ProfileKey key,
        IEnumerable<CadCustomEntity> entities)
    {
        var materialized = entities.ToArray();
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
            materialized.Count(entity => entity.NativeSemantics is not null),
            materialized.Count(entity => entity.Representation == CadCustomEntityRepresentation.ProxyGraphics),
            materialized.Count(entity => entity.RawDwgObjectRecord is not null));
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
