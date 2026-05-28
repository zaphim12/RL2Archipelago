using System.Linq;

namespace RL2Archipelago.Items;

/// <summary>
/// String constants shared between <see cref="ItemRegistry"/> and
/// <see cref="RL2Archipelago.Locations.LocationRegistry"/>.
/// Must stay in lockstep with <c>apworld/rogue_legacy_2/constants.py</c>.
/// </summary>
public static class GameConstants
{
    // ── Biomes ───────────────────────────────────────────────────────────────
    // Ordered to match biomeIndex used in location ID calculations (Castle=0 … Cave=5).

    public const string BiomeCitadelAgartha   = "Citadel Agartha";
    public const string BiomeAxisMundi        = "Axis Mundi";
    public const string BiomeKerguelenPlateau = "Kerguelen Plateau";
    public const string BiomeStygianStudy     = "Stygian Study";
    public const string BiomeSunTower         = "Sun Tower";
    public const string BiomePishonDryLake    = "Pishon Dry Lake";

    public static readonly string[] BiomeNames =
    [
        BiomeCitadelAgartha, BiomeAxisMundi, BiomeKerguelenPlateau,
        BiomeStygianStudy, BiomeSunTower, BiomePishonDryLake,
    ];

    // ── Runes ────────────────────────────────────────────────────────────────
    // Ordered to match ItemRegistry.ToRuneType() index mapping.

    public static readonly string[] RuneNames =
    [
        "Reinforced",   "Dash",        "Vault",    "Bounty",
        "Haste",        "Lifesteal",   "Magnesis", "Retaliation",
        "Siphon",       "Capacity",    "Trick",    "Amplification",
        "Soulsteal",    "Resolve",     "Stone",    "Red",
        "Sharpened",    "Focal",       "Might",    "Eldar",
        "Lucky Roller", "High Stakes", "Folded",   "Quenching",
    ];

    // ── Blueprints ───────────────────────────────────────────────────────────
    // ID = BASE_ID + BLUEPRINT_OFFSET + categoryIndex * 16 + typeIndex

    public static readonly string[] BlueprintCategories = [ "Weapon", "Helm", "Chest", "Cape", "Trinket" ];
    public static readonly string[] BlueprintTypes =
    [
        "Leather",    "Scholar",   "Warden",  "Sanguine",
        "Ammonite",   "Crescent",  "Drowned", "Gilded",
        "Obsidian",   "Leviathan", "Kin",
        "White Wood", "Black Root",
    ];

    // ── Manor upgrade item names ─────────────────────────────────────────────────────
    // Ordered to match ItemRegistry.s_skillTreeTypes (indices 0-68 core, 69-71 NPC).

