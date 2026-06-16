using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.UI;

/// <summary>
/// A persistent corner indicator that stays on screen whenever the Archipelago
/// connection has dropped and the client is attempting to reconnect. Unlike
/// <see cref="APTextLog"/> (which auto-fades each entry), this label persists until
/// <see cref="APClient.ConnectionState"/> leaves <see cref="APConnectionState.Reconnecting"/>.
///
/// <para>Built on the same self-contained pattern as <see cref="APTextLog"/>: a
/// <see cref="RenderMode.ScreenSpaceOverlay"/> canvas on a <c>DontDestroyOnLoad</c>
/// GameObject, with black outline layers behind a white rich-text label. It polls
/// <see cref="APClient.ConnectionState"/> every frame in <see cref="Update"/>, so no
/// cross-thread marshaling is needed (the reconnect worker only flips the enum).</para>
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

    private const string Message = "[!] Archipelago disconnected - reconnecting…";

    // Backing field uses Unity's == so a destroyed object is treated as null.
    private static APConnectionStatusOverlay _instance;

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

    private CanvasGroup _group;

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
        AddTextLayer(root.transform, Message, Color.black, new Vector2(-OutlineSize,  OutlineSize));
        AddTextLayer(root.transform, Message, Color.black, new Vector2( OutlineSize,  OutlineSize));
        AddTextLayer(root.transform, Message, Color.black, new Vector2(-OutlineSize, -OutlineSize));
        AddTextLayer(root.transform, Message, Color.black, new Vector2( OutlineSize, -OutlineSize));
        AddTextLayer(root.transform, $"<color={WarnColor}>{Message}</color>", Color.white, Vector2.zero);
    }

    private static void AddTextLayer(Transform parent, string text, Color color, Vector2 pixelOffset)
    {
        var go = new GameObject("TextLayer");
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
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
    }

    private void Update()
    {
        if (APClient.ConnectionState == APConnectionState.Reconnecting)
        {
            // Gentle pulse so the indicator reads as "actively working", never fully hidden.
            _group.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f));
        }
        else
        {
            _group.alpha = 0f;
        }
    }
}
