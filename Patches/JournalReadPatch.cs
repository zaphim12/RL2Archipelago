using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

/// <summary>
/// Reports journal and memory reads to the Archipelago server as location checks.
///
/// Hooks <c>PlayerSaveData.SetJournalsRead</c>, the single write path for all
/// journal/memory count updates. Save-migration calls use <c>additive=false</c>
/// and are ignored; only player-initiated reads (<c>additive=true</c>) are tracked.
///
/// Mode Grouped: fires when a biome's read count reaches the biome total.
/// Mode Individual: fires once per entry, using (count - 1) as the zero-based index.
/// </summary>
[HarmonyPatch]
internal static class JournalReadPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerSaveData), nameof(PlayerSaveData.SetJournalsRead))]
    private static void SetJournalsRead_Postfix(
        JournalCategoryType journalCategoryType,
        JournalType journalType,
        bool additive)
    {
        if (!additive) return;
        if (!APClient.IsSessionActive || APClient.RunState == null) return;
        if (journalType != JournalType.Journal && journalType != JournalType.MemoryFragment) return;

        var mode = APClient.JournalChecksMode;
        if (mode == JournalChecksMode.Disabled) return;

        int readCount  = SaveManager.PlayerSaveData.GetJournalsRead(journalCategoryType, journalType);
        int totalCount = Journal_EV.GetNumJournals(journalCategoryType, journalType);
        if (totalCount <= 0) return;

        if (mode == JournalChecksMode.Grouped)
        {
            if (readCount < totalCount) return;

            long? locationId = journalType == JournalType.Journal
                ? LocationRegistry.GetGroupedJournalLocation(journalCategoryType)
                : LocationRegistry.GetGroupedMemoryLocation(journalCategoryType);

            if (locationId.HasValue)
                APClient.SendLocationCheck(locationId.Value);
        }
        else // Individual
        {
            if (readCount <= 0) return;
            int index = readCount - 1;

            long? locationId = journalType == JournalType.Journal
                ? LocationRegistry.GetIndividualJournalLocation(journalCategoryType, index)
                : LocationRegistry.GetIndividualMemoryLocation(journalCategoryType, index);

            if (locationId.HasValue)
                APClient.SendLocationCheck(locationId.Value);
        }
    }
}
