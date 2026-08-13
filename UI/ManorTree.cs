using RL2Archipelago.Items;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RL2Archipelago.UI;

/// <summary>
/// The manor upgrade tree, derived at runtime from the game's own data, plus the branch color
/// assignment used by <see cref="ManorBranchOverlay"/>.
///
/// <para>Connections come from <see cref="SkillTreeSlot.UnlockSlotList"/> - the authored list
/// that vanilla <c>UnlockConnectedSkillSlots()</c> consumes. Despite the name it behaves as an
/// <em>undirected adjacency list</em>, not a parent → child list: a fair number of slots also
/// name the node above them, so trusting its direction gets the tree upside down in places and
/// makes some nodes look like they have two parents. This class therefore ignores direction
/// entirely, builds an undirected graph, and derives the parent relation by breadth-first search
/// from <see cref="ManorBranchLayout.RootNode"/>. Every node then has exactly one parent by
/// construction, so conflicting parents are impossible.</para>
/// </summary>
internal sealed class ManorTree
{
    /// <summary>Parent → child, oriented by the BFS, in discovery order.</summary>
    public readonly List<(SkillTreeSlot From, SkillTreeSlot To)> Edges = new();

    /// <summary>Child → parent. Absent for roots and for isolated nodes.</summary>
    public readonly Dictionary<SkillTreeType, SkillTreeType> Parent = new();

    /// <summary>One per connected component, in the order they were rooted.</summary>
    public readonly List<SkillTreeType> Roots = new();

    private readonly Dictionary<SkillTreeType, SkillTreeSlot> _byType = new();
    private readonly Dictionary<SkillTreeType, List<SkillTreeType>> _adjacency = new();
    private readonly Dictionary<SkillTreeType, List<SkillTreeType>> _children = new();
    private readonly Dictionary<SkillTreeType, Color> _color = new();
    private readonly Dictionary<SkillTreeType, int> _subtreeSize = new();
    private readonly HashSet<long> _seen = new();
    private readonly HashSet<long> _cycles = new();

    // ── Construction ─────────────────────────────────────────────────────────

    public static ManorTree Build(SkillTreeSlot[] slots)
    {
        var tree = new ManorTree();

        foreach (var slot in slots)
        {
            if (slot == null || !slot.HasData) continue;
            // Vanilla Initialize() throws on duplicate SkillTreeTypes, so this should never
            // collide. Guard anyway so a bad prefab degrades instead of crashing the overlay.
            if (!tree._byType.ContainsKey(slot.SkillTreeType)) tree._byType[slot.SkillTreeType] = slot;
        }

        tree.BuildAdjacency(slots);
        tree.OrientFromRoots();
        tree.AssignColors();
        return tree;
    }

    private void BuildAdjacency(SkillTreeSlot[] slots)
    {
        foreach (var slot in slots)
        {
            if (slot == null || !slot.HasData) continue;

            var connected = slot.UnlockSlotList;
            if (connected == null) continue;
            var disabled = slot.UnlockSlotDisableList;

            for (int i = 0; i < connected.Count; i++)
            {
                var other = connected[i];
                if (other == null || other == slot || !other.HasData) continue;
                // Entries flagged in the parallel disable list are authored-but-inactive links.
                if (disabled != null && i < disabled.Count && disabled[i]) continue;

                AddUndirected(slot.SkillTreeType, other.SkillTreeType);
            }
        }

        foreach (var (from, to) in ManorBranchLayout.ExtraEdges)
        {
            if (_byType.ContainsKey(from) && _byType.ContainsKey(to)) AddUndirected(from, to);
            else Plugin.Log.LogWarning($"[ManorBranchOverlay] ExtraEdges names a node the manor does not have: {from} -> {to}");
        }
    }

