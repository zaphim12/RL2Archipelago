using System.Collections;
using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

/// <summary>
/// Patches covering the Theia's Sun Lantern location check, which is acquired by talking
/// to Johan (an NPC) rather than interacting with a heirloom statue.
///
/// <para><b>GiveHeirloomCoroutine prefix</b> - fires when Johan is about to hand the player
/// the lantern. Sends the AP location check and suppresses the vanilla grant (heirloom,
/// victory animation, SpecialItemDrop window). The lantern ability arrives later via
/// <see cref="APClient.GrantItem"/> when the AP server responds.</para>
///
/// <para><b>IsJohanSpawnConditionTrue postfix</b> - overrides the
/// <c>TowerBossBeatenAndNotCollectedLantern</c> spawn condition so that Johan keeps
/// appearing until the AP location is checked, regardless of whether the player already
/// received the lantern item from the AP server (e.g. sent early by another player).</para>
///
/// <para><b>InitializePooledPropOnEnter prefix/finalizer</b> - vanilla despawns Johan
/// outright (<c>gameObject.SetActive(false)</c>) at the top of that method once all six
/// main bosses are beaten AND <c>GetHeirloomLevel(CaveLantern) &gt; 0</c>, returning
/// before the spawn-condition block ever runs. In AP mode the lantern can arrive from
/// the server long before the location is checked, which would permanently strand the
/// check. The prefix temporarily reports the lantern as unowned for the duration of the
/// call and the finalizer restores it, so vanilla takes its pre-lantern branch and the
/// spawn-condition block above gets a chance to run.</para>
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
        if (!APClient.IsSessionActive) return true;
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

        if (!APClient.IsSessionActive) return;
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

    // Lantern level stashed for the duration of a single InitializePooledPropOnEnter call.
    // -1 means "nothing stashed"; the call is synchronous and main-thread only, so a pair
    // of static fields is sufficient and cannot interleave across prop instances.
    private static int _stashedLanternLevel = -1;
    private static bool _stashedTemporaryLantern;

    /// <summary>
    /// Hides the lantern from the vanilla "Johan is done with the overworld" early-out
    /// (all six main bosses beaten + lantern owned) while the AP location is still
    /// unchecked. Reads/writes <c>HeirloomLevelTable</c> directly rather than going
    /// through <c>SetHeirloomLevel</c> so no <c>HeirloomLevelChanged</c> event fires and
    /// the original value can be restored exactly.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(JohanPropController), "InitializePooledPropOnEnter")]
    private static void InitializePooledPropOnEnter_Prefix()
    {
        _stashedLanternLevel = -1;
        _stashedTemporaryLantern = false;

        if (!APClient.IsSessionActive) return;
        if (APClient.RunState == null) return;
        if (APClient.RunState.CheckedLocations.Contains(LocationRegistry.HeirloomCaveLantern)) return;

        var saveData = SaveManager.PlayerSaveData;
        if (saveData?.HeirloomLevelTable == null) return;
        if (!saveData.HeirloomLevelTable.TryGetValue(HeirloomType.CaveLantern, out var level)) return;

        // GetHeirloomLevel also reports 1 for heirlooms held only in the temporary list,
        // so that has to be suppressed too or the early-out still fires.
        var temporaryList = saveData.TemporaryHeirloomList;
        bool isTemporary = temporaryList != null && temporaryList.Contains(HeirloomType.CaveLantern);
        if (level <= 0 && !isTemporary) return;

        _stashedLanternLevel = level;
        _stashedTemporaryLantern = isTemporary;

        saveData.HeirloomLevelTable[HeirloomType.CaveLantern] = 0;
        if (isTemporary) temporaryList.Remove(HeirloomType.CaveLantern);

        Plugin.Log.LogDebug(
            "[JohanLanternPatch] Temporarily hiding CaveLantern (level="
            + $"{level}, temporary={isTemporary}) for Johan's spawn evaluation.");
    }

    /// <summary>
    /// Restores whatever <see cref="InitializePooledPropOnEnter_Prefix"/> stashed. A
    /// finalizer rather than a postfix so the lantern is never left zeroed if the
    /// vanilla method throws.
    /// </summary>
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(JohanPropController), "InitializePooledPropOnEnter")]
    private static void InitializePooledPropOnEnter_Finalizer()
    {
        if (_stashedLanternLevel < 0) return;

        var saveData = SaveManager.PlayerSaveData;
        if (saveData?.HeirloomLevelTable != null)
        {
            saveData.HeirloomLevelTable[HeirloomType.CaveLantern] = _stashedLanternLevel;

            var temporaryList = saveData.TemporaryHeirloomList;
            if (_stashedTemporaryLantern
                && temporaryList != null
                && !temporaryList.Contains(HeirloomType.CaveLantern))
            {
                temporaryList.Add(HeirloomType.CaveLantern);
            }
        }

        _stashedLanternLevel = -1;
        _stashedTemporaryLantern = false;
    }

    private static IEnumerator EmptyCoroutine() { yield break; }
}
