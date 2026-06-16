using HarmonyLib;
using RL2Archipelago.Items;
using System.Collections.Generic;

namespace RL2Archipelago.Patches;

/// <summary>
/// Handles two concerns for the randomize_starting_class feature:
///
///   1. Ensures the starting class unlock is applied to save data BEFORE heir generation
///      runs. OnSceneLoaded fires before the first Update() tick, so ProcessPendingItems()
///      alone is too late for the initial character select; the patch on
///      CreateRandomCharacters provides the timing guarantee.
///
///   2. Removes Knight (ClassType.SwordClass) from the heir class pool when
///      RandomizeStartingClass is active and the Knight Class item hasn't been received.
///      Knight is hardcoded in Player_EV.STARTING_UNLOCKED_CLASSES which bypasses
///      IsClassUnlocked(), so a postfix on GetAvailableClasses is the only way to exclude it.
/// </summary>
[HarmonyPatch]
internal static class StartingClassPatch
{
    // Runs before CharacterCreator.GenerateRandomCharacter() is called for each heir slot.
    // If the starting class hasn't been persisted to the save yet (first game entry for
    // this seed), apply it now so the class is available for the available-classes query.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LineageWindowController), "CreateRandomCharacters")]
    private static void CreateRandomCharacters_Prefix()
    {
        if (!APClient.IsSessionActive || !APClient.RandomizeStartingClass) return;
        if (APClient.RunState == null || APClient.RunState.StartingClassApplied) return;

        APClient.ApplyStartingClass();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterCreator), nameof(CharacterCreator.GetAvailableClasses))]
    private static void GetAvailableClasses_Postfix(ref List<ClassType> __result)
    {
        if (!APClient.IsSessionActive) return;
        if (!APClient.RandomizeStartingClass) return;
        if (APClient.StartingClassIndex == 0) return; // Knight IS the starting class - keep it
        if (APClient.KnightClassReceived) return;     // Knight was received as an AP item - allow it

        // Safety guard: never leave the heir pool empty.
        if (__result.Count <= 1) return;

        __result.Remove(ClassType.SwordClass);
    }

    // Overrides the hardcoded SwordClass assigned during the tutorial intro sequence.
    // The vanilla method sets up gender/look/name first, then hardcodes ClassType.SwordClass
    // with Knight's default spell/talent. We replace the class and re-apply the kit after.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TutorialRoomController), "CreateStartingCharacter")]
    private static void CreateStartingCharacter_Postfix()
    {
        if (!APClient.IsSessionActive || !APClient.RandomizeStartingClass) return;
        if (APClient.StartingClassIndex <= 0) return; // Knight is the starting class - no change needed

        var slot = GameConstants.ManorSlots[43 + APClient.StartingClassIndex];
        ClassType startingClass = SkillTreeLogicHelper.GetClassTypeFromSkill(slot.SkillTree);
        if (startingClass == ClassType.None) return;

        CharacterData character = SaveManager.PlayerSaveData.CurrentCharacter;
        PlayerController player = PlayerManager.GetPlayerController();

        // Assign the correct class + a random valid weapon/spell/talent kit.
        CharacterCreator.GenerateClass(startingClass, character);

        // Re-apply to the active PlayerController (mirrors what the vanilla method does).
        player.CharacterClass.ClassType = character.ClassType;
        player.CharacterClass.SetAbility(CastAbilityType.Weapon, character.Weapon);
        player.CharacterClass.SetAbility(CastAbilityType.Spell, character.Spell);
        player.CharacterClass.SetAbility(CastAbilityType.Talent, character.Talent);
        player.ResetCharacter();
        player.SetMana(0f, additive: false, runEvents: true);
    }
}
