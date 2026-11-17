using HarmonyLib;
using RL_Windows;
using System;

namespace RL2Archipelago.Save;

/// <summary>
/// Adjusts the game's own corrupt-save prompt for the case where the broken profile is an
/// Archipelago seed folder rather than a vanilla save slot.
///
/// <para>The prompt itself is reused as-is - its <b>Load Backup</b> button is exactly what we want,
/// and with the redirect still live it operates on the seed's backups. Only the escape routes need
/// changing: <b>Switch Profile</b> would drop the player into the profile-select screen while save
/// I/O is still redirected, so they would be browsing (and able to create) profiles inside the seed
/// folder, believing they were their real save slots.</para>
/// </summary>
[HarmonyPatch]
internal static class APSaveRecoveryPatches
{
    /// <summary>
    /// Index of the first button. In the no-backups branch the game wires this to Switch Profile; in
    /// the backups branch it is Load Backup, which is left alone.
    /// </summary>
    private const int FirstButtonIndex = 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuWindowController), "InitializeLoadingFailedConfirmMenu")]
    private static void InitializeLoadingFailedConfirmMenu_Postfix()
    {
        if (!APSaveRedirect.APSaveActive) return;

        try
        {
            if (WindowManager.GetWindowController(WindowID.ConfirmMenu)
                is not ConfirmMenuWindowController confirmMenu)
                return;

            // Escape / back out of the prompt returns to vanilla saves instead of quitting the game.
            confirmMenu.SetOnCancelAction(APSaveRecovery.Abort);

            // Mirror the branch the game just took. When backups exist, button 0 is Load Backup and
            // must stay as it is.
            if (HasPlayerBackups()) return;

            var cancelButton = confirmMenu.GetButtonAtIndex(FirstButtonIndex);
            if (cancelButton == null) return;

            cancelButton.SetButtonText("Cancel", isLocID: false);
            cancelButton.SetOnClickAction(CancelFromPrompt);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(
                $"[APSave] Could not adapt the save-error prompt for Archipelago: {ex.Message}");
        }
    }

    private static void CancelFromPrompt()
    {
        try
        {
            WindowManager.SetWindowIsOpen(WindowID.ConfirmMenu, isOpen: false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[APSave] Could not close the save-error prompt: {ex.Message}");
        }

        APSaveRecovery.Abort();
    }

    private static bool HasPlayerBackups()
    {
        try
        {
            return SaveManager.GetBackupPathes(SaveManager.CurrentProfile, SaveDataType.Player).Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
