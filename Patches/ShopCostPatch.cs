using HarmonyLib;
using UnityEngine;

namespace RL2Archipelago.Patches;

/// <summary>
/// Removes the gold cost from blacksmith and enchantress purchases in AP mode.
/// Blacksmith items cost only equipment ore; enchantress runes cost only rune ore.
///
/// <para>Zeroing <c>GoldCostToUpgrade</c> on the data objects cascades correctly through
/// all game systems that read this property:
/// <list type="bullet">
///   <item><c>EquipmentManager.CanAfford / RuneManager.CanAfford</c> – gold check becomes
///   <c>bank &gt;= 0</c> (always true), so only ore availability gates purchases.</item>
///   <item><c>PurchaseEquipment / ConfirmUpgradePurchase / PurchaseRune</c> – deduct
///   0 gold, so the player's gold is untouched.</item>
///   <item><c>OnPurchaseFailed</c> – the gold path is unreachable; failures always
///   display "no ore" rather than "no money".</item>
///   <item>Any cost display that reads the property shows 0.</item>
/// </list></para>
///
/// <para>The <c>PurchaseBoxDialogueController</c> patch hides the gold display
/// element so the UI makes it clear no gold is required.</para>
/// </summary>
[HarmonyPatch]
internal static class ShopCostPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EquipmentObj), "get_GoldCostToUpgrade")]
    private static void EquipmentGoldCost_Postfix(ref int __result)
    {
        if (APClient.IsConnected) __result = 0;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RuneObj), "get_GoldCostToUpgrade")]
    private static void RuneGoldCost_Postfix(ref int __result)
    {
        if (APClient.IsConnected) __result = 0;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PurchaseBoxDialogueController), "Awake")]
    private static void HideGoldDisplay_Postfix(PurchaseBoxDialogueController __instance)
    {
        if (!APClient.IsConnected) return;
        var goldGO = Traverse.Create(__instance)
            .Field<GameObject>("m_purchaseBoxGoldGO").Value;
        if (goldGO != null)
            goldGO.SetActive(false);
    }
}
