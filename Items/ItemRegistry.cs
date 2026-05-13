using System.Collections.Generic;

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

    // ── Manor upgrade items ──────────────────────────────────────────────────
    //
    // Canonical ordered list mapping list index → SkillTreeType.
    // MUST stay in lockstep with _CORE_MANOR_UPGRADES + _NPC_MANOR_UPGRADES in items.py.
    // Indices 0-68 are core upgrades; 69-71 are NPC unlock slots.
    /// <summary>Ordered list mapping slot index → <see cref="SkillTreeType"/>, in lockstep with the Python apworld.</summary>
    public static IReadOnlyList<SkillTreeType> SkillTreeTypes => s_skillTreeTypes;

    internal static readonly SkillTreeType[] s_skillTreeTypes =
    [
        SkillTreeType.Health_Up,                  // 0
        SkillTreeType.Health_Up2,                 // 1
        SkillTreeType.Health_Up3,                 // 2
        // SkillTreeType.Death_Dodge,             
        SkillTreeType.Potion_Up,                  // 3
        // SkillTreeType.Invuln_Time_Up,          
        SkillTreeType.Attack_Up,                  // 4
        SkillTreeType.Attack_Up2,                 // 5
        SkillTreeType.Attack_Up3,                 // 6
        SkillTreeType.Down_Strike_Up,             // 7
        SkillTreeType.Dexterity_Add1,             // 8
        SkillTreeType.Dexterity_Add2,             // 9
        SkillTreeType.Dexterity_Add3,             // 10
        SkillTreeType.Crit_Damage_Up,             // 11
        SkillTreeType.Magic_Attack_Up,            // 12
        SkillTreeType.Magic_Attack_Up2,           // 13
        SkillTreeType.Magic_Attack_Up3,           // 14
        SkillTreeType.Focus_Up1,                  // 15
        SkillTreeType.Focus_Up2,                  // 16
        SkillTreeType.Focus_Up3,                  // 17
        SkillTreeType.Magic_Crit_Damage_Up,       // 18
        // SkillTreeType.Cooldown_Reduction_Up,   
        SkillTreeType.Equip_Up,                   // 19
        SkillTreeType.Equip_Up2,                  // 20
        SkillTreeType.Equip_Up3,                  // 21
        SkillTreeType.Rune_Equip_Up,              // 22
        SkillTreeType.Rune_Equip_Up2,             // 23
        SkillTreeType.Rune_Equip_Up3,             // 24
        SkillTreeType.Armor_Up,                   // 25
        SkillTreeType.Armor_Up2,                  // 26
        SkillTreeType.Armor_Up3,                  // 27
        SkillTreeType.Traits_Give_Gold,           // 28
        SkillTreeType.Traits_Give_Gold_Gain_Mod,  // 29
        SkillTreeType.Equipment_Ore_Find_Up,      // 30
        SkillTreeType.Rune_Ore_Find_Up,           // 31
        SkillTreeType.Gold_Gain_Up,               // 32
        // SkillTreeType.Gold_Gain_Up_2,          
        // SkillTreeType.Gold_Gain_Up_3,          
        // SkillTreeType.Gold_Gain_Up_4,          
        // SkillTreeType.Gold_Gain_Up_5,          
        SkillTreeType.Randomize_Children,         // 33
        SkillTreeType.Weight_CD_Reduce,           // 34
        // SkillTreeType.Mana_Cost_Down,         
        SkillTreeType.Gold_Saved_Cap_Up,          // 35
        SkillTreeType.Gold_Saved_Amount_Saved,    // 36
        SkillTreeType.BoxingGlove_Class_Unlock,   // 37
        SkillTreeType.Saber_Class_Unlock,         // 38
        SkillTreeType.DualBlades_Class_Unlock,    // 39
        SkillTreeType.Architect_Cost_Down,        // 40
        // SkillTreeType.Polymorph_Class_Unlock,  
        SkillTreeType.More_Children,              // 41
        SkillTreeType.Ladle_Class_Unlock,         // 42
        // SkillTreeType.Chakram_Class_Unlock,    
        // SkillTreeType.Tonfa_Class_Unlock,      
        // SkillTreeType.Sword_Class_Unlock,      
        SkillTreeType.Axe_Class_Unlock,           // 43
        SkillTreeType.Wand_Class_Unlock,          // 44
        SkillTreeType.Bow_Class_Unlock,           // 45
        SkillTreeType.Spear_Class_Unlock,         // 46
        // SkillTreeType.Kunai_Class_Unlock,      
        // SkillTreeType.Siphon_Class_Unlock,     
        // SkillTreeType.Cane_Class_Unlock,       
        SkillTreeType.Gun_Class_Unlock,           // 47
        SkillTreeType.Samurai_Class_Unlock,       // 48
        SkillTreeType.Music_Class_Unlock,         // 49
        SkillTreeType.Pirate_Class_Unlock,        // 50
        SkillTreeType.Astro_Class_Unlock,         // 51
        // SkillTreeType.Weapon_Master_Upgrade,   
        // SkillTreeType.Knight_Upgrade,          
        SkillTreeType.Lancer_Class_Unlock,        // 52
        SkillTreeType.XP_Up,                      // 53
        SkillTreeType.Equipment_Ore_Gain_Up,      // 54
        SkillTreeType.Rune_Ore_Gain_Up,           // 55
        // SkillTreeType.Potions_Free_Cast_Up,    
        SkillTreeType.Unlock_Dummy,               // 56
        SkillTreeType.Boss_Health_Restore,        // 57
        SkillTreeType.Unlock_Totem,               // 58
        SkillTreeType.Relic_Cost_Down,            // 59
        SkillTreeType.Reroll_Relic,               // 60
        SkillTreeType.Potion_Recharge_Talent,     // 61
        SkillTreeType.Resolve_Up,                 // 62
        SkillTreeType.Dash_Strike_Up,             // 63
        SkillTreeType.Charon_Gold_Stat_Bonus,     // 64
        SkillTreeType.Crit_Chance_Flat_Up,        // 65
        SkillTreeType.Magic_Crit_Chance_Flat_Up,  // 66
        SkillTreeType.Reroll_Relic_Room_Cap,      // 67
        SkillTreeType.Architect,                  // 68
        // NPC unlock slots — active only when randomize_npc_unlocks=true
        SkillTreeType.Gold_Saved_Unlock,          // 69
        SkillTreeType.Smithy,                     // 70
        SkillTreeType.Enchantress,                // 71
        // SkillTreeType.Banker,                  
    ];

    // Display names parallel to s_skillTreeTypes — sourced from GameConstants so the
    // string literals live in exactly one place.
    internal static readonly string[] s_manorDisplayNames = GameConstants.ManorUpgradeNames;

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
        for (int i = 0; i < s_manorDisplayNames.Length; i++)
            d[BASE_ID + MANOR_OFFSET + i] = $"Manor: {s_manorDisplayNames[i]}";

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
}
