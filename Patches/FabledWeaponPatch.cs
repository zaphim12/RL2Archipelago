using HarmonyLib;
using RL2Archipelago.Items;
using System.Collections.Generic;
using System.Linq;

namespace RL2Archipelago.Patches;

/// <summary>
/// Implements <see cref="FabledWeaponMode.Unlocked"/>: every class can use its fabled weapon as
/// soon as that class itself is unlocked.
///
/// <para>Done by answering <c>LineageWindowController.IsFabledWeaponUnlocked</c> rather than by
/// granting the 15 Soul Shop entries. Owning an entry is what the game reads as having paid for it
/// (see the soul-cost note in <see cref="SoulShopPatch"/>), so granting them would mean granting
/// and then exempting 15 entries to express something that is not really save state at all. It also
/// gets the "so long as you have that class" part for free: the question is only ever asked about
/// an heir that exists, and an heir only exists for a class the player has unlocked.</para>
///
/// <para>Nothing here writes save data, so turning the option off again simply stops answering.</para>
/// </summary>
[HarmonyPatch]
internal static class FabledWeaponPatch
{
    /// <summary>
    /// The classes that actually have a fabled weapon, derived from the same table the grant path
    /// uses plus Knight, which has no manor slot of its own.
    ///
    /// <para>Needed because vanilla also returns false for a class with no variant at all: the
    /// handful of <c>ClassType</c> values the game never ships as a playable class. Forcing true
    /// for one of those would show the swap prompt for a weapon that does not exist, since
    /// <c>OnVerticalButtonPressed</c> has no case to swap it to.</para>
    /// </summary>
    private static readonly HashSet<ClassType> s_classesWithFabledWeapon =
    [
        ClassType.SwordClass,  // Knight
        .. GameConstants.ManorSlots
            .Where(slot => slot.IsClassUnlock)
            .Select(slot => SkillTreeLogicHelper.GetClassTypeFromSkill(slot.SkillTree))
            .Where(classType => classType != ClassType.None),
    ];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LineageWindowController), "IsFabledWeaponUnlocked")]
    private static void IsFabledWeaponUnlocked_Postfix(CharacterData charData, ref bool __result)
    {
        if (__result) return;
        if (!APClient.IsSessionActive) return;
        if (APClient.FabledWeapons != FabledWeaponMode.Unlocked) return;
        if (charData == null) return;

        // Preserved deliberately: vanilla refuses the fabled weapon to an heir whose kit was
        // randomized by a trait, and that is a gameplay rule rather than a cost gate, so this mode
        // has no business overriding it.
        if (charData.TraitOne == TraitType.RandomizeKit || charData.TraitTwo == TraitType.RandomizeKit)
            return;

        if (!s_classesWithFabledWeapon.Contains(charData.ClassType)) return;

        __result = true;
    }
}
