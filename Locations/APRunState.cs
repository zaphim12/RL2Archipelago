using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace RL2Archipelago.Locations;

/// <summary>
/// Per-seed persistent state for an Archipelago run: which locations the player
/// has checked, plus any future run-scoped bookkeeping.
///
/// The file lives alongside (but separate from) the game's own save data so we
/// can recover gracefully if a location check was submitted but the server
/// never acknowledged it (network drop, crash, etc.). On reconnect we diff
/// against <c>Session.Locations.AllLocationsChecked</c> and re-send anything
/// the server doesn't know about.
/// </summary>
public class APRunState
{
    /// <summary>Set of Archipelago location IDs the player has checked in this run.</summary>
    public HashSet<long> CheckedLocations { get; set; } = new();

    // ── Persistence ──────────────────────────────────────────────────────────

    private static readonly string RootDir = Path.Combine(Paths.ConfigPath, "RL2Archipelago", "ap-runs");

    private static string FilePathFor(string saveDirectoryName) =>
        Path.Combine(RootDir, saveDirectoryName, "run-state.json");

    /// <summary>
    /// Loads the run state for the given AP save directory name (typically
    /// <c>{RoomId}_{SlotName}</c>). Returns a fresh empty state if no file exists.
    /// </summary>
    public static APRunState Load(string saveDirectoryName)
    {
        var path = FilePathFor(saveDirectoryName);
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var state = JsonConvert.DeserializeObject<APRunState>(json);
                if (state != null)
                {
                    Plugin.Log.LogInfo(
                        $"[APRunState] Loaded {state.CheckedLocations.Count} checked locations from {path}");
                    return state;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APRunState] Failed to load run state: {ex.Message}");
        }

        return new APRunState();
    }

    public void Save(string saveDirectoryName)
    {
        var path = FilePathFor(saveDirectoryName);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APRunState] Failed to save run state: {ex.Message}");
        }
    }
}
