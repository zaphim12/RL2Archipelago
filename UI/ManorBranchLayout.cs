using System.Collections.Generic;
using UnityEngine;

namespace RL2Archipelago.UI;

/// <summary>
/// Hand-editable tuning data for <see cref="ManorBranchOverlay"/>.
///
/// <para>Nothing here is required for the overlay to work. With every collection left empty
/// the overlay derives the whole tree from the game's own data and picks its own colors. Edit
/// this file only when the automatic result needs correcting.</para>
///
/// <para>The manor is a strict tree: every node has exactly one parent and children never merge
/// back together. The game's own connection data does not record which end of a link is the
/// parent, so the tree is oriented by searching outward from <see cref="RootNode"/>.</para>
/// </summary>
internal static class ManorBranchLayout
{
    // ── Appearance ───────────────────────────────────────────────────────────
    // Thickness is in canvas units (the manor canvas is authored against 1920x1080),
    // so ~8 reads as a chunky-but-not-obtrusive line at default resolution.

    /// <summary>Line width, in canvas units.</summary>
    public const float Thickness = 8f;

    /// <summary>Width of the soft edge along the outline, in canvas units. Coverage ramps from
    /// nothing at the silhouette to solid this far inside it, which is what stops diagonals from
    /// stair-stepping. Roughly one screen pixel at 1080p; raise it for a softer look.</summary>
    public const float EdgeFeather = 1.5f;

    /// <summary>Radius of the fillet applied where two lines meet, in canvas units. The outside of
    /// a bend gets its corner cut back by an arc tangent to both lines; the crease on the inside of
    /// a fork gets that same arc filling it in, which is what stops a branch point from looking
    /// pinched. Zero leaves every junction sharp.
    ///
    /// <para>Clamped to half <see cref="Thickness"/>, since past that a cut corner would eat into
    /// its neighbour. At exactly that value a bend becomes a perfect quarter-circle round join and
    /// a dead end ends in a semicircular cap.</para></summary>
    public const float CornerRounding = 4f;

    /// <summary>Distance over which a line changes from its parent's branch color into its own,
    /// measured from the parent node, in canvas units. The junction itself is therefore solidly
    /// the parent's color and each child line grows into its own, which is what makes a fork
    /// between two different colors read as a split rather than a seam. Zero switches colors
    /// abruptly at the node.</summary>
    public const float ColorBlendLength = 45f;

    /// <summary>Alpha used when either endpoint of a line is a node the game has not revealed
    /// yet. Unrevealed branches still show their shape, albeit partially transparent.</summary>
    public const float DimAlpha = 0.3f;

    /// <summary>Alpha used when both endpoints are visible on screen.</summary>
    public const float FullAlpha = 1f;

    /// <summary>Color for any line whose branch could not be resolved. Deliberately a drab grey
    /// so unclaimed regions stand out against the palette and are easy to spot and fix.</summary>
    public const string UnassignedColor = "#7A7A7A";

    // ── The on-screen prompt ─────────────────────────────────────────────────
    //
    // See ManorBranchPrompt. The glyph in front of the label is not configured here: it is
    // resolved from the live Spin-Kick binding by the game's own TextGlyphConverter.

    /// <summary>Text shown after the Spin-Kick glyph.</summary>
    public const string PromptLabel = "TOGGLE COLOR TREE VIEW";

    /// <summary>Where the prompt's bottom-right corner sits relative to the bottom-right corner of
    /// the manor canvas, in canvas units. X is negative (leftward, away from the right edge) and Y
    /// positive (upward, away from the bottom edge).
    ///
    /// <para>The prompt keeps the size the game authored it at, since the glyph and the label are
    /// positioned against that rect. The label is allowed to overflow, so a longer
    /// <see cref="PromptLabel"/> spreads out from wherever the vanilla one was centred rather than
    /// resizing anything.</para></summary>
    public static readonly Vector2 PromptOffset = new(-170f, 125f);

    // ── Tree root ────────────────────────────────────────────────────────────
    //
    // The manor's entrance node. This is Universal Health Stair
    //
    // It has to be stated rather than detected, because the game's UnlockSlotList is an
    // undirected adjacency list: plenty of slots also name the node above them, so there is no
    // reliable "this one has no parent" signal in the data. Everything is oriented by breadth-
    // first search outward from here.

    /// <summary>The node the whole tree hangs from.</summary>
    public const SkillTreeType RootNode = SkillTreeType.Traits_Give_Gold;

