using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

/// <summary>
/// Intercepts rune drops from fairy chests and converts them into
/// Archipelago location checks.
///
/// Follows the same biome-pool strategy as <see cref="BlueprintDropPatch"/>:
/// checks are allocated from a per-biome pool rather than tied to specific
/// rune types. When the pool is exhausted the postfix returns null so the
/// chest coroutine's existing null-check for fairy chests falls back to
/// dropping red aether instead.
/// </summary>
[HarmonyPatch]
internal static class RuneDropPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChestObj), "CalculateSpecialItemDropObj")]
    private static void CalculateSpecialItemDropObj_Postfix(ref ISpecialItemDrop __result)
    {
        if (!APClient.IsConnected || APClient.RunState == null) return;
        if (__result is not IRuneDrop) return;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) { __result = null; return; }

        var biomeIndex = LocationRegistry.GetBiomeIndex(room.BiomeType);
        if (biomeIndex == null) { __result = null; return; }

        var locationId = LocationRegistry.NextRuneChestLocation(biomeIndex.Value, APClient.RunState.CheckedLocations);
        if (locationId == null) { __result = null; } // pool exhausted — null triggers Rune Ore fallback
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemDropManager), nameof(ItemDropManager.DropSpecialItem))]
    private static bool DropSpecialItem_Prefix(ISpecialItemDrop specialItemDrop)
    {
        if (!APClient.IsConnected || APClient.RunState == null) return true;
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
