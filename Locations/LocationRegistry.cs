using System.Collections.Generic;

namespace RL2Archipelago.Locations;

/// <summary>
/// Central registry of Archipelago location IDs and their display names.
///
/// Numeric IDs here MUST stay in lockstep with the Python .apworld
/// (<c>apworld/rogue_legacy_2/locations_and_regions.py</c>). Renumbering an
/// existing ID would invalidate any generated multiworld seeds that embed it.
/// </summary>
public static class LocationRegistry
{
    /// <summary>
    /// Shared base offset with <c>apworld/items.py</c>. Every location ID is
    /// <c>BASE_ID + category offset + index</c>, which keeps IDs readable in
    /// logs and leaves room to grow each category without renumbering.
    /// </summary>
    public const long BASE_ID = 0xBEEF0000L;

    private const long BOSS_KILL_OFFSET = 0x100;

    // ── Boss kill locations ──────────────────────────────────────────────────

    public const long CastleBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 0;
    public const long BridgeBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 1;
    public const long ForestBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 2;
    public const long StudyBossDefeated  = BASE_ID + BOSS_KILL_OFFSET + 3;
    public const long TowerBossDefeated  = BASE_ID + BOSS_KILL_OFFSET + 4;
    public const long CaveBossDefeated   = BASE_ID + BOSS_KILL_OFFSET + 5;
    public const long GardenBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 6;
    public const long FinalBossDefeated  = BASE_ID + BOSS_KILL_OFFSET + 7;

    /// <summary>Human-readable name for each location ID, used in logs and UI.</summary>
    public static readonly IReadOnlyDictionary<long, string> Names = new Dictionary<long, string>
    {
        [CastleBossDefeated] = "Citadel Agartha - Estuary Lamech Defeated",
        [BridgeBossDefeated] = "Axis Mundi - Void Beasts Defeated",
        [ForestBossDefeated] = "Kerguelen Plateau - Estuary Naamah Defeated",
        [StudyBossDefeated]  = "Stygian Study - Estuary Enoch Defeated",
        [TowerBossDefeated]  = "Sun Tower - Estuary Irad Defeated",
        [CaveBossDefeated]   = "Pishon Dry Lake - Estuary Tubal Defeated",
        [GardenBossDefeated] = "Garden of Eden - Jonah Defeated",
        [FinalBossDefeated]  = "Castle Hamson - The Traitor Defeated",
    };

    /// <summary>
    /// Maps the <see cref="PlayerSaveFlag"/> the game sets on a boss kill (the
    /// non-"FirstTime" flag — see <c>BossRoomController.SetBossFlagDefeated</c>)
    /// to the corresponding Archipelago location ID.
    /// Returns <c>null</c> if the flag isn't a tracked boss kill.
    /// </summary>
    public static long? FromBossSaveFlag(PlayerSaveFlag flag) => flag switch
    {
        PlayerSaveFlag.CastleBoss_Defeated => CastleBossDefeated,
        PlayerSaveFlag.BridgeBoss_Defeated => BridgeBossDefeated,
        PlayerSaveFlag.ForestBoss_Defeated => ForestBossDefeated,
        PlayerSaveFlag.StudyBoss_Defeated  => StudyBossDefeated,
        PlayerSaveFlag.TowerBoss_Defeated  => TowerBossDefeated,
        PlayerSaveFlag.CaveBoss_Defeated   => CaveBossDefeated,
        PlayerSaveFlag.GardenBoss_Defeated => GardenBossDefeated,
        PlayerSaveFlag.FinalBoss_Defeated  => FinalBossDefeated,
        _ => null,
    };
}
