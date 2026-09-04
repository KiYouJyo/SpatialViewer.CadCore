using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace SpatialViewer.Formats.Cad;

public enum CadProxyGeometryDiffStatus
{
    Comparable,
    LayoutMismatch,
    MissingProxyGraphics
}

/// <summary>Anonymous location of a changed proxy-geometry value. No coordinate or source drawing value is retained.</summary>
public enum CadProxyGeometryField
{
    PointX,
    PointY,
    Bulge,
    IsClosed,
    CenterX,
    CenterY,
    Radius,
    StartRadians,
    SweepRadians,
    EdgeStartX,
    EdgeStartY,
    EdgeEndX,
    EdgeEndY,
    ClipPointX,
    ClipPointY,
    DrawBoundary,
    TextOriginX,
    TextOriginY,
    TextHeight,
    TextRotation,
    TextWidthFactor,
    TextObliqueAngle,
    TextTracking,
    TextContent,
    FacePointX,
    FacePointY
}

public sealed record CadProxyGeometryValueChange(
    string PrimitivePath,
    CadProxyGeometryField Field,
    int ElementIndex = -1);

/// <summary>
/// Privacy-safe comparison of two proxy-graphics trees. Layout fingerprints are derived only from primitive
/// kinds/counts; changed coordinates/text are represented by anonymous structural positions, never values.
/// </summary>
public sealed record CadProxyGeometryDiffReport(
    CadProxyGeometryDiffStatus Status,
    string BeforeLayoutFingerprint,
    string AfterLayoutFingerprint,
    IReadOnlyList<CadProxyGeometryValueChange> ValueChanges)
{
    public bool IsComparable => Status == CadProxyGeometryDiffStatus.Comparable;
    public int ChangedValueCount => ValueChanges.Count;
}

public static class CadProxyGeometryDiffer
{
    public static CadProxyGeometryDiffReport Compare(CadCustomEntity before, CadCustomEntity after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        CadDxfCustomPayloadDiffer.ValidateEntityIdentity(before, after);

        if (before.ProxyPrimitives.Count == 0 || after.ProxyPrimitives.Count == 0)
        {
            return Report(
                CadProxyGeometryDiffStatus.MissingProxyGraphics,
                LayoutFingerprint(before.ProxyPrimitives),
                LayoutFingerprint(after.ProxyPrimitives),
                Array.Empty<CadProxyGeometryValueChange>());
        }

        var beforeFingerprint = LayoutFingerprint(before.ProxyPrimitives);
        var afterFingerprint = LayoutFingerprint(after.ProxyPrimitives);
        if (!string.Equals(beforeFingerprint, afterFingerprint, StringComparison.Ordinal))
        {
            return Report(
                CadProxyGeometryDiffStatus.LayoutMismatch,
                beforeFingerprint,
                afterFingerprint,
                Array.Empty<CadProxyGeometryValueChange>());
        }

        var changes = new List<CadProxyGeometryValueChange>();
        CompareList(before.ProxyPrimitives, after.ProxyPrimitives, string.Empty, changes);
        return Report(
            CadProxyGeometryDiffStatus.Comparable,
            beforeFingerprint,
            afterFingerprint,
            changes
                .Distinct()
                .OrderBy(change => change.PrimitivePath, StringComparer.Ordinal)
                .ThenBy(change => change.Field)
                .ThenBy(change => change.ElementIndex)
                .ToArray());
    }

    private static void CompareList(
        IReadOnlyList<CadProxyPrimitive> before,
        IReadOnlyList<CadProxyPrimitive> after,
        string parentPath,
        List<CadProxyGeometryValueChange> changes)
    {
        for (var index = 0; index < before.Count; index++)
        {
            var path = parentPath.Length == 0 ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{parentPath}/{index}";
            ComparePrimitive(before[index], after[index], path, changes);
        }
    }

