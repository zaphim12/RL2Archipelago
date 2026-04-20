using HarmonyLib;
using Rewired;
using RL_Windows;
using UnityEngine;

namespace RL2Archipelago.Patches;

[HarmonyPatch]
internal static class DebugPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WindowManager), "Update")]
    private static void WindowManager_Update_Postfix()
    {
        var keyboard = ReInput.controllers.Keyboard;

        if (keyboard.GetKeyDown(KeyCode.Alpha0))
        {
            if (MapController.IsInitialized)
            {
                MapController.SetAllBiomeVisibility(isAllVisible: true, updateWasVisitedState: true, retainVisitedRoomData: false);
                Plugin.Log.LogInfo("[Debug] Revealed all map rooms.");
            }
            else
            {
                Plugin.Log.LogInfo("[Debug] Key 0 pressed but MapController is not initialized.");
            }
        }

        if (keyboard.GetKeyDown(KeyCode.Alpha9))
        {
            if (PlayerManager.IsInstantiated)
            {
                var player = PlayerManager.GetPlayerController();
                player.SetHealth(player.ActualMaxHealth, additive: false, runEvents: true);
                Plugin.Log.LogInfo("[Debug] Restored player to full health.");
            }
            else
            {
                Plugin.Log.LogInfo("[Debug] Key 9 pressed but PlayerManager is not instantiated.");
            }
        }
    }
}
