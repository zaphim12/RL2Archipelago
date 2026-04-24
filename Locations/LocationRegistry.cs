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

    private const long BOSS_KILL_OFFSET     = 0x100;
    private const long MINIBOSS_KILL_OFFSET = 0x200;
    private const long HEIRLOOM_OFFSET      = 0x300;
    private const long BLUEPRINT_OFFSET     = 0x400;
    private const long RUNE_OFFSET          = 0x500;

    // ── Boss kill locations ──────────────────────────────────────────────────

    public const long CastleBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 0;
    public const long BridgeBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 1;
    public const long ForestBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 2;
    public const long StudyBossDefeated  = BASE_ID + BOSS_KILL_OFFSET + 3;
    public const long TowerBossDefeated  = BASE_ID + BOSS_KILL_OFFSET + 4;
    public const long CaveBossDefeated   = BASE_ID + BOSS_KILL_OFFSET + 5;
    public const long GardenBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 6;
    public const long FinalBossDefeated  = BASE_ID + BOSS_KILL_OFFSET + 7;

    // ── Heirloom interaction locations ──────────────────────────────────────

    public const long HeirloomAirDash             = BASE_ID + HEIRLOOM_OFFSET + 0;
    public const long HeirloomDoubleJump          = BASE_ID + HEIRLOOM_OFFSET + 1;
    public const long HeirloomMemory              = BASE_ID + HEIRLOOM_OFFSET + 2;
    public const long HeirloomBouncableDownstrike = BASE_ID + HEIRLOOM_OFFSET + 3;
    public const long HeirloomVoidDash            = BASE_ID + HEIRLOOM_OFFSET + 4;
    public const long HeirloomCaveLantern         = BASE_ID + HEIRLOOM_OFFSET + 5;

    // ── Miniboss kill locations ──────────────────────────────────────────────

    public const long StudyMiniboss_SwordKnight_Defeated = BASE_ID + MINIBOSS_KILL_OFFSET + 0;
    public const long StudyMiniboss_SpearKnight_Defeated = BASE_ID + MINIBOSS_KILL_OFFSET + 1;
    public const long CaveMiniboss_White_Defeated        = BASE_ID + MINIBOSS_KILL_OFFSET + 2;
    public const long CaveMiniboss_Black_Defeated        = BASE_ID + MINIBOSS_KILL_OFFSET + 3;

    // ── Blueprint chest locations (biome-slot based) ─────────────────────────
    //
    // Checks are allocated from a per-biome pool rather than tied to specific
    // equipment pieces, so room-level gating doesn't leave any locations
    // permanently inaccessible. The number of slots per biome is configurable
    // via the player's YAML (0–16, default 11).
    //
    // Location ID = BASE_ID + BLUEPRINT_OFFSET + biomeIndex * 16 + slotIndex
    //   biomeIndex: Castle=0, Lake=1, Forest=2, Study=3, Tower=4, Cave=5
    //   slotIndex:  0 .. (_checksPerBiome - 1)
    //
    // The stride of 16 keeps IDs stable regardless of N so a seed generated
    // with N=11 and one with N=5 share the same IDs for their active slots.

    private static int _checksPerBiome = 11;

    /// <summary>
    /// Sets the number of blueprint chest checks per biome. Called once after
    /// a successful login with the value from slot data.
    /// </summary>
    public static void SetBlueprintChecksPerBiome(int n) =>
        _checksPerBiome = n < 0 ? 0 : n > 16 ? 16 : n;

    // ── Fairy chest (rune) locations (biome-slot based) ──────────────────────
    //
    // Same stride-16 pooling strategy as blueprints. Default 4 slots per biome
    // (24 total), configurable via "rune_checks_per_biome" in slot data.
    //
    // Location ID = BASE_ID + RUNE_OFFSET + biomeIndex * 16 + slotIndex

    private static int _runeChecksPerBiome = 4;

    /// <summary>
    /// Sets the number of fairy chest checks per biome. Called once after
    /// a successful login with the value from slot data.
    /// </summary>
    public static void SetRuneChecksPerBiome(int n) =>
        _runeChecksPerBiome = n < 0 ? 0 : n > 16 ? 16 : n;

    /// <summary>Human-readable name for each location ID, used in logs and UI.</summary>
    public static readonly IReadOnlyDictionary<long, string> Names = BuildNames();

    private static Dictionary<long, string> BuildNames()
    {
        Dictionary<long, string> d = new()
        {
            [CastleBossDefeated] = "Citadel Agartha - Estuary Lamech Defeated",
            [BridgeBossDefeated] = "Axis Mundi - Void Beasts Defeated",
            [ForestBossDefeated] = "Kerguelen Plateau - Estuary Naamah Defeated",
            [StudyBossDefeated]  = "Stygian Study - Estuary Enoch Defeated",
            [TowerBossDefeated]  = "Sun Tower - Estuary Irad Defeated",
            [CaveBossDefeated]   = "Pishon Dry Lake - Estuary Tubal Defeated",
            [GardenBossDefeated] = "Garden of Eden - Jonah Defeated",
            [FinalBossDefeated]  = "Castle Hamson - The Traitor Defeated",

            [StudyMiniboss_SwordKnight_Defeated] = "Stygian Study - Gongheads Miniboss Defeated",
            [StudyMiniboss_SpearKnight_Defeated] = "Stygian Study - Murmur Miniboss Defeated",
            [CaveMiniboss_White_Defeated]        = "Pishon Dry Lake - Briareus and Cottus Minibosses Defeated",
            [CaveMiniboss_Black_Defeated]        = "Pishon Dry Lake - Gyges and Aegaeon Minibosses Defeated",

            [HeirloomAirDash]             = "Citadel Agartha - Ananke's Shawl",
            [HeirloomDoubleJump]          = "Kerguelen Plateau - Aether's Wings",
            [HeirloomMemory]              = "Citadel Agartha - Aesop's Tome",
            [HeirloomBouncableDownstrike] = "Axis Mundi - Echo's Boots",
            [HeirloomVoidDash]            = "Stygian Study - Pallas' Void Bell",
            [HeirloomCaveLantern]         = "Pishon Dry Lake - Theia's Sun Lantern",
        };

        string[] biomeNames = [ "Citadel Agartha", "Axis Mundi", "Kerguelen Plateau", "Stygian Study", "Sun Tower", "Pishon Dry Lake" ];
        for (int biome = 0; biome < 6; biome++)
            for (int slot = 0; slot < 16; slot++)
                d[BASE_ID + BLUEPRINT_OFFSET + biome * 16 + slot] = $"{biomeNames[biome]} - Blueprint Chest {slot + 1}";

        for (int biome = 0; biome < 6; biome++)
            for (int slot = 0; slot < 16; slot++)
                d[BASE_ID + RUNE_OFFSET + biome * 16 + slot] = $"{biomeNames[biome]} - Fairy Chest {slot + 1}";

        return d;
    }

    // ── Lookup methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Maps the <see cref="PlayerSaveFlag"/> the game sets on a boss kill to the
    /// corresponding Archipelago location ID, or <c>null</c> if not tracked.
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

    /// <summary>
    /// Maps a <see cref="HeirloomType"/> to its Archipelago location ID, or <c>null</c> if not tracked.
    /// </summary>
    public static long? FromHeirloomType(HeirloomType type) => type switch
    {
        HeirloomType.UnlockAirDash             => HeirloomAirDash,
        HeirloomType.UnlockDoubleJump          => HeirloomDoubleJump,
        HeirloomType.UnlockMemory              => HeirloomMemory,
        HeirloomType.UnlockBouncableDownstrike => HeirloomBouncableDownstrike,
        HeirloomType.UnlockVoidDash            => HeirloomVoidDash,
        HeirloomType.CaveLantern               => HeirloomCaveLantern,
        _ => null,
    };

    /// <summary>
    /// Maps the <see cref="PlayerSaveFlag"/> set on a miniboss defeat to its
    /// Archipelago location ID, or <c>null</c> if not tracked.
    /// </summary>
    public static long? FromMinibossSaveFlag(PlayerSaveFlag flag) => flag switch
    {
        PlayerSaveFlag.StudyMiniboss_SwordKnight_Defeated => StudyMiniboss_SwordKnight_Defeated,
        PlayerSaveFlag.StudyMiniboss_SpearKnight_Defeated => StudyMiniboss_SpearKnight_Defeated,
        PlayerSaveFlag.CaveMiniboss_White_Defeated        => CaveMiniboss_White_Defeated,
        PlayerSaveFlag.CaveMiniboss_Black_Defeated        => CaveMiniboss_Black_Defeated,
        _ => null,
    };

    /// <summary>
    /// Maps a <see cref="BiomeType"/> to its biome-slot pool index (0–5),
    /// or <c>null</c> for non-relevant areas (hub, town, etc.).
    /// Sub-variants (CaveMiddle, ForestTop, etc.) resolve to their parent biome.
    /// </summary>
    public static int? GetBiomeIndex(BiomeType biome) => biome switch
    {
        BiomeType.Castle                                                    => 0, // Citadel Agartha
        BiomeType.Stone                                                     => 1, // Axis Mundi
        BiomeType.Forest or BiomeType.ForestTop or BiomeType.ForestBottom   => 2, // Kerguelen Plateau
        BiomeType.Study                                                     => 3, // Stygian Study
        BiomeType.Tower or BiomeType.TowerExterior                          => 4, // Sun Tower
        BiomeType.Cave or BiomeType.CaveMiddle or BiomeType.CaveBottom      => 5, // Pishon Dry Lake
        _                                                                   => null,
    };

    /// <summary>
    /// Returns the location ID of the next unchecked blueprint slot for the given
    /// biome index, or <c>null</c> if all slots in that biome have been used.
    /// </summary>
    public static long? NextBlueprintChestLocation(int biomeIndex, HashSet<long> checkedLocations)
    {
        for (int i = 0; i < _checksPerBiome; i++)
        {
            long id = BASE_ID + BLUEPRINT_OFFSET + biomeIndex * 16 + i;
            if (!checkedLocations.Contains(id))
                return id;
        }
        return null;
    }

    /// <summary>
    /// Returns the location ID of the next unchecked fairy chest slot for the given
    /// biome index, or <c>null</c> if all slots in that biome have been used.
    /// </summary>
    public static long? NextRuneChestLocation(int biomeIndex, HashSet<long> checkedLocations)
    {
        for (int i = 0; i < _runeChecksPerBiome; i++)
        {
            long id = BASE_ID + RUNE_OFFSET + biomeIndex * 16 + i;
            if (!checkedLocations.Contains(id))
                return id;
        }
        return null;
    }
}
