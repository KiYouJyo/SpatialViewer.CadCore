using SpatialViewer.Core;

namespace SpatialViewer.Formats.Cad;

/// <summary>Meaning assigned only after a retained custom-object handle reference resolves to a real drawing entity.</summary>
public enum CadCustomRelationshipKind
{
    ObjectReference,
    TianzhengOpeningHostWall
}

/// <summary>
/// One resolved edge between an application-defined CAD entity and another entity in the same document.
/// The original DXF group code remains attached so later decoders can distinguish soft/hard pointer forms.
/// </summary>
public sealed record CadCustomRelationship(
    string SourceHandle,
    string TargetHandle,
    int GroupCode,
    CadCustomRelationshipKind Kind,
    string SourceEntityType,
    string TargetEntityType)
{
    public ObjectId SourceObjectId => CadIds.ToObjectId(SourceHandle);
    public ObjectId TargetObjectId => CadIds.ToObjectId(TargetHandle);
}

/// <summary>
/// Resolves raw custom-object references against the entities that actually survived import. Vendor-specific
/// meaning is assigned only when both endpoints provide strong type identity; reference position/order is never guessed.
/// </summary>
public static class CadCustomRelationshipResolver
{
    public static IReadOnlyList<CadCustomRelationship> Resolve(
        IReadOnlyList<CadEntity> modelSpace,
        IReadOnlyList<CadBlockDefinition> blocks,
        IReadOnlyList<CadLayoutDefinition> layouts)
    {
        ArgumentNullException.ThrowIfNull(modelSpace);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(layouts);

        var entities = EnumerateEntities(modelSpace, blocks, layouts).ToArray();
        var byHandle = new Dictionary<string, CadEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            if (!string.IsNullOrWhiteSpace(entity.Handle)) byHandle.TryAdd(entity.Handle, entity);
        }

        var relationships = new List<CadCustomRelationship>();
        var seen = new HashSet<RelationshipKey>();
        foreach (var source in entities.OfType<CadCustomEntity>())
        {
            foreach (var reference in source.HandleReferences)
            {
                if (!byHandle.TryGetValue(reference.TargetHandle, out var target)) continue;
                var kind = IsTianzhengOpening(source) && target is CadCustomEntity customTarget && IsTianzhengWall(customTarget)
                    ? CadCustomRelationshipKind.TianzhengOpeningHostWall
                    : CadCustomRelationshipKind.ObjectReference;
                var key = new RelationshipKey(source.Handle, target.Handle, reference.GroupCode, kind);
                if (!seen.Add(key)) continue;
                relationships.Add(new CadCustomRelationship(
                    source.Handle,
                    target.Handle,
                    reference.GroupCode,
                    kind,
                    CustomType(source),
                    CustomType(target)));
            }
        }

        return relationships;
    }

    private static IEnumerable<CadEntity> EnumerateEntities(
        IReadOnlyList<CadEntity> modelSpace,
        IReadOnlyList<CadBlockDefinition> blocks,
        IReadOnlyList<CadLayoutDefinition> layouts)
    {
        foreach (var entity in modelSpace) yield return entity;
        foreach (var block in blocks)
            foreach (var entity in block.Entities)
                yield return entity;
        foreach (var layout in layouts.Where(layout => layout.IsPaperSpace))
            foreach (var entity in layout.Entities)
                yield return entity;
    }

    private static bool IsTianzhengOpening(CadCustomEntity entity)
        => string.Equals(entity.SourceEntityType, "TCH_OPENING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.ClassDefinition?.DxfName, "TCH_OPENING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.ClassDefinition?.CppClassName, "TDbOpening", StringComparison.OrdinalIgnoreCase);

    private static bool IsTianzhengWall(CadCustomEntity entity)
        => entity.NativeSemantics is CadTianzhengWallSemantic
            || string.Equals(entity.SourceEntityType, "TCH_WALL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.ClassDefinition?.DxfName, "TCH_WALL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.ClassDefinition?.CppClassName, "TDbWall", StringComparison.OrdinalIgnoreCase);

    private static string CustomType(CadEntity entity)
        => entity is CadCustomEntity custom && !string.IsNullOrWhiteSpace(custom.ClassDefinition?.DxfName)
            ? custom.ClassDefinition.DxfName
            : entity is CadCustomEntity customEntity
                ? customEntity.SourceEntityType
                : entity.GetType().Name.Replace("Cad", string.Empty, StringComparison.Ordinal).Replace("Entity", string.Empty, StringComparison.Ordinal);

    private readonly record struct RelationshipKey(
        string SourceHandle,
        string TargetHandle,
        int GroupCode,
        CadCustomRelationshipKind Kind);
}
