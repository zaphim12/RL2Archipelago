using System.Collections.Generic;
using System.Linq;

namespace RL2Archipelago.Items;

/// <summary>
/// Central registry of Archipelago item IDs and their display names.
///
/// IDs MUST stay in lockstep with the Python .apworld
/// (<c>apworld/rogue_legacy_2/items.py</c>). Items and locations occupy
/// separate ID namespaces in Archipelago, so the same numeric offset can
/// safely appear in both registries.
/// </summary>
public static class ItemRegistry
{
    public const long BASE_ID = 0xBEEF0000L;

    private const long HEIRLOOM_OFFSET   = 0x300;
    private const long BLUEPRINT_OFFSET  = 0x400;
    private const long RUNE_OFFSET       = 0x500;
    private const long MANOR_OFFSET      = 0x600;
    private const long TELEPORTER_OFFSET = 0x700;
    private const long FILLER_OFFSET     = 0x800;
    private const long TRAP_OFFSET       = 0x900;

    // ── Manor upgrade items ──────────────────────────────────────────────────
    //
    // Canonical ordered list mapping list index → SkillTreeType.
    // MUST stay in lockstep with _CORE_MANOR_UPGRADES + _NPC_MANOR_UPGRADES in items.py.
    // Indices 0-68 are core upgrades; 69-71 are NPC unlock slots.
    /// <summary>Ordered list mapping slot index → <see cref="SkillTreeType"/>, in lockstep with the Python apworld.</summary>
    public static IReadOnlyList<SkillTreeType> SkillTreeTypes => s_skillTreeTypes;

    internal static readonly SkillTreeType[] s_skillTreeTypes =
        GameConstants.ManorSlots.Select(s => s.SkillTree).ToArray();

    // ── Trap items ───────────────────────────────────────────────────────────

    public const long TrapCannonballRain  = BASE_ID + TRAP_OFFSET + 0;
    public const long TrapDragonLancers   = BASE_ID + TRAP_OFFSET + 1;
    public const long TrapAutomatonSwarm  = BASE_ID + TRAP_OFFSET + 2;
    public const long TrapGiantSnowflakes = BASE_ID + TRAP_OFFSET + 3;
    public const long TrapVoidWaves       = BASE_ID + TRAP_OFFSET + 4;

    // ── Filler items ─────────────────────────────────────────────────────────

    public const long GoldCoins = BASE_ID + FILLER_OFFSET + 0;

    // ── Teleporter unlock items ──────────────────────────────────────────────

    public const long TeleporterAxisMundi        = BASE_ID + TELEPORTER_OFFSET + 0;
    public const long TeleporterKerguelenPlateau = BASE_ID + TELEPORTER_OFFSET + 1;
    public const long TeleporterStygianStudy     = BASE_ID + TELEPORTER_OFFSET + 2;
    public const long TeleporterSunTower         = BASE_ID + TELEPORTER_OFFSET + 3;
    public const long TeleporterPishonDryLake    = BASE_ID + TELEPORTER_OFFSET + 4;

    // ── Heirloom items ───────────────────────────────────────────────────────

    public const long HeirloomAirDash             = BASE_ID + HEIRLOOM_OFFSET + 0;
    public const long HeirloomDoubleJump          = BASE_ID + HEIRLOOM_OFFSET + 1;
    public const long HeirloomMemory              = BASE_ID + HEIRLOOM_OFFSET + 2;
    public const long HeirloomBouncableDownstrike = BASE_ID + HEIRLOOM_OFFSET + 3;
    public const long HeirloomVoidDash            = BASE_ID + HEIRLOOM_OFFSET + 4;
    public const long HeirloomCaveLantern         = BASE_ID + HEIRLOOM_OFFSET + 5;

    /// <summary>Human-readable name for each item ID, used in logs and UI.</summary>
    public static readonly IReadOnlyDictionary<long, string> Names = BuildNames();

