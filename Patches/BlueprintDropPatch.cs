using HarmonyLib;
using RL2Archipelago.Locations;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Intercepts blueprint drops from bronze and silver chests and converts them
/// into Archipelago location checks.
///
/// Two-stage interception:
///   1. GetSpecialItemTypeToDrop_Postfix - overrides the drop type for bronze/silver
///      chests based on the configured AP chance. A hit forces SpecialItemType.Blueprint
///      so the game enters the blueprint code path; a miss forces SpecialItemType.Gold
///      so the game drops coins normally without any AP involvement.
///
///   2. CalculateSpecialItemDropObj_Postfix - runs after the game resolves what
///      blueprint object to drop. Converts the result into an AP location check
///      from a per-biome pool, or returns null (triggering gold fallback) when the
///      pool is exhausted. Injects a placeholder BlueprintDrop when the game's own
///      level/rarity filter would have returned null despite the probability roll.
///
///   3. DropSpecialItem_Prefix - suppresses the vanilla blueprint-grant window and
///      sends the AP check to the server, then re-enables game input so the chest
///      coroutine's WaitUntil(IsMapEnabled) resolves.
/// </summary>
[HarmonyPatch]
internal static class BlueprintDropPatch
{
    // Runs after ChestObj determines which SpecialItemType to drop. For bronze and
    // silver chests we override the result with our own probability roll so that
    // the configured AP chance replaces the game's native blueprint drop odds.
    //
    // Forcing Blueprint on a hit ensures the downstream postfix (and the coroutine's
    // existing blueprint code path) take over. Forcing Gold on a miss keeps the
    // vanilla gold-drop flow intact. Crucially, that path is safe with respect to
    // the coroutine's DisableInput / WaitUntil(IsMapEnabled) sequence.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChestObj), "GetSpecialItemTypeToDrop")]
    private static void GetSpecialItemTypeToDrop_Postfix(ChestObj __instance, ref SpecialItemType __result)
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return;
        if (__instance.ChestType != ChestType.Bronze && __instance.ChestType != ChestType.Silver) return;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) return;

        var biomeIndex = LocationRegistry.GetBiomeIndex(room.BiomeType);
        if (biomeIndex == null) return;

        var chance = __instance.ChestType == ChestType.Bronze
            ? APClient.BronzeChestApChance
            : APClient.SilverChestApChance;

        __result = Random.Range(0, 100) < chance
            ? SpecialItemType.Blueprint  // enter blueprint path → AP check may fire
            : SpecialItemType.Gold;      // vanilla gold drop, no AP involvement
    }

    // Runs after ChestObj resolves what blueprint object to drop. If AP is active
    // but there is no available location for this biome, we return null so the chest
    // coroutine falls back to its gold-drop path instead of granting a blueprint.
    //
    // We also handle the case where the game wanted to drop a blueprint but returned
    // null because no equipment met the room's level/rarity requirements. The
    // probability roll already happened in GetSpecialItemTypeToDrop, so we still
    // owe an AP check; we inject a placeholder BlueprintDrop so the chest coroutine
    // proceeds to DropSpecialItem (which our prefix intercepts and suppresses).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChestObj), "CalculateSpecialItemDropObj")]
    private static void CalculateSpecialItemDropObj_Postfix(ref ISpecialItemDrop __result, SpecialItemType specialItemType)
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return;

        bool triedBlueprint = specialItemType == SpecialItemType.Blueprint;
        bool gotBlueprint   = __result is IBlueprintDrop;
        if (!triedBlueprint && !gotBlueprint) return;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) { __result = null; return; }

        var biomeIndex = LocationRegistry.GetBiomeIndex(room.BiomeType);
        if (biomeIndex == null) { __result = null; return; }

        var locationId = LocationRegistry.NextBlueprintChestLocation(biomeIndex.Value, APClient.RunState.CheckedLocations);
        if (locationId == null) { __result = null; return; } // pool exhausted; null triggers gold fallback in coroutine

        // Level/rarity filter blocked the drop but the probability roll said "blueprint".
        // Inject a placeholder so DropSpecialItem_Prefix can send the AP check; the
        // prefix returns false so the dummy category/type values are never used.
        if (!gotBlueprint)
            __result = new BlueprintDrop(default, default);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemDropManager), nameof(ItemDropManager.DropSpecialItem))]
    private static bool DropSpecialItem_Prefix(ISpecialItemDrop specialItemDrop)
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return true;
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
