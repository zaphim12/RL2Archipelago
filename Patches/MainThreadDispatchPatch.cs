using HarmonyLib;
using RL_Windows;
using RL2Archipelago.UI;

namespace RL2Archipelago.Patches;

/// <summary>
/// Code in Plugin.Update() doesn't seem to actual trigger. This patch adds an alternative Update() method we can use
/// </summary>
[HarmonyPatch]
internal static class MainThreadDispatchPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WindowManager), "Update")]
    private static void WindowManager_Update_Postfix()
    {
        APClient.ProcessPendingItems();
        APClient.ProcessPendingDeaths();
        APClient.ProcessConnectionEvents();
        APNotifications.Tick();

        // Lazily create the disconnect indicator on the main thread once a session
        // is active. It self-shows while reconnecting and hides otherwise.
        if (APClient.ConnectionState != APConnectionState.Disconnected)
            APConnectionStatusOverlay.EnsureExists();
    }
}
