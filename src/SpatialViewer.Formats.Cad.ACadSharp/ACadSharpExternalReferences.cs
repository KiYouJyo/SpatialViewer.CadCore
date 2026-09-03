using ACadSharp.Blocks;
using ACadSharp.IO;
using SpatialViewer.Core;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>Format of an external CAD resource explicitly supplied by the host.</summary>
public enum CadExternalReferenceFormat
{
    Dxf,
    Dwg
}

/// <summary>
/// One Xref resolution request. SourceReference is passed only to the host resolver; CadCore never
/// interprets it as a local path, combines it with directories, probes it, or opens it itself.
/// </summary>
public sealed record CadExternalReferenceRequest(
    string ParentDocumentPath,
    string ReferenceName,
    string SourceReference,
    bool IsOverlay);

/// <summary>
/// Host-approved Xref bytes. The stream must be readable and seekable and positioned arbitrarily;
/// CadCore rewinds it before reading and disposes it when the resolution attempt completes.
/// </summary>
public sealed record CadExternalReferenceResource(Stream Content, CadExternalReferenceFormat Format);

/// <summary>
/// Explicit trust boundary for external references. Returning null declines resolution. Implementations
/// decide whether a source reference is allowed and where its bytes come from.
/// </summary>
public interface ICadExternalReferenceResolver
{
    CadExternalReferenceResource? Resolve(CadExternalReferenceRequest request, CancellationToken cancellationToken);
}

public sealed partial class ACadSharpCadImporter
{
    /// <summary>
    /// Imports the requested drawing and then resolves eligible empty Xref definitions only through
    /// the supplied host resolver. Ordinary <see cref="ImportAsync"/> never follows Xref paths.
    /// </summary>
    public async Task<ImportResult> ImportWithExternalReferencesAsync(
        ImportRequest request,
        ICadExternalReferenceResolver resolver,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolver);

        var local = await ImportAsync(request, progress, cancellationToken).ConfigureAwait(false);
        if (local.Document is not CadDocument localDocument) return local;

        return await Task.Run(
            () => ExpandExternalReferences(request, localDocument, local.Diagnostics, resolver, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private static ImportResult ExpandExternalReferences(
        ImportRequest request,
        CadDocument localDocument,
        IReadOnlyList<Diagnostic> localDiagnostics,
        ICadExternalReferenceResolver resolver,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>(localDiagnostics);
        global::ACadSharp.CadDocument source;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var reader = CadReaderFactory.CreateReader(request.FilePath);
            source = reader.Read();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(SafeXrefDiagnostic(
                DiagnosticSeverity.Warning,
                "CAD_XREF_PARENT_RESCAN_FAILED",
                "External-reference discovery could not rescan the already imported parent drawing; local content was kept unchanged.",
                exception.GetType().Name));
            return RebuildDocument(localDocument, diagnostics, localDocument.CadLayers, localDocument.Blocks, XrefResolutionStats.Empty);
        }

        var blocks = localDocument.Blocks.ToDictionary(block => block.Name, StringComparer.OrdinalIgnoreCase);
        var layers = localDocument.CadLayers.ToDictionary(layer => layer.Name, StringComparer.OrdinalIgnoreCase);
        var stats = new MutableXrefResolutionStats();
        var ordinal = 0;

        foreach (var record in source.BlockRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsXref(record)) continue;

            stats.DefinitionCount++;
            ordinal++;

            // Cached Xref geometry already present in the parent is authoritative and needs no host access.
            if (record.Entities.Count > 0)
            {
                stats.LocalCacheCount++;
                continue;
            }

            // Respect an intentionally unloaded reference instead of silently changing drawing state.
            if (record.IsUnloaded)
            {
                stats.UnloadedCount++;
                continue;
            }

            var sourceReference = record.BlockEntity.XRefPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sourceReference))
            {
                stats.MissingSourceReferenceCount++;
                continue;
            }

