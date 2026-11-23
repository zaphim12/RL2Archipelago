using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.UI;

/// <summary>
/// A persistent corner indicator for conditions the player needs to keep seeing, rather than be told
/// about once. Unlike <see cref="APTextLog"/> (which auto-fades each entry), this label stays up until
/// the condition clears.
///
/// <para>Two conditions, in priority order: the save system has stopped writing (the worse problem -
/// play continues but nothing is being kept), and the Archipelago connection has dropped and is
/// reconnecting.</para>
///
/// <para>Built on the same self-contained pattern as <see cref="APTextLog"/>: a
/// <see cref="RenderMode.ScreenSpaceOverlay"/> canvas on a <c>DontDestroyOnLoad</c>
/// GameObject, with black outline layers behind a white rich-text label. It polls state every frame
/// in <see cref="Update"/>, so no cross-thread marshaling is needed (the reconnect worker only flips
/// an enum).</para>
/// </summary>
internal class APConnectionStatusOverlay : MonoBehaviour
{
    // Position constants (top-right anchor). RightOffset is negative = px in from the right
    // screen edge; TopOffset is negative = px down from the top. Constants are set such that
    // the text sits just below the Minimap and optional "House Rules" text
    private const float RightOffset = -30f;
    private const float TopOffset   = -300f;
    private const float Width       = 460f;
    private const float Height      = 40f;
    private const float FontSize    = 22f;
    private const float OutlineSize = .5f;
    private const string WarnColor  = "#ff5555"; // red

    private const string ReconnectingMessage = "[!] Archipelago disconnected - reconnecting…";
    private const string SaveBlockedMessage  = "[!] Progress is NOT being saved - see the log";

    // Backing field uses Unity's == so a destroyed object is treated as null.
    private static APConnectionStatusOverlay _instance;

    // Latched while saving is blocked so the explanation is logged once per occurrence rather than
    // every frame. Static so recreating the overlay does not re-log. Cleared when the condition goes
    // away, so a second occurrence is reported again.
    private static bool _warnedSavingBlocked;

    /// <summary>Creates the overlay if it doesn't exist yet. Must be called from the
    /// Unity main thread (e.g. from <see cref="Patches.MainThreadDispatchPatch"/>).</summary>
    public static void EnsureExists()
    {
        if (_instance == null)
        {
            var go = new GameObject("RL2AP_ConnStatus");
            DontDestroyOnLoad(go);
            go.AddComponent<APConnectionStatusOverlay>();
        }
    }

    /// <summary>
    /// True while a banner is actually on screen. Read by <see cref="APBiomeTracker"/>, which
    /// shares this corner and stands down rather than risk drawing over the warning.
    /// </summary>
    internal static bool IsShowing => _instance != null && _instance._group.alpha > 0f;

    private CanvasGroup _group;

    // All five text layers, kept so the message can be swapped at runtime. The first four are the
    // black outline copies; the last is the colored one and takes rich-text markup.
    private readonly List<TextMeshProUGUI> _layers = new();
    private string _currentMessage;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 101; // just above APTextLog (100)
        gameObject.AddComponent<CanvasScaler>();

        BuildLabel();
        _group.alpha = 0f;
    }

    private void BuildLabel()
    {
        var root = new GameObject("ConnStatusEntry", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(Width, Height);
        rt.anchoredPosition = new Vector2(RightOffset, TopOffset);

        _group = root.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        // Outline: plain black text at the 4 corners, then colored rich text on top.
        // Matches APTextLog so it renders regardless of TMP shader/font configuration.
        _layers.Clear();
        _layers.Add(AddTextLayer(root.transform, Color.black, new Vector2(-OutlineSize,  OutlineSize)));
        _layers.Add(AddTextLayer(root.transform, Color.black, new Vector2( OutlineSize,  OutlineSize)));
        _layers.Add(AddTextLayer(root.transform, Color.black, new Vector2(-OutlineSize, -OutlineSize)));
        _layers.Add(AddTextLayer(root.transform, Color.black, new Vector2( OutlineSize, -OutlineSize)));
        _layers.Add(AddTextLayer(root.transform, Color.white, Vector2.zero));

        SetMessage(ReconnectingMessage);
    }

    /// <summary>
    /// Rewrites every layer. The last layer carries the color markup; the outline copies stay plain
    /// so they render as a silhouette behind it.
    /// </summary>
    private void SetMessage(string message)
    {
        if (_currentMessage == message) return;
        _currentMessage = message;

        for (int i = 0; i < _layers.Count; i++)
        {
            if (_layers[i] == null) continue;
            bool isForeground = i == _layers.Count - 1;
            _layers[i].text = isForeground ? $"<color={WarnColor}>{message}</color>" : message;
        }
    }

    private static TextMeshProUGUI AddTextLayer(Transform parent, Color color, Vector2 pixelOffset)
    {
        var go = new GameObject("TextLayer");
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize           = FontSize;
        tmp.color              = color;
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.alignment          = TextAlignmentOptions.MidlineRight; // hug the right screen edge

        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(4f + pixelOffset.x, 2f + pixelOffset.y);
        r.offsetMax = new Vector2(-4f + pixelOffset.x, -2f + pixelOffset.y);

        return tmp;
    }

    private void Update()
    {
        // A blocked save system outranks a dropped connection: the connection recovers on its own,
        // whereas play continuing with writes disabled silently throws the session away. The game
        // latches LoadingFailed and then refuses every save with nothing but a Debug.Log, so this
        // banner is the only thing the player would see.
        if (APClient.IsSessionActive && IsSavingBlocked())
        {
            WarnSavingBlockedOnce();
            SetMessage(SaveBlockedMessage);
            _group.alpha = 1f; // steady, not pulsing: this one is not "working on it"
            return;
        }

        _warnedSavingBlocked = false;

        if (APClient.ConnectionState == APConnectionState.Reconnecting)
        {
            SetMessage(ReconnectingMessage);
            // Gentle pulse so the indicator reads as "actively working", never fully hidden.
            _group.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f));
            return;
        }

        _group.alpha = 0f;
    }

    /// <summary>
    /// Writes the explanation to the log once per time the condition arises. The banner has room for
    /// one line; the log is where a player can actually be told what happened and where to read more,
    /// and it is what they will send along in a bug report.
    /// </summary>
    private static void WarnSavingBlockedOnce()
    {
        if (_warnedSavingBlocked) return;
        _warnedSavingBlocked = true;

        Plugin.Log.LogError(
            "[AP] The game has stopped writing save files, so nothing from this point on is being " +
            "kept. Your Archipelago progress is safe (checks and received items are tracked " +
            "separately and will be restored on the next connect), but the game's own save data is " +
            "not. Stop playing, quit to desktop, and connect again - the mod re-checks the save on " +
            "connect and will offer to restore from a backup save if one is needed and available. " +
            "See the Troubleshooting section of the README: " +
            "https://github.com/zaphim12/RL2Archipelago#troubleshooting");
    }

    /// <summary>
    /// True when the game will refuse to write save data. <c>DisableSaving</c> is excluded on
    /// purpose - the challenge-tunnel code toggles it legitimately during normal play.
    /// </summary>
    private static bool IsSavingBlocked() => SaveManager.LoadingFailed;
}
