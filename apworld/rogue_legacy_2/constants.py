# String constants shared across multiple Python files.
# These must stay in lockstep with GameConstants.cs in the C# mod.

# ---------------------------------------------------------------------------
# Biomes
# ---------------------------------------------------------------------------

BIOME_CITADEL_AGARTHA   = "Citadel Agartha"
BIOME_AXIS_MUNDI        = "Axis Mundi"
BIOME_KERGUELEN_PLATEAU = "Kerguelen Plateau"
BIOME_STYGIAN_STUDY     = "Stygian Study"
BIOME_SUN_TOWER         = "Sun Tower"
BIOME_PISHON_DRY_LAKE   = "Pishon Dry Lake"

# Ordered to match biomeIndex used in location ID calculations (Castle=0 … Cave=5).
BIOME_NAMES: list[str] = [
    BIOME_CITADEL_AGARTHA,
    BIOME_AXIS_MUNDI,
    BIOME_KERGUELEN_PLATEAU,
    BIOME_STYGIAN_STUDY,
    BIOME_SUN_TOWER,
    BIOME_PISHON_DRY_LAKE,
]

# ---------------------------------------------------------------------------
# Heirlooms
# ---------------------------------------------------------------------------

HEIRLOOM_ANANKES_SHAWL       = "Ananke's Shawl"
HEIRLOOM_AETHERS_WINGS       = "Aether's Wings"
HEIRLOOM_AESOPS_TOME         = "Aesop's Tome"
HEIRLOOM_ECHOS_BOOTS         = "Echo's Boots"
HEIRLOOM_PALLAS_VOID_BELL    = "Pallas' Void Bell"
HEIRLOOM_THEIAS_SUN_LANTERN  = "Theia's Sun Lantern"

HEIRLOOM_NAMES: list[str] = [
    HEIRLOOM_ANANKES_SHAWL,
    HEIRLOOM_AETHERS_WINGS,
    HEIRLOOM_AESOPS_TOME,
    HEIRLOOM_ECHOS_BOOTS,
    HEIRLOOM_PALLAS_VOID_BELL,
    HEIRLOOM_THEIAS_SUN_LANTERN,
]

# ---------------------------------------------------------------------------
# Teleporters
# ---------------------------------------------------------------------------

TELEPORTER_AXIS_MUNDI        = "Axis Mundi Teleporter"
TELEPORTER_KERGUELEN_PLATEAU = "Kerguelen Plateau Teleporter"
TELEPORTER_STYGIAN_STUDY     = "Stygian Study Teleporter"
TELEPORTER_SUN_TOWER         = "Sun Tower Teleporter"
TELEPORTER_PISHON_DRY_LAKE   = "Pishon Dry Lake Teleporter"

# Ordered to match ItemRegistry.TELEPORTER_OFFSET indices in C#.
TELEPORTER_NAMES: list[str] = [
    TELEPORTER_AXIS_MUNDI,
    TELEPORTER_KERGUELEN_PLATEAU,
    TELEPORTER_STYGIAN_STUDY,
    TELEPORTER_SUN_TOWER,
    TELEPORTER_PISHON_DRY_LAKE,
]

# ---------------------------------------------------------------------------
# Runes  (ordered to match ItemRegistry.ToRuneType() index mapping in C#)
# ---------------------------------------------------------------------------

RUNE_NAMES: list[str] = [
    "Reinforced",   "Dash",        "Vault",    "Bounty",
    "Haste",        "Lifesteal",   "Magnesis", "Retaliation",
    "Siphon",       "Capacity",    "Trick",    "Amplification",
    "Soulsteal",    "Resolve",     "Stone",    "Red",
    "Sharpened",    "Focal",       "Might",    "Eldar",
    "Lucky Roller", "High Stakes", "Folded",   "Quenching",
]

# ---------------------------------------------------------------------------
# Blueprints
# ID = BASE_ID + BLUEPRINT_OFFSET + categoryIndex * 16 + typeIndex
# ---------------------------------------------------------------------------

