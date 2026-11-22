using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RL2Archipelago.Items;
using RL2Archipelago.Locations;
using RL2Archipelago.Patches;
using RL2Archipelago.Save;
using RL2Archipelago.Traps;
using RL2Archipelago.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RL2Archipelago;

public enum JournalChecksMode { Disabled = 0, Individual = 1, Grouped = 2 }

/// <summary>
/// How this seed turns class mastery ranks into location checks. Values match the apworld's
/// MasteryChecks option.
///
/// <para><see cref="Total"/> tracks the overall mastery level (the sum of every class's rank)
/// and <see cref="PerClass"/> tracks each class's rank separately. 
/// A seed uses one or the other, so only that mode's location block is ever sent.</para>
/// </summary>
public enum MasteryChecksMode { Disabled = 0, Total = 1, PerClass = 2 }

/// <summary>
/// How the manor is structured for this seed. Values match the apworld's ManorLayout option.
///
/// <para><see cref="Tree"/> is the vanilla skill tree: 72 fixed nodes, each its own location,
/// revealed as their parents are bought. <see cref="Shop"/> replaces it with four columns of
/// purchases on four set slots, where only the next unpurchased entry of a column
/// is on offer, so the manor can contain an arbitrary number of nodes.</para>
/// </summary>
public enum ManorLayoutMode { Tree = 0, Shop = 1 }

/// <summary>
/// How this seed hands out fabled weapons. Values match the apworld's FabledWeapons option.
///
/// <para><see cref="Randomized"/> shuffles them into the multiworld as the second stage of each
/// progressive class item, and the Soul Shop no longer sells them. <see cref="SoulShop"/> leaves
/// them as vanilla Soul Shop purchases, so they stay listed there and cost souls.
/// <see cref="Unlocked"/> gives every class its fabled weapon for free the moment that class is
/// unlocked, so they are neither randomized nor sold.</para>
/// </summary>
public enum FabledWeaponMode { Randomized = 0, SoulShop = 1, Unlocked = 2 }

/// <summary>
/// High-level connection lifecycle state. Distinct from <see cref="APClient.IsConnected"/>
/// (a raw socket bool) so the UI can tell an in-progress reconnect from a clean
/// user-initiated disconnect.
/// </summary>
public enum APConnectionState { Disconnected = 0, Connected = 1, Reconnecting = 2 }

/// <summary>
/// Manages the Archipelago session lifecycle: connect, disconnect, item/message
/// event handling, and thread-safe main-thread dispatch.
/// </summary>
public static class APClient
{
    public static ArchipelagoSession Session { get; private set; }

    /// <summary>A list of options determined by the yaml which can modify the AP client's behavior. E.g. death_link, completion_criteria, etc.</summary>
    public static Dictionary<string, object> SlotData { get; private set; }

    /// <summary>True only when the websocket is live. Use for decisions that require a
    /// real connection right now (sending to the server, queue-vs-send).</summary>
    public static bool IsConnected => Session?.Socket?.Connected ?? false;

    /// <summary>
    /// True whenever an AP session is logically active, i.e. connected OR temporarily
    /// reconnecting after a drop. Gameplay patches gate on this (rather than the raw
    /// <see cref="IsConnected"/>) so checks keep being recorded locally and queued for
    /// resync, and randomizer behavior stays consistent, while the connection is down.
    /// </summary>
    public static bool IsSessionActive => ConnectionState != APConnectionState.Disconnected;

    /// <summary>True while an AP session's save directory is active; controls the SaveFileSystem path redirect.</summary>
    public static bool APSaveActive => APSaveRedirect.APSaveActive;

    /// <summary>Sanitized "{RoomId}_{SlotName}" used as the save subdirectory name.</summary>
    public static string APSaveDirectoryName => APSaveRedirect.APSaveDirectoryName;

    /// <summary>Persistent state for the active run: checked locations, received items, etc.</summary>
    public static APRunState RunState { get; private set; }

    /// <summary>Levels granted per manor upgrade item received. Read from slot data on connect.</summary>
    public static int ManorUpgradeBundleSize { get; private set; } = 5;

    /// <summary>Controls whether journal/memory reads generate location checks. Read from slot data on connect.</summary>
    public static JournalChecksMode JournalChecksMode { get; private set; } = JournalChecksMode.Grouped;

    /// <summary>Controls whether mastery rank gains generate location checks. Read from slot data on connect.</summary>
    public static MasteryChecksMode MasteryChecks { get; private set; } = MasteryChecksMode.Disabled;

    /// <summary>
    /// Mastery XP award multiplier as a percentage, where 100 is the vanilla rate. Read from slot
    /// data on connect and applied by <see cref="MasteryXPPatch"/>. Independent of
    /// <see cref="MasteryChecks"/>: how fast ranks come is a pacing preference in its own right,
    /// whether or not those ranks are also location checks.
    /// </summary>
    public static int MasteryXpPercent { get; private set; } = 300;

    /// <summary>Percent chance (1–100) a bronze chest triggers an AP check. Read from slot data on connect.</summary>
    public static int BronzeChestApChance { get; private set; } = 15;

    /// <summary>Percent chance (1–100) a silver chest triggers an AP check. Read from slot data on connect.</summary>
    public static int SilverChestApChance { get; private set; } = 99;

    /// <summary>Percent chance (1–100) a fairy chest triggers an AP check. Read from slot data on connect.</summary>
    public static int FairyChestApChance { get; private set; } = 100;

    /// <summary>
    /// When true, traps show their real identity wherever a scouted item is displayed.
    /// When false (the default) they are concealed behind a real item's identity by
    /// <see cref="TrapDisguise"/> until purchased. Read from slot data on connect.
    /// </summary>
    public static bool TrapAppearanceVisible { get; private set; } = false;

    /// <summary>
    /// When true, every manor slot is shown from the start rather than only the ones that
    /// are purchase-able. Purchase rules are unaffected: a slot still cannot be
    /// bought until its parent has been (see <see cref="ManorUpgradePatch.IsSlotUnlocked"/>).
    /// Read from slot data on connect. Tree layout only; the shop layout always shows
    /// exactly the next purchase of each column. </summary>
    public static bool RevealManorUpgrades { get; private set; } = false;

    /// <summary>
    /// Which manor structure this seed uses. Read from slot data on connect.
    /// </summary>
    public static ManorLayoutMode ManorLayout { get; private set; } = ManorLayoutMode.Tree;

    /// <summary>Convenience for the many shop-vs-tree branches in the manor patches.</summary>
    public static bool IsManorShopLayout => ManorLayout == ManorLayoutMode.Shop;

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
    /// Written by <see cref="APItemReconciler"/>, which owns applying received items.
    /// </summary>
    public static bool KnightClassReceived { get; internal set; } = false;

