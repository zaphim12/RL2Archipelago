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
using System.Threading;
using System.Threading.Tasks;

namespace RL2Archipelago;

public enum JournalChecksMode { Disabled = 0, Individual = 1, Grouped = 2 }

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

    /// <summary>
    /// When true, traps show their real identity wherever a scouted item is displayed.
    /// When false (the default) they are concealed behind a real item's identity by
    /// <see cref="TrapDisguise"/> until purchased. Read from slot data on connect.
    /// </summary>
    public static bool TrapAppearanceVisible { get; private set; } = false;

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

    // Our own player slot number, cached on (re)connect so send notifications can resolve
    // "found by yourself" vs "sent to X" even while disconnected (Session may be null then).
    private static int _ourSlot = -1;

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

    // Guards RunState.CheckedLocations against concurrent access: the main thread adds
    // checks via SendLocationCheck while the reconnect worker reads them in Resync.
    private static readonly object _checkedLock = new object();

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

            // Default to concealed when the key is absent (older apworld / older seed).
            TrapAppearanceVisible = SlotData.TryGetValue("trap_appearance", out var taObj)
                && Convert.ToInt32(taObj) != 0;

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
                $"Slot data keys: {string.Join(", ", SlotData.Keys)}");

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

            // ── Per-(re)connect wiring: item/message handlers, DeathLink service,
            //    check resync, and location scouting. The reconnect worker runs the
            //    same helper after re-establishing a session. ──
            WirePostLogin();

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

        Plugin.Log.LogDebug("[AP] Calling TryConnectAndLogin...");

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
                Plugin.Log.LogDebug($"[AP] {error} Abandoning attempt.");
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

        Plugin.Log.LogDebug($"[AP] TryConnectAndLogin returned. Successful={loginResult.Successful}");

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
        // Reset the index BEFORE wiring/draining so concurrently-arriving items get
        // correct indices. AllItems replays from index 0 on every (re)connect; the
        // GrantedItemCount guard in ProcessPendingItems skips already-applied items.
        _nextItemIndex = 0;

        Session.Items.ItemReceived += APSession_ItemReceived;
        Session.MessageLog.OnMessageReceived += APSession_OnMessageReceived;
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

        ConnectionState = APConnectionState.Connected;
    }

    private static void APSession_SocketClosed(string reason) => HandleConnectionDropped(reason);

    /// <summary>Observes a fire-and-forget task so a fault is logged here instead of
    /// surfacing later as an unobserved TaskScheduler exception at GC time.</summary>
    private static void ObserveFault(Task task, string what) =>
        task?.ContinueWith(
            t => Plugin.Log.LogWarning($"[AP] {what} failed: {t.Exception?.Flatten().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);

    /// <summary>Detaches our handlers from a dead/abandoned session and aborts its socket so
    /// its polling loop exits instead of looping on errors. Safe to call with a null session.</summary>
    private static void DetachAndAbort(ArchipelagoSession dead)
    {
        if (dead == null) return;
        dead.Socket.SocketOpened  -= APSession_SocketOpened;
        dead.Items.ItemReceived -= APSession_ItemReceived;
        dead.MessageLog.OnMessageReceived -= APSession_OnMessageReceived;
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

        // Latch this first so the reconnect worker / SocketClosed handler stand down.
        _intentionalDisconnect = true;
        ConnectionState = APConnectionState.Disconnected;

        if (Session != null)
        {
            Session.Items.ItemReceived -= APSession_ItemReceived;
            Session.MessageLog.OnMessageReceived -= APSession_OnMessageReceived;
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

        // Deactivate the save redirect before restoring the vanilla profile so
        // LoadCurrentProfileData reads from the original paths.
        if (APSaveActive)
        {
            APSaveActive = false;
            SaveManager.ConfigData.CurrentProfile = _previousProfile;
            // Force the game to re-verify its save directories against the now-restored vanilla path
            // on the next save, mirroring what we do on connect. Guards against the reverse of the
            // AP-save-directory bug (solved via EnsureRedirectedSaveDirectories())
            // for a session that connected before any vanilla save occurred.
            ResetSaveDirectoryCheck();
            SaveManager.LoadCurrentProfileData();
            Plugin.Log.LogInfo("[AP] Save redirect deactivated; vanilla profile restored.");
        }

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
        SkillTreeManager.SetSkillObjLevel(slot.SkillTree, ManorUpgradeBundleSize, additive: false, runEvents: false);
        Plugin.Log.LogInfo($"[AP] Starting class applied: {slot.ItemName}");
        RunState.StartingClassApplied = true;
        RunState.Save(APSaveDirectoryName);
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
