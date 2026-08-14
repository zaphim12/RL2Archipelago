using HarmonyLib;
using Rewired;
using RL2Archipelago.Patches;
using System.Collections.Generic;
using UnityEngine;

namespace RL2Archipelago.UI;

/// <summary>
/// Draws colored connector lines between manor upgrade nodes, one color per branch of the tree,
/// rendered behind the node boxes. Archipelago hints name a manor location but not where it sits
/// on the grid, so following a branch's color from a node you recognise makes the layout legible.
///
/// <para>Toggled with whatever key or button the player has bound to Spin-Kick. See
/// <see cref="ManorBranchLayout"/> for the tuning knobs and <see cref="ManorTree"/> for how the
/// tree is derived.</para>
///
/// <para>Structure, parented under the window's <c>m_skillTreeIconsCanvasGroup</c>:</para>
/// <code>
/// AP_ManorBranchOverlay   sibling index 0, always active, carries this MonoBehaviour
///   └─ Lines              toggled on/off; carries the ManorBranchMesh that draws every connector
/// </code>
/// The MonoBehaviour deliberately lives on the always-active root: if it sat on the object being
/// toggled, <see cref="Update"/> would stop running once hidden and the overlay could never be
/// turned back on.
/// </summary>
internal class ManorBranchOverlay : MonoBehaviour
{
    private const string RootName  = "AP_ManorBranchOverlay";
    private const string LinesName = "Lines";

    /// <summary>On by default, remembered for the lifetime of the process (so it survives
    /// leaving and re-entering the manor) and reset on game restart.</summary>
    private static bool s_visible = true;

    /// <summary>The live overlay. It is a child of the window prefab, which the game destroys and
    /// re-instantiates on every scene load. Unity's overloaded <c>==</c> makes the destroyed
    /// object compare null, which is what triggers a rebuild against the new window.
    ///
    /// <para>Deliberately not cleared when the window merely closes: the GameObjects survive a
    /// close/reopen, so dropping the reference there would stack a duplicate overlay on every
    /// visit.</para></summary>
    private static ManorBranchOverlay s_instance;

    // The tree, flattened for ManorBranchMesh. _nodes and _colors run parallel: index i is the
    // slot the mesh's joint i was built from, and its branch color (RGB only. Alpha is
    // recomputed by RefreshAlpha).
    private readonly List<SkillTreeSlot> _nodes = new();
    private readonly List<Color> _colors = new();
    private readonly List<(int A, int B)> _links = new();
    private readonly List<Color> _tinted = new();

    private RectTransform _lines;
    private ManorBranchMesh _mesh;

    // Resolved Spin-Kick binding, refreshed on every window open so rebinds are picked up.
    private KeyCode _kbKey = KeyCode.None;
    private int _mouseElementId = -1;
    private int _padElementId = -1;

    // ── Entry points (called from ManorBranchOverlayPatch) ───────────────────

    /// <summary>Creates the overlay if needed and rebuilds every line from scratch.</summary>
    public static void Build(SkillTreeWindowController controller)
    {
        var container = Traverse.Create(controller).Field<CanvasGroup>("m_skillTreeIconsCanvasGroup").Value;
        if (container == null)
        {
            Plugin.Log.LogWarning("[ManorBranchOverlay] m_skillTreeIconsCanvasGroup not found; overlay skipped.");
            return;
        }

        // Reuse the overlay only if it still belongs to this window instance.
        if (s_instance == null || s_instance._lines == null || s_instance._mesh == null
            || s_instance.transform.parent != container.transform)
        {
            if (s_instance != null) Destroy(s_instance.gameObject);
            s_instance = Create(container.transform);
        }

        s_instance.Rebuild(controller);
    }

    /// <summary>Recomputes per-line alpha only. Cheap enough to run on every slot refresh.</summary>
    public static void RefreshAlpha()
    {
        if (s_instance == null) return;
        s_instance.RefreshAlphaInternal();
    }

    // ── Construction ─────────────────────────────────────────────────────────

