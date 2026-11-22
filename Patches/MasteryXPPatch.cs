using HarmonyLib;
using RL2Archipelago.Locations;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Controls how much mastery XP a run earns, and from when.
///
/// <para>Vanilla puts two separate Sage Totem gates in the way, and both have to move for mastery
/// ranks to work as Archipelago checks without the whole category being useless until the sage totem:</para>
///
/// <list type="number">
/// <item><b>Accrual.</b> <c>EnemyHitResponse.CharacterDamaged</c> is the only place the game ever
/// awards mastery XP, and the entire award block is inside a
/// <c>SkillTreeLogicHelper.IsTotemUnlocked()</c> test. Without the Totem a kill earns nothing at
/// all; it is not a hidden tally that the Totem later reveals.</item>
/// <item><b>Banking.</b> <c>PlayerDeathRankTextController.ApplyXPToSaveFile</c> is the only thing
/// that moves <c>RunAccumulatedXP</c> into the per-class XP table, and it is only reached through
/// the death screen's rank tally, which <c>PlayerDeathWindowController</c> skips when the Totem is
/// locked. <c>SaveManager</c> then zeroes <c>RunAccumulatedXP</c> when the next run starts, so
/// opening accrual on its own would just throw the XP away every heir.</item>
/// </list>
///
/// <para>Both are opened here. What is deliberately <em>not</em> touched is what the Totem does
/// besides gating accrual: showing you the numbers, and, via <see cref="MasteryBonusPatch"/>,
/// paying out the stat bonuses those ranks earn. The rank readout on the death screen and the
/// lineage screen, and the Totem NPC itself, all ask <c>IsTotemUnlocked()</c> from their own call
/// sites and still get the honest answer. So ranks accumulate from the first heir, but stay both
/// invisible and inert until the Totem is found, which leaves it well worth finding without
/// letting it strand a whole category of checks.</para>
/// </summary>
[HarmonyPatch]
internal static class MasteryXPPatch
{
    /// <summary>
    /// True only for the duration of one <c>EnemyHitResponse.CharacterDamaged</c> call.
    ///
    /// <para>This is how the accrual gate is opened without opening the display gates: the XP block
    /// and the rank readouts all call the same <c>IsTotemUnlocked()</c>, so the answer has to depend
    /// on who is asking. Scoping a flag around the one vanilla method that awards XP says that
    /// precisely, and leaves the method's own code alone.</para>
    /// </summary>
    private static bool _inEnemyDamage;

    /// <summary>
    /// Whether this seed turns mastery ranks into checks, and so wants the Totem's gates on XP
    /// accrual and on the stat bonuses moved. Governs everything here except the multiplier.
    /// </summary>
    internal static bool MasteryActive =>
        APClient.IsSessionActive && APClient.MasteryChecks != MasteryChecksMode.Disabled;

    /// <summary>
    /// Whether the XP multiplier has anything to do.
    ///
    /// <para>Deliberately not tied to <see cref="MasteryActive"/>. Wanting ranks to come faster (or
    /// slower) is a pacing preference that stands on its own, and a player who left mastery out of
    /// their check pool can still want that. The 100 test is just an early-out, since scaling by
    /// exactly 1 is a no-op.</para>
    /// </summary>
    private static bool XpMultiplierActive =>
        APClient.IsSessionActive && APClient.MasteryXpPercent != 100;

    /// <summary>
    /// Whether the Sage Totem is genuinely owned, read straight from the skill tree rather than
    /// through <c>SkillTreeLogicHelper.IsTotemUnlocked()</c>.
    ///
    /// <para>That distinction matters because <see cref="IsTotemUnlocked_Postfix"/> deliberately
    /// makes that helper lie during the XP award. Anything that needs the honest answer: the
    /// banking below and <see cref="MasteryBonusPatch"/>, has to ask the question the helper
    /// wraps instead of asking the helper, or it would inherit the lie. The accessor is null-safe
    /// the same way the vanilla helper is.</para>
    /// </summary>
    internal static bool SageTotemOwned =>
        SkillTreeManager.GetSkillObjLevel(SkillTreeType.Unlock_Totem) > 0;

