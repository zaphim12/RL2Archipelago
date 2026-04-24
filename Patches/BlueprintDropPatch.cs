using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

/// <summary>
/// Intercepts equipment blueprint drops from chests and converts them into
/// Archipelago location checks.
///
/// Rather than tying each check to a specific blueprint (which would potentially let 
/// room-level gating prevent some locations from ever appearing), checks are allocated 
/// from a per-biome pool. They are given when a player would have rolled into a 
/// blueprint drop from a particular chest opening
///
/// When the AP location pool is exhausted (or the biome is unrecognised), the
/// postfix on CalculateSpecialItemDropObj returns null so that the chest coroutine's
/// existing null-check takes over and drops gold — exactly what vanilla does when
/// no blueprints are available.
/// </summary>
[HarmonyPatch]
internal static class BlueprintDropPatch
{
    // Runs after ChestObj resolves what special item to drop. If AP is active but
    // there is no available location for this biome, we return null so the chest
    // coroutine falls back to its gold-drop path instead of granting a blueprint.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChestObj), "CalculateSpecialItemDropObj")]
    private static void CalculateSpecialItemDropObj_Postfix(ref ISpecialItemDrop __result)
    {
        if (!APClient.IsConnected || APClient.RunState == null) return;
        if (__result is not IBlueprintDrop) return;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) { __result = null; return; }

        var biomeIndex = LocationRegistry.GetBiomeIndex(room.BiomeType);
        if (biomeIndex == null) { __result = null; return; }

        var locationId = LocationRegistry.NextBlueprintChestLocation(biomeIndex.Value, APClient.RunState.CheckedLocations);
        if (locationId == null) { __result = null; } // pool exhausted — null triggers gold fallback in coroutine
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemDropManager), nameof(ItemDropManager.DropSpecialItem))]
    private static bool DropSpecialItem_Prefix(ISpecialItemDrop specialItemDrop)
    {
        if (!APClient.IsConnected || APClient.RunState == null) return true;
        if (specialItemDrop is not IBlueprintDrop) return true;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) return true;

        var biomeIndex = LocationRegistry.GetBiomeIndex(room.BiomeType);
        if (biomeIndex == null) return true;

        var locationId = LocationRegistry.NextBlueprintChestLocation(biomeIndex.Value, APClient.RunState.CheckedLocations);
        if (locationId == null) return true;

        APClient.SendLocationCheck(locationId.Value);

        // OpenChestAnimCoroutine disables game input before calling DropSpecialItem,
        // then waits on WaitUntil(IsMapEnabled) for the SpecialItemDrop window to close.
        // Since we're skipping that window, we must re-enable input ourselves or the
        // coroutine hangs indefinitely.
        RewiredMapController.SetMapEnabled(GameInputMode.Game, enabled: true);

        return false; // suppress vanilla blueprint grant; AP server sends item back
    }
}