    private static ManorBranchOverlay Create(Transform parent)
    {
        // Defensive sweep: destroy any overlay left behind that s_instance no longer tracks
        // (a plugin hot reload, say) so the container can never accumulate duplicates.
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name == RootName) Destroy(child.gameObject);
        }

        var rootGo = new GameObject(RootName, typeof(RectTransform));
        var rootRt = (RectTransform)rootGo.transform;
        rootRt.SetParent(parent, worldPositionStays: false);
        Stretch(rootRt);

        // uGUI draws siblings in order, so index 0 puts every line behind every node. Do NOT add
        // a nested Canvas with overrideSorting here. That would lift the lines above the slots.
        rootRt.SetAsFirstSibling();

        var linesGo = new GameObject(LinesName, typeof(RectTransform));
        var linesRt = (RectTransform)linesGo.transform;
        linesRt.SetParent(rootRt, worldPositionStays: false);
        Stretch(linesRt);

        var mesh = linesGo.AddComponent<ManorBranchMesh>();
        mesh.raycastTarget = false;               // never steal clicks or hovers from a slot

        var overlay = rootGo.AddComponent<ManorBranchOverlay>();
        overlay._lines = linesRt;
        overlay._mesh = mesh;
        return overlay;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private void Rebuild(SkillTreeWindowController controller)
    {
        _nodes.Clear();
        _colors.Clear();
        _links.Clear();

        ResolveToggleBinding();
        transform.SetAsFirstSibling();

        // includeInactive: true matters - the vanilla Initialize() omits it and so misses every
        // node the player has not revealed yet, which is exactly what we want lines for.
        var slots = controller.GetComponentsInChildren<SkillTreeSlot>(includeInactive: true);
        var tree = ManorTree.Build(slots);

        Canvas.ForceUpdateCanvases();

        var index = new Dictionary<SkillTreeType, int>();
        var joints = new List<ManorBranchMesh.Joint>();

        foreach (var (from, to) in tree.Edges)
            _links.Add((AddNode(from, tree, index, joints), AddNode(to, tree, index, joints)));

        _mesh.SetTree(joints, _links);

        _lines.gameObject.SetActive(s_visible);
        RefreshAlphaInternal();

        if (APSettings.DebugKeysEnabled) tree.Dump(_lines);
    }

    /// <summary>Registers a node with the mesh (or returns the index it already has) and records
    /// its position and branch color.</summary>
    private int AddNode(SkillTreeSlot slot, ManorTree tree,
                        Dictionary<SkillTreeType, int> index, List<ManorBranchMesh.Joint> joints)
    {
        if (index.TryGetValue(slot.SkillTreeType, out var existing)) return existing;

        // Measured in _lines' own space so the maths holds regardless of how the overlay or the
        // individual slots are anchored. Inactive GameObjects keep valid transform data, which is
        // what lets unrevealed nodes still get a (dimmed) line.
        Vector2 position = (Vector2)_lines.InverseTransformPoint(slot.transform.position)
                         + ManorBranchLayout.NudgeFor(slot.SkillTreeType);
        var color = tree.ColorFor(slot.SkillTreeType);

        index[slot.SkillTreeType] = _nodes.Count;
        _nodes.Add(slot);
        _colors.Add(color);
        joints.Add(new ManorBranchMesh.Joint { Pos = position, Color = color });

        return _nodes.Count - 1;
    }

    private void RefreshAlphaInternal()
    {
        if (_mesh == null) return;

        _tinted.Clear();

        for (int i = 0; i < _nodes.Count; i++)
        {
            // activeSelf, not activeInHierarchy: the game reveals a node by calling SetActive on
            // the slot itself, so activeSelf is precisely "has this node been revealed". Using
            // activeInHierarchy would also report false whenever an ancestor is temporarily
            // inactive (the garden transition hides the whole icon group), dimming everything.
            //
            // IsSlotUnlocked keeps that meaning intact under reveal_manor_upgrades, where every
            // node is active and activeSelf alone would light the whole tree up uniformly. It
            // returns true whenever the option is off, so this is a no-op by default.
            bool revealed = _nodes[i] != null
                         && _nodes[i].gameObject.activeSelf
                         && ManorUpgradePatch.IsSlotUnlocked(_nodes[i].SkillTreeType);

            // Alpha is per node rather than per line now, because the mesh blends each line
            // between its two endpoints. A line therefore dims as it approaches a node that is
            // not yet reachable instead of switching wholesale, and since the manor is only ever
            // unlocked outwards from the root, a reachable node never hangs off an unreachable one.
            var color = _colors[i];
            color.a = revealed ? ManorBranchLayout.FullAlpha : ManorBranchLayout.DimAlpha;
            _tinted.Add(color);
        }

        _mesh.SetJointColors(_tinted);
    }

    // ── The Spin-Kick toggle ─────────────────────────────────────────────────
    //
    // Spin-Kick is the Rewired action "Downstrike". While any window is open the game switches to
    // GameInputMode.Window (WindowManager.OpenWindow), which disables the Player action maps, so
    // Rewired_RL.Player.GetButtonDown("Downstrike") always returns false here. Instead we resolve
    // whatever element the player has bound and poll that hardware directly, which is unaffected
    // by which maps are enabled.
    //
    // Only the dedicated Downstrike binding toggles the overlay. Spin-Kick also fires on
    // Jump-while-holding-down and optionally on Attack (CharacterDownStrike_RL.HandleInput), but
    // Jump doubles as the manor's confirm button, so wiring it would toggle on every purchase.

    private void ResolveToggleBinding()
    {
        _kbKey = KeyCode.None;
        _mouseElementId = -1;
        _padElementId = -1;

        if (!ReInput.isReady) return;

        try
        {
            var kbm = Rewired_RL.GetActionElementMap(
                useGamepad: false, Rewired_RL.Action_Downstrike, Pole.Positive,
                prioritizeNonRemappable: false, controllerID: 0);

            if (kbm != null)
            {
                if (kbm.controllerMap.controllerType == ControllerType.Keyboard) _kbKey = kbm.keyCode;
                else if (kbm.controllerMap.controllerType == ControllerType.Mouse) _mouseElementId = kbm.elementIdentifierId;
            }

            int joystickId = RewiredOnStartupController.GetFirstAvailableJoystickControllerID();
            var action = ReInput.mapping.GetAction(Rewired_RL.Action_Downstrike);

            if (joystickId >= 0 && action != null)
            {
                // Remappable first, then the fixed map, which is the precedence
                // TextGlyphManager.CreateOrReplaceGamepadGlyphTable ends up with: it walks all four
                // categories in order and lets each overwrite the last, so a remappable binding
                // wins. Matching that is what keeps the glyph and this binding in agreement.
                _padElementId = TemplateElementFor(joystickId, action.id, Rewired_RL.MapCategoryType.ActionRemappable);

                if (_padElementId < 0)
                    _padElementId = TemplateElementFor(joystickId, action.id, Rewired_RL.MapCategoryType.Action);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[ManorBranchOverlay] Could not resolve the Spin-Kick binding; the toggle is disabled.\n{ex.Message}");
        }
    }

    /// <summary>The gamepad-TEMPLATE element an action is bound to in one map category, or -1.
    ///
    /// <para>The conversion is the whole point. A joystick <c>ControllerMap</c> holds element ids
    /// belonging to the physical controller, while the template exposes its own ids (its layout
    /// interleaves the trigger axes among the buttons, so the two disagree). Reading
    /// <c>ActionElementMap.elementIdentifierId</c> straight off the joystick map and then looking
    /// it up in the template silently resolves to the wrong physical button. Converting the map
    /// first, exactly as <c>TextGlyphManager.CreateAllGlyphTables</c> does, is what guarantees the
    /// button being polled is the one the on-screen glyph is showing.</para></summary>
    private static int TemplateElementFor(int joystickId, int actionId, Rewired_RL.MapCategoryType category)
    {
        var map = Rewired_RL.Player.controllers.maps.GetMap(
            ControllerType.Joystick, joystickId, Rewired_RL.GetMapCategoryID(category), 0);

        var templateMap = map?.ToControllerTemplateMap<IGamepadTemplate>();
        if (templateMap == null) return -1;

        foreach (var elementMap in templateMap.ElementMaps)
            if (elementMap.actionId == actionId) return elementMap.elementIdentifierId;

        return -1;
    }

    private bool ToggleWasPressed()
    {
        if (!ReInput.isReady) return false;

        if (_kbKey != KeyCode.None && ReInput.controllers.Keyboard.GetKeyDown(_kbKey)) return true;
        if (_mouseElementId >= 0 && ReInput.controllers.Mouse.GetButtonDownById(_mouseElementId)) return true;

        if (_padElementId >= 0)
        {
            var player = Rewired_RL.Player;
            if (player != null)
            {
                foreach (var joystick in player.controllers.Joysticks)
                {
                    // _padElementId is a gamepad-template element id (see TemplateElementFor), so
                    // the template is what has to be polled: it maps that id onto whichever
                    // physical element this particular controller uses. GetElement() takes an
                    // index rather than an id, hence the scan.
                    var template = joystick.GetTemplate<IGamepadTemplate>();
                    if (template == null) continue;

                    foreach (var element in template.elements)
                    {
                        if (element.id != _padElementId) continue;
                        if (element is IControllerTemplateButton button && button.justPressed) return true;
                        break;
                    }
                }
            }
        }

        return false;
    }

    private void Update()
    {
        if (_lines == null || !ToggleWasPressed()) return;

        s_visible = !s_visible;
        _lines.gameObject.SetActive(s_visible);
    }
}
