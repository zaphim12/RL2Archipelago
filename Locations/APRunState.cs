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

    /// <summary>
    /// Number of AP items that have been successfully granted. Used to skip
    /// items that were already applied in a previous session. The AP server
    /// replays all items from index 0 on every reconnect when using AllItems.
    /// </summary>
    public int GrantedItemCount { get; set; } = 0;

    /// <summary>
    /// Trap burdens that are currently active for this run. Persisted so traps
    /// can be restored if the player saves and quits mid-run. Cleared on death.
    /// </summary>
    public HashSet<BurdenType> ActiveTraps { get; set; } = new();

    /// <summary>
    /// True once the randomized starting class has been applied to the save via
    /// SkillTreeManager. Prevents redundant re-application on reconnect.
    /// </summary>
    public bool StartingClassApplied { get; set; } = false;

    /// <summary>
    /// True once the Knight Class item has been received from the multiworld.
    /// When false (and RandomizeStartingClass is on), Knight is removed from the
    /// available heir class pool.
    /// </summary>
    public bool KnightClassReceived { get; set; } = false;

    // ── Persistence ──────────────────────────────────────────────────────────

    internal static readonly string RootDir = Path.Combine(Paths.ConfigPath, "RL2Archipelago", "ap-runs");

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
            WriteTextAtomic(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APRunState] Failed to save run state: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes text to <paramref name="path"/> atomically: write to a temp file in the same
    /// directory, then swap it into place in a single filesystem operation. If the process is
    /// killed mid-write (force-close, power loss), the original file is left intact rather than
    /// truncated; a truncated JSON file would fail to deserialize on next load and silently
    /// reset state. Shared by run-state and player-settings persistence.
    /// </summary>
    internal static void WriteTextAtomic(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents);
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
    }
}