    private static void ComparePrimitive(
        CadProxyPrimitive before,
        CadProxyPrimitive after,
        string path,
        List<CadProxyGeometryValueChange> changes)
    {
        switch (before, after)
        {
            case (CadProxyPolyline a, CadProxyPolyline b):
                ComparePoints(a.Points, b.Points, path, CadProxyGeometryField.PointX, CadProxyGeometryField.PointY, changes);
                break;
            case (CadProxyLwPolyline a, CadProxyLwPolyline b):
                ComparePoints(a.Points, b.Points, path, CadProxyGeometryField.PointX, CadProxyGeometryField.PointY, changes);
                for (var i = 0; i < a.Bulges.Count; i++)
                    if (!Equal(a.Bulges[i], b.Bulges[i])) changes.Add(new(path, CadProxyGeometryField.Bulge, i));
                if (a.IsClosed != b.IsClosed) changes.Add(new(path, CadProxyGeometryField.IsClosed));
                break;
            case (CadProxyPolygon a, CadProxyPolygon b):
                ComparePoints(a.Points, b.Points, path, CadProxyGeometryField.PointX, CadProxyGeometryField.PointY, changes);
                break;
            case (CadProxyCircle a, CadProxyCircle b):
                ComparePoint(a.Center, b.Center, path, CadProxyGeometryField.CenterX, CadProxyGeometryField.CenterY, -1, changes);
                if (!Equal(a.Radius, b.Radius)) changes.Add(new(path, CadProxyGeometryField.Radius));
                break;
            case (CadProxyArc a, CadProxyArc b):
                ComparePoint(a.Center, b.Center, path, CadProxyGeometryField.CenterX, CadProxyGeometryField.CenterY, -1, changes);
                if (!Equal(a.Radius, b.Radius)) changes.Add(new(path, CadProxyGeometryField.Radius));
                if (!Equal(a.StartRadians, b.StartRadians)) changes.Add(new(path, CadProxyGeometryField.StartRadians));
                if (!Equal(a.SweepRadians, b.SweepRadians)) changes.Add(new(path, CadProxyGeometryField.SweepRadians));
                break;
            case (CadProxyEdgeSet a, CadProxyEdgeSet b):
                for (var i = 0; i < a.Edges.Count; i++)
                {
                    ComparePoint(a.Edges[i].Start, b.Edges[i].Start, path, CadProxyGeometryField.EdgeStartX, CadProxyGeometryField.EdgeStartY, i, changes);
                    ComparePoint(a.Edges[i].End, b.Edges[i].End, path, CadProxyGeometryField.EdgeEndX, CadProxyGeometryField.EdgeEndY, i, changes);
                }
                break;
            case (CadProxySurfaceSet a, CadProxySurfaceSet b):
                for (var faceIndex = 0; faceIndex < a.Faces.Count; faceIndex++)
                {
                    var facePath = $"{path}/F{faceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    ComparePoints(
                        a.Faces[faceIndex].Points,
                        b.Faces[faceIndex].Points,
                        facePath,
                        CadProxyGeometryField.FacePointX,
                        CadProxyGeometryField.FacePointY,
                        changes);
                }
                for (var edgeIndex = 0; edgeIndex < a.Edges.Count; edgeIndex++)
                {
                    ComparePoint(a.Edges[edgeIndex].Start, b.Edges[edgeIndex].Start, path, CadProxyGeometryField.EdgeStartX, CadProxyGeometryField.EdgeStartY, edgeIndex, changes);
                    ComparePoint(a.Edges[edgeIndex].End, b.Edges[edgeIndex].End, path, CadProxyGeometryField.EdgeEndX, CadProxyGeometryField.EdgeEndY, edgeIndex, changes);
                }
                break;
            case (CadProxyClipGroup a, CadProxyClipGroup b):
                ComparePoints(a.ClipPolygon, b.ClipPolygon, path, CadProxyGeometryField.ClipPointX, CadProxyGeometryField.ClipPointY, changes);
                if (a.DrawBoundary != b.DrawBoundary) changes.Add(new(path, CadProxyGeometryField.DrawBoundary));
                CompareList(a.Children, b.Children, path, changes);
                break;
            case (CadProxyText a, CadProxyText b):
                ComparePoint(a.Origin, b.Origin, path, CadProxyGeometryField.TextOriginX, CadProxyGeometryField.TextOriginY, -1, changes);
                if (!Equal(a.Height, b.Height)) changes.Add(new(path, CadProxyGeometryField.TextHeight));
                if (!Equal(a.RotationRadians, b.RotationRadians)) changes.Add(new(path, CadProxyGeometryField.TextRotation));
                if (!Equal(a.WidthFactor, b.WidthFactor)) changes.Add(new(path, CadProxyGeometryField.TextWidthFactor));
                if (!Equal(a.ObliqueAngleRadians, b.ObliqueAngleRadians)) changes.Add(new(path, CadProxyGeometryField.TextObliqueAngle));
                if (!Equal(a.TrackingPercentage, b.TrackingPercentage)) changes.Add(new(path, CadProxyGeometryField.TextTracking));
                if (!string.Equals(a.Text, b.Text, StringComparison.Ordinal)) changes.Add(new(path, CadProxyGeometryField.TextContent));
                break;
            default:
                throw new InvalidOperationException("Proxy geometry layouts matched but primitive runtime types differed.");
        }
    }

    private static void ComparePoints(
        IReadOnlyList<SpatialViewer.Core.Point2D> before,
        IReadOnlyList<SpatialViewer.Core.Point2D> after,
        string path,
        CadProxyGeometryField xField,
        CadProxyGeometryField yField,
        List<CadProxyGeometryValueChange> changes)
    {
        for (var i = 0; i < before.Count; i++) ComparePoint(before[i], after[i], path, xField, yField, i, changes);
    }

    private static void ComparePoint(
        SpatialViewer.Core.Point2D before,
        SpatialViewer.Core.Point2D after,
        string path,
        CadProxyGeometryField xField,
        CadProxyGeometryField yField,
        int index,
        List<CadProxyGeometryValueChange> changes)
    {
        if (!Equal(before.X, after.X)) changes.Add(new(path, xField, index));
        if (!Equal(before.Y, after.Y)) changes.Add(new(path, yField, index));
    }

    private static bool Equal(double left, double right)
        => left.Equals(right);

    private static CadProxyGeometryDiffReport Report(
        CadProxyGeometryDiffStatus status,
        string beforeFingerprint,
        string afterFingerprint,
        IEnumerable<CadProxyGeometryValueChange> changes)
        => new(
            status,
            beforeFingerprint,
            afterFingerprint,
            new ReadOnlyCollection<CadProxyGeometryValueChange>(changes.ToArray()));

    private static string LayoutFingerprint(IReadOnlyList<CadProxyPrimitive> primitives)
    {
        var builder = new StringBuilder();
        AppendLayout(primitives, string.Empty, builder);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendLayout(
        IReadOnlyList<CadProxyPrimitive> primitives,
        string parentPath,
        StringBuilder builder)
    {
        builder.Append("N=").Append(primitives.Count).Append(';');
        for (var index = 0; index < primitives.Count; index++)
        {
            var primitive = primitives[index];
            var path = parentPath.Length == 0 ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{parentPath}/{index}";
            builder.Append(path).Append('|').Append(primitive.GetType().Name).Append('|').Append(primitive.SourceKind).Append('|');
            switch (primitive)
            {
                case CadProxyPolyline item:
                    builder.Append("P=").Append(item.Points.Count);
                    break;
                case CadProxyLwPolyline item:
                    builder.Append("P=").Append(item.Points.Count).Append(",B=").Append(item.Bulges.Count);
                    break;
                case CadProxyPolygon item:
                    builder.Append("P=").Append(item.Points.Count);
                    break;
                case CadProxyCircle:
                    builder.Append('C');
                    break;
                case CadProxyArc:
                    builder.Append('A');
                    break;
                case CadProxyEdgeSet item:
                    builder.Append("E=").Append(item.Edges.Count).Append(",K=").Append(item.ProxyEdgeKind);
                    break;
                case CadProxySurfaceSet item:
                    builder.Append("F=").Append(item.Faces.Count)
                        .Append(",E=").Append(item.Edges.Count)
                        .Append(",K=").Append(item.ProxySurfaceKind);
                    for (var faceIndex = 0; faceIndex < item.Faces.Count; faceIndex++)
                        builder.Append(",P").Append(faceIndex).Append('=').Append(item.Faces[faceIndex].Points.Count);
                    break;
                case CadProxyClipGroup item:
                    builder.Append("CP=").Append(item.ClipPolygon.Count).Append(",CH=").Append(item.Children.Count).Append(';');
                    AppendLayout(item.Children, path, builder);
                    break;
                case CadProxyText item:
                    builder.Append("T=").Append(item.ProxyTextKind);
                    break;
            }
            builder.Append(';');
        }
    }
}

