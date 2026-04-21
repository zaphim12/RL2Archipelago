using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RL2Archipelago.UI;

/// <summary>
/// Re-uses the vanilla "Insight Discovered" HUD (<see cref="ObjectiveCompleteHUDController"/>)
/// to surface Archipelago send/receive activity in-game.
///
/// <para>Notifications are queued and drained on the main thread one at a time, with a
/// fixed display window between each. The queue is bounded by <see cref="MaxQueueSize"/>
/// so a flood of incoming items (e.g. when another player completes their game and
/// releases everything) cannot pin the HUD on screen for minutes — extras are dropped.</para>
///
/// <para>Display only happens while a gameplay scene is loaded (the HUD MonoBehaviour
/// only exists then). Notifications enqueued from the title menu are silently dropped
/// rather than queued, so the user just sees no UI for them — items still apply.</para>
/// </summary>
internal static class APNotifications
{
    /// <summary>How long each notification stays on screen (matches the vanilla insight HUD).</summary>
    public const float DisplayDurationSec = 5f;

    /// <summary>Cap so a 80-item dump doesn't lock the HUD on screen for ~7 minutes.</summary>
    public const int MaxQueueSize = 6;

    /// <summary>
    /// When true (default), the vanilla insight discovery sting plays with each
    /// notification (the "Insight" UnityEvent on the HUD controller).
    /// Flip to false for a silent variant.
    /// </summary>
    public static bool PlaySfx { get; set; } = true;

    private struct Notification
    {
        public string Title;
        public string Subtitle;
        public string Description;
    }

    private static readonly Queue<Notification> _queue = new Queue<Notification>();
    private static float _nextDisplayTime;
    private static ObjectiveCompleteHUDController _cachedHud;

    /// <summary>Queues a notification for display the next time the HUD is free.
    /// When <paramref name="critical"/> is true, both the no-HUD and queue-full
    /// drops are bypassed so the notification is guaranteed to land — used for
    /// heirlooms and other items the player must not miss seeing.</summary>
    public static void Enqueue(string title, string subtitle, string description = null, bool critical = false)
    {
        // Drop silently when off the main game scene — no HUD MonoBehaviour to fire it on.
        // Critical notifications stay queued so they surface once gameplay resumes.
        if (GetHudController() == null && !critical)
        {
            Plugin.Log.LogDebug($"[AP] No HUD available; dropping notification: {title} / {subtitle}");
            return;
        }

        if (_queue.Count >= MaxQueueSize && !critical)
        {
            Plugin.Log.LogDebug(
                $"[AP] Notification queue full (max {MaxQueueSize}); dropping: {title} / {subtitle}");
            return;
        }

        _queue.Enqueue(new Notification
        {
            Title = title,
            Subtitle = subtitle,
            Description = description,
        });
    }

    /// <summary>Clears the queue and cached HUD reference. Call on disconnect.</summary>
    public static void Reset()
    {
        _queue.Clear();
        _nextDisplayTime = 0f;
        _cachedHud = null;
    }

    /// <summary>Drains the queue at one HUD per <see cref="DisplayDurationSec"/>.
    /// Must be called from the Unity main thread.</summary>
    public static void Tick()
    {
        if (_queue.Count == 0) return;
        if (Time.unscaledTime < _nextDisplayTime) return;

        var hud = GetHudController();
        if (hud == null) return;

        var n = _queue.Dequeue();

        // HeirloomDash is a known key in Insight_EV.LocIDTable, which suppresses
        // the red "Insight not found" Debug.Log the controller emits for unknown
        // values. The text overrides below replace the resulting strings entirely.
        var args = new InsightObjectiveCompleteHUDEventArgs(
            insightType: InsightType.HeirloomDash,
            discovered: false,
            displayDuration: DisplayDurationSec,
            titleTextOverride: n.Title,
            subtitleTextOverride: n.Subtitle,
            // The HUD only re-shows the description GameObject when it has visible
            // text. Pass a single space when caller had nothing to say so the layout
            // stays consistent rather than collapsing.
            descriptionTextOverride: string.IsNullOrEmpty(n.Description) ? " " : n.Description);

        if (PlaySfx)
            Broadcast(args);
        else
            BroadcastSilent(hud, args);

        _nextDisplayTime = Time.unscaledTime + DisplayDurationSec;
    }

    private static void Broadcast(InsightObjectiveCompleteHUDEventArgs args)
    {
        Messenger<UIMessenger, UIEvent>.Broadcast(
            UIEvent.DisplayObjectiveCompleteHUD, Plugin.Instance, args);
    }

    /// <summary>
    /// Fires the HUD with the SFX UnityEvent temporarily nulled, then restores it.
    /// Lets us reuse the visual without the audio sting.
    /// </summary>
    private static void BroadcastSilent(ObjectiveCompleteHUDController hud,
                                       InsightObjectiveCompleteHUDEventArgs args)
    {
        var field = Traverse.Create(hud).Field<UnityEvent>("m_insightCompleteUnityEvent");
        var saved = field.Value;
        field.Value = null;
        try
        {
            Broadcast(args);
        }
        finally
        {
            field.Value = saved;
        }
    }

    private static ObjectiveCompleteHUDController GetHudController()
    {
        // Unity's overloaded == treats destroyed objects as null, so this also
        // re-finds after a scene unload destroys the previous HUD instance.
        if (_cachedHud == null)
            _cachedHud = Object.FindObjectOfType<ObjectiveCompleteHUDController>();
        return _cachedHud;
    }
}