BLUEPRINT_CATEGORIES: list[str] = ["Weapon", "Helm", "Chest", "Cape", "Trinket"]
BLUEPRINT_TYPES: list[str] = [
    "Leather",    "Scholar",   "Warden",  "Sanguine",
    "Ammonite",   "Crescent",  "Drowned", "Gilded",
    "Obsidian",   "Leviathan", "Kin",
    "White Wood", "Black Root",
]

# ---------------------------------------------------------------------------
# Manor upgrades  (ordered to match ItemRegistry.s_skillTreeTypes in C#)
# Indices 0-68 are core upgrades; 69-71 are NPC unlock slots.
# ---------------------------------------------------------------------------

# ── Vitality ─────────────────────────────────────────────────────────────────
MESS_HALL               = "Mess Hall (Vitality Up I)"                      # 0  Health_Up
FRUIT_JUICE_BAR         = "Fruit Juice Bar (Vitality Up II)"               # 1  Health_Up2
METEORA_GYM             = "Meteora Gym (Vitality Up III)"                  # 2  Health_Up3
# ── Misc survival ────────────────────────────────────────────────────────────
INSTITUTE_OF_GASTRONOMY = "Institute of Gastronomy (Health Drop Scaling)"  # 3  Potion_Up
# ── Strength ─────────────────────────────────────────────────────────────────
ARSENAL                 = "Arsenal (Strength Up I)"                        # 4  Attack_Up
SAUNA                   = "Sauna (Strength Up II)"                         # 5  Attack_Up2
ROCK_CLIMBING_WALL      = "Rock Climbing Wall (Strength Up III)"           # 6  Attack_Up3
# ── Spin kicks ───────────────────────────────────────────────────────────────
BAMBOO_GARDEN           = "Bamboo Garden (Spin Kick scales with INT)"      # 7  Down_Strike_Up
# ── Dexterity ────────────────────────────────────────────────────────────────
GYM                     = "Gym (Dexterity Up I)"                           # 8  Dexterity_Add1
YOGA_CLASS              = "Yoga Class (Dexterity Up II)"                   # 9  Dexterity_Add2
FLOWER_SHOP             = "Flower Shop (Dexterity Up III)"                 # 10 Dexterity_Add3
# ── Weapon crit damage ───────────────────────────────────────────────────────
LAUNDROMAT              = "The Laundromat (Weapon Crit Damage Up)"         # 11 Crit_Damage_Up
# ── Intelligence ─────────────────────────────────────────────────────────────
STUDY_HALL              = "Study Hall (Intelligence Up I)"                 # 12 Magic_Attack_Up
MATH_CLUB               = "Math Club (Intelligence Up II)"                 # 13 Magic_Attack_Up2
UNIVERSITY              = "University (Intelligence Up III)"               # 14 Magic_Attack_Up3
# ── Focus ────────────────────────────────────────────────────────────────────
LIBRARY                 = "Library (Focus Up I)"                           # 15 Focus_Up1
HALL_OF_WISDOM          = "Hall of Wisdom (Focus Up II)"                   # 16 Focus_Up2
COURT_OF_THE_WISE       = "Court of the Wise (Focus Up III)"               # 17 Focus_Up3
# ── Magic crit damage ────────────────────────────────────────────────────────
LODGE                   = "The Lodge (Magic Crit Damage Up)"               # 18 Magic_Crit_Damage_Up
# ── Equipment weight ─────────────────────────────────────────────────────────
FASHION_CHAMBERS        = "Fashion Chambers (Max Weight Up I)"             # 19 Equip_Up
TAILORS                 = "Tailors (Max Weight Up II)"                     # 20 Equip_Up2
ARTISAN                 = "Artisan (Max Weight Up III)"                    # 21 Equip_Up3
# ── Rune weight ──────────────────────────────────────────────────────────────
ETCHING_CHAMBERS        = "Etching Chambers (Max Rune Weight Up I)"        # 22 Rune_Equip_Up
PILLOW_MILL             = "Pillow Mill (Max Rune Weight Up II)"            # 23 Rune_Equip_Up2
BED_MILL                = "Bed Mill (Max Rune Weight Up III)"              # 24 Rune_Equip_Up3
# ── Armor ────────────────────────────────────────────────────────────────────
FOUNDRY                 = "Foundry (Armor Up I)"                           # 25 Armor_Up
BLAST_FURNACE           = "Blast Furnace (Armor Up II)"                    # 26 Armor_Up2
SOME_KIND_OF_KILN       = "Some Kind of Kiln (Armor Up III)"               # 27 Armor_Up3
# ── Gold & economy ───────────────────────────────────────────────────────────
UNIVERSAL_HEALTH_STAIR  = "Universal Health Stair (Traits Give Gold)"      # 28 Traits_Give_Gold
REPURPOSED_MINING_SHAFT = "Repurposed Mining Shaft (Traits Gold Gain Up)"  # 29 Traits_Give_Gold_Gain_Mod
GEOLOGISTS_CAMP         = "Geologist's Camp (Ore Drop Chance Up)"          # 30 Equipment_Ore_Find_Up
DOWSING_CENTER          = "Dowsing Center (Red Aether Drop Chance Up)"     # 31 Rune_Ore_Find_Up
MASSIVE_VAULT           = "Massive Vault (Gold Gain Up I)"                 # 32 Gold_Gain_Up
# ── Rerolls ──────────────────────────────────────────────────────────────────
CAREER_CENTER           = "Career Center (Re-Roll Children)"               # 33 Randomize_Children
AEROBICS_CLASSROOM      = "Aerobics Classroom (Encumbrance Limit Up)"      # 34 Weight_CD_Reduce
# ── Living safe ──────────────────────────────────────────────────────────────
COURTHOUSE              = "Courthouse (Living Safe Max Gold Up)"           # 35 Gold_Saved_Cap_Up
SCRIBES_OFFICE          = "Scribe's Office (Living Safe Conversion Up)"    # 36 Gold_Saved_Amount_Saved
# ── Class unlocks ────────────────────────────────────────────────────────────
FIGHTING_RING           = "Fighting Ring (Boxer Class)"                    # 37 BoxingGlove_Class_Unlock
DANCE_HALL              = "Dance Hall (Duelist Class)"                     # 38 Saber_Class_Unlock
GUILD_OF_DARK_ARTS      = "Guild of Dark Arts (Assassin Class)"            # 39 DualBlades_Class_Unlock
DRILL_STORE             = "Drill Store (Architect Cost Reduction)"         # 40 Architect_Cost_Down
ADOPTION_CENTER         = "Adoption Center (More Heirs)"                   # 41 More_Children
KITCHEN                 = "The Kitchen (Chef Class)"                       # 42 Ladle_Class_Unlock
BUTCHERS_SHOPPE         = "Butcher's Shoppe (Barbarian Class)"             # 43 Axe_Class_Unlock
ACADEMY                 = "Academy (Mage Class)"                           # 44 Wand_Class_Unlock
ARCHERY_RANGE           = "Archery Range (Ranger Class)"                   # 45 Bow_Class_Unlock
SAND_PITS               = "Sand Pits (Valkyrie Class)"                     # 46 Spear_Class_Unlock
SALTPETER_MINES         = "Saltpeter Mines (Gunslinger Class)"             # 47 Gun_Class_Unlock
RYOKAN                  = "Ryokan (Ronin Class)"                           # 48 Samurai_Class_Unlock
TAVERN                  = "The Tavern (Bard Class)"                        # 49 Music_Class_Unlock
FLYING_DOCKS            = "The Flying Docks (Pirate Class)"                # 50 Pirate_Class_Unlock
ASTRAL_GARDENS          = "The Astral Gardens (Astromancer Class)"         # 51 Astro_Class_Unlock
AVIARY                  = "The Aviary (Dragon Lancer Class)"               # 52 Lancer_Class_Unlock
# ── Progression & utility ────────────────────────────────────────────────────
TROPHY_ROOM             = "Trophy Room (XP Up)"                            # 53 XP_Up
JEWELER                 = "Jeweler (Ore Gain Up)"                          # 54 Equipment_Ore_Gain_Up
BURIED_TOMB             = "Buried Tomb (Red Aether Gain Up)"               # 55 Rune_Ore_Gain_Up
DUMMY                   = "Dummy (Training Dummy Unlock)"                  # 56 Unlock_Dummy
MEDITATION_STUDIES      = "Meditation Studies (Boss Health/Mana Restore)"  # 57 Boss_Health_Restore
SAGE_TOTEM              = "Sage Totem (Mastery Rank Unlock)"               # 58 Unlock_Totem
ARCHAEOLOGY_CAMP        = "Archaeology Camp (Relic Resolve Cost Down)"     # 59 Relic_Cost_Down
MEDIEVAL_FORGERY        = "Medieval Forgery (Relic Reroll Up)"             # 60 Reroll_Relic
ALCHEMY_LAB             = "Alchemy Lab (Potion Recharge Talent)"           # 61 Potion_Recharge_Talent
PSYCHIATRIST            = "Psychiatrist (Resolve Up)"                      # 62 Resolve_Up
JOUSTING_STUDIES        = "Jousting Studies (Dash Damage Reduction)"       # 63 Dash_Strike_Up
CHARITY_DUNGEON         = "Charity Dungeon (Charon Donation Bonus Unlock)" # 64 Charon_Gold_Stat_Bonus
DICERS_DEN              = "The Dicer's Den (Weapon Crit Chance Up)"        # 65 Crit_Chance_Flat_Up
QUANTUM_OBSERVATORY     = "The Quantum Observatory (Magic Crit Chance Up)" # 66 Magic_Crit_Chance_Flat_Up
BIZARRE_BAZAAR          = "The Bizarre Bazaar (Reroll Relic Room Cap Up)"  # 67 Reroll_Relic_Room_Cap
SCREW_DISTILLERY        = "Screw Distillery (Architect Unlock)"            # 68 Architect
# ── NPC unlocks ──────────────────────────────────────────────────────────────
OFFSHORE_BANK_ACCOUNT   = "Offshore Bank Account (Living Safe)"            # 69 Gold_Saved_Unlock
FOUNDATION              = "Foundation (Smithy Unlock)"                     # 70 Smithy
ENCHANTRESS_QUARTERS    = "Enchantress' Quarters (Enchantress Unlock)"     # 71 Enchantress

