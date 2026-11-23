using System;
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
    public const long BASE_ID = 0x0BEEF000L;

    private const long BOSS_KILL_OFFSET     = 0x100;
    private const long MINIBOSS_KILL_OFFSET = 0x200;
    private const long HEIRLOOM_OFFSET      = 0x300;
    private const long MANOR_OFFSET         = 0x600;
    private const long TELEPORTER_OFFSET    = 0x700;

    // Shop-layout manor locations get their own block distinct from the tree layout: 0xA00-0xBFF.
    private const long MANOR_SHOP_OFFSET    = 0xA00;  // + position * MANOR_SHOP_ID_STRIDE + depthIndex

    private const long JOURNAL_GROUPED_OFFSET    = 0x800;  // BASE_ID + offset + biomeIndex (0-5)
    private const long MEMORY_GROUPED_OFFSET     = 0x808;  // BASE_ID + offset + biomeIndex (only 0, 2 active)
    private const long JOURNAL_INDIVIDUAL_OFFSET = 0x810;  // BASE_ID + offset + biomeIndex * JOURNAL_ID_STRIDE + journalIndex
    private const long MEMORY_INDIVIDUAL_OFFSET  = 0x870;  // BASE_ID + offset + biomeIndex * JOURNAL_ID_STRIDE + memoryIndex

    // Per-biome stride for the individual journal and memory pools, which both use it. Not strictly
    // needed since memories and journals numbers are constants based on the vanilla game. But it
    // helps when easily indexing the slots into biome + index numbers.
    private const long JOURNAL_ID_STRIDE = 16;

    // Mastery rank locations. A seed uses one mode or the other, never both, but they get
    // separate blocks so a (mode, rank) pair keeps the same ID either way.
    //
    // Total needs 750 consecutive IDs (the 15 classes at MAX_MASTERY_RANK_PER_CLASS each), so
    // 0xC00-0xFFF is reserved for it; per-class needs 960 (15 at MASTERY_CLASS_ID_STRIDE each)
    // and follows at 0x1000.
    private const long MASTERY_TOTAL_OFFSET = 0xC00;   // + (level - 1)
    private const long MASTERY_CLASS_OFFSET = 0x1000;  // + classPosition * MASTERY_CLASS_ID_STRIDE + (level - 1)

    // Blueprint and rune chest pools, 6 biomes at CHEST_SLOT_ID_STRIDE each. They start after the
    // per-class mastery block (which ends at 0x13BF).
    private const long BLUEPRINT_OFFSET = 0x1400;  // + biomeIndex * CHEST_SLOT_ID_STRIDE + slotIndex, to 0x157F
    private const long RUNE_OFFSET      = 0x1600;  // + biomeIndex * CHEST_SLOT_ID_STRIDE + slotIndex, to 0x177F

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

    /// <summary>
    /// The boss-defeat location ID of a biome, by its pool index. The first six boss offsets are
    /// in biome index order, so this is the same arithmetic the constants above spell out.
    /// </summary>
    public static long BiomeBossDefeated(int biomeIndex) =>
        BASE_ID + BOSS_KILL_OFFSET + biomeIndex;

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
    // via the player's YAML (0–50, default 11).
    //
    // Location ID = BASE_ID + BLUEPRINT_OFFSET + biomeIndex * CHEST_SLOT_ID_STRIDE + slotIndex
    //   biomeIndex: Castle=0, Lake=1, Forest=2, Study=3, Tower=4, Cave=5
    //   slotIndex:  0 .. (_checksPerBiome - 1)
    //
    // The stride keeps IDs stable regardless of N so a seed generated
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

    // ── Manor shop locations (position-depth based) ──────────────────────────
    //
    // The alternative manor layout: MANOR_SHOP_POSITIONS independent columns, each
    // _manorShopDepth purchases deep, with only the next unpurchased entry of a column
    // ever on offer. Which entry that is derives entirely from the checked-location set,
    // so no column progress is persisted separately.
    //
    // Location ID = BASE_ID + MANOR_SHOP_OFFSET + position * MANOR_SHOP_ID_STRIDE + depthIndex
    //
    // The stride keeps IDs stable regardless of the configured depth, the same way
    // CHEST_SLOT_ID_STRIDE does for the blueprint and rune pools.

    /// <summary>Number of shop positions. Mirrors MANOR_SHOP_POSITIONS in the apworld.</summary>
    public const int MANOR_SHOP_POSITIONS = 4;

    /// <summary>Upper bound of the apworld's ManorShopDepth option.</summary>
    public const int MAX_MANOR_SHOP_DEPTH = 100;

    /// <summary>Per-position stride for shop location IDs: <see cref="MAX_MANOR_SHOP_DEPTH"/>
    /// rounded up to a power of two. It must never be smaller than that bound, or one
    /// position's deeper purchases would collide with the next position's shallow ones.
    /// Mirrors MANOR_SHOP_ID_STRIDE in the apworld.</summary>
    public const int MANOR_SHOP_ID_STRIDE = 128;

    private static int _manorShopDepth = 25;

    /// <summary>Configured depth of each shop column. Valid positions are
    /// 0..<see cref="MANOR_SHOP_POSITIONS"/>-1 and depths 0..this-1.</summary>
    public static int ManorShopDepth => _manorShopDepth;

    /// <summary>
    /// Sets the depth of each shop column. Called once after a successful login with the
    /// value from slot data.
    /// </summary>
    public static void SetManorShopDepth(int n) =>
        _manorShopDepth = n < 1 ? 1 : n > MAX_MANOR_SHOP_DEPTH ? MAX_MANOR_SHOP_DEPTH : n;

    /// <summary>The location ID of a specific purchase, by zero-based position and depth.</summary>
    public static long ManorShopLocation(int position, int depthIndex) =>
        BASE_ID + MANOR_SHOP_OFFSET + position * MANOR_SHOP_ID_STRIDE + depthIndex;

    /// <summary>
    /// Splits a shop location ID back into its position and zero-based depth, or
    /// <c>null</c> if the ID is not in the shop block or is out of range for it.
    /// </summary>
    public static (int Position, int DepthIndex)? ParseManorShopLocation(long id)
    {
        long offset = id - BASE_ID - MANOR_SHOP_OFFSET;
        if (offset < 0) return null;
        int position = (int)(offset / MANOR_SHOP_ID_STRIDE);
        int depthIndex = (int)(offset % MANOR_SHOP_ID_STRIDE);
        if (position >= MANOR_SHOP_POSITIONS || depthIndex >= MAX_MANOR_SHOP_DEPTH) return null;
        return (position, depthIndex);
    }

    /// <summary>
    /// The location ID currently on offer at the given shop position - the first purchase
    /// in that column not yet checked - or <c>null</c> once the column is exhausted.
    /// </summary>
    public static long? NextManorShopLocation(int position, HashSet<long> checkedLocations)
    {
        for (int depth = 0; depth < _manorShopDepth; depth++)
        {
            long id = ManorShopLocation(position, depth);
            if (!checkedLocations.Contains(id))
                return id;
        }
        return null;
    }

    /// <summary>How many purchases of the given shop position have been made.</summary>
    public static int ManorShopPurchasedCount(int position, HashSet<long> checkedLocations)
    {
        int count = 0;
        for (int depth = 0; depth < _manorShopDepth; depth++)
            if (checkedLocations.Contains(ManorShopLocation(position, depth)))
                count++;
        return count;
    }

    /// <summary>Total purchases made across every shop position.</summary>
    public static int ManorShopTotalPurchased(HashSet<long> checkedLocations)
    {
        int count = 0;
        for (int position = 0; position < MANOR_SHOP_POSITIONS; position++)
            count += ManorShopPurchasedCount(position, checkedLocations);
        return count;
    }

    /// <summary>
    /// The ceiling on both chest pools' per-biome slot counts, and so on the
    /// <c>blueprint_checks_per_biome</c> and <c>rune_checks_per_biome</c> options. Mirrors
    /// MAX_CHEST_CHECKS_PER_BIOME in the apworld.
    /// </summary>
    public const int MAX_CHEST_CHECKS_PER_BIOME = 50;

    /// <summary>
    /// Per-biome stride for blueprint and rune location IDs. It keeps a biome's IDs stable at any
    /// configured slot count, and it must never fall below <see cref="MAX_CHEST_CHECKS_PER_BIOME"/>
    /// or one biome's deep slots would collide with the next biome's shallow ones. Mirrors
    /// CHEST_SLOT_ID_STRIDE in the apworld.
    /// </summary>
    public const int CHEST_SLOT_ID_STRIDE = 64;

    private static int _checksPerBiome = 11;

    /// <summary>
    /// Sets the number of blueprint chest checks per biome. Called once after
    /// a successful login with the value from slot data.
    /// </summary>
    public static void SetBlueprintChecksPerBiome(int n) =>
        _checksPerBiome = n < 0 ? 0 : n > MAX_CHEST_CHECKS_PER_BIOME ? MAX_CHEST_CHECKS_PER_BIOME : n;

    /// <summary>How many blueprint chest slots each biome has in this seed.</summary>
    public static int BlueprintChecksPerBiome => _checksPerBiome;

    /// <summary>The location ID of a blueprint chest slot, by biome index and zero-based slot.</summary>
    public static long BlueprintChestLocation(int biomeIndex, int slotIndex) =>
        BASE_ID + BLUEPRINT_OFFSET + biomeIndex * CHEST_SLOT_ID_STRIDE + slotIndex;

    // ── Fairy chest (rune) locations (biome-slot based) ──────────────────────
    //
    // Same pooling strategy and stride as blueprints. Default 4 slots per biome
    // (24 total), configurable via "rune_checks_per_biome" in slot data.
    //
    // Location ID = BASE_ID + RUNE_OFFSET + biomeIndex * CHEST_SLOT_ID_STRIDE + slotIndex

    private static int _runeChecksPerBiome = 4;

    /// <summary>
    /// Sets the number of fairy chest checks per biome. Called once after
    /// a successful login with the value from slot data.
    /// </summary>
    public static void SetRuneChecksPerBiome(int n) =>
        _runeChecksPerBiome = n < 0 ? 0 : n > MAX_CHEST_CHECKS_PER_BIOME ? MAX_CHEST_CHECKS_PER_BIOME : n;

    /// <summary>How many fairy chest slots each biome has in this seed.</summary>
    public static int RuneChecksPerBiome => _runeChecksPerBiome;

    /// <summary>The location ID of a fairy chest slot, by biome index and zero-based slot.</summary>
    public static long RuneChestLocation(int biomeIndex, int slotIndex) =>
        BASE_ID + RUNE_OFFSET + biomeIndex * CHEST_SLOT_ID_STRIDE + slotIndex;

    // ── Mastery rank locations ──────────────────────────────────────────────
    //
    // Mastery ranks are derived from the save's per-class XP, so there is nothing to track
    // here beyond how many ranks this seed turned into checks. The mod reports every rank up
    // to the player's current one on each commit, which is idempotent, so a rank gained while
    // the socket was down resyncs on reconnect like any other check.

    /// <summary>
    /// The game's ceiling on a single class's mastery rank, and so the ceiling on
    /// <see cref="MasteryLevelsPerClass"/>. Mirrors MAX_MASTERY_RANK_PER_CLASS in the apworld.
    /// </summary>
    public const int MAX_MASTERY_RANK_PER_CLASS = 50;

    /// <summary>
    /// Per-class stride for mastery location IDs. Like <see cref="CHEST_SLOT_ID_STRIDE"/> on the
    /// blueprint and rune pools, it keeps a class's IDs stable at any configured level count, and it
    /// must never fall below <see cref="MAX_MASTERY_RANK_PER_CLASS"/> or one class's deep ranks
    /// would collide with the next class's shallow ones. Mirrors MASTERY_CLASS_ID_STRIDE
    /// in the apworld.
    /// </summary>
    public const int MASTERY_CLASS_ID_STRIDE = 64;

    /// <summary>Every playable class at the cap: the ceiling on <see cref="MasteryTotalLevels"/>.</summary>
    public static readonly int MAX_TOTAL_MASTERY_RANK =
        MAX_MASTERY_RANK_PER_CLASS * GameConstants.MasteryClassTypes.Length;

    private static int _masteryTotalLevels    = 50;
    private static int _masteryLevelsPerClass = 10;

    /// <summary>How many overall mastery levels are checks. Used by the Total mode only.</summary>
    public static int MasteryTotalLevels => _masteryTotalLevels;

    /// <summary>How many ranks of each class are checks. Used by the PerClass mode only.</summary>
    public static int MasteryLevelsPerClass => _masteryLevelsPerClass;

    /// <summary>
    /// Sets both mastery level counts. Called once after a successful login with the values
    /// from slot data; the unused mode's count is harmless.
    /// </summary>
    public static void SetMasteryLevelCounts(int totalLevels, int perClassLevels)
    {
        _masteryTotalLevels = totalLevels < 1 ? 1
            : totalLevels > MAX_TOTAL_MASTERY_RANK ? MAX_TOTAL_MASTERY_RANK : totalLevels;
        _masteryLevelsPerClass = perClassLevels < 1 ? 1
            : perClassLevels > MAX_MASTERY_RANK_PER_CLASS ? MAX_MASTERY_RANK_PER_CLASS : perClassLevels;
    }

    /// <summary>The location ID for the given one-based overall mastery level.</summary>
    public static long TotalMasteryLocation(int level) =>
        BASE_ID + MASTERY_TOTAL_OFFSET + level - 1;

    /// <summary>
    /// The location ID for rank <paramref name="level"/> (one-based) of the class at
    /// <paramref name="classPosition"/> in <see cref="GameConstants.MasteryClassTypes"/>.
    /// </summary>
    public static long ClassMasteryLocation(int classPosition, int level) =>
        BASE_ID + MASTERY_CLASS_OFFSET + classPosition * MASTERY_CLASS_ID_STRIDE + level - 1;

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
            if (GroupedJournalLocation(biome) is long journalGroup)
                d[journalGroup] = $"{GameConstants.BiomeNames[biome]} - All Journals Read";
            if (GroupedMemoryLocation(biome) is long memoryGroup)
                d[memoryGroup] = $"{GameConstants.BiomeNames[biome]} - All Memories Read";
            for (int j = 0; j < s_journalCounts[biome]; j++)
                d[IndividualJournalLocation(biome, j)] = $"{GameConstants.BiomeNames[biome]} - Journal Entry {j + 1}";
            for (int m = 0; m < s_memoryCounts[biome]; m++)
                d[IndividualMemoryLocation(biome, m)] = $"{GameConstants.BiomeNames[biome]} - Memory Fragment {m + 1}";
        }

        // Registered at the maximum slot count so a name is available whatever the seed configured.
        for (int biome = 0; biome < 6; biome++)
            for (int slot = 0; slot < MAX_CHEST_CHECKS_PER_BIOME; slot++)
                d[BlueprintChestLocation(biome, slot)] = $"{GameConstants.BiomeNames[biome]} - Blueprint Chest {slot + 1}";

        for (int biome = 0; biome < 6; biome++)
            for (int slot = 0; slot < MAX_CHEST_CHECKS_PER_BIOME; slot++)
                d[RuneChestLocation(biome, slot)] = $"{GameConstants.BiomeNames[biome]} - Fairy Chest {slot + 1}";

        // Manor upgrade location names
        for (int i = 0; i < GameConstants.ManorLocationNames.Length; i++)
            d[BASE_ID + MANOR_OFFSET + i] = GameConstants.ManorLocationNames[i];

        // Shop-layout manor location names, registered at the maximum depth so a name is
        // available whatever manor_shop_depth the seed used.
        for (int position = 0; position < MANOR_SHOP_POSITIONS; position++)
            for (int depth = 0; depth < MAX_MANOR_SHOP_DEPTH; depth++)
                d[ManorShopLocation(position, depth)] = $"Manor - Slot {position + 1} Purchase {depth + 1}";

        // Mastery rank names for both modes at their maximum level counts.
        // All names should be available regardless of what the seed configured.
        for (int level = 1; level <= MAX_TOTAL_MASTERY_RANK; level++)
            d[TotalMasteryLocation(level)] = $"Mastery - Total Rank {level}";

        for (int position = 0; position < GameConstants.MasteryClassNames.Length; position++)
            for (int level = 1; level <= MAX_MASTERY_RANK_PER_CLASS; level++)
                d[ClassMasteryLocation(position, level)] =
                    $"Mastery - {GameConstants.MasteryClassNames[position]} Rank {level}";

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
    /// Returns the location ID of the next unconsumed blueprint slot for the given
    /// biome index, or <c>null</c> if all slots in that biome have been used.
    /// </summary>
    public static long? NextBlueprintChestLocation(int biomeIndex, APRunState runState)
    {
        for (int i = 0; i < _checksPerBiome; i++)
        {
            long id = BlueprintChestLocation(biomeIndex, i);
            if (!runState.IsPooledSlotConsumed(id))
                return id;
        }
        return null;
    }

    /// <summary>
    /// Returns the location ID of the next unconsumed fairy chest slot for the given
    /// biome index, or <c>null</c> if all slots in that biome have been used.
    /// </summary>
    public static long? NextRuneChestLocation(int biomeIndex, APRunState runState)
    {
        for (int i = 0; i < _runeChecksPerBiome; i++)
        {
            long id = RuneChestLocation(biomeIndex, i);
            if (!runState.IsPooledSlotConsumed(id))
                return id;
        }
        return null;
    }

    // Every pool is laid out as 6 biomes at a fixed stride regardless of how many slots per biome
    // the seed actually uses, so a whole pool is one contiguous block from its offset.
    private const long CHEST_POOL_SPAN   = 6 * CHEST_SLOT_ID_STRIDE;
    private const long JOURNAL_POOL_SPAN = 6 * JOURNAL_ID_STRIDE;

    /// <summary>
    /// True for an ID in one of the pooled location categories: the blueprint and rune chest pools,
    /// and the individual journal and memory pools. What these have in common is that the slot a
    /// pickup consumes is chosen at pickup time rather than being bound to a fixed in-world event,
    /// which makes them the only categories where a check the server already has must be skipped
    /// over rather than simply ignored. See <see cref="APRunState.RemoteCheckedLocations"/>.
    ///
    /// <para>The <em>grouped</em> journal and memory locations are deliberately excluded: there is
    /// one per biome, earned by reading everything in it, so there is no pool to skip within.</para>
    /// </summary>
    public static bool IsPooledLocation(long id) =>
        InPool(id, BLUEPRINT_OFFSET, CHEST_POOL_SPAN)
        || InPool(id, RUNE_OFFSET, CHEST_POOL_SPAN)
        || InPool(id, JOURNAL_INDIVIDUAL_OFFSET, JOURNAL_POOL_SPAN)
        || InPool(id, MEMORY_INDIVIDUAL_OFFSET, JOURNAL_POOL_SPAN);

    private static bool InPool(long id, long poolOffset, long poolSpan)
    {
        long index = id - BASE_ID - poolOffset;
        return index >= 0 && index < poolSpan;
    }

    /// <summary>
    /// The <see cref="JournalCategoryType"/> of each biome pool index, in biome index order. Both
    /// directions of the mapping are derived from this one table so they cannot drift.
    /// </summary>
    private static readonly JournalCategoryType[] s_journalCategories =
    {
        JournalCategoryType.Castle, JournalCategoryType.Bridge, JournalCategoryType.Forest,
        JournalCategoryType.Study,  JournalCategoryType.Tower,  JournalCategoryType.Cave,
    };

    /// <summary>
    /// Maps a <see cref="JournalCategoryType"/> to the biome pool index (0–5) used for
    /// journal/memory location IDs, or <c>null</c> for untracked categories.
    /// </summary>
    private static int? GetJournalBiomeIndex(JournalCategoryType cat)
    {
        int index = Array.IndexOf(s_journalCategories, cat);
        return index < 0 ? null : index;
    }

    /// <summary>
    /// The <see cref="JournalCategoryType"/> for a biome pool index, for reading the save's own
    /// journal counts (<c>PlayerSaveData.GetJournalsRead</c>). The inverse of
    /// <see cref="GetJournalBiomeIndex"/>.
    /// </summary>
    public static JournalCategoryType ToJournalCategory(int biomeIndex) =>
        s_journalCategories[biomeIndex];

    /// <summary>How many journal entries the given biome has.</summary>
    public static int JournalCount(int biomeIndex) => s_journalCounts[biomeIndex];

    /// <summary>How many memory fragments the given biome has. Zero for four of the six.</summary>
    public static int MemoryCount(int biomeIndex) => s_memoryCounts[biomeIndex];

    /// <summary>
    /// In Grouped mode, the location ID for completing all journals in the given biome, or
    /// <c>null</c> for a biome with no journals.
    /// </summary>
    public static long? GroupedJournalLocation(int biomeIndex) =>
        s_journalCounts[biomeIndex] == 0 ? null : BASE_ID + JOURNAL_GROUPED_OFFSET + biomeIndex;

    /// <summary>
    /// In Grouped mode, the location ID for completing all memories in the given biome, or
    /// <c>null</c> for a biome with no memories.
    /// </summary>
    public static long? GroupedMemoryLocation(int biomeIndex) =>
        s_memoryCounts[biomeIndex] == 0 ? null : BASE_ID + MEMORY_GROUPED_OFFSET + biomeIndex;

    /// <summary>The location ID of an individual journal slot, by biome index and zero-based index.</summary>
    public static long IndividualJournalLocation(int biomeIndex, int journalIndex) =>
        BASE_ID + JOURNAL_INDIVIDUAL_OFFSET + biomeIndex * JOURNAL_ID_STRIDE + journalIndex;

    /// <summary>The location ID of an individual memory slot, by biome index and zero-based index.</summary>
    public static long IndividualMemoryLocation(int biomeIndex, int memoryIndex) =>
        BASE_ID + MEMORY_INDIVIDUAL_OFFSET + biomeIndex * JOURNAL_ID_STRIDE + memoryIndex;

    /// <summary>
    /// In Grouped mode, returns the location ID for completing all journals in the given
    /// biome, or <c>null</c> for an untracked category or a biome with no journals.
    /// </summary>
    public static long? GetGroupedJournalLocation(JournalCategoryType cat)
    {
        var bi = GetJournalBiomeIndex(cat);
        return bi is null ? null : GroupedJournalLocation(bi.Value);
    }

    /// <summary>
    /// In Grouped mode, returns the location ID for completing all memories in the given
    /// biome, or <c>null</c> for an untracked category or a biome with no memories.
    /// </summary>
    public static long? GetGroupedMemoryLocation(JournalCategoryType cat)
    {
        var bi = GetJournalBiomeIndex(cat);
        return bi is null ? null : GroupedMemoryLocation(bi.Value);
    }

    /// <summary>
    /// In Individual mode, returns the location ID of the next unconsumed journal slot in the given
    /// biome, or <c>null</c> for an untracked category or once every slot in it has been used.
    ///
    /// <para>A read consumes the lowest slot that is still live rather than the one matching the
    /// save's read count, so slots the server already has checked are stepped over instead of
    /// spending a read on an item that has already gone out (due to a slot being completed or /collect
    /// command). With nothing checked remotely the two agree: the Nth read takes slot N.</para>
    /// </summary>
    public static long? NextIndividualJournalLocation(JournalCategoryType cat, APRunState runState)
    {
        var bi = GetJournalBiomeIndex(cat);
        if (bi is null) return null;
        return NextJournalPoolSlot(
            JOURNAL_INDIVIDUAL_OFFSET, bi.Value, s_journalCounts[bi.Value], runState);
    }

    /// <summary>
    /// In Individual mode, returns the location ID of the next unconsumed memory slot in the given
    /// biome, or <c>null</c> for an untracked category or once every slot in it has been used.
    /// Behaves exactly like <see cref="NextIndividualJournalLocation"/>.
    /// </summary>
    public static long? NextIndividualMemoryLocation(JournalCategoryType cat, APRunState runState)
    {
        var bi = GetJournalBiomeIndex(cat);
        if (bi is null) return null;
        return NextJournalPoolSlot(
            MEMORY_INDIVIDUAL_OFFSET, bi.Value, s_memoryCounts[bi.Value], runState);
    }

    // Shared by the journal and memory pools, which have the same JOURNAL_ID_STRIDE layout. Not
    // usable for the chest pools, those stride by CHEST_SLOT_ID_STRIDE.
    private static long? NextJournalPoolSlot(
        long poolOffset, int biomeIndex, int slotCount, APRunState runState)
    {
        for (int i = 0; i < slotCount; i++)
        {
            long id = BASE_ID + poolOffset + biomeIndex * JOURNAL_ID_STRIDE + i;
            if (!runState.IsPooledSlotConsumed(id))
                return id;
        }
        return null;
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
