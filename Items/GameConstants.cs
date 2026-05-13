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

    // ── Manor upgrades ───────────────────────────────────────────────────────
    // Ordered to match ItemRegistry.s_skillTreeTypes (indices 0-68 core, 69-71 NPC).

    // Vitality
    public const string MessHall             = "Mess Hall (Vitality Up I)";
    public const string FruitJuiceBar        = "Fruit Juice Bar (Vitality Up II)";
    public const string MeteoraGym           = "Meteora Gym (Vitality Up III)";
    // Misc survival
    public const string InstituteOfGastronomy = "Institute of Gastronomy (Health Drop Scaling)";
    // Strength
    public const string Arsenal              = "Arsenal (Strength Up I)";
    public const string Sauna                = "Sauna (Strength Up II)";
    public const string RockClimbingWall     = "Rock Climbing Wall (Strength Up III)";
    // Spin kicks
    public const string BambooGarden         = "Bamboo Garden (Spin Kick scales with INT)";
    // Dexterity
    public const string Gym                  = "Gym (Dexterity Up I)";
    public const string YogaClass            = "Yoga Class (Dexterity Up II)";
    public const string FlowerShop           = "Flower Shop (Dexterity Up III)";
    // Weapon crit damage
    public const string Laundromat           = "The Laundromat (Weapon Crit Damage Up)";
    // Intelligence
    public const string StudyHall            = "Study Hall (Intelligence Up I)";
    public const string MathClub             = "Math Club (Intelligence Up II)";
    public const string University           = "University (Intelligence Up III)";
    // Focus
    public const string Library              = "Library (Focus Up I)";
    public const string HallOfWisdom         = "Hall of Wisdom (Focus Up II)";
    public const string CourtOfTheWise       = "Court of the Wise (Focus Up III)";
    // Magic crit damage
    public const string Lodge                = "The Lodge (Magic Crit Damage Up)";
    // Equipment weight
    public const string FashionChambers      = "Fashion Chambers (Max Weight Up I)";
    public const string Tailors              = "Tailors (Max Weight Up II)";
    public const string Artisan              = "Artisan (Max Weight Up III)";
    // Rune weight
    public const string EtchingChambers      = "Etching Chambers (Max Rune Weight Up I)";
    public const string PillowMill           = "Pillow Mill (Max Rune Weight Up II)";
    public const string BedMill              = "Bed Mill (Max Rune Weight Up III)";
    // Armor
    public const string Foundry              = "Foundry (Armor Up I)";
    public const string BlastFurnace         = "Blast Furnace (Armor Up II)";
    public const string SomeKindOfKiln       = "Some Kind of Kiln (Armor Up III)";
    // Gold & economy
    public const string UniversalHealthStair  = "Universal Health Stair (Traits Give Gold)";
    public const string RepurposedMiningShaft = "Repurposed Mining Shaft (Traits Gold Gain Up)";
    public const string GeologistsCamp        = "Geologist's Camp (Ore Drop Chance Up)";
    public const string DowsingCenter         = "Dowsing Center (Red Aether Drop Chance Up)";
    public const string MassiveVault          = "Massive Vault (Gold Gain Up I)";
    // Rerolls
    public const string CareerCenter          = "Career Center (Re-Roll Children)";
    public const string AerobicsClassroom     = "Aerobics Classroom (Encumbrance Limit Up)";
    // Living safe
    public const string Courthouse            = "Courthouse (Living Safe Max Gold Up)";
    public const string ScribesOffice         = "Scribe's Office (Living Safe Conversion Up)";
    // Class unlocks
    public const string FightingRing          = "Fighting Ring (Boxer Class)";
    public const string DanceHall             = "Dance Hall (Duelist Class)";
    public const string GuildOfDarkArts       = "Guild of Dark Arts (Assassin Class)";
    public const string DrillStore            = "Drill Store (Architect Cost Reduction)";
    public const string AdoptionCenter        = "Adoption Center (More Heirs)";
    public const string Kitchen               = "The Kitchen (Chef Class)";
    public const string ButchersShoppe        = "Butcher's Shoppe (Barbarian Class)";
    public const string Academy               = "Academy (Mage Class)";
    public const string ArcheryRange          = "Archery Range (Ranger Class)";
    public const string SandPits              = "Sand Pits (Valkyrie Class)";
    public const string SaltpeterMines        = "Saltpeter Mines (Gunslinger Class)";
    public const string Ryokan               = "Ryokan (Ronin Class)";
    public const string Tavern                = "The Tavern (Bard Class)";
    public const string FlyingDocks           = "The Flying Docks (Pirate Class)";
    public const string AstralGardens         = "The Astral Gardens (Astromancer Class)";
    public const string Aviary                = "The Aviary (Dragon Lancer Class)";
    // Progression & utility
    public const string TrophyRoom            = "Trophy Room (XP Up)";
    public const string Jeweler               = "Jeweler (Ore Gain Up)";
    public const string BuriedTomb            = "Buried Tomb (Red Aether Gain Up)";
    public const string Dummy                 = "Dummy (Training Dummy Unlock)";
    public const string MeditationStudies     = "Meditation Studies (Boss Health/Mana Restore)";
    public const string SageTotem             = "Sage Totem (Mastery Rank Unlock)";
    public const string ArchaeologyCamp       = "Archaeology Camp (Relic Resolve Cost Down)";
    public const string MedievalForgery       = "Medieval Forgery (Relic Reroll Up)";
    public const string AlchemyLab            = "Alchemy Lab (Potion Recharge Talent)";
    public const string Psychiatrist          = "Psychiatrist (Resolve Up)";
    public const string JoustingStudies       = "Jousting Studies (Dash Damage Reduction)";
    public const string CharityDungeon        = "Charity Dungeon (Charon Donation Bonus Unlock)";
    public const string DicersDen             = "The Dicer's Den (Weapon Crit Chance Up)";
    public const string QuantumObservatory    = "The Quantum Observatory (Magic Crit Chance Up)";
    public const string BizarreBazaar         = "The Bizarre Bazaar (Reroll Relic Room Cap Up)";
    public const string ScrewDistillery       = "Screw Distillery (Architect Unlock)";
    // NPC unlocks
    public const string OffshoreBankAccount   = "Offshore Bank Account (Living Safe)";
    public const string Foundation            = "Foundation (Smithy Unlock)";
    public const string EnchantressQuarters   = "Enchantress' Quarters (Enchantress Unlock)";

    /// <summary>
    /// Ordered display names for all manor upgrade slots, parallel to
    /// <see cref="ItemRegistry.s_skillTreeTypes"/>. Indices 0-68 are core upgrades;
    /// 69-71 are NPC unlock slots.
    /// </summary>
    public static readonly string[] ManorUpgradeNames =
    [
        MessHall, FruitJuiceBar, MeteoraGym,
        InstituteOfGastronomy,
        Arsenal, Sauna, RockClimbingWall,
        BambooGarden,
        Gym, YogaClass, FlowerShop,
        Laundromat,
        StudyHall, MathClub, University,
        Library, HallOfWisdom, CourtOfTheWise,
        Lodge,
        FashionChambers, Tailors, Artisan,
        EtchingChambers, PillowMill, BedMill,
        Foundry, BlastFurnace, SomeKindOfKiln,
        UniversalHealthStair, RepurposedMiningShaft,
        GeologistsCamp, DowsingCenter, MassiveVault,
        CareerCenter, AerobicsClassroom,
        Courthouse, ScribesOffice,
        FightingRing, DanceHall, GuildOfDarkArts,
        DrillStore, AdoptionCenter, Kitchen,
        ButchersShoppe, Academy, ArcheryRange, SandPits,
        SaltpeterMines, Ryokan, Tavern, FlyingDocks, AstralGardens,
        Aviary,
        TrophyRoom, Jeweler, BuriedTomb,
        Dummy, MeditationStudies, SageTotem,
        ArchaeologyCamp, MedievalForgery, AlchemyLab,
        Psychiatrist, JoustingStudies, CharityDungeon,
        DicersDen, QuantumObservatory, BizarreBazaar,
        ScrewDistillery,
        // NPC unlock slots (indices 69-71)
        OffshoreBankAccount, Foundation, EnchantressQuarters,
    ];
}