CORE_MANOR_UPGRADES: list[str] = [
    MESS_HALL, FRUIT_JUICE_BAR, METEORA_GYM,
    # "Veterinarian Clinic (Revive Chance Up)",
    INSTITUTE_OF_GASTRONOMY,
    # "Stadium (Invulnerability Up)",
    ARSENAL, SAUNA, ROCK_CLIMBING_WALL,
    BAMBOO_GARDEN,
    GYM, YOGA_CLASS, FLOWER_SHOP,
    LAUNDROMAT,
    STUDY_HALL, MATH_CLUB, UNIVERSITY,
    LIBRARY, HALL_OF_WISDOM, COURT_OF_THE_WISE,
    LODGE,
    # "The Thaumaturgy (Cooldown Reduction)",
    FASHION_CHAMBERS, TAILORS, ARTISAN,
    ETCHING_CHAMBERS, PILLOW_MILL, BED_MILL,
    FOUNDRY, BLAST_FURNACE, SOME_KIND_OF_KILN,
    UNIVERSAL_HEALTH_STAIR, REPURPOSED_MINING_SHAFT,
    GEOLOGISTS_CAMP, DOWSING_CENTER, MASSIVE_VAULT,
    # "Gold Gain Up II", "Gold Gain Up III", "Sky Bridge", "Tree Bridge",
    CAREER_CENTER, AEROBICS_CLASSROOM,
    # "The Fissary (Mana Cost Down)",
    COURTHOUSE, SCRIBES_OFFICE,
    FIGHTING_RING, DANCE_HALL, GUILD_OF_DARK_ARTS,
    DRILL_STORE,
    # "Genesis Pool (Polymorph Class)",
    ADOPTION_CENTER, KITCHEN,
    # "Forest Village (Chakram Class)", "Martial Arts School (Tonfa Class)", "The Pilgrim's Steps (Knight Class)",
    BUTCHERS_SHOPPE, ACADEMY, ARCHERY_RANGE, SAND_PITS,
    # "Hidden Dojo (Ninja Class)", "Ancestral Plot (Lich Class)", "Miner's Camp (Spelunker Class)",
    SALTPETER_MINES, RYOKAN, TAVERN, FLYING_DOCKS, ASTRAL_GARDENS,
    # "Weapon Master Upgrade", "Knight Upgrade",
    AVIARY,
    TROPHY_ROOM, JEWELER, BURIED_TOMB,
    # "Herb Garden (Free Cast Up)",
    DUMMY, MEDITATION_STUDIES, SAGE_TOTEM,
    ARCHAEOLOGY_CAMP, MEDIEVAL_FORGERY, ALCHEMY_LAB,
    PSYCHIATRIST, JOUSTING_STUDIES, CHARITY_DUNGEON,
    DICERS_DEN, QUANTUM_OBSERVATORY, BIZARRE_BAZAAR,
    SCREW_DISTILLERY,
]