public sealed record CadProxyGeometryExperimentObservation(
    CadCustomExperimentIdentity Identity,
    CadProxyGeometryDiffStatus Status,
    string BeforeLayoutFingerprint,
    string AfterLayoutFingerprint,
    IReadOnlyList<CadProxyGeometryValueChange> ValueChanges);

public sealed record CadProxyGeometryExperimentConsensus(
    CadCustomExperimentIdentity Identity,
    string LayoutFingerprint,
    int ObservationCount,
    IReadOnlyList<CadProxyGeometryValueChange> StableValueChanges)
{
    public bool HasStableCandidate => StableValueChanges.Count > 0;
}

public static class CadProxyGeometryExperimentAnalyzer
{
    private const int MaxObservations = 10_000;

    public static CadProxyGeometryExperimentObservation Observe(CadCustomEntity before, CadCustomEntity after)
    {
        var diff = CadProxyGeometryDiffer.Compare(before, after);
        return new(
            Identity(before, after),
            diff.Status,
            diff.BeforeLayoutFingerprint,
            diff.AfterLayoutFingerprint,
            new ReadOnlyCollection<CadProxyGeometryValueChange>(diff.ValueChanges.ToArray()));
    }

    public static CadProxyGeometryExperimentConsensus BuildConsensus(
        IEnumerable<CadProxyGeometryExperimentObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.Take(MaxObservations + 1).ToList();
        if (items.Count < 2)
            throw new ArgumentException("At least two independent proxy-geometry observations are required.", nameof(observations));
        if (items.Count > MaxObservations)
            throw new ArgumentException($"Proxy-geometry consensus supports at most {MaxObservations} observations.", nameof(observations));

        var first = items[0] ?? throw new ArgumentException("Proxy-geometry observation cannot be null.", nameof(observations));
        if (first.Status != CadProxyGeometryDiffStatus.Comparable
            || !string.Equals(first.BeforeLayoutFingerprint, first.AfterLayoutFingerprint, StringComparison.Ordinal))
            throw new ArgumentException("Proxy-geometry consensus requires comparable observations with one unchanged layout.", nameof(observations));

        var stable = new HashSet<CadProxyGeometryValueChange>(first.ValueChanges);
        foreach (var item in items.Skip(1))
        {
            if (item is null) throw new ArgumentException("Proxy-geometry observation cannot be null.", nameof(observations));
            if (!SameIdentity(first.Identity, item.Identity))
                throw new ArgumentException("Proxy-geometry consensus observations must have the same custom-object identity.", nameof(observations));
            if (item.Status != CadProxyGeometryDiffStatus.Comparable
                || !string.Equals(item.BeforeLayoutFingerprint, item.AfterLayoutFingerprint, StringComparison.Ordinal)
                || !string.Equals(first.BeforeLayoutFingerprint, item.BeforeLayoutFingerprint, StringComparison.Ordinal))
                throw new ArgumentException("Proxy-geometry consensus requires one shared structural layout.", nameof(observations));
            stable.IntersectWith(item.ValueChanges);
        }

        var ordered = stable
            .OrderBy(change => change.PrimitivePath, StringComparer.Ordinal)
            .ThenBy(change => change.Field)
            .ThenBy(change => change.ElementIndex)
            .ToArray();
        return new(
            first.Identity,
            first.BeforeLayoutFingerprint,
            items.Count,
            new ReadOnlyCollection<CadProxyGeometryValueChange>(ordered));
    }

    private static CadCustomExperimentIdentity Identity(CadCustomEntity before, CadCustomEntity after)
    {
        var dxfName = string.IsNullOrWhiteSpace(before.ClassDefinition?.DxfName) ? before.SourceEntityType : before.ClassDefinition.DxfName;
        var cpp = string.IsNullOrWhiteSpace(before.ClassDefinition?.CppClassName) ? after.ClassDefinition?.CppClassName ?? string.Empty : before.ClassDefinition.CppClassName;
        var app = string.IsNullOrWhiteSpace(before.ClassDefinition?.ApplicationName) ? after.ClassDefinition?.ApplicationName ?? string.Empty : before.ClassDefinition.ApplicationName;
        return new(dxfName, cpp, app);
    }

    private static bool SameIdentity(CadCustomExperimentIdentity left, CadCustomExperimentIdentity right)
        => string.Equals(left.DxfName, right.DxfName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.CppClassName, right.CppClassName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.ApplicationName, right.ApplicationName, StringComparison.OrdinalIgnoreCase);
}
