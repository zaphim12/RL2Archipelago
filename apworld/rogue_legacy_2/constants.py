# String constants shared across multiple Python files.
# These must stay in lockstep with GameConstants.cs in the C# mod.

from typing import NamedTuple

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

class ManorSlot(NamedTuple):
    item_name: str      # AP item display name  (e.g. "Vitality Up I")
    location_name: str  # In-game building name (e.g. "Mess Hall")
    max_level: int      # Max purchasable levels; single-level slots are 1
    depth: int          # Manor tree depth; drives gold cost formula in __init__.py

_MANOR_SLOTS: list[ManorSlot] = [
    # ── Vitality ─────────────────────────────────────────────────────────────
    ManorSlot("Vitality Up I",               "Mess Hall",                 10,  3),  # 0  Health_Up
    ManorSlot("Vitality Up II",              "Fruit Juice Bar",           20,  5),  # 1  Health_Up2
    ManorSlot("Vitality Up III",             "Meteora Gym",               30,  7),  # 2  Health_Up3
    # ── Strength ─────────────────────────────────────────────────────────────
    ManorSlot("Strength Up I",               "Arsenal",                   10,  5),  # 3  Attack_Up
    ManorSlot("Strength Up II",              "Sauna",                     20,  6),  # 4  Attack_Up2
    ManorSlot("Strength Up III",             "Rock Climbing Wall",        30,  9),  # 5  Attack_Up3
    # ── Dexterity ────────────────────────────────────────────────────────────
    ManorSlot("Dexterity Up I",              "Gym",                       10,  7),  # 6  Dexterity_Add1
    ManorSlot("Dexterity Up II",             "Yoga Class",                25,  9),  # 7  Dexterity_Add2
    ManorSlot("Dexterity Up III",            "Flower Shop",               35, 11),  # 8  Dexterity_Add3
    # ── Weapon crit ──────────────────────────────────────────────────────────
    ManorSlot("Weapon Crit Damage Up",       "The Laundromat",            10, 10),  # 9  Crit_Damage_Up
    ManorSlot("Weapon Crit Chance Up",       "The Dicer's Den",           20, 10),  # 10 Crit_Chance_Flat_Up    TODO: technically max is 0 without Absolute Strength
    # ── Intelligence ─────────────────────────────────────────────────────────
    ManorSlot("Intelligence Up I",           "Study Hall",                10,  5),  # 11 Magic_Attack_Up
    ManorSlot("Intelligence Up II",          "Math Club",                 20,  8),  # 12 Magic_Attack_Up2
    ManorSlot("Intelligence Up III",         "University",                30, 10),  # 13 Magic_Attack_Up3
    # ── Focus ────────────────────────────────────────────────────────────────
    ManorSlot("Focus Up I",                  "Library",                   10,  7),  # 14 Focus_Up1
    ManorSlot("Focus Up II",                 "Hall of Wisdom",            25,  9),  # 15 Focus_Up2
    ManorSlot("Focus Up III",                "Court of the Wise",         25, 11),  # 16 Focus_Up3
    # ── Magic crit ───────────────────────────────────────────────────────────
    ManorSlot("Magic Crit Damage Up",        "The Lodge",                 10, 10),  # 17 Magic_Crit_Damage_Up
    ManorSlot("Magic Crit Chance Up",        "The Quantum Observatory",   20, 11),  # 18 Magic_Crit_Chance_Flat_Up  TODO: technically max is 0 without Infinite Knowledge
    # ── Equipment weight ─────────────────────────────────────────────────────
    ManorSlot("Max Weight Up I",             "Fashion Chambers",           5,  4),  # 19 Equip_Up
    ManorSlot("Max Weight Up II",            "Tailors",                   15,  6),  # 20 Equip_Up2
    ManorSlot("Max Weight Up III",           "Artisan",                   25,  7),  # 21 Equip_Up3
    # ── Rune weight ──────────────────────────────────────────────────────────
    ManorSlot("Max Rune Weight Up I",        "Etching Chambers",           5,  4),  # 22 Rune_Equip_Up
    ManorSlot("Max Rune Weight Up II",       "Pillow Mill",               15,  6),  # 23 Rune_Equip_Up2
    ManorSlot("Max Rune Weight Up III",      "Bed Mill",                  15,  7),  # 24 Rune_Equip_Up3
    # ── Armor ────────────────────────────────────────────────────────────────
    ManorSlot("Armor Up I",                  "Foundry",                   15,  6),  # 25 Armor_Up
    ManorSlot("Armor Up II",                 "Blast Furnace",             25,  8),  # 26 Armor_Up2
    ManorSlot("Armor Up III",                "Some Kind of Kiln",         35, 10),  # 27 Armor_Up3
    # ── Health restore ───────────────────────────────────────────────────────
    ManorSlot("Health Drop Scaling",         "Institute of Gastronomy",   10,  8),  # 28 Potion_Up           TODO: technically max is 0 without Unbreakable Will
    ManorSlot("Boss Health/Mana Restore",    "Meditation Studies",         5,  5),  # 29 Boss_Health_Restore
    # ── Spin kicks ───────────────────────────────────────────────────────────
    ManorSlot("Spin Kick scales with INT",   "Bamboo Garden",              5,  8),  # 30 Down_Strike_Up
    # ── Gold & economy ───────────────────────────────────────────────────────
    ManorSlot("Traits Give Gold",            "Universal Health Stair",     1,  1),  # 31 Traits_Give_Gold
    ManorSlot("Traits Gold Gain Up",         "Repurposed Mining Shaft",   10,  3),  # 32 Traits_Give_Gold_Gain_Mod
    ManorSlot("Ore Drop Chance Up",          "Geologist's Camp",           5,  6),  # 33 Equipment_Ore_Find_Up
    ManorSlot("Red Aether Drop Chance Up",   "Dowsing Center",             5,  8),  # 34 Rune_Ore_Find_Up
    ManorSlot("Gold Gain Up I",              "Massive Vault",             20, 10),  # 35 Gold_Gain_Up           TODO: technically max is 0 without Unbreakable Will
    ManorSlot("Ore Gain Up",                 "Jeweler",                   20, 11),  # 36 Equipment_Ore_Gain_Up  TODO: technically max is 0 without Absolute Strength
    ManorSlot("Red Aether Gain Up",          "Buried Tomb",               20,  9),  # 37 Rune_Ore_Gain_Up       TODO: technically max is 0 without Infinite Knowledge
    # ── Heirs ────────────────────────────────────────────────────────────────
    ManorSlot("Re-Roll Children",            "Career Center",              5,  5),  # 38 Randomize_Children
    ManorSlot("More Heirs",                  "Adoption Center",            1, 10),  # 39 More_Children          TODO: technically max is 0 without Infinite Knowledge
    # ── Living safe ──────────────────────────────────────────────────────────
    ManorSlot("Living Safe Max Gold Up",     "Courthouse",                20,  4),  # 40 Gold_Saved_Cap_Up
    ManorSlot("Living Safe Conversion Up",   "Scribe's Office",           10,  5),  # 41 Gold_Saved_Amount_Saved
    # ── Architect ────────────────────────────────────────────────────────────
    ManorSlot("Architect Unlock",            "Screw Distillery",           1,  5),  # 42 Architect
    ManorSlot("Architect Cost Reduction",    "Drill Store",                5,  6),  # 43 Architect_Cost_Down
    # ── Class unlocks ────────────────────────────────────────────────────────
    ManorSlot("Boxer Class",                 "Fighting Ring",              1,  6),  # 44 BoxingGlove_Class_Unlock
    ManorSlot("Duelist Class",               "Dance Hall",                 1,  6),  # 45 Saber_Class_Unlock
    ManorSlot("Assassin Class",              "Guild of Dark Arts",         1,  8),  # 46 DualBlades_Class_Unlock
    ManorSlot("Chef Class",                  "The Kitchen",                1,  6),  # 47 Ladle_Class_Unlock
    ManorSlot("Barbarian Class",             "Butcher's Shoppe",           1,  4),  # 48 Axe_Class_Unlock
    ManorSlot("Mage Class",                  "Academy",                    1,  4),  # 49 Wand_Class_Unlock
    ManorSlot("Ranger Class",                "Archery Range",              1,  2),  # 50 Bow_Class_Unlock
    ManorSlot("Valkyrie Class",              "Sand Pits",                  1,  4),  # 51 Spear_Class_Unlock
    ManorSlot("Gunslinger Class",            "Saltpeter Mines",            1,  8),  # 52 Gun_Class_Unlock
    ManorSlot("Ronin Class",                 "Ryokan",                     1,  8),  # 53 Samurai_Class_Unlock
    ManorSlot("Bard Class",                  "The Tavern",                 1,  7),  # 54 Music_Class_Unlock
    ManorSlot("Pirate Class",                "The Flying Docks",           1,  9),  # 55 Pirate_Class_Unlock
    ManorSlot("Astromancer Class",           "The Astral Gardens",         1,  9),  # 56 Astro_Class_Unlock
    ManorSlot("Dragon Lancer Class",         "The Aviary",                 1,  7),  # 57 Lancer_Class_Unlock
    # ── Relics ───────────────────────────────────────────────────────────────
    ManorSlot("Relic Resolve Cost Down",     "Archaeology Camp",           5,  7),  # 58 Relic_Cost_Down
    ManorSlot("Relic Reroll Up",             "Medieval Forgery",          10,  8),  # 59 Reroll_Relic
    ManorSlot("Reroll Relic Room Cap Up",    "The Bizarre Bazaar",         2,  9),  # 60 Reroll_Relic_Room_Cap  TODO: technically max is 0 without Master Smith
    # ── Progression & utility ────────────────────────────────────────────────
    ManorSlot("Encumbrance Limit Up",        "Aerobics Classroom",        10,  5),  # 61 Weight_CD_Reduce
    ManorSlot("XP Up",                       "Trophy Room",               10,  5),  # 62 XP_Up
    ManorSlot("Resolve Up",                  "Psychiatrist",              20,  6),  # 63 Resolve_Up
    ManorSlot("Training Dummy Unlock",       "Dummy",                      1,  4),  # 64 Unlock_Dummy
    ManorSlot("Mastery Rank Unlock",         "Sage Totem",                 1,  4),  # 65 Unlock_Totem
    ManorSlot("Potion Recharge Talent",      "Alchemy Lab",                1,  7),  # 66 Potion_Recharge_Talent
    ManorSlot("Dash Damage Reduction",       "Jousting Studies",           5,  7),  # 67 Dash_Strike_Up
    ManorSlot("Charon Donation Bonus Unlock","Charity Dungeon",            1,  3),  # 68 Charon_Gold_Stat_Bonus
    # ── NPC unlocks ──────────────────────────────────────────────────────────
    ManorSlot("Living Safe",                 "Offshore Bank Account",      1,  2),  # 69 Gold_Saved_Unlock
    ManorSlot("Blacksmith Unlock",           "Foundation",                 1,  3),  # 70 Blacksmith
    ManorSlot("Enchantress Unlock",          "Enchantress' Quarters",      1,  3),  # 71 Enchantress
]