    // ── Branch color pins ────────────────────────────────────────────────────
    //
    // Colors are assigned automatically, so this is normally left EMPTY. The rule:
    //   - exactly ONE child carries the parent's color onward, so a branch keeps its identity
    //     along its whole length (the largest subtree is chosen, so the color follows the main
    //     line rather than veering down a short offshoot),
    //   - the other children each take a new color, distinct from their siblings and from the
    //     line coming in, so diverging paths can always be told apart,
    //   - LEAVES are the exception: a childless node always inherits the parent's color and does
    //     not use up the continuation slot, since a dead-end stub is not a new branch. A node
    //     with three children, two of them leaves, therefore has all three the same color.
    // A line takes the color of its CHILD endpoint. Colors come from AutoPalette and repeat
    // across the tree; if the palette is not big enough for global uniqueness, then local 
    // distinctness is still guaranteed.
    //
    // Add an entry here to pin one node's branch to a specific color. Its descendants inherit it
    // under the normal rules, so pinning a fork's child recolors that whole sub-branch.
    //
    // Example:
    //     (SkillTreeType.Health_Up,  "#E4572E"),   // force the vitality line red

    public static readonly (SkillTreeType Node, string Hex)[] BranchRoots =
    {
        // (empty = fully automatic)
    };

    // ── Continuation overrides ───────────────────────────────────────────────
    //
    // Which child carries a parent's color onward. By default the largest subtree wins, which
    // is usually the visually obvious "main line" but not always. An entry here names the child
    // explicitly: { parent, child that should inherit the parent's color }.
    //
    // The named child must be one that has children of its own; naming a leaf does nothing,
    // because leaves already inherit the parent's color without consuming the slot.

    public static readonly Dictionary<SkillTreeType, SkillTreeType> ContinuationOverrides = new()
    {
        { SkillTreeType.Dexterity_Add1,   SkillTreeType.DualBlades_Class_Unlock }, // was Attack_Up2
        { SkillTreeType.Enchantress,      SkillTreeType.Unlock_Totem },            // was Rune_Equip_Up
        { SkillTreeType.Axe_Class_Unlock, SkillTreeType.Health_Up2 },              // was Boss_Health_Restore
    };

    /// <summary>Colors handed out automatically. Cycled in order; add entries if forks in the
    /// manor run out of distinct colors too often. Chosen to stay distinguishable against the
    /// castle backdrop.</summary>
    public static readonly string[] AutoPalette =
    {
        "#e2423d", // red
        "#07911e", // green
        "#1142ca", // blue
        "#E8C547", // yellow
        "#b031e2", // purple
        "#2FBFC4", // cyan
        "#6d3704", // brown
        "#FFFFFF", // white
        "#fd1da7", // magenta
        "#eb8221", // orange
        "#8FBF3F", // lime
        "#eb85a7", // light-pink
    };

    // ── Geometry corrections ─────────────────────────────────────────────────
    //
    // Edges come from each slot's UnlockSlotList (the game's own "this node reveals these nodes"
    // data), so these should rarely be needed. Order within a pair does not matter for
    // suppression; for ExtraEdges the first element is treated as the parent.

    /// <summary>Extra lines to draw that the game's unlock data does not contain.</summary>
    public static readonly (SkillTreeType From, SkillTreeType To)[] ExtraEdges =
    {
        // (SkillTreeType.Health_Up, SkillTreeType.Death_Dodge),
    };

    /// <summary>Lines to omit even though the game's unlock data contains them.</summary>
    public static readonly (SkillTreeType From, SkillTreeType To)[] SuppressedEdges =
    {
        // (SkillTreeType.Health_Up, SkillTreeType.Death_Dodge),
    };

    /// <summary>Shifts where lines attach to a node, in canvas units, without moving the node
    /// itself. Use when a line's endpoint looks off-centre against the node art.</summary>
    public static readonly Dictionary<SkillTreeType, Vector2> EndpointNudge = new()
    {
        // { SkillTreeType.Health_Up, new Vector2(0f, -12f) },
    };

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Parses a "#RRGGBB" string, falling back to magenta so a typo is loud rather than
    /// silently invisible.</summary>
    public static Color ParseColor(string hex)
    {
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var color)) return color;
        Plugin.Log.LogWarning($"[ManorBranchOverlay] Could not parse color '{hex}'; using magenta.");
        return Color.magenta;
    }

    /// <summary>Endpoint offset for a node, or zero if none is configured.</summary>
    public static Vector2 NudgeFor(SkillTreeType type) =>
        EndpointNudge.TryGetValue(type, out var offset) ? offset : Vector2.zero;
}
