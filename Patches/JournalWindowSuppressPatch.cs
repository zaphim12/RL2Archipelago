using HarmonyLib;
using RL_Windows;
using RL2Archipelago.UI;

namespace RL2Archipelago.Patches;

/// <summary>
/// Suppresses the AP notification HUD while a journal or memory dialogue window is open,
/// so the pop-up appears after the player finishes reading rather than during.
///
/// TriggerJournal opens the dialogue window (and calls SetJournalsRead, which triggers
/// JournalReadPatch and enqueues a send notification). We set IsJournalWindowOpen = true
/// in its Postfix — after the body runs but before the next Tick() drains the queue.
/// OnJournalWindowClosed clears the flag once the player dismisses the text window.
/// OnDisable acts as a safety reset for scene transitions or prop pooling.
/// </summary>
[HarmonyPatch]
internal static class JournalWindowSuppressPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(JournalSpecialPropController), nameof(JournalSpecialPropController.TriggerJournal))]
    private static void TriggerJournal_Postfix(JournalSpecialPropController __instance)
    {
        var openWindow = Traverse.Create(__instance).Field<WindowID>("m_openWindow").Value;
        if (openWindow != WindowID.None)
            APNotifications.IsJournalWindowOpen = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(JournalSpecialPropController), "OnJournalWindowClosed")]
    private static void OnJournalWindowClosed_Postfix()
    {
        APNotifications.IsJournalWindowOpen = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(JournalSpecialPropController), "OnDisable")]
    private static void OnDisable_Postfix()
    {
        APNotifications.IsJournalWindowOpen = false;
    }
}
