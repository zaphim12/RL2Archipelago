using System.Collections.Generic;
using RL2Archipelago.Items;

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
    private const long MANOR_OFFSET         = 0x600;
    private const long TELEPORTER_OFFSET    = 0x700;

    private const long JOURNAL_GROUPED_OFFSET    = 0x800;  // BASE_ID + offset + biomeIndex (0-5)
    private const long MEMORY_GROUPED_OFFSET     = 0x808;  // BASE_ID + offset + biomeIndex (only 0, 2 active)
    private const long JOURNAL_INDIVIDUAL_OFFSET = 0x810;  // BASE_ID + offset + biomeIndex * 16 + journalIndex
    private const long MEMORY_INDIVIDUAL_OFFSET  = 0x870;  // BASE_ID + offset + biomeIndex * 16 + memoryIndex

    // Journals and memories per biome, indexed Castle=0, Bridge=1, Forest=2, Study=3, Tower=4, Cave=5.
    // Verified from Journal_EV.GetNumJournals() via Assembly-CSharp decompile.
    private static readonly int[] s_journalCounts = { 4, 4, 4, 6, 7, 7 };
    private static readonly int[] s_memoryCounts  = { 4, 0, 5, 0, 0, 0 };

    // ── Boss kill locations ──────────────────────────────────────────────────

    public const long CastleBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 0;
    public const long BridgeBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 1;
    public const long ForestBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 2;
    public const long StudyBossDefeated  = BASE_ID + BOSS_KILL_OFFSET + 3;
    public const long TowerBossDefeated  = BASE_ID + BOSS_KILL_OFFSET + 4;
    public const long CaveBossDefeated   = BASE_ID + BOSS_KILL_OFFSET + 5;
    public const long GardenBossDefeated = BASE_ID + BOSS_KILL_OFFSET + 6;
    // Offset + 7 (FinalBoss/Cain) is intentionally absent: defeating Cain triggers
    // the goal status update (SetGoalAchieved), not a location check.

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

    // ── Teleporter unlock locations ──────────────────────────────────────────
    //
    // One location per biome where the pizza girl sells a teleporter unlock.
    // The starting biome (Castle/Citadel Agartha) has no pizza girl, so only
    // the 5 non-starting biomes are tracked.
    // Location ID = BASE_ID + TELEPORTER_OFFSET + biomeIndex
    //   0 = Axis Mundi (Stone), 1 = Kerguelen Plateau (Forest),
    //   2 = Stygian Study (Study), 3 = Sun Tower (Tower), 4 = Pishon Dry Lake (Cave)

    public const long TeleporterAxisMundi        = BASE_ID + TELEPORTER_OFFSET + 0;
    public const long TeleporterKerguelenPlateau = BASE_ID + TELEPORTER_OFFSET + 1;
    public const long TeleporterStygianStudy     = BASE_ID + TELEPORTER_OFFSET + 2;
    public const long TeleporterSunTower         = BASE_ID + TELEPORTER_OFFSET + 3;
    public const long TeleporterPishonDryLake    = BASE_ID + TELEPORTER_OFFSET + 4;

    // ── Manor upgrade locations ──────────────────────────────────────────────
    //
    // One location per SkillTreeType slot, using the same canonical ordered list
    // as ItemRegistry.s_skillTreeTypes. Index in that array = ID offset from MANOR_OFFSET.

    // Built once at startup: SkillTreeType → location ID.
    private static readonly Dictionary<SkillTreeType, long> s_skillTreeToLocation
        = BuildSkillTreeToLocation();

    private static Dictionary<SkillTreeType, long> BuildSkillTreeToLocation()
    {
        var d = new Dictionary<SkillTreeType, long>(ItemRegistry.s_skillTreeTypes.Length);
        for (int i = 0; i < ItemRegistry.s_skillTreeTypes.Length; i++)
            d[ItemRegistry.s_skillTreeTypes[i]] = BASE_ID + MANOR_OFFSET + i;
        return d;
    }

    /// <summary>
    /// Maps a <see cref="SkillTreeType"/> to its Archipelago location ID, or
    /// <c>null</c> if the type isn't a tracked manor upgrade slot.
    /// </summary>
    public static long? FromSkillTreeType(SkillTreeType type)
    {
        if (!s_skillTreeToLocation.TryGetValue(type, out var id)) return null;
        return id;
    }

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
            [CastleBossDefeated] = "Estuary Lamech Defeated",
            [BridgeBossDefeated] = "Byarrrith and Halpharr Defeated",
            [ForestBossDefeated] = "Estuary Naamah Defeated",
            [StudyBossDefeated]  = "Estuary Enoch Defeated",
            [TowerBossDefeated]  = "Estuary Irad Defeated",
            [CaveBossDefeated]   = "Estuary Tubal Defeated",
            [GardenBossDefeated] = "Jonah Defeated",

            [StudyMiniboss_SpearKnight_Defeated] = "Gongheads Miniboss Defeated",
            [StudyMiniboss_SwordKnight_Defeated] = "Murmur Miniboss Defeated",
            [CaveMiniboss_White_Defeated]        = "Briareus and Cottus Minibosses Defeated",
            [CaveMiniboss_Black_Defeated]        = "Gyges and Aegaeon Minibosses Defeated",

            [HeirloomAirDash]             = "Ananke's Shawl Statue",
            [HeirloomDoubleJump]          = "Aether's Wings Statue",
            [HeirloomMemory]              = "Aesop's Tome Statue",
            [HeirloomBouncableDownstrike] = "Echo's Boots Statue",
            [HeirloomVoidDash]            = "Pallas' Void Bell Statue",
            [HeirloomCaveLantern]         = "Theia's Sun Lantern Conversation",
        };

        d[TeleporterAxisMundi]        = "Axis Mundi Teleporter Purchase";
        d[TeleporterKerguelenPlateau] = "Kerguelen Plateau Teleporter Purchase";
        d[TeleporterStygianStudy]     = "Stygian Study Teleporter Purchase";
        d[TeleporterSunTower]         = "Sun Tower Teleporter Purchase";
        d[TeleporterPishonDryLake]    = "Pishon Dry Lake Teleporter Purchase";

        for (int biome = 0; biome < GameConstants.BiomeNames.Length; biome++)
        {
            if (s_journalCounts[biome] > 0)
                d[BASE_ID + JOURNAL_GROUPED_OFFSET + biome] = $"{GameConstants.BiomeNames[biome]} - All Journals Read";
            if (s_memoryCounts[biome] > 0)
                d[BASE_ID + MEMORY_GROUPED_OFFSET + biome] = $"{GameConstants.BiomeNames[biome]} - All Memories Read";
            for (int j = 0; j < s_journalCounts[biome]; j++)
                d[BASE_ID + JOURNAL_INDIVIDUAL_OFFSET + biome * 16 + j] = $"{GameConstants.BiomeNames[biome]} - Journal Entry {j + 1}";
            for (int m = 0; m < s_memoryCounts[biome]; m++)
                d[BASE_ID + MEMORY_INDIVIDUAL_OFFSET + biome * 16 + m] = $"{GameConstants.BiomeNames[biome]} - Memory Fragment {m + 1}";
        }

        for (int biome = 0; biome < 6; biome++)
            for (int slot = 0; slot < 16; slot++)
                d[BASE_ID + BLUEPRINT_OFFSET + biome * 16 + slot] = $"{GameConstants.BiomeNames[biome]} - Blueprint Chest {slot + 1}";

        for (int biome = 0; biome < 6; biome++)
            for (int slot = 0; slot < 16; slot++)
                d[BASE_ID + RUNE_OFFSET + biome * 16 + slot] = $"{GameConstants.BiomeNames[biome]} - Fairy Chest {slot + 1}";

        // Manor upgrade location names
        for (int i = 0; i < GameConstants.ManorLocationNames.Length; i++)
            d[BASE_ID + MANOR_OFFSET + i] = GameConstants.ManorLocationNames[i];

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

    /// <summary>
    /// Maps a <see cref="JournalCategoryType"/> to the biome pool index (0–5) used for
    /// journal/memory location IDs, or <c>null</c> for untracked categories.
    /// </summary>
    private static int? GetJournalBiomeIndex(JournalCategoryType cat) => cat switch
    {
        JournalCategoryType.Castle => 0,
        JournalCategoryType.Bridge => 1,
        JournalCategoryType.Forest => 2,
        JournalCategoryType.Study  => 3,
        JournalCategoryType.Tower  => 4,
        JournalCategoryType.Cave   => 5,
        _                          => null,
    };

    /// <summary>
    /// In Grouped mode, returns the location ID for completing all journals in the given
    /// biome, or <c>null</c> for an untracked category or a biome with no journals.
    /// </summary>
    public static long? GetGroupedJournalLocation(JournalCategoryType cat)
    {
        var bi = GetJournalBiomeIndex(cat);
        if (bi is null || s_journalCounts[bi.Value] == 0) return null;
        return BASE_ID + JOURNAL_GROUPED_OFFSET + bi.Value;
    }

    /// <summary>
    /// In Grouped mode, returns the location ID for completing all memories in the given
    /// biome, or <c>null</c> for an untracked category or a biome with no memories.
    /// </summary>
    public static long? GetGroupedMemoryLocation(JournalCategoryType cat)
    {
        var bi = GetJournalBiomeIndex(cat);
        if (bi is null || s_memoryCounts[bi.Value] == 0) return null;
        return BASE_ID + MEMORY_GROUPED_OFFSET + bi.Value;
    }

    /// <summary>
    /// In Individual mode, returns the location ID for a specific journal entry (zero-based
    /// <paramref name="index"/>), or <c>null</c> for an untracked category or out-of-range index.
    /// </summary>
    public static long? GetIndividualJournalLocation(JournalCategoryType cat, int index)
    {
        var bi = GetJournalBiomeIndex(cat);
        if (bi is null || index < 0 || index >= s_journalCounts[bi.Value]) return null;
        return BASE_ID + JOURNAL_INDIVIDUAL_OFFSET + bi.Value * 16 + index;
    }

    /// <summary>
    /// In Individual mode, returns the location ID for a specific memory fragment (zero-based
    /// <paramref name="index"/>), or <c>null</c> for an untracked category or out-of-range index.
    /// </summary>
    public static long? GetIndividualMemoryLocation(JournalCategoryType cat, int index)
    {
        var bi = GetJournalBiomeIndex(cat);
        if (bi is null || index < 0 || index >= s_memoryCounts[bi.Value]) return null;
        return BASE_ID + MEMORY_INDIVIDUAL_OFFSET + bi.Value * 16 + index;
    }

    /// <summary>
    /// Maps a <see cref="BiomeType"/> to its pizza girl teleporter Archipelago location ID,
    /// or <c>null</c> if the biome has no tracked teleporter (e.g. Castle/starting biome).
    /// </summary>
    public static long? FromBiomeTypeTeleporter(BiomeType biome) => biome switch
    {
        BiomeType.Stone                                                   => TeleporterAxisMundi,
        BiomeType.Forest or BiomeType.ForestTop or BiomeType.ForestBottom => TeleporterKerguelenPlateau,
        BiomeType.Study                                                   => TeleporterStygianStudy,
        BiomeType.Tower or BiomeType.TowerExterior                        => TeleporterSunTower,
        BiomeType.Cave or BiomeType.CaveMiddle or BiomeType.CaveBottom    => TeleporterPishonDryLake,
        _                                                                 => null,
    };
}
