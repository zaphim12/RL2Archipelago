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
    /// Pooled locations the <em>server</em> already considers checked even though this client never
    /// checked them (i.e. released by another player or given via adming command). Their items have
    /// already gone out to their recipients, so if we don't track it, we'll cause players to check
    /// chests or read journals that effectively give nothing.
    ///
    /// <para>Deliberately separate from <see cref="CheckedLocations"/>. That set means "this player
    /// found it", and other systems read it that way: <c>APSaveRedirect.PassesHealthGate</c> treats a
    /// non-empty set as proof this run's save files must exist, which would be wrong for checks the
    /// player never made. Only the pooled categories consult this set, via
    /// <see cref="IsPooledSlotConsumed"/>.</para>
    /// </summary>
    public HashSet<long> RemoteCheckedLocations { get; set; } = new();

    /// <summary>
    /// How many copies of each Archipelago item ID this slot has been sent, keyed by item ID.
    /// This is the run's source of truth for received items and the input to
    /// <see cref="Items.APItemReconciler"/>, which derives an absolute target state from it.
    ///
    /// <para>It replaced an index high-water mark that counted "items already applied". That design
    /// made this file silently authoritative over the game save: if the <c>.rc2dat</c> files ever lost
    /// ground (a crash mid-write, a save tree that was never created), every item below the mark was
    /// skipped forever and the player had no way to get them back. Counts plus absolute targets are
    /// self-healing instead - the server replays everything on each connect, and the reconciler
    /// re-derives what the save should contain.</para>
    /// </summary>
    public Dictionary<long, int> ReceivedItemCounts { get; set; } = new();

    /// <summary>
    /// How many copies of each <em>consumable</em> item have actually been applied. Only gold uses
    /// this: it is spendable, so a granted amount cannot be read back out of the save and compared
    /// the way a manor level or a blueprint count can. Everything else is reconciled against the
    /// game's own state and needs no bookkeeping here.
    /// </summary>
    public Dictionary<long, int> AppliedItemCounts { get; set; } = new();

    /// <summary>
    /// <see cref="SkillTreeType"/> name of the randomized starting class slot, or null. Recorded so
    /// the reconciler can fold the free starting level into that slot's absolute target; otherwise the
    /// first manor item for that slot would compute a target that omits it and regress the class.
    /// </summary>
    public string StartingClassSkill { get; set; }

    /// <summary>
    /// Latched true the first time this seed's profile is seen to have started a game. Lets the
    /// connect-time health gate tell "brand-new seed" apart from "this run's save files have
    /// vanished" - the game cannot, because it probes only <c>Player.rc2dat</c> and treats a missing
    /// set as a fresh profile without reporting anything.
    /// </summary>
    public bool HasStartedGameObserved { get; set; } = false;

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

    /// <summary>
    /// True once the final boss has been defeated. Persisted so a goal achieved while
    /// disconnected is re-sent to the server on the next reconnect.
    /// </summary>
    public bool GoalAchieved { get; set; } = false;

    /// <summary>
    /// True once the player has been shown the burden picker for this seed and confirmed their choices. 
    /// Gates the one automatic open, so reconnecting never re-prompts.
    /// </summary>
    public bool BurdensChosen { get; set; } = false;

    // ── Ledger accessors ─────────────────────────────────────────────────────

    /// <summary>Copies of <paramref name="itemId"/> the multiworld has sent this slot.</summary>
    public int ReceivedCount(long itemId) =>
        ReceivedItemCounts.TryGetValue(itemId, out var count) ? count : 0;

    /// <summary>Copies of a consumable <paramref name="itemId"/> already applied to the save.</summary>
    public int AppliedCount(long itemId) =>
        AppliedItemCounts.TryGetValue(itemId, out var count) ? count : 0;

    /// <summary>
    /// True when a pooled slot must not be handed out again: either this player checked it, or the
    /// server already did. Every non-pooled location category is keyed off
    /// <see cref="CheckedLocations"/> alone, because those checks are tied to a fixed in-world event
    /// rather than drawn from a pool at pickup time.
    /// </summary>
    public bool IsPooledSlotConsumed(long locationId) =>
        CheckedLocations.Contains(locationId) || RemoteCheckedLocations.Contains(locationId);

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
                    // Log the ledger alongside the checks: when a player reports missing items, the
                    // first question is whether the run state knows about them at all.
                    Plugin.Log.LogInfo(
                        $"[APRunState] Loaded {state.CheckedLocations.Count} checked location(s) and " +
                        $"{state.ReceivedItemCounts.Count} received item type(s) from {path}");
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
