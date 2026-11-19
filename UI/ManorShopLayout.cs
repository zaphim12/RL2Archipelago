using RL2Archipelago.Locations;
using System.Collections.Generic;
using UnityEngine;

namespace RL2Archipelago.UI;

/// <summary>
/// Hand-editable tuning data for the shop manor layout: which prefab slots host its four
/// positions, and where the row sits. See <see cref="Patches.ManorShopPatch"/> for how the
/// screen is actually assembled.
///
/// <para>The manor screen is prefab-driven: node positions are authored <c>RectTransform</c>s
/// and prerequisites are the authored <c>SkillTreeSlot.UnlockSlotList</c>. There is no data
/// path for adding a node, so the shop does not add any. It hides every node and then
/// repurposes four existing prefab slots as its positions, repositioning them into a row.</para>
/// </summary>
internal static class ManorShopLayout
{
    // ── Host slots ───────────────────────────────────────────────────────────
    //
    // Which prefab slots become the four shop positions, in on-screen order, left to right.
    //
    // The choice is close to arbitrary. A host is repositioned into the row, stripped of its
    // animator param and unlock list, and has its icon, title, description, cost and counter
    // all overridden, so nothing of its original identity survives. The only real requirement
    // is that the slot exists in the manor window prefab. Every SkillTreeType in 
    // GameConstants.ManorSlots satisfies that by construction. Therefore, hosting on an 
    // arbitrary slot is harmless.

    /// <summary>The slots hosting each shop position, in on-screen order. Chosen arbitrarily.</summary>
    public static readonly SkillTreeType[] Hosts =
    {
        SkillTreeType.Pirate_Class_Unlock,
        SkillTreeType.Armor_Up3,
        SkillTreeType.Dexterity_Add2,
        SkillTreeType.Samurai_Class_Unlock,
    };

    // ── Row geometry ─────────────────────────────────────────────────────────
    //
    // Hosts are reparented to the window's m_skillTreeIconsCanvasGroup and given centred
    // anchors, so these are offsets from the centre of the manor canvas (authored against
    // 1920x1080) and do not depend on wherever the prefab originally put each slot.

    /// <summary>Centre of the row, relative to the centre of the manor canvas. Pushed below
    /// the middle.</summary>
    public static readonly Vector2 RowCenter = new(0f, -210f);

    /// <summary>Horizontal gap between adjacent positions, in canvas units. A slot icon is
    /// roughly 100 units across, so this leaves a clear gap between them.</summary>
    public const float Spacing = 190f;

    /// <summary>Anchored position of a shop position, relative to the canvas centre. The row
    /// is centred on <see cref="RowCenter"/> regardless of how many positions there are.</summary>
    public static Vector2 AnchoredPositionFor(int position)
    {
        float span = (LocationRegistry.MANOR_SHOP_POSITIONS - 1) * Spacing;
        return RowCenter + new Vector2(position * Spacing - span * 0.5f, 0f);
    }

    // ── Lookups ──────────────────────────────────────────────────────────────
    //
    // Built from Hosts at type load, so these answer correctly from the first call rather
    // than only once a manor window has been opened.

    private static readonly Dictionary<SkillTreeType, int> s_positionOf = BuildPositions();

    private static Dictionary<SkillTreeType, int> BuildPositions()
    {
        var d = new Dictionary<SkillTreeType, int>(Hosts.Length);
        for (int i = 0; i < Hosts.Length; i++) d[Hosts[i]] = i;

        if (d.Count != LocationRegistry.MANOR_SHOP_POSITIONS)
            Plugin.Log.LogError(
                $"[AP] ManorShopLayout.Hosts lists {d.Count} distinct slots but the shop has " +
                $"{LocationRegistry.MANOR_SHOP_POSITIONS} positions; the manor will not build.");

        return d;
    }

    /// <summary>Whether this slot is hosting a shop position.</summary>
    public static bool IsHost(SkillTreeType type) => s_positionOf.ContainsKey(type);

    /// <summary>The shop position this slot hosts, or <c>null</c> if it hosts none.</summary>
    public static int? PositionOf(SkillTreeType type) =>
        s_positionOf.TryGetValue(type, out var position) ? position : null;

    /// <summary>False when <see cref="Hosts"/> does not line up with the configured number of
    /// shop positions, which is an authoring mistake in this file rather than a runtime state.</summary>
    public static bool IsValid => s_positionOf.Count == LocationRegistry.MANOR_SHOP_POSITIONS;
}
