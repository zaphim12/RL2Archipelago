using HarmonyLib;
using RL_Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.UI;

/// <summary>
/// Drives the game's own burden picker (<see cref="WindowID.NewGamePlusNPC"/>) for Archipelago runs.
///
/// <para>Vanilla, that window is reachable only by talking to the Elpis at the docks. An AP run never
/// takes that path, so without this the player has no way to add burdens. We reuse the window as a
/// standalone difficulty picker: shown once automatically on the first connect to a seed, and reopenable
/// afterwards only through a debug key.</para>
///
/// <para>Opening it is cheap - <c>NewGamePlusShop.DisplayShopWindowCoroutine</c> is just load, wait a
/// frame, open, with no dependency on the NPC, the room or the scene, and <c>WindowManager</c> is a
/// <c>DontDestroyOnLoad</c> singleton holding every window prefab. What is <em>not</em> cheap is the
/// window's assumption that it is being used to start a NG+; <see cref="Patches.BurdenSelectionPatches"/>
/// neutralizes that while <see cref="Active"/> is set.</para>
///
/// <para>Burden levels need no persistence of their own: they live in
/// <c>PlayerSaveData.BurdenObjTable</c>, which <c>Patches.APSavePatch</c> already scopes per seed.
/// <see cref="BurdenDefaults"/> separately remembers the chosen set machine-wide so the next new run
/// doesn't require the player to re-set all their preferred burdens.</para>
/// </summary>
/// <summary>Why the picker is being shown, which decides how it behaves.</summary>
internal enum BurdenPickerMode
{
    /// <summary>
    /// The one automatic prompt per seed. Pre-fills from the remembered defaults, and refuses to
    /// close until the player confirms so that the player must acknowledge whether they are adding
    /// burdens or not. We want the acknowledgement because burdens are semi-permanent and we don't
    /// want a player to accidentally apply/not-apply them when spamming through prompts.
    /// </summary>
    FirstRun,

    /// <summary>
    /// A deliberate reopen through the debug key, mid-run. The save is already authoritative so
    /// nothing is pre-filled, and backing out is allowed because the player chose to be here.
    /// </summary>
    Revisit,
}

internal static class BurdenSelection
{
    /// <summary>
    /// True while our picker owns the window. Read by <see cref="Patches.BurdenSelectionPatches"/> to
    /// decide whether to suppress the window's NG+ behavior; the vanilla docks interaction must keep
    /// working untouched, so none of those patches fire unless this is set.
    /// </summary>
    internal static bool Active { get; private set; }

    /// <summary>
    /// Whether Escape / B may dismiss the picker. False for <see cref="BurdenPickerMode.FirstRun"/>
    /// only: that prompt requires the player to make a choice. 
    /// On a revisit it hands Escape to the window's own handler, which offers to discard.
    /// </summary>
    internal static bool CanCancel { get; private set; }

    /// <summary>
    /// True when the player left through Confirm, which is the only way to keep their edits.
    ///
    /// <para>This is what decides save-versus-discard, and it does so by choosing whether to
    /// suppress the window's own <c>OnClose</c> restore loop. That loop is the game's discard
    /// mechanism which rolls every burden back to the snapshot <c>OnOpen</c> took. So discarding is
    /// a matter of letting it run rather than anything we implement.</para>
    /// </summary>
    internal static bool ConfirmedClose { get; private set; }

    /// <summary>
    /// Closes the picker keeping the player's edits. The Confirm button's only job.
    /// </summary>
    internal static void CloseAndSave()
    {
        ConfirmedClose = true;
        WindowManager.SetWindowIsOpen(WindowID.NewGamePlusNPC, isOpen: false);
    }

    /// <summary>
    /// Set by <see cref="APClient"/> on the first connect to a seed. Consumed by
    /// <see cref="Patches.MenuPatch"/> once the main menu has focus again, because the connect path
    /// runs a transition that closes and rebuilds the menu and would tear down a window opened before
    /// it.
    /// </summary>
    internal static bool PendingOpen { get; set; }

    // The window needs a frame between being instantiated and being opened (the game's own shop
    // coroutine yields once for the same reason). There is no MonoBehaviour here to run a coroutine
    // on, and Plugin.Update() does not fire in this game, so the wait is a countdown ticked from the
    // WindowManager.Update postfix - the same pump APSaveRecovery and APNotifications use.
    private static int _framesUntilOpen = -1;

    // Dimming layer between the picker and whatever is behind it. Also swallows mouse clicks, which
    // is half of keeping the main menu from staying interactive underneath; the other half is
    // m_lockInput below.
    private static GameObject _backdrop;