    // Vitality
    public const string MessHallItemName             = "Vitality Up I";
    public const string FruitJuiceBarItemName        = "Vitality Up II";
    public const string MeteoraGymItemName           = "Vitality Up III";
    // Misc survival
    public const string InstituteOfGastronomyItemName = "Health Drop Scaling";
    // Strength
    public const string ArsenalItemName              = "Strength Up I";
    public const string SaunaItemName                = "Strength Up II";
    public const string RockClimbingWallItemName     = "Strength Up III";
    // Spin kicks
    public const string BambooGardenItemName         = "Spin Kick scales with INT";
    // Dexterity
    public const string GymItemName                  = "Dexterity Up I";
    public const string YogaClassItemName            = "Dexterity Up II";
    public const string FlowerShopItemName           = "Dexterity Up III";
    // Weapon crit damage
    public const string LaundromatItemName           = "Weapon Crit Damage Up";
    // Intelligence
    public const string StudyHallItemName            = "Intelligence Up I";
    public const string MathClubItemName             = "Intelligence Up II";
    public const string UniversityItemName           = "Intelligence Up III";
    // Focus
    public const string LibraryItemName              = "Focus Up I";
    public const string HallOfWisdomItemName         = "Focus Up II";
    public const string CourtOfTheWiseItemName       = "Focus Up III";
    // Magic crit damage
    public const string LodgeItemName                = "Magic Crit Damage Up";
    // Equipment weight
    public const string FashionChambersItemName      = "Max Weight Up I";
    public const string TailorsItemName              = "Max Weight Up II";
    public const string ArtisanItemName              = "Max Weight Up III";
    // Rune weight
    public const string EtchingChambersItemName      = "Max Rune Weight Up I";
    public const string PillowMillItemName           = "Max Rune Weight Up II";
    public const string BedMillItemName              = "Max Rune Weight Up III";
    // Armor
    public const string FoundryItemName              = "Armor Up I";
    public const string BlastFurnaceItemName         = "Armor Up II";
    public const string SomeKindOfKilnItemName       = "Armor Up III";
    // Gold & economy
    public const string UniversalHealthStairItemName  = "Traits Give Gold";
    public const string RepurposedMiningShaftItemName = "Traits Gold Gain Up";
    public const string GeologistsCampItemName        = "Ore Drop Chance Up";
    public const string DowsingCenterItemName         = "Red Aether Drop Chance Up";
    public const string MassiveVaultItemName          = "Gold Gain Up I";
    // Rerolls
    public const string CareerCenterItemName          = "Re-Roll Children";
    public const string AerobicsClassroomItemName     = "Encumbrance Limit Up";
    // Living safe
    public const string CourthouseItemName            = "Living Safe Max Gold Up";
    public const string ScribesOfficeItemName         = "Living Safe Conversion Up";
    // Class unlocks
    public const string FightingRingItemName          = "Boxer Class";
    public const string DanceHallItemName             = "Duelist Class";
    public const string GuildOfDarkArtsItemName       = "Assassin Class";
    public const string DrillStoreItemName            = "Architect Cost Reduction";
    public const string AdoptionCenterItemName        = "More Heirs";
    public const string KitchenItemName               = "Chef Class";
    public const string ButchersShoppeItemName        = "Barbarian Class";
    public const string AcademyItemName               = "Mage Class";
    public const string ArcheryRangeItemName          = "Ranger Class";
    public const string SandPitsItemName              = "Valkyrie Class";
    public const string SaltpeterMinesItemName        = "Gunslinger Class";
    public const string RyokanItemName                = "Ronin Class";
    public const string TavernItemName                = "Bard Class";
    public const string FlyingDocksItemName           = "Pirate Class";
    public const string AstralGardensItemName         = "Astromancer Class";
    public const string AviaryItemName                = "Dragon Lancer Class";
    // Progression & utility
    public const string TrophyRoomItemName            = "XP Up";
    public const string JewelerItemName               = "Ore Gain Up";
    public const string BuriedTombItemName            = "Red Aether Gain Up";
    public const string DummyItemName                 = "Training Dummy Unlock";
    public const string MeditationStudiesItemName     = "Boss Health/Mana Restore";
    public const string SageTotemItemName             = "Mastery Rank Unlock";
    public const string ArchaeologyCampItemName       = "Relic Resolve Cost Down";
    public const string MedievalForgeryItemName       = "Relic Reroll Up";
    public const string AlchemyLabItemName            = "Potion Recharge Talent";
    public const string PsychiatristItemName          = "Resolve Up";
    public const string JoustingStudiesItemName       = "Dash Damage Reduction";
    public const string CharityDungeonItemName        = "Charon Donation Bonus Unlock";
    public const string DicersDenItemName             = "Weapon Crit Chance Up";
    public const string QuantumObservatoryItemName    = "Magic Crit Chance Up";
    public const string BizarreBazaarItemName         = "Reroll Relic Room Cap Up";
    public const string ScrewDistilleryItemName       = "Architect Unlock";
    // NPC unlocks
    public const string OffshoreBankAccountItemName   = "Living Safe Unlock";
    public const string FoundationItemName            = "Blacksmith Unlock";
    public const string EnchantressQuartersItemName   = "Enchantress Unlock";
    // Knight Class (conditional AP item; only in pool when starting class ≠ Knight)
    public const string KnightClassItemName           = "Knight Class";

    // ── Manor location slot names ─────────────────────────────────────────────────────

