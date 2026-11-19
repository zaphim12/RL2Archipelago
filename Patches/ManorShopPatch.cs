using HarmonyLib;
using RL2Archipelago.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.Patches;

/// <summary>
/// Builds the manor for the shop layout: hides every vanilla manor node and repurposes four
/// slots as a row of shop positions. Inert unless <see cref="APClient.IsManorShopLayout"/>.
///
/// <para>Purchasing, pricing and all of the per-slot display overrides stay in
/// <see cref="ManorUpgradePatch"/>, which branches on the layout. This class only owns the
/// screen's shape.</para>
///
/// <para>Why repurpose rather than instantiate: node positions and prerequisites are authored
/// into the window prefab (<c>RectTransform</c>s and <c>SkillTreeSlot.UnlockSlotList</c>), and
/// <c>SkillTreeWindowController.Initialize</c> throws on a duplicate <c>SkillTreeType</c>, so
/// there is no safe way to add a node. Four existing slots are taken over instead; see
/// <see cref="ManorShopLayout.Hosts"/> for which, and why those.</para>
/// </summary>
[HarmonyPatch]
internal static class ManorShopPatch
{
    /// <summary>The window the row was last built against. The prefab is re-instantiated on
    /// every scene load, and Unity's overloaded <c>==</c> reports the destroyed object as null,
    /// which is what makes the rebuild trigger.</summary>
    private static SkillTreeWindowController s_builtFor;

    /// <summary>The resolved host slots (the vanilla manor slots being re-purposed into
    /// shop nodes), in position order.</summary>
    private static readonly List<SkillTreeSlot> s_hostSlots = new();

    /// <summary>
    /// Assembles the row, idempotently.
    ///
    /// <para>Called from both an <c>Initialize</c> postfix and an <c>OnOpen</c> prefix.
    /// <c>Initialize</c> runs once per window load and is the natural home, but it can run
    /// before the player has connected (so before the layout is known), and in that case
    /// <c>OnOpen</c> is the first chance to build. Whichever happens first wins and the other
    /// is a no-op.</para>
    /// </summary>
    private static void EnsureBuilt(SkillTreeWindowController controller)
    {
        if (!APClient.IsSessionActive || !APClient.IsManorShopLayout) return;
        if (controller == null || !ManorShopLayout.IsValid) return;
        if (s_builtFor == controller) return;

        try
        {
            // includeInactive: true because the tree is already switched off by the time this
            // runs - ManorUpgradePatch.Initialize_Prefix zeroes every manor level, so vanilla
            // Initialize activates nothing.
            var slots = controller.GetComponentsInChildren<SkillTreeSlot>(includeInactive: true);

            // Each host is a hardcoded manor slot, so the window always has one. A miss
            // would mean the prefab no longer matches GameConstants.ManorSlots, which breaks
            // far more than the shop. In that case, bail rather than build a row with a hole in it,
            // since a missing position makes its whole column of checks unobtainable.
            s_hostSlots.Clear();
            foreach (var host in ManorShopLayout.Hosts)
            {
                var slot = FindSlot(controller, host);
                if (slot == null)
                {
                    s_hostSlots.Clear();
                    Plugin.Log.LogError(
                        $"[AP] Manor shop host '{host}' is not in the manor window; manor locations " +
                        "will be unobtainable.");
                    return;
                }
                s_hostSlots.Add(slot);
            }

            var container = Traverse.Create(controller)
                .Field<CanvasGroup>("m_skillTreeIconsCanvasGroup").Value;

            foreach (var slot in slots)
            {
                if (slot == null) continue;
                bool isHost = slot.HasData && ManorShopLayout.IsHost(slot.SkillTreeType);
                if (!isHost)
                {
                    if (slot.gameObject.activeSelf) slot.gameObject.SetActive(false);
                    continue;
                }
                ConfigureHost(slot, ManorShopLayout.PositionOf(slot.SkillTreeType).Value, container);
            }

            // Vanilla OnOpen force-activates and selects m_startingHighlightedButton
            // regardless of level, so pointing it at the first position is what makes the shop
            // the thing the window opens onto rather than the tree's root node.
            Traverse.Create(controller)
                .Field<SkillTreeSlot>("m_startingHighlightedButton").Value = s_hostSlots[0];

            foreach (var slot in s_hostSlots)
                ApplyShopNavigation(slot);

            s_builtFor = controller;
            Plugin.Log.LogDebug(
                $"[AP] Manor shop layout built: {ManorShopLayout.Hosts.Length} positions " +
                $"x {Locations.LocationRegistry.ManorShopDepth} deep " +
                $"({string.Join(", ", ManorShopLayout.Hosts)}).");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to build the manor shop layout.\n{ex}");
        }
    }