NPC_MANOR_UPGRADES: list[str] = [
    OFFSHORE_BANK_ACCOUNT,
    FOUNDATION,
    ENCHANTRESS_QUARTERS,
    # "Banker Unlock",
]

# Max level for each manor upgrade slot, keyed by "Manor: {name}".
# Used by create_items() to compute how many AP items to generate per slot.
# Single-level slots (class/NPC unlocks, binary toggles) are set to 1.
MANOR_MAX_LEVELS: dict[str, int] = {
    # ── Vitality ─────────────────────────────────────────────────────────────
    f"Manor: {MESS_HALL}":               10,
    f"Manor: {FRUIT_JUICE_BAR}":         20,
    f"Manor: {METEORA_GYM}":             30,
    # ── Misc survival ────────────────────────────────────────────────────────
    f"Manor: {INSTITUTE_OF_GASTRONOMY}": 10, # TODO technically max is 0 without Unbreakable Will
    # ── Strength ─────────────────────────────────────────────────────────────
    f"Manor: {ARSENAL}":                 10,
    f"Manor: {SAUNA}":                   20,
    f"Manor: {ROCK_CLIMBING_WALL}":      30,
    # ── Spin Kicks ───────────────────────────────────────────────────────────
    f"Manor: {BAMBOO_GARDEN}":           5,
    # ── Dexterity ────────────────────────────────────────────────────────────
    f"Manor: {GYM}":                     10,
    f"Manor: {YOGA_CLASS}":              25,
    f"Manor: {FLOWER_SHOP}":             35,
    # ── Weapon Crit Damage ───────────────────────────────────────────────────
    f"Manor: {LAUNDROMAT}":              10,
    # ── Intelligence ─────────────────────────────────────────────────────────
    f"Manor: {STUDY_HALL}":              10,
    f"Manor: {MATH_CLUB}":               20,
    f"Manor: {UNIVERSITY}":              30,
    # ── Focus ────────────────────────────────────────────────────────────────
    f"Manor: {LIBRARY}":                 10,
    f"Manor: {HALL_OF_WISDOM}":          25,
    f"Manor: {COURT_OF_THE_WISE}":       25,
    # ── Spell Crit Damage ────────────────────────────────────────────────────
    f"Manor: {LODGE}":                   10,
    # ── Equipment weight ─────────────────────────────────────────────────────
    f"Manor: {FASHION_CHAMBERS}":        5,
    f"Manor: {TAILORS}":                 15,
    f"Manor: {ARTISAN}":                 25,
    # ── Rune weight ──────────────────────────────────────────────────────────
    f"Manor: {ETCHING_CHAMBERS}":        5,
    f"Manor: {PILLOW_MILL}":             15,
    f"Manor: {BED_MILL}":                15,
    # ── Armor ────────────────────────────────────────────────────────────────
    f"Manor: {FOUNDRY}":                 15,
    f"Manor: {BLAST_FURNACE}":           25,
    f"Manor: {SOME_KIND_OF_KILN}":       35,
    # ── Gold & economy ───────────────────────────────────────────────────────
    f"Manor: {UNIVERSAL_HEALTH_STAIR}":  1,
    f"Manor: {REPURPOSED_MINING_SHAFT}": 10,
    f"Manor: {GEOLOGISTS_CAMP}":         5,
    f"Manor: {DOWSING_CENTER}":          5,
    f"Manor: {MASSIVE_VAULT}":           20, # TODO technically max is 0 without Unbreakable Will
    # ── Rerolls ──────────────────────────────────────────────────────────────
    f"Manor: {CAREER_CENTER}":           5,
    f"Manor: {ADOPTION_CENTER}":         1,  # TODO technically max is 0 without Infinite Knowledge
    # ── Encumbrance ──────────────────────────────────────────────────────────
    f"Manor: {AEROBICS_CLASSROOM}":      10,
    # ── Living safe ──────────────────────────────────────────────────────────
    f"Manor: {COURTHOUSE}":              20,
    f"Manor: {SCRIBES_OFFICE}":          10,
    # ── Architect ────────────────────────────────────────────────────────────
    f"Manor: {DRILL_STORE}":             5,
    # ── Class unlocks ────────────────────────────────────────────────────────
    f"Manor: {FIGHTING_RING}":           1,
    f"Manor: {DANCE_HALL}":              1,
    f"Manor: {GUILD_OF_DARK_ARTS}":      1,
    f"Manor: {KITCHEN}":                 1,
    f"Manor: {BUTCHERS_SHOPPE}":         1,
    f"Manor: {ACADEMY}":                 1,
    f"Manor: {ARCHERY_RANGE}":           1,
    f"Manor: {SAND_PITS}":               1,
    f"Manor: {SALTPETER_MINES}":         1,
    f"Manor: {RYOKAN}":                  1,
    f"Manor: {TAVERN}":                  1,
    f"Manor: {FLYING_DOCKS}":            1,
    f"Manor: {ASTRAL_GARDENS}":          1,
    f"Manor: {AVIARY}":                  1,
    # ── Progression & utility ────────────────────────────────────────────────
    f"Manor: {TROPHY_ROOM}":             10,
    f"Manor: {JEWELER}":                 20, # TODO technically max is 0 without Absolute Strength
    f"Manor: {BURIED_TOMB}":             20, # TODO technically max is 0 without Infinite Knowledge
    f"Manor: {DUMMY}":                   1,
    f"Manor: {MEDITATION_STUDIES}":      5,
    f"Manor: {SAGE_TOTEM}":              1,
    f"Manor: {ARCHAEOLOGY_CAMP}":        5,
    f"Manor: {MEDIEVAL_FORGERY}":        10,
    f"Manor: {ALCHEMY_LAB}":             1,
    f"Manor: {PSYCHIATRIST}":            20,
    f"Manor: {JOUSTING_STUDIES}":        5,
    f"Manor: {CHARITY_DUNGEON}":         1,
    f"Manor: {DICERS_DEN}":              20, # TODO technically max is 0 without Absolute Strength
    f"Manor: {QUANTUM_OBSERVATORY}":     20, # TODO technically max is 0 without Infinite Knowledge
    f"Manor: {BIZARRE_BAZAAR}":          2,  # TODO technically max is 0 without Master Smith
    f"Manor: {SCREW_DISTILLERY}":        1,
    # ── NPC unlocks ──────────────────────────────────────────────────────────
    f"Manor: {OFFSHORE_BANK_ACCOUNT}":   1,
    f"Manor: {FOUNDATION}":              1,
    f"Manor: {ENCHANTRESS_QUARTERS}":    1,
}
