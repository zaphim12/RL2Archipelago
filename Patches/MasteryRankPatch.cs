using HarmonyLib;
using RL2Archipelago.Items;
using RL2Archipelago.Locations;
using System;
using System.Collections.Generic;

namespace RL2Archipelago.Patches;

/// <summary>
/// Reports class mastery rank gains to the Archipelago server as location checks.
///
/// <para>Hooks <c>PlayerSaveData.SetClassXP</c>, the single write path for the per-class XP
/// table that ranks are derived from. In normal play that is reached once per heir, from
/// <c>PlayerDeathRankTextController.ApplyXPToSaveFile</c> on the death screen. The run's XP is
/// only banked when the heir dies, and the same postfix also covers the NG+ rescale and
/// challenge save/restore paths, which move ranks without any run having ended.</para>
///
/// <para>Rather than work out what changed, each pass re-derives the current rank and reports
/// every tracked rank up to it. Ranks come from XP and XP only ever grows, so that is monotone,
/// and <see cref="APClient.SendLocationCheck"/> deduplicates, which makes the pass safe to run
/// on a path that temporarily zeroes the table (a challenge) and on one that re-raises it.</para>
/// </summary>
[HarmonyPatch]
internal static class MasteryRankPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerSaveData), nameof(PlayerSaveData.SetClassXP))]
    private static void SetClassXP_Postfix()
    {
        if (!APClient.IsSessionActive || APClient.RunState == null) return;

        var checkedLocations = APClient.RunState.CheckedLocations;

        switch (APClient.MasteryChecks)
        {
            case MasteryChecksMode.Total:
                SendRanksUpTo(
                    Mastery_EV.GetTotalMasteryRank(),
                    LocationRegistry.MasteryTotalLevels,
                    LocationRegistry.TotalMasteryLocation,
                    checkedLocations);
                break;

            case MasteryChecksMode.PerClass:
                for (int position = 0; position < GameConstants.MasteryClassTypes.Length; position++)
                {
                    var classType = GameConstants.MasteryClassTypes[position];
                    if (classType == ClassType.None) continue;

                    int classPosition = position;
                    SendRanksUpTo(
                        SaveManager.PlayerSaveData.GetClassMasteryRank(classType),
                        LocationRegistry.MasteryLevelsPerClass,
                        level => LocationRegistry.ClassMasteryLocation(classPosition, level),
                        checkedLocations);
                }
                break;
        }
    }

    /// <summary>
    /// Sends a check for every rank from 1 up to <paramref name="rank"/> that this seed tracks
    /// and has not already sent.
    ///
    /// <para>The already-checked test is here rather than left to
    /// <see cref="APClient.SendLocationCheck"/> because the per-class mode walks up to 225
    /// ranks on every call, and all but a couple of them are repeats.</para>
    /// </summary>
    private static void SendRanksUpTo(
        int rank, int trackedLevels, Func<int, long> locationForLevel, HashSet<long> checkedLocations)
    {
        int highest = rank < trackedLevels ? rank : trackedLevels;
        for (int level = 1; level <= highest; level++)
        {
            long locationId = locationForLevel(level);
            if (!checkedLocations.Contains(locationId))
                APClient.SendLocationCheck(locationId);
        }
    }
}
