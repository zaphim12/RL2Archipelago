using HarmonyLib;
using System;
using System.IO;

namespace RL2Archipelago.Patches;

[HarmonyPatch]
internal static class APSavePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveFileSystem), nameof(SaveFileSystem.PersistentDataPath), MethodType.Getter)]
    private static void PersistentDataPath_Postfix(ref string __result)
    {
        if (APClient.APSaveActive)
            __result = Path.Combine(__result, "AP_Saves", APClient.APSaveDirectoryName);
    }

    /// <summary>
    /// Safety net for the "stuck in a settings submenu" bug. SuboptionsWindowController.OnClose
    /// calls SaveManager.SaveConfigFile() before removing the window from the open list, and that
    /// method re-throws on any IO failure. An exception there unwinds out of OnClose before the
    /// window is closed, leaving the submenu permanently unclosable. <see cref="APClient"/> now
    /// ensures the redirected save directory exists, but this finalizer guarantees that even an
    /// unexpected config-save failure (e.g. a transient file lock) can never wedge the menu: it
    /// logs the error and suppresses it so OnClose can finish and the window closes normally.
    /// </summary>
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveConfigFile))]
    private static Exception SaveConfigFile_Finalizer(Exception __exception)
    {
        if (__exception != null)
            Plugin.Log.LogError(
                $"[AP] SaveConfigFile failed; suppressing so the menu can still close: {__exception}");
        return null; // swallow
    }
}
