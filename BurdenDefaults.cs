using BepInEx;
using Newtonsoft.Json;
using RL2Archipelago.Locations;
using System;
using System.Collections.Generic;
using System.IO;

namespace RL2Archipelago;

/// <summary>
/// Remembers the burden set the player last chose, machine-wide rather than per-seed, so starting a
/// fresh Archipelago run with the same difficulty is a single confirm instead of 35 button presses.
/// This mirrors <see cref="Plugin.LoadConnectionData"/>, which remembers the last server for the same
/// reason; the file sits next to <c>connection-data.json</c>.
///
/// <para>Burden levels for a run in progress are <em>not</em> stored here. They live in
/// <c>PlayerSaveData.BurdenObjTable</c>, which the per-seed save redirect already scopes correctly.
/// This file is only a seed for the next new run, and is applied exactly once per seed - before the
/// picker opens, never behind the player's back.</para>
///
/// <para>Keys are <see cref="BurdenType"/> <em>names</em>, not their numeric values. The enum is
/// sparse and deliberately spaced (<c>EnemyDamage = 10</c> … <c>UnlockNG = 280</c>), so names keep
/// the file readable and survive a game patch that renumbers it. Unrecognized names are skipped.</para>
/// </summary>
internal static class BurdenDefaults
{
    private static readonly string FilePath = Path.Combine(
        Paths.ConfigPath, "RL2Archipelago", "burden-defaults.json");

    /// <summary>
    /// Writes the remembered levels into the loaded save. Must run before the picker window opens:
    /// <c>NewGamePlusOmniUIWindowController.OnOpen</c> snapshots every level into
    /// <c>m_storedBurdenValues</c>, and anything applied after that point is invisible to the UI.
    /// </summary>
    internal static void ApplyToSave()
    {
        var stored = Read();
        if (stored == null || stored.Count == 0) return;

        int applied = 0;
        foreach (var pair in stored)
        {
            if (!Enum.TryParse<BurdenType>(pair.Key, out var burdenType) || burdenType == BurdenType.None)
            {
                Plugin.Log.LogWarning(
                    $"[AP] Ignoring unknown burden '{pair.Key}' in burden-defaults.json.");
                continue;
            }

            // Disabled burdens (GardenBossUp at time of writing) are hidden by
            // NewGamePlusOmniUIEntry.UpdateActive, so applying one would be live but unseeable.
            var data = BurdenLibrary.GetBurdenData(burdenType);
            if (data == null || data.Disabled) continue;

            // BurdenObj.SetLevel already clamps to [0, MaxLevel], so a stale level from a patch that
            // lowered a cap lands on the new cap instead of being rejected. broadcast: false matches
            // the game's own restore loop - there is no run in progress to refresh.
            BurdenManager.SetBurdenLevel(burdenType, pair.Value, additive: false, broadcast: false);
            applied++;
        }

        Plugin.Log.LogDebug($"[AP] Applied {applied} remembered burden(s) to the new run.");
    }

    /// <summary>
    /// Records the save's current burden levels as the defaults for the next new run. Called every
    /// time the picker closes, including debug-key reopens, so the file always reflects the last
    /// thing the player actually chose.
    /// </summary>
    internal static void CaptureFromSave()
    {
        try
        {
            // Only non-zero levels, so "no burdens" is an empty object rather than 35 zeroes.
            var levels = new Dictionary<string, int>();
            foreach (var burdenType in BurdenType_RL.TypeArray)
            {
                if (burdenType == BurdenType.None) continue;

                int level = BurdenManager.GetBurdenLevel(burdenType);
                if (level > 0) levels[burdenType.ToString()] = level;
            }

            APRunState.WriteTextAtomic(FilePath, JsonConvert.SerializeObject(levels, Formatting.Indented));
            Plugin.Log.LogDebug($"[AP] Remembered {levels.Count} burden(s) for future runs.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to save burden defaults: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the defaults file, or null if it is absent or unusable. A damaged file is reported and
    /// treated as empty rather than allowed to block the picker - the same posture as
    /// <see cref="Plugin.LoadConnectionData"/>, and the next close overwrites it anyway.
    /// </summary>
    private static Dictionary<string, int> Read()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Plugin.Log.LogDebug("[AP] No remembered burden defaults found.");
                return null;
            }

            return JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(FilePath));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to read burden defaults: {ex.Message}");
            return null;
        }
    }
}