    private void AddUndirected(SkillTreeType a, SkillTreeType b)
    {
        if (IsSuppressed(a, b)) return;
        // Reciprocal entries (A lists B and B lists A) collapse to a single connection here.
        if (!_seen.Add(PairKey(a, b))) return;

        Neighbours(a).Add(b);
        Neighbours(b).Add(a);
    }

    private List<SkillTreeType> Neighbours(SkillTreeType type)
    {
        if (!_adjacency.TryGetValue(type, out var list)) _adjacency[type] = list = new List<SkillTreeType>();
        return list;
    }

    private List<SkillTreeType> ChildrenOf(SkillTreeType type)
    {
        if (!_children.TryGetValue(type, out var list)) _children[type] = list = new List<SkillTreeType>();
        return list;
    }

    private static bool IsSuppressed(SkillTreeType a, SkillTreeType b)
    {
        foreach (var (from, to) in ManorBranchLayout.SuppressedEdges)
            if ((from == a && to == b) || (from == b && to == a)) return true;
        return false;
    }

    /// <summary>Order-independent key, so A–B and B–A are the same connection.</summary>
    private static long PairKey(SkillTreeType a, SkillTreeType b)
    {
        int x = (int)a, y = (int)b;
        if (x > y) (x, y) = (y, x);
        return ((long)x << 32) | (uint)y;
    }

    // ── Orientation ──────────────────────────────────────────────────────────

