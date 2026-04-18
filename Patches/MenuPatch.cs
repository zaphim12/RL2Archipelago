using HarmonyLib;
using RL_Windows;
using SceneManagement_RL;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Injects an "Archipelago" button into Rogue Legacy 2's main menu.
///
/// Strategy:
///   Postfix MainMenuWindowController.Initialize() — called once when the window is
///   first loaded.  At that point m_menuButtonList is fully populated by
///   GetComponentsInChildren().  We clone one of the existing buttons, change its
///   text, clear its inherited event handlers, wire up the controller's private
///   UpdateSelectedOptionItem / PlaySelectedSFX delegates so keyboard/controller
///   navigation works correctly, then append it to the list.
///
/// The injected button's m_selectorType stays None, so ExecuteButton()'s switch
/// statement is a no-op.  We subscribe our own AP action via MenuButtonActivated,
/// which is invoked *before* the switch in ExecuteButton().
/// </summary>
[HarmonyPatch]
internal static class MenuPatch
{
    private const string ButtonName = "Button-Archipelago";

    // Retained so the success handler can update labels / visibility after connection.
    private static MainMenuButton _apButton;
    private static MainMenuButton _startLegacyButton;
    private static MainMenuButton _saveSlotButton;

    // ── Patch target ───────────────────────────────────────────────────────────

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuWindowController), nameof(MainMenuWindowController.Initialize))]
    private static void Initialize_Postfix(MainMenuWindowController __instance)
    {
        try
        {
            InjectArchipelagoButton(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(
                $"[MenuPatch] Failed to inject Archipelago button:\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    // OnFocus fires after OnOpenCoroutine and unconditionally overwrites m_startingButton
    // text with a localized string.  Re-apply our label after it runs.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuWindowController), "OnFocus")]
    private static void OnFocus_Postfix()
    {
        if (APClient.IsConnected)
            ApplyConnectedMenuState();
    }

    // ── Implementation ─────────────────────────────────────────────────────────

    private static void InjectArchipelagoButton(MainMenuWindowController controller)
    {
        // Access the private button list via HarmonyLib's Traverse helper.
        var buttonListTraverse = Traverse.Create(controller).Field<List<MainMenuButton>>("m_menuButtonList");
        var buttonList = buttonListTraverse.Value;

        if (buttonList == null || buttonList.Count == 0)
        {
            Plugin.Log.LogWarning("[MenuPatch] m_menuButtonList is null or empty — skipping injection.");
            return;
        }

        // Guard: already injected (e.g. menu reopened after returning from gameplay).
        if (buttonList.Exists(b => b != null && b.gameObject.name == ButtonName))
        {
            Plugin.Log.LogDebug("[MenuPatch] Archipelago button already present — skipping injection.");
            return;
        }

        // Use the very first button as a visual template (inherits font, colours,
        // selected-indicator prefab, etc.).
        var template = buttonList[0];

        // Clone the template as a sibling under the same parent layout group.
        var newGO = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
        newGO.name = ButtonName;

        // Place the button at the top of the menu so it's the first thing the
        // player sees.  The VerticalLayoutGroup will respect sibling order.
        newGO.transform.SetSiblingIndex(0);

        var apButton = newGO.GetComponent<MainMenuButton>();

        // Clear the event handlers cloned from the template — they still point
        // to the template's index, which would break navigation.
        apButton.MenuButtonSelected = null;
        apButton.MenuButtonActivated = null;

        // Set m_selectorType = None via Traverse so ExecuteButton()'s switch is a
        // no-op and we get full control through MenuButtonActivated.
        Traverse.Create(apButton)
                .Field("m_selectorType")
                .SetValue(MainMenuButton.MainMenuSelectionType.None);

        // Update the button's display text.
        var label = newGO.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = "Connect to Archipelago";

        // Determine the new button's list index.  We're inserting at position 0,
        // so every existing button needs its Index bumped by one.
        const int newButtonIndex = 0;

        // Re-index all existing buttons.  MainMenuButton.Initialize() is public and
        // safe to call multiple times: it only updates Index, m_mainMenuWindow, and
        // resets the selected-indicator (which isn't shown yet at this point).
        for (int i = 0; i < buttonList.Count; i++)
            buttonList[i].Initialize(controller, i + 1);

        // Initialize our new button at index 0.
        apButton.Initialize(controller, newButtonIndex);

        // Wire up the controller's private selection/SFX handlers via reflection so
        // keyboard and controller navigation work exactly like the vanilla buttons.
        WireControllerHandlers(controller, apButton);

        // Our AP-specific action: open the connection dialog.
        apButton.MenuButtonActivated += _ => OnArchipelagoButtonClicked(controller);

        // Retain a reference so OnConnectWithData can update the label later.
        _apButton = apButton;

        // Insert at the front of the list to match the sibling-index order.
        buttonList.Insert(0, apButton);

        // Persist the modified list back through Traverse (the field is a reference
        // type so this is technically a no-op for the list object itself, but being
        // explicit avoids future confusion if the field ever becomes a value type).
        buttonListTraverse.Value = buttonList;

        // Cache the start legacy and save-slot buttons so the connection callback can
        // update them without having to re-traverse the controller.
        _startLegacyButton   = buttonList.Find(b => b.SelectorType == MainMenuButton.MainMenuSelectionType.Start);
        _saveSlotButton = buttonList.Find(b => b.SelectorType == MainMenuButton.MainMenuSelectionType.SelectProfile);

        // If already connected (e.g. menu was closed and reopened), apply visuals now.
        if (APClient.IsConnected)
            ApplyConnectedMenuState();

        Plugin.Log.LogInfo("[MenuPatch] Archipelago button injected into main menu.");
    }

    /// <summary>
    /// Subscribes the controller's private UpdateSelectedOptionItem and
    /// PlaySelectedSFX methods as delegates on <paramref name="button"/> so that
    /// hovering / selecting it updates the controller's selection index and plays
    /// the normal menu sound.
    /// </summary>
    private static void WireControllerHandlers(MainMenuWindowController controller, MainMenuButton button)
    {
        var updateMethod = AccessTools.Method(typeof(MainMenuWindowController), "UpdateSelectedOptionItem");
        var sfxMethod    = AccessTools.Method(typeof(MainMenuWindowController), "PlaySelectedSFX");

        if (updateMethod == null || sfxMethod == null)
        {
            Plugin.Log.LogWarning(
                "[MenuPatch] Could not find UpdateSelectedOptionItem / PlaySelectedSFX — " +
                "keyboard navigation may not work correctly for the Archipelago button.");
            return;
        }

        // MainMenuButtonSelectedHandler is delegate void (MainMenuButton).
        var delegateType = typeof(MainMenuButtonSelectedHandler);

        var updateDelegate = (MainMenuButtonSelectedHandler)
            Delegate.CreateDelegate(delegateType, controller, updateMethod);
        var sfxDelegate = (MainMenuButtonSelectedHandler)
            Delegate.CreateDelegate(delegateType, controller, sfxMethod);

        button.MenuButtonSelected  += updateDelegate;
        button.MenuButtonActivated += sfxDelegate;
    }

    private static void ApplyConnectedMenuState()
    {
        if (_apButton?.Text != null)
            _apButton.Text.text = "Archipelago (Connected)";

        if (_startLegacyButton?.Text != null)
            _startLegacyButton.Text.text = "Play Randomizer";

        // remove the profile select button because we don't want players changing profiles mid-session and causing desyncs; the AP save system will automatically load the correct profile slot on game start
        _saveSlotButton?.gameObject.SetActive(false);
    }

    // ── Button click handler ───────────────────────────────────────────────────

    private static void OnArchipelagoButtonClicked(MainMenuWindowController controller)
    {
        Plugin.Log.LogDebug("[MenuPatch] Archipelago button clicked.");
        Traverse.Create(controller).Field("m_lockInput").SetValue(true);

        UI.ConnectionDialog.Show(
            initialData: Plugin.ConnectionData,
            onComplete: connData => OnConnectWithData(controller, connData),
            onCancel:   () => Traverse.Create(controller).Field("m_lockInput").SetValue(false));
    }

    private static void OnConnectWithData(MainMenuWindowController controller, APConnectionData connData)
    {
        Plugin.InitNewConnectionData(connData);

        APClient.Connect(
            connData,
            onSuccess: () =>
            {
                Plugin.Log.LogInfo("Successfully connected to Archipelago server!");
                // Run the transition animation that typically takes place upon changing profiles
                SceneLoader_RL.RunTransitionWithLogic(
                    () =>
                    {
                        // Closing and reopening the main menu triggers OnOpenCoroutine,
                        // which reloads profile data and syncs all visuals: NG+ background,
                        // castle/moon/crown objects, profile slot text, and start-button copy.
                        // OnOpenCoroutine also clears m_lockInput, so no manual unlock needed.
                        WindowManager.SetWindowIsOpen(WindowID.MainMenu, isOpen: false);
                        WindowManager.SetWindowIsOpen(WindowID.MainMenu, isOpen: true);

                        ApplyConnectedMenuState();
                    },
                    TransitionID.QuickSwipe,
                    cleanup: false);
            },
            onFailure: errorMsg =>
            {
                Plugin.Log.LogError($"AP connection failed: {errorMsg}");
                UI.ConnectionDialog.Show(
                    initialData: connData,
                    onComplete: retry => OnConnectWithData(controller, retry),
                    onCancel:   () => Traverse.Create(controller).Field("m_lockInput").SetValue(false),
                    errorMessage: errorMsg);
            });
    }
}
