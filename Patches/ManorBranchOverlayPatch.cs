using HarmonyLib;
using RL2Archipelago.UI;
using System;

namespace RL2Archipelago.Patches;

/// <summary>
/// Wires <see cref="ManorBranchOverlay"/> into the manor window's lifecycle.
///
/// <para>The window prefab is instantiated fresh on every scene load and destroyed by
/// <c>WindowManager.UnloadWindow</c>, so the overlay is rebuilt per open and never cached across
/// scenes.</para>
/// </summary>
[HarmonyPatch]
internal static class ManorBranchOverlayPatch
{
    /// <summary>Build (or rebuild) the overlay and its on-screen hint once the window is fully
    /// open. Runs after OnOpen's own RefreshAllButtons call, so every slot's active state is
    /// already settled.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "OnOpen")]
    private static void OnOpen_Postfix(SkillTreeWindowController __instance)
    {
        if (!APClient.IsSessionActive) return;

        try
        {
            ManorBranchOverlay.Build(__instance);
            ManorBranchPrompt.Build(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[ManorBranchOverlay] Failed to build the overlay.\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>Re-dim lines after a purchase reveals new nodes. Geometry never changes (slot
    /// animations only touch localScale and rotation about the node centre), so this is
    /// alpha-only.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SkillTreeWindowController), "RefreshAllButtons")]
    private static void RefreshAllButtons_Postfix()
    {
        if (!APClient.IsSessionActive) return;

        try
        {
            ManorBranchOverlay.RefreshAlpha();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[ManorBranchOverlay] Failed to refresh line visibility.\n{ex.Message}\n{ex.StackTrace}");
        }
    }
}
