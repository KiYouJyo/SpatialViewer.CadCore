using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.Tables;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Privacy-safe structural profile for locating CAD content that can otherwise look "missing"
/// when a host only inspects model space or when external content is unavailable.
/// Counts only; no drawing text, handles, file-system paths or Xref path names are retained.
/// </summary>
public sealed record CadSourceContentProfile(
    int ModelSpaceEntityCount,
    int PaperSpaceEntityCount,
    int PaperViewportCount,
    int BlockDefinitionCount,
    int ModelSpaceBlockReferenceCount,
    int PaperSpaceBlockReferenceCount,
    int AnonymousBlockDefinitionCount,
    int AnonymousBlockReferenceCount,
    int TableEntityCount,
    int TableCacheBlockDefinitionCount,
    int ExternalReferenceDefinitionCount,
    int ExternalReferenceReferenceCount,
    int UnloadedExternalReferenceDefinitionCount,
    int EmptyExternalReferenceDefinitionCount)
{
    public bool HasPaperSpaceContent => PaperSpaceEntityCount > 0 || PaperViewportCount > 0;
    public bool HasAnonymousBlockContent => AnonymousBlockDefinitionCount > 0 || AnonymousBlockReferenceCount > 0;
    public bool HasTableContent => TableEntityCount > 0 || TableCacheBlockDefinitionCount > 0;
    public bool HasExternalReferenceDependency => ExternalReferenceDefinitionCount > 0 || ExternalReferenceReferenceCount > 0;
}

/// <summary>
/// Examines source-level ACadSharp structure without interpreting proprietary application semantics.
/// This is intended for fidelity diagnostics such as distinguishing Paper Space, anonymous/dynamic
/// blocks, TABLE cache blocks and Xrefs.
/// </summary>
public static class CadSourceContentProfiler
{
    public static CadSourceContentProfile AnalyzeFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath)) throw new FileNotFoundException("CAD source file was not found.", filePath);

        var extension = Path.GetExtension(filePath);
        if (!extension.Equals(".dwg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".dxf", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Unsupported CAD source extension: {extension}");

        using var reader = CadReaderFactory.CreateReader(filePath);
        return Analyze(reader.Read());
    }

    internal static CadSourceContentProfile Analyze(global::ACadSharp.CadDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var modelEntities = source.Entities.ToArray();
        var paperLayouts = source.Layouts.Where(layout => layout.IsPaperSpace).ToArray();
        var paperEntities = paperLayouts
            .SelectMany(layout => layout.AssociatedBlock is { } block
                ? block.Entities.AsEnumerable()
                : Enumerable.Empty<Entity>())
            .Where(entity => entity is not Viewport)
            .ToArray();
        var paperViewportCount = paperLayouts.Sum(layout => layout.Viewports.Count());

        var blockDefinitions = source.BlockRecords
            .Where(record => !IsSpaceRecord(record.Name))
            .ToArray();
        var blockDefinitionEntities = blockDefinitions.SelectMany(record => record.Entities).ToArray();
        var allEntities = modelEntities.Concat(paperEntities).Concat(blockDefinitionEntities).ToArray();

        var modelReferences = modelEntities.OfType<Insert>().ToArray();
        var paperReferences = paperEntities.OfType<Insert>().ToArray();
        var allReferences = allEntities.OfType<Insert>().ToArray();

        var anonymousDefinitions = blockDefinitions.Where(IsAnonymous).ToArray();
        var anonymousReferences = allReferences.Count(reference => reference.Block is { } block && IsAnonymous(block));

        var tables = allEntities.OfType<TableEntity>().ToArray();
        var tableCacheNames = new HashSet<string>(
            tables.Select(table => table.Block?.Name)
                .OfType<string>()
                .Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);
        var tableCacheDefinitions = blockDefinitions.Count(record => tableCacheNames.Contains(record.Name));

        var externalDefinitions = blockDefinitions.Where(IsExternalReference).ToArray();
        var externalReferences = allReferences.Count(reference => reference.Block is { } block && IsExternalReference(block));

        return new CadSourceContentProfile(
            modelEntities.Length,
            paperEntities.Length,
            paperViewportCount,
            blockDefinitions.Length,
            modelReferences.Length,
            paperReferences.Length,
            anonymousDefinitions.Length,
            anonymousReferences,
            tables.Length,
            tableCacheDefinitions,
            externalDefinitions.Length,
            externalReferences,
            externalDefinitions.Count(record => record.IsUnloaded),
            externalDefinitions.Count(record => !record.Entities.Any()));
    }

    private static bool IsSpaceRecord(string name)
        => name.StartsWith("*Model_Space", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase);

    private static bool IsAnonymous(BlockRecord record)
        => record.IsAnonymous || record.Name.StartsWith('*');

    private static bool IsExternalReference(BlockRecord record)
        => (record.Flags & (BlockTypeFlags.XRef | BlockTypeFlags.XRefOverlay)) != 0;
}
