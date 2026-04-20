using System.Collections;
using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

/// <summary>
/// Patches covering the Theia's Sun Lantern location check, which is acquired by talking
/// to Johan (an NPC) rather than interacting with a heirloom statue.
///
/// <para><b>GiveHeirloomCoroutine prefix</b> — fires when Johan is about to hand the player
/// the lantern. Sends the AP location check and suppresses the vanilla grant (heirloom,
/// victory animation, SpecialItemDrop window). The lantern ability arrives later via
/// <see cref="APClient.GrantItem"/> when the AP server responds.</para>
///
/// <para><b>IsJohanSpawnConditionTrue postfix</b> — overrides the
/// <c>TowerBossBeatenAndNotCollectedLantern</c> spawn condition so that Johan keeps
/// appearing until the AP location is checked, regardless of whether the player already
/// received the lantern item from the AP server (e.g. sent early by another player).</para>
/// </summary>
[HarmonyPatch]
internal static class JohanLanternPatch
{
    /// <summary>
    /// Handles giving an AP check when talking to Johan after beating Irad and suppressing the vanilla lantern-granting behavior.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(JohanPropController), "GiveHeirloomCoroutine")]
    private static bool GiveHeirloomCoroutine_Prefix(JohanPropController __instance, ref IEnumerator __result)
    {
        if (!APClient.IsConnected) return true;
        if (APClient.RunState == null) return true;
        if (APClient.RunState.CheckedLocations.Contains(LocationRegistry.HeirloomCaveLantern)) return true;

        Traverse.Create(__instance).Field<bool>("m_canGiveLantern").Value = false;
        RewiredMapController.SetCurrentMapEnabled(enabled: true);
        APClient.SendLocationCheck(LocationRegistry.HeirloomCaveLantern);

        __result = EmptyCoroutine();
        return false;
    }

    /// <summary>
    /// Ensure that Johan still spawns as an AP location check until it is checked even if Theia's sun lantern was gotten previously
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(JohanPropController), nameof(JohanPropController.IsJohanSpawnConditionTrue))]
    private static void IsJohanSpawnConditionTrue_Postfix(
        JohanPropController.Johan_SpawnCondition spawnCondition,
        ref bool __result)
    {
        bool vanillaResult = __result;

        if (!APClient.IsConnected) return;
        if (APClient.RunState == null) return;
        if (spawnCondition != JohanPropController.Johan_SpawnCondition.TowerBossBeatenAndNotCollectedLantern) return;

        __result = BossID_RL.IsBossBeaten(BossID.Tower_Boss)
                   && !APClient.RunState.CheckedLocations.Contains(LocationRegistry.HeirloomCaveLantern);

        Plugin.Log.LogDebug(
            $"[JohanLanternPatch] IsJohanSpawnConditionTrue override: "
            + $"vanilla={vanillaResult} -> ap={__result} "
            + $"(towerBeaten={BossID_RL.IsBossBeaten(BossID.Tower_Boss)}, "
            + $"locationChecked={APClient.RunState.CheckedLocations.Contains(LocationRegistry.HeirloomCaveLantern)})");
    }

    private static IEnumerator EmptyCoroutine() { yield break; }
}