    /// <summary>
    /// Turns one authored node into a shop position: visible, inert as far as the castle and
    /// the tree layout are concerned, and moved into the row.
    /// </summary>
    private static void ConfigureHost(SkillTreeSlot slot, int position, CanvasGroup container)
    {
        // Clearing the animator param makes HasAnimParam false, which is what keeps the castle
        // from growing a wing every time a purchase is made: it short-circuits
        // UpdateAnimatorParams and empties out AnimateBGImage.
        slot.BGAnimatorParam = string.Empty;
        slot.AnimType = SkillTreeSlot.SkillTreeAnimType.None;

        // Emptied rather than left authored so vanilla UnlockConnectedSkillSlots, which still
        // runs off the SkillLevelChanged broadcast after a purchase, cannot reveal a node the
        // shop layout means to keep hidden.
        slot.UnlockSlotList = new List<SkillTreeSlot>();
        slot.UnlockSlotDisableList = new List<bool>();

        // Cut the authored navigation links. Nothing should be walking them now that
        // UpdateNavigationNodes_Prefix intercepts the hosts, but a null link makes
        // RecursiveNodeSearch return immediately instead of recursing, so even an
        // unanticipated caller cannot reach the depth limit through a host.
        slot.TopNode = null;
        slot.BottomNode = null;
        slot.LeftNode = null;
        slot.RightNode = null;
        slot.DisableTopNavigationNode = true;
        slot.DisableBottomNavigationNode = true;

        slot.gameObject.SetActive(true);

        if (container == null)
        {
            // Without the container the slots cannot be moved into a row, so they stay
            // scattered wherever the castle put them. The shop still works; it just looks
            // nothing like a shop, which is worth a line in the log.
            Plugin.Log.LogWarning(
                "[AP] m_skillTreeIconsCanvasGroup not found; manor shop positions are left " +
                "at their original places on the castle.");
            return;
        }

        // Reparented to a known container and given centred anchors, so the row's position is
        // decided entirely by ManorShopLayout rather than by wherever in the castle the prefab
        // happened to put this node.
        var rect = (RectTransform)slot.transform;
        var storedScale = slot.StoredScale;
        rect.SetParent(container.transform, worldPositionStays: false);
        rect.SetAsLastSibling();          // above the castle art and the branch-overlay layer
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = ManorShopLayout.AnchoredPositionFor(position);
        // Reparenting preserves localScale, but the select and shake tweens both animate back
        // to the scale Initialize recorded, so keep the two in agreement explicitly.
        rect.localScale = storedScale;
    }

    /// <summary>
    /// Points one position's left/right at its neighbours and kills its up/down, by writing
    /// <c>Button.navigation</c> directly.
    ///
    /// <para>Deliberately NOT done through <c>SkillTreeSlot.UpdateNavigationNodes</c>. That
    /// resolves each direction with <c>RecursiveNodeSearch</c>, which accepts a neighbour only
    /// if it is <c>isActiveAndEnabled</c> and otherwise recurses into that neighbour's own
    /// neighbour, throwing once it is 20 deep. During <c>Initialize</c> the window canvas is
    /// still inactive, so every slot fails that test and any cycle among the hosts recurses
    /// until it throws. Writing the targets here sidesteps the walk completely, and
    /// <see cref="UpdateNavigationNodes_Prefix"/> keeps vanilla from running it on a host
    /// later and undoing this.</para>
    /// </summary>
    private static void ApplyShopNavigation(SkillTreeSlot slot)
    {
        int index = s_hostSlots.IndexOf(slot);
        if (index < 0) return;

        // Every entry is non-null: EnsureBuilt bails rather than keep a row with a gap in it.
        int count = s_hostSlots.Count;
        var navigation = slot.Button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        // Wrapping at the ends is safe here precisely because these are final targets rather
        // than the start of a search.
        navigation.selectOnLeft = count > 1 ? s_hostSlots[(index - 1 + count) % count].Button : null;
        navigation.selectOnRight = count > 1 ? s_hostSlots[(index + 1) % count].Button : null;
        navigation.selectOnUp = null;
        navigation.selectOnDown = null;
        slot.Button.navigation = navigation;
    }

