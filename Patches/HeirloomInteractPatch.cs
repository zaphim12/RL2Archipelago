using HarmonyLib;
using RL2Archipelago.Items;
using RL2Archipelago.Locations;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Three patches covering the full heirloom-location lifecycle in AP mode.
///
/// <para><b>TriggerHeirloom prefix</b> — fires when the player interacts with a
/// heirloom statue. Sends the AP location check, marks the room complete, and
/// hides the statue visuals immediately, then suppresses the vanilla dialog +
/// teleport by returning <c>false</c>. Does NOT call
/// <c>SetHeirloomLevel</c> — the heirloom ability is only granted when the AP
/// server actually sends the item back via <see cref="APClient.GrantItem"/>.</para>
///
/// <para><b>RoomCompleted prefix</b> — suppresses the vanilla auto-completion that
/// fires when <c>GetHeirloomLevel &gt; 0</c>. Without this, receiving a heirloom
/// item from the AP server causes the statue room to auto-complete on entry, making
/// the location uncollectable. Completion is only allowed once the AP location has
/// been checked (i.e. it is present in <see cref="APRunState.CheckedLocations"/>).</para>
///
/// <para><b>OnPlayerEnterRoom postfix</b> — fires every time the player enters a
/// heirloom room. If the AP location was already checked in a prior run (tracked
/// in <see cref="APRunState.CheckedLocations"/>), marks the room complete and
/// hides the statue so future runs show the alternate relic-choice layout without
/// the game needing <c>GetHeirloomLevel &gt; 0</c> to be true.</para>
/// </summary>
[HarmonyPatch]
internal static class HeirloomInteractPatch
{
    /// <summary>
    /// Handles interactions related to the heirloom statue: 
    /// sending the AP location check, marking the room complete, and hiding the statue visuals 
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HeirloomStatuePropController), nameof(HeirloomStatuePropController.TriggerHeirloom))]
    private static bool TriggerHeirloom_Prefix(HeirloomStatuePropController __instance)
    {
        if (!APClient.IsConnected) return true;

        var heirloomRoom = Traverse.Create(__instance).Field<HeirloomRoomController>("m_heirloomRoom").Value;
        if (heirloomRoom == null) return true;

        var locationId = LocationRegistry.FromHeirloomType(heirloomRoom.HeirloomType);
        if (locationId is null) return true;

        APClient.SendLocationCheck(locationId.Value);

        if (!heirloomRoom.IsRoomComplete)
            heirloomRoom.RoomCompleted();

        DisableStatueVisuals(__instance);
        return false;
    }

    /// <summary>
    /// Prevents the vanilla auto-completion path from firing when the player already
    /// has the heirloom ability but hasn't yet collected the AP location check.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HeirloomRoomController), nameof(HeirloomRoomController.RoomCompleted))]
    private static bool RoomCompleted_Prefix(HeirloomRoomController __instance)
    {
        if (!APClient.IsConnected) return true;
        if (APClient.RunState == null) return true;
        if (__instance.IsFinalRoom) return true; // Not sure this is actually needed, I think it may be for the golden apple interaction, which we don't touch

        var locationId = LocationRegistry.FromHeirloomType(__instance.HeirloomType);
        if (locationId is null) return true;

        Plugin.Log.LogInfo($"Checking if heirloom room {__instance.HeirloomType} is complete for location ID {locationId.Value}. Room complete = {APClient.RunState.CheckedLocations.Contains(locationId.Value)}");
        return APClient.RunState.CheckedLocations.Contains(locationId.Value);
    }

    /// <summary>
    /// Replaces the statue's icon sprite with a graphic representing the AP item
    /// that will actually drop at this location (a native heirloom icon when the
    /// item belongs to this slot, otherwise a generic Archipelago logo).
    /// The base method sets <c>m_iconSprite.sprite</c> to the vanilla heirloom
    /// icon, so this runs as a postfix to override that assignment.
    /// Skipped when not connected, when the location has already been checked
    /// (the statue gets hidden anyway), or when the scout reply hasn't arrived yet.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HeirloomStatuePropController), "InitializePooledPropOnEnter")]
    private static void InitializePooledPropOnEnter_Postfix(HeirloomStatuePropController __instance)
    {
        if (!APClient.IsConnected) return;

        var heirloomRoom = Traverse.Create(__instance).Field<HeirloomRoomController>("m_heirloomRoom").Value;
        if (heirloomRoom == null) return;

        var locationId = LocationRegistry.FromHeirloomType(heirloomRoom.HeirloomType);
        if (locationId is null) return;

        if (APClient.RunState != null && APClient.RunState.CheckedLocations.Contains(locationId.Value))
            return;

        var iconSprite = Traverse.Create(__instance).Field<SpriteRenderer>("m_iconSprite").Value;
        if (iconSprite == null) return;

        var replacement = APSprites.GetSpriteForLocation(locationId.Value);
        if (replacement != null)
            iconSprite.sprite = replacement;
    }

    /// <summary>
    /// Handles room-completion status for previously completed AP checks
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HeirloomRoomController), "OnPlayerEnterRoom")]
    private static void OnPlayerEnterRoom_Postfix(HeirloomRoomController __instance)
    {
        if (APClient.RunState == null) return;
        if (__instance.IsFinalRoom) return; 

        var locationId = LocationRegistry.FromHeirloomType(__instance.HeirloomType);
        if (locationId is null) return;

        if (!APClient.RunState.CheckedLocations.Contains(locationId.Value)) return;

        if (!__instance.IsRoomComplete)
            __instance.RoomCompleted();

        foreach (var statue in __instance.Room.GetComponentsInChildren<HeirloomStatuePropController>())
            DisableStatueVisuals(statue);
    }

    private static void DisableStatueVisuals(HeirloomStatuePropController statue)
    {
        var t = Traverse.Create(statue);
        t.Field<GameObject>("m_heirloomParticlesGO").Value?.SetActive(false);
        t.Field<SpriteRenderer>("m_iconSprite").Value?.gameObject.SetActive(false);
        t.Field<Interactable>("m_interactable").Value?.SetIsInteractableActive(false);
    }

    /// <summary>
    /// Suppresses the vanilla "Insight Discovered" HUD on heirloom statues —
    /// our APNotifications system reuses the same HUD to display the AP item
    /// being sent, and double-firing would clobber that text.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HeirloomStatuePropController), "RunInsightDiscovered")]
    private static bool RunInsightDiscovered_Prefix(ref bool __result)
    {
        if (!APClient.IsConnected) return true;
        __result = false;
        return false;
    }

    /// <summary>
    /// Suppresses the vanilla "Insight Resolved" HUD that fires when entering a
    /// heirloom room with the heirloom already owned. Same reason as above.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HeirloomRoomController), "RunInsightResolved")]
    private static bool RunInsightResolved_Prefix()
    {
        return !APClient.IsConnected;
    }
}
