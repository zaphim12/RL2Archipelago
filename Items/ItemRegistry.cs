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

    // ── Manor upgrade items ──────────────────────────────────────────────────
    //
    // Canonical ordered list mapping list index → SkillTreeType.
    // MUST stay in lockstep with _CORE_MANOR_UPGRADES + _NPC_MANOR_UPGRADES in items.py.
    // Indices 0-68 are core upgrades; 69-71 are NPC unlock slots.
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

    // Display names parallel to s_skillTreeTypes — indices must match exactly.
    // Declared before Names so it is initialized before BuildNames() runs.
    internal static readonly string[] s_manorDisplayNames =
    [
        "Mess Hall (Vitality Up I)",
        "Fruit Juice Bar (Vitality Up II)",
        "Meteora Gym (Vitality Up III)",
        // "Veterinarian Clinic (Revive Chance Up)",
        "Institute of Gastronomy (Health Drop Scaling)",
        // "Stadium (Invulnerability Up)",
        "Arsenal (Strength Up I)",
        "Sauna (Strength Up II)",
        "Rock Climbing Wall (Strength Up III)",
        "Bamboo Garden (Spin Kick scales with INT)",
        "Gym (Dexterity Up I)",
        "Yoga Class (Dexterity Up II)",
        "Flower Shop (Dexterity Up III)",
        "The Laundromat (Weapon Crit Damage Up)",
        "Study Hall (Intelligence Up I)",
        "Math Club (Intelligence Up II)",
        "University (Intelligence Up III)",
        "Library (Focus Up I)",
        "Hall of Wisdom (Focus Up II)",
        "Court of the Wise (Focus Up III)",
        "The Lodge (Magic Crit Damage Up)",
        // "The Thaumaturgy (Cooldown Reduction)",
        "Fashion Chambers (Max Weight Up I)",
        "Tailors (Max Weight Up II)",
        "Artisan (Max Weight Up III)",
        "Etching Chambers (Max Rune Weight Up I)",
        "Pillow Mill (Max Rune Weight Up II)",
        "Bed Mill (Max Rune Weight Up III)",
        "Foundry (Armor Up I)",
        "Blast Furnace (Armor Up II)",
        "Some Kind of Kiln (Armor Up III)",
        "Universal Health Stair (Traits Give Gold)",
        "Repurposed Mining Shaft (Traits Gold Gain Up)",
        "Geologist's Camp (Ore Drop Chance Up)", 
        "Dowsing Center (Red Aether Drop Chance Up)",
        "Massive Vault (Gold Gain Up I)",       
        // "Gold Gain Up II",
        // "Gold Gain Up III",
        // "Sky Bridge (Gold Gain Up IV)",
        // "Tree Bridge (Gold Gain Up V)",
        "Career Center (Re-Roll Children)",
        "Aerobics Classroom (Encumbrance Limit Up)",
        // "The Fissary (Mana Cost Down)",
        "Courthouse (Living Safe Max Gold Up)",
        "Scribe's Office (Living Safe Conversion Up)",
        "Fighting Ring (Boxer Class)",
        "Dance Hall (Duelist Class)",
        "Guild of Dark Arts (Assassin Class)",
        "Drill Store (Architect Cost Reduction)",
        // "Genesis Pool (Polymorph Class)",
        "Adoption Center (More Heirs)",
        "The Kitchen (Chef Class)",
        // "Forest Village (Chakram Class)",
        // "Martial Arts School (Tonfa Class)",
        // "The Pilgrim's Steps (Knight Class)",
        "Butcher's Shoppe (Barbarian Class)",
        "Academy (Mage Class)",
        "Archery Range (Ranger Class)",
        "Sand Pits (Valkyrie Class)",
        // "Hidden Dojo (Ninja Class)",
        // "Ancestral Plot (Lich Class)",
        // "Miner's Camp (Spelunker Class)",
        "Saltpeter Mines (Gunslinger Class)",
        "Ryokan (Ronin Class)",
        "The Tavern (Bard Class)",
        "The Flying Docks (Pirate Class)",
        "The Astral Gardens (Astromancer Class)",
        // "Weapon Master Upgrade",
        // "Knight Upgrade",
        "The Aviary (Dragon Lancer Class)",
        "Trophy Room (XP Up)",
        "Jeweler (Ore Gain Up)",
        "Buried Tomb (Red Aether Gain Up)",
        // "Herb Garden (Free Cast Up)",
        "Dummy (Training Dummy Unlock)",
        "Meditation Studies (Boss Health/Mana Restore)",
        "Sage Totem (Mastery Rank Unlock)",
        "Archaeology Camp (Relic Resolve Cost Down)",
        "Medieval Forgery (Relic Reroll Up)",
        "Alchemy Lab (Potion Recharge Talent)",
        "Psychiatrist (Resolve Up)",
        "Jousting Studies (Dash Damage Reduction)",
        "Charity Dungeon (Charon Donation Bonus Unlock)",
        "The Dicer's Den (Weapon Crit Chance Up)",
        "The Quantum Observatory (Magic Crit Chance Up)",
        "The Bizarre Bazaar (Reroll Relic Room Cap Up)",
        "Screw Distillery (Architect Unlock)",
        // NPC unlock slots (indices 68-71)
        "Offshore Bank Account (Living Safe)",
        "Foundation (Smithy Unlock)",
        "Enchantress' Quarters (Enchantress Unlock)",
        // "Banker Unlock",
    ];

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
        string[] runeNames =
        [
            "Reinforced",   "Dash",        "Vault",    "Bounty",
            "Haste",        "Lifesteal",   "Magnesis", "Retaliation",
            "Siphon",       "Capacity",    "Trick",    "Amplification",
            "Soulsteal",    "Resolve",     "Stone",    "Red",
            "Sharpened",    "Focal",       "Might",    "Eldar",
            "Lucky Roller", "High Stakes", "Folded",   "Quenching",
        ];
        for (int i = 0; i < runeNames.Length; i++)
            d[BASE_ID + RUNE_OFFSET + i] = $"{runeNames[i]} Rune";

        // Blueprint item names
        string[] categoryNames = [ "Weapon", "Helm", "Chest", "Cape", "Trinket" ];
        string[] typeNames =
        [
            "Leather",    "Scholar",   "Warden",  "Sanguine",
            "Ammonite",   "Crescent",  "Drowned", "Gilded",
            "Obsidian",   "Leviathan", "Kin",
            "White Wood", "Black Root", 
        ];
        for (int c = 0; c < 5; c++)
            for (int t = 0; t < 13; t++)
                d[BASE_ID + BLUEPRINT_OFFSET + c * 16 + t] = $"{typeNames[t]} {categoryNames[c]} Blueprint";

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
            11 => EquipmentType.GEAR_EMPTY_1,
            12 => EquipmentType.GEAR_EMPTY_2,
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