    // Vitality
    public const string MessHallLocationName             = "Mess Hall";
    public const string FruitJuiceBarLocationName        = "Fruit Juice Bar";
    public const string MeteoraGymLocationName           = "Meteora Gym";
    // Misc survival
    public const string InstituteOfGastronomyLocationName = "Institute of Gastronomy";
    // Strength
    public const string ArsenalLocationName              = "Arsenal";
    public const string SaunaLocationName                = "Sauna";
    public const string RockClimbingWallLocationName     = "Rock Climbing Wall";
    // Spin kicks
    public const string BambooGardenLocationName         = "Bamboo Garden";
    // Dexterity
    public const string GymLocationName                  = "Gym";
    public const string YogaClassLocationName            = "Yoga Class";
    public const string FlowerShopLocationName           = "Flower Shop";
    // Weapon crit damage
    public const string LaundromatLocationName           = "The Laundromat";
    // Intelligence
    public const string StudyHallLocationName            = "Study Hall";
    public const string MathClubLocationName             = "Math Club";
    public const string UniversityLocationName           = "University";
    // Focus
    public const string LibraryLocationName              = "Library";
    public const string HallOfWisdomLocationName         = "Hall of Wisdom";
    public const string CourtOfTheWiseLocationName       = "Court of the Wise";
    // Magic crit damage
    public const string LodgeLocationName                = "The Lodge";
    // Equipment weight
    public const string FashionChambersLocationName      = "Fashion Chambers";
    public const string TailorsLocationName              = "Tailors";
    public const string ArtisanLocationName              = "Artisan";
    // Rune weight
    public const string EtchingChambersLocationName      = "Etching Chambers";
    public const string PillowMillLocationName           = "Pillow Mill";
    public const string BedMillLocationName              = "Bed Mill";
    // Armor
    public const string FoundryLocationName              = "Foundry";
    public const string BlastFurnaceLocationName         = "Blast Furnace";
    public const string SomeKindOfKilnLocationName       = "Some Kind of Kiln";
    // Gold & economy
    public const string UniversalHealthStairLocationName  = "Universal Health Stair";
    public const string RepurposedMiningShaftLocationName = "Repurposed Mining Shaft";
    public const string GeologistsCampLocationName        = "Geologist's Camp";
    public const string DowsingCenterLocationName         = "Dowsing Center";
    public const string MassiveVaultLocationName          = "Massive Vault";
    // Rerolls
    public const string CareerCenterLocationName          = "Career Center";
    public const string AerobicsClassroomLocationName     = "Aerobics Classroom";
    // Living safe
    public const string CourthouseLocationName            = "Courthouse";
    public const string ScribesOfficeLocationName         = "Scribe's Office";
    // Class unlocks
    public const string FightingRingLocationName          = "Fighting Ring";
    public const string DanceHallLocationName             = "Dance Hall";
    public const string GuildOfDarkArtsLocationName       = "Guild of Dark Arts";
    public const string DrillStoreLocationName            = "Drill Store";
    public const string AdoptionCenterLocationName        = "Adoption Center";
    public const string KitchenLocationName               = "The Kitchen";
    public const string ButchersShoppeLocationName        = "Butcher's Shoppe";
    public const string AcademyLocationName               = "Academy";
    public const string ArcheryRangeLocationName          = "Archery Range";
    public const string SandPitsLocationName              = "Sand Pits";
    public const string SaltpeterMinesLocationName        = "Saltpeter Mines";
    public const string RyokanLocationName               = "Ryokan";
    public const string TavernLocationName                = "The Tavern";
    public const string FlyingDocksLocationName           = "The Flying Docks";
    public const string AstralGardensLocationName         = "The Astral Gardens";
    public const string AviaryLocationName                = "The Aviary";
    // Progression & utility
    public const string TrophyRoomLocationName            = "Trophy Room";
    public const string JewelerLocationName               = "Jeweler";
    public const string BuriedTombLocationName            = "Buried Tomb";
    public const string DummyLocationName                 = "Dummy";
    public const string MeditationStudiesLocationName     = "Meditation Studies";
    public const string SageTotemLocationName             = "Sage Totem";
    public const string ArchaeologyCampLocationName       = "Archaeology Camp";
    public const string MedievalForgeryLocationName       = "Medieval Forgery";
    public const string AlchemyLabLocationName            = "Alchemy Lab";
    public const string PsychiatristLocationName          = "Psychiatrist";
    public const string JoustingStudiesLocationName       = "Jousting Studies";
    public const string CharityDungeonLocationName        = "Charity Dungeon";
    public const string DicersDenLocationName             = "The Dicer's Den";
    public const string QuantumObservatoryLocationName    = "The Quantum Observatory";
    public const string BizarreBazaarLocationName         = "The Bizarre Bazaar";
    public const string ScrewDistilleryLocationName       = "Screw Distillery";
    // NPC unlocks
    public const string OffshoreBankAccountLocationName   = "Offshore Bank Account";
    public const string FoundationLocationName            = "Foundation";
    public const string EnchantressQuartersLocationName   = "Enchantress' Quarters";


