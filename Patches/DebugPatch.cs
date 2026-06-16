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
        if (!APSettings.DebugKeysEnabled) return;
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

        if (keyboard.GetKeyDown(KeyCode.Alpha8))
        {
            if (SaveManager.PlayerSaveData != null)
            {
                int prevGold = SaveManager.PlayerSaveData.GoldCollected;
                SaveManager.PlayerSaveData.GoldCollected += 1000;
                Messenger<GameMessenger, GameEvent>.Broadcast(GameEvent.GoldChanged, null, new GoldChangedEventArgs(prevGold, SaveManager.PlayerSaveData.GoldCollected));
                Plugin.Log.LogInfo("[Debug] Added 1000 gold to player.");
            }
            else
            {
                Plugin.Log.LogInfo("[Debug] Key 8 pressed but PlayerSaveData is not available.");
            }
        }

        if (keyboard.GetKeyDown(KeyCode.Alpha7))
        {
            if (SaveManager.PlayerSaveData != null)
            {
                SaveManager.PlayerSaveData.SetFlag(PlayerSaveFlag.CastleBoss_Defeated, value: true);
                SaveManager.PlayerSaveData.SetFlag(PlayerSaveFlag.BridgeBoss_Defeated, value: true);
                SaveManager.PlayerSaveData.SetFlag(PlayerSaveFlag.ForestBoss_Defeated, value: true);
                SaveManager.PlayerSaveData.SetFlag(PlayerSaveFlag.StudyBoss_Defeated, value: true);
                SaveManager.PlayerSaveData.SetFlag(PlayerSaveFlag.TowerBoss_Defeated, value: true);
                SaveManager.PlayerSaveData.SetFlag(PlayerSaveFlag.CaveBoss_Defeated, value: true);
                Plugin.Log.LogInfo("[Debug] Set all 6 main boss defeated flags.");
            }
            else
            {
                Plugin.Log.LogInfo("[Debug] Key 7 pressed but PlayerSaveData is not available.");
            }
        }
    }
}
