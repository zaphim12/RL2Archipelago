using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.UI;

internal class APTextLog : MonoBehaviour
{
    private const float FadeInDuration  = 0.2f;
    private const float DisplayDuration = 5.0f;
    private const float FadeOutDuration = 0.7f;
    private const int   MaxEntries      = 3;
    private const float EntryWidth      = 340f;
    private const float EntryHeight     = 56f; // fits ~2-3 wrapped lines at 15pt
    private const float EntrySpacing    = 4f;
    private const float RightOffset     = -430f; // px from right screen edge, clears minimap
    private const float TopOffset       = -30f;  // px from top screen edge
    private const float FontSize        = 22f;
    private const float OutlineSize     = .5f;

    private const string ItemColor   = "#66dd77"; // green
    private const string PlayerColor = "#88aaff"; // blue

    // Backing field uses Unity's == so destroyed objects are treated as null.
    private static APTextLog _instance;

    // Always returns a valid instance, creating lazily during gameplay when Unity is ready.
    public static APTextLog Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("RL2AP_TextLog");
                DontDestroyOnLoad(go);
                go.AddComponent<APTextLog>();
            }
            return _instance;
        }
    }

    // For Reset(): clear without triggering lazy creation.
    public static void ClearIfExists()
    {
        if (_instance != null)
            _instance.Clear();
    }

    private class LogEntry
    {
        public GameObject    Root;
        public CanvasGroup   Group;
        public RectTransform Rt;
        public float TimeAlive;
        public float CurrentY;
        public float TargetY;
    }

    private readonly List<LogEntry> _entries = new();

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        gameObject.AddComponent<CanvasScaler>();
    }

    public void Add(string title, string subtitle, string description)
    {
        if (_entries.Count >= MaxEntries)
        {
            Destroy(_entries[0].Root);
            _entries.RemoveAt(0);
            RepositionTargets();
        }

        var entry = CreateEntry(FormatText(title, subtitle, description));
        _entries.Add(entry);
        RepositionTargets();
        entry.CurrentY = entry.TargetY;
    }

    public void Clear()
    {
        foreach (var e in _entries)
            Destroy(e.Root);
        _entries.Clear();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            e.TimeAlive += dt;

            float alpha;
            if (e.TimeAlive < FadeInDuration)
            {
                alpha = e.TimeAlive / FadeInDuration;
            }
            else if (e.TimeAlive < FadeInDuration + DisplayDuration)
            {
                alpha = 1f;
            }
            else
            {
                float fadeElapsed = e.TimeAlive - FadeInDuration - DisplayDuration;
                alpha = 1f - Mathf.Clamp01(fadeElapsed / FadeOutDuration);

                if (alpha <= 0f)
                {
                    Destroy(e.Root);
                    _entries.RemoveAt(i);
                    RepositionTargets();
                    continue;
                }
            }

            e.Group.alpha = alpha;
            e.CurrentY    = Mathf.Lerp(e.CurrentY, e.TargetY, dt * 10f);
            e.Rt.anchoredPosition = new Vector2(RightOffset, e.CurrentY);
        }
    }

    private void RepositionTargets()
    {
        for (int i = 0; i < _entries.Count; i++)
            _entries[i].TargetY = TopOffset - i * (EntryHeight + EntrySpacing);
    }

    private LogEntry CreateEntry(string richText)
    {
        var root = new GameObject("APLogEntry", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(EntryWidth, EntryHeight);

        var group = root.AddComponent<CanvasGroup>();
        group.alpha          = 0f;
        group.blocksRaycasts = false;
        group.interactable   = false;

        // Outline: render plain black text at 4 corners, then colored rich text on top.
        // This works regardless of TMP shader or font asset configuration.
        string plain = StripTags(richText);
        AddTextLayer(root.transform, plain, Color.black, new Vector2(-OutlineSize,  OutlineSize));
        AddTextLayer(root.transform, plain, Color.black, new Vector2( OutlineSize,  OutlineSize));
        AddTextLayer(root.transform, plain, Color.black, new Vector2(-OutlineSize, -OutlineSize));
        AddTextLayer(root.transform, plain, Color.black, new Vector2( OutlineSize, -OutlineSize));
        AddTextLayer(root.transform, richText, Color.white, Vector2.zero);

        return new LogEntry { Root = root, Group = group, Rt = rt };
    }

    private static void AddTextLayer(Transform parent, string text, Color color, Vector2 pixelOffset)
    {
        var go  = new GameObject("TextLayer");
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = FontSize;
        tmp.color              = color;
        tmp.enableWordWrapping = true;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;

        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(4f + pixelOffset.x, 2f + pixelOffset.y);
        r.offsetMax = new Vector2(-4f + pixelOffset.x, -2f + pixelOffset.y);
    }

    private static string StripTags(string richText)
    {
        var sb    = new System.Text.StringBuilder(richText.Length);
        bool inTag = false;
        foreach (char c in richText)
        {
            if      (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag)   sb.Append(c);
        }
        return sb.ToString();
    }

    private static string FormatText(string title, string subtitle, string description)
    {
        string item   = $"<color={ItemColor}>{subtitle}</color>";
        string player(string name) => $"<color={PlayerColor}>{name}</color>";

        return title switch
        {
            "Item Found" =>
                $"Sent {item} to {player("yourself")}",

            "Item Sent" when description?.StartsWith("Sent to ") == true =>
                $"Sent {item} to {player(description.Substring(8))}",

            "Item Received" or "Trap!" when description?.StartsWith("Sent by ") == true =>
                $"Received {item} from {player(description.Substring(8))}",

            "Death Link!" =>
                $"Death Link received from {player(subtitle)}",

            _ => string.IsNullOrWhiteSpace(description)
                    ? $"{title}: {subtitle}"
                    : $"{title}: {subtitle} ({description})",
        };
    }
}
