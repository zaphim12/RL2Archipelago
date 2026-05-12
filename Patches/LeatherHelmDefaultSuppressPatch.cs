using HarmonyLib;

namespace RL2Archipelago.Patches;

[HarmonyPatch]
internal static class LeatherHelmDefaultSuppressPatch
{
    private static bool _wasLocked;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EquipmentSaveData), "SetStartingEquipmentSaveData")]
    private static void SetStartingEquipmentSaveData_Prefix(EquipmentSaveData __instance)
    {
        _wasLocked = APClient.APSaveActive &&
            __instance.HeadEquipmentDict[EquipmentType.GEAR_BONUS_WEIGHT].UpgradeLevel == (int)FoundState.NotFound;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EquipmentSaveData), "SetStartingEquipmentSaveData")]
    private static void SetStartingEquipmentSaveData_Postfix(EquipmentSaveData __instance)
    {
        if (!_wasLocked) return;
        __instance.HeadEquipmentDict[EquipmentType.GEAR_BONUS_WEIGHT].UpgradeLevel = (int)FoundState.NotFound;
    }
}
