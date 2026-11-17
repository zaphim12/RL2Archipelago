using RL2Archipelago.Locations;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace RL2Archipelago.Save;

/// <summary>
/// Owns the per-seed save redirect: activating it, proving the redirected save is actually healthy,
/// and putting the vanilla profile back if anything goes wrong.
///
/// <para>This used to be inlined in <see cref="APClient.Connect"/> and <see cref="APClient.Disconnect"/>,
/// where an exception part-way through left <c>APSaveActive == true</c> with no session - quietly
/// sending every later vanilla save into the AP folder. The ordering here is load-bearing; see
/// <see cref="TryActivate"/>.</para>
/// </summary>
internal static class APSaveRedirect
{
    /// <summary>Folder under the game's persistent data path that holds every per-seed save tree.</summary>
    internal const string RootFolderName = "AP_Saves";

    /// <summary>
    /// <c>SaveManager.IsValidBackupPath</c> substring-matches these against the <em>whole absolute
    /// path</em> to decide which save type a backup file belongs to. A seed folder containing one of
    /// them makes every backup register as that type, and the backup window pairs the five types by
    /// list index - so a restore would be handed a Stage file as its Player file.
    /// </summary>
    private static readonly string[] ReservedTokens =
    {
        "Player", "Stage", "Equipment", "Lineage", "GameMode", "Account", ".rc2dat",
    };

    private static readonly string[] ReservedDeviceNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>How long to wait for an in-flight vanilla save before flipping the path anyway.</summary>
    private const int SaveIdleTimeoutMs = 3000;

    /// <summary>True while save I/O is redirected into this seed's folder.</summary>
    internal static bool APSaveActive { get; private set; }

    /// <summary>Sanitized <c>{RoomId}_{SlotName}</c> used as the per-seed subdirectory name.</summary>
    internal static string APSaveDirectoryName { get; private set; }

    /// <summary>
    /// Everything a failed activation has to put back. The five save-data statics matter because
    /// <c>LoadCurrentProfileData</c>'s failure branch calls <c>CreateNewSaveData()</c>, which replaces
    /// them with blank instances - so a failed connect would otherwise leave the player staring at an
    /// empty vanilla profile.
    /// </summary>
    private sealed class VanillaSnapshot
    {
        public byte CurrentProfile;
        public bool LoadingFailed;
        public PlayerSaveData Player;
        public StageSaveData Stage;
        public EquipmentSaveData Equipment;
        public LineageSaveData Lineage;
        public ModeSaveData Mode;
    }

    private static VanillaSnapshot _snapshot;

