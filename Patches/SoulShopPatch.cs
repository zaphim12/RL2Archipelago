using HarmonyLib;
using System.Collections.Generic;

namespace RL2Archipelago.Patches;

/// <summary>
/// Reduces the Soul Shop to the handful of entries that still make sense in an Archipelago run.
///
/// <para>Almost everything the Soul Shop sells is permanent meta-progression that Archipelago
/// either already hands out as items (class unlocks, stat boosts) or that would let a player buy
/// their way past the randomizer. Souls are also earned largely from scar challenges, which the
/// randomizer does not touch (at least not yet). We remove all of those entries.
/// What survives is the four entries that are pure convenience and grant no power: the Music Box,
/// the two ore/aether converters, and the soul stone exchange.</para>
///
/// <para>The one conditional entry is the fabled weapons, and only
/// <see cref="FabledWeaponMode.SoulShop"/> keeps them listed.</para>
///
/// <para>Hiding is done exactly the way the base game hides a disabled entry: deactivate the
/// GameObject in <c>UpdateActive</c>. <c>BaseOmniUIWindowController.UpdateActiveArrays</c> rebuilds
/// its entry array with <c>GetComponentsInChildren(includeInactive: false)</c> and re-indexes, so a
/// deactivated row drops out of navigation cleanly rather than leaving a gap.</para>
/// </summary>
[HarmonyPatch]
internal static class SoulShopPatch
{
    /// <summary>
    /// Entries that remain available in every AP run.
    /// </summary>
    private static readonly HashSet<SoulShopType> s_alwaysAllowed =
    [
        SoulShopType.UnlockJukebox,  // Music Box
        SoulShopType.OreAetherSwap,  // Ore Converter
        SoulShopType.AetherOreSwap,  // Aether Converter
        SoulShopType.SoulSwap,       // Strange Transaction
    ];

    /// <summary>
    /// The 15 fabled weapon entries, taken from the game's own list so it cannot drift from the
    /// set <c>LineageWindowController.IsFabledWeaponUnlocked</c> actually consults.
    /// </summary>
    private static readonly HashSet<SoulShopType> s_fabledWeapons =
        [.. SpecialMode_EV.SOULSHOP_FABLED_WEAPON_ARRAY];

    /// <summary>
    /// Whether this entry is still on offer. Fabled weapons are allowed only when they are not
    /// being randomized, which is the whole of the conditional behaviour.
    /// </summary>
    private static bool IsAllowed(SoulShopType type) =>
        s_alwaysAllowed.Contains(type)
        || (APClient.FabledWeapons == FabledWeaponMode.SoulShop && s_fabledWeapons.Contains(type));

    /// <summary>
    /// Hides every entry outside the allow-list. This is the patch that does the real work; the
    /// two below only matter for an entry that somehow renders anyway.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoulShopOmniUIEntry), nameof(SoulShopOmniUIEntry.UpdateActive))]
    private static void UpdateActive_Postfix(SoulShopOmniUIEntry __instance)
    {
        if (!APClient.IsSessionActive) return;
        if (IsAllowed(__instance.SoulShopType)) return;

        if (__instance.gameObject.activeSelf)
            __instance.gameObject.SetActive(value: false);
    }

    /// <summary>
    /// Ungates the surviving entries.
    ///
    /// <para>Vanilla gates each entry behind <c>GetTotalSoulShopObjOwnedLevel() >= UnlockLevel</c>,
    /// i.e. "buy N other things first". With the rest of the shop hidden the player can no longer
    /// accumulate those owned levels, so without this the Music Box would be permanently stuck
    /// behind a counter that can never advance. Only entries that are already allowed are touched,
    /// so this never reveals anything the patch above hides.</para>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoulShopOmniUIEntry), nameof(SoulShopOmniUIEntry.IsUnlocked), MethodType.Getter)]
    private static void IsUnlocked_Postfix(SoulShopOmniUIEntry __instance, ref bool __result)
    {
        if (!APClient.IsSessionActive) return;
        if (!IsAllowed(__instance.SoulShopType)) return;

        __result = true;
    }