    // Window furniture switched off while the picker is up, restored on close so the vanilla docks
    // interaction is unaffected if it ever opens this same window instance.
    private static readonly List<GameObject> _hidden = new();

    // The picker's own speech bubble controller, so the patches that keep its text overwritten can
    // tell it apart from the blacksmith's and the other shops'.
    private static PurchaseBoxDialogueController _bubble;

    /// <summary>
    /// Shows the burden picker. <paramref name="mode"/> carries both of the differences between the
    /// forced first prompt and a debug-key reopen, so the two can never be set inconsistently.
    /// </summary>
    internal static void Open(BurdenPickerMode mode)
    {
        if (Active || _framesUntilOpen >= 0) return;

        bool applyDefaults = mode == BurdenPickerMode.FirstRun;
        CanCancel      = mode != BurdenPickerMode.FirstRun;
        ConfirmedClose = false;

        try
        {
            if (SaveManager.PlayerSaveData == null)
            {
                Plugin.Log.LogWarning("[AP] Cannot open the burden picker: no save data is loaded.");
                return;
            }

            if (!WindowManager.IsInstantiated)
            {
                Plugin.Log.LogWarning("[AP] Cannot open the burden picker: WindowManager is not ready.");
                return;
            }

            // Without this, IsBurdenUnlocked gates every *BossUp and *BiomeUp burden behind
            // HighestNGPlusBeaten: they render as "???" and their levels read back as 0 even once set.
            // Deliberately not also setting TimesBeatenTraitor, which the vanilla "Unlock All Burdens"
            // purchase does and which means something else entirely.
            SaveManager.PlayerSaveData.BurdenNGUnlocked = true;

            if (applyDefaults)
                BurdenDefaults.ApplyToSave();

            if (!WindowManager.GetIsWindowLoaded(WindowID.NewGamePlusNPC))
                WindowManager.LoadWindow(WindowID.NewGamePlusNPC);

            _framesUntilOpen = 1;
        }
        catch (Exception ex)
        {
            _framesUntilOpen = -1;
            Plugin.Log.LogError($"[AP] Failed to open the burden picker:\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>Pumped every frame from <see cref="Patches.MainThreadDispatchPatch"/>.</summary>
    internal static void Tick()
    {
        if (_framesUntilOpen < 0)
        {
            // Safety net for a window that went away without OnClose running - a scene change can
            // unload it outright. NotifyClosed clears Active first, so this cannot double-commit.
            if (Active && !WindowManager.GetIsWindowOpen(WindowID.NewGamePlusNPC))
            {
                Plugin.Log.LogWarning("[AP] The burden picker vanished without closing; committing anyway.");
                NotifyClosed();
            }
            return;
        }

        if (_framesUntilOpen-- > 0) return;

        try
        {
            var controller = WindowManager.GetWindowController(WindowID.NewGamePlusNPC);
            if (controller == null)
            {
                Plugin.Log.LogError("[AP] The burden picker window failed to load.");
                return;
            }

            // Set before opening: OnOpen runs synchronously inside SetWindowIsOpen, and the patches
            // that keep it from behaving like a NG+ screen read Active.
            Active = true;
            WindowManager.SetWindowIsOpen(WindowID.NewGamePlusNPC, isOpen: true);

            // After opening, so the window has its sort order assigned and the backdrop can slot in
            // underneath it.
            ShowBackdrop(controller);
            SetMainMenuInputLocked(true);
            HidePurchaseBoxExtras(controller);

            Plugin.Log.LogInfo("[AP] Burden picker opened.");
        }
        catch (Exception ex)
        {
            Active = false;
            HideBackdrop();
            RestoreHidden();
            SetMainMenuInputLocked(false);
            Plugin.Log.LogError($"[AP] Failed to show the burden picker:\n{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            _framesUntilOpen = -1;
        }
    }

    /// <summary>
    /// Commits the player's choices. Called from the <c>OnClose</c> postfix in
    /// <see cref="Patches.BurdenSelectionPatches"/>, deliberately <em>not</em> from
    /// <c>WindowClosedEvent</c>: <c>WindowController.SetIsOpen</c> fires that event <em>before</em>
    /// calling <c>OnClose</c>, so committing there would clear <see cref="Active"/> while the
    /// window's own burden-restoring <c>OnClose</c> was still to come, undoing
    /// everything the player just picked.
    /// </summary>
    internal static void NotifyClosed()
    {
        // Read before clearing: the window's OnClose has already run by this point, so on a discard
        // the burden levels are back at the snapshot and there is nothing to persist.
        bool saved = ConfirmedClose;

        Active         = false;
        ConfirmedClose = false;
        HideBackdrop();
        RestoreHidden();
        SetMainMenuInputLocked(false);

        try
        {
            // DeathMark is mirrored into a relic, and PlayerSaveData only re-syncs that mirror inside
            // ResetForNewGamePlus which an AP run never reaches. Do it here or the burden is inert.
            // Runs on both paths: it mirrors whatever the level ended up being, reverted or not.
            SyncDeathMarkRelic();

            if (!saved)
            {
                Plugin.Log.LogDebug("[AP] Burden picker dismissed. Changes discarded.");
                return;
            }

            BurdenDefaults.CaptureFromSave();

            if (APClient.RunState != null && !APClient.RunState.BurdensChosen)
            {
                APClient.RunState.BurdensChosen = true;
                APClient.RunState.Save(APClient.APSaveDirectoryName);
            }

            // Burden levels are part of PlayerSaveData, so they are only as safe as the last profile
            // write. force: true because the debounce exists to coalesce bursts of item grants, and
            // skipping this one would leave a deliberate player choice in memory only; at the main
            // menu nothing else may save for a long time.
            APClient.RequestProfileSave(force: true);

            Plugin.Log.LogInfo(
                $"[AP] Burden picker closed. Total burden level: {BurdenManager.GetTotalBurdenLevel()}.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to commit burden selection:\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    // ── Focus and framing ───────────────────────────────────────────────────

    /// <summary>
    /// Dims everything behind the picker and swallows clicks aimed at it.
    ///
    /// <para>This goes inside the window's own canvas as its first child rather than onto a canvas
    /// of its own. A standalone canvas has to reproduce four things correctly - render mode, the UI
    /// camera, the sorting order and, most easily missed, the <em>layer</em>, since
    /// <c>CameraController.UICamera</c> culls by layer and anything left on the default layer is
    /// simply never drawn. Parenting inherits all of it, and within one canvas the draw order is
    /// just the hierarchy order, so being first sibling puts it behind the window's content.</para>
    /// </summary>
    private static void ShowBackdrop(WindowController controller)
    {
        if (_backdrop != null) return;

        var windowCanvas = controller.WindowCanvas;
        if (windowCanvas == null) return;

        _backdrop = new GameObject("RL2AP_BurdenBackdrop", typeof(RectTransform));
        _backdrop.layer = windowCanvas.gameObject.layer;
        _backdrop.transform.SetParent(windowCanvas.transform, false);
        _backdrop.transform.SetAsFirstSibling();

        // A ScreenSpaceCamera canvas's RectTransform always matches the camera viewport, so
        // stretching to it fills the screen at any resolution.
        var rt = (RectTransform)_backdrop.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var image = _backdrop.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.75f);
        image.raycastTarget = true; // absorbs clicks aimed at the menu behind
    }

    /// <summary>
    /// Replaces Elpis' flavour line, which is vanilla New Game+ chatter
    /// ("Off to a new thread, traveler?") as the only text left in the bubble once the two readouts
    /// below are hidden. Deliberately short: the bubble is small, and the full explanation already
    /// sits in the description panel.
    /// </summary>
    internal const string BubbleText = "Set your burdens for this Archipelago run, then hit Confirm. " + 
    " These choices will last for the whole run.";

    /// <summary>
    /// True when <paramref name="bubble"/> is the picker's own speech bubble. The controller type is
    /// shared with the blacksmith, enchantress and soul shop, so patches on it match the exact
    /// instance rather than trusting that ours is the only one listening.
    /// </summary>
    internal static bool OwnsBubble(PurchaseBoxDialogueController bubble) =>
        Active && bubble != null && bubble == _bubble;

    /// <summary>
    /// Writes <see cref="BubbleText"/> into the bubble. Called when the picker opens, and again from
    /// the patches on the two methods that rewrite the line afterwards.
    /// </summary>
    internal static void ApplyBubbleText()
    {
        if (_bubble == null) return;

        try
        {
            var text = Traverse.Create(_bubble).Field("m_purchaseBoxDialogueText").GetValue<TMP_Text>();
            if (text != null) text.text = BubbleText;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] Could not set the burden picker's bubble text: {ex.Message}");
        }
    }

    /// <summary>
    /// Strips Elpis' speech bubble down to just its flavour line, and rewrites that line.
    ///
    /// <para>The bubble (the "Purchase Box") carries two readouts under a horizontal rule: burden
    /// weight against the amount a New Game+ would require, and the burden gold bonus. Neither means
    /// anything here; an AP run never enters a NG+, and the gold bonus is suppressed outright. So
    /// the horizontal rule is left dividing the flavour text from nothing and must also be removed.</para>
    ///
    /// <para>The two readouts are <c>BaseOmniUIPurchaseBoxEntry</c> components told apart by a
    /// protected nested enum (<c>None, FlavourText, MoneyOwned, MoneyCost</c>), which is not
    /// referenceable from here, so they are matched on the underlying int. The rule is a plain
    /// sibling <c>Image</c>.</para>
    /// </summary>
    private static void HidePurchaseBoxExtras(WindowController controller)
    {
        const int MoneyOwned = 2; // the gold bonus line
        const int MoneyCost  = 3; // "<weight>/<required>"

        try
        {
            var entries = controller.GetComponentsInChildren<NewGamePlusOmniUIPurchaseBoxEntry>(includeInactive: true);
            if (entries.Length == 0) return;

            foreach (var entry in entries)
            {
                var type = Traverse.Create(entry).Field("m_descriptionType").GetValue();
                if (type == null) continue;

                int value = Convert.ToInt32(type);
                if (value == MoneyOwned || value == MoneyCost) Hide(entry.gameObject);
            }

            var bubble = CommonAncestor(entries.Select(e => e.transform).ToArray());
            if (bubble == null) return;

            var line = bubble.Find("Horizontal Line");

            // Fall back to matching the sprite, so a rename in a game patch degrades to a no-op
            // rather than silently hiding the wrong object.
            if (line == null)
            {
                for (int i = 0; i < bubble.childCount; i++)
                {
                    var image = bubble.GetChild(i).GetComponent<Image>();
                    if (image != null && image.sprite != null &&
                        image.sprite.name.IndexOf("PurchaseBoxLine", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        line = bubble.GetChild(i);
                        break;
                    }
                }
            }

            if (line != null) Hide(line.gameObject);

            _bubble = bubble.GetComponentInParent<PurchaseBoxDialogueController>()
                      ?? bubble.GetComponentInChildren<PurchaseBoxDialogueController>(includeInactive: true);
            ApplyBubbleText();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] Could not tidy the burden picker's speech bubble: {ex.Message}");
        }
    }

    private static void Hide(GameObject go)
    {
        if (go == null || !go.activeSelf) return;

        go.SetActive(false);
        _hidden.Add(go);
    }

    private static void RestoreHidden()
    {
        foreach (var go in _hidden)
        {
            if (go != null) go.SetActive(true);
        }
        _hidden.Clear();

        // The bubble's own text is left as-is; the next thing to broadcast a dialogue event
        // rewrites it, and dropping the reference is what stops our patches firing for it.
        _bubble = null;
    }

    private static Transform CommonAncestor(Transform[] transforms)
    {
        var candidate = transforms[0];

        while (candidate != null)
        {
            bool containsAll = true;
            foreach (var t in transforms)
            {
                if (!t.IsChildOf(candidate)) { containsAll = false; break; }
            }

            if (containsAll) return candidate;
            candidate = candidate.parent;
        }

        return null;
    }


    private static void HideBackdrop()
    {
        if (_backdrop == null) return;

        UnityEngine.Object.Destroy(_backdrop);
        _backdrop = null;
    }

    /// <summary>
    /// The main menu subscribes its navigation and confirm handlers to Rewired directly, so they keep
    /// firing while another window holds focus - the menu stays keyboard-navigable behind whatever is
    /// on top of it. <c>m_lockInput</c> is the game's own switch for that, and is what the connection
    /// dialog uses for the same reason (see <c>MenuPatch.OnArchipelagoButtonClicked</c>).
    ///
    /// <para>No-ops when the main menu is not loaded, which is the case for a debug-key reopen
    /// during a run.</para>
    /// </summary>
    private static void SetMainMenuInputLocked(bool locked)
    {
        try
        {
            if (!WindowManager.IsInstantiated || !WindowManager.GetIsWindowLoaded(WindowID.MainMenu))
                return;

            var mainMenu = WindowManager.GetWindowController(WindowID.MainMenu);
            if (mainMenu == null) return;

            Traverse.Create(mainMenu).Field("m_lockInput").SetValue(locked);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] Could not toggle main menu input lock: {ex.Message}");
        }
    }

    private static void SyncDeathMarkRelic()
    {
        var relics = SaveManager.PlayerSaveData?.RelicObjTable;
        if (relics == null || !relics.TryGetValue(RelicType.DeathMark, out var relic) || relic == null)
            return;

        relic.SetLevel(BurdenManager.GetBurdenLevel(BurdenType.DeathMark), additive: false);
    }
}