    // ── Activation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Points save I/O at this seed's folder, loads its run state and profile data, and verifies the
    /// result. On failure nothing is left changed: the vanilla profile, backup cache and save-directory
    /// latch are all restored before returning.
    ///
    /// <para>Returns false with a player-facing <paramref name="error"/>. The caller must surface it
    /// and must not proceed with the session.</para>
    /// </summary>
    internal static bool TryActivate(APConnectionData connData, out APRunState runState, out string error)
    {
        runState = null;
        error = null;

        // 1. Never flip the path with a write in flight - it would land in the wrong tree.
        APSaveGuardPatches.HealStuckSaveState();
        WaitForSaveIdle();

        // 2. Capture what a failure has to restore, before anything is touched.
        _snapshot = CaptureVanillaSnapshot();

        try
        {
            // 3. Resolve the folder name and load run state while paths are still vanilla
            //    (ResolveDirectoryName probes the real persistent data path).
            APSaveDirectoryName = ResolveDirectoryName(connData);
            runState = APRunState.Load(APSaveDirectoryName);

            // 4. Path and profile must flip together: GetSaveDirectoryPath reads both.
            APSaveActive = true;
            SaveManager.ConfigData.CurrentProfile = 0;

            // 5. Build the redirected tree. The explicit config-directory create comes first so that
            //    backing out of a settings submenu (which calls SaveConfigFile and rethrows) cannot
            //    fail before anything else has had a chance to create it.
            var configDir = Path.GetDirectoryName(SaveManager.GetConfigPath());
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            APSaveGuardPatches.ResetSaveDirectoryCheck();
            APSaveGuardPatches.CreateSaveDirectories();

            // 6. Re-scan backups against the redirected path. Must follow step 5: this leaves a slot
            //    null when the directory does not exist, and a null slot faults the save chain on the
            //    first castle entry. It is also what stops AP play from pruning vanilla backups, since
            //    the cache no longer holds vanilla paths for DeleteOldBackups to sort and trim.
            SaveManager.InitializeBackupFilePaths();
            APSaveGuardPatches.EnsureBackupSlots();

            // 7. Clear the latch so step 9 sees a fresh verdict, not a sticky vanilla one.
            SaveManager.LoadingFailed = false;

            // 8. Load this seed's profile.
            SaveManager.LoadCurrentProfileData();

            // 9. Prove it actually loaded.
            if (!PassesHealthGate(runState, out error))
            {
                runState = null;
                return false;
            }

            Plugin.Log.LogInfo($"[APSave] Save redirected to {RootFolderName}/{APSaveDirectoryName}");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] Failed to activate the save redirect: {ex}");
            error = $"Could not prepare the Archipelago save folder: {ex.Message}";
            Rollback(restoreSnapshot: true);
            runState = null;
            return false;
        }
    }

    // ── Health gate ───────────────────────────────────────────────────────────

    /// <summary>
    /// Decides whether the redirected profile is safe to play on, and routes recovery if not.
    ///
    /// <para>Two independent failures look identical to the player but not to the game:</para>
    /// <list type="bullet">
    /// <item><b>Corrupt</b> - a save file exists but failed to deserialize. The game notices
    /// (<c>LoadingFailed</c>) and silently disables all further saving.</item>
    /// <item><b>Unexpectedly blank</b> - the save files are missing entirely.
    /// <c>LoadCurrentProfileData</c> probes only <c>Player.rc2dat</c>, so it treats this as a brand-new
    /// profile, leaves <c>LoadingFailed</c> false, and keeps saving enabled - which then writes the
    /// blank state over whatever was there. The game reports nothing at all.</item>
    /// </list>
    /// </summary>
    private static bool PassesHealthGate(APRunState runState, out string error)
    {
        error = null;

        var player = SaveManager.PlayerSaveData;
        bool corrupt = SaveManager.LoadingFailed;

        // Only treat "blank" as a failure when we know this run's save was real at some point.
        // Checked locations can only be produced from inside a run, and HasStartedGameObserved is
        // latched the first time we see a healthy save - so either one proves the save should exist.
        bool knownEstablishedRun = runState.CheckedLocations.Count > 0 || runState.HasStartedGameObserved;
        bool unexpectedlyBlank = !corrupt
            && player != null
            && !player.HasStartedGame
            && knownEstablishedRun;

        if (!corrupt && !unexpectedlyBlank)
        {
            // Healthy. Latch that this run's save is real so a later blank load is detectable.
            if (player != null && player.HasStartedGame && !runState.HasStartedGameObserved)
            {
                runState.HasStartedGameObserved = true;
                runState.Save(APSaveDirectoryName);
            }
            return true;
        }

        var reason = corrupt
            ? "could not be loaded (corrupt save file)"
            : "came back empty even though this run has progress recorded";

        Plugin.Log.LogError(
            $"[APSave] The Archipelago save for '{APSaveDirectoryName}' {reason}. Refusing to continue.");

        if (!HasPlayerBackups())
        {
            Rollback(restoreSnapshot: true);
            error = $"The Archipelago save for this seed {reason}, and no backups exist.\n" +
                    $"Folder: {RootFolderName}/{APSaveDirectoryName}";
            return false;
        }

        // Backups exist, so hand the player to the game's own restore flow. The redirect stays live
        // on purpose: the backup window reads SaveManager.CurrentProfile and the backup path cache,
        // both of which would revert to vanilla if we rolled back here.
        //
        // Setting LoadingFailed is what makes MainMenuWindowController raise its "Save File Error"
        // prompt. In the blank case the game never set it, because it never noticed - and it also
        // blocks all writes, which is exactly what we want while the backups are the only good copy.
        SaveManager.LoadingFailed = true;
        APSaveRecovery.Begin(APSaveDirectoryName);

        error = $"The Archipelago save for this seed {reason}.\n" +
                "Restore a backup from the save-error prompt, then connect again.";
        return false;
    }

    private static bool HasPlayerBackups()
    {
        try
        {
            return SaveManager.GetBackupPathes(SaveManager.CurrentProfile, SaveDataType.Player).Count > 0;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[APSave] Could not enumerate backups: {ex.Message}");
            return false;
        }
    }

    // ── Rollback ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Puts the vanilla save path, profile and backup cache back. Idempotent, and safe to call when
    /// the redirect was never activated.
    /// </summary>
    /// <param name="restoreSnapshot">
    /// True on a failure path: re-seat the save-data statics captured at activation, which is cheaper
    /// than a reload and cannot re-trip <c>LoadingFailed</c>. False on a clean disconnect, where a
    /// genuine reload of the vanilla profile is wanted.
    /// </param>
    internal static void Rollback(bool restoreSnapshot)
    {
        if (!APSaveActive && _snapshot == null) return;

        bool wasActive = APSaveActive;

        // Deactivate first so every path rebuilt below resolves against the vanilla tree.
        APSaveActive = false;

        try
        {
            if (_snapshot != null)
                SaveManager.ConfigData.CurrentProfile = _snapshot.CurrentProfile;

            SaveManager.InitializeBackupFilePaths();
            APSaveGuardPatches.EnsureBackupSlots();
            APSaveGuardPatches.ResetSaveDirectoryCheck();

            if (restoreSnapshot && _snapshot != null)
                RestoreVanillaSnapshot(_snapshot);
            else
                SaveManager.LoadCurrentProfileData();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] Rollback hit an error: {ex}");
        }
        finally
        {
            _snapshot = null;
            APSaveDirectoryName = null;
            if (wasActive)
                Plugin.Log.LogInfo("[APSave] Save redirect deactivated; vanilla profile restored.");
        }
    }

    private static VanillaSnapshot CaptureVanillaSnapshot() => new()
    {
        CurrentProfile = SaveManager.ConfigData.CurrentProfile,
        LoadingFailed  = SaveManager.LoadingFailed,
        Player         = SaveManager.PlayerSaveData,
        Stage          = SaveManager.StageSaveData,
        Equipment      = SaveManager.EquipmentSaveData,
        Lineage        = SaveManager.LineageSaveData,
        Mode           = SaveManager.ModeSaveData,
    };

    private static void RestoreVanillaSnapshot(VanillaSnapshot s)
    {
        SaveManager.PlayerSaveData    = s.Player;
        SaveManager.StageSaveData     = s.Stage;
        SaveManager.EquipmentSaveData = s.Equipment;
        SaveManager.LineageSaveData   = s.Lineage;
        SaveManager.ModeSaveData      = s.Mode;
        SaveManager.LoadingFailed     = s.LoadingFailed;
    }

    // ── Path helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the <c>AP_Saves\{seed}</c> segment from a path that was built while the redirect was
    /// active, so individual callers can be exempted from it (see
    /// <see cref="APSaveGuardPatches.GetControllerPath_Postfix"/>). No-op when not redirected.
    /// </summary>
    internal static string StripRedirect(string path)
    {
        if (!APSaveActive || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(APSaveDirectoryName))
            return path;

        var sep = Path.DirectorySeparatorChar;
        var needle = $"{sep}{RootFolderName}{sep}{APSaveDirectoryName}{sep}";

        int at = path.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return path;

        // Remove the segment but leave one separator behind so the path stays well-formed.
        return path.Remove(at, needle.Length - 1);
    }

    /// <summary>
    /// Picks this seed's folder name. Prefers an existing folder built by an older build so an
    /// in-progress run is never re-pointed at an empty directory; only new seeds get the stricter name.
    /// </summary>
    private static string ResolveDirectoryName(APConnectionData connData)
    {
        var raw = $"{connData.RoomId}_{connData.SlotName}";
        var legacy = SanitizeFileName(raw);
        var safe = MakeSaveManagerSafe(legacy, raw);

        if (!string.Equals(legacy, safe, StringComparison.Ordinal) && SeedFolderExists(legacy))
        {
            Plugin.Log.LogWarning(
                $"[APSave] Keeping the existing save folder '{legacy}'. Its name contains a token the " +
                "game matches against backup paths, so this seed's backup list may be unreliable.");
            return legacy;
        }

        return safe;
    }

    /// <summary>Must be called while the redirect is inactive, so the base path is the vanilla one.</summary>
    private static bool SeedFolderExists(string directoryName)
    {
        try
        {
            return Directory.Exists(
                Path.Combine(SaveFileSystem.PersistentDataPath, RootFolderName, directoryName));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[APSave] Could not probe for an existing seed folder: {ex.Message}");
            return false;
        }
    }

    /// <summary>Replaces characters that are illegal in a filename. The original behavior.</summary>
    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    /// <summary>
    /// Neutralizes anything in the folder name that would confuse the game's own path handling, and
    /// appends a short hash of the original when it had to change anything, so two seeds that differ
    /// only in a neutralized token cannot collide.
    /// </summary>
    private static string MakeSaveManagerSafe(string sanitized, string raw)
    {
        var result = sanitized;
        bool changed = false;

        foreach (var token in ReservedTokens)
        {
            int at;
            while ((at = result.IndexOf(token, StringComparison.Ordinal)) >= 0)
            {
                // Breaking the first character is enough to stop the substring match, and keeps the
                // name recognizable to a human browsing the folder.
                result = result.Remove(at, 1).Insert(at, "_");
                changed = true;
            }
        }

        // Windows silently drops trailing dots and spaces from directory names.
        var trimmed = result.TrimEnd('.', ' ');
        if (trimmed.Length != result.Length)
        {
            result = trimmed;
            changed = true;
        }

        if (result.Length == 0)
        {
            result = "seed";
            changed = true;
        }

        if (IsReservedDeviceName(result))
        {
            result = "_" + result;
            changed = true;
        }

        return changed ? $"{result}_{Hashing.Fnv1a32Hex(raw)}" : result;
    }

    private static bool IsReservedDeviceName(string name)
    {
        int dot = name.IndexOf('.');
        var stem = dot >= 0 ? name.Substring(0, dot) : name;

        foreach (var reserved in ReservedDeviceNames)
            if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    private static void WaitForSaveIdle()
    {
        if (!SaveFileSystem.IsSaving) return;

        var watch = Stopwatch.StartNew();
        while (SaveFileSystem.IsSaving && watch.ElapsedMilliseconds < SaveIdleTimeoutMs)
            Thread.Sleep(16);

        if (SaveFileSystem.IsSaving)
            Plugin.Log.LogWarning(
                $"[APSave] A game save was still in flight after {SaveIdleTimeoutMs}ms; continuing anyway.");
    }
}
