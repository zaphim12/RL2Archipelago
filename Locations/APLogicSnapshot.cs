using RL2Archipelago.Items;

namespace RL2Archipelago.Locations;

/// <summary>
/// A client-side re-derivation of the apworld's access rules for the four biome-scoped location
/// categories: blueprint chests, fairy chests, journals and memories.
///
/// <para>Archipelago computes reachability in the generator and never sends it to clients. The
/// region graph and its rules are Python closures that cannot cross the wire. The only way to show
/// an "in logic" figure in game is to reproduce the rules here, which is tractable for these four
/// categories because their rules are small and every input is already local: received item counts
/// live in <see cref="APRunState.ReceivedItemCounts"/>, and the gating parameters arrive in slot
/// data. Anything broader (manor depth, mastery) is deliberately out of scope.</para>
///
/// <para><b>Lockstep.</b> This mirrors <c>set_rules</c> in
/// <c>apworld/rogue_legacy_2/locations_and_regions.py</c>. A change to the biome entrance rules or
/// the chest pre-boss rule there must be made here too, or the tracker will quietly lie. See
/// ITEMS_AND_LOGIC.md.</para>
///
/// <para><b>What the number means.</b> Rules 2 and 3 below are the apworld's own anti-tedium gates,
/// not real access requirements. A player can physically walk into Axis Mundi with no stat
/// upgrades at all. So "in logic" here is a generator-sphere figure: a location can be out of logic
/// and still perfectly obtainable.</para>
/// </summary>
internal static class APLogicSnapshot
{
    // Stat-upgrade tier multiplier per biome index; 0 means never gated. Mirrors
    // _BIOME_STAT_UPGRADE_TIERS in locations_and_regions.py.
    private static readonly int[] s_statUpgradeTiers = { 0, 0, 1, 2, 3, 4 };

    // NPC unlocks required per biome index when early_npc_unlocks is on. Mirrors
    // _BIOME_NPC_TIERS in locations_and_regions.py.
    private static readonly int[] s_npcTiers = { 0, 0, 0, 1, 2, 3 };

    /// <summary>True once the multiworld has sent this slot at least one copy of the item.</summary>
    private static bool Has(APRunState runState, long itemId) => runState.ReceivedCount(itemId) > 0;

    /// <summary>
    /// Whether the biome's region is reachable in logic. Covers all three entrance rules, so it is
    /// the only gate journals need and the base gate for everything else in the biome.
    /// Mirrors <c>_biome_access_rules</c> plus gates [4] and [5] in locations_and_regions.py.
    /// </summary>
    public static bool IsBiomeInLogic(int biomeIndex, APRunState runState)
    {
        // ── 1. Hard access: heirlooms, and one teleporter shortcut ───────────
        bool reachable = biomeIndex switch
        {
            0 => true,                                              // Citadel Agartha
            1 => Has(runState, ItemRegistry.HeirloomBouncableDownstrike)
                 || (Has(runState, ItemRegistry.HeirloomAirDash)
                     && Has(runState, ItemRegistry.HeirloomDoubleJump)),
            2 => Has(runState, ItemRegistry.HeirloomBouncableDownstrike)
                 || Has(runState, ItemRegistry.TeleporterKerguelenPlateau),
            3 => Has(runState, ItemRegistry.HeirloomDoubleJump)
                 && Has(runState, ItemRegistry.HeirloomVoidDash),
            4 => Has(runState, ItemRegistry.HeirloomAirDash)
                 && Has(runState, ItemRegistry.HeirloomBouncableDownstrike)
                 && Has(runState, ItemRegistry.HeirloomDoubleJump)
                 && Has(runState, ItemRegistry.HeirloomVoidDash),
            5 => Has(runState, ItemRegistry.HeirloomCaveLantern),
            _ => false,
        };
        if (!reachable) return false;

        // ── 2. Soft gate: received stat upgrades, counted in copies ──────────
        int statPerTier = APClient.StatUpgradesPerBiomeTier;
        if (statPerTier > 0)
        {
            int required = s_statUpgradeTiers[biomeIndex] * statPerTier;
            if (required > 0 && CountStatUpgrades(runState) < required) return false;
        }

        // ── 3. Soft gate: NPC unlocks, counted in slots owned ────────────────
        if (APClient.EarlyNpcUnlocks && CountNpcUnlocks(runState) < s_npcTiers[biomeIndex])
            return false;

        return true;
    }

    /// <summary>
    /// Whether memories are readable at all. Memories sit behind the biome entrance like everything
    /// else in a biome, and additionally need Aesop's Tome; callers that already know the entrance
    /// state combine the two rather than re-deriving it.
    /// </summary>
    public static bool HasAesopsTome(APRunState runState) =>
        Has(runState, ItemRegistry.HeirloomMemory);

    /// <summary>
    /// How many slots of a chest pool of <paramref name="slotCount"/> are in logic, ignoring
    /// whether the biome itself is reachable. Callers combine this with <see cref="IsBiomeInLogic"/>.
    ///
    /// <para>The lowest <c>floor(N * chest_pre_boss_percent / 100)</c> slots are freely in logic;
    /// the rest need the biome boss dead. That the generator gates the <em>highest</em> slots while
    /// <see cref="LocationRegistry.NextBlueprintChestLocation"/> hands out the <em>lowest</em> live
    /// one is deliberate and self-consistent: a player who has not killed the boss naturally
    /// consumes the in-logic end of the pool first.</para>
    /// </summary>
    public static int InLogicChestSlots(int biomeIndex, int slotCount, APRunState runState)
    {
        if (slotCount <= 0) return 0;

        int pct = APClient.ChestPreBossPercent;
        if (pct >= 100 || IsBiomeBossCleared(biomeIndex, runState)) return slotCount;

        return slotCount * pct / 100;  // integer division, matching Python's math.floor
    }

    /// <summary>
    /// Whether this biome's boss has been killed. The apworld gates the deep chest slots on the
    /// boss <em>Cleared</em> event, which in a real playthrough is collected exactly when the boss
    /// is defeated, so the defeat check is the faithful client-side proxy.
    /// </summary>
    public static bool IsBiomeBossCleared(int biomeIndex, APRunState runState) =>
        runState.CheckedLocations.Contains(LocationRegistry.BiomeBossDefeated(biomeIndex));

    /// <summary>
    /// Copies of stat-upgrade manor items received. Counts copies rather than slots, matching
    /// <c>_count_stat_upgrades</c>, because the gate is expressed in upgrade levels.
    /// </summary>
    private static int CountStatUpgrades(APRunState runState)
    {
        int total = 0;
        foreach (long itemId in ItemRegistry.StatUpgradeItemIds)
            total += runState.ReceivedCount(itemId);
        return total;
    }

    /// <summary>
    /// NPC unlock slots owned. Counts slots rather than copies, matching
    /// <c>_count_npcs_unlocked</c>; each is single-level anyway.
    /// </summary>
    private static int CountNpcUnlocks(APRunState runState)
    {
        int total = 0;
        foreach (long itemId in ItemRegistry.NpcItemIds)
            if (Has(runState, itemId)) total++;
        return total;
    }
}
