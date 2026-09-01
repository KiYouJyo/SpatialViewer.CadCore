using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace SpatialViewer.Core;

/// <summary>Structural diagnostics for the lazily-built scene spatial index.</summary>
public readonly record struct SceneSpatialIndexStatistics(int ItemCount, int IndexedItemCount, int FallbackItemCount, int NodeCount);

/// <summary>Spatial queries over a flattened scene. Results preserve the original scene draw order.</summary>
public static class SceneSpatialQueries
{
    private static readonly ConditionalWeakTable<Scene2D, SceneSpatialIndex> Indices = new();

    public static IEnumerable<SceneItem> QueryItems(this Scene2D scene, BoundingBox2D worldBounds, bool visibleOnly = true)
    {
        ArgumentNullException.ThrowIfNull(scene);
        foreach (var index in QueryItemIndices(scene, worldBounds))
        {
            var item = scene.Items[index];
            if (!visibleOnly || item.Layer.IsVisible) yield return item;
        }
    }

    public static SceneSpatialIndexStatistics GetSpatialIndexStatistics(this Scene2D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return GetIndex(scene).Statistics;
    }

    internal static IReadOnlyList<int> QueryItemIndices(Scene2D scene, BoundingBox2D worldBounds) => GetIndex(scene).Query(worldBounds);

    private static SceneSpatialIndex GetIndex(Scene2D scene) => Indices.GetValue(scene, static value => SceneSpatialIndex.Build(value.Items));
}

internal sealed class SceneSpatialIndex
{
    private const int LeafCapacity = 16;
    private readonly BoundingBox2D[] _bounds;
    private readonly int[] _orderedIndices;
    private readonly int[] _fallbackIndices;
    private readonly Node[] _nodes;

    private SceneSpatialIndex(BoundingBox2D[] bounds, int[] orderedIndices, int[] fallbackIndices, Node[] nodes)
    {
        _bounds = bounds;
        _orderedIndices = orderedIndices;
        _fallbackIndices = fallbackIndices;
        _nodes = nodes;
        Statistics = new(bounds.Length, orderedIndices.Length, fallbackIndices.Length, nodes.Length);
    }

    public SceneSpatialIndexStatistics Statistics { get; }

    public static SceneSpatialIndex Build(IReadOnlyList<SceneItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var bounds = new BoundingBox2D[items.Count];
        var indexed = new List<int>(items.Count);
        var fallback = new List<int>();
        for (var index = 0; index < items.Count; index++)
        {
            bounds[index] = items[index].Bounds;
            if (IsIndexable(bounds[index])) indexed.Add(index);
            else fallback.Add(index);
        }

        var ordered = indexed.ToArray();
        var nodes = ordered.Length == 0 ? Array.Empty<Node>() : new Builder(bounds, ordered, LeafCapacity).Build();
        return new(bounds, ordered, fallback.ToArray(), nodes);
    }

    public IReadOnlyList<int> Query(BoundingBox2D query)
    {
        if (query.IsEmpty) return Array.Empty<int>();
        if (!IsFinite(query)) return Enumerable.Range(0, _bounds.Length).ToArray();

        var candidates = new List<int>();
        candidates.AddRange(_fallbackIndices);
        if (_nodes.Length > 0)
        {
            var pending = new Stack<int>();
            pending.Push(0);
            while (pending.Count > 0)
            {
                var node = _nodes[pending.Pop()];
                if (!node.Bounds.Intersects(query)) continue;
                if (node.IsLeaf)
                {
                    var end = node.Start + node.Count;
                    for (var offset = node.Start; offset < end; offset++)
                    {
                        var itemIndex = _orderedIndices[offset];
                        if (_bounds[itemIndex].Intersects(query)) candidates.Add(itemIndex);
                    }
                }
                else
                {
                    pending.Push(node.Left);
                    pending.Push(node.Right);
                }
            }
        }

        candidates.Sort();
        return candidates;
    }

    private static bool IsIndexable(BoundingBox2D bounds) => !bounds.IsEmpty && IsFinite(bounds);

    private static bool IsFinite(BoundingBox2D bounds) =>
        double.IsFinite(bounds.MinX) && double.IsFinite(bounds.MinY) && double.IsFinite(bounds.MaxX) && double.IsFinite(bounds.MaxY);

    private readonly record struct Node(BoundingBox2D Bounds, int Left, int Right, int Start, int Count)
    {
        public bool IsLeaf => Count > 0;
    }

    private sealed class Builder
    {
        private readonly BoundingBox2D[] _bounds;
        private readonly int[] _indices;
        private readonly int _leafCapacity;
        private readonly List<Node> _nodes = new();

        public Builder(BoundingBox2D[] bounds, int[] indices, int leafCapacity)
        {
            _bounds = bounds;
            _indices = indices;
            _leafCapacity = leafCapacity;
        }

        public Node[] Build()
        {
            _ = BuildNode(0, _indices.Length);
            return _nodes.ToArray();
        }

        private int BuildNode(int start, int count)
        {
            var nodeBounds = BoundingBox2D.Empty;
            for (var offset = start; offset < start + count; offset++) nodeBounds = nodeBounds.Union(_bounds[_indices[offset]]);

            var nodeIndex = _nodes.Count;
            _nodes.Add(default);
            if (count <= _leafCapacity)
            {
                _nodes[nodeIndex] = new(nodeBounds, -1, -1, start, count);
                return nodeIndex;
            }

            var splitOnX = nodeBounds.Width >= nodeBounds.Height;
            Array.Sort(_indices, start, count, Comparer<int>.Create((left, right) => CompareCenters(left, right, splitOnX)));
            var leftCount = count / 2;
            var left = BuildNode(start, leftCount);
            var right = BuildNode(start + leftCount, count - leftCount);
            _nodes[nodeIndex] = new(nodeBounds, left, right, 0, 0);
            return nodeIndex;
        }

        private int CompareCenters(int left, int right, bool xAxis)
        {
            var leftCenter = xAxis ? (_bounds[left].MinX + _bounds[left].MaxX) * .5 : (_bounds[left].MinY + _bounds[left].MaxY) * .5;
            var rightCenter = xAxis ? (_bounds[right].MinX + _bounds[right].MaxX) * .5 : (_bounds[right].MinY + _bounds[right].MaxY) * .5;
            var comparison = leftCenter.CompareTo(rightCenter);
            return comparison != 0 ? comparison : left.CompareTo(right);
        }
    }
}
