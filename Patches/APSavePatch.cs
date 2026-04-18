using HarmonyLib;
using System.IO;

namespace RL2Archipelago.Patches;

[HarmonyPatch]
internal static class APSavePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveFileSystem), nameof(SaveFileSystem.PersistentDataPath), MethodType.Getter)]
    private static void PersistentDataPath_Postfix(ref string __result)
    {
        if (APClient.APSaveActive)
            __result = Path.Combine(__result, "AP_Saves", APClient.APSaveDirectoryName);
    }
}
