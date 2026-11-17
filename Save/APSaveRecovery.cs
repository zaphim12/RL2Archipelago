using RL_Windows;
using System;
using System.Diagnostics;

namespace RL2Archipelago.Save;

/// <summary>
/// Drives the player through the game's own corrupt-save recovery when a seed's save cannot be
/// loaded, and guarantees the save redirect is torn down afterwards no matter how they leave it.
///
/// <para>The game already has everything needed: <c>MainMenuWindowController.OnOpenCoroutine</c>
/// re-runs <c>LoadCurrentProfileData()</c>, checks <c>SaveManager.LoadingFailed</c>, and raises a
/// "Save File Error" prompt offering <b>Load Backup</b> / <b>Switch Profile</b> / <b>Quit</b>. All we
/// have to do is reopen the main menu with the redirect still live, so that flow operates on the AP
/// folder's backups instead of the vanilla ones.</para>
///
/// <para>The redirect deliberately stays active for the whole flow - the backup window reads
/// <c>SaveManager.CurrentProfile</c> and the backup path cache, both of which revert on rollback -
/// so this class is also the thing that makes sure it cannot leak past the flow.</para>
/// </summary>
internal static class APSaveRecovery
{
    private enum Phase
    {
        /// <summary>No recovery underway.</summary>
        Idle,

        /// <summary>Menu reload requested; waiting for the game to raise its save-error prompt.</summary>
        PendingPrompt,

        /// <summary>The prompt (or the backup window) is open; waiting for the player to finish.</summary>
        PromptOpen,
    }

    /// <summary>
    /// How long to wait for the game's prompt before giving up and rolling back. Generous: the menu
    /// reload runs a scene transition and the prompt coroutine waits on it.
    /// </summary>
    private const int PromptTimeoutMs = 20000;

    /// <summary>
    /// Consecutive idle ticks that must observe a live redirect with no session before the safety net
    /// acts. Everything that touches the redirect runs synchronously on the main thread, so this is
    /// belt-and-braces against a reordering we did not anticipate.
    /// </summary>
    private const int StrayRedirectTicksBeforeRollback = 2;

    private static Phase _phase = Phase.Idle;
    private static bool _menuReloadRequested;
    private static string _directoryName;
    private static int _strayRedirectTicks;
    private static readonly Stopwatch _watch = new();

    /// <summary>True while the player is being walked through save recovery.</summary>
    internal static bool InProgress => _phase != Phase.Idle;

    /// <summary>
    /// Starts recovery for the given seed folder. The caller must leave the redirect active and must
    /// not re-prompt the player for connection details - the game's own dialog takes over from here.
    /// </summary>
    internal static void Begin(string directoryName)
    {
        if (InProgress) return;

        _phase = Phase.PendingPrompt;
        _menuReloadRequested = false;
        _directoryName = directoryName;
        _watch.Restart();

        Plugin.Log.LogWarning(
            $"[APSave] Entering save recovery for '{directoryName}'. Reopening the main menu so the " +
            "game's backup prompt runs against this seed's backups.");
    }

    /// <summary>Cancels recovery without restoring anything, and returns the game to vanilla saves.</summary>
    internal static void Abort() => Finish("the player cancelled");

    /// <summary>
    /// Pumped every frame from <see cref="Patches.MainThreadDispatchPatch"/>. Also carries an
    /// unconditional safety net: the redirect must never outlive its session.
    /// </summary>
    internal static void Tick()
    {
        switch (_phase)
        {
            case Phase.Idle:
                TickStrayRedirectGuard();
                return;

            case Phase.PendingPrompt:
                TickPendingPrompt();
                return;

            case Phase.PromptOpen:
                if (!IsPromptOpen())
                    Finish(null);
                return;
        }
    }

    /// <summary>
    /// Rolls the redirect back if it is somehow still active with no session and no recovery underway.
    /// Without this, a path we failed to cover would silently send every vanilla save into a seed folder.
    /// </summary>
    private static void TickStrayRedirectGuard()
    {
        if (!APSaveRedirect.APSaveActive || APClient.IsSessionActive)
        {
            _strayRedirectTicks = 0;
            return;
        }

        if (++_strayRedirectTicks < StrayRedirectTicksBeforeRollback) return;

        _strayRedirectTicks = 0;
        Plugin.Log.LogWarning(
            "[APSave] Save redirect was still active with no Archipelago session; rolling it back so " +
            "vanilla saves do not land in the seed folder.");
        APSaveRedirect.Rollback(restoreSnapshot: false);
    }

    private static void TickPendingPrompt()
    {
        // Deferred by one frame on purpose: Begin() runs inside the connection dialog's own click
        // handler, and reopening a window from there is asking for trouble.
        if (!_menuReloadRequested)
        {
            _menuReloadRequested = true;
            RequestMenuReload();
            return;
        }

        if (IsPromptOpen())
        {
            _phase = Phase.PromptOpen;
            return;
        }

        if (_watch.ElapsedMilliseconds > PromptTimeoutMs)
            Finish($"the save-error prompt never appeared within {PromptTimeoutMs}ms");
    }

    /// <summary>
    /// Closes and reopens the main menu, which re-runs <c>OnOpenCoroutine</c> - the one place the game
    /// checks <c>LoadingFailed</c> and offers the backup prompt.
    /// </summary>
    private static void RequestMenuReload()
    {
        try
        {
            if (!WindowManager.IsInstantiated || !WindowManager.GetIsWindowLoaded(WindowID.MainMenu))
            {
                Finish("the main menu was not available to reload");
                return;
            }

            WindowManager.SetWindowIsOpen(WindowID.MainMenu, isOpen: false);
            WindowManager.SetWindowIsOpen(WindowID.MainMenu, isOpen: true);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] Could not reopen the main menu for save recovery: {ex.Message}");
            Finish("the main menu could not be reopened");
        }
    }

    private static bool IsPromptOpen()
    {
        try
        {
            return WindowManager.IsInstantiated
                && (WindowManager.GetIsWindowOpen(WindowID.ConfirmMenu)
                    || WindowManager.GetIsWindowOpen(WindowID.Backup));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[APSave] Could not read window state during save recovery: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ends recovery and returns to vanilla saves. <paramref name="abortReason"/> is null when the
    /// player worked through the prompt normally (whether or not they restored anything).
    /// </summary>
    private static void Finish(string abortReason)
    {
        if (_phase == Phase.Idle) return;

        var directoryName = _directoryName;

        // A successful restore clears LoadingFailed (BackupSlotEntry.LoadBackup does it before
        // re-saving). If it is still set, either nothing was restored or we set it ourselves to raise
        // the prompt - and it must not stay latched, because it silently blocks every vanilla save
        // too. Clearing it before the rollback's reload lets that reload re-evaluate honestly.
        bool restored = !SaveManager.LoadingFailed;
        SaveManager.LoadingFailed = false;

        _phase = Phase.Idle;
        _menuReloadRequested = false;
        _directoryName = null;
        _strayRedirectTicks = 0;
        _watch.Reset();

        APSaveRedirect.Rollback(restoreSnapshot: false);

        // Log only, no toast: the player is already looking at the game's own dialog, which says
        // everything this would and says it better.
        if (abortReason != null)
            Plugin.Log.LogWarning($"[APSave] Save recovery for '{directoryName}' ended early: {abortReason}.");
        else if (restored)
            Plugin.Log.LogInfo($"[APSave] Backup restored for '{directoryName}'. Reconnect to resume.");
        else
            Plugin.Log.LogWarning($"[APSave] Save recovery for '{directoryName}' closed without a restore.");
    }
}