_CORE_COUNT = 69

CORE_MANOR_UPGRADES:       list[str] = [f"Upgrade: {s.item_name}"    for s in _MANOR_SLOTS[:_CORE_COUNT]]
CORE_MANOR_LOCATION_NAMES: list[str] = [f"Manor - {s.location_name}" for s in _MANOR_SLOTS[:_CORE_COUNT]]
NPC_MANOR_UPGRADES:        list[str] = [f"Upgrade: {s.item_name}"    for s in _MANOR_SLOTS[_CORE_COUNT:]]
NPC_MANOR_LOCATION_NAMES:  list[str] = [f"Manor - {s.location_name}" for s in _MANOR_SLOTS[_CORE_COUNT:]]

# Keyed by "Upgrade: {item_name}" — used by create_items() to compute how many
# AP items to generate per slot. Single-level slots are set to 1.
MANOR_MAX_LEVELS: dict[str, int] = {f"Upgrade: {s.item_name}": s.max_level for s in _MANOR_SLOTS}

# Keyed by "Upgrade: {item_name}" — depth in the manor unlock tree, used by
# _compute_manor_upgrade_costs() to scale gold costs. Defaults to 1 if absent.
MANOR_UPGRADE_DEPTHS: dict[str, int] = {f"Upgrade: {s.item_name}": s.depth for s in _MANOR_SLOTS}
