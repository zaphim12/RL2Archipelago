using HarmonyLib;
using RL2Archipelago.Traps;
using UnityEngine;

namespace RL2Archipelago.Patches;

[HarmonyPatch]
internal static class PlayerDeathPatch
{
    private static bool _wasAlreadyDead;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerController), "KillCharacter")]
    private static void KillCharacter_Prefix(PlayerController __instance)
    {
        _wasAlreadyDead = __instance.IsDead;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerController), "KillCharacter")]
    private static void KillCharacter_Postfix()
    {
        if (_wasAlreadyDead) return;

        TrapManager.ClearAllTraps();
        if (APClient.RunState != null)
        {
            APClient.RunState.ActiveTraps.Clear();
            APClient.RunState.Save(APClient.APSaveDirectoryName);
        }

        if (!APClient.IsConnected || !APClient.DeathLinkEnabled || !APClient.IsInGame) return;
        if (APClient.IsReceivingDeathLink) return;

        APClient.SendDeathLink();
    }
}
