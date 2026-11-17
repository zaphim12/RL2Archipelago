namespace RL2Archipelago.Patches;

/// <summary>
/// Grants the "Gold Coins" filler item.
///
/// <para>Gold is the only item the mod hands out that the player can spend, which makes it the only
/// one <see cref="Items.APItemReconciler"/> cannot reconcile: once it is in the player's pocket there
/// is no way to tell a spent coin from a lost one. So it is tracked by ordinal instead - the Nth gold
/// item received is granted exactly once, recorded in <c>APRunState.AppliedItemCounts</c>, and never
/// recomputed.</para>
///
/// <para>The amount is therefore derived from the room seed and the ordinal rather than drawn at
/// random, so the same gold item is always worth the same thing no matter when it is applied.</para>
/// </summary>
internal static class GoldCoinsPatch
{
    private const int BaseAmount = 500;
    private const int StepAmount = 50;
    private const int StepCount = 11; // 500-1000 in steps of 50, matching the original roll

    /// <summary>Amount the <paramref name="ordinal"/>-th gold item is worth for this seed.</summary>
    internal static int AmountFor(string seed, int ordinal) =>
        BaseAmount + (int)(Hashing.Fnv1a32($"{seed}:gold:{ordinal}") % StepCount) * StepAmount;

    /// <summary>
    /// Adds the ordinal's gold to the player's purse. Returns false (without granting) when save data
    /// is not available, so the caller can leave the ordinal unapplied and retry later rather than
    /// losing it.
    /// </summary>
    internal static bool TryGrant(string seed, int ordinal)
    {
        if (SaveManager.PlayerSaveData == null)
        {
            Plugin.Log.LogWarning("[AP] Cannot grant gold coins yet: PlayerSaveData is not available.");
            return false;
        }

        int amount = AmountFor(seed, ordinal);
        int prevGold = SaveManager.PlayerSaveData.GoldCollected;
        SaveManager.PlayerSaveData.GoldCollected += amount;
        Messenger<GameMessenger, GameEvent>.Broadcast(
            GameEvent.GoldChanged,
            null,
            new GoldChangedEventArgs(prevGold, SaveManager.PlayerSaveData.GoldCollected));

        Plugin.Log.LogInfo($"[AP] Granted {amount} gold coins.");
        return true;
    }
}
