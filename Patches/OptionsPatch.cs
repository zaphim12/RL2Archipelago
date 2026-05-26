using HarmonyLib;
using RL_Windows;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Patches which handle creating and managing the main "Archipelago Settings" button we inject into the
/// pause menu's options list. This opens the suboptions menu handled by <see cref="SuboptionsPatch"/>.
/// </summary>
[HarmonyPatch]
internal static class OptionsPatch
{
    private const string ButtonName = "Button-APSettings";
    private static OpenSuboptionOptionItem _apSettingsButton;
    private static TMP_Text _apSettingsLabel;

    // ── Patch targets ──────────────────────────────────────────────────────────

    // Postfix on Initialize() to inject our button into the options list.
    // Initialize() populates m_optionItemList via GetComponentsInChildren once (null-guard),
    // so we inject here rather than on every OnOpen().
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionsWindowController), nameof(OptionsWindowController.Initialize))]
    private static void Initialize_Postfix(OptionsWindowController __instance)
    {
        // SuboptionsWindowController extends OptionsWindowController - skip it.
        if (__instance is SuboptionsWindowController) return;

        try
        {
            InjectArchipelagoSettingsButton(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(
                $"[OptionsPatch] Failed to inject Archipelago Settings button:\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    // OnFocus fires after OnOpenCoroutine and re-applies localized text to all items.
    // Re-set our label here so it survives that pass, mirroring how MenuPatch handles it.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionsWindowController), "OnFocus")]
    private static void OnFocus_Postfix(OptionsWindowController __instance)
    {
        if (__instance is SuboptionsWindowController) return;
        if (_apSettingsLabel != null)
            _apSettingsLabel.text = "Archipelago Settings";
    }

    // Prefix on OpenSuboptionOptionItem.ActivateOption() to intercept our button.
    // Returns false (skips original) only for our specific button instance so all
    // vanilla buttons continue to work normally.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(OpenSuboptionOptionItem), nameof(OpenSuboptionOptionItem.ActivateOption))]
    private static bool ActivateOption_Prefix(OpenSuboptionOptionItem __instance)
    {
        if (__instance != _apSettingsButton) return true;

        // Fire OptionItemActivated manually so the selection SFX plays (PlaySelectedSFX
        // is wired there during injection), matching vanilla button behavior.
        __instance.OptionItemActivated?.Invoke(__instance);

        // Mirror exactly what OpenSuboptionOptionItem.ActivateOption() does for vanilla buttons:
        // open the Suboptions window, then switch it to our injected AP entry.
        WindowManager.SetWindowIsOpen(WindowID.Suboptions, isOpen: true);
        var subCtrl = WindowManager.GetWindowController(WindowID.Suboptions) as SuboptionsWindowController;
        subCtrl?.ChangeSuboptionDisplayed(SuboptionsPatch.APSuboptionType);

        return false; // skip: prevents the real Suboptions window from opening for this button's own SuboptionType
    }

    // ── Implementation ─────────────────────────────────────────────────────────

    private static void InjectArchipelagoSettingsButton(OptionsWindowController controller)
    {
        var itemListTraverse = Traverse.Create(controller).Field<List<BaseOptionItem>>("m_optionItemList");
        var itemList = itemListTraverse.Value;

        if (itemList == null || itemList.Count == 0)
        {
            Plugin.Log.LogWarning("[OptionsPatch] m_optionItemList is null or empty. skipping injection.");
            return;
        }

        // Guard: already injected (e.g. controller reused after returning to menu).
        if (itemList.Exists(b => b != null && b.gameObject.name == ButtonName))
        {
            Plugin.Log.LogDebug("[OptionsPatch] AP Settings button already present. skipping injection.");
            return;
        }

        // Clone the first item (a vanilla OpenSuboptionOptionItem) for visual fidelity:
        // it carries all the correct fonts, colors, layout, and selection indicator prefab.
        var template = itemList[0];
        var newGO = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
        newGO.name = ButtonName;

        // Place at the top of the vertical layout so it appears first in the list.
        newGO.transform.SetSiblingIndex(0);

        var apItem = newGO.GetComponent<OpenSuboptionOptionItem>();

        // Clear event handlers cloned from the template; they point to the wrong index.
        apItem.OptionItemSelected   = null;
        apItem.OptionItemActivated  = null;
        apItem.OptionItemDeactivated = null;

        // Update display label. Store the reference so OnFocus_Postfix can re-apply
        // the text after the window's localization pass overwrites it.
        var label = newGO.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = "Archipelago Settings";
            _apSettingsLabel = label;
        }

        // Remove the value/increment text if present (not needed for this button type).
        var incrementText = Traverse.Create(apItem).Field<TMP_Text>("m_incrementValueText").Value;
        if (incrementText != null)
            incrementText.text = "";

        // Wire the controller's private selection and SFX handlers via reflection so
        // keyboard and controller navigation work exactly like vanilla buttons.
        WireControllerHandlers(controller, apItem);

        _apSettingsButton = apItem;

        // Insert at the front of the list to match sibling-index order.
        itemList.Insert(0, apItem);
        itemListTraverse.Value = itemList;

        Plugin.Log.LogInfo("[OptionsPatch] Archipelago Settings button injected into options menu.");
    }

    // Wires the controller's private UpdateSelectedOptionItem and PlaySelectedSFX
    // methods onto the button so hover/selection and audio work correctly.
    private static void WireControllerHandlers(OptionsWindowController controller, BaseOptionItem button)
    {
        var updateMethod = AccessTools.Method(typeof(OptionsWindowController), "UpdateSelectedOptionItem");
        var sfxMethod    = AccessTools.Method(typeof(OptionsWindowController), "PlaySelectedSFX");

        if (updateMethod == null || sfxMethod == null)
        {
            Plugin.Log.LogWarning(
                "[OptionsPatch] Could not find UpdateSelectedOptionItem / PlaySelectedSFX. " +
                "keyboard navigation may not work correctly for the Archipelago Settings button.");
            return;
        }

        var updateDelegate = (OptionItemSelectedHandler)
            Delegate.CreateDelegate(typeof(OptionItemSelectedHandler), controller, updateMethod);
        var sfxDelegate = (OptionItemSelectedHandler)
            Delegate.CreateDelegate(typeof(OptionItemSelectedHandler), controller, sfxMethod);

        button.OptionItemSelected  += updateDelegate;
        button.OptionItemActivated += sfxDelegate;
    }
}