            stats.ResolverRequestCount++;
            CadExternalReferenceResource? resource;
            try
            {
                resource = resolver.Resolve(
                    new CadExternalReferenceRequest(
                        request.FilePath,
                        record.Name,
                        sourceReference,
                        (record.Flags & BlockTypeFlags.XRefOverlay) != 0),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                stats.FailedCount++;
                diagnostics.Add(SafeXrefDiagnostic(
                    DiagnosticSeverity.Warning,
                    "CAD_XREF_RESOLVER_FAILED",
                    "The host Xref resolver failed for one reference; the parent drawing remains available without that external geometry.",
                    exception.GetType().Name));
                continue;
            }

            if (resource is null)
            {
                stats.DeclinedCount++;
                continue;
            }

            using var content = resource.Content;
            if (content is null || !content.CanRead || !content.CanSeek)
            {
                stats.FailedCount++;
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "CAD_XREF_RESOURCE_INVALID",
                    "The host supplied an Xref resource that is not both readable and seekable; it was rejected without probing any source path."));
                continue;
            }

            try
            {
                content.Position = 0;
                var externalSource = ReadExternalSource(content, resource.Format);
                var expansion = MapExternalSource(externalSource, record.Name, ordinal, diagnostics);

                blocks[record.Name] = expansion.RootDefinition;
                foreach (var block in expansion.NestedBlocks) blocks[block.Name] = block;
                foreach (var layer in expansion.Layers) layers.TryAdd(layer.Name, layer);

                stats.ResolvedCount++;
                stats.NestedBlockCount += expansion.NestedBlocks.Count;
                stats.NestedXrefDefinitionCount += expansion.NestedXrefDefinitionCount;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                stats.FailedCount++;
                diagnostics.Add(SafeXrefDiagnostic(
                    DiagnosticSeverity.Warning,
                    "CAD_XREF_RESOURCE_READ_FAILED",
                    "A host-supplied Xref resource could not be read as the declared CAD format; the parent drawing remains available without that external geometry.",
                    exception.GetType().Name));
            }
        }

        var frozen = stats.Freeze();
        if (frozen.DefinitionCount > 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Info,
                "CAD_XREF_RESOLUTION_SUMMARY",
                "External-reference resolution completed through the explicit host resolver boundary.",
                frozen.ToMetadata()));
        }

        return RebuildDocument(localDocument, diagnostics, layers.Values.ToArray(), blocks.Values.ToArray(), frozen);
    }

    private static global::ACadSharp.CadDocument ReadExternalSource(Stream stream, CadExternalReferenceFormat format)
    {
        return format switch
        {
            CadExternalReferenceFormat.Dxf => ReadDxf(stream),
            CadExternalReferenceFormat.Dwg => ReadDwg(stream),
            _ => throw new NotSupportedException("Unsupported host-supplied Xref format.")
        };

        static global::ACadSharp.CadDocument ReadDxf(Stream stream)
        {
            using var reader = new DxfReader(stream);
            return reader.Read();
        }

        static global::ACadSharp.CadDocument ReadDwg(Stream stream)
        {
            using var reader = new DwgReader(stream);
            return reader.Read();
        }
    }

    private static ExternalSourceExpansion MapExternalSource(
        global::ACadSharp.CadDocument externalSource,
        string rootName,
        int ordinal,
        List<Diagnostic> diagnostics)
    {
        var globalLineTypeScale = externalSource.Header.LineTypeScale;
        var namespacePrefix = $"__XREF_{ordinal:D4}__";
        var handlePrefix = $"XREF{ordinal:D4}:";

        var externalBlocks = MapBlocks(externalSource, diagnostics, globalLineTypeScale);
        var blockNames = externalBlocks.ToDictionary(
            block => block.Name,
            block => $"{namespacePrefix}::{block.Name}",
            StringComparer.OrdinalIgnoreCase);

        var layerNames = externalSource.Layers.ToDictionary(
            layer => layer.Name,
            layer => string.Equals(layer.Name, "0", StringComparison.OrdinalIgnoreCase)
                ? "0"
                : $"{namespacePrefix}|{layer.Name}",
            StringComparer.OrdinalIgnoreCase);

        var layers = externalSource.Layers
            .Where(layer => !string.Equals(layer.Name, "0", StringComparison.OrdinalIgnoreCase))
            .Select(layer => new CadLayer(
                layerNames[layer.Name],
                MapColor(layer.Color),
                layer.IsOn,
                false,
                NameOf(layer.LineType),
                ParseLineWeight(layer.LineWeight)))
            .ToArray();

        var rootEntities = externalSource.Entities
            .Select(entity => MapEntity(entity, diagnostics, globalLineTypeScale))
            .Select(entity => NamespaceExternalEntity(entity, handlePrefix, layerNames, blockNames))
            .ToArray();

        var nestedBlocks = externalBlocks
            .Select(block => new CadBlockDefinition(
                blockNames[block.Name],
                block.BasePoint,
                block.Entities.Select(entity => NamespaceExternalEntity(entity, handlePrefix, layerNames, blockNames)).ToArray()))
            .ToArray();

        var nestedXrefDefinitionCount = externalSource.BlockRecords.Count(IsXref);
        var rootBasePoint = Point(externalSource.Header.ModelSpaceInsertionBase);
        return new ExternalSourceExpansion(
            new CadBlockDefinition(rootName, rootBasePoint, rootEntities),
            nestedBlocks,
            layers,
            nestedXrefDefinitionCount);
    }

    private static CadEntity NamespaceExternalEntity(
        CadEntity entity,
        string handlePrefix,
        IReadOnlyDictionary<string, string> layerNames,
        IReadOnlyDictionary<string, string> blockNames)
    {
        var layerName = layerNames.TryGetValue(entity.LayerName, out var mappedLayer) ? mappedLayer : entity.LayerName;
        var handle = handlePrefix + entity.Handle;

        if (entity is CadBlockReferenceEntity reference)
        {
            var blockName = blockNames.TryGetValue(reference.BlockName, out var mappedBlock)
                ? mappedBlock
                : reference.BlockName;
            var attributes = reference.Attributes.Select(attribute =>
            {
                var attributeLayer = layerNames.TryGetValue(attribute.LayerName, out var mappedAttributeLayer)
                    ? mappedAttributeLayer
                    : attribute.LayerName;
                return attribute with
                {
                    Handle = handlePrefix + attribute.Handle,
                    LayerName = attributeLayer
                };
            }).ToArray();

            return reference with
            {
                Handle = handle,
                LayerName = layerName,
                BlockName = blockName,
                Attributes = attributes
            };
        }

        return entity with { Handle = handle, LayerName = layerName };
    }

    private static ImportResult RebuildDocument(
        CadDocument source,
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<CadLayer> layers,
        IReadOnlyList<CadBlockDefinition> blocks,
        XrefResolutionStats stats)
    {
        var metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal);
        foreach (var pair in stats.ToMetadata()) metadata[pair.Key] = pair.Value;

        var rebuilt = new CadDocument(
            source.DisplayName,
            source.SourceFormat,
            source.Version,
            source.Units,
            layers,
            blocks,
            source.ModelSpace,
            diagnostics,
            metadata,
            source.Layouts)
        {
            CustomClasses = source.CustomClasses
        };
        return new ImportResult(rebuilt, diagnostics);
    }

    private static Diagnostic SafeXrefDiagnostic(DiagnosticSeverity severity, string code, string message, string failureType)
        => new(
            severity,
            code,
            message,
            new Dictionary<string, string> { ["FailureType"] = failureType });

    private static bool IsXref(global::ACadSharp.Tables.BlockRecord record)
        => (record.Flags & (BlockTypeFlags.XRef | BlockTypeFlags.XRefOverlay)) != 0;

    private sealed record ExternalSourceExpansion(
        CadBlockDefinition RootDefinition,
        IReadOnlyList<CadBlockDefinition> NestedBlocks,
        IReadOnlyList<CadLayer> Layers,
        int NestedXrefDefinitionCount);

    private sealed class MutableXrefResolutionStats
    {
        public int DefinitionCount { get; set; }
        public int LocalCacheCount { get; set; }
        public int UnloadedCount { get; set; }
        public int MissingSourceReferenceCount { get; set; }
        public int ResolverRequestCount { get; set; }
        public int DeclinedCount { get; set; }
        public int ResolvedCount { get; set; }
        public int FailedCount { get; set; }
        public int NestedBlockCount { get; set; }
        public int NestedXrefDefinitionCount { get; set; }

        public XrefResolutionStats Freeze() => new(
            DefinitionCount,
            LocalCacheCount,
            UnloadedCount,
            MissingSourceReferenceCount,
            ResolverRequestCount,
            DeclinedCount,
            ResolvedCount,
            FailedCount,
            NestedBlockCount,
            NestedXrefDefinitionCount);
    }

    private sealed record XrefResolutionStats(
        int DefinitionCount,
        int LocalCacheCount,
        int UnloadedCount,
        int MissingSourceReferenceCount,
        int ResolverRequestCount,
        int DeclinedCount,
        int ResolvedCount,
        int FailedCount,
        int NestedBlockCount,
        int NestedXrefDefinitionCount)
    {
        public static XrefResolutionStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        public IReadOnlyDictionary<string, string> ToMetadata() => new Dictionary<string, string>
        {
            ["XrefDefinitionCount"] = DefinitionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefLocalCacheCount"] = LocalCacheCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefUnloadedCount"] = UnloadedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefMissingSourceReferenceCount"] = MissingSourceReferenceCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefResolverRequestCount"] = ResolverRequestCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefResolverDeclinedCount"] = DeclinedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefResolverResolvedCount"] = ResolvedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefResolverFailedCount"] = FailedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefResolvedNestedBlockCount"] = NestedBlockCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["XrefNestedDependencyCount"] = NestedXrefDefinitionCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
