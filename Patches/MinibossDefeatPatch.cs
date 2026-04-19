using HarmonyLib;
using RL2Archipelago.Locations;

namespace RL2Archipelago.Patches;

/// <summary>
/// Reports miniboss kills to the Archipelago server as location checks.
///
/// <para>
/// Hooks <c>PlayerSaveData.SetFlag</c> — the single write path for all
/// <c>PlayerSaveFlag</c> values — and reacts only when a tracked miniboss
/// defeat flag is written as <c>true</c>.  This covers both the Study
/// minibosses (Sword Knight, Spear Knight) and the Cave minibosses
/// (White, Black) without needing to find each room controller separately.
/// </para>
///
/// <para>
/// We use a postfix so the save write happens first; our send is idempotent
/// on the AP server and <see cref="APClient.RunState"/> deduplicates locally.
/// </para>
/// </summary>
[HarmonyPatch]
internal static class MinibossDefeatPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerSaveData), nameof(PlayerSaveData.SetFlag))]
    private static void SetFlag_Postfix(PlayerSaveFlag flag, bool value)
    {
        if (!value) return;
        if (!APClient.IsConnected) return;

        var locationId = LocationRegistry.FromMinibossSaveFlag(flag);
        if (locationId is null)
            return;

        APClient.SendLocationCheck(locationId.Value);
    }
}