    // The following SkillTreeType values exist in the game's enum but are excluded
    // from the randomizer because they are unimplemented:
    //   Death_Dodge           (Veterinarian Clinic  - Revive Chance Up)
    //   Invuln_Time_Up        (Stadium              - Invulnerability Up)
    //   Cooldown_Reduction_Up (The Thaumaturgy      - Cooldown Reduction)
    //   Gold_Gain_Up_2/3/4/5  (Sky Bridge, Tree Bridge, ... - Gold Gain II-V)
    //   Mana_Cost_Down        (The Fissary          - Mana Cost Down)
    //   Potions_Free_Cast_Up  (Herb Garden          - Free Cast Up)
    //   Weapon_Master_Upgrade, Knight_Upgrade
    //   Polymorph_Class_Unlock  (Genesis Pool)
    //   Chakram_Class_Unlock    (Forest Village)
    //   Tonfa_Class_Unlock      (Martial Arts School)
    //   Sword_Class_Unlock      (The Pilgrim's Steps)
    //   Kunai_Class_Unlock      (Hidden Dojo)
    //   Siphon_Class_Unlock     (Ancestral Plot)
    //   Cane_Class_Unlock       (Miner's Camp)
    //   Banker                  (NPC - not present in base game)

    /// <summary>
    /// One entry per randomized manor slot, ordered to match <c>_MANOR_SLOTS</c> in
    /// constants.py. Indices 0-68 are core upgrades; 69-71 are NPC unlock slots.
    /// <see cref="ManorItemNames"/> and <see cref="ManorLocationNames"/> are derived
    /// from this array, as is <see cref="ItemRegistry.s_skillTreeTypes"/>.
    /// </summary>
    public readonly struct ManorSlot
    {
        public readonly SkillTreeType SkillTree;
        public readonly string ItemName;
        public readonly string LocationName;
        public ManorSlot(SkillTreeType skillTree, string itemName, string locationName)
            => (SkillTree, ItemName, LocationName) = (skillTree, itemName, locationName);
    }

