using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

/// <summary>
/// Fires a RuneChestLocation AP check when a gold chest is opened, using the
/// same per-biome rune pool as fairy chests. The gold chest's normal Challenge
/// drop and ore drops are unaffected (this is a pure side-effect hook).
/// </summary>
[HarmonyPatch]
internal static class GoldChestPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChestObj), "GetSpecialItemTypeToDrop")]
    private static void GetSpecialItemTypeToDrop_Postfix(ChestObj __instance)
    {
        if (!APClient.IsConnected || APClient.RunState == null) return;
        if (__instance.ChestType != ChestType.Gold) return;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) return;

        var biomeIndex = LocationRegistry.GetBiomeIndex(room.BiomeType);
        if (biomeIndex == null) return;

        var locationId = LocationRegistry.NextRuneChestLocation(biomeIndex.Value, APClient.RunState.CheckedLocations);
        if (locationId == null) return; // pool exhausted; gold chest behaves normally

        APClient.SendLocationCheck(locationId.Value);
        // __result is left unchanged (SpecialItemType.Challenge); ore and Challenge
        // drops continue through the normal gold-chest flow unmodified.
    }
}
