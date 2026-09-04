using System.Collections.ObjectModel;

namespace SpatialViewer.Formats.Cad;

public enum CadCustomReferenceEndpointKind
{
    CustomEntity,
    BlockReference,
    Text,
    Attribute,
    Line,
    Polyline,
    Circle,
    Arc,
    Ellipse,
    Point,
    Spline,
    Hatch,
    Unsupported,
    OtherCadEntity
}

/// <summary>
/// Privacy-safe structural description of one resolved reference target. Block names, text, coordinates,
/// layers and handles are deliberately excluded. Custom targets retain CLASSES identity because it is
/// structural application metadata needed for compatibility research.
/// </summary>
public sealed record CadCustomReferenceEndpointDescriptor(
    CadCustomReferenceEndpointKind Kind,
    string DxfName,
    string CppClassName,
    string ApplicationName,
    CadCustomObjectVendor Vendor);

public enum CadCustomReferenceEndpointObservationStatus
{
    Comparable,
    ReferenceLayoutMismatch,
    SlotNotChanged,
    SourceNotInDocument,
    TargetUnresolved,
    TargetStructureMismatch
}

/// <summary>
/// Endpoint-type evidence for one changed anonymous reference slot. Source/target handles are used only
/// during local resolution and are never retained in this record.
/// </summary>
public sealed record CadCustomReferenceEndpointExperimentObservation(
    CadCustomExperimentIdentity SourceIdentity,
    CadCustomHandleReferenceValueChange Slot,
    CadCustomReferenceEndpointObservationStatus Status,
    CadCustomReferenceEndpointDescriptor? TargetDescriptor);

public sealed record CadCustomReferenceEndpointExperimentConsensus(
    CadCustomExperimentIdentity SourceIdentity,
    CadCustomHandleReferenceValueChange Slot,
    int ObservationCount,
    CadCustomReferenceEndpointDescriptor TargetDescriptor);

public static class CadCustomReferenceEndpointExperimentAnalyzer
{
    private const int MaxObservations = 10_000;

    public static CadCustomReferenceEndpointExperimentObservation Observe(
        CadDocument beforeDocument,
        CadCustomEntity before,
        CadDocument afterDocument,
        CadCustomEntity after,
        CadCustomHandleReferenceValueChange slot)
    {
        ArgumentNullException.ThrowIfNull(beforeDocument);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(afterDocument);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(slot);
        CadDxfCustomPayloadDiffer.ValidateEntityIdentity(before, after);

        var identity = Identity(before, after);
        if (!ContainsSource(beforeDocument, before) || !ContainsSource(afterDocument, after))
            return new(identity, slot, CadCustomReferenceEndpointObservationStatus.SourceNotInDocument, null);

        var diff = CadCustomHandleReferenceDiffer.Compare(before, after);
        if (diff.Status != CadCustomHandleReferenceDiffStatus.Comparable)
            return new(identity, slot, CadCustomReferenceEndpointObservationStatus.ReferenceLayoutMismatch, null);
        if (!diff.ValueChanges.Contains(slot))
            return new(identity, slot, CadCustomReferenceEndpointObservationStatus.SlotNotChanged, null);

        var beforeReference = FindSlot(before.HandleReferences, slot);
        var afterReference = FindSlot(after.HandleReferences, slot);
        if (beforeReference is null || afterReference is null)
            return new(identity, slot, CadCustomReferenceEndpointObservationStatus.ReferenceLayoutMismatch, null);

        var beforeTarget = FindEntityByHandle(beforeDocument, beforeReference.TargetHandle);
        var afterTarget = FindEntityByHandle(afterDocument, afterReference.TargetHandle);
        if (beforeTarget is null || afterTarget is null)
            return new(identity, slot, CadCustomReferenceEndpointObservationStatus.TargetUnresolved, null);

        var beforeDescriptor = Describe(beforeTarget);
        var afterDescriptor = Describe(afterTarget);
        if (beforeDescriptor != afterDescriptor)
            return new(identity, slot, CadCustomReferenceEndpointObservationStatus.TargetStructureMismatch, null);

        return new(identity, slot, CadCustomReferenceEndpointObservationStatus.Comparable, beforeDescriptor);
    }

    public static CadCustomReferenceEndpointExperimentConsensus BuildConsensus(
        IEnumerable<CadCustomReferenceEndpointExperimentObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent reference-endpoint observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"Reference-endpoint consensus supports at most {MaxObservations} observations.", nameof(observations));

        var first = items[0] ?? throw new ArgumentException("Reference-endpoint observation cannot be null.", nameof(observations));
        ValidateComparable(first, nameof(observations));
        foreach (var item in items.Skip(1))
        {
            if (item is null) throw new ArgumentException("Reference-endpoint observation cannot be null.", nameof(observations));
            ValidateComparable(item, nameof(observations));
            if (!SameIdentity(first.SourceIdentity, item.SourceIdentity))
                throw new ArgumentException("Reference-endpoint observations must have the same source custom-object identity.", nameof(observations));
            if (first.Slot != item.Slot)
                throw new ArgumentException("Reference-endpoint observations must describe the same anonymous reference slot.", nameof(observations));
            if (first.TargetDescriptor != item.TargetDescriptor)
                throw new ArgumentException("Reference-endpoint observations do not agree on one target structural descriptor.", nameof(observations));
        }