    /// <summary>
    /// How this seed hands out fabled weapons. Mirrors the fabled_weapons YAML option.
    /// </summary>
    public static FabledWeaponMode FabledWeapons { get; private set; } = FabledWeaponMode.Randomized;

    /// <summary>
    /// Whether class unlocks are the two-stage progressive items rather than the plain one-stage
    /// ones. True only under <see cref="FabledWeaponMode.Randomized"/>, since that is the only mode
    /// that puts a fabled weapon in the item pool, and it decides which of the two parallel class
    /// item sets this seed generated.
    /// </summary>
    public static bool ProgressiveClassItems => FabledWeapons == FabledWeaponMode.Randomized;

    /// <summary>
    /// Pre-computed gold costs for each manor upgrade slot, keyed by <see cref="SkillTreeType"/>.
    /// Populated from slot data on connect under the tree layout. Empty when not connected,
    /// and empty under the shop layout, which uses <see cref="ManorShopCosts"/> instead.
    /// </summary>
    public static IReadOnlyDictionary<SkillTreeType, int> ManorUpgradeCosts { get; private set; }
        = new Dictionary<SkillTreeType, int>();

    /// <summary>
    /// Pre-computed gold costs for every shop-layout manor purchase, keyed by location ID.
    /// Populated from slot data on connect under the shop layout. Empty otherwise.
    /// </summary>
    public static IReadOnlyDictionary<long, int> ManorShopCosts { get; private set; }
        = new Dictionary<long, int>();

    /// <summary>True while we are applying an incoming death beacon, so the death patch skips sending an echo.</summary>
    internal static bool IsReceivingDeathLink { get; private set; }

    private static DeathLinkService _deathLinkService;
    private static string _slotName;

    // Our own player slot number, cached on (re)connect so send notifications can resolve
    // "found by yourself" vs "sent to X" even while disconnected (Session may be null then).
    private static int _ourSlot = -1;

    // Incoming death beacon queued for main-thread application. Written by the websocket thread,
    // read by the main thread; volatile ensures the flag write is visible after the string fields.
    private static volatile bool _pendingDeathLink;
    private static string _pendingDeathSource;
    private static string _pendingDeathCause;

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
        public int Generation;
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

    // Sequence number stamped on queued items, for log ordering only. Reset on every (re)connect
    // because AllItems makes the server replay from the start each time.
    private static int _nextItemIndex = 0;

    // How many copies of each item ID the current connection has seen. The server replays everything
    // on every connect, so this is what distinguishes "a replay of an item we already know about"
    // from "a genuinely new item": anything at or below RunState.ReceivedItemCounts is a replay.
    //
    // Only ever touched on the main thread, in DrainReceivedItems.
    private static readonly Dictionary<long, int> _replayCounts = new();

    // Bumped once per (re)connect, and stamped on every queued item. The main thread uses it to tell
    // the current replay apart from items left over from a previous session, and to know when to
    // reset _replayCounts.
    //
    // This exists instead of having the connect path clear _replayCounts and the queue directly:
    // WirePostLogin also runs on the background reconnect worker, so doing that would race the
    // main-thread drain on a plain Dictionary. A stamp is written by one thread and only read by the
    // other, so there is nothing to race.
    private static volatile int _connectionGeneration;

    // Generation whose replay the main thread is currently counting.
    private static int _drainedGeneration = -1;

    // Set when the ledger gains an entry (or the starting class is recorded), so the next tick runs a
    // reconcile pass. Keeps the steady-state cost of ProcessPendingItems to a couple of bool checks.
    // Volatile because WirePostLogin may set it from the reconnect worker.
    private static volatile bool _ledgerDirty;

    // Last observed value of APItemReconciler.CanApplyWorldEffects, so the transition into a built
    // world can trigger the pass that applies effects needing live objects (traps, gold, heirlooms).
    private static bool _worldEffectsReady;

    // Debounce for the forced profile save issued after item effects are applied.
    private static float _lastForcedSaveTime = float.NegativeInfinity;
    private const float ForcedSaveDebounceSeconds = 2f;

    // Budget for re-running a reconcile pass that reported a failure, so a permanently broken item
    // cannot re-run (and re-log) the pass every frame. Refilled on any clean pass.
    private const int MaxReconcileRetries = 10;
    private static int _reconcileRetriesLeft = MaxReconcileRetries;

    // ── Connection lifecycle / auto-reconnect ───────────────────────────────────

    /// <summary>High-level connection state used by the UI (e.g. the disconnect overlay).</summary>
    public static APConnectionState ConnectionState
    {
        get => _connectionState;
        private set => _connectionState = value;
    }
    private static volatile APConnectionState _connectionState = APConnectionState.Disconnected;

    // True from the moment a user-initiated Disconnect() begins until the next Connect().
    // Lets the SocketClosed handler and reconnect worker distinguish an intentional
    // teardown from a dropped connection so they don't fight a manual disconnect.
    private static volatile bool _intentionalDisconnect;

    // The connection parameters for the active session, captured on Connect() so the
    // background reconnect worker can re-establish the session after a drop.
    private static APConnectionData _activeConnData;

    // Guards RunState.CheckedLocations and RunState.RemoteCheckedLocations against concurrent
    // access: the main thread adds checks via SendLocationCheck and AbsorbRemoteChecks while the
    // reconnect worker reads them in Resync.
    private static readonly object _checkedLock = new object();

    // Chest-pool locations the server reported checked, queued off the websocket thread for the
    // main thread to fold into RunState. Drained by ProcessConnectionEvents().
    private static readonly ConcurrentQueue<long> _pendingRemoteChecks = new();

    // 0 = no reconnect worker running, 1 = running. Toggled via Interlocked so only one
    // backoff loop exists at a time.
    private static int _reconnectWorkerRunning;

    private static readonly Random _reconnectRng = new Random();

    private const int ReconnectBaseDelayMs = 2000;
    private const int ReconnectMaxDelayMs  = 30000;

    // Client-side cap on a single connect+login attempt. The sync TryConnectAndLogin can
    // block indefinitely if the TCP socket connects but the AP handshake never completes,
    // which would stall the whole reconnect loop on one attempt.
    private const int ConnectTimeoutMs = 10000;

    // Set by the reconnect worker (background thread) on success; drained on the main
    // thread by ProcessConnectionEvents() so the toast is raised where Unity is safe.
    private static volatile bool _pendingReconnectedToast;

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
            if (IsConnected || ConnectionState != APConnectionState.Disconnected)
                Disconnect(manual: false);

            // Clear the intentional-disconnect latch the teardown above set, so the
            // SocketClosed handler will react to drops on this new session.
            _intentionalDisconnect = false;

            if (!OpenSessionAndLogin(connData, out var loginError))
            {
                Plugin.Log.LogError($"AP login failed:\n{loginError}");
                onFailure?.Invoke(loginError);
                return;
            }

