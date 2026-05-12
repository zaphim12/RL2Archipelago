using UnityEngine;

namespace RL2Archipelago.Patches;

internal static class GoldCoinsPatch
{
    internal static void Grant()
    {
        if (SaveManager.PlayerSaveData == null)
        {
            Plugin.Log.LogWarning("[AP] Cannot grant gold coins: PlayerSaveData is not available.");
            return;
        }
        int amount = 500 + Random.Range(0, 11) * 50;
        int prevGold = SaveManager.PlayerSaveData.GoldCollected;
        SaveManager.PlayerSaveData.GoldCollected += amount;
        Messenger<GameMessenger, GameEvent>.Broadcast(GameEvent.GoldChanged, null, new GoldChangedEventArgs(prevGold, SaveManager.PlayerSaveData.GoldCollected));
        Plugin.Log.LogInfo($"[AP] Granted {amount} gold coins.");
    }
}
