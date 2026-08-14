using HarmonyLib;
using RL2Archipelago.Items;
using RL2Archipelago.Locations;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.Patches;

/// <summary>
/// Contains all patches required to randomize manor slots as locations and to grant
/// manor rewards as AP items
/// </summary>
[HarmonyPatch]
internal static class ManorUpgradePatch
{
    // ── Soul-lock bypass ─────────────────────────────────────────────────────
    //
    // Some manor slots have SkillTreeData.MaxLevel == 0 and rely entirely on a
    // soul shop purchase (e.g. "Unbreakable Will", "Absolute Strength") to get a
    // non-zero MaxLevel. Until purchased, IsSoulLocked returns true and the slot
    // is unpurchasable. In AP mode we bypass the soul shop entirely, so we ensure
    // MaxLevel is at least 1 for any AP-tracked slot. This fixes IsSoulLocked
    // (which is just MaxLevel <= 0) and ClampedLevel simultaneously.

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeObj), "get_MaxLevel")]
    private static void MaxLevel_Postfix(SkillTreeObj __instance, ref int __result)
    {
        if (!APClient.IsSessionActive) return;
        if (LocationRegistry.FromSkillTreeType(__instance.SkillTreeType) == null) return;
        if (__result <= 0) __result = 1;
    }

    // ── Shared level-remap helpers ────────────────────────────────────────────
    //
    // Several vanilla methods (Initialize, UpdateAnimatorParams) read the raw
    // skillTreeObj.Level to decide slot visibility or built/unbuilt visual state.
    // In AP mode the level can be non-zero from item receipt without a purchase,
    // or zero despite a purchase when the item hasn't arrived yet. We temporarily
    // remap levels to match the "purchased" state before each such method runs,
    // then restore afterward. s_savedLevels is reused by both patch pairs.

    private static readonly Dictionary<SkillTreeType, int> s_savedLevels = new();

    /// <summary>A slot the base game never reveals under any circumstance. Written as an
    /// explicit null test rather than <c>?.</c> because SkillTreeData is a UnityEngine.Object,
    /// whose overloaded <c>==</c> is the only thing that reports a destroyed asset as null.</summary>
    private static bool IsHiddenSlot(SkillTreeType type)
    {
        var data = SkillTreeLibrary.GetSkillTreeData(type);
        return data != null && data.SkillUnlockState == SkillUnlockState.Hidden;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "Initialize")]
    private static void Initialize_Prefix()
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return;

        s_savedLevels.Clear();
        foreach (var skillType in ItemRegistry.s_skillTreeTypes)
        {
            var locationId = LocationRegistry.FromSkillTreeType(skillType);
            if (locationId == null) continue;

            int currentLevel = SkillTreeManager.GetSkillObjLevel(skillType);
            bool isChecked = APClient.RunState.CheckedLocations.Contains(locationId.Value);

            // ── reveal_manor_upgrades ────────────────────────────────────────
            // Show the whole tree by giving every tracked slot a non-zero level, which is
            // the only thing vanilla Initialize consults. Purchasability is decided
            // separately by IsSlotUnlocked, so this is presentation only.
            if (APClient.RevealManorUpgrades)
            {
                // Vanilla never reveals a Hidden slot even at level > 0; respect that.
                if (IsHiddenSlot(skillType)) continue;

                if (currentLevel == 0)
                {
                    s_savedLevels[skillType] = 0;
                    SkillTreeManager.SetSkillObjLevel(skillType, 1, additive: false, runEvents: false);
                }
                // currentLevel > 0 already activates the slot, whether or not it was
                // purchased, and under this option there is nothing left to hide.
                continue;
            }

            if (isChecked && currentLevel == 0)
            {
                // Checked but AP item not yet received: slot must still activate.
                s_savedLevels[skillType] = 0;
                SkillTreeManager.SetSkillObjLevel(skillType, 1, additive: false, runEvents: false);
            }
            else if (!isChecked && currentLevel > 0)
            {
                // AP item received but location not purchased: hide the slot.
                s_savedLevels[skillType] = currentLevel;
                SkillTreeManager.SetSkillObjLevel(skillType, 0, additive: false, runEvents: false);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "Initialize")]
    private static void Initialize_Postfix(SkillTreeWindowController __instance)
    {
        foreach (var kv in s_savedLevels)
            SkillTreeManager.SetSkillObjLevel(kv.Key, kv.Value, additive: false, runEvents: false);
        s_savedLevels.Clear();

        BuildUnlockGraph(__instance);
    }

    // ── Unlock graph ─────────────────────────────────────────────────────────
    //
    // Without reveal_manor_upgrades, visibility *is* the purchase gate: an unreachable slot
    // is an inactive GameObject and simply cannot be clicked, so PurchaseSkillUpgrade_Prefix
    // never had to check the tree. When enabled, every slot is on screen so that no longer holds,
    // so the rule vanilla enforced implicitly is reconstructed here and enforced explicitly.
    //
    // The rule being mirrored is SkillTreeSlot.UnlockConnectedSkillSlots: purchasing a slot
    // activates every non-disabled entry of its UnlockSlotList that is not Hidden. Inverting
    // that gives, for each slot, the slots whose purchase would have revealed it.
    //
    // Direction matters here and is deliberately kept, unlike ManorTree (UI/ManorTree.cs)
    // which flattens the same data into an undirected graph for drawing branch lines. Some
    // slots list the node above them as well, and honouring only the authored direction is
    // what reproduces vanilla's reveal order exactly.

    private static readonly Dictionary<SkillTreeType, List<SkillTreeType>> s_revealedBy = new();

    /// <summary>The slot vanilla force-activates in OnOpen regardless of level, and therefore
    /// the one slot that is purchasable with nothing bought yet.</summary>
    private static SkillTreeType s_rootSlot = SkillTreeType.None;

    private static void BuildUnlockGraph(SkillTreeWindowController controller)
    {
        s_revealedBy.Clear();
        s_rootSlot = SkillTreeType.None;

        if (!APClient.IsSessionActive || !APClient.RevealManorUpgrades) return;

        try
        {
            var root = Traverse.Create(controller).Field<SkillTreeSlot>("m_startingHighlightedButton").Value;
            if (root != null && root.HasData) s_rootSlot = root.SkillTreeType;

            // includeInactive: true because at this point most of the tree is still off.
            foreach (var slot in controller.GetComponentsInChildren<SkillTreeSlot>(includeInactive: true))
            {
                if (slot == null || !slot.HasData) continue;

                var connected = slot.UnlockSlotList;
                if (connected == null) continue;
                var disabled = slot.UnlockSlotDisableList;

                for (int i = 0; i < connected.Count; i++)
                {
                    var other = connected[i];
                    if (other == null || other == slot || !other.HasData) continue;
                    // Entries flagged in the parallel disable list are authored-but-inactive links.
                    if (disabled != null && i < disabled.Count && disabled[i]) continue;
                    if (IsHiddenSlot(other.SkillTreeType)) continue;

                    if (!s_revealedBy.TryGetValue(other.SkillTreeType, out var sources))
                        s_revealedBy[other.SkillTreeType] = sources = new();
                    if (!sources.Contains(slot.SkillTreeType)) sources.Add(slot.SkillTreeType);
                }
            }
        }
        catch (Exception ex)
        {
            // IsSlotUnlocked treats an empty graph as "everything unlocked", so a failure here
            // degrades to the pre-option purchase behavior rather than soft-locking the manor.
            s_revealedBy.Clear();
            Plugin.Log.LogError($"[AP] Failed to build the manor unlock graph; all revealed slots stay purchasable.\n{ex.Message}");
        }
    }

    /// <summary>
    /// Whether <paramref name="type"/> would have been revealed by the base game given the
    /// current AP purchase state, i.e. whether it is purchasable now.
    /// <para>
    /// Always true when <see cref="APClient.RevealManorUpgrades"/> is off, so every caller is
    /// a no-op in the default configuration and the vanilla-visibility gate stays in charge.
    /// </para>
    /// </summary>
    internal static bool IsSlotUnlocked(SkillTreeType type)
    {
        if (!APClient.RevealManorUpgrades) return true;
        // Graph not built yet (or it failed to build): never gate on incomplete data.
        if (s_revealedBy.Count == 0) return true;
        if (type == s_rootSlot) return true;
        if (IsSlotPurchased(type)) return true;

        if (s_revealedBy.TryGetValue(type, out var sources))
            foreach (var source in sources)
                if (IsSlotPurchased(source)) return true;

        return false;
    }

    /// <summary>Purchase state of a slot: for AP-tracked slots that is whether its location has
    /// been checked, never its skill level, so an item arriving early never unlocks a neighbour.
    /// Untracked slots fall back to the vanilla level test.</summary>
    private static bool IsSlotPurchased(SkillTreeType type)
    {
        var locationId = LocationRegistry.FromSkillTreeType(type);
        if (locationId == null) return SkillTreeManager.GetSkillObjLevel(type) > 0;
        return APClient.RunState?.CheckedLocations?.Contains(locationId.Value) ?? false;
    }

    // UpdateAnimatorParams runs inside OnOpen → RefreshAllButtons(true) and sets
    // m_castleAnimator's BGAnimatorParam based on skillTreeObj.Level. Apply the
    // same remap so built state persists across window close/reopen.

    private static SkillTreeType s_animParamSkillType;
    private static int s_animParamSavedLevel = -1;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkillTreeSlot), nameof(SkillTreeSlot.UpdateAnimatorParams))]
    private static void UpdateAnimatorParams_Prefix(SkillTreeSlot __instance)
    {
        s_animParamSavedLevel = -1;
        if (!APClient.IsSessionActive || APClient.RunState == null) return;
        var locationId = LocationRegistry.FromSkillTreeType(__instance.SkillTreeType);
        if (locationId == null) return;

        int currentLevel = SkillTreeManager.GetSkillObjLevel(__instance.SkillTreeType);
        bool isChecked = APClient.RunState.CheckedLocations.Contains(locationId.Value);

        if (isChecked && currentLevel == 0)
        {
            s_animParamSkillType = __instance.SkillTreeType;
            s_animParamSavedLevel = 0;
            SkillTreeManager.SetSkillObjLevel(__instance.SkillTreeType, 1, additive: false, runEvents: false);
        }
        else if (!isChecked && currentLevel > 0)
        {
            s_animParamSkillType = __instance.SkillTreeType;
            s_animParamSavedLevel = currentLevel;
            SkillTreeManager.SetSkillObjLevel(__instance.SkillTreeType, 0, additive: false, runEvents: false);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeSlot), nameof(SkillTreeSlot.UpdateAnimatorParams))]
    private static void UpdateAnimatorParams_Postfix()
    {
        if (s_animParamSavedLevel == -1) return;
        SkillTreeManager.SetSkillObjLevel(s_animParamSkillType, s_animParamSavedLevel, additive: false, runEvents: false);
        s_animParamSavedLevel = -1;
    }

    // ── Purchase replacement ──────────────────────────────────────────────────
    //
    // Replaces PurchaseSkillUpgrade entirely for AP-tracked slots (returns false).
    //
    // Needs to handle a few different issues:
    //   UIEvent.SkillLevelChanged fires synchronously inside the vanilla
    //     method, before the Postfix could add the location to CheckedLocations.
    //     By recording the check first and then broadcasting SkillLevelChanged,
    //     RefreshSlotState_Postfix sees the location as checked and shows 1/1.
    //
    //   Vanilla SetSkillObjLevel grants the skill stat immediately on
    //     purchase. In AP mode the stat comes from the AP item receipt, not purchase.
    //     Skipping the vanilla body prevents the premature grant.
    //
    //   Vanilla allows for purchasing multiple levels. AP slots have exactly
    //     one purchase; we fail immediately if the location is already checked.
    //     to prevent purchasing the same slot more than once

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkillTreeSlot), nameof(SkillTreeSlot.PurchaseSkillUpgrade))]
    private static bool PurchaseSkillUpgrade_Prefix(SkillTreeSlot __instance)
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return true;
        var locationId = LocationRegistry.FromSkillTreeType(__instance.SkillTreeType);
        if (locationId == null) return true; // non-AP slot: let vanilla run

        var skillTreeObj = SkillTreeManager.GetSkillTreeObj(__instance.SkillTreeType);

        if (skillTreeObj == null || skillTreeObj.IsLocked || skillTreeObj.IsSoulLocked)
        {
            PlayFailAnim(__instance);
            return false;
        }

        // With reveal_manor_upgrades on, being on screen no longer implies being reachable,
        // so the tree rule vanilla used to enforce through visibility is checked here instead.
        // Gated on the option rather than on IsSlotUnlocked alone so that a seed without it
        // keeps exactly the old code path.
        if (APClient.RevealManorUpgrades && !IsSlotUnlocked(__instance.SkillTreeType))
        {
            PlayFailAnim(__instance);
            return false;
        }

        // Already purchased this AP location; prevent multiple purchases for slots which originally had multiple levels
        if (APClient.RunState.CheckedLocations.Contains(locationId.Value))
        {
            PlayFailAnim(__instance);
            return false;
        }

        int goldCost = APClient.ManorUpgradeCosts.TryGetValue(skillTreeObj.SkillTreeType, out var precomputedCost)
            ? precomputedCost
            : skillTreeObj.GoldCostWithLevelAppreciation;
        if (SaveManager.PlayerSaveData.GoldCollectedIncludingBank < goldCost)
        {
            PlayFailAnim(__instance);
            return false;
        }

        // Deduct gold, mirroring vanilla.
        int goldBefore = SaveManager.PlayerSaveData.GoldCollectedIncludingBank;
        SaveManager.PlayerSaveData.SubtractFromGoldIncludingBank(goldCost);
        SaveManager.PlayerSaveData.GoldSpent += goldCost;
        SaveManager.PlayerSaveData.GoldSpentOnSkills += goldCost;
        BroadcastGoldChanged(__instance, goldBefore, goldCost);

        // Record the check BEFORE refreshing the UI so RefreshSlotState_Postfix
        // already sees this location as checked and renders 1/1.
        APClient.SendLocationCheck(locationId.Value);

        // Broadcast SkillLevelChanged (prevLevel=0→newLevel=1) so the
        // SkillTreeWindowController listener plays the manor building animation,
        // calls UnlockConnectedSkillSlots, and refreshes all buttons.
        Messenger<UIMessenger, UIEvent>.Broadcast(
            UIEvent.SkillLevelChanged,
            null,
            new SkillLevelChangedEventArgs(__instance.SkillTreeType, 0, 1));

        __instance.FullyUpgradedRelay.Dispatch();

        return false; // skip vanilla. no SetSkillObjLevel, no vanilla stat grant
    }

    // ── Suppress purchase pop-up for AP slots ────────────────────────────────
    //
    // OnSkillLevelChanged calls DisplaySkillTreePopUp when GetPopupData returns
    // non-null. For AP-tracked slots the popup would show vanilla unlock text
    // (class name, NPC description, etc.) rather than the AP item, so we return
    // null to skip it. The normal slot animation still plays via the else branch.

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkillTreePopupLibrary), nameof(SkillTreePopupLibrary.GetPopupData))]
    private static bool GetPopupData_Prefix(SkillTreeType skillType, ref SkillTreePopupData __result)
    {
        if (!APClient.IsSessionActive) return true;
        if (LocationRegistry.FromSkillTreeType(skillType) == null) return true;
        __result = null;
        return false;
    }

    // ── Suppress the "labour costs" pop-up ───────────────────────────────────
    //
    // After every purchase the window runs UnlockLabourCostAnimCoroutine, which
    // fires a one-time SkillTreePopUp (SkillTreeType.LabourCosts_Unlocked) once the
    // total manor level passes 30, explaining vanilla's escalating upgrade surcharge.
    // AP slots use costs precomputed by the apworld (APClient.ManorUpgradeCosts), so
    // that surcharge never applies and the pop-up is just misinformation.
    //
    // All three call sites (OnSkillLevelChanged, AnimateBGImageCoroutine,
    // DisplaySkillTreePopUp) route through this one kickoff method, so replacing its
    // enumerator with an empty one covers them all. PlayerSaveFlag.LabourCostsUnlocked
    // is left unset since it doesn't affect AP gameplay. The tail call to UpdateLabourCosts
    // is dropped because that method is itself neutered below.

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "UnlockLabourCostAnimCoroutine")]
    private static bool UnlockLabourCostAnimCoroutine_Prefix(ref System.Collections.IEnumerator __result)
    {
        if (!APClient.IsSessionActive) return true;
        __result = NoOpCoroutine();
        return false;
    }

    private static System.Collections.IEnumerator NoOpCoroutine()
    {
        yield break;
    }

    // ── Suppress the "labour costs" surcharge readout ────────────────────────
    //
    // The same vanilla surcharge drives a persistent readout in the manor UI, showing
    // floor((totalLevel - 30) * 14 / 5) * 5 as a percentage. AP costs come precomputed
    // from the apworld, so the surcharge is never applied and the number is meaningless.
    // Hide the element outright. Patching the method rather than the pop-up coroutine
    // also covers the OnOpen and OnSkillLevelChanged call sites.

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "UpdateLabourCosts")]
    private static bool UpdateLabourCosts_Prefix(SkillTreeWindowController __instance)
    {
        if (!APClient.IsSessionActive) return true;

        var labourCostGO = Traverse.Create(__instance).Field<GameObject>("m_labourCostGO").Value;
        if (labourCostGO != null && labourCostGO.activeSelf)
            labourCostGO.SetActive(false);

        return false;
    }

    // ── Manor level display ──────────────────────────────────────────────────
    //
    // Vanilla GetTotalSkillObjLevel sums every skill's level, which is wrong in
    // AP mode: received items inflate the count, and purchased slots whose AP
    // item hasn't arrived yet don't contribute. Override with the count of
    // checked manor locations so the HUD matches what the player actually bought.

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeManager), nameof(SkillTreeManager.GetTotalSkillObjLevel))]
    private static void GetTotalSkillObjLevel_Postfix(ref int __result)
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return;

        int count = 0;
        foreach (var skillType in ItemRegistry.s_skillTreeTypes)
        {
            var locationId = LocationRegistry.FromSkillTreeType(skillType);
            if (locationId == null) continue;
            if (APClient.RunState.CheckedLocations.Contains(locationId.Value))
                count++;
        }
        __result = count;
    }

    private static void PlayFailAnim(SkillTreeSlot slot)
    {
        var shakeEnum = Traverse.Create(slot)
            .Method("ShakeAnimCoroutine")
            .GetValue<System.Collections.IEnumerator>();
        if (shakeEnum != null)
            slot.StartCoroutine(shakeEnum);
        slot.UpgradeFailedRelay.Dispatch();
    }

    private static void BroadcastGoldChanged(SkillTreeSlot slot, int goldBefore, int goldCost)
    {
        try
        {
            var goldArgs = Traverse.Create(typeof(SkillTreeSlot))
                .Field("m_goldChangedArgs").GetValue();
            if (goldArgs == null) return;
            Traverse.Create(goldArgs)
                .Method("Initialize", new object[] { goldBefore, goldBefore + goldCost })
                .GetValue();
            Messenger<GameMessenger, GameEvent>.Broadcast(
                GameEvent.GoldChanged, slot, goldArgs as EventArgs);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] GoldChanged broadcast failed: {ex.Message}");
        }
    }

    // ── Slot state display override ──────────────────────────────────────────
    /// <summary>
    /// Handles all border and indicator changes which need to be made so that the visuals for a slot match
    /// the AP purchase state for the item which is held there
    /// This includes showing the "$" for affordable items and removing padlocks from gated items
    /// <para>
    /// Also handles updates to the visual for items which are purchased (like gold border, "1/1" text, etc.)
    /// </para>
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeSlot), nameof(SkillTreeSlot.RefreshSlotState))]
    private static void RefreshSlotState_Postfix(SkillTreeSlot __instance)
    {
        if (!APClient.IsSessionActive) return;
        var locationId = LocationRegistry.FromSkillTreeType(__instance.SkillTreeType);
        if (locationId == null) return;

        var t = Traverse.Create(__instance);
        var levelText = t.Field<TMP_Text>("m_levelText").Value;
        if (levelText == null) return;

        bool isChecked = APClient.RunState?.CheckedLocations?.Contains(locationId.Value) ?? false;
        levelText.text = isChecked ? "1 / 1" : "0 / 1";

        var iconFrame       = t.Field<Image>("m_iconFrame").Value;
        var iconCanvasGroup = t.Field<CanvasGroup>("m_iconCanvasGroup").Value;
        var newIndicator    = t.Field<Image>("m_newIndicator").Value;
        var levelLockGO     = t.Field<GameObject>("m_levelLockGO").Value;

        // Vanilla's manor-level gating (the padlock) is suppressed in AP mode for all tracked
        // slots. When reveal_manor_upgrades is enabled, the padlock is instead used to indicate
        // which slots are not purchase-able given the current manor state. i.e. the slot is shown
        // on screen for planning, but its parent has not been purchased yet.
        //
        // The number vanilla prints on the padlock is SkillTreeObj.UnlockLevel, the total manor
        // level the slot would otherwise need. That threshold is not enforced in AP mode and the
        // reused padlock has nothing numeric to say, so the label is blanked and only the lock
        // sprite remains.
        bool unlocked = IsSlotUnlocked(__instance.SkillTreeType);
        if (levelLockGO != null && levelLockGO.activeSelf != !unlocked)
            levelLockGO.SetActive(!unlocked);
        if (t.Field<TMP_Text>("m_levelLockText").Value is { } levelLockText && levelLockText.text.Length > 0)
            levelLockText.text = string.Empty;

        if (isChecked)
        {
            // Show purchased visuals: gold frame + full opacity.
            if (iconFrame != null)
                iconFrame.overrideSprite = t.Field<Sprite>("m_goldFrameSprite").Value;
            if (iconCanvasGroup != null)
                iconCanvasGroup.alpha = 1f;
            // Slot already purchased - hide the "affordable" dollar-sign indicator.
            if (newIndicator != null && newIndicator.gameObject.activeSelf)
                newIndicator.gameObject.SetActive(false);
        }
        else
        {
            // Slot not yet purchased: undo any "purchased" visuals the game applied
            // because the skill level may be > 0 due to AP item receipt.
            if (iconFrame != null)
                iconFrame.overrideSprite = null;
            if (iconCanvasGroup != null)
                iconCanvasGroup.alpha = 0.5f;

            // Recompute the "affordable" indicator ignoring level gating, since we
            // allow all visible slots to be purchased regardless of manor level.
            if (newIndicator != null)
            {
                var skillTreeObj = SkillTreeManager.GetSkillTreeObj(__instance.SkillTreeType);
                int slotCost = APClient.ManorUpgradeCosts.TryGetValue(__instance.SkillTreeType, out var precomputed)
                    ? precomputed
                    : skillTreeObj.GoldCostWithLevelAppreciation;
                bool canAfford = unlocked
                    && skillTreeObj != null
                    && !skillTreeObj.IsLocked
                    && !skillTreeObj.IsSoulLocked
                    && SaveManager.PlayerSaveData.GoldCollectedIncludingBank >= slotCost;
                newIndicator.gameObject.SetActive(canAfford);
            }
        }
    }

    // ── Slot icon replacement ────────────────────────────────────────────────
    /// <summary>
    /// Updates the icon associated with this manor upgrade to reflect the randomized item placed there
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeSlot), nameof(SkillTreeSlot.UpdateSkillTreeType))]
    private static void UpdateSkillTreeType_Postfix(SkillTreeSlot __instance)
    {
        if (!APClient.IsSessionActive) return;
        var locationId = LocationRegistry.FromSkillTreeType(__instance.SkillTreeType);
        if (locationId == null) return;

        var iconImage = Traverse.Create(__instance).Field<Image>("m_iconImage").Value;
        if (iconImage == null) return;

        var replacement = APSprites.GetSpriteForLocation(locationId.Value);
        if (replacement != null)
            iconImage.sprite = replacement;
    }

    // ── Description box icon override ───────────────────────────────────────
    //
    // m_infoPlateIcon is set in three places: OnHighlightedSkillChanged (hover),
    // OnOpen (window opens), and OnFocus (window regains focus after a sub-window).

    private static void OverrideInfoPlateIcon(SkillTreeWindowController instance, SkillTreeType skillTreeType)
    {
        if (!APClient.IsSessionActive) return;
        var locationId = LocationRegistry.FromSkillTreeType(skillTreeType);
        if (locationId == null) return;
        var replacement = APSprites.GetSpriteForLocation(locationId.Value);
        if (replacement == null) return;
        if (Traverse.Create(instance).Field<Image>("m_infoPlateIcon").Value is { } icon)
            icon.sprite = replacement;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "OnHighlightedSkillChanged")]
    private static void OnHighlightedSkillChanged_Postfix(SkillTreeWindowController __instance, EventArgs args)
    {
        if (args is HighlightedSkillChangedEventArgs e)
            OverrideInfoPlateIcon(__instance, e.SkillTreeType);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "OnOpen")]
    private static void OnOpen_Postfix(SkillTreeWindowController __instance)
    {
        var selected = Traverse.Create(__instance).Field<SkillTreeSlot>("m_selectedSkillTreeButton").Value;
        if (selected != null)
            OverrideInfoPlateIcon(__instance, selected.SkillTreeType);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "OnFocus")]
    private static void OnFocus_Postfix(SkillTreeWindowController __instance)
    {
        var selected = Traverse.Create(__instance).Field<SkillTreeSlot>("m_selectedSkillTreeButton").Value;
        if (selected != null)
            OverrideInfoPlateIcon(__instance, selected.SkillTreeType);
    }

    // ── Hover description ────────────────────────────────────────────────────
    /// <summary>
    /// Handles all text and description changes which need to be made for a slot match
    /// so that they reflect the AP item which is held there, instead of the non-randomized 
    /// item which originally resided there.
    /// This includes descriptions, titles, etc.
    /// <para>
    /// This includes suppresion for making sure that there is no text which shows the price
    /// for buying x5 of an upgrade since the AP only allows one level per-slot
    /// </para>
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="__0"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeDescriptionUpdater), "UpdateText")]
    private static void UpdateText_Postfix(SkillTreeDescriptionUpdater __instance, SkillTreeType __0)
    {
        if (!APClient.IsSessionActive) return;
        var locationId = LocationRegistry.FromSkillTreeType(__0);
        if (locationId == null) return;

        // CostMultiple shows "Cost (xN): ###" for multi-level slots. AP slots always
        // have 1 level, so hide this element unconditionally regardless of scouting state.
        if (__instance.DescriptionType == SkillTreeDescriptionUpdater.SkillTreeDescriptionType.CostMultiple)
        {
            if (__instance.gameObject.activeSelf)
                __instance.gameObject.SetActive(false);
            return;
        }

        // Purchase shows the "Upgrade xN" keybind prompt. For checked slots it should
        // disappear entirely; for unchecked slots it should always say "Upgrade x1".
        if (__instance.DescriptionType == SkillTreeDescriptionUpdater.SkillTreeDescriptionType.Purchase)
        {
            // Hidden for a locked slot too: under reveal_manor_upgrades it is only on screen
            // for planning, so advertising a purchase that would just shake and fail is wrong.
            bool isChecked = APClient.RunState?.CheckedLocations?.Contains(locationId.Value) ?? false;
            if (isChecked || !IsSlotUnlocked(__0))
            {
                if (__instance.gameObject.activeSelf)
                    __instance.gameObject.SetActive(false);
            }
            else if (__instance.DescriptionLocItem?.TextObj is { } purchaseText)
            {
                if (!__instance.gameObject.activeSelf)
                    __instance.gameObject.SetActive(true);
                purchaseText.text = string.Format(
                    LocalizationManager.GetString("LOC_ID_SKILL_TREE_UI_PURCHASE_MULTIPLE_1", isFemale: false), 1);
            }
            return;
        }

        if (__instance.DescriptionType == SkillTreeDescriptionUpdater.SkillTreeDescriptionType.Cost)
        {
            if (__instance.CurrentValue is { } costCv)
            {
                bool isChecked = APClient.RunState?.CheckedLocations?.Contains(locationId.Value) ?? false;
                if (isChecked)
                    costCv.text = LocalizationManager.GetString("LOC_ID_GENERAL_UI_NA_1", isFemale: false);
                else if (APClient.ManorUpgradeCosts.TryGetValue(__0, out var customCost))
                    costCv.text = customCost.ToString();
            }
            return;
        }

        // Null while the scout cache is empty (notably during auto-reconnect). Every AP slot
        // then falls back to vanilla RL2 text uniformly, so no slot stands out. This guard must
        // stay identity-agnostic: a trap-specific fallback here would introduce a tell on any 
        // locations that contain a trap
        var scouted = APClient.GetItemView(locationId.Value);
        if (scouted == null) return;

        var descText = __instance.DescriptionLocItem?.TextObj;
        if (descText == null) return;

        var itemName = scouted.DisplayName;
        var playerName = scouted.PlayerName;

        if (__instance.DescriptionType == SkillTreeDescriptionUpdater.SkillTreeDescriptionType.Title)
        {
            descText.text = itemName;
        }
        else if (__instance.DescriptionType == SkillTreeDescriptionUpdater.SkillTreeDescriptionType.Description)
        {
            descText.text = playerName == null
                ? $"[AP] {itemName}"
                : $"[AP] {itemName}\nfor {playerName}";
            descText.text += scouted.FlavorText;
        }
        else if (__instance.DescriptionType == SkillTreeDescriptionUpdater.SkillTreeDescriptionType.Stat)
        {
            descText.text = playerName == null
                ? $"[AP] {itemName}"
                : $"[AP] {itemName}\nfor {playerName}";
            if (__instance.CurrentValue is { } statCv)
                statCv.text = "0 (+1)";
        }
        else if (__instance.DescriptionType == SkillTreeDescriptionUpdater.SkillTreeDescriptionType.Level)
        {
            if (__instance.CurrentValue is { } levelCv)
            {
                bool isChecked = APClient.RunState?.CheckedLocations?.Contains(locationId.Value) ?? false;
                levelCv.text = isChecked ? "1 / 1" : "0 / 1";
            }
        }
    }
}
