using HarmonyLib;
using RL2Archipelago.Locations;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Intercepts rune drops from fairy chests and converts them into
/// Archipelago location checks.
///
/// Follows the same biome-pool strategy as <see cref="BlueprintDropPatch"/>.
/// Fairy chests always roll SpecialItemType.Rune natively, so no override of
/// GetSpecialItemTypeToDrop is needed; the configured fairy_chest_ap_chance is
/// applied directly inside CalculateSpecialItemDropObj_Postfix before the pool
/// check. A miss returns null, which the chest coroutine's existing null-check
/// for fairy chests converts to a red-aether drop instead.
/// </summary>
[HarmonyPatch]
internal static class RuneDropPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChestObj), "CalculateSpecialItemDropObj")]
    private static void CalculateSpecialItemDropObj_Postfix(ref ISpecialItemDrop __result, SpecialItemType specialItemType)
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return;

        bool triedRune = specialItemType == SpecialItemType.Rune;
        bool gotRune   = __result is IRuneDrop;
        if (!triedRune && !gotRune) return;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) { __result = null; return; }

        var biomeIndex = LocationRegistry.GetBiomeIndex(room.BiomeType);
        if (biomeIndex == null) { __result = null; return; }

        // Apply configured chance before checking the pool.
        if (Random.Range(0, 100) >= APClient.FairyChestApChance)
        { __result = null; return; } // chance miss - null triggers red-aether fallback

        var locationId = LocationRegistry.NextRuneChestLocation(biomeIndex.Value, APClient.RunState.CheckedLocations);
        if (locationId == null) { __result = null; return; } // pool exhausted - null triggers red-aether fallback

        if (!gotRune)
            __result = new RuneDrop(default);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemDropManager), nameof(ItemDropManager.DropSpecialItem))]
    private static bool DropSpecialItem_Prefix(ISpecialItemDrop specialItemDrop)
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return true;
        if (specialItemDrop is not IRuneDrop) return true;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) return true;

        var biomeIndex = LocationRegistry.GetBiomeIndex(room.BiomeType);
        if (biomeIndex == null) return true;

        var locationId = LocationRegistry.NextRuneChestLocation(biomeIndex.Value, APClient.RunState.CheckedLocations);
        if (locationId == null) return true;

        APClient.SendLocationCheck(locationId.Value);

        // OpenChestAnimCoroutine disables game input before calling DropSpecialItem,
        // then waits on WaitUntil(IsMapEnabled) for the SpecialItemDrop window to close.
        // Since we're skipping that window, we must re-enable input ourselves or the
        // coroutine hangs indefinitely.
        RewiredMapController.SetMapEnabled(GameInputMode.Game, enabled: true);

        return false; // suppress vanilla rune grant; AP server sends item back
    }
}
