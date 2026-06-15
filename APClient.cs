using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RL2Archipelago.Items;
using RL2Archipelago.Locations;
using RL2Archipelago.Patches;
using RL2Archipelago.Traps;
using RL2Archipelago.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RL2Archipelago;

public enum JournalChecksMode { Disabled = 0, Individual = 1, Grouped = 2 }

/// <summary>
/// Manages the Archipelago session lifecycle: connect, disconnect, item/message
/// event handling, and thread-safe main-thread dispatch.
/// </summary>
public static class APClient
{
    public static ArchipelagoSession Session { get; private set; }

    /// <summary>A list of options determined by the yaml which can modify the AP client's behavior. E.g. death_link, completion_criteria, etc.</summary>
    public static Dictionary<string, object> SlotData { get; private set; }

    public static bool IsConnected => Session?.Socket?.Connected ?? false;

    /// <summary>True while an AP session's save directory is active; controls the SaveFileSystem path redirect.</summary>
    public static bool APSaveActive { get; private set; }

    /// <summary>Sanitized "{RoomId}_{SlotName}" used as the save subdirectory name.</summary>
    public static string APSaveDirectoryName { get; private set; }

    /// <summary>Persistent state for the active run: checked locations, received items, etc.</summary>
    public static APRunState RunState { get; private set; }

    /// <summary>Levels granted per manor upgrade item received. Read from slot data on connect.</summary>
    public static int ManorUpgradeBundleSize { get; private set; } = 5;

    /// <summary>Controls whether journal/memory reads generate location checks. Read from slot data on connect.</summary>
    public static JournalChecksMode JournalChecksMode { get; private set; } = JournalChecksMode.Grouped;

    /// <summary>Percent chance (1–100) a bronze chest triggers an AP check. Read from slot data on connect.</summary>
    public static int BronzeChestApChance { get; private set; } = 15;

    /// <summary>Percent chance (1–100) a silver chest triggers an AP check. Read from slot data on connect.</summary>
    public static int SilverChestApChance { get; private set; } = 99;

    /// <summary>Percent chance (1–100) a fairy chest triggers an AP check. Read from slot data on connect.</summary>
    public static int FairyChestApChance { get; private set; } = 100;

    /// <summary>True once the player has left the main menu and entered an active run. Item processing is gated on this flag.</summary>
    public static bool IsInGame { get; internal set; } = false;

    /// <summary>True when death_link is enabled for this slot.</summary>
    public static bool DeathLinkEnabled { get; private set; }

    /// <summary>True when the randomize_starting_class option is on for this seed.</summary>
    public static bool RandomizeStartingClass { get; private set; } = false;

    /// <summary>
    /// Index of the chosen starting class: 0 = Knight (vanilla), 1–14 = one of the 14
    /// non-Knight classes in ManorSlots[44]–ManorSlots[57] order.
    /// </summary>
    public static int StartingClassIndex { get; private set; } = 0;

    /// <summary>
    /// True once the Knight Class item has been received from the multiworld.
    /// When false (and RandomizeStartingClass is on), Knight is excluded from available heirs.
    /// </summary>
    public static bool KnightClassReceived { get; private set; } = false;

    /// <summary>
    /// Pre-computed gold costs for each manor upgrade slot, keyed by <see cref="SkillTreeType"/>.
    /// Populated from slot data on connect. Empty when not connected.
    /// </summary>
    public static IReadOnlyDictionary<SkillTreeType, int> ManorUpgradeCosts { get; private set; }
        = new Dictionary<SkillTreeType, int>();

    /// <summary>True while we are applying an incoming death beacon, so the death patch skips sending an echo.</summary>
    internal static bool IsReceivingDeathLink { get; private set; }

    private static DeathLinkService _deathLinkService;
    private static string _slotName;

    // Incoming death beacon queued for main-thread application. Written by the websocket thread,
    // read by the main thread; volatile ensures the flag write is visible after the string fields.
    private static volatile bool _pendingDeathLink;
    private static string _pendingDeathSource;
    private static string _pendingDeathCause;

    // Profile slot that was active before AP mode was entered; restored on disconnect.
    private static byte _previousProfile;

    /// <summary>Fired on the main thread after a successful login.</summary>
    public static event Action<ArchipelagoSession> OnSessionOpened;

    /// <summary>Fired on the main thread when a session is manually closed or the application is closed.</summary>
    public static event Action OnSessionClosed;

    // Items received on the AP websocket thread; drained each Update() tick on
    // the Unity main thread so game-state mutations are thread-safe. The display
    // fields are captured here (not at grant time) because they're sourced from
    // the ItemInfo we dequeue, which lives on the websocket thread.
    private struct PendingItem
    {
        public int Index;
        public long ItemId;
        public string ItemDisplayName;
        public int SourceSlot;
        public string SourcePlayerName;
    }
    private static readonly ConcurrentQueue<PendingItem> _pendingItems = new();