        return new(
            first.SourceIdentity,
            first.Slot,
            items.Count,
            first.TargetDescriptor!);
    }

    private static void ValidateComparable(
        CadCustomReferenceEndpointExperimentObservation observation,
        string parameterName)
    {
        if (observation.Status != CadCustomReferenceEndpointObservationStatus.Comparable
            || observation.TargetDescriptor is null)
        {
            throw new ArgumentException(
                "Reference-endpoint consensus requires resolved, structurally stable endpoint observations.",
                parameterName);
        }
    }

    private static bool ContainsSource(CadDocument document, CadCustomEntity source)
    {
        var entity = FindEntityByHandle(document, source.Handle) as CadCustomEntity;
        if (entity is null) return false;
        try
        {
            CadDxfCustomPayloadDiffer.ValidateEntityIdentity(source, entity);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static CadCustomHandleReference? FindSlot(
        IReadOnlyList<CadCustomHandleReference> references,
        CadCustomHandleReferenceValueChange slot)
    {
        var occurrence = 0;
        foreach (var reference in references)
        {
            if (reference.GroupCode != slot.GroupCode) continue;
            occurrence++;
            if (occurrence == slot.CodeOccurrence) return reference;
        }
        return null;
    }

    private static CadEntity? FindEntityByHandle(CadDocument document, string handle)
    {
        foreach (var entity in EnumerateEntities(document))
            if (string.Equals(entity.Handle, handle, StringComparison.OrdinalIgnoreCase)) return entity;
        return null;
    }

    private static IEnumerable<CadEntity> EnumerateEntities(CadDocument document)
    {
        foreach (var entity in document.ModelSpace) yield return entity;
        foreach (var block in document.Blocks)
            foreach (var entity in block.Entities)
                yield return entity;
        foreach (var layout in document.Layouts.Where(layout => layout.IsPaperSpace))
            foreach (var entity in layout.Entities)
                yield return entity;
    }

    private static CadCustomReferenceEndpointDescriptor Describe(CadEntity target)
    {
        if (target is CadCustomEntity custom)
        {
            return new(
                CadCustomReferenceEndpointKind.CustomEntity,
                string.IsNullOrWhiteSpace(custom.ClassDefinition?.DxfName)
                    ? custom.SourceEntityType
                    : custom.ClassDefinition.DxfName,
                custom.ClassDefinition?.CppClassName ?? string.Empty,
                custom.ClassDefinition?.ApplicationName ?? string.Empty,
                custom.Vendor);
        }

        return new(
            Kind(target),
            string.Empty,
            string.Empty,
            string.Empty,
            CadCustomObjectVendor.Unknown);
    }

    private static CadCustomReferenceEndpointKind Kind(CadEntity entity)
        => entity switch
        {
            CadBlockReferenceEntity => CadCustomReferenceEndpointKind.BlockReference,
            CadTextEntity => CadCustomReferenceEndpointKind.Text,
            CadAttributeEntity => CadCustomReferenceEndpointKind.Attribute,
            CadLineEntity => CadCustomReferenceEndpointKind.Line,
            CadPolylineEntity => CadCustomReferenceEndpointKind.Polyline,
            CadCircleEntity => CadCustomReferenceEndpointKind.Circle,
            CadArcEntity => CadCustomReferenceEndpointKind.Arc,
            CadEllipseEntity => CadCustomReferenceEndpointKind.Ellipse,
            CadPointEntity => CadCustomReferenceEndpointKind.Point,
            CadSplineEntity => CadCustomReferenceEndpointKind.Spline,
            CadHatchEntity => CadCustomReferenceEndpointKind.Hatch,
            CadUnsupportedEntity => CadCustomReferenceEndpointKind.Unsupported,
            _ => CadCustomReferenceEndpointKind.OtherCadEntity
        };

    private static CadCustomExperimentIdentity Identity(CadCustomEntity before, CadCustomEntity after)
    {
        var dxfName = string.IsNullOrWhiteSpace(before.ClassDefinition?.DxfName)
            ? before.SourceEntityType
            : before.ClassDefinition.DxfName;
        var cpp = string.IsNullOrWhiteSpace(before.ClassDefinition?.CppClassName)
            ? after.ClassDefinition?.CppClassName ?? string.Empty
            : before.ClassDefinition.CppClassName;
        var application = string.IsNullOrWhiteSpace(before.ClassDefinition?.ApplicationName)
            ? after.ClassDefinition?.ApplicationName ?? string.Empty
            : before.ClassDefinition.ApplicationName;
        return new(dxfName, cpp, application);
    }

    private static bool SameIdentity(CadCustomExperimentIdentity left, CadCustomExperimentIdentity right)
        => string.Equals(left.DxfName, right.DxfName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.CppClassName, right.CppClassName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.ApplicationName, right.ApplicationName, StringComparison.OrdinalIgnoreCase);
}
