using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

[HarmonyPatch]
internal static class RoomReplacementPatch
{
    // In vanilla, CreateSpecialRoomPool replaces the two Citadel Agartha heirloom rooms with relic
    // rooms on future runs by checking IsConditionFulfilled(ReplacementCriteria), which tests
    // GetHeirloomLevel > 0. In AP mode we never set the heirloom level at collection time — the
    // ability is only granted when the AP server sends the item back — so the replacement never
    // triggers after the player dies and a new world is generated. This prefix treats a checked AP
    // location as equivalent to "heirloom obtained" for the purposes of that room-replacement check.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ConditionFlag_RL), nameof(ConditionFlag_RL.IsConditionFulfilled))]
    private static bool IsConditionFulfilled_Prefix(ConditionFlag id, ref bool __result)
    {
        if (!APClient.IsConnected) return true;
        if (APClient.RunState == null) return true;

        long? locationId = id switch
        {
            ConditionFlag.Heirloom_Dash       => LocationRegistry.HeirloomAirDash,
            ConditionFlag.Heirloom_Memory     => LocationRegistry.HeirloomMemory,
            ConditionFlag.Heirloom_DoubleJump => LocationRegistry.HeirloomDoubleJump,
            ConditionFlag.Heirloom_Downstrike => LocationRegistry.HeirloomBouncableDownstrike,
            ConditionFlag.Heirloom_VoidDash   => LocationRegistry.HeirloomVoidDash,
            _ => null
        };

        if (locationId is null) return true;

        // Always short-circuit for heirloom flags: never let vanilla's GetHeirloomLevel check run.
        // If we fell through, a received-but-unchecked heirloom ability would trigger replacement
        // and permanently block the AP location from being collected.
        __result = APClient.RunState.CheckedLocations.Contains(locationId.Value);
        return false;
    }
}
