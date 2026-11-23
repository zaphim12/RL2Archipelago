using System.Collections.Generic;
using System.Text;
using RL2Archipelago.Locations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.UI;

/// <summary>
/// A persistent panel showing what the player has left to do on the four biome-scoped check
/// categories: blueprint chests, fairy chests, journals and memories. Tracks for whichever biome the
/// player is standing in right now. 
///
/// <para>Rows read <c>collected / in logic / total</c> for the chest pools and
/// <c>collected / total</c> for journals and memories. Only the chest pools get a middle figure,
/// because <c>chest_pre_boss_percent</c> is the apworld's only per-slot rule; journal and memory
/// logic is a whole-category boolean, so a third number there would only ever repeat one of the
/// other two.</para>
///
/// <para>Each figure is coloured by what it means, not by its position: green for what the player
/// has, white for what is reachable now, grey for what is out of reach for the moment. So the final
/// figure is grey only while the in-logic count falls short of it, and a category whose every slot
/// is reachable reads white to the end. A category that is out of logic altogether also dims its
/// label, which keeps the figures free to carry their own meaning.</para>
///
/// <para>It is a list of what remains, not a scoreboard: a category drops off once it is finished,
/// and when a biome has nothing left the panel disappears entirely.</para>
///
/// <para>Built on the same self-contained pattern as <see cref="APTextLog"/> and
/// <see cref="APConnectionStatusOverlay"/>: a <see cref="RenderMode.ScreenSpaceOverlay"/> canvas on
/// a <c>DontDestroyOnLoad</c> GameObject, with black outline layers behind the coloured rich text,
/// polling state every frame in <see cref="Update"/>.</para>
///
/// <para>The panel is two text cells, not one per row: a numbers cell and a labels cell, each
/// holding every row as newline-separated lines. Both strings are composed in one pass over the
/// same ordered row list, so the two columns cannot fall out of register, and a category that does
/// not apply simply contributes no line rather than needing to be hidden.</para>
/// </summary>
internal class APBiomeTracker : MonoBehaviour
{
    // ── Layout ───────────────────────────────────────────────────────────────
    // Flush to the right screen edge like APConnectionStatusOverlay, and low enough to clear the
    // minimap HUD above it - the gold/ore readouts and the NG+ burden counter (MinimapHUDController
    // m_ngPlusText), whose own position lives in a prefab and so cannot be read from the
    // decompile. TopOffset is therefore an estimate and the first thing to nudge in game.
    //
    // This band overlaps APConnectionStatusOverlay (y -300..-340), which is harmless: the panel
    // stands down entirely while that banner is up. See CurrentBiomeIndex.
    private const float RightOffset  = -25f;  // near the right edge of the screen
    private const float TopOffset    = -310f; // below the minimap block and the burden counter
    private const float NumbersWidth = 130f;  // must fit "50 / 50 / 50" at FontSize, or the
                                              // numbers would run into the labels: the cells do
                                              // not clip, they overflow
    private const float ColumnGap    = 14f;   // breathing room between the two columns
    private const float LabelWidth   = 150f;  // must fit the longest label, "Fairy Chests"
    private const float BlockWidth   = NumbersWidth + ColumnGap + LabelWidth;
    private const float RowHeight    = 28f;   // estimated TMP line pitch; only sizes the rects
    private const int   MaxRows      = 4;     // one per category
    private const float FontSize     = 24f;   // matches APTextLog.FontSize
    private const float OutlineSize  = .5f;

    // ── Colours ──────────────────────────────────────────────────────────────
    private const string CollectedColor = "#66dd77"; // green, matching APTextLog.ItemColor
    private const string InLogicColor   = "#ffffff"; // white
    private const string TotalColor     = "#999999"; // grey; separators use it too
    private const string LabelColor     = "#cccccc"; // light grey
    private const string DimColor       = "#777777"; // whole row, when the category is out of logic

    // Backing field uses Unity's == so a destroyed object is treated as null.
    private static APBiomeTracker _instance;

    /// <summary>Creates the panel if it doesn't exist yet. Must be called from the Unity main
    /// thread (e.g. from <see cref="Patches.MainThreadDispatchPatch"/>).</summary>
    public static void EnsureExists()
    {
        if (_instance == null)
        {
            var go = new GameObject("RL2AP_BiomeTracker");
            DontDestroyOnLoad(go);
            go.AddComponent<APBiomeTracker>();
        }
    }

    private CanvasGroup _group;

    // Five layers per cell: four black outline copies, then the coloured rich-text one.
    private readonly List<TextMeshProUGUI> _numberLayers = new();
    private readonly List<TextMeshProUGUI> _labelLayers  = new();

    private string _currentNumbers;
    private string _currentLabels;

    // Recount bookkeeping. The biome is polled every frame (cheap), but the counting and the TMP
    // text assignment only happen on a biome change or once the throttle expires.
    private int   _lastBiomeIndex = -1;
    private float _lastRecount    = float.NegativeInfinity;
    private const float RecountInterval = 0.25f;