    /// <summary>
    /// Likely redundant: <c>IsEntryActive</c> is what <c>BaseOmniUIEntry.UpdateState</c> consults to
    /// deactivate an entry's buy buttons, so forcing it false guarantees a hidden entry cannot be
    /// purchased even if something reactivates its row.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoulShopOmniUIEntry), nameof(SoulShopOmniUIEntry.IsEntryActive), MethodType.Getter)]
    private static void IsEntryActive_Postfix(SoulShopOmniUIEntry __instance, ref bool __result)
    {
        if (!APClient.IsSessionActive) return;
        if (IsAllowed(__instance.SoulShopType)) return;

        __result = false;
    }

    // ── Fabled weapons must not cost souls ───────────────────────────────────
    //
    // Souls are not a stored balance. Souls_EV.GetTotalSoulsCollected re-derives what has been
    // spent, every time it is called, by walking each owned SoulShopObj and re-adding its cost -
    // ModeSaveData.SoulSpent is written on purchase but never read back. So an AP-granted fabled
    // weapon bills the player simply by being owned, and there is no "moment of deduction" to
    // intercept: the charge reappears on every read for as long as the entry is owned.
    //
    // Vanilla has the same problem when a special mode force-unlocks the fabled weapons, and
    // handles it inside that loop, skipping any entry in SOULSHOP_FABLED_WEAPON_ARRAY. We cannot
    // join that exemption (it is gated on SpecialModeType, which is None in a normal run), so the
    // pair below reproduces it: zero the variants' owned levels for the duration of the call, and
    // the loop's own `CurrentOwnedLevel > 0` test skips them.
    //
    // Blanking the state around a vanilla call rather than recomputing its result is the same
    // approach ManorUpgradePatch uses for skill levels, and for the same reason: the cost curve
    // lives in ScriptableObject data (BaseCost, ScalingCost, MaxLevelScalingCap, MaxSoulCostCap)
    // that is not ours to reimplement. The game does its own arithmetic; it just does it over a
    // set that excludes the items the multiworld handed out.
    //
    // Only active under FabledWeaponMode.Randomized, where the shop refuses to sell them,
    // so every owned variant in such a seed is one AP granted, and none can have been paid for.

    private static readonly Dictionary<SoulShopType, int> s_suppressedOwnedLevels = [];

    /// <summary>Guards against the restore running without a matching blank, and against reentry.</summary>
    private static bool s_suppressing;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Souls_EV), nameof(Souls_EV.GetTotalSoulsCollected))]
    private static void GetTotalSoulsCollected_Prefix(bool deductSpentSouls)
    {
        // deductSpentSouls false never runs the spend loop, so there is nothing to suppress.
        if (!deductSpentSouls || s_suppressing) return;
        if (!APClient.IsSessionActive || !APClient.ProgressiveClassItems) return;

        var table = SaveManager.ModeSaveData?.SoulShopTable;
        if (table == null) return;

        s_suppressing = true;
        s_suppressedOwnedLevels.Clear();

        foreach (var variant in s_fabledWeapons)
        {
            if (!table.TryGetValue(variant, out var entry) || entry == null) continue;
            if (entry.CurrentOwnedLevel <= 0) continue;

            s_suppressedOwnedLevels[variant] = entry.CurrentOwnedLevel;
            entry.SetOwnedLevel(0, additive: false, clampToMaxLevel: false);
        }
    }

    /// <summary>
    /// A finalizer, not a postfix: if the vanilla body throws, a postfix would not run and every
    /// fabled weapon would be left permanently unowned - which also silently revokes it, since
    /// <c>SetEquippedLevel</c> clamps to the owned level.
    /// </summary>
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Souls_EV), nameof(Souls_EV.GetTotalSoulsCollected))]
    private static void GetTotalSoulsCollected_Finalizer()
    {
        if (!s_suppressing) return;

        var table = SaveManager.ModeSaveData?.SoulShopTable;
        if (table != null)
        {
            foreach (var kvp in s_suppressedOwnedLevels)
            {
                if (table.TryGetValue(kvp.Key, out var entry) && entry != null)
                    entry.SetOwnedLevel(kvp.Value, additive: false, clampToMaxLevel: false);
            }
        }

        s_suppressedOwnedLevels.Clear();
        s_suppressing = false;
    }
}