    public static readonly ManorSlot[] ManorSlots =
    [
        // ── Vitality ─────────────────────────────────────────────────────────
        new(SkillTreeType.Health_Up,                 MessHallItemName,              MessHallLocationName),              // 0
        new(SkillTreeType.Health_Up2,                FruitJuiceBarItemName,         FruitJuiceBarLocationName),         // 1
        new(SkillTreeType.Health_Up3,                MeteoraGymItemName,            MeteoraGymLocationName),            // 2
        // ── Strength ─────────────────────────────────────────────────────────
        new(SkillTreeType.Attack_Up,                 ArsenalItemName,               ArsenalLocationName),               // 3
        new(SkillTreeType.Attack_Up2,                SaunaItemName,                 SaunaLocationName),                 // 4
        new(SkillTreeType.Attack_Up3,                RockClimbingWallItemName,      RockClimbingWallLocationName),      // 5
        // ── Dexterity ────────────────────────────────────────────────────────
        new(SkillTreeType.Dexterity_Add1,            GymItemName,                   GymLocationName),                   // 6
        new(SkillTreeType.Dexterity_Add2,            YogaClassItemName,             YogaClassLocationName),             // 7
        new(SkillTreeType.Dexterity_Add3,            FlowerShopItemName,            FlowerShopLocationName),            // 8
        // ── Weapon crit ──────────────────────────────────────────────────────
        new(SkillTreeType.Crit_Damage_Up,            LaundromatItemName,            LaundromatLocationName),            // 9
        new(SkillTreeType.Crit_Chance_Flat_Up,       DicersDenItemName,             DicersDenLocationName),             // 10
        // ── Intelligence ─────────────────────────────────────────────────────
        new(SkillTreeType.Magic_Attack_Up,           StudyHallItemName,             StudyHallLocationName),             // 11
        new(SkillTreeType.Magic_Attack_Up2,          MathClubItemName,              MathClubLocationName),              // 12
        new(SkillTreeType.Magic_Attack_Up3,          UniversityItemName,            UniversityLocationName),            // 13
        // ── Focus ────────────────────────────────────────────────────────────
        new(SkillTreeType.Focus_Up1,                 LibraryItemName,               LibraryLocationName),               // 14
        new(SkillTreeType.Focus_Up2,                 HallOfWisdomItemName,          HallOfWisdomLocationName),          // 15
        new(SkillTreeType.Focus_Up3,                 CourtOfTheWiseItemName,        CourtOfTheWiseLocationName),        // 16
        // ── Magic crit ───────────────────────────────────────────────────────
        new(SkillTreeType.Magic_Crit_Damage_Up,      LodgeItemName,                 LodgeLocationName),                 // 17
        new(SkillTreeType.Magic_Crit_Chance_Flat_Up, QuantumObservatoryItemName,    QuantumObservatoryLocationName),    // 18
        // ── Equipment weight ─────────────────────────────────────────────────
        new(SkillTreeType.Equip_Up,                  FashionChambersItemName,       FashionChambersLocationName),       // 19
        new(SkillTreeType.Equip_Up2,                 TailorsItemName,               TailorsLocationName),               // 20
        new(SkillTreeType.Equip_Up3,                 ArtisanItemName,               ArtisanLocationName),               // 21
        // ── Rune weight ──────────────────────────────────────────────────────
        new(SkillTreeType.Rune_Equip_Up,             EtchingChambersItemName,       EtchingChambersLocationName),       // 22
        new(SkillTreeType.Rune_Equip_Up2,            PillowMillItemName,            PillowMillLocationName),            // 23
        new(SkillTreeType.Rune_Equip_Up3,            BedMillItemName,               BedMillLocationName),               // 24
        // ── Armor ────────────────────────────────────────────────────────────
        new(SkillTreeType.Armor_Up,                  FoundryItemName,               FoundryLocationName),               // 25
        new(SkillTreeType.Armor_Up2,                 BlastFurnaceItemName,          BlastFurnaceLocationName),          // 26
        new(SkillTreeType.Armor_Up3,                 SomeKindOfKilnItemName,        SomeKindOfKilnLocationName),        // 27
        // ── Health restore ───────────────────────────────────────────────────
        new(SkillTreeType.Potion_Up,                 InstituteOfGastronomyItemName, InstituteOfGastronomyLocationName), // 28
        new(SkillTreeType.Boss_Health_Restore,       MeditationStudiesItemName,     MeditationStudiesLocationName),     // 29
        // ── Spin kicks ───────────────────────────────────────────────────────
        new(SkillTreeType.Down_Strike_Up,            BambooGardenItemName,          BambooGardenLocationName),          // 30
        // ── Gold & economy ───────────────────────────────────────────────────
        new(SkillTreeType.Traits_Give_Gold,          UniversalHealthStairItemName,  UniversalHealthStairLocationName),  // 31
        new(SkillTreeType.Traits_Give_Gold_Gain_Mod, RepurposedMiningShaftItemName, RepurposedMiningShaftLocationName), // 32
        new(SkillTreeType.Equipment_Ore_Find_Up,     GeologistsCampItemName,        GeologistsCampLocationName),        // 33
        new(SkillTreeType.Rune_Ore_Find_Up,          DowsingCenterItemName,         DowsingCenterLocationName),         // 34
        new(SkillTreeType.Gold_Gain_Up,              MassiveVaultItemName,          MassiveVaultLocationName),          // 35
        new(SkillTreeType.Equipment_Ore_Gain_Up,     JewelerItemName,               JewelerLocationName),               // 36
        new(SkillTreeType.Rune_Ore_Gain_Up,          BuriedTombItemName,            BuriedTombLocationName),            // 37
        // ── Heirs ────────────────────────────────────────────────────────────
        new(SkillTreeType.Randomize_Children,        CareerCenterItemName,          CareerCenterLocationName),          // 38
        new(SkillTreeType.More_Children,             AdoptionCenterItemName,        AdoptionCenterLocationName),        // 39
        // ── Living safe ──────────────────────────────────────────────────────
        new(SkillTreeType.Gold_Saved_Cap_Up,         CourthouseItemName,            CourthouseLocationName),            // 40
        new(SkillTreeType.Gold_Saved_Amount_Saved,   ScribesOfficeItemName,         ScribesOfficeLocationName),         // 41
        // ── Architect ────────────────────────────────────────────────────────
        new(SkillTreeType.Architect,                 ScrewDistilleryItemName,       ScrewDistilleryLocationName),       // 42
        new(SkillTreeType.Architect_Cost_Down,       DrillStoreItemName,            DrillStoreLocationName),            // 43
        // ── Class unlocks ────────────────────────────────────────────────────
        new(SkillTreeType.BoxingGlove_Class_Unlock,  FightingRingItemName,          FightingRingLocationName),          // 44
        new(SkillTreeType.Saber_Class_Unlock,        DanceHallItemName,             DanceHallLocationName),             // 45
        new(SkillTreeType.DualBlades_Class_Unlock,   GuildOfDarkArtsItemName,       GuildOfDarkArtsLocationName),       // 46
        new(SkillTreeType.Ladle_Class_Unlock,        KitchenItemName,               KitchenLocationName),               // 47
        new(SkillTreeType.Axe_Class_Unlock,          ButchersShoppeItemName,        ButchersShoppeLocationName),        // 48
        new(SkillTreeType.Wand_Class_Unlock,         AcademyItemName,               AcademyLocationName),               // 49
        new(SkillTreeType.Bow_Class_Unlock,          ArcheryRangeItemName,          ArcheryRangeLocationName),          // 50
        new(SkillTreeType.Spear_Class_Unlock,        SandPitsItemName,              SandPitsLocationName),              // 51
        new(SkillTreeType.Gun_Class_Unlock,          SaltpeterMinesItemName,        SaltpeterMinesLocationName),        // 52
        new(SkillTreeType.Samurai_Class_Unlock,      RyokanItemName,                RyokanLocationName),                // 53
        new(SkillTreeType.Music_Class_Unlock,        TavernItemName,                TavernLocationName),                // 54
        new(SkillTreeType.Pirate_Class_Unlock,       FlyingDocksItemName,           FlyingDocksLocationName),           // 55
        new(SkillTreeType.Astro_Class_Unlock,        AstralGardensItemName,         AstralGardensLocationName),         // 56
        new(SkillTreeType.Lancer_Class_Unlock,       AviaryItemName,                AviaryLocationName),                // 57
        // ── Relics ───────────────────────────────────────────────────────────
        new(SkillTreeType.Relic_Cost_Down,           ArchaeologyCampItemName,       ArchaeologyCampLocationName),       // 58
        new(SkillTreeType.Reroll_Relic,              MedievalForgeryItemName,       MedievalForgeryLocationName),       // 59
        new(SkillTreeType.Reroll_Relic_Room_Cap,     BizarreBazaarItemName,         BizarreBazaarLocationName),         // 60
        // ── Progression & utility ────────────────────────────────────────────
        new(SkillTreeType.Weight_CD_Reduce,          AerobicsClassroomItemName,     AerobicsClassroomLocationName),     // 61
        new(SkillTreeType.XP_Up,                     TrophyRoomItemName,            TrophyRoomLocationName),            // 62
        new(SkillTreeType.Resolve_Up,                PsychiatristItemName,          PsychiatristLocationName),          // 63
        new(SkillTreeType.Unlock_Dummy,              DummyItemName,                 DummyLocationName),                 // 64
        new(SkillTreeType.Unlock_Totem,              SageTotemItemName,             SageTotemLocationName),             // 65
        new(SkillTreeType.Potion_Recharge_Talent,    AlchemyLabItemName,            AlchemyLabLocationName),            // 66
        new(SkillTreeType.Dash_Strike_Up,            JoustingStudiesItemName,       JoustingStudiesLocationName),       // 67
        new(SkillTreeType.Charon_Gold_Stat_Bonus,    CharityDungeonItemName,        CharityDungeonLocationName),        // 68
        // ── NPC unlock slots ─────────────────────────────────────────────────
        new(SkillTreeType.Gold_Saved_Unlock,         OffshoreBankAccountItemName,   OffshoreBankAccountLocationName),   // 69
        new(SkillTreeType.Smithy,                    FoundationItemName,            FoundationLocationName),            // 70
        new(SkillTreeType.Enchantress,               EnchantressQuartersItemName,   EnchantressQuartersLocationName),   // 71
    ];

    /// <summary>Ordered AP item names (with "Upgrade: " prefix), derived from <see cref="ManorSlots"/>.</summary>
    public static readonly string[] ManorItemNames     = [.. ManorSlots.Select(s => $"Upgrade: {s.ItemName}")];
    /// <summary>Ordered AP location names (with "Manor - " prefix), derived from <see cref="ManorSlots"/>.</summary>
    public static readonly string[] ManorLocationNames = [.. ManorSlots.Select(s => $"Manor - {s.LocationName}")];
}