    private void Awake()
    {
        // enabled = false matters as well as Destroy: Destroy lands at end of frame, so Update
        // could otherwise still run once on this duplicate before its layers exist.
        if (_instance != null && _instance != this) { enabled = false; Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // same band as APTextLog; they never overlap
        gameObject.AddComponent<CanvasScaler>();

        BuildBlock();
        _group.alpha = 0f;
    }

    private void BuildBlock()
    {
        var root = new GameObject("BiomeTrackerBlock", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(BlockWidth, MaxRows * RowHeight);
        rt.anchoredPosition = new Vector2(RightOffset, TopOffset);

        _group = root.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        // Both columns are left-aligned, so each starts at a fixed x down the whole panel. The
        // numbers cell is sized for its worst case rather than its contents, which is what puts
        // ColumnGap's worth of clear space before the labels on every row.
        BuildCell(rt, _numberLayers, "Numbers", left: 0f,
                  width: NumbersWidth, height: MaxRows * RowHeight);

        BuildCell(rt, _labelLayers, "Labels", left: NumbersWidth + ColumnGap,
                  width: LabelWidth, height: MaxRows * RowHeight);
    }

    // One cell: four black outline copies at the corners, then the coloured layer on top. Matches
    // APTextLog so it renders regardless of TMP shader or font asset configuration.
    private static void BuildCell(
        RectTransform parent, List<TextMeshProUGUI> layers, string name,
        float left, float width, float height)
    {
        var cell = new GameObject(name, typeof(RectTransform));
        cell.transform.SetParent(parent, false);

        // Positioned from the block's top-left corner, so `left` reads as px in from it.
        var rt = (RectTransform)cell.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(left, 0f);

        layers.Clear();
        layers.Add(AddTextLayer(cell.transform, Color.black, new Vector2(-OutlineSize,  OutlineSize)));
        layers.Add(AddTextLayer(cell.transform, Color.black, new Vector2( OutlineSize,  OutlineSize)));
        layers.Add(AddTextLayer(cell.transform, Color.black, new Vector2(-OutlineSize, -OutlineSize)));
        layers.Add(AddTextLayer(cell.transform, Color.black, new Vector2( OutlineSize, -OutlineSize)));
        layers.Add(AddTextLayer(cell.transform, Color.white, Vector2.zero));
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
        tmp.alignment          = TextAlignmentOptions.TopLeft;

        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(pixelOffset.x, pixelOffset.y);
        r.offsetMax = new Vector2(pixelOffset.x, pixelOffset.y);

        return tmp;
    }

    private void Update()
    {
        // An Update that throws every frame would bury the BepInEx log, which is the artifact a
        // player attaches to a bug report. Degrade to "the tracker is missing" instead.
        try
        {
            Tick();
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError(
                $"[AP] Biome tracker disabled after an error:\n{ex.Message}\n{ex.StackTrace}");
            if (_group != null) _group.alpha = 0f;
            enabled = false;
        }
    }

    private void Tick()
    {
        int? biomeIndex = CurrentBiomeIndex();
        if (biomeIndex == null) { Hide(); return; }

        // Recount on a biome change, or when the throttle expires. Unscaled time, because the
        // pause menu holding the toggle for this panel runs at timeScale 0.
        bool biomeChanged = biomeIndex.Value != _lastBiomeIndex;
        if (!biomeChanged && Time.unscaledTime - _lastRecount < RecountInterval) return;

        _lastBiomeIndex = biomeIndex.Value;
        _lastRecount    = Time.unscaledTime;

        var progress = APBiomeProgress.For(biomeIndex.Value, APClient.RunState);
        if (progress.IsEmpty)
        {
            // Nothing left to track here. _lastBiomeIndex is kept so a finished or untracked biome
            // stays on the throttle instead of recounting every frame.
            _group.alpha = 0f;
            return;
        }

        Render(progress);
        _group.alpha = 1f;
    }

    /// <summary>
    /// The biome pool index the player is standing in, or <c>null</c> whenever the panel has no
    /// business being up: the setting is off, there is no AP run, the run state or slot data is not
    /// loaded yet, the connection overlay is using this corner, or the player is somewhere that is
    /// not one of the six biomes (hub, town, throne room, Garden of Eden, the manor, a menu).
    /// </summary>
    private static int? CurrentBiomeIndex()
    {
        if (!APSettings.ShowBiomeTracker) return null;

        // IsSessionActive rather than IsConnected: every figure shown comes from the local run
        // state, so the panel stays correct and useful while the socket is reconnecting.
        if (!APClient.IsSessionActive) return null;

        // Hard data dependency, and it also covers the connect window and teardown. Because slot
        // data is parsed before RunState is assigned, this transitively guarantees the gating
        // parameters APLogicSnapshot reads are populated.
        if (APClient.RunState == null) return null;

        // Both live in the top-right. Yielding is also the honest thing to do: a progress panel is
        // actively misleading next to "Progress is NOT being saved".
        if (APConnectionStatusOverlay.IsShowing) return null;

        // Mandatory, not defensive. GetCurrentPlayerRoom dereferences PlayerManager.Instance, whose
        // getter Debug.Logs a full stack trace and then returns null when the manager is not up -
        // which from an every-frame Update would flood the log and throw. IsInstantiated is a plain
        // field test. The gameplay patches that call GetCurrentPlayerRoom without this guard only
        // ever run when a world already exists.
        if (!PlayerManager.IsInstantiated) return null;

        var room = PlayerManager.GetCurrentPlayerRoom();
        if (room.IsNativeNull()) return null;

        // Sub-biomes (ForestTop, CaveMiddle, TowerExterior, …) resolve to their parent here, which
        // is why the recount compares resolved indices rather than BiomeType.
        return LocationRegistry.GetBiomeIndex(room.BiomeType);
    }

    private void Hide()
    {
        _group.alpha    = 0f;
        _lastBiomeIndex = -1;  // force a recount when the panel comes back
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private readonly StringBuilder _numbersPlain = new();
    private readonly StringBuilder _numbersRich  = new();
    private readonly StringBuilder _labelsPlain  = new();
    private readonly StringBuilder _labelsRich   = new();

    private void Render(APBiomeProgress progress)
    {
        _numbersPlain.Clear();
        _numbersRich.Clear();
        _labelsPlain.Clear();
        _labelsRich.Clear();

        // Fixed row order. Only the chest pools take a middle figure - see the class summary.
        AppendRow(progress.Blueprints,  "Blueprints",   showInLogic: true);
        AppendRow(progress.FairyChests, "Fairy Chests", showInLogic: true);
        AppendRow(progress.Journals,    "Journals",     showInLogic: false);
        AppendRow(progress.Memories,    "Memories",     showInLogic: false);

        SetCell(_numberLayers, _numbersPlain.ToString(), _numbersRich.ToString(), ref _currentNumbers);
        SetCell(_labelLayers,  _labelsPlain.ToString(),  _labelsRich.ToString(),  ref _currentLabels);
    }

    /// <summary>
    /// Appends one row to every builder, or nothing at all when the category has no row. Omitting
    /// on <c>!Present</c> covers every case at once: a configured chest count of zero, journals
    /// disabled, a biome with no memories, and a category the player has finished. So no row ever
    /// reads "0 / 0", and none lingers once it is done.
    /// </summary>
    private void AppendRow(APCategoryProgress category, string label, bool showInLogic)
    {
        if (!category.Present) return;

        if (_labelsPlain.Length > 0)
        {
            _numbersPlain.Append('\n');
            _numbersRich.Append('\n');
            _labelsPlain.Append('\n');
            _labelsRich.Append('\n');
        }

        // Grey on the total means "there is more here than you can reach yet"; once everything in
        // the category is in logic it goes white, like the in-logic figure itself. The two-figure
        // rows lean on this: their second figure is the total, and it is simultaneously the
        // in-logic count whenever the category is reachable at all, so it belongs in white.
        string totalColor = category.InLogic == category.Total ? InLogicColor : TotalColor;

        // Being out of logic dims the label only. The figures keep their own colours, so a row the
        // player has already made progress on still shows that progress in green - and the grey
        // total above is what says the rest is unreachable. There is no "complete" colour to apply:
        // a finished category has no row at all.
        _labelsPlain.Append(label);
        _labelsRich.Append($"<color={(category.Blocked ? DimColor : LabelColor)}>{label}</color>");

        if (showInLogic)
            AppendFigures(
                [CollectedColor, InLogicColor, totalColor],
                category.Collected, category.InLogic, category.Total);
        else
            AppendFigures(
                [CollectedColor, totalColor],
                category.Collected, category.Total);
    }

    /// <summary>
    /// Writes a row's figures as <c>a / b / c</c>, each tinted by what it means rather than by its
    /// position, which is what makes the row readable without a legend: green is what the player
    /// has, white is reachable now, grey is out of reach for the moment.
    /// </summary>
    private void AppendFigures(string[] colors, params int[] figures)
    {
        for (int i = 0; i < figures.Length; i++)
        {
            if (i > 0)
            {
                _numbersPlain.Append(" / ");
                _numbersRich.Append($" <color={TotalColor}>/</color> ");
            }

            _numbersPlain.Append(figures[i]);
            _numbersRich.Append($"<color={colors[i]}>{figures[i]}</color>");
        }
    }

    /// <summary>
    /// Rewrites a cell's layers. The last layer carries the colour markup; the outline copies stay
    /// plain so they render as a silhouette behind it. Guarded on the composed string so a steady
    /// panel never forces a TMP re-mesh, which is the only part of this that is not free.
    /// </summary>
    private static void SetCell(
        List<TextMeshProUGUI> layers, string plain, string rich, ref string last)
    {
        if (last == rich) return;
        last = rich;

        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i] == null) continue;
            layers[i].text = i == layers.Count - 1 ? rich : plain;
        }
    }
}