            _activeConnData = connData;

            // ── One-time per-session setup (slot data, save redirect, run state). ──
            // The reconnect worker skips all of this; it reuses the state established here.
            //
            // Every key below is written unconditionally by fill_slot_data in the apworld, so
            // they are read directly rather than defaulted. A missing key means the seed was
            // generated by a different version of the apworld than this mod was built against
            // The throw is caught below and reported as a connection failure.
            LocationRegistry.SetBlueprintChecksPerBiome(Convert.ToInt32(SlotData["blueprint_checks_per_biome"]));
            LocationRegistry.SetRuneChecksPerBiome(Convert.ToInt32(SlotData["rune_checks_per_biome"]));

            BronzeChestApChance = Convert.ToInt32(SlotData["bronze_chest_ap_chance"]);
            SilverChestApChance = Convert.ToInt32(SlotData["silver_chest_ap_chance"]);
            FairyChestApChance = Convert.ToInt32(SlotData["fairy_chest_ap_chance"]);

            ManorUpgradeBundleSize = Convert.ToInt32(SlotData["manor_upgrade_bundle_size"]);
            JournalChecksMode = (JournalChecksMode)Convert.ToInt32(SlotData["journal_checks"]);
            MasteryChecks = (MasteryChecksMode)Convert.ToInt32(SlotData["mastery_checks"]);
            MasteryXpPercent = Convert.ToInt32(SlotData["mastery_xp_multiplier"]);
            LocationRegistry.SetMasteryLevelCounts(
                Convert.ToInt32(SlotData["total_mastery_levels"]),
                Convert.ToInt32(SlotData["mastery_levels_per_class"]));
            TrapAppearanceVisible = Convert.ToInt32(SlotData["trap_appearance"]) != 0;
            RevealManorUpgrades = Convert.ToInt32(SlotData["reveal_manor_upgrades"]) != 0;
            ManorLayout = (ManorLayoutMode)Convert.ToInt32(SlotData["manor_layout"]);
            LocationRegistry.SetManorShopDepth(Convert.ToInt32(SlotData["manor_shop_depth"]));

            _slotName = connData.SlotName;
            bool serverDeathLink = Convert.ToInt32(SlotData["death_link"]) != 0;

            // The apworld computes costs for the active layout only and sends the other array
            // empty, so exactly one of these is populated.
            if (ManorLayout == ManorLayoutMode.Shop)
            {
                // Shop costs arrive flat and position-major: index = position * depth + depthIndex.
                // Re-key by location ID so every call site is a single lookup with no arithmetic.
                int depth = LocationRegistry.ManorShopDepth;
                var shopCostsArray = (JArray)SlotData["manor_shop_costs"];
                var shopMap = new Dictionary<long, int>(shopCostsArray.Count);
                for (int index = 0; index < shopCostsArray.Count; index++)
                {
                    int position = index / depth;
                    if (position >= LocationRegistry.MANOR_SHOP_POSITIONS) break;
                    shopMap[LocationRegistry.ManorShopLocation(position, index % depth)] =
                        shopCostsArray[index].Value<int>();
                }
                ManorShopCosts = shopMap;
            }
            else
            {
                var types = ItemRegistry.SkillTreeTypes;
                var costsArray = (JArray)SlotData["manor_upgrade_costs"];
                var map = new Dictionary<SkillTreeType, int>(types.Count);
                for (int i = 0; i < costsArray.Count && i < types.Count; i++)
                    map[types[i]] = costsArray[i].Value<int>();
                ManorUpgradeCosts = map;
            }

            Plugin.Log.LogInfo(
                $"Connected! Room: {Session.RoomState.Seed}  " +
                $"Slot data keys: {string.Join(", ", SlotData.Keys)}");

            // Redirect all save I/O to a directory scoped to this room + slot, load this seed's run
            // state, and verify the redirected profile actually loaded. A refusal here means the save
            // is corrupt or has gone missing: the redirect is already rolled back (or handed to the
            // game's backup prompt), and continuing would quietly overwrite whatever is left of it.
            if (!APSaveRedirect.TryActivate(connData, out var runState, out var saveError))
            {
                Plugin.Log.LogError($"[AP] Refusing to connect: {saveError}");
                TearDownSession();
                onFailure?.Invoke(saveError);
                return;
            }
            RunState = runState;

            RandomizeStartingClass = Convert.ToInt32(SlotData["randomize_starting_class"]) != 0;
            StartingClassIndex = Convert.ToInt32(SlotData["starting_class_index"]);

            FabledWeapons = (FabledWeaponMode)Convert.ToInt32(SlotData["fabled_weapons"]);
            KnightClassReceived = RunState.KnightClassReceived;

            // On first connect to this seed: queue the burden picker. MenuPatch opens it once the main
            // menu has focus again, because the success path below runs a transition that closes and
            // rebuilds the menu and would tear down a window opened from here.
            if (!RunState.BurdensChosen)
                BurdenSelection.PendingOpen = true;

            // Load per-seed player settings; user override wins over server value.
            APSettings.DeathLink = null;
            LoadPlayerSettings(APSaveDirectoryName);
            DeathLinkEnabled = APSettings.DeathLink ?? serverDeathLink;

            // ── Per-(re)connect wiring: item/message handlers, DeathLink service,
            //    check resync, and location scouting. The reconnect worker runs the
            //    same helper after re-establishing a session. ──
            WirePostLogin();

            // Reconcile before the player can press Start. WirePostLogin has just queued the
            // server's full item replay, so this is where a save that lost ground gets repaired:
            // every manor level, blueprint, rune, heirloom and teleporter is set back to the
            // absolute state the multiworld says this slot is owed, and the result is forced to disk.
            _ledgerDirty = true;
            ProcessPendingItems();

