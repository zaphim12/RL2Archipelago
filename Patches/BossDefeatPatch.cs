using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

/// <summary>
/// Reports boss kills to the Archipelago server as location checks.
///
/// <para>
/// Hooks <c>BossRoomController.SetBossFlagDefeated</c>, which the game calls
/// from the boss outro once the boss has died and the save flag is being
/// persisted. <c>m_bossSaveFlag</c> identifies which biome's boss was defeated.
/// </para>
///
/// <para>
/// The only subclass override, <c>FinalBossRoomController.SetBossFlagDefeated</c>,
/// calls <c>base.SetBossFlagDefeated()</c>, so this single postfix covers every
/// boss room — no per-subclass patch needed.
/// </para>
///
/// <para>
/// We use a postfix so the game's save-write happens first; our send is
/// idempotent on the AP server and <see cref="APClient.RunState"/>
/// deduplicates locally.
/// </para>
/// </summary>
[HarmonyPatch]
internal static class BossDefeatPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BossRoomController), "SetBossFlagDefeated")]
    private static void SetBossFlagDefeated_Postfix(BossRoomController __instance)
    {
        if (!APClient.IsConnected) return;

        var flag = Traverse.Create(__instance).Field<PlayerSaveFlag>("m_bossSaveFlag").Value;
        var locationId = LocationRegistry.FromBossSaveFlag(flag);
        if (locationId is null)
        {
            Plugin.Log.LogDebug(
                $"[BossDefeatPatch] Boss flag {flag} is not a tracked AP location — ignoring.");
            return;
        }

        APClient.SendLocationCheck(locationId.Value);
    }
}