    /// <summary>BFSes outward from the root, which assigns every node exactly one parent.
    /// Direction from the authored data is deliberately not consulted.</summary>
    private void OrientFromRoots()
    {
        var visited = new HashSet<SkillTreeType>();

        while (TryPickRoot(visited, out var root))
        {
            Roots.Add(root);
            visited.Add(root);

            var queue = new Queue<SkillTreeType>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!_adjacency.TryGetValue(node, out var neighbours)) continue;

                // Sorted so the tree, and therefore the palette assignment, is identical
                // between runs regardless of the order Unity returned the slots in.
                foreach (var next in neighbours.OrderBy(t => (int)t))
                {
                    if (!visited.Add(next))
                    {
                        // Already reached. Normally this is just the back-edge to our own
                        // parent; anything else closes a loop, which a tree cannot have.
                        bool isOwnParent = Parent.TryGetValue(node, out var p) && p == next;
                        if (!isOwnParent) _cycles.Add(PairKey(node, next));
                        continue;
                    }

                    Parent[next] = node;
                    ChildrenOf(node).Add(next);
                    Edges.Add((_byType[node], _byType[next]));
                    queue.Enqueue(next);
                }
            }
        }

        if (_cycles.Count > 0)
        {
            Plugin.Log.LogWarning(
                $"[ManorBranchOverlay] The manor graph contains {_cycles.Count} connection(s) that close a loop, " +
                "so it is not a pure tree. Lines still draw correctly; branch colors past a loop may look " +
                "arbitrary. Drop the unwanted link via ManorBranchLayout.SuppressedEdges.");
        }
    }

    /// <summary>The configured root for the main component; for any further (disconnected)
    /// component, the lowest remaining enum value so the result stays deterministic.</summary>
    private bool TryPickRoot(HashSet<SkillTreeType> visited, out SkillTreeType root)
    {
        if (Roots.Count == 0)
        {
            root = ManorBranchLayout.RootNode;
            if (_adjacency.ContainsKey(root)) return true;

            Plugin.Log.LogWarning(
                $"[ManorBranchOverlay] RootNode is set to {root}, which has no connections in the manor. " +
                "Branch colors will be rooted arbitrarily; fix ManorBranchLayout.RootNode.");
        }

        root = SkillTreeType.None;
        bool found = false;

        foreach (var type in _adjacency.Keys)
        {
            if (visited.Contains(type)) continue;
            if (found && (int)type >= (int)root) continue;
            root = type;
            found = true;
        }

        return found;
    }

    // ── Branch colors ────────────────────────────────────────────────────────

    /// <summary>Walks each component from its root handing out colors, so that a branch keeps
    /// its identity along its whole length and only genuinely new paths get new colors.
    ///
    /// <para>At every node, exactly one child carries the parent's color onward (i.e. the branch
    /// continues rather than ending) and the remaining children each take a new color distinct
    /// from their siblings and from the incoming line. Leaves are the exception: a childless node
    /// always inherits the parent's color and does not use up the one continuation slot, since a
    /// dead-end stub is not a new branch. So a node with three children, two of them leaves, has
    /// all three children the same color as itself.</para></summary>
    private void AssignColors()
    {
        var pins = new Dictionary<SkillTreeType, Color>();
        foreach (var (node, hex) in ManorBranchLayout.BranchRoots)
        {
            if (_byType.ContainsKey(node)) pins[node] = ManorBranchLayout.ParseColor(hex);
            else Plugin.Log.LogWarning($"[ManorBranchOverlay] BranchRoots names a node the manor does not have: {node}");
        }

        var palette = ManorBranchLayout.AutoPalette.Select(ManorBranchLayout.ParseColor).ToArray();
        if (palette.Length == 0)
        {
            Plugin.Log.LogWarning("[ManorBranchOverlay] AutoPalette is empty; every line will be grey.");
            return;
        }

        int cursor = 0;

        foreach (var root in Roots)
        {
            _color[root] = pins.TryGetValue(root, out var pinnedRoot) ? pinnedRoot : NextColor(palette, ref cursor);

            var stack = new Stack<SkillTreeType>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                var children = ChildrenOf(node);
                if (children.Count == 0) continue;

                var nodeColor = _color[node];

                // A leaf is a dead-end stub, not a new branch, so it always inherits and is not
                // counted when deciding which child continues the branch.
                foreach (var leaf in children.Where(c => ChildrenOf(c).Count == 0))
                    _color[leaf] = pins.TryGetValue(leaf, out var pinnedLeaf) ? pinnedLeaf : nodeColor;

                var branching = children.Where(c => ChildrenOf(c).Count > 0).ToList();
                if (branching.Count == 0) continue;

                var continuation = PickContinuation(node, children, branching);

                // Everything else must look new: avoid the incoming color, and the one before it
                // too, so a fresh branch never reads as the line it just split away from.
                var taken = new List<Color> { nodeColor };
                if (Parent.TryGetValue(node, out var grandparent) && _color.TryGetValue(grandparent, out var grandColor))
                    taken.Add(grandColor);

                foreach (var child in branching)
                {
                    Color color;
                    if (pins.TryGetValue(child, out var pinnedChild)) color = pinnedChild;
                    else if (child == continuation) color = nodeColor;
                    else
                    {
                        color = NextDistinctColor(palette, ref cursor, taken);
                        taken.Add(color);
                    }

                    _color[child] = color;
                    stack.Push(child);
                }
            }
        }
    }

    /// <summary>Which child carries <paramref name="node"/>'s color onward. Honours an explicit
    /// <see cref="ManorBranchLayout.ContinuationOverrides"/> entry; otherwise picks the largest
    /// subtree, so the branch follows its main line rather than veering down a short offshoot.</summary>
    private SkillTreeType PickContinuation(SkillTreeType node, List<SkillTreeType> children, List<SkillTreeType> branching)
    {
        if (ManorBranchLayout.ContinuationOverrides.TryGetValue(node, out var preferred))
        {
            if (branching.Contains(preferred)) return preferred;

            // Distinguish the two ways an override can be a no-op, since they need different fixes.
            if (children.Contains(preferred))
                Plugin.Log.LogWarning($"[ManorBranchOverlay] ContinuationOverrides sends {node}'s color to {preferred}, but that is a leaf - leaves already inherit it. Entry ignored.");
            else
                Plugin.Log.LogWarning($"[ManorBranchOverlay] ContinuationOverrides sends {node}'s color to {preferred}, which is not one of its children. Entry ignored.");
        }

        return branching.OrderByDescending(SubtreeSize).ThenBy(t => (int)t).First();
    }

    /// <summary>Node count of the subtree rooted at <paramref name="type"/>, used to decide which
    /// child continues a branch. Safe to recurse: the BFS orientation guarantees no cycles.</summary>
    private int SubtreeSize(SkillTreeType type)
    {
        if (_subtreeSize.TryGetValue(type, out var cached)) return cached;

        int size = 1;
        foreach (var child in ChildrenOf(type)) size += SubtreeSize(child);

        _subtreeSize[type] = size;
        return size;
    }

    private static Color NextColor(Color[] palette, ref int cursor)
    {
        var color = palette[cursor % palette.Length];
        cursor = (cursor + 1) % palette.Length;
        return color;
    }

    /// <summary>Next palette entry that is not already in <paramref name="avoid"/>, so siblings
    /// at a fork never collide. Falls back to the next entry if the palette is too small.</summary>
    private static Color NextDistinctColor(Color[] palette, ref int cursor, List<Color> avoid)
    {
        for (int i = 0; i < palette.Length; i++)
        {
            var candidate = palette[(cursor + i) % palette.Length];
            if (avoid.Contains(candidate)) continue;

            cursor = (cursor + i + 1) % palette.Length;
            return candidate;
        }
        return NextColor(palette, ref cursor);
    }

    /// <summary>The branch color for a node, or <see cref="ManorBranchLayout.UnassignedColor"/>
    /// for a node with no connections at all. Lines take the color of their child endpoint.</summary>
    public Color ColorFor(SkillTreeType type) =>
        _color.TryGetValue(type, out var color) ? color : ManorBranchLayout.ParseColor(ManorBranchLayout.UnassignedColor);

    // ── Debug dump ───────────────────────────────────────────────────────────

    /// <summary>Logs the derived tree as an indented listing, with each node's position in
    /// <paramref name="space"/> and its resolved branch color. This is what makes
    /// <see cref="ManorBranchLayout"/> tunable without guesswork.</summary>
    public void Dump(RectTransform space)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[ManorBranchOverlay] Derived manor tree: {_byType.Count} nodes, {Edges.Count} lines, {Roots.Count} root(s).");

        var visited = new HashSet<SkillTreeType>();
        foreach (var root in Roots)
        {
            sb.AppendLine($"  ROOT {root}");
            AppendNode(sb, space, root, 1, visited);
        }

        foreach (var isolated in _byType.Keys.Where(t => !visited.Contains(t)).OrderBy(t => (int)t))
            sb.AppendLine($"  [no connections] {isolated}");

        if (_cycles.Count > 0) sb.AppendLine($"  LOOPS: {_cycles.Count} connection(s) close a cycle.");

        Plugin.Log.LogInfo(sb.ToString());
    }

    private void AppendNode(StringBuilder sb, RectTransform space, SkillTreeType type, int depth, HashSet<SkillTreeType> visited)
    {
        if (!visited.Add(type)) return;

        Vector2 pos = space.InverseTransformPoint(_byType[type].transform.position);
        string color = "#" + ColorUtility.ToHtmlStringRGB(ColorFor(type));

        sb.Append(' ', 2 + depth * 2)
          .AppendLine($"{type}  ({pos.x:F0}, {pos.y:F0})  {color}{ApLocationName(type)}");

        foreach (var child in ChildrenOf(type).OrderBy(t => (int)t))
            AppendNode(sb, space, child, depth + 1, visited);
    }

    /// <summary>The Archipelago location name for a slot, so a hint like "Manor - Mess Hall" can
    /// be found directly in the dump. Empty for slots this randomizer does not track.</summary>
    private static string ApLocationName(SkillTreeType type)
    {
        foreach (var slot in GameConstants.ManorSlots)
            if (slot.SkillTree == type) return $"  \"{slot.LocationName}\"";
        return string.Empty;
    }
}