    // ── Accrual: let the vanilla XP award run before the Totem is owned ──────

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyHitResponse), "CharacterDamaged")]
    private static void CharacterDamaged_Prefix() => _inEnemyDamage = true;

    /// <summary>
    /// A finalizer rather than a postfix: <c>CharacterDamaged</c> is a long method full of relic and
    /// status-effect work, and if any of it throws, a postfix never runs and the flag would latch on,
    /// which would leave <see cref="IsTotemUnlocked_Postfix"/> lying to every caller for the rest of 
    /// the session.
    /// </summary>
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(EnemyHitResponse), "CharacterDamaged")]
    private static void CharacterDamaged_Finalizer() => _inEnemyDamage = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeLogicHelper), nameof(SkillTreeLogicHelper.IsTotemUnlocked))]
    private static void IsTotemUnlocked_Postfix(ref bool __result)
    {
        if (__result || !_inEnemyDamage || !MasteryActive) return;
        __result = true;
    }

    // ── Rank cap: hand over the game's full range without the Soul Shop ─────

    /// <summary>
    /// Raises the per-class mastery rank cap to the game's true maximum.
    ///
    /// <para>Vanilla starts the cap at 15 and sells the rest one rank at a time through the Soul
    /// Shop's <c>MaxMasteryFlat</c> entry. This is not intended in an Archipelago run. Leaving
    /// it there would quietly cap <c>MasteryLevelsPerClass</c> at 15 and make three quarters of
    /// the rank range unreachable, so the full range is granted outright instead.</para>
    ///
    /// <para>The returned number is one below the reachable rank, not equal to it: vanilla clamps
    /// XP to <c>XP_REQUIRED[GetMaxMasteryRank()]</c> and then <c>GetClassMasteryRank</c> clamps the
    /// derived rank to <c>GetMaxMasteryRank() + 1</c>, which is why an untouched game stops at 15
    /// while this method returns 14. Hence the subtraction rather than the obvious constant.</para>
    /// 
    /// <see cref="LocationRegistry.MAX_MASTERY_RANK_PER_CLASS"/> either way.</para>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Mastery_EV), nameof(Mastery_EV.GetMaxMasteryRank))]
    private static void GetMaxMasteryRank_Postfix(ref int __result)
    {
        if (!MasteryActive) return;

        int lifted = LocationRegistry.MAX_MASTERY_RANK_PER_CLASS - 1;
        if (__result < lifted)
            __result = lifted;
    }

    // ── XP multiplier ────────────────────────────────────────────────────────
    //
    // Applied at PlayerController.SetMasteryXP, the single funnel every point of run XP passes
    // through, so the whole vanilla formula (per-kill cap, enemy rank bonuses, the XP Up manor
    // skill, the NoGoldXPBonus relic) is left to compute itself and only the result is scaled.
    // UpdateFrameAccumulatedXP feeds the floating "+XP" popup from the same number at the same call
    // site, so it is scaled identically; otherwise the popup would disagree with what was banked.

    /// <summary>Scales an XP award, keeping a nonzero award nonzero however low the multiplier.</summary>
    private static int Scale(int amount)
    {
        if (amount <= 0) return amount;
        int scaled = Mathf.RoundToInt(amount * (APClient.MasteryXpPercent / 100f));
        return scaled < 1 ? 1 : scaled;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.SetMasteryXP))]
    private static void SetMasteryXP_Prefix(ref int value, bool additive)
    {
        // Only the additive path is a run XP award; a non-additive call would be setting an absolute
        // total, which must not be scaled. Nothing in the game does that today.
        if (!additive || !XpMultiplierActive) return;
        value = Scale(value);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.UpdateFrameAccumulatedXP))]
    private static void UpdateFrameAccumulatedXP_Prefix(ref float amount)
    {
        if (!XpMultiplierActive) return;
        amount = Scale(Mathf.RoundToInt(amount));
    }

    // ── Banking: commit the run's XP when the Totem is not there to do it ────

    /// <summary>
    /// Moves <c>RunAccumulatedXP</c> into the current class's total when the death screen is about
    /// to skip its rank tally.
    ///
    /// <para>This is the one piece of <c>ApplyXPToSaveFile</c> worth reproducing, because it is not
    /// a formula, it is a value being moved from one save field to another, and the clamping stays
    /// inside the vanilla <c>SetClassXP</c> that does it. Running here, in <c>OnOpen</c>, means it
    /// happens before either death-screen coroutine checks <c>m_masteryUnlocked</c>, and
    /// <see cref="MasteryRankPatch"/>'s postfix on <c>SetClassXP</c> picks the new ranks up and
    /// sends their checks.</para>
    ///
    /// <para>Deliberately skipped when the Totem <em>is</em> owned. In that case, vanilla's tally
    /// will do it, and doing it here too would just hand that tally a zero and animate nothing. 
    /// The high-mastery achievement vanilla also grants there is left alone on purpose - an 
    /// achievement is not run state, and granting one off the back of a mod's bookkeeping is 
    /// not this patch's business.</para>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerDeathWindowController), "OnOpen")]
    private static void OnOpen_Postfix()
    {
        if (!MasteryActive) return;
        if (SageTotemOwned) return;

        var saveData = SaveManager.PlayerSaveData;
        int runXP = saveData.RunAccumulatedXP;
        if (runXP <= 0) return;

        var classType = saveData.CurrentCharacter.ClassType;
        if (classType == ClassType.None) return;

        saveData.SetClassXP(classType, runXP, additive: true, forceOverride: false,
                            ignoreMaxLevelClamping: false);
        saveData.RunAccumulatedXP = 0;

        Plugin.Log.LogInfo(
            $"[MasteryXP] Banked {runXP} mastery XP for {classType} with the Sage Totem unfound; " +
            $"rank is now {saveData.GetClassMasteryRank(classType)}.");
    }
}
