using HarmonyLib;

namespace RL2Archipelago.Patches;

[HarmonyPatch]
internal static class StartingRuneSuppressPatch
{
    private static bool _lifestealLevelLocked;
    private static bool _lifestealBlueprintLocked;
    private static bool _magnetLevelLocked;
    private static bool _magnetBlueprintLocked;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EquipmentSaveData), "SetStartingEquipmentSaveData")]
    private static void SetStartingEquipmentSaveData_Prefix(EquipmentSaveData __instance)
    {
        if (!APClient.APSaveActive) return;

        _lifestealLevelLocked     = __instance.RuneDict[RuneType.Lifesteal].UpgradeLevel == (int)FoundState.NotFound;
        _lifestealBlueprintLocked = __instance.RuneDict[RuneType.Lifesteal].UpgradeBlueprintsFound == 0;
        _magnetLevelLocked        = __instance.RuneDict[RuneType.Magnet].UpgradeLevel == (int)FoundState.NotFound;
        _magnetBlueprintLocked    = __instance.RuneDict[RuneType.Magnet].UpgradeBlueprintsFound == 0;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EquipmentSaveData), "SetStartingEquipmentSaveData")]
    private static void SetStartingEquipmentSaveData_Postfix(EquipmentSaveData __instance)
    {
        if (_lifestealLevelLocked)
            __instance.RuneDict[RuneType.Lifesteal].UpgradeLevel = (int)FoundState.NotFound;
        if (_lifestealBlueprintLocked)
            __instance.RuneDict[RuneType.Lifesteal].UpgradeBlueprintsFound = 0;
        if (_magnetLevelLocked)
            __instance.RuneDict[RuneType.Magnet].UpgradeLevel = (int)FoundState.NotFound;
        if (_magnetBlueprintLocked)
            __instance.RuneDict[RuneType.Magnet].UpgradeBlueprintsFound = 0;
    }
}