    // Scouted item info keyed by location ID. Populated asynchronously after login so
    // in-world graphics (e.g. heirloom pedestals) can show what item will drop at a
    // given location. Cleared on disconnect.
    private static readonly ConcurrentDictionary<long, ScoutedItemInfo> _scoutedItems = new();

    // Tracks the next item index to assign. Reset to 0 on each connect because
    // AllItems causes the server to replay from index 0 on every reconnect.
    // Tracked locally because the server's index stores the total number of items,
    // not the index of the item's being received. So when receiving items after a reconnect,
    // the server's index will be higher than the index of the item being received until we catch up.
    private static int _nextItemIndex = 0;

    // Trap state lives only in memory, so it's lost on game restart. This flag
    // gates a one-time restore of RunState.ActiveTraps on the first ProcessPendingItems
    // tick after entering a game; reset on each Connect so a fresh session restores correctly.
    private static bool _trapsRestored = false;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to connect and login to the Archipelago server described by
    /// <paramref name="connData"/>.  Both callbacks are invoked on the calling
    /// thread (Unity main thread when triggered from UI code).
    /// </summary>
    public static void Connect(
        APConnectionData connData,
        Action onSuccess,
        Action<string> onFailure)
    {
        Plugin.Log.LogInfo(
            $"Connecting to AP server {connData.Hostname}:{connData.Port} " +
            $"as slot \"{connData.SlotName}\"");

        try
        {
            // Tear down any existing session cleanly before creating a new one.
            if (IsConnected)
                Disconnect(manual: false);

            Session = ArchipelagoSessionFactory.CreateSession(connData.Hostname, connData.Port);

            // Hook diagnostics before TryConnectAndLogin so we capture events that
            // fire during the handshake
            Session.Socket.SocketOpened    += APSession_SocketOpened;
            Session.Socket.ErrorReceived   += APSession_ErrorReceived;

            Plugin.Log.LogDebug("[AP] Calling TryConnectAndLogin...");

            LoginResult loginResult = Session.TryConnectAndLogin(
                "Rogue Legacy 2",
                connData.SlotName,
                ItemsHandlingFlags.AllItems,
                password: string.IsNullOrEmpty(connData.Password) ? null : connData.Password,
                requestSlotData: true);

            Plugin.Log.LogDebug($"[AP] TryConnectAndLogin returned. Successful={loginResult.Successful}");

            Session.Socket.SocketOpened -= APSession_SocketOpened;

            if (!loginResult.Successful)
            {
                LoginFailure failure = (LoginFailure)loginResult;
                var errors = string.Join("\n", failure.Errors);
                Plugin.Log.LogError($"AP login failed:\n{errors}");
                Session = null;
                onFailure?.Invoke(errors);
                return;
            }

            var success = (LoginSuccessful)loginResult;
            SlotData = success.SlotData;

            if (SlotData.TryGetValue("blueprint_checks_per_biome", out var bpCountObj))
                LocationRegistry.SetBlueprintChecksPerBiome(Convert.ToInt32(bpCountObj));
            else
                LocationRegistry.SetBlueprintChecksPerBiome(11);

            if (SlotData.TryGetValue("rune_checks_per_biome", out var runeCountObj))
                LocationRegistry.SetRuneChecksPerBiome(Convert.ToInt32(runeCountObj));
            else
                LocationRegistry.SetRuneChecksPerBiome(4);

            BronzeChestApChance = SlotData.TryGetValue("bronze_chest_ap_chance", out var bcObj)
                ? Convert.ToInt32(bcObj) : 15;
            SilverChestApChance = SlotData.TryGetValue("silver_chest_ap_chance", out var scObj)
                ? Convert.ToInt32(scObj) : 99;
            FairyChestApChance = SlotData.TryGetValue("fairy_chest_ap_chance", out var fcObj)
                ? Convert.ToInt32(fcObj) : 100;

            ManorUpgradeBundleSize = SlotData.TryGetValue("manor_upgrade_bundle_size", out var bundleSizeObj)
                ? Convert.ToInt32(bundleSizeObj) : 5;

            JournalChecksMode = SlotData.TryGetValue("journal_checks", out var jObj)
                ? (JournalChecksMode)Convert.ToInt32(jObj) : JournalChecksMode.Grouped;

            _slotName = connData.SlotName;
            bool serverDeathLink = SlotData.TryGetValue("death_link", out var dlObj) && Convert.ToInt32(dlObj) != 0;

            if (SlotData.TryGetValue("manor_upgrade_costs", out var costsObj) && costsObj is JArray costsArray)
            {
                var types = ItemRegistry.SkillTreeTypes;
                var map = new Dictionary<SkillTreeType, int>(types.Count);
                int i = 0;
                foreach (var token in costsArray)
                {
                    if (i >= types.Count) break;
                    map[types[i++]] = token.Value<int>();
                }
                ManorUpgradeCosts = map;
            }

            Plugin.Log.LogInfo(
                $"Connected! Room: {Session.RoomState.Seed}  " +
                $"Slot data keys: {string.Join(", ", success.SlotData.Keys)}");

            // Persist the room ID so we can warn on multiworld mismatch later.
            connData.RoomId = Session.RoomState.Seed;

            // Redirect all save I/O to a directory scoped to this room + slot.
            _previousProfile = SaveManager.ConfigData.CurrentProfile;
            APSaveDirectoryName = SanitizeDirectoryName($"{connData.RoomId}_{connData.SlotName}");
            APSaveActive = true;
            SaveManager.ConfigData.CurrentProfile = 0;
            EnsureRedirectedSaveDirectories();
            SaveManager.LoadCurrentProfileData();
            Plugin.Log.LogInfo($"[AP] Save redirected to AP_Saves/{APSaveDirectoryName}");

            // Load any prior run state (checked locations, etc.) for this seed+slot.
            RunState = APRunState.Load(APSaveDirectoryName);
            _nextItemIndex = 0;
            _trapsRestored = false;

            RandomizeStartingClass = SlotData.TryGetValue("randomize_starting_class", out var rscObj)
                && Convert.ToInt32(rscObj) != 0;
            StartingClassIndex = SlotData.TryGetValue("starting_class_index", out var sciObj)
                ? Convert.ToInt32(sciObj) : 0;
            KnightClassReceived = RunState.KnightClassReceived;

            // Load per-seed player settings; user override wins over server value.
            APSettings.DeathLink = null;
            LoadPlayerSettings(APSaveDirectoryName);
            DeathLinkEnabled = APSettings.DeathLink ?? serverDeathLink;

            _deathLinkService = Session.CreateDeathLinkService();
            if (DeathLinkEnabled)
            {
                _deathLinkService.OnDeathLinkReceived += APSession_DeathLinkReceived;
                _deathLinkService.EnableDeathLink();
                Plugin.Log.LogInfo("[AP] DeathLink enabled.");
            }

            // Register websocket-thread event handlers AFTER resetting _nextItemIndex so
            // any items that arrive concurrently get correct indices. Then manually drain
            // the helper to pick up items the server sent during TryConnectAndLogin before
            // the handler was wired; those sit in the library's buffer unfired.
            Session.Items.ItemReceived += APSession_ItemReceived;
            Session.MessageLog.OnMessageReceived += APSession_OnMessageReceived;
            APSession_ItemReceived(Session.Items);

            // Reconcile local and server state. If the client recorded a check
            // that never made it to the server (e.g. network drop mid-send),
            // resend it now so the multiworld stays consistent.
            ResyncCheckedLocations();

            // Scout every unchecked tracked location so in-world graphics
            // (e.g. heirloom pedestals) can show the item that will drop there.
            ScoutTrackedLocations();

            // Fire the session-opened event and the caller's success callback on
            // the main thread.  We're already on the main thread here (called from UI),
            // so invoke directly.
            OnSessionOpened?.Invoke(Session);
            onSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"AP connection threw an exception:\n{ex.Message}\n{ex.StackTrace}");
            Session = null;
            onFailure?.Invoke(ex.Message);
        }
    }

    /// <summary>Tears down the current session and fires <see cref="OnSessionClosed"/>.</summary>
    public static void Disconnect(bool manual = true)
    {
        if (Session == null) return;

        Session.Items.ItemReceived -= APSession_ItemReceived;
        Session.MessageLog.OnMessageReceived -= APSession_OnMessageReceived;
        Session.Socket.ErrorReceived -= APSession_ErrorReceived;

        if (Session.Socket.Connected)
            Session.Socket.DisconnectAsync().Wait(2000);

        if (_deathLinkService is not null)
        {
            _deathLinkService.OnDeathLinkReceived -= APSession_DeathLinkReceived;
            _deathLinkService = null;
        }
        DeathLinkEnabled = false;
        _pendingDeathLink = false;
        RandomizeStartingClass = false;
        StartingClassIndex = 0;
        KnightClassReceived = false;

        // Deactivate the save redirect before restoring the vanilla profile so
        // LoadCurrentProfileData reads from the original paths.
        APSaveActive = false;
        SaveManager.ConfigData.CurrentProfile = _previousProfile;
        // Force the game to re-verify its save directories against the now-restored vanilla path
        // on the next save, mirroring what we do on connect. Guards against the reverse of the
        // AP-save-directory bug (solved via EnsureRedirectedSaveDirectories())
        // for a session that connected before any vanilla save occurred.
        ResetSaveDirectoryCheck();
        SaveManager.LoadCurrentProfileData();
        Plugin.Log.LogInfo("[AP] Save redirect deactivated; vanilla profile restored.");

        APNotifications.Reset();
        _scoutedItems.Clear();
        RunState = null;
        IsInGame = false;
        Session = null;

        if (manual)
            OnSessionClosed?.Invoke();

        Plugin.Log.LogInfo("Disconnected from AP server.");
    }

    /// <summary>
    /// Replaces invalid filename chars in the given name with underscores. TODO: Determine if this is necessary
    /// </summary>
    /// <param name="name">Directory name which will be sanitized</param>
    /// <returns>The sanitized directory name</returns>
    private static string SanitizeDirectoryName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    /// <summary>
    /// Ensures the game's save directory tree exists under the active AP save redirect.
    ///
    /// RL2 builds its save directories only once per session, guarded by SaveManager's private
    /// <c>m_checkedForSaveDirectories</c> flag. If anything triggered a save before the player
    /// connected (an autosave, or changing a config option), that flag is consumed against the
    /// vanilla save path. Once we redirect <see cref="SaveFileSystem.PersistentDataPath"/> to
    /// <c>AP_Saves/{dir}</c>, the game then never creates that redirected tree:
    ///   - game saves (<c>SaveGameData</c>) fail silently, because <c>CreateSaveFile</c> swallows
    ///     the IO exception, so the player's run progress quietly never reaches disk; and
    ///   - <c>SaveConfigFile</c> re-throws on menu close, and that exception propagates out of
    ///     <c>SuboptionsWindowController.OnClose</c> before the window is removed, leaving the
    ///     options submenu permanently unclosable (force-close is the only escape, losing the
    ///     unsaved run).
    ///
    /// We fix both by (1) creating the config directory immediately so the very next config save
    /// can't throw, and (2) clearing the one-time flag so the game rebuilds its full profile and
    /// backup tree under the redirected path on the next save.
    /// </summary>
    private static void EnsureRedirectedSaveDirectories()
    {
        try
        {
            // (1) Immediately create the directory GameConfig.ini lives in so that backing out of
            //     a settings submenu (which calls SaveConfigFile) can't throw before any other
            //     save has had a chance to create it.
            var configDir = Path.GetDirectoryName(SaveManager.GetConfigPath());
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            // (2) Clear SaveManager's one-time directory-check flag so its own
            //     CreateSaveDirectoriesIfNeeded() rebuilds the full profile/backup tree under the
            //     redirected path on the next save. This also restores reliable run saves.
            ResetSaveDirectoryCheck();

            Plugin.Log.LogInfo("[AP] Ensured redirected save directories exist.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to ensure redirected save directories: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears SaveManager's private <c>m_checkedForSaveDirectories</c> flag so the game rebuilds
    /// its save directory tree against the current <see cref="SaveFileSystem.PersistentDataPath"/>
    /// on the next save. Called whenever the save path changes (connect and disconnect) so neither
    /// the redirected AP tree nor the vanilla tree can be left uncreated for the active path.
    /// </summary>
    private static void ResetSaveDirectoryCheck()
    {
        var instance = Traverse.Create(typeof(SaveManager)).Field("m_instance").GetValue();
        if (instance != null)
            Traverse.Create(instance).Field("m_checkedForSaveDirectories").SetValue(false);
        else
            Plugin.Log.LogWarning("[AP] Could not access SaveManager instance to reset the save-directory flag.");
    }

    /// <summary>
    /// Reports a completed location check to the Archipelago server.
    ///
    /// Persists the check to <see cref="RunState"/> before sending so that if
    /// the network send is lost, the next successful connect will resync it.
    /// Repeated calls for the same location ID are no-ops.
    /// </summary>
    public static void SendLocationCheck(long locationId)
    {
        if (RunState == null)
        {
            Plugin.Log.LogWarning(
                $"[AP] SendLocationCheck({locationId}) called while no run is active. ignoring.");
            return;
        }

        var displayName = LocationRegistry.Names.TryGetValue(locationId, out var n) ? n : locationId.ToString();

        // Persist first, then send. If the send is dropped, Resync will retry it.
        if (!RunState.CheckedLocations.Add(locationId))
        {
            Plugin.Log.LogDebug($"[AP] Location '{displayName}' already checked. skipping re-send.");
            return;
        }
        RunState.Save(APSaveDirectoryName);

        if (!IsConnected)
        {
            Plugin.Log.LogInfo(
                $"[AP] Not connected; queued location '{displayName}' for resync on next connect.");
            return;
        }

        if (!Session.Locations.AllLocations.Contains(locationId))
            Plugin.Log.LogWarning(
                $"[AP] Location '{displayName}' (ID {locationId}) is not in this slot's location list. ensure the apworld defines it.");

        try
        {
            Session.Locations.CompleteLocationChecksAsync(new[] { locationId })
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Plugin.Log.LogError($"[AP] Failed to send location check '{displayName}': {t.Exception?.Flatten().Message}");
                    else
                        Plugin.Log.LogInfo($"[AP] Sent location check: '{displayName}' (ID {locationId})");
                });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(
                $"[AP] Failed to send location check '{displayName}': {ex.Message}");
        }

        EnqueueSendNotification(locationId);
    }

    /// <summary>
    /// Pushes the "you sent X to Y" (or "you discovered your own item") HUD,
    /// using the scout reply cached at connect time. Silently no-ops when the
    /// scout hasn't returned yet; the message log still shows the activity.
    /// </summary>
    private static void EnqueueSendNotification(long locationId)
    {
        if (!_scoutedItems.TryGetValue(locationId, out var scouted))
            return;

        var itemName = scouted.ItemDisplayName ?? scouted.ItemName ?? "an item";
        var ourSlot = Session.ConnectionInfo.Slot;
        var isHeirloom = ItemRegistry.ToHeirloomType(scouted.ItemId).HasValue;

        if (scouted.Player.Slot == ourSlot)
        {
            // Self-item: GrantItem will skip its own notification so this is the
            // only HUD that fires for this check.
            APNotifications.Enqueue(
                title: "Item Found",
                subtitle: itemName,
                description: "Discovered by yourself",
                critical: isHeirloom);
        }
        else
        {
            var recipient = !string.IsNullOrEmpty(scouted.Player.Alias)
                ? scouted.Player.Alias
                : (scouted.Player.Name ?? $"Player {scouted.Player.Slot}");
            APNotifications.Enqueue(
                title: "Item Sent",
                subtitle: itemName,
                description: $"Sent to {recipient}",
                critical: isHeirloom);
        }
    }

    /// <summary>
    /// Returns the item that will drop at <paramref name="locationId"/>, or
    /// <c>null</c> if the scout hasn't come back yet or the location isn't tracked.
    /// Scouts are requested asynchronously right after a successful connect.
    /// </summary>
    public static ScoutedItemInfo GetScoutedItem(long locationId) =>
        _scoutedItems.TryGetValue(locationId, out var info) ? info : null;

    /// <summary>
    /// Asynchronously scouts every location in <see cref="LocationRegistry.Names"/>
    /// and caches the result in <see cref="_scoutedItems"/>. Consumers (e.g. the
    /// heirloom-statue icon swap) should handle the cache being empty if the
    /// player enters the area before the scout reply arrives.
    /// </summary>
    private static void ScoutTrackedLocations()
    {
        if (Session == null) return;

        // Only scout locations the server actually knows about (AllLocations is
        // the slot's location list). Filtering here avoids a warning from the
        // library when an ID isn't in this slot.
        var ids = LocationRegistry.Names.Keys
            .Where(Session.Locations.AllLocations.Contains)
            .ToArray();

        if (ids.Length == 0) return;

        try
        {
            Session.Locations.ScoutLocationsAsync(HintCreationPolicy.None, ids)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Plugin.Log.LogError($"[AP] Scout request failed: {t.Exception?.Flatten().Message}");
                        return;
                    }
                    foreach (var kv in t.Result)
                        _scoutedItems[kv.Key] = kv.Value;
                    Plugin.Log.LogInfo($"[AP] Scouted {t.Result.Count} tracked location(s).");
                });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to issue scout request: {ex.Message}");
        }
    }

    /// <summary>
    /// Re-sends any locally-checked locations that the server doesn't know about.
    /// Called once after a successful connect/login.
    /// </summary>
    private static void ResyncCheckedLocations()
    {
        if (RunState == null || Session == null) return;

        var serverKnown = Session.Locations.AllLocationsChecked;
        var missing = RunState.CheckedLocations.Where(id => !serverKnown.Contains(id)).ToArray();

        if (missing.Length == 0)
        {
            Plugin.Log.LogDebug("[AP] Checked-location state is already in sync with the server.");
            return;
        }

        Plugin.Log.LogInfo(
            $"[AP] Resyncing {missing.Length} location(s) the server hadn't recorded yet.");
        try
        {
            Session.Locations.CompleteLocationChecksAsync(missing);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Resync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Notifies the Archipelago server that the player has achieved the goal condition.
    /// Call this instead of <see cref="SendLocationCheck"/> when the final boss is defeated.
    /// </summary>
    public static void SendGoalAchieved()
    {
        if (!IsConnected) return;
        try
        {
            Session.SetGoalAchieved();
            Plugin.Log.LogInfo("[AP] Goal achieved. sent status update to server.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to send goal achieved: {ex.Message}");
        }
    }

    /// <summary>Sends a death beacon to all other DeathLink-enabled players in the multiworld.</summary>
    public static void SendDeathLink()
    {
        if (_deathLinkService is null) return;
        try
        {
            _deathLinkService.SendDeathLink(new DeathLink(_slotName));
            Plugin.Log.LogInfo("[AP] DeathLink sent.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to send DeathLink: {ex.Message}");
        }
    }

    /// <summary>
    /// Called each frame from the main thread to apply any incoming death beacon.
    /// Drops the beacon silently if the player is already dead or not yet in-game.
    /// </summary>
    public static void ProcessPendingDeaths()
    {
        if (!_pendingDeathLink || !IsInGame) return;
        if (!WorldBuilder.IsInstantiated || WorldBuilder.State != BiomeBuildStateID.Complete)
        {
            _pendingDeathLink = false;
            return;
        }
        if (!PlayerManager.IsInstantiated) return;

        var player = PlayerManager.GetPlayerController();
        if (player == null || player.IsDead)
        {
            _pendingDeathLink = false;
            return;
        }

        if (PlayerManager.GetCurrentPlayerRoom()?.BiomeType == BiomeType.HubTown)
        {
            _pendingDeathLink = false;
            return;
        }

        _pendingDeathLink = false;

        var source = _pendingDeathSource ?? "Another player";
        var cause = !string.IsNullOrEmpty(_pendingDeathCause) ? _pendingDeathCause : $"{source} has died";

        IsReceivingDeathLink = true;
        try
        {
            player.KillCharacter(null, broadcastEvent: true);
        }
        finally
        {
            IsReceivingDeathLink = false;
        }

        APNotifications.Enqueue(
            title: "Death Link!",
            subtitle: source,
            description: cause,
            critical: true);
    }

    /// <summary>
    /// Grants the randomized starting class unlock to the save on the first game
    /// entry for this seed. Only called when <see cref="RandomizeStartingClass"/> is
    /// true and <see cref="APRunState.StartingClassApplied"/> is not yet set.
    /// </summary>
    internal static void ApplyStartingClass()
    {
        if (StartingClassIndex <= 0) // 0 = Knight - already always available
        {
            RunState.StartingClassApplied = true;
            RunState.Save(APSaveDirectoryName);
            return;
        }

        // ManorSlots[44] = index 1 (Boxer), …, ManorSlots[57] = index 14 (Dragon Lancer).
        var slot = GameConstants.ManorSlots[43 + StartingClassIndex];
        SkillTreeManager.SetSkillObjLevel(slot.SkillTree, ManorUpgradeBundleSize, additive: false, runEvents: false);
        Plugin.Log.LogInfo($"[AP] Starting class applied: {slot.ItemName}");
        RunState.StartingClassApplied = true;
        RunState.Save(APSaveDirectoryName);
    }

    /// <summary>
    /// Called from <see cref="Plugin.Update"/> each frame to drain any item IDs
    /// received on the AP websocket thread and apply them to game state.
    /// </summary>
    public static void ProcessPendingItems()
    {
        if (!IsInGame) return;

        if (!_trapsRestored && RunState != null)
        {
            foreach (var trap in RunState.ActiveTraps)
                TrapManager.ActivateTrap(trap);
            _trapsRestored = true;
        }

        if (RandomizeStartingClass && RunState != null && !RunState.StartingClassApplied)
            ApplyStartingClass();

        while (_pendingItems.TryDequeue(out var pending))
        {
            if (RunState != null && pending.Index < RunState.GrantedItemCount)
            {
                var skippedName = ItemRegistry.Names.TryGetValue(pending.ItemId, out var sn) ? sn : pending.ItemId.ToString();
                Plugin.Log.LogDebug($"[AP] Skipping already-granted item '{skippedName}' at index {pending.Index}");
                continue;
            }

            try
            {
                GrantItem(pending.ItemId);
                EnqueueReceiveNotification(pending);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AP] Exception granting item {pending.ItemId}: {ex.Message}\n{ex.StackTrace}");
            }

            if (RunState != null)
            {
                RunState.GrantedItemCount = Math.Max(RunState.GrantedItemCount, pending.Index + 1);
                RunState.Save(APSaveDirectoryName);
            }
        }
    }

    /// <summary>
    /// Pushes the "you received X from Y" HUD. Skipped for self-source items
    /// because <see cref="EnqueueSendNotification"/> already showed the
    /// "you discovered your own item" HUD at send time.
    /// </summary>
    private static void EnqueueReceiveNotification(PendingItem pending)
    {
        if (Session != null && pending.SourceSlot == Session.ConnectionInfo.Slot)
            return;

        var itemName = !string.IsNullOrEmpty(pending.ItemDisplayName)
            ? pending.ItemDisplayName
            : (ItemRegistry.Names.TryGetValue(pending.ItemId, out var n) ? n : pending.ItemId.ToString());
        var sender = !string.IsNullOrEmpty(pending.SourcePlayerName)
            ? pending.SourcePlayerName
            : $"Player {pending.SourceSlot}";

        bool isTrap = ItemRegistry.ToTrapBurdenType(pending.ItemId).HasValue;
        APNotifications.Enqueue(
            title: isTrap ? "Trap!" : "Item Received",
            subtitle: itemName,
            description: $"Sent by {sender}",
            critical: isTrap || ItemRegistry.ToHeirloomType(pending.ItemId).HasValue);
    }

    private static void GrantItem(long itemId)
    {
        var displayName = ItemRegistry.Names.TryGetValue(itemId, out var n) ? n : itemId.ToString();

        var heirloomType = ItemRegistry.ToHeirloomType(itemId);
        if (heirloomType.HasValue)
        {
            SaveManager.PlayerSaveData.SetHeirloomLevel(heirloomType.Value, 1, additive: false, broadcast: false);
            PlayerManager.GetPlayerController()?.InitializeAbilities();
            Plugin.Log.LogInfo($"[AP] Granted heirloom: {displayName}");
            return;
        }

        var equipBlueprint = ItemRegistry.ToEquipmentBlueprint(itemId);
        if (equipBlueprint.HasValue)
        {
            var (cat, equip) = equipBlueprint.Value;
            if (EquipmentManager.GetFoundState(cat, equip) == FoundState.NotFound)
                EquipmentManager.SetFoundState(cat, equip, FoundState.FoundButNotViewed, overrideValues: false);
            else
                EquipmentManager.SetUpgradeBlueprintsFound(cat, equip, 1, additive: true);
            Plugin.Log.LogInfo($"[AP] Granted blueprint: {displayName}");
            return;
        }

        var runeType = ItemRegistry.ToRuneType(itemId);
        if (runeType.HasValue)
        {
            RuneManager.SetUpgradeBlueprintsFound(runeType.Value, 1, additive: true);
            Plugin.Log.LogInfo($"[AP] Granted rune blueprint: {displayName}");
            return;
        }

        if (itemId == ItemRegistry.KnightClassItem)
        {
            RunState.KnightClassReceived = true;
            KnightClassReceived = true;
            RunState.Save(APSaveDirectoryName);
            Plugin.Log.LogInfo($"[AP] Knight Class unlocked.");
            return;
        }

        var skillTreeType = ItemRegistry.ToSkillTreeType(itemId);
        if (skillTreeType.HasValue)
        {
            // 'runEvents: false' prevents UnlockConnectedSkillSlots from revealing adjacent
            // manor slots prematurely. Adjacent slots should only become visible when the
            // player physically purchases the prerequisite slot in the manor (location check),
            // not when the corresponding AP item arrives from the multiworld.
            SkillTreeManager.SetSkillObjLevel(skillTreeType.Value, ManorUpgradeBundleSize, additive: true, runEvents: false);
            Plugin.Log.LogInfo($"[AP] Granted manor upgrade: {displayName} (+{ManorUpgradeBundleSize} levels)");
            return;
        }

        var teleporterBiome = ItemRegistry.ToTeleporterBiomeType(itemId);
        if (teleporterBiome.HasValue)
        {
            SaveManager.PlayerSaveData.SetTeleporterIsUnlocked(teleporterBiome.Value, state: true);
            Plugin.Log.LogInfo($"[AP] Granted teleporter unlock: {displayName}");
            return;
        }

        if (itemId == ItemRegistry.GoldCoins)
        {
            GoldCoinsPatch.Grant();
            return;
        }

        var trapBurden = ItemRegistry.ToTrapBurdenType(itemId);
        if (trapBurden.HasValue)
        {
            TrapManager.ActivateTrap(trapBurden.Value);
            RunState?.ActiveTraps.Add(trapBurden.Value);
            Plugin.Log.LogInfo($"[AP] Activated trap: {displayName}");
            return;
        }

        Plugin.Log.LogWarning($"[AP] No handler for item '{displayName}' (ID {itemId}). ignoring.");
    }

    // ── Player settings (per-seed persistence) ───────────────────────────────

    private struct PlayerSettingsData
    {
        public bool? DeathLink   { get; set; }
        public bool? UseMinimalistAPLog  { get; set; }
    }

    private static string PlayerSettingsPath(string saveDirectoryName) =>
        Path.Combine(Locations.APRunState.RootDir, saveDirectoryName, "player-settings.json");

    private static void LoadPlayerSettings(string saveDirectoryName)
    {
        var path = PlayerSettingsPath(saveDirectoryName);
        try
        {
            if (!File.Exists(path)) return;
            var data = JsonConvert.DeserializeObject<PlayerSettingsData>(File.ReadAllText(path));
            APSettings.DeathLink   = data.DeathLink;
            APSettings.UseMinimalistAPLog  = data.UseMinimalistAPLog ?? false;
            Plugin.Log.LogInfo($"[AP] Player settings loaded (DeathLink={APSettings.DeathLink}, UseMinimalistAPLog={APSettings.UseMinimalistAPLog}).");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to load player settings: {ex.Message}");
        }
    }

    internal static void SavePlayerSettings()
    {
        if (string.IsNullOrEmpty(APSaveDirectoryName)) return;
        var path = PlayerSettingsPath(APSaveDirectoryName);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var data = new PlayerSettingsData { DeathLink = APSettings.DeathLink, UseMinimalistAPLog = APSettings.UseMinimalistAPLog };
            File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
            Plugin.Log.LogDebug("[AP] Player settings saved.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to save player settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies a runtime death-link toggle change. Safe to call when disconnected (no-op on the service).
    /// </summary>
    public static void ApplyDeathLinkSetting(bool enabled)
    {
        DeathLinkEnabled = enabled;
        if (_deathLinkService == null) return;

        if (enabled)
        {
            _deathLinkService.OnDeathLinkReceived -= APSession_DeathLinkReceived;
            _deathLinkService.OnDeathLinkReceived += APSession_DeathLinkReceived;
            _deathLinkService.EnableDeathLink();
        }
        else
        {
            _deathLinkService.OnDeathLinkReceived -= APSession_DeathLinkReceived;
            _deathLinkService.DisableDeathLink();
        }
    }

    // ~~~ Websocket-thread event handlers ~~~
    // These are called on the AP client's internal websocket thread.
    // Never touch Unity or game-state objects directly here; always enqueue. 
    // The Unity main thread (from a method like Update()) should be used for updating game state

    private static void APSession_SocketOpened()
    {
        // Fires synchronously inside StartPolling(), which is called after the
        // WebSocket handshake completes.  Reaching here means the PollingLoop
        // and SendLoop tasks have been started.
        Plugin.Log.LogInfo("[AP] Socket opened. PollingLoop started.");
    }

    private static void APSession_ItemReceived(IReceivedItemsHelper helper)
    {
        while (helper.PeekItem() != null)
        {
            var item = helper.DequeueItem();
            int itemIndex = _nextItemIndex++;
            Plugin.Log.LogDebug($"[AP] Item queued: {item.ItemName} (ID {item.ItemId}) at index {itemIndex}");

            // Resolve display strings on the websocket thread (where the helpers
            // live) so the main-thread tick has nothing left to look up.
            var sourcePlayer = item.Player;
            var sourceName = !string.IsNullOrEmpty(sourcePlayer?.Alias)
                ? sourcePlayer.Alias
                : sourcePlayer?.Name;

            _pendingItems.Enqueue(new PendingItem
            {
                Index = itemIndex,
                ItemId = item.ItemId,
                ItemDisplayName = item.ItemDisplayName ?? item.ItemName,
                SourceSlot = sourcePlayer?.Slot ?? -1,
                SourcePlayerName = sourceName,
            });
        }
    }

    private static void APSession_OnMessageReceived(LogMessage message)
    {
        Plugin.Log.LogInfo($"[AP] {message}");
        // TODO: Surface this in an in-game console / chat overlay.
    }

    // Called on the AP websocket thread. only set flags; no game-state access.
    private static void APSession_DeathLinkReceived(DeathLink deathLink)
    {
        Plugin.Log.LogInfo($"[AP] DeathLink received from '{deathLink.Source}': {deathLink.Cause}");
        _pendingDeathSource = deathLink.Source ?? "Another player";
        _pendingDeathCause = deathLink.Cause;
        _pendingDeathLink = true;
    }

    private static void APSession_ErrorReceived(Exception ex, string message)
    {
        Plugin.Log.LogError($"[AP] Socket error: {message}");
        if (ex != null)
        {
            Plugin.Log.LogError($"[AP] Exception: {ex}");
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                Plugin.Log.LogError($"[AP] Inner exception: {inner}");
        }
    }
}
