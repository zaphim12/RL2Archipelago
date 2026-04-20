using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using RL2Archipelago.Items;
using RL2Archipelago.Locations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RL2Archipelago;

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

    // Profile slot that was active before AP mode was entered; restored on disconnect.
    private static byte _previousProfile;

    /// <summary>Fired on the main thread after a successful login.</summary>
    public static event Action<ArchipelagoSession> OnSessionOpened;

    /// <summary>Fired on the main thread when a session is manually closed or the application is closed.</summary>
    public static event Action OnSessionClosed;

    // (index, itemId) pairs received on the AP websocket thread; drained each
    // Update() tick on the Unity main thread so game-state mutations are thread-safe.
    private struct PendingItem { public int Index; public long ItemId; }
    private static readonly ConcurrentQueue<PendingItem> _pendingItems = new();

    // Tracks the next item index to assign. Reset to 0 on each connect because
    // AllItems causes the server to replay from index 0 on every reconnect.
    // Tracked locally because the server's index stores the total number of items,
    // not the index of the item's being received. So when receiving items after a reconnect,
    // the server's index will be higher than the index of the item being received until we catch up.
    private static int _nextItemIndex = 0;

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
            Plugin.Log.LogInfo(
                $"Connected! Room: {Session.RoomState.Seed}  " +
                $"Slot data keys: {string.Join(", ", success.SlotData.Keys)}");

            // Persist the room ID so we can warn on multiworld mismatch later.
            connData.RoomId = Session.RoomState.Seed;

            // Register websocket-thread event handlers.
            Session.Items.ItemReceived += APSession_ItemReceived;
            Session.MessageLog.OnMessageReceived += APSession_OnMessageReceived;

            // Redirect all save I/O to a directory scoped to this room + slot.
            _previousProfile = SaveManager.ConfigData.CurrentProfile;
            APSaveDirectoryName = SanitizeDirectoryName($"{connData.RoomId}_{connData.SlotName}");
            APSaveActive = true;
            SaveManager.ConfigData.CurrentProfile = 0;
            SaveManager.LoadCurrentProfileData();
            Plugin.Log.LogInfo($"[AP] Save redirected to AP_Saves/{APSaveDirectoryName}");

            // Load any prior run state (checked locations, etc.) for this seed+slot.
            RunState = APRunState.Load(APSaveDirectoryName);
            _nextItemIndex = 0;

            // Reconcile local and server state. If the client recorded a check
            // that never made it to the server (e.g. network drop mid-send),
            // resend it now so the multiworld stays consistent.
            // Item resync is handled automatically: AllItems causes the server to
            // replay every received item from index 0 on each connect, and
            // ProcessPendingItems skips anything below GrantedItemCount.
            ResyncCheckedLocations();

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

        // Deactivate the save redirect before restoring the vanilla profile so
        // LoadCurrentProfileData reads from the original paths.
        APSaveActive = false;
        SaveManager.ConfigData.CurrentProfile = _previousProfile;
        SaveManager.LoadCurrentProfileData();
        Plugin.Log.LogInfo("[AP] Save redirect deactivated; vanilla profile restored.");

        RunState = null;
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
                $"[AP] SendLocationCheck({locationId}) called while no run is active — ignoring.");
            return;
        }

        var displayName = LocationRegistry.Names.TryGetValue(locationId, out var n) ? n : locationId.ToString();

        // Persist first, then send. If the send is dropped, Resync will retry it.
        if (!RunState.CheckedLocations.Add(locationId))
        {
            Plugin.Log.LogDebug($"[AP] Location '{displayName}' already checked — skipping re-send.");
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
                $"[AP] Location '{displayName}' (ID {locationId}) is not in this slot's location list — ensure the apworld defines it.");

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
    /// Called from <see cref="Plugin.Update"/> each frame to drain any item IDs
    /// received on the AP websocket thread and apply them to game state.
    /// </summary>
    public static void ProcessPendingItems()
    {
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

        Plugin.Log.LogWarning($"[AP] No handler for item '{displayName}' (ID {itemId}) — ignoring.");
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
        Plugin.Log.LogInfo("[AP] Socket opened — PollingLoop started.");
    }

    private static void APSession_ItemReceived(IReceivedItemsHelper helper)
    {
        while (helper.PeekItem() != null)
        {
            var item = helper.DequeueItem();
            int itemIndex = _nextItemIndex++;
            Plugin.Log.LogDebug($"[AP] Item queued: {item.ItemName} (ID {item.ItemId}) at index {itemIndex}");
            _pendingItems.Enqueue(new PendingItem { Index = itemIndex, ItemId = item.ItemId });
        }
    }

    private static void APSession_OnMessageReceived(LogMessage message)
    {
        Plugin.Log.LogInfo($"[AP] {message}");
        // TODO: Surface this in an in-game console / chat overlay.
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