    private static Dictionary<long, string> BuildNames()
    {
        Dictionary<long, string> d = new()
        {
            [GoldCoins] = "Gold Coins",

            [TrapCannonballRain]  = "Trap: Cannonball Rain",
            [TrapDragonLancers]   = "Trap: Dragon Lancers",
            [TrapAutomatonSwarm]  = "Trap: Automaton Swarm",
            [TrapGiantSnowflakes] = "Trap: Giant Snowflakes",
            [TrapVoidWaves]       = "Trap: Void Waves",

            [TeleporterAxisMundi]        = "Axis Mundi Teleporter",
            [TeleporterKerguelenPlateau] = "Kerguelen Plateau Teleporter",
            [TeleporterStygianStudy]     = "Stygian Study Teleporter",
            [TeleporterSunTower]         = "Sun Tower Teleporter",
            [TeleporterPishonDryLake]    = "Pishon Dry Lake Teleporter",

            [HeirloomAirDash]             = "Ananke's Shawl",
            [HeirloomDoubleJump]          = "Aether's Wings",
            [HeirloomMemory]              = "Aesop's Tome",
            [HeirloomBouncableDownstrike] = "Echo's Boots",
            [HeirloomVoidDash]            = "Pallas' Void Bell",
            [HeirloomCaveLantern]         = "Theia's Sun Lantern",
        };

        // Rune item names
        for (int i = 0; i < GameConstants.RuneNames.Length; i++)
            d[BASE_ID + RUNE_OFFSET + i] = $"{GameConstants.RuneNames[i]} Rune";

        // Blueprint item names
        for (int c = 0; c < GameConstants.BlueprintCategories.Length; c++)
            for (int t = 0; t < GameConstants.BlueprintTypes.Length; t++)
                d[BASE_ID + BLUEPRINT_OFFSET + c * 16 + t] = $"{GameConstants.BlueprintTypes[t]} {GameConstants.BlueprintCategories[c]} Blueprint";

        // Manor upgrade item names
        for (int i = 0; i < GameConstants.ManorItemNames.Length; i++)
            d[BASE_ID + MANOR_OFFSET + i] = GameConstants.ManorItemNames[i];

        return d;
    }

    // ── Lookup methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Maps an Archipelago item ID to its <see cref="RuneType"/>.
    /// Returns <c>null</c> if the item isn't a tracked rune.
    /// </summary>
    public static RuneType? ToRuneType(long itemId)
    {
        long offset = itemId - BASE_ID - RUNE_OFFSET;
        if (offset < 0 || offset > 23) return null;
        return (int)offset switch
        {
            0  => RuneType.ArmorRegen,
            1  => RuneType.Dash,
            2  => RuneType.DoubleJump,
            3  => RuneType.GoldGain,
            4  => RuneType.Haste,
            5  => RuneType.Lifesteal,
            6  => RuneType.Magnet,
            7  => RuneType.ReturnDamage,
            8  => RuneType.ManaRegen,
            9  => RuneType.MaxMana,
            10 => RuneType.ManaOnSpinKick,
            11 => RuneType.StatusEffectDuration,
            12 => RuneType.SoulSteal,
            13 => RuneType.ResolveGain,
            14 => RuneType.OreGain,
            15 => RuneType.RuneOreGain,
            16 => RuneType.WeaponCritChanceAdd,
            17 => RuneType.MagicCritChanceAdd,
            18 => RuneType.WeaponCritDamageAdd,
            19 => RuneType.MagicCritDamageAdd,
            20 => RuneType.SuperCritChanceAdd,
            21 => RuneType.SuperCritDamageAdd,
            22 => RuneType.ArmorMinBlock,
            23 => RuneType.ArmorHealth,
            _  => null,
        };
    }

    /// <summary>
    /// Maps an Archipelago item ID to its <see cref="HeirloomType"/>.
    /// Returns <c>null</c> if the item isn't a tracked heirloom.
    /// </summary>
    public static HeirloomType? ToHeirloomType(long itemId) => itemId switch
    {
        HeirloomAirDash             => HeirloomType.UnlockAirDash,
        HeirloomDoubleJump          => HeirloomType.UnlockDoubleJump,
        HeirloomMemory              => HeirloomType.UnlockMemory,
        HeirloomBouncableDownstrike => HeirloomType.UnlockBouncableDownstrike,
        HeirloomVoidDash            => HeirloomType.UnlockVoidDash,
        HeirloomCaveLantern         => HeirloomType.CaveLantern,
        _ => null,
    };

