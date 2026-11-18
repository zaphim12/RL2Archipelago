using HarmonyLib;
using RL2Archipelago.UI;
using RL_Windows;
using System;
using TMPro;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Repurposes the NG+ shop window as a standalone burden picker for Archipelago runs.
///
/// <para>The window was built for one job: let the player apply burdens, then enter a new
/// timeline. Three of its behaviors are hostile to using it as a plain difficulty picker, and each
/// gets one patch here:</para>
/// <list type="number">
/// <item><c>OnOpen</c> snapshots every burden level into <c>m_storedBurdenValues</c>, and
/// <c>OnClose</c> restores that snapshot unless the static <c>EnteringNGPlus</c> is set. Left alone
/// it would discard everything the player just picked - the giveaway in the log is the game's own
/// "Player did not enter new timeline. Resetting burden values."</item>
/// <item>The timeline row and the "Unlock All Burdens" row really do start a NG+ / set
/// <c>TimesBeatenTraitor</c>. Both route through one confirm handler, which is the cheapest place to
/// block them.</item>
/// <item>Cancel means "discard", so it raises a confirm prompt when anything changed. For us cancel
/// <em>is</em> save, and the prompt would be a lie.</item>
/// </list>
///
/// <para>The remaining two patches are run-wide rather than picker-wide, and key off
/// <see cref="APClient.APSaveActive"/> instead: unlock gating and the burden gold bonus.</para>
///
/// <para>Everything here no-ops for the vanilla docks interaction, which must keep working as
/// shipped for anyone playing a normal save with the mod installed.</para>
/// </summary>
[HarmonyPatch]
internal static class BurdenSelectionPatches
{
    // ── Keeping the player's choices ───────────────────────────────────────────

