using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace RL2Archipelago.Save;

/// <summary>
/// Defensive patches and reflection helpers over the game's own save pipeline.
///
/// <para>RL2's saver has three failure modes that are unrecoverable without restarting the game,
/// and all three are reachable (or made more likely) by redirecting
/// <see cref="SaveFileSystem.PersistentDataPath"/>:</para>
/// <list type="number">
/// <item><c>SaveAsync</c> chains every save onto the previous one via <c>await lastAsyncTask</c>.
/// If one save's task faults, every later save rethrows at that await and writes nothing - for the
/// rest of the process, silently, with no way to heal.</item>
/// <item>That fault is easy to trigger: <c>SaveAsync</c> does
/// <c>m_backFilePathsArray[profileSlot].Add(path)</c> for a backup write, and
/// <c>InitializeBackupFilePaths</c> leaves a slot <c>null</c> whenever the backup directory did not
/// exist when it ran. The NRE also skips <c>saveBatch.DecrementRefCount()</c>.</item>
/// <item><see cref="SaveFileSystem.WriteAllBytes"/> and <see cref="SaveFileSystem.DeleteFile"/> have
/// no try/finally around their Increment/DecrementRefCount pair, so a throwing write leaks a batch
/// ref-count permanently. <c>SaveFileSystem.IsSaving</c> then never returns false, which costs a hard
/// 30-second stall on every main-menu entry (it is the only reader).</item>
/// </list>
///
/// <para>These patches do not change behavior on the happy path. They only keep a failure from
/// becoming permanent.</para>
/// </summary>
[HarmonyPatch]
internal static class APSaveGuardPatches
{
    /// <summary>Number of profile slots the game allocates (<c>SaveManager.m_backFilePathsArray</c>).</summary>
    internal const int ProfileSlotCount = 5;

    private static readonly FieldInfo LastAsyncTaskField =
        AccessTools.Field(typeof(SaveManager), "m_lastAsyncTask");

    private static readonly FieldInfo BackupFilePathsField =
        AccessTools.Field(typeof(SaveManager), "m_backFilePathsArray");

    private static readonly FieldInfo SaveManagerInstanceField =
        AccessTools.Field(typeof(SaveManager), "m_instance");

    private static readonly FieldInfo CheckedForSaveDirectoriesField =
        AccessTools.Field(typeof(SaveManager), "m_checkedForSaveDirectories");

    private static readonly FieldInfo TotalBatchRefCountField =
        AccessTools.Field(typeof(SaveFileSystem), "m_totalBatchRefCount");

    private static readonly MethodInfo CreateSaveDirectoriesIfNeededMethod =
        AccessTools.Method(typeof(SaveManager), "CreateSaveDirectoriesIfNeeded");

    // ── (a) Keep the async save chain usable ───────────────────────────────────

    /// <summary>
    /// Runs before every <c>SaveFileAsync</c>. Clears a faulted task chain so this save can proceed,
    /// and guarantees the backup-path slot exists so the save cannot fault in the first place.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveManager), "SaveFileAsync")]
    private static void SaveFileAsync_Prefix(SaveManager __instance, int profileSlot, bool saveBackup)
    {
        HealFaultedSaveChain();

        // Account data saves pass -1; only real profile slots index the backup array.
        if (saveBackup && profileSlot >= 0 && profileSlot < ProfileSlotCount)
            EnsureBackupSlot(__instance, profileSlot);
    }

    /// <summary>
    /// Clears <c>SaveManager.m_lastAsyncTask</c> when it has faulted. Without this, the faulted task
    /// is awaited by every subsequent save and rethrown before any write happens, permanently.
    /// </summary>
    internal static void HealFaultedSaveChain()
    {
        try
        {
            if (LastAsyncTaskField?.GetValue(null) is not Task { IsFaulted: true } faulted)
                return;

            // Touch Exception so the fault is observed rather than resurfacing at finalization.
            var detail = faulted.Exception?.Flatten().Message;
            LastAsyncTaskField.SetValue(null, null);
            Plugin.Log.LogWarning(
                $"[APSave] Cleared a faulted save-task chain so saving can resume. Original fault: {detail}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] Could not inspect the save-task chain: {ex.Message}");
        }
    }

    /// <summary>Fills <c>m_backFilePathsArray[slot]</c> when it is null, so a backup write can't NRE.</summary>
    private static void EnsureBackupSlot(SaveManager instance, int profileSlot)
    {
        try
        {
            if (instance == null) return;
            if (BackupFilePathsField?.GetValue(instance) is not List<string>[] slots) return;
            if (profileSlot >= slots.Length || slots[profileSlot] != null) return;

            slots[profileSlot] = new List<string>();
            Plugin.Log.LogWarning(
                $"[APSave] Backup path list for profile {profileSlot} was null; initialized it to " +
                "prevent a save-chain fault.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] Could not initialize the backup path list: {ex.Message}");
        }
    }

    /// <summary>
    /// Fills every null entry in <c>m_backFilePathsArray</c>. Call right after
    /// <see cref="SaveManager.InitializeBackupFilePaths"/>, which leaves a slot null when that
    /// profile's backup directory does not exist.
    /// </summary>
    internal static void EnsureBackupSlots()
    {
        var instance = GetSaveManagerInstance();
        if (instance == null) return;
        for (int i = 0; i < ProfileSlotCount; i++)
            EnsureBackupSlot(instance, i);
    }

