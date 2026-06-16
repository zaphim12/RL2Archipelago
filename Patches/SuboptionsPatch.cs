using HarmonyLib;
using RL_Windows;
using RL2Archipelago.UI;
using System;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Patches which handle creating and managing the sub-options available within the "Archipelago Settings"
/// sub-menu we inject into the pause menu.
/// </summary>
[HarmonyPatch]
internal static class SuboptionsPatch
{
    // Cast to the enum type we inject under. Using an unused int (70) so we never
    // collide with any existing SuboptionType (Game=10 … Assist=60).
    internal const SuboptionType APSuboptionType = (SuboptionType)70;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SuboptionsWindowController), nameof(SuboptionsWindowController.Initialize))]
    private static void Initialize_Postfix(SuboptionsWindowController __instance)
    {
        try
        {
            InjectAPSuboption(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(
                $"[SuboptionsPatch] Failed to inject AP suboption:\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void InjectAPSuboption(SuboptionsWindowController controller)
    {
        var tv   = Traverse.Create(controller);
        var dict = tv.Field<SuboptionTypePrefabDictionary>("m_suboptionPrefabDict").Value;
        var canvasGroup = tv.Field<CanvasGroup>("m_optionItemsCanvasGroup").Value;

        if (dict == null || canvasGroup == null)
        {
            Plugin.Log.LogWarning("[SuboptionsPatch] Could not access SuboptionsWindowController fields. skipping injection.");
            return;
        }

        // Guard: already injected on a previous Initialize call.
        if (dict.ContainsKey(APSuboptionType)) return;

        if (!dict.TryGetValue(SuboptionType.Graphics, out var graphicsPanel) || graphicsPanel == null)
        {
            Plugin.Log.LogWarning("[SuboptionsPatch] Graphics panel not found. skipping injection.");
            return;
        }

        var template = graphicsPanel.GetComponentInChildren<SelectionListOptionItem>();
        if (template == null)
        {
            Plugin.Log.LogWarning("[SuboptionsPatch] No SelectionListOptionItem found in Graphics panel. skipping injection.");
            return;
        }

        // Find a disclaimer GO from any existing suboption item to use as a visual template
        // (font, size, colour, background) AND as the source of the vanilla position "slot".
        var disclaimerTemplate = FindDisclaimerTemplate(canvasGroup.gameObject);
        if (disclaimerTemplate == null)
            Plugin.Log.LogWarning("[SuboptionsPatch] No disclaimer template found in any suboption panel; descriptions will be absent.");

        // Read the vanilla "slot" the disclaimer snaps to. This is the scene object placed at
        // the bottom-of-menu disclaimer spot; its world position is what we copy so our text
        // lands exactly where the base game's does, no manual coordinates. We don't reparent
        // onto it, we only read its position.
        GameObject disclaimerSlot = null;
        if (disclaimerTemplate != null)
        {
            var dpc = disclaimerTemplate.GetComponent<DisclaimerPositionController>();
            if (dpc != null)
                disclaimerSlot = Traverse.Create(dpc).Field<GameObject>("m_disclaimerPositionObj").Value;
        }

        // Clone the entire Graphics panel so we inherit its background image, RectTransform
        // layout group, and anchor settings; instead of a bare container with nothing.
        var parentGO = UnityEngine.Object.Instantiate(graphicsPanel, canvasGroup.transform, false);
        parentGO.name = "APSettings_SuboptionContainer";
        parentGO.SetActive(false);

        // Immediately destroy all cloned vanilla settings items so they don't show up when
        // ChangeSuboptionDisplayed rebuilds m_optionItemList via GetComponentsInChildren.
        foreach (var item in parentGO.GetComponentsInChildren<BaseOptionItem>(true))
            UnityEngine.Object.DestroyImmediate(item.gameObject);

        // Resolve the items-container in the clone by walking the same relative path
        // that the template lived under inside the original panel.
        var itemsParentPath = GetRelativePath(graphicsPanel.transform, template.transform.parent);
        var clonedItemsParent = (itemsParentPath != null
            ? parentGO.transform.Find(itemsParentPath)
            : null) ?? parentGO.transform;

        BuildToggleItem(clonedItemsParent, parentGO, template, disclaimerTemplate, disclaimerSlot, "Death Link",
            description: "Toggle death-link enabled. This will override your yaml setting",
            getter: () => APSettings.DeathLink ?? APClient.DeathLinkEnabled,
            setter: v =>
            {
                APSettings.DeathLink = v;
                APClient.SavePlayerSettings();
                APClient.ApplyDeathLinkSetting(v);
            });

        BuildToggleItem(clonedItemsParent, parentGO, template, disclaimerTemplate, disclaimerSlot, "Minimalist AP Log",
            description: "When enabled, AP logs use simple plaintext notifications on the top-right.\nWhen disabled, AP logs use the in-game banner system",
            getter: () => APSettings.UseMinimalistAPLog,
            setter: v =>
            {
                APSettings.UseMinimalistAPLog = v;
                APClient.SavePlayerSettings();
                if (!v) APTextLog.Instance?.Clear();
            });

        BuildToggleItem(clonedItemsParent, parentGO, template, disclaimerTemplate, disclaimerSlot, "Debug Keys",
            description: "When enabled, debug keys for testing will be usable (number keys 7-0).\nNote: This is not intended for actual playthroughs",
            getter: () => APSettings.DebugKeysEnabled,
            setter: v =>
            {
                APSettings.DebugKeysEnabled = v;
                APClient.SavePlayerSettings();
            });

        dict[APSuboptionType] = parentGO;

        Plugin.Log.LogInfo("[SuboptionsPatch] AP suboption entry injected.");
    }

    // Searches all suboption panels (including inactive ones) for any option item
    // that has a disclaimer GO. Returns the disclaimer GO to use as a clone template,
    // or null if none is found.
    private static GameObject FindDisclaimerTemplate(GameObject canvasRoot)
    {
        var ctrl = canvasRoot.GetComponentInChildren<DisclaimerPositionController>(includeInactive: true);
        return ctrl?.gameObject;
    }

    // Destroys any game-specific (non-Unity) MonoBehaviours on go. i.e. the
    // localization component that would otherwise re-apply the original locale string
    // and overwrite our custom text every time the panel is shown.
    private static void StripLocalization(GameObject go)
    {
        var toDestroy = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (var comp in go.GetComponents<MonoBehaviour>())
        {
            var ns = comp.GetType().Namespace ?? "";
            if (!ns.StartsWith("TMPro") && !ns.StartsWith("UnityEngine"))
                toDestroy.Add(comp);
        }
        foreach (var comp in toDestroy)
            UnityEngine.Object.DestroyImmediate(comp);
    }

    // Builds a "/" separated path from root to target suitable for Transform.Find.
    // Returns null if target is root itself or is not a descendant of root.
    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == null || target == root) return null;
        var path    = target.name;
        var current = target.parent;
        while (current != null && current != root)
        {
            path    = current.name + "/" + path;
            current = current.parent;
        }
        return current == root ? path : null;
    }

    // Clones the template item GO, swaps out the vanilla component for APBoolOptionItem,
    // copies all visual-field references, then calls Initialize() so the item is ready
    // to display when ChangeSuboptionDisplayed wires delegates and shows the panel.
    private static APBoolOptionItem BuildToggleItem(
        Transform parent,
        GameObject panelRoot,
        SelectionListOptionItem template,
        GameObject disclaimerTemplate,
        GameObject disclaimerSlot,
        string settingName,
        string description,
        Func<bool> getter,
        Action<bool> setter)
    {
        var cloneGO = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        cloneGO.name = $"APSetting_{settingName.Replace(" ", "")}";

        var oldItem = cloneGO.GetComponent<BaseOptionItem>();

        // Capture visual field references before the old component is gone.
        // All these fields are protected in BaseOptionItem; Traverse accesses them here.
        var oldTv = Traverse.Create(oldItem);
        var titleText              = oldTv.Field("m_titleText").GetValue();
        var incrementValueText     = oldTv.Field("m_incrementValueText").GetValue();

        // Remove any localization MonoBehaviour on the title text GO so it can't
        // override our custom setting name after the panel is shown.
        if (titleText is TMPro.TMP_Text titleTMP)
            StripLocalization(titleTMP.gameObject);
        var isSuboptionItem        = oldTv.Field("m_isSuboptionItem").GetValue();
        var selectedBG             = oldTv.Field("m_selectedBG").GetValue();
        var selectedIndicator      = oldTv.Field("m_selectedIndicator").GetValue();
        var leftArrow              = oldTv.Field("m_leftArrow").GetValue();
        var rightArrow             = oldTv.Field("m_rightArrow").GetValue();
        var incrementBar           = oldTv.Field("m_incrementBar").GetValue();
        var incrementMaskRT        = oldTv.Field("m_incrementMaskRectTransform").GetValue();

        // Destroy any disclaimer the cloned template item brought along; we're
        // replacing it with our own.
        var oldDisclaimer = oldTv.Field<GameObject>("m_disclaimerObj").Value;
        if (oldDisclaimer != null)
            UnityEngine.Object.DestroyImmediate(oldDisclaimer);

        // Build our disclaimer by cloning the vanilla template for its visuals, then swap the
        // game's DisclaimerPositionController for our own positioner. The vanilla controller
        // would reparent the disclaimer onto the (currently inactive) slot, making it invisible;
        // ours just copies the slot's world position so the text lands in the same spot while
        // staying parented inside our always-active panel.
        GameObject newDisclaimer = null;
        if (disclaimerTemplate != null && !string.IsNullOrEmpty(description))
        {
            // Parent under the panel root, not the option item, so the item's layout/scroll
            // can't drag the disclaimer around, mirroring how the vanilla disclaimer lives
            // outside the item it describes.
            newDisclaimer = UnityEngine.Object.Instantiate(disclaimerTemplate, panelRoot.transform, false);
            newDisclaimer.name = "APDisclaimer";
            newDisclaimer.SetActive(false);

            // Replace the vanilla position controller with ours.
            foreach (var dpc in newDisclaimer.GetComponents<DisclaimerPositionController>())
                UnityEngine.Object.DestroyImmediate(dpc);
            newDisclaimer.AddComponent<APDisclaimerPositioner>().Slot = disclaimerSlot;

            // Keep it out of any layout group on the panel root.
            var le = newDisclaimer.GetComponent<UnityEngine.UI.LayoutElement>()
                     ?? newDisclaimer.AddComponent<UnityEngine.UI.LayoutElement>();
            le.ignoreLayout = true;

            var descTMP = newDisclaimer.GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);
            if (descTMP != null)
            {
                // Strip localization so it can't overwrite our text.
                StripLocalization(descTMP.gameObject);
                descTMP.text = description;
            }
        }

        // Disable the old component so it doesn't respond to pointer/selection events
        // while it's still alive this frame (Destroy is end-of-frame).
        oldItem.enabled = false;
        UnityEngine.Object.Destroy(oldItem);

        // Add our replacement component.
        var newItem = cloneGO.AddComponent<APBoolOptionItem>();

        // Copy visual field refs into the new component.
        var newTv = Traverse.Create(newItem);
        newTv.Field("m_titleText").SetValue(titleText);
        newTv.Field("m_incrementValueText").SetValue(incrementValueText);
        newTv.Field("m_isSuboptionItem").SetValue(isSuboptionItem);
        newTv.Field("m_selectedBG").SetValue(selectedBG);
        newTv.Field("m_selectedIndicator").SetValue(selectedIndicator);
        newTv.Field("m_leftArrow").SetValue(leftArrow);
        newTv.Field("m_rightArrow").SetValue(rightArrow);
        newTv.Field("m_incrementBar").SetValue(incrementBar);
        newTv.Field("m_incrementMaskRectTransform").SetValue(incrementMaskRT);
        newTv.Field("m_disclaimerObj").SetValue(newDisclaimer);

        // Assign behaviour.
        newItem.SettingName = settingName;
        newItem.Description = description;
        newItem.Getter      = getter;
        newItem.Setter      = setter;

        // Initialize the item so its internal state is ready.
        // DO NOT call InitializeSuboptionPrefab; that wires delegates which would
        // then be double-wired by ChangeSuboptionDisplayed when the panel first opens.
        newItem.Initialize();

        // The cloned template text starts white (selected color). OnDeselect resets both
        // text colors and selectedBG to their unselected state so only the item that
        // receives gamepad focus on first open appears highlighted.
        newItem.OnDeselect(null);

        return newItem;
    }
}
