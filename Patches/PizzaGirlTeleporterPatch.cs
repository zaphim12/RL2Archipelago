using System;
using HarmonyLib;
using RL2Archipelago.Items;
using RL2Archipelago.Locations;
using RL_Windows;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Patches covering the full pizza girl teleporter lifecycle in AP mode.
///
/// <para>The core problem: vanilla uses <c>GetTeleporterIsUnlocked</c> to drive
/// pizza girl's visibility and dialogue state, but in AP mode the two systems
/// are decoupled; the check is sent when the player pays, and the actual
/// teleporter unlock is applied when the AP item arrives from the server.
/// All patches here use <c>APRunState.CheckedLocations</c> as the authoritative
/// source for "has this location been purchased", and
/// <c>GetTeleporterIsUnlocked</c> only for fast-travel availability.</para>
///
/// <para><b>PlayUnlockTeleporterDialogue prefix</b> - corrects the dialogue
/// branch chosen by vanilla:
/// <list type="bullet">
///   <item>Location checked but item not yet received → show "already built" dialogue.</item>
///   <item>Item received but location not yet checked → force the payment dialogue
///   so the player can still complete the check.</item>
/// </list></para>
///
/// <para><b>UnlockTeleporter prefix</b> - intercepts the purchase confirmation.
/// Deducts gold, sends the AP location check, and drives the success/failure
/// dialogue flow, suppressing the vanilla call to
/// <c>SetTeleporterIsUnlocked</c>. Also guards against a double-purchase if
/// the location was somehow already checked before the confirm was clicked.</para>
///
/// <para><b>InitializePooledPropOnEnter postfix</b> - corrects visibility on
/// room entry:
/// <list type="bullet">
///   <item>Location checked → hide pizza girl (check done, no re-purchase).</item>
///   <item>Teleporter unlocked by item but location not checked → re-enable
///   pizza girl so the player can still pay and complete the check.</item>
/// </list></para>
/// </summary>
[HarmonyPatch]
internal static class PizzaGirlTeleporterPatch
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int CalculateCost(BiomeType biome)
    {
        if (!NPC_EV.PIZZA_GIRL_TELEPORTER_COST_TABLE.ContainsKey(biome)) return 0;
        int baseCost = NPC_EV.PIZZA_GIRL_TELEPORTER_COST_TABLE[biome];
        int ng = SaveManager.PlayerSaveData.NewGamePlusLevel;
        int cost = ng <= 0
            ? baseCost
            : Mathf.RoundToInt(baseCost * (ng * 2.5f) + ng * ng * 250f);
        if (SaveManager.PlayerSaveData.SpecialModeType != SpecialModeType.None)
            cost = (int)(cost * 0.25f);
        return cost;
    }

    private static void ShowAlreadyBuiltDialogue(NPCController npc, Action endInteraction)
    {
        DialogueManager.StartNewDialogue(npc);
        DialogueManager.AddDialogue(
            "LOC_ID_NAME_PIZZA_GIRL_1",
            "LOC_ID_DIALOGUE_TELEPORTER_NPC_PORTAL_BUILD_DONE_1",
            SaveManager.PlayerSaveData.CurrentCharacter.IsFemale);
        WindowManager.SetWindowIsOpen(WindowID.Dialogue, isOpen: true);
        DialogueManager.AddDialogueCompleteEndHandler(endInteraction);
    }

    // ── Patch 1: PlayUnlockTeleporterDialogue ─────────────────────────────────

    /// <summary>
    /// Overrides vanilla's dialogue branch selection to use AP location state,
    /// and injects the scouted AP item name into the pre-purchase dialogue so
    /// the player knows what they will receive before committing gold.
    /// <list type="bullet">
    ///   <item>Location already checked → "already built" (vanilla if both systems
    ///   agree; custom if check sent but item not yet received).</item>
    ///   <item>Location not yet checked → always build the payment dialogue here so
    ///   we can append what AP item the player will receive.</item>
    /// </list>
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PizzaGirlPropController), "PlayUnlockTeleporterDialogue")]
    private static bool PlayUnlockTeleporterDialogue_Prefix(PizzaGirlPropController __instance)
    {
        if (!APClient.IsConnected || APClient.RunState == null) return true;

        var locationId = LocationRegistry.FromBiomeTypeTeleporter(__instance.Room.BiomeType);
        if (locationId == null) return true;

        bool locationChecked = APClient.RunState.CheckedLocations.Contains(locationId.Value);

        if (locationChecked)
        {
            // Let vanilla run the "already built" path when both systems agree.
            if (SaveManager.PlayerSaveData.GetTeleporterIsUnlocked(__instance.Room.BiomeType)) return true;
            // Check sent but item not yet received; force "already built".
            var t2 = Traverse.Create(__instance);
            ShowAlreadyBuiltDialogue(t2.Field<NPCController>("m_npcController").Value,
                                     t2.Field<Action>("m_endInteraction").Value);
            return false;
        }

        // Location not yet checked. build the payment dialogue ourselves so we can
        // inject the scouted AP item the player will receive.
        var t                  = Traverse.Create(__instance);
        var npcController      = t.Field<NPCController>("m_npcController").Value;
        var displayConfirmMenu = t.Field<Action>("m_displayUnlockTeleporterConfirmMenu").Value;

        int cost = CalculateCost(__instance.Room.BiomeType);

        string locKey = SaveManager.PlayerSaveData.GetFlag(PlayerSaveFlag.PizzaGirl_UnlockTeleporter_Dialogue_Intro)
            ? "LOC_ID_DIALOGUE_TELEPORTER_NPC_PORTAL_EXPLAIN_REPEAT_1"
            : "LOC_ID_DIALOGUE_TELEPORTER_NPC_PORTAL_EXPLAIN_TALK_1";
        if (!SaveManager.PlayerSaveData.GetFlag(PlayerSaveFlag.PizzaGirl_UnlockTeleporter_Dialogue_Intro))
            SaveManager.PlayerSaveData.SetFlag(PlayerSaveFlag.PizzaGirl_UnlockTeleporter_Dialogue_Intro, value: true);

        string text = string.Format(
            LocalizationManager.GetString(locKey, SaveManager.PlayerSaveData.CurrentCharacter.IsFemale),
            cost);

        var scouted = APClient.GetScoutedItem(locationId.Value);
        if (scouted != null)
        {
            string itemName = !string.IsNullOrEmpty(scouted.ItemDisplayName)
                ? scouted.ItemDisplayName
                : (scouted.ItemName ?? "Unknown Item");
            int ourSlot = APClient.Session?.ConnectionInfo?.Slot ?? -1;
            if (scouted.Player.Slot == ourSlot)
            {
                text += $"\nContains: {itemName}";
            }
            else
            {
                string playerName = !string.IsNullOrEmpty(scouted.Player.Alias)
                    ? scouted.Player.Alias
                    : (scouted.Player.Name ?? $"Player {scouted.Player.Slot}");
                text += $"\nContains: {itemName} (for {playerName})";
            }
        }

        string speaker = LocalizationManager.GetString("LOC_ID_NAME_PIZZA_GIRL_1", isFemale: false);
        DialogueManager.StartNewDialogue(npcController);
        DialogueManager.AddNonLocDialogue(speaker, text);
        WindowManager.SetWindowIsOpen(WindowID.Dialogue, isOpen: true);
        DialogueManager.AddDialogueCompleteEndHandler(displayConfirmMenu);
        return false;
    }

    // ── Patch 2: UnlockTeleporter ─────────────────────────────────────────────

    /// <summary>
    /// Intercepts the teleporter purchase confirmation. Replaces
    /// <c>SetTeleporterIsUnlocked</c> with an AP location check while
    /// replicating the rest of the vanilla gold-deduction and dialogue flow.
    /// Guards against double-purchase if the location was already checked.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PizzaGirlPropController), "UnlockTeleporter")]
    private static bool UnlockTeleporter_Prefix(PizzaGirlPropController __instance)
    {
        if (!APClient.IsConnected || APClient.RunState == null) return true;

        var locationId = LocationRegistry.FromBiomeTypeTeleporter(__instance.Room.BiomeType);
        if (locationId == null) return true;

        var t              = Traverse.Create(__instance);
        var npcController  = t.Field<NPCController>("m_npcController").Value;
        var endInteraction = t.Field<Action>("m_endInteraction").Value;
        var goldArgs       = t.Field<GoldChangedEventArgs>("m_goldChangedEventArgs").Value;

        WindowManager.SetWindowIsOpen(WindowID.ConfirmMenu, isOpen: false);

        // Guard: location already checked (e.g. checked via another source between
        // dialogue open and OK click). show "already built" and bail out.
        if (APClient.RunState.CheckedLocations.Contains(locationId.Value))
        {
            ShowAlreadyBuiltDialogue(npcController, endInteraction);
            return false;
        }

        int cost = CalculateCost(__instance.Room.BiomeType);

        DialogueManager.StartNewDialogue(npcController);

        if (SaveManager.PlayerSaveData.GoldCollected >= cost)
        {
            int oldGold = SaveManager.PlayerSaveData.GoldCollected;
            SaveManager.PlayerSaveData.GoldCollected -= cost;
            SaveManager.PlayerSaveData.GoldSpent     += cost;
            SaveManager.PlayerSaveData.TeleporterUnlockDialogueIndex.x++;
            goldArgs.Initialize(oldGold, SaveManager.PlayerSaveData.GoldCollected);
            Messenger<GameMessenger, GameEvent>.Broadcast(GameEvent.GoldChanged, __instance, goldArgs);

            APClient.SendLocationCheck(locationId.Value);

            DialogueManager.AddDialogue(
                "LOC_ID_NAME_PIZZA_GIRL_1",
                "LOC_ID_DIALOGUE_TELEPORTER_NPC_PORTAL_BUILD_YES_1",
                SaveManager.PlayerSaveData.CurrentCharacter.IsFemale);
            WindowManager.SetWindowIsOpen(WindowID.Dialogue, isOpen: true);
        }
        else
        {
            DialogueManager.AddDialogue(
                "LOC_ID_NAME_PIZZA_GIRL_1",
                "LOC_ID_DIALOGUE_TELEPORTER_NPC_PORTAL_BUILD_FAIL_1",
                SaveManager.PlayerSaveData.CurrentCharacter.IsFemale);
            WindowManager.SetWindowIsOpen(WindowID.Dialogue, isOpen: true);
        }

        DialogueManager.AddDialogueCompleteEndHandler(endInteraction);
        return false;
    }

    // ── Patch 3: InitializePooledPropOnEnter ──────────────────────────────────

    /// <summary>
    /// Corrects pizza girl visibility on room entry based on AP location state
    /// rather than <c>GetTeleporterIsUnlocked</c>:
    /// <list type="bullet">
    ///   <item>Location checked → hide (check done, no re-purchase needed).</item>
    ///   <item>Teleporter unlocked by item but location not checked → re-enable
    ///   so the player can still pay and complete the check.  Only re-enables when
    ///   the other vanilla hide conditions (NPC not unlocked, TrueRogue) are false,
    ///   so those reasons still correctly suppress her.</item>
    /// </list>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PizzaGirlPropController), "InitializePooledPropOnEnter")]
    private static void InitializePooledPropOnEnter_Postfix(PizzaGirlPropController __instance)
    {
        if (!APClient.IsConnected || APClient.RunState == null) return;
        if (__instance.Room?.RoomType != RoomType.Transition) return;

        var locationId = LocationRegistry.FromBiomeTypeTeleporter(__instance.Room.BiomeType);
        if (locationId == null) return;

        bool locationChecked    = APClient.RunState.CheckedLocations.Contains(locationId.Value);
        bool teleporterUnlocked = SaveManager.PlayerSaveData.GetTeleporterIsUnlocked(__instance.Room.BiomeType);

        if (locationChecked)
        {
            // Check sent - always hide.
            __instance.gameObject.SetActive(false);
        }
        else if (!__instance.gameObject.activeSelf && teleporterUnlocked)
        {
            // Vanilla hid her because the teleporter is unlocked (item received before
            // check was sent). Re-enable only if the other vanilla hide conditions don't apply.
            bool pizzaGirlNPCUnlocked = SaveManager.PlayerSaveData.GetFlag(PlayerSaveFlag.PizzaGirlUnlocked);
            bool isTrueRogue          = SaveManager.PlayerSaveData.SpecialModeType == SpecialModeType.TrueRogue;
            if (pizzaGirlNPCUnlocked && !isTrueRogue)
                __instance.gameObject.SetActive(true);
        }
    }
}