    // ── (b) Stop a throwing write from leaking a batch ref-count ──────────────

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SaveFileSystem), nameof(SaveFileSystem.WriteAllBytes))]
    private static void WriteAllBytes_Finalizer(SaveFileSystem.SaveBatch batch, Exception __exception) =>
        ReleaseLeakedRefCount(batch, __exception, nameof(SaveFileSystem.WriteAllBytes));

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SaveFileSystem), nameof(SaveFileSystem.DeleteFile))]
    private static void DeleteFile_Finalizer(SaveFileSystem.SaveBatch batch, Exception __exception) =>
        ReleaseLeakedRefCount(batch, __exception, nameof(SaveFileSystem.DeleteFile));

    /// <summary>
    /// Balances the ref-count that the patched method incremented but never decremented because it
    /// threw. Returning void leaves <paramref name="exception"/> to propagate unchanged - callers
    /// already handle it (<c>CreateSaveFile</c> catches and logs; <c>SaveConfigFile</c> flows into the
    /// finalizer in <see cref="Patches.APSavePatch"/>).
    /// </summary>
    private static void ReleaseLeakedRefCount(
        SaveFileSystem.SaveBatch batch, Exception exception, string origin)
    {
        if (exception == null || batch == null) return;
        try
        {
            batch.DecrementRefCount();
            Plugin.Log.LogError(
                $"[APSave] {origin} threw; released its save-batch ref-count so the save system " +
                $"does not wedge. Error: {exception.Message}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] Could not release the leaked save-batch ref-count: {ex.Message}");
        }
    }

    // ── (c) Keep global preferences out of the per-seed save folder ───────────

    /// <summary>
    /// Controller maps are per-Steam-user, not per-profile, so a redirected path would strand a
    /// player's keybinds inside one seed's folder the moment they remapped anything in an AP session.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveManager), "GetControllerPath")]
    private static void GetControllerPath_Postfix(ref string __result) =>
        __result = APSaveRedirect.StripRedirect(__result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveManager), "GetTrailerPath")]
    private static void GetTrailerPath_Postfix(ref string __result) =>
        __result = APSaveRedirect.StripRedirect(__result);

    // ── (d) Shared reflection helpers ─────────────────────────────────────────

    /// <summary>The <c>SaveManager</c> singleton, or null if the game has not created it yet.</summary>
    internal static SaveManager GetSaveManagerInstance()
    {
        try
        {
            return SaveManagerInstanceField?.GetValue(null) as SaveManager;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[APSave] Could not reach the SaveManager instance: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears SaveManager's one-shot <c>m_checkedForSaveDirectories</c> latch so its own
    /// <c>CreateSaveDirectoriesIfNeeded()</c> rebuilds the profile and backup tree against the
    /// current <see cref="SaveFileSystem.PersistentDataPath"/>. Must be called on every path change.
    /// </summary>
    internal static void ResetSaveDirectoryCheck()
    {
        var instance = GetSaveManagerInstance();
        if (instance == null)
        {
            Plugin.Log.LogWarning(
                "[APSave] Could not access the SaveManager instance to reset the save-directory flag.");
            return;
        }

        try
        {
            CheckedForSaveDirectoriesField?.SetValue(instance, false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] Could not reset the save-directory flag: {ex.Message}");
        }
    }

    /// <summary>
    /// Invokes the game's own <c>CreateSaveDirectoriesIfNeeded()</c>, which creates the base tree,
    /// every <c>Profile{n}/Backups</c> directory, and fills null backup-path slots. Call after
    /// <see cref="ResetSaveDirectoryCheck"/> so the latch does not short-circuit it.
    /// </summary>
    internal static void CreateSaveDirectories()
    {
        if (GetSaveManagerInstance() == null)
        {
            Plugin.Log.LogWarning("[APSave] SaveManager is not available; cannot create save directories.");
            return;
        }

        try
        {
            CreateSaveDirectoriesIfNeededMethod?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] CreateSaveDirectoriesIfNeeded failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Heals a <c>SaveFileSystem.IsSaving</c> that is stuck true because a throwing write leaked a
    /// ref-count. Only acts when the async save chain is demonstrably idle, so it cannot race a real
    /// in-flight save.
    /// </summary>
    internal static void HealStuckSaveState()
    {
        HealFaultedSaveChain();

        try
        {
            if (!SaveFileSystem.IsSaving) return;

            // A live save would leave a pending task behind; if there is none, the count is a leak.
            if (LastAsyncTaskField?.GetValue(null) is Task { IsCompleted: false })
                return;

            var leaked = TotalBatchRefCountField?.GetValue(null);
            TotalBatchRefCountField?.SetValue(null, 0);
            Plugin.Log.LogWarning(
                $"[APSave] SaveFileSystem.IsSaving was stuck true with no save in flight " +
                $"(leaked ref-count {leaked}); reset it to avoid a 30s stall on the next menu entry.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSave] Could not heal the stuck save state: {ex.Message}");
        }
    }
}