            // Fire the session-opened event and the caller's success callback on
            // the main thread.  We're already on the main thread here (called from UI),
            // so invoke directly.
            OnSessionOpened?.Invoke(Session);
            onSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"AP connection threw an exception:\n{ex.Message}\n{ex.StackTrace}");

            // The redirect may already be live (TryActivate succeeded and something later threw).
            // Leaving it active with no session would send every subsequent vanilla save into this
            // seed's folder, silently.
            APSaveRedirect.Rollback(restoreSnapshot: true);
            RunState = null;
            TearDownSession();
            onFailure?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// Drops a half-established session without touching the save redirect or run state. Used on the
    /// connect failure paths, where <see cref="APSaveRedirect"/> has already decided what happens to
    /// the save side.
    /// </summary>
    private static void TearDownSession()
    {
        ConnectionState = APConnectionState.Disconnected;
        DetachAndAbort(Session);
        Session = null;

        // No session means the reconnect worker has nothing to reconnect to.
        _activeConnData = null;

        DrainWebsocketQueues();
        _replayCounts.Clear();
        _nextItemIndex = 0;
        _ledgerDirty = false;
        _worldEffectsReady = false;
        _reconcileRetriesLeft = MaxReconcileRetries;
    }

    /// <summary>
    /// Creates a fresh session, wires the diagnostic + lifecycle socket handlers,
    /// and attempts login. On success assigns <see cref="Session"/> / <see cref="SlotData"/>,
    /// stamps <c>connData.RoomId</c>, and returns true. On failure tears the failed
    /// session down, sets <paramref name="error"/>, and returns false.
    /// Shared by the initial <see cref="Connect"/> and the reconnect worker.
    /// </summary>
    private static bool OpenSessionAndLogin(APConnectionData connData, out string error)
    {
        error = null;

        Session = ArchipelagoSessionFactory.CreateSession(connData.Hostname, connData.Port);

        // Hook diagnostics + lifecycle before TryConnectAndLogin so we capture events
        // that fire during the handshake. SocketClosed drives the auto-reconnect.
        Session.Socket.SocketOpened  += APSession_SocketOpened;
        Session.Socket.ErrorReceived += APSession_ErrorReceived;
        Session.Socket.SocketClosed  += APSession_SocketClosed;

        // Run the blocking login on a worker task with a client-side timeout: TryConnectAndLogin
        // can block forever when the TCP socket connects but the AP handshake never finishes,
        // which would hang the single reconnect worker and stall every later attempt. Aborting
        // the session on timeout unblocks the abandoned task shortly after.
        LoginResult loginResult;
        try
        {
            var session = Session;
            var loginTask = Task.Run(() => session.TryConnectAndLogin(
                "Rogue Legacy 2",
                connData.SlotName,
                ItemsHandlingFlags.AllItems,
                password: string.IsNullOrEmpty(connData.Password) ? null : connData.Password,
                requestSlotData: true));

            if (!loginTask.Wait(ConnectTimeoutMs))
            {
                error = $"Connect attempt timed out after {ConnectTimeoutMs}ms.";
                Plugin.Log.LogInfo($"[AP] {error} Abandoning attempt.");
                ObserveFault(loginTask, "Abandoned login");  // don't let the orphan task fault unobserved
                DetachAndAbort(Session);
                Session = null;
                return false;
            }

            loginResult = loginTask.Result;
        }
        catch (Exception ex)
        {
            error = ex.InnerException?.Message ?? ex.Message;
            DetachAndAbort(Session);
            Session = null;
            return false;
        }

        Session.Socket.SocketOpened -= APSession_SocketOpened;

        if (!loginResult.Successful)
        {
            error = string.Join("\n", ((LoginFailure)loginResult).Errors);
            // Abort (not just detach): leaving a half-open socket behind makes the *next*
            // reconnect attempt hang instead of timing out cleanly.
            DetachAndAbort(Session);
            Session = null;
            return false;
        }

        SlotData = ((LoginSuccessful)loginResult).SlotData;
        // Persist the room ID so we can warn on multiworld mismatch later.
        connData.RoomId = Session.RoomState.Seed;
        return true;
    }

    /// <summary>
    /// Per-(re)connect wiring that must run every time a session is established:
    /// websocket item/message handlers, the DeathLink service, check resync, and
    /// location scouting. Assumes the one-time setup (slot-data parsing, save
    /// redirect, RunState load, <see cref="DeathLinkEnabled"/>) has already happened.
    /// Ends by marking the connection <see cref="APConnectionState.Connected"/>.
    /// May run on the reconnect worker thread; touches only network/library state.
    /// </summary>
    private static void WirePostLogin()
    {
        // Each item in _pendingItems is stamped with a connectionGeneration number, representing
        // how many times the client has established a connection to an AP world in the current game
        // session. This way, if a previous session's items are still in the queue when we reconnect, 
        // the main thread can tell them apart and discard the old ones. 
        //
        // Disconnect() and TearDownSession() already empty the queue, so this is not the primary
        // defence against a previous session's items being applied to this one. What it catches is
        // the case a drain cannot: the outgoing session's socket thread can be partway through
        // APSession_ItemReceived when we detach it, and enqueue after the drain has run. Those items
        // carry the old stamp and are discarded.
        Interlocked.Increment(ref _connectionGeneration);
        _nextItemIndex = 0;

        Session.Items.ItemReceived += APSession_ItemReceived;
        Session.MessageLog.OnMessageReceived += APSession_OnMessageReceived;
        Session.Locations.CheckedLocationsUpdated += APSession_CheckedLocationsUpdated;
        // Manually drain items the server sent during the handshake before the handler
        // was wired; those sit in the library's buffer unfired.
        APSession_ItemReceived(Session.Items);

        // Recreate the DeathLink service against the new session.
        // Cache our slot so notifications work without a live Session during a drop.
        _ourSlot = Session.ConnectionInfo.Slot;

        _deathLinkService = Session.CreateDeathLinkService();
        if (DeathLinkEnabled)
        {
            _deathLinkService.OnDeathLinkReceived += APSession_DeathLinkReceived;
            _deathLinkService.EnableDeathLink();
            Plugin.Log.LogInfo("[AP] DeathLink enabled.");
        }

        // Reconcile local and server state. If the client recorded a check that never
        // made it to the server (network drop mid-send, or checks made while offline),
        // resend it now so the multiworld stays consistent.
        ResyncCheckedLocations();

        // The reverse direction, for the pooled categories only. The Connected packet's
        // checked_locations was parsed before the handler above was attached, so its contents never
        // raise the event; seed from the snapshot instead.
        foreach (var id in Session.Locations.AllLocationsChecked)
            if (LocationRegistry.IsPooledLocation(id))
                _pendingRemoteChecks.Enqueue(id);

        // Re-send goal completion if the final boss was beaten while disconnected.
        // SetGoalAchieved is idempotent server-side.
        if (RunState?.GoalAchieved == true)
        {
            try
            {
                Session.SetGoalAchieved();
                Plugin.Log.LogInfo("[AP] Re-sent goal completion after reconnect.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AP] Failed to re-send goal completion: {ex.Message}");
            }
        }

        // Scout every unchecked tracked location so in-world graphics (e.g. heirloom
        // pedestals) can show the item that will drop there.
        ScoutTrackedLocations();

        // The replay queued above has to be reconciled; the next tick picks it up.
        _ledgerDirty = true;

        ConnectionState = APConnectionState.Connected;
    }

    /// <summary>
    /// Empties the queues that the websocket thread feeds, so nothing from an outgoing session is applied
    /// to the next one. <c>ConcurrentQueue&lt;T&gt;.Clear()</c> does not exist on the .NET Framework
    /// target, hence the dequeue loops.
    /// </summary>
    private static void DrainWebsocketQueues()
    {
        while (_pendingItems.TryDequeue(out _)) { }
        while (_pendingRemoteChecks.TryDequeue(out _)) { }
    }

    private static void APSession_SocketClosed(string reason) => HandleConnectionDropped(reason);

    /// <summary>Observes a fire-and-forget task so a fault is logged here instead of
    /// surfacing later as an unobserved TaskScheduler exception at GC time.</summary>
    private static void ObserveFault(Task task, string what) =>
        task?.ContinueWith(
            t => Plugin.Log.LogError($"[AP] {what} failed: {t.Exception?.Flatten().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);

    /// <summary>Detaches our handlers from a dead/abandoned session and aborts its socket so
    /// its polling loop exits instead of looping on errors. Safe to call with a null session.</summary>
    private static void DetachAndAbort(ArchipelagoSession dead)
    {
        if (dead == null) return;
        dead.Socket.SocketOpened  -= APSession_SocketOpened;
        dead.Items.ItemReceived -= APSession_ItemReceived;
        dead.MessageLog.OnMessageReceived -= APSession_OnMessageReceived;
        dead.Locations.CheckedLocationsUpdated -= APSession_CheckedLocationsUpdated;
        dead.Socket.ErrorReceived -= APSession_ErrorReceived;
        dead.Socket.SocketClosed  -= APSession_SocketClosed;
        try { ObserveFault(dead.Socket.DisconnectAsync(), "Socket abort"); } catch { /* best-effort */ }
    }

    /// <summary>
    /// Handles a dropped connection on the websocket thread. Triggered both by
    /// <see cref="IArchipelagoSocketHelper.SocketClosed"/> and by a fatal
    /// <see cref="IArchipelagoSocketHelper.ErrorReceived"/>. The MultiClient.Net polling
    /// loop does not always raise SocketClosed on a forcible close; it can instead spin
    /// firing ErrorReceived, so we must react to the error too.
    ///
    /// <para>Only the first event while <see cref="APConnectionState.Connected"/> acts:
    /// it transitions to Reconnecting, detaches and aborts the dead session (so its
    /// polling loop stops spamming), and starts the backoff worker. Local state
    /// (checked locations, scout cache, save redirect) is left intact so play
    /// continues offline.</para>
    /// </summary>
    private static void HandleConnectionDropped(string reason)
    {
        if (_intentionalDisconnect) return;

        // Only a live session going down should start a reconnect. Ignore errors while
        // Disconnected (no session) or Reconnecting (already handling). This also
        // collapses the rapid ErrorReceived spam down to a single reconnect trigger.
        if (ConnectionState != APConnectionState.Connected) return;
        ConnectionState = APConnectionState.Reconnecting;

        Plugin.Log.LogWarning($"[AP] Connection lost: {reason}. Attempting to reconnect.");

        // Detach + abort the dead session so its polling loop stops firing into us.
        DetachAndAbort(Session);

        StartReconnectWorker();
    }

    /// <summary>
    /// Launches (at most one) background task that retries the connection with
    /// exponential backoff + jitter, capped at <see cref="ReconnectMaxDelayMs"/>,
    /// until it reconnects or the player manually disconnects. Runs off the main
    /// thread because <see cref="ArchipelagoSession.TryConnectAndLogin"/> blocks.
    /// </summary>
    private static void StartReconnectWorker()
    {
        if (Interlocked.CompareExchange(ref _reconnectWorkerRunning, 1, 0) != 0)
            return; // a worker is already running

        Task.Run(() =>
        {
            try
            {
                int attempt = 0;
                while (ConnectionState == APConnectionState.Reconnecting && !_intentionalDisconnect)
                {
                    int delay = (int)Math.Min(
                        ReconnectBaseDelayMs * Math.Pow(2, attempt), ReconnectMaxDelayMs);
                    // ±20% jitter so many clients reconnecting at once don't sync up.
                    int jitter = delay / 5;
                    delay += _reconnectRng.Next(-jitter, jitter + 1);
                    Thread.Sleep(Math.Max(500, delay));

                    if (_intentionalDisconnect || ConnectionState != APConnectionState.Reconnecting)
                        break;

                    var conn = _activeConnData;
                    if (conn == null) break; // disconnected out from under us

                    Plugin.Log.LogInfo($"[AP] Reconnect attempt {attempt + 1}...");
                    try
                    {
                        if (OpenSessionAndLogin(conn, out var error))
                        {
                            // The player may have manually disconnected during the blocking
                            // login; if so, abandon the just-opened session.
                            if (_intentionalDisconnect)
                            {
                                DetachAndAbort(Session);
                                Session = null;
                                break;
                            }

                            WirePostLogin();
                            Plugin.Log.LogInfo("[AP] Reconnected successfully.");
                            // Defer the toast to the main thread (Unity isn't safe here).
                            _pendingReconnectedToast = true;
                            break;
                        }

                        Plugin.Log.LogWarning($"[AP] Reconnect attempt {attempt + 1} failed: {error}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError($"[AP] Reconnect attempt threw: {ex.Message}");
                        // If login succeeded but WirePostLogin threw, a live half-wired session
                        // is attached to our handlers. Tear it down before retrying so we don't
                        // leak a second event source feeding items/errors into the client.
                        if (Session != null && ConnectionState != APConnectionState.Connected)
                        {
                            DetachAndAbort(Session);
                            Session = null;
                        }
                    }

                    attempt++;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _reconnectWorkerRunning, 0);
            }
        });
    }

    /// <summary>Tears down the current session and fires <see cref="OnSessionClosed"/>.
    /// Safe to call mid-reconnect (when no live <see cref="Session"/> exists).</summary>
    public static void Disconnect(bool manual = true)
    {
        // Nothing to tear down (and avoid double-teardown from the Connect() preamble).
        if (Session == null && RunState == null && ConnectionState == APConnectionState.Disconnected)
            return;

        // Rolling back the save redirect swaps the in-memory profile out from under a live run: the
        // vanilla profile would be loaded over it and every later save would write to the wrong tree,
        // damaging both profiles. No UI reaches this today (the Archipelago button is main-menu only),
        // so refusing is safe and cannot strand anyone.
        if (manual && IsInGame)
        {
            Plugin.Log.LogError(
                "[AP] Refusing to disconnect during an active run - that would swap the save profile " +
                "mid-game. Return to the main menu first.");
            return;
        }

        // Latch this first so the reconnect worker / SocketClosed handler stand down.
        _intentionalDisconnect = true;
        ConnectionState = APConnectionState.Disconnected;

        if (Session != null)
        {
            Session.Items.ItemReceived -= APSession_ItemReceived;
            Session.MessageLog.OnMessageReceived -= APSession_OnMessageReceived;
            Session.Locations.CheckedLocationsUpdated -= APSession_CheckedLocationsUpdated;
            Session.Socket.ErrorReceived -= APSession_ErrorReceived;
            Session.Socket.SocketClosed  -= APSession_SocketClosed;

            // Guard the blocking close: on a faulted socket .Wait() throws, and the
            // save-redirect revert below MUST still run or saves keep going to AP_Saves/.
            if (Session.Socket.Connected)
            {
                try { Session.Socket.DisconnectAsync().Wait(2000); }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[AP] Error closing socket during disconnect: {ex.Message}");
                }
            }
        }

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
        FabledWeapons = FabledWeaponMode.Randomized;
        APItemReconciler.Reset();
        MasteryChecks = MasteryChecksMode.Disabled;
        MasteryXpPercent = 300;
        RevealManorUpgrades = false;
        ManorLayout = ManorLayoutMode.Tree;
        // Cleared so one layout's prices can never be read while the other layout is active.
        ManorUpgradeCosts = new Dictionary<SkillTreeType, int>();
        ManorShopCosts = new Dictionary<long, int>();
        ManorShopPatch.Reset();

        // Put the vanilla save path, profile and backup cache back. Skipped while save recovery is
        // underway: that flow deliberately keeps the redirect live so the game's own backup window
        // operates on this seed's folder, and tears it down itself when the player is finished.
        if (!APSaveRecovery.InProgress)
            APSaveRedirect.Rollback(restoreSnapshot: false);

        DrainWebsocketQueues();
        _replayCounts.Clear();
        _nextItemIndex = 0;
        _ledgerDirty = false;
        _worldEffectsReady = false;
        _reconcileRetriesLeft = MaxReconcileRetries;
        _lastForcedSaveTime = float.NegativeInfinity;

        // Trap state lives in static flags, so without this an AP trap would keep firing in a
        // subsequent vanilla session.
        TrapManager.ClearAllTraps();

        APNotifications.Reset();
        _scoutedItems.Clear();
        TrapDisguise.Clear();
        RunState = null;
        IsInGame = false;
        Session = null;
        _activeConnData = null;

        if (manual)
            OnSessionClosed?.Invoke();

        Plugin.Log.LogInfo("Disconnected from AP server.");
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
        // Lock guards the HashSet against the reconnect worker reading it in Resync.
        bool added;
        lock (_checkedLock)
            added = RunState.CheckedLocations.Add(locationId);
        if (!added)
        {
            Plugin.Log.LogDebug($"[AP] Location '{displayName}' already checked. skipping re-send.");
            return;
        }
        RunState.Save(APSaveDirectoryName);

        // Show the "found/sent" toast from the scout cache regardless of connection so
        // offline pickups still surface in the AP log (the cache survives a drop).
        EnqueueSendNotification(locationId);

        if (!IsConnected)
        {
            Plugin.Log.LogInfo(
                $"[AP] Not connected; queued location '{displayName}' for resync on next connect.");
            return;
        }

        if (!Session.Locations.AllLocations.Contains(locationId))
            Plugin.Log.LogError(
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
        var ourSlot = _ourSlot;
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
    /// Returns the <em>true</em> item at <paramref name="locationId"/>, or <c>null</c> if the
    /// scout hasn't come back yet or the location isn't tracked. Scouts are requested
    /// asynchronously right after a successful connect.
    ///
    /// <para>
    /// Do not use this to display an unclaimed location to the player: it reports traps by their
    /// real name and would defeat <see cref="TrapDisguise"/>. Use <see cref="GetItemView"/> for
    /// anything the player can see before purchase. This accessor is for ground truth only,
    /// e.g. reporting an item the player has already claimed.
    /// </para>
    /// </summary>
    public static ScoutedItemInfo GetScoutedItem(long locationId) =>
        _scoutedItems.TryGetValue(locationId, out var info) ? info : null;

    /// <summary>
    /// Returns the item identity that should be <em>shown</em> to the player for
    /// <paramref name="locationId"/>, or <c>null</c> if the scout hasn't come back yet.
    ///
    /// <para>
    /// Every display surface must go through here rather than <see cref="GetScoutedItem"/>:
    /// this is the single point at which a trap is swapped for its disguise, so anything
    /// reading the scout cache directly would leak it. <see cref="GetScoutedItem"/> remains
    /// for consumers that legitimately need ground truth (e.g. the post-purchase toast).
    /// </para>
    /// </summary>
    internal static APItemView GetItemView(long locationId)
    {
        if (!_scoutedItems.TryGetValue(locationId, out var scouted)) return null;
        return TrapDisguise.ViewFor(locationId, scouted, _ourSlot, Session?.ConnectionInfo?.Game);
    }

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

                    // Disguise candidates are drawn from the scout reply itself, so the pool
                    // can only be built once the cache is populated. Rebuilding after a
                    // reconnect is harmless: selection is a pure function of seed + location.
                    TrapDisguise.BuildPool(
                        _scoutedItems,
                        Session?.RoomState?.Seed,
                        _ourSlot,
                        Session?.ConnectionInfo?.Game);
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

        // Snapshot under lock: this may run on the reconnect worker thread while the
        // main thread is adding checks via SendLocationCheck.
        long[] localChecks;
        lock (_checkedLock)
            localChecks = RunState.CheckedLocations.ToArray();

        var serverKnown = Session.Locations.AllLocationsChecked;
        var missing = localChecks.Where(id => !serverKnown.Contains(id)).ToArray();

        if (missing.Length == 0)
        {
            Plugin.Log.LogDebug("[AP] Checked-location state is already in sync with the server.");
            return;
        }

        Plugin.Log.LogInfo(
            $"[AP] Resyncing {missing.Length} location(s) the server hadn't recorded yet.");
        try
        {
            ObserveFault(Session.Locations.CompleteLocationChecksAsync(missing), "Resync");
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
        // Persist first so a goal reached while disconnected survives and is re-sent on
        // reconnect (WirePostLogin), mirroring how location checks are queued.
        if (RunState != null && !RunState.GoalAchieved)
        {
            RunState.GoalAchieved = true;
            RunState.Save(APSaveDirectoryName);
        }

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

        RunState.StartingClassSkill = slot.SkillTree.ToString();
        RunState.StartingClassApplied = true;
        RunState.Save(APSaveDirectoryName);
        _ledgerDirty = true;

        Plugin.Log.LogInfo($"[AP] Starting class recorded: {slot.ItemName}");
    }

    /// <summary>
    /// Main-thread drain for connection-lifecycle UI events the background reconnect
    /// worker can't safely raise itself (Unity APIs are main-thread only). Call each
    /// frame from the dispatch patch.
    /// </summary>
    public static void ProcessConnectionEvents()
    {
        if (_pendingReconnectedToast)
        {
            _pendingReconnectedToast = false;
            APNotifications.Enqueue(
                title: "Archipelago",
                subtitle: "Reconnected",
                description: "Connection restored.",
                critical: true);
        }

        AbsorbRemoteChecks();
    }

    /// <summary>
    /// Folds pooled locations the server already has checked into
    /// <see cref="APRunState.RemoteCheckedLocations"/>, so the blueprint, rune and individual
    /// journal/memory pools step over them instead of spending a pickup on a slot whose item has
    /// already gone out to its recipient.
    ///
    /// <para>Runs on the main thread because it writes the run state to disk. The queue it drains is
    /// filled from the websocket thread by <see cref="APSession_CheckedLocationsUpdated"/> and,
    /// at connect, from the handshake's <c>checked_locations</c> snapshot.</para>
    /// </summary>
    private static void AbsorbRemoteChecks()
    {
        if (_pendingRemoteChecks.IsEmpty) return;

        var runState = RunState;
        if (runState == null)
        {
            // No run to record against (a session torn down between enqueue and drain). Drop them;
            // the next connect re-seeds from the server's snapshot anyway.
            while (_pendingRemoteChecks.TryDequeue(out _)) { }
            return;
        }

        int added = 0;
        while (_pendingRemoteChecks.TryDequeue(out var id))
        {
            lock (_checkedLock)
            {
                // A check this player made is already accounted for; keep the remote set meaning
                // strictly "checked by someone other than us".
                if (runState.CheckedLocations.Contains(id)) continue;
                if (!runState.RemoteCheckedLocations.Add(id)) continue;
            }

            added++;
            var displayName = LocationRegistry.Names.TryGetValue(id, out var n) ? n : id.ToString();
            Plugin.Log.LogDebug($"[AP] '{displayName}' was checked without us; its pool will skip it.");
        }

        if (added == 0) return;

        runState.Save(APSaveDirectoryName);
        Plugin.Log.LogInfo(
            $"[AP] {added} pooled location(s) were already checked on the server. " +
            "The chest and journal pools will skip them instead of awarding an empty slot.");
    }

    /// <summary>
    /// Main-thread item pump, called every frame from the dispatch patch and once directly from
    /// <see cref="Connect"/>. Two separate jobs:
    ///
    /// <list type="number">
    /// <item><b>Record</b> what the server has sent into <see cref="APRunState.ReceivedItemCounts"/>.
    /// Deliberately ungated by <see cref="IsInGame"/>: the connect-time reconcile needs the ledger
    /// populated before the player can press Start, which is where a damaged save gets repaired.</item>
    /// <item><b>Apply</b> it, by asking <see cref="APItemReconciler"/> to raise anything in the save
    /// that falls short of what the ledger says is owed.</item>
    /// </list>
    ///
    /// <para>Splitting the two is the whole point. Recording is never conditional on an apply
    /// succeeding, and applying is an idempotent absolute set that is retried on the next pass - so
    /// an item can no longer be lost by failing to apply once, which is what the old
    /// grant-once-then-advance-the-watermark design did on any exception.</para>
    /// </summary>
    public static void ProcessPendingItems()
    {
        var runState = RunState;
        if (runState == null) return;

        DrainReceivedItems(runState);

        if (!APItemReconciler.CanApplySaveData) return;

        if (RandomizeStartingClass && !runState.StartingClassApplied)
            ApplyStartingClass();

        bool worldReady = APItemReconciler.CanApplyWorldEffects;
        bool worldJustBecameReady = worldReady && !_worldEffectsReady;
        _worldEffectsReady = worldReady;

        bool changed = false;

        // Reconcile when the ledger moved, or when the world has just finished building and effects
        // that need live objects (traps, ability refresh) can finally be applied.
        if (_ledgerDirty || worldJustBecameReady)
        {
            _ledgerDirty = false;
            changed = RunReconcilePass();
        }

        if (worldReady)
            changed |= ApplyPendingGold(runState);

        if (changed)
            RequestProfileSave();
    }

    /// <summary>
    /// Runs one reconcile pass and decides whether to try again next tick.
    ///
    /// <para>A pass that reports a failure is re-armed, because the usual cause is transient state
    /// that will be fine a frame later. The retry budget stops a genuinely broken item from re-running
    /// (and re-logging) the pass every frame forever; a new item or a world transition re-arms it
    /// regardless, so nothing is permanently abandoned.</para>
    /// </summary>
    private static bool RunReconcilePass()
    {
        ReconcileResult result;
        try
        {
            result = APItemReconciler.Reconcile();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSync] Reconcile pass threw: {ex}");
            RearmAfterFailure();
            return false;
        }

        if (result.AnyFailure)
            RearmAfterFailure();
        else
            _reconcileRetriesLeft = MaxReconcileRetries;

        // Some effects live only in the run state (the Knight Class unlock). Without this they would
        // be set in memory, never written, and re-applied on every single connect.
        if (result.RunStateChanged && RunState != null)
            RunState.Save(APSaveDirectoryName);

        return result.ChangedSaveData;
    }

    private static void RearmAfterFailure()
    {
        if (_reconcileRetriesLeft <= 0)
        {
            Plugin.Log.LogWarning(
                "[APSync] Giving up on retrying the reconcile pass for now; it will run again when a " +
                "new item arrives or the world reloads.");
            return;
        }

        _reconcileRetriesLeft--;
        _ledgerDirty = true;
    }

    /// <summary>
    /// Moves everything the websocket thread queued into the run state's ledger. The server replays
    /// every item on each connect, so a dequeued item only counts as new when this connection has now
    /// seen more copies of that ID than the ledger already records.
    /// </summary>
    private static void DrainReceivedItems(APRunState runState)
    {
        bool dirty = false;
        int generation = _connectionGeneration;

        while (_pendingItems.TryDequeue(out var pending))
        {
            // Left over from an earlier session (possibly a different seed entirely).
            if (pending.Generation != generation) continue;

            // First item of a new connection: start counting this replay from scratch.
            if (_drainedGeneration != generation)
            {
                _drainedGeneration = generation;
                _replayCounts.Clear();
            }

            _replayCounts.TryGetValue(pending.ItemId, out int seen);
            _replayCounts[pending.ItemId] = ++seen;

            if (seen <= runState.ReceivedCount(pending.ItemId))
                continue; // replay of an item already on record

            runState.ReceivedItemCounts[pending.ItemId] = seen;
            dirty = true;

            // Traps are per-life rather than cumulative (PlayerDeathPatch clears them on death), so a
            // newly received one is latched here instead of being derived from the received ledger -
            // reconciling against that would resurrect every trap the player had ever been sent.
            var trap = ItemRegistry.ToTrapBurdenType(pending.ItemId);
            if (trap.HasValue)
                runState.ActiveTraps.Add(trap.Value);

            EnqueueReceiveNotification(pending, seen);
        }

        if (!dirty) return;

        runState.Save(APSaveDirectoryName);
        _ledgerDirty = true;
    }

    /// <summary>
    /// Grants any gold the ledger says has arrived but that has not been applied yet, one ordinal at
    /// a time. Gold is the only consumable the mod hands out, so it cannot be reconciled by reading
    /// the save back - a spent coin is indistinguishable from a lost one. An ordinal is promoted only
    /// once its grant actually succeeds, so a grant attempted too early is retried rather than lost.
    /// </summary>
    private static bool ApplyPendingGold(APRunState runState)
    {
        int received = runState.ReceivedCount(ItemRegistry.GoldCoins);
        int applied = runState.AppliedCount(ItemRegistry.GoldCoins);
        if (applied >= received) return false;

        bool granted = false;
        for (int ordinal = applied; ordinal < received; ordinal++)
        {
            if (!GoldCoinsPatch.TryGrant(APSaveDirectoryName, ordinal))
                break; // not ready; leave the ordinal unapplied and retry on a later tick

            runState.AppliedItemCounts[ItemRegistry.GoldCoins] = ordinal + 1;
            granted = true;
        }

        if (granted)
            runState.Save(APSaveDirectoryName);

        return granted;
    }

    /// <summary>
    /// Forces the profile to disk after item effects were applied, so the window in which the ledger
    /// is ahead of the game save stays small. Debounced, because a reconcile pass can correct many
    /// items at once.
    ///
    /// <para><c>saveStageData: false</c> is deliberate: <c>SaveGameData</c> refuses to write Stage
    /// data outside the World scene and would only log about it.</para>
    /// </summary>
    /// <param name="force">
    /// Bypasses the debounce. For one-off writes that represent a deliberate player action rather
    /// than one of many item grants, where silently coalescing the save could lose the change.
    /// </param>
    internal static void RequestProfileSave(bool force = false)
    {
        if (!force && UnityEngine.Time.unscaledTime < _lastForcedSaveTime + ForcedSaveDebounceSeconds) return;
        _lastForcedSaveTime = UnityEngine.Time.unscaledTime;

        try
        {
            SaveManager.SaveAllCurrentProfileGameData(
                SavingType.FileOnly, saveStageData: false, ignoreTimeRestriction: true);
            Plugin.Log.LogDebug("[APSync] Forced a profile save after applying item effects.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[APSync] Forced profile save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Pushes the "you received X from Y" HUD. Skipped for self-source items
    /// because <see cref="EnqueueSendNotification"/> already showed the
    /// "you discovered your own item" HUD at send time.
    /// </summary>
    /// <param name="receivedCount">
    /// Copies of this item the slot has now been sent, including this one. Only used to tell the
    /// two stages of a progressive class item apart, which otherwise share a name and would give
    /// the player no way to know whether they just got the class or its fabled weapon.
    /// </param>
    private static void EnqueueReceiveNotification(PendingItem pending, int receivedCount)
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
        var description = IsFabledWeaponStage(pending.ItemId, receivedCount)
            ? $"Fabled weapon unlocked - sent by {sender}"
            : $"Sent by {sender}";

        APNotifications.Enqueue(
            title: isTrap ? "Trap!" : "Item Received",
            subtitle: itemName,
            description: description,
            critical: isTrap || ItemRegistry.ToHeirloomType(pending.ItemId).HasValue);
    }

    /// <summary>
    /// Whether the copy that just arrived is the fabled-weapon stage of a progressive class item.
    ///
    /// <para>Mirrors the arithmetic in <see cref="APItemReconciler"/>: the starting class is
    /// unlocked outside the ledger, so for that one class the first copy received is already the
    /// second stage.</para>
    /// </summary>
    private static bool IsFabledWeaponStage(long itemId, int receivedCount)
    {
        var position = ItemRegistry.ToProgressiveClassPosition(itemId);
        if (!position.HasValue) return false;

        var skill = ItemRegistry.ProgressiveClassSkill(position.Value);
        bool isStartingClass = skill.HasValue
            ? StartingClassIndex > 0
              && GameConstants.ManorSlots[43 + StartingClassIndex].SkillTree == skill.Value
            : StartingClassIndex == 0;   // no backing slot means Knight

        return receivedCount + (isStartingClass ? 1 : 0) >= 2;
    }


    // ── Player settings (per-seed persistence) ───────────────────────────────

    private struct PlayerSettingsData
    {
        public bool? DeathLink   { get; set; }
        public bool? UseMinimalistAPLog  { get; set; }
        public bool? DebugKeysEnabled { get; set; }
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
            APSettings.DebugKeysEnabled = data.DebugKeysEnabled ?? false;
            Plugin.Log.LogInfo($"[AP] Player settings loaded (DeathLink={APSettings.DeathLink}, UseMinimalistAPLog={APSettings.UseMinimalistAPLog}, DebugKeysEnabled={APSettings.DebugKeysEnabled}).");
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
            var data = new PlayerSettingsData { DeathLink = APSettings.DeathLink, UseMinimalistAPLog = APSettings.UseMinimalistAPLog, DebugKeysEnabled = APSettings.DebugKeysEnabled };
            APRunState.WriteTextAtomic(path, JsonConvert.SerializeObject(data, Formatting.Indented));
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
        
        // Currently a No Op. This could trigger a notification or log message if desired.
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
                Generation = _connectionGeneration,
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
        Plugin.Log.LogMessage($"[AP] {message}");
        // TODO: Surface this in an in-game console / chat overlay.
    }

    /// <summary>
    /// Called on the AP websocket thread when the server reports locations checked (including ones
    /// this client never checked itself like from a slot being released). Only the
    /// pooled categories care, and only so they can skip a slot whose item has already been sent;
    /// filter here and leave the rest to the main-thread drain.
    /// </summary>
    private static void APSession_CheckedLocationsUpdated(ReadOnlyCollection<long> newCheckedLocations)
    {
        if (newCheckedLocations == null) return;
        foreach (var id in newCheckedLocations)
            if (LocationRegistry.IsPooledLocation(id))
                _pendingRemoteChecks.Enqueue(id);
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
        // The polling loop can spam this on a forcible close. Log the full detail only
        // for the first error of a live session; quiet the rest to avoid flooding the log.
        if (ConnectionState == APConnectionState.Connected)
        {
            Plugin.Log.LogError($"[AP] Socket error: {message}");
            if (ex != null)
            {
                Plugin.Log.LogError($"[AP] Exception: {ex}");
                for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                    Plugin.Log.LogError($"[AP] Inner exception: {inner}");
            }
        }
        else
        {
            Plugin.Log.LogDebug($"[AP] Socket error (while {ConnectionState}): {message}");
        }

        // A socket error on a live session means the connection is going down. MultiClient.Net
        // doesn't reliably raise SocketClosed for a forcible close (it loops on ErrorReceived
        // instead), so drive the reconnect from here too. No-op unless currently Connected.
        HandleConnectionDropped(message);
    }
}
