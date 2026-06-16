using HarmonyLib;

namespace RL2Archipelago.Patches;

[HarmonyPatch]
internal static class ItemDescriptionPatch
{
    // Spells: shown as "???" on heir screen when GetSpellSeenState returns false
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerSaveData), nameof(PlayerSaveData.GetSpellSeenState))]
    private static void SpellSeenState_Postfix(ref bool __result)
    {
        if (!APClient.IsSessionActive) return;
        __result = true;
    }

    // Traits: shown as "???" on heir screen when GetTraitSeenState < SeenOnce
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TraitManager), nameof(TraitManager.GetTraitSeenState))]
    private static void TraitSeenState_Postfix(ref TraitSeenState __result)
    {
        if (!APClient.IsSessionActive) return;
        if (__result < TraitSeenState.SeenOnce)
            __result = TraitSeenState.SeenOnce;
    }

    // Relics: shown as "???" on heir screen and in-run pedestal when WasSeen is false
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicObj), nameof(RelicObj.WasSeen), MethodType.Getter)]
    private static void RelicWasSeen_Postfix(ref bool __result)
    {
        if (!APClient.IsSessionActive) return;
        __result = true;
    }
}