    /// <summary>
    /// Keeps vanilla navigation resolution away from the hosts. <c>RefreshAllButtons</c> calls
    /// <c>UpdateNavigationNodes</c> on every active slot after each purchase, so without this
    /// the explicit targets would be recomputed - and the walk that recomputes them is the one
    /// that throws. See <see cref="ApplyShopNavigation"/>.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkillTreeSlot), nameof(SkillTreeSlot.UpdateNavigationNodes))]
    private static bool UpdateNavigationNodes_Prefix(SkillTreeSlot __instance)
    {
        if (!APClient.IsSessionActive || !APClient.IsManorShopLayout) return true;
        if (__instance == null || !__instance.HasData) return true;
        if (!ManorShopLayout.IsHost(__instance.SkillTreeType)) return true;

        ApplyShopNavigation(__instance);
        return false;
    }

    private static SkillTreeSlot FindSlot(SkillTreeWindowController controller, SkillTreeType type)
    {
        var dict = Traverse.Create(controller)
            .Field<Dictionary<SkillTreeType, SkillTreeSlot>>("m_skillTreeButtonDict")
            .Value;
        if (dict != null && dict.TryGetValue(type, out var slot)) return slot;
        return null;
    }

    /// <summary>
    /// Re-hides anything but the four hosts. Cheap enough to run on every window open, and
    /// run there rather than only at build time because <c>EnsureBuilt</c> is a once-per-window
    /// operation while a stray node only has to be activated once to stay on screen for the
    /// rest of the session. Vanilla <c>OnOpen</c> and <c>OnFocus</c> both force-activate
    /// whatever <c>m_selectedSkillTreeButton</c> points at, so this is also the backstop if
    /// that field ever ends up on a tree node.
    /// </summary>
    private static void ReassertVisibility(SkillTreeWindowController controller)
    {
        if (!APClient.IsSessionActive || !APClient.IsManorShopLayout) return;
        // Only once the row is actually standing: before then the tree is the only manor
        // there is, and hiding it would leave the player with a blank screen.
        if (controller == null || s_builtFor != controller) return;

        try
        {
            foreach (var slot in controller.GetComponentsInChildren<SkillTreeSlot>(includeInactive: true))
            {
                if (slot == null || !slot.HasData) continue;
                bool shouldBeActive = ManorShopLayout.IsHost(slot.SkillTreeType);
                if (slot.gameObject.activeSelf != shouldBeActive)
                    slot.gameObject.SetActive(shouldBeActive);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Failed to re-hide the manor tree.\n{ex}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "Initialize")]
    private static void Initialize_Postfix(SkillTreeWindowController __instance) => EnsureBuilt(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "OnOpen")]
    private static void OnOpen_Prefix(SkillTreeWindowController __instance)
    {
        EnsureBuilt(__instance);
        ReassertVisibility(__instance);
    }

    /// <summary>Runs after vanilla OnOpen, which force-activates the selected button and then
    /// calls RefreshAllButtons, either of which could put a tree node back on screen.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "OnOpen")]
    private static void OnOpen_Postfix(SkillTreeWindowController __instance) => ReassertVisibility(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "OnFocus")]
    private static void OnFocus_Postfix(SkillTreeWindowController __instance) => ReassertVisibility(__instance);

    /// <summary>Drops the cached window when a session ends, so a later session against a
    /// tree-layout seed never sees shop state.</summary>
    internal static void Reset()
    {
        s_builtFor = null;
        s_hostSlots.Clear();
    }
}
