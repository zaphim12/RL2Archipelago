using HarmonyLib;

namespace RL2Archipelago.Patches;

/// <summary>
/// Withholds the stat bonuses that mastery ranks grant until the Sage Totem is found.
///
/// <para><see cref="MasteryXPPatch"/> lets ranks accrue from the first heir so that mastery check 
/// locations don't require the Sage Totem. On its own that would also hand over every rank's stat
/// bonus for free, which is the what the Totem should actually provide. Splitting the two gives the
/// item a real job again: the ranks are yours to earn whenever, but the payout waits for the Totem,
/// and finding it cashes in everything banked so far at once.</para>
///
/// <para>Done in one place because the game has one: <c>Mastery_EV.GetTotalMasteryBonus</c> is the
/// sole read path for every mastery bonus there is. All twenty call sites go through it (the
/// <c>PlayerController.Initialize*Mods</c> stat builders, <c>Economy_EV</c>'s ore and aether gain,
/// <c>ProjectileManager</c>'s spin kick damage, and the Totem window's own display), so zeroing its
/// result is sufficient.</para>
///
/// <para>On timing: most of those call sites cache the result into a field (<c>VitalityAdd</c>,
/// <c>StrengthMod</c>, and so on) rather than reading it live, so the payout needs a stat rebuild
/// to land. <see cref="Items.APItemReconciler"/> forces one whenever it writes a manor level, which
/// includes receiving the Totem, so this happens the moment the item arrives and needs nothing from
/// the player. Spin kick damage does not even wait for that, since <c>ProjectileManager</c> reads
/// the bonus live per projectile.</para>
///
/// <para>The Totem window's own call site is unreachable without the Totem, so the gate is a no-op
/// there.</para>
/// </summary>
[HarmonyPatch]
internal static class MasteryBonusPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Mastery_EV), nameof(Mastery_EV.GetTotalMasteryBonus))]
    private static void GetTotalMasteryBonus_Postfix(ref float __result)
    {
        if (__result == 0f) return;
        if (!MasteryXPPatch.MasteryActive) return;
        if (MasteryXPPatch.SageTotemOwned) return;

        __result = 0f;
    }
}
