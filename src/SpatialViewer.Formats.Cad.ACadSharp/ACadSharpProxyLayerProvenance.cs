using ACadSharp.Entities.ProxyGraphics;
using SpatialViewer.Formats.Cad;

namespace SpatialViewer.Formats.Cad.ACadSharp;

/// <summary>
/// Preserves the serialized SUBENT_LAYER state on already validated proxy primitives without guessing
/// how ACAD_PROXY_ENTITY's layer-reference index maps back to a layer table record. The source proxy
/// stream and mapped primitive stream must reconcile one-for-one; otherwise provenance is withheld.
/// </summary>
internal static class ACadSharpProxyLayerProvenance
{
    public static IReadOnlyList<CadProxyPrimitive> Apply(
        IEnumerable<IProxyGeometry> graphics,
        IReadOnlyList<CadProxyPrimitive> mapped,
        out int handledLayerCommandCount)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(mapped);

        var source = graphics.ToArray();
        var layerCommandCount = source.Count(graphic => graphic is ProxySubentLayer);
        handledLayerCommandCount = 0;
        if (layerCommandCount == 0 || mapped.Count == 0) return mapped;

        var layerStates = new List<int?>();
        int? currentLayerIndex = null;
        foreach (var graphic in source)
        {
            if (graphic is ProxySubentLayer layer)
            {
                currentLayerIndex = layer.LayerIndex;
                continue;
            }

            if (CanEmitPrimitive(graphic.GraphicsType))
                layerStates.Add(currentLayerIndex);
        }

        var mappedLeafCount = mapped.Sum(CountLeaves);
        if (mappedLeafCount != layerStates.Count)
            return mapped;

        var stateIndex = 0;
        var rewritten = mapped
            .Select(primitive => Rewrite(primitive, layerStates, ref stateIndex))
            .ToArray();
        if (stateIndex != layerStates.Count)
            return mapped;

        handledLayerCommandCount = layerCommandCount;
        return rewritten;
    }

    public static IReadOnlyList<int> CollectLayerIndices(IEnumerable<CadProxyPrimitive> primitives)
    {
        ArgumentNullException.ThrowIfNull(primitives);
        var indices = new List<int>();
        foreach (var primitive in primitives) CollectLayerIndices(primitive, indices);
        return indices;
    }

    private static CadProxyPrimitive Rewrite(
        CadProxyPrimitive primitive,
        IReadOnlyList<int?> layerStates,
        ref int stateIndex)
    {
        if (primitive is CadProxyClipGroup group)
        {
            var children = group.Children
                .Select(child => Rewrite(child, layerStates, ref stateIndex))
                .ToArray();
            return group with { Children = children };
        }

        var layerIndex = layerStates[stateIndex++];
        return layerIndex is { } value
            ? primitive with { Traits = primitive.Traits with { LayerIndex = value } }
            : primitive;
    }

    private static int CountLeaves(CadProxyPrimitive primitive)
        => primitive is CadProxyClipGroup group
            ? group.Children.Sum(CountLeaves)
            : 1;

    private static void CollectLayerIndices(CadProxyPrimitive primitive, List<int> indices)
    {
        if (primitive is CadProxyClipGroup group)
        {
            foreach (var child in group.Children) CollectLayerIndices(child, indices);
            return;
        }

        if (primitive.Traits.LayerIndex is { } layerIndex) indices.Add(layerIndex);
    }

    private static bool CanEmitPrimitive(GraphicsType type)
        => type is GraphicsType.Polyline
            or GraphicsType.PolylineWithNormal
            or GraphicsType.LwPolyine
            or GraphicsType.Polygon
            or GraphicsType.Circle
            or GraphicsType.CirclePt3
            or GraphicsType.CircularArc
            or GraphicsType.CircularArc3Pt
            or GraphicsType.Text
            or GraphicsType.Text2
            or GraphicsType.UnicodeText
            or GraphicsType.UnicodeText2;
}