    /// <summary>
    /// Decides whether the player's edits survive the close.
    ///
    /// <para><c>OnClose</c> rolls every burden back to the snapshot <c>OnOpen</c> took, unless
    /// <c>EnteringNGPlus</c> is set. That rollback is exactly the discard behavior we want from the
    /// cancel path, so the flag is borrowed only when the player left by confirming. Borrowing it
    /// is safe because the patch below makes entering a NG+ unreachable while the picker is ours.</para>
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIWindowController), "OnClose")]
    private static void OnClose_Prefix(out bool __state)
    {
        __state = NewGamePlusOmniUIWindowController.EnteringNGPlus;
        if (BurdenSelection.Active && BurdenSelection.ConfirmedClose)
            NewGamePlusOmniUIWindowController.EnteringNGPlus = true;
    }

    /// <summary>
    /// Restores the borrowed flag and commits the player's choices.
    ///
    /// <para>This is where the commit lives, rather than on <c>WindowClosedEvent</c>, because
    /// <c>WindowController.SetIsOpen</c> invokes that event <em>before</em> calling <c>OnClose</c>.
    /// Committing from the event would clear <see cref="BurdenSelection.Active"/> while the restore
    /// loop was still to come, and the loop would then run and revert everything.</para>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIWindowController), "OnClose")]
    private static void OnClose_Postfix(bool __state)
    {
        if (!BurdenSelection.Active) return;

        NewGamePlusOmniUIWindowController.EnteringNGPlus = __state;
        BurdenSelection.NotifyClosed();
    }

    /// <summary>
    /// <c>OnOpen</c> ends by focusing the timeline row, which is the one row our picker has disabled.
    /// Move the cursor into the burden list so the window opens on something the player can use.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIWindowController), "OnOpen")]
    private static void OnOpen_Postfix(NewGamePlusOmniUIWindowController __instance)
    {
        if (!BurdenSelection.Active) return;

        try
        {
            __instance.SetTimelineEntryActive(active: false, playSFX: false, usingMouse: false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] Could not focus the burden list: {ex.Message}");
        }
    }

    // ── Making the NG+ rows inert ──────────────────────────────────────────────

    /// <summary>
    /// The single choke point for every NG+ mutation the window can perform:
    /// <c>ConfirmEnterNewGamePlus</c> (writes <c>NewGamePlusLevel</c> and closes all windows, which
    /// the docks NPC would follow with <c>ResetForNewGamePlus</c> and a scene load) and
    /// <c>ConfirmUnlockNG</c> (sets <c>TimesBeatenTraitor = 7</c>).
    ///
    /// <para>The Enter button is repurposed as the picker's "done" button: closing the window is 
    /// what commits the choices, via the <c>OnClose</c> pair above. The "Unlock All Burdens" row 
    /// has genuinely nothing to do, since everything is already unlocked for an AP run, so it is hidden.</para>
    ///
    /// <para>The rows stay visible rather than being hidden, because hiding them would mean patching
    /// every navigation handler too: <c>OnUpButtonJustPressed</c>, <c>OnDownButtonJustPressed</c> and
    /// <c>OnPointerEnter</c> all route back into <c>SetTimelineEntryActive</c>.</para>
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIEnterButton), nameof(NewGamePlusOmniUIEnterButton.OnConfirmButtonPressed))]
    private static bool EnterButton_OnConfirmButtonPressed_Prefix(NewGamePlusOmniUIEnterButton __instance)
    {
        if (!BurdenSelection.Active) return true;

        if (__instance.BurdenType == BurdenType.UnlockNG)
        {
            Plugin.Log.LogDebug("[AP] Ignored \"Unlock All Burdens\": already unlocked for this run.");
            return false;
        }

        // Closing is what commits, and CloseAndSave is what marks the close as a keeping one.
        // A close reached any other way lets OnClose roll the burden changes back.
        BurdenSelection.CloseAndSave();
        return false;
    }

    /// <summary>
    /// The timeline row's left/right arrows are the NG+ level selector, which shares a button class
    /// with the burden level arrows and is told apart by <c>BurdenType.None</c>.
    ///
    /// <para>Blocking it is not cosmetic. <c>GetHighestAllowedNGPlus</c> returns 7 once
    /// <c>BurdenNGUnlocked</c> is set, so the player could wind the level up to NG+7; the Enter
    /// button's own <c>UpdateState</c> then gates on <c>CanEnterNewGamePlus</c> for that level and
    /// would go inactive, leaving them unable to confirm. Pinning the level to the save's actual
    /// value keeps Enter usable.</para>
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIEquipButton), nameof(NewGamePlusOmniUIEquipButton.OnConfirmButtonPressed))]
    private static bool EquipButton_OnConfirmButtonPressed_Prefix(NewGamePlusOmniUIEquipButton __instance)
    {
        if (!BurdenSelection.Active || __instance.BurdenType != BurdenType.None) return true;

        return false;
    }

    /// <summary>
    /// Handles Escape / B through the window's cancel action.
    ///
    /// <para>On the forced first prompt we do not allow cancelling since we need the player
    /// to choose their burdens and acknowledge that choice. So we swallow any attempt
    /// to cancel on the initial prompt.</para>
    ///
    /// <para>On a debug-key reopen the vanilla handler runs untouched. It already does what we want:
    /// if anything changed it raises a confirm prompt, and accepting that closes the window without
    /// the Confirm flag set, so <c>OnClose</c>'s rollback restores the burdens. If nothing changed
    /// it just closes.</para>
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIWindowController), "OnCancelButtonJustPressed")]
    private static bool OnCancelButtonJustPressed_Prefix() =>
        !BurdenSelection.Active || BurdenSelection.CanCancel;

    /// <summary>
    /// Rewords the discard prompt, whose vanilla copy is about leaving without starting a new
    /// timeline. Runs after <c>InitializeQuitMenu</c> has filled the shared ConfirmMenu in, so this
    /// only has to overwrite the two strings.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIWindowController), "InitializeQuitMenu")]
    private static void InitializeQuitMenu_Postfix()
    {
        if (!BurdenSelection.Active) return;

        try
        {
            if (WindowManager.GetWindowController(WindowID.ConfirmMenu) is not ConfirmMenuWindowController menu)
                return;

            menu.SetTitleText("Discard burden changes?", isLocID: false);
            menu.SetDescriptionText(
                "Your burdens will be reverted to as they were when you opened this screen.", isLocID: false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] Could not reword the discard prompt: {ex.Message}");
        }
    }

    // ── Explaining what this screen is ─────────────────────────────────────────

    // The vanilla copy for the timeline row is all about starting a new NG+ thread ("Off to a new
    // thread, traveler?"), which describes nothing this screen now does. These replace the three
    // description boxes and the row's own label.
    private const string TimelineTitle = "Archipelago Burdens";

    private const string ConfirmButtonLabel = "CONFIRM";

    private const string TimelineDescription =
        "Choose the burdens for this Archipelago then click confirm. Your choice lasts the whole run. " + 
        "These are not required, but provide an optional challenge for experienced players.";

    // The controls box is a narrow strip under the description, so this stays to one line.
    private const string TimelineControls =
        "Choose carefully as these are permanent for this Archipelago world. "
        + "The only method to change these later is to enable Debug Keys in the " +
        "Archipelago settings and press 6 to view this menu again.";

    /// <summary>
    /// Rewrites the title / description / controls boxes for the timeline row
    /// (<c>BurdenType.None</c>), which is the only entry whose copy is about New Game+ rather than
    /// about a burden. Burden entries keep their own text untouched.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIDescriptionBoxEntry), "DisplayDescriptionBox")]
    private static void DisplayDescriptionBox_Postfix(
        NewGamePlusOmniUIDescriptionBoxEntry __instance, NewGamePlusOmniUIDescriptionEventArgs args)
    {
        if (!BurdenSelection.Active || args == null || args.BurdenType != BurdenType.None) return;

        try
        {
            var traverse = Traverse.Create(__instance);

            // Both fields are declared on the generic base BaseOmniUIDescriptionBoxEntry<,>.
            var titleText = traverse.Field("m_titleText").GetValue<TMP_Text>();
            if (titleText == null) return;

            switch (traverse.Field("m_descriptionType")
                            .GetValue<NewGamePlusOmniUIDescriptionBoxEntry.NewGamePlusOmniUIDescriptionBoxType>())
            {
                case NewGamePlusOmniUIDescriptionBoxEntry.NewGamePlusOmniUIDescriptionBoxType.Title:
                    titleText.text = TimelineTitle;
                    break;
                case NewGamePlusOmniUIDescriptionBoxEntry.NewGamePlusOmniUIDescriptionBoxType.Description:
                    titleText.text = TimelineDescription;
                    break;
                case NewGamePlusOmniUIDescriptionBoxEntry.NewGamePlusOmniUIDescriptionBoxType.Controls:
                    titleText.text = TimelineControls;
                    break;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] Could not relabel the burden picker description: {ex.Message}");
        }
    }

    /// <summary>
    /// Relabels the timeline row's action button, whose prefab text reads as entering a new
    /// timeline, and guarantees it stays usable.
    ///
    /// <para>The button normally gates itself on <c>CanEnterNewGamePlus</c>. That is true for an AP
    /// run at NG+0 with the level selector pinned, but forcing it makes "confirm always works" a
    /// property of the picker rather than something inferred from two other patches.</para>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIEnterButton), nameof(NewGamePlusOmniUIEnterButton.UpdateState))]
    private static void EnterButton_UpdateState_Postfix(NewGamePlusOmniUIEnterButton __instance)
    {
        if (!BurdenSelection.Active || __instance.BurdenType != BurdenType.None) return;

        try
        {
            var traverse = Traverse.Create(__instance);
            traverse.Property(nameof(NewGamePlusOmniUIEnterButton.IsButtonActive)).SetValue(true);

            var enterText = traverse.Field("m_enterText").GetValue<TMP_Text>();
            if (enterText != null) enterText.text = ConfirmButtonLabel;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] Could not relabel the burden picker confirm button: {ex.Message}");
        }
    }

    /// <summary>
    /// Drops the "Unlock All Burdens" row. It buys the ability to pick any burden, which an AP run
    /// is handed for free, so it is a row that can only ever say "already done".
    ///
    /// <para>Deactivating the entry is the game's own way of removing it. Vanilla does exactly this
    /// once <c>TimesBeatenTraitor</c> reaches 7. <c>UpdateActiveArrays</c> calls this method on every
    /// entry and only then rebuilds <c>ActiveEntryArray</c> from the still-active children, so the
    /// row drops out of keyboard navigation and the indices stay consistent.</para>
    ///
    /// <para>No restore needed on close: this only hides while the picker is active, and the next
    /// <c>UpdateActiveArrays</c> after that hands the decision back to the game.</para>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIEntry), nameof(NewGamePlusOmniUIEntry.UpdateActive))]
    private static void Entry_UpdateActive_Postfix(NewGamePlusOmniUIEntry __instance)
    {
        if (!BurdenSelection.Active || !__instance.IsUnlockNGButton) return;

        if (__instance.gameObject.activeSelf) __instance.gameObject.SetActive(false);
    }

    /// <summary>
    /// The timeline row relabels itself to "Activate Timeline (NG+n)" on every state update, so the
    /// replacement has to be reapplied here rather than written once.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NewGamePlusOmniUIEntry), nameof(NewGamePlusOmniUIEntry.UpdateState))]
    private static void Entry_UpdateState_Postfix(NewGamePlusOmniUIEntry __instance)
    {
        if (!BurdenSelection.Active || !__instance.IsNGPlusButton) return;

        try
        {
            var traverse = Traverse.Create(__instance);

            var titleText = traverse.Field("m_titleText").GetValue<TMP_Text>();
            if (titleText != null) titleText.text = TimelineTitle;

            // m_equipButtonGO holds the row's two arrows and the number between them - here the NG+
            // level selector, which the picker pins and ignores, so it is dead UI. The window does
            // exactly this for the "Unlock All Burdens" row, which has no level to change either.
            var equipButtonGO = traverse.Field("m_equipButtonGO").GetValue<GameObject>();
            if (equipButtonGO != null && equipButtonGO.activeSelf)
                equipButtonGO.SetActive(false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[AP] Could not relabel the burden picker timeline row: {ex.Message}");
        }
    }

    /// <summary>
    /// Keeps the speech bubble on our text.
    ///
    /// <para>The flavour line is picked at random from the controller's <c>m_welcomeDialogue</c>
    /// whenever a dialogue event is broadcast, so setting it once when the picker opens is not
    /// enough. Selecting entries re-broadcasts and would put the New Game+ chatter back.</para>
    ///
    /// <para><c>RefreshText</c> is patched for the same reason on a language change, where it
    /// re-resolves the stored loc ID.</para>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PurchaseBoxDialogueController), "OnPurchaseDialogueUpdated")]
    private static void OnPurchaseDialogueUpdated_Postfix(PurchaseBoxDialogueController __instance)
    {
        if (BurdenSelection.OwnsBubble(__instance)) BurdenSelection.ApplyBubbleText();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PurchaseBoxDialogueController), "RefreshText")]
    private static void RefreshText_Postfix(PurchaseBoxDialogueController __instance)
    {
        if (BurdenSelection.OwnsBubble(__instance)) BurdenSelection.ApplyBubbleText();
    }

    // ── Run-wide burden behavior ───────────────────────────────────────────────

    /// <summary>
    /// An AP run sits at NG+0 with <c>HighestNGPlusBeaten = -1</c>, which locks every <c>*BossUp</c>
    /// and <c>*BiomeUp</c> burden: the entries render as "???" and, worse,
    /// <c>GetBurdenLevel</c>/<c>GetBurdenWeight</c>/<c>GetBurdenStatGain</c> all return 0 even for a
    /// level that was successfully stored, so the burden would be chosen but inert.
    ///
    /// <para><see cref="BurdenSelection.Open"/> also sets <c>BurdenNGUnlocked</c>, which is what
    /// actually persists. This patch covers the window between a save being loaded and the picker
    /// being opened, and saves written before this feature existed.</para>
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BurdenManager), nameof(BurdenManager.IsBurdenUnlocked))]
    private static bool IsBurdenUnlocked_Prefix(BurdenType burdenType, ref bool __result)
    {
        if (!APClient.APSaveActive) return true;

        // Still honor Disabled: those entries are hidden in the UI and must not read as available.
        var data = BurdenLibrary.GetBurdenData(burdenType);
        __result = data != null && !data.Disabled;
        return false;
    }

    /// <summary>
    /// Burdens normally pay +5% gold per point of weight over the NG requirement, capped at +25%.
    /// An Archipelago seed prices its manor upgrades at generation time with no adjustment based on
    /// the player's burden choices. So burdens stay pure difficulty and the economy remains as intended.
    ///
    /// <para>Zeroing the source rather than the consumer also keeps the picker honest: the purchase
    /// box reads the same function and will show +0%.</para>
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BurdenManager), nameof(BurdenManager.GetGoldGainMod))]
    private static bool GetGoldGainMod_Prefix(ref bool isMax, ref float __result)
    {
        if (!APClient.APSaveActive) return true;

        isMax = false;
        __result = 0f;
        return false;
    }
}
