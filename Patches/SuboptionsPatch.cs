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

        BuildToggleItem(clonedItemsParent, template, "Death Link",
            getter: () => APSettings.DeathLink,
            setter: v  => APSettings.DeathLink = v);

        BuildToggleItem(clonedItemsParent, template, "Show Item Names",
            getter: () => APSettings.ShowItemNames,
            setter: v  => APSettings.ShowItemNames = v);

        dict[APSuboptionType] = parentGO;

        Plugin.Log.LogInfo("[SuboptionsPatch] AP suboption entry injected.");
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
        SelectionListOptionItem template,
        string settingName,
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
        var disclaimerObj          = oldTv.Field("m_disclaimerObj").GetValue();

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
        newTv.Field("m_disclaimerObj").SetValue(disclaimerObj);

        // Assign behaviour.
        newItem.SettingName = settingName;
        newItem.Getter      = getter;
        newItem.Setter      = setter;

        // Initialize the item so its internal state is ready.
        // DO NOT call InitializeSuboptionPrefab; that wires delegates which would
        // then be double-wired by ChangeSuboptionDisplayed when the panel first opens.
        newItem.Initialize();

        return newItem;
    }
}