    /// <summary>
    /// Maps an Archipelago item ID to its <see cref="EquipmentCategoryType"/> and
    /// <see cref="EquipmentType"/> pair. Returns <c>null</c> if the item isn't a
    /// tracked equipment blueprint.
    /// </summary>
    public static (EquipmentCategoryType Category, EquipmentType EquipType)? ToEquipmentBlueprint(long itemId)
    {
        long offset = itemId - BASE_ID - BLUEPRINT_OFFSET;
        if (offset < 0) return null;

        int categoryIndex = (int)(offset / 16);
        int typeIndex     = (int)(offset % 16);
        if (categoryIndex > 4 || typeIndex > 12) return null;

        EquipmentCategoryType category = categoryIndex switch
        {
            0 => EquipmentCategoryType.Weapon,
            1 => EquipmentCategoryType.Head,
            2 => EquipmentCategoryType.Chest,
            3 => EquipmentCategoryType.Cape,
            4 => EquipmentCategoryType.Trinket,
            _ => EquipmentCategoryType.None,
        };
        EquipmentType equipType = typeIndex switch
        {
            0  => EquipmentType.GEAR_BONUS_WEIGHT,
            1  => EquipmentType.GEAR_MAGIC_CRIT,
            2  => EquipmentType.GEAR_STRENGTH_CRIT,
            3  => EquipmentType.GEAR_LIFE_STEAL,
            4  => EquipmentType.GEAR_ARMOR,
            5  => EquipmentType.GEAR_MAGIC_DMG,
            6  => EquipmentType.GEAR_MOBILITY,
            7  => EquipmentType.GEAR_GOLD,
            8  => EquipmentType.GEAR_RETURN_DMG,
            9  => EquipmentType.GEAR_MAG_ON_HIT,
            10 => EquipmentType.GEAR_LIFE_STEAL_2,
            11 => EquipmentType.GEAR_REVIVE,
            12 => EquipmentType.GEAR_FINAL_BOSS,
            _  => EquipmentType.None,
        };
        if (category == EquipmentCategoryType.None || equipType == EquipmentType.None) return null;

        return (category, equipType);
    }

    /// <summary>
    /// Maps an Archipelago item ID to its <see cref="SkillTreeType"/>.
    /// Returns <c>null</c> if the item isn't a tracked manor upgrade.
    /// </summary>
    public static SkillTreeType? ToSkillTreeType(long itemId)
    {
        long offset = itemId - BASE_ID - MANOR_OFFSET;
        if (offset < 0 || offset >= s_skillTreeTypes.Length) return null;
        return s_skillTreeTypes[(int)offset];
    }

    /// <summary>
    /// Maps an Archipelago item ID to the <see cref="BiomeType"/> whose teleporter
    /// it unlocks. Returns <c>null</c> if the item isn't a tracked teleporter unlock.
    /// </summary>
    public static BiomeType? ToTeleporterBiomeType(long itemId) => itemId switch
    {
        TeleporterAxisMundi        => BiomeType.Stone,
        TeleporterKerguelenPlateau => BiomeType.Forest,
        TeleporterStygianStudy     => BiomeType.Study,
        TeleporterSunTower         => BiomeType.Tower,
        TeleporterPishonDryLake    => BiomeType.Cave,
        _                          => null,
    };

    /// <summary>
    /// Maps an Archipelago trap item ID to the <see cref="BurdenType"/> it activates.
    /// Returns <c>null</c> if the item isn't a trap.
    /// </summary>
    public static BurdenType? ToTrapBurdenType(long itemId) => itemId switch
    {
        TrapCannonballRain  => BurdenType.BridgeBiomeUp,
        TrapDragonLancers   => BurdenType.TowerBiomeUp,
        TrapAutomatonSwarm  => BurdenType.CaveBiomeUp,
        TrapGiantSnowflakes => BurdenType.ForestBiomeUp,
        TrapVoidWaves       => BurdenType.StudyBiomeUp,
        _                   => null,
    };
}
