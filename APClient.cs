using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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

    /// <summary>Fired on the main thread after a successful login.</summary>
    public static event Action<ArchipelagoSession> OnSessionOpened;

    /// <summary>Fired on the main thread when a session is manually closed or the application is closed.</summary>
    public static event Action OnSessionClosed;

    // Item IDs received on the AP websocket thread; drained each Update() tick on
    // the Unity main thread so that game-state mutations are thread-safe.
    private static readonly ConcurrentBag<long> _pendingItems = new();

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

            // TODO: Sync Savedata current state with the server. 
            // This means updating the server of any locations which the client has checked and the server doesn't know
            // And also giving any items to the client which the server reports and client hasn't received yet

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

        Session = null;

        if (manual)
            OnSessionClosed?.Invoke();

        Plugin.Log.LogInfo("Disconnected from AP server.");
    }

    /// <summary>
    /// Called from <see cref="Plugin.Update"/> each frame to drain any item IDs
    /// received on the AP websocket thread and apply them to game state.
    /// </summary>
    public static void ProcessPendingItems()
    {
        while (_pendingItems.TryTake(out var itemId))
        {
            try { GrantItem(itemId); }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Exception granting item {itemId}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    private static void GrantItem(long itemId)
    {
        Plugin.Log.LogInfo($"[AP] Granting item ID {itemId}");
        // TODO: apply the item to the player (set save-data flag, grant ability, add gold, etc.)
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

    private static void APSession_ItemReceived(IReceivedItemsHelper receivedItemsHelper)
    {
        while (receivedItemsHelper.PeekItem() != null)
        {
            var item = receivedItemsHelper.DequeueItem();
            Plugin.Log.LogInfo($"[AP] Item received: {item.ItemName} (ID {item.ItemId})");
            _pendingItems.Add(item.ItemId);
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
