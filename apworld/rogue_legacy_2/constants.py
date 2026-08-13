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
    item_name: str           # AP item display name  (e.g. "Vitality Up I")
    location_name: str       # In-game building name (e.g. "Mess Hall")
    max_level: int           # Max purchasable levels; single-level slots are 1
    depth: int               # Manor tree depth; drives gold cost formula in __init__.py
    is_stat_upgrade: bool    # counts toward the stat-upgrade total for biome gating

_MANOR_SLOTS: list[ManorSlot] = [
    # ── Vitality ─────────────────────────────────────────────────────────────
    ManorSlot("Vitality Up I",               "Red Path Upgrade 3",        10,  3, True),    # 0  Health_Up
    ManorSlot("Vitality Up II",              "Red Path Upgrade 5",        20,  5, True),    # 1  Health_Up2
    ManorSlot("Vitality Up III",             "Red Path Upgrade 7",        30,  7, True),    # 2  Health_Up3
    # ── Strength ─────────────────────────────────────────────────────────────
    ManorSlot("Strength Up I",               "Cyan Path Upgrade 2",       10,  5, True),    # 3  Attack_Up
    ManorSlot("Strength Up II",              "Brown Path Upgrade 1",      20,  6, True),    # 4  Attack_Up2
    ManorSlot("Strength Up III",             "Brown Path Upgrade 4",      30,  9, True),    # 5  Attack_Up3
    # ── Dexterity ────────────────────────────────────────────────────────────
    ManorSlot("Dexterity Up I",              "Cyan Path Upgrade 4",       10,  7, True),    # 6  Dexterity_Add1
    ManorSlot("Dexterity Up II",             "Cyan Path Upgrade 6",       25,  9, True),    # 7  Dexterity_Add2
    ManorSlot("Dexterity Up III",            "Cyan Path Upgrade 8",       35, 11, True),    # 8  Dexterity_Add3
    # ── Weapon crit ──────────────────────────────────────────────────────────
    ManorSlot("Weapon Crit Damage Up",       "Cyan Path Upgrade 7",       10, 10, True),    # 9  Crit_Damage_Up
    ManorSlot("Weapon Crit Chance Up",       "Brown Path Upgrade 5",      20, 10, True),    # 10 Crit_Chance_Flat_Up    TODO: technically max is 0 without Absolute Strength
    # ── Intelligence ─────────────────────────────────────────────────────────
    ManorSlot("Intelligence Up I",           "Purple Path Upgrade 2",     10,  5, True),    # 11 Magic_Attack_Up
    ManorSlot("Intelligence Up II",          "Purple Path Upgrade 5",     20,  8, True),    # 12 Magic_Attack_Up2
    ManorSlot("Intelligence Up III",         "Purple Path Upgrade 7",     30, 10, True),    # 13 Magic_Attack_Up3
    # ── Focus ────────────────────────────────────────────────────────────────
    ManorSlot("Focus Up I",                  "Purple Path Upgrade 4",     10,  7, True),    # 14 Focus_Up1
    ManorSlot("Focus Up II",                 "White Path Upgrade 2",      25,  9, True),    # 15 Focus_Up2
    ManorSlot("Focus Up III",                "White Path Upgrade 4",      25, 11, True),    # 16 Focus_Up3
    # ── Magic crit ───────────────────────────────────────────────────────────
    ManorSlot("Magic Crit Damage Up",        "White Path Upgrade 3",      10, 10, True),    # 17 Magic_Crit_Damage_Up
    ManorSlot("Magic Crit Chance Up",        "Purple Path Upgrade 8",     20, 11, True),    # 18 Magic_Crit_Chance_Flat_Up  TODO: technically max is 0 without Infinite Knowledge
    # ── Equipment weight ─────────────────────────────────────────────────────
    ManorSlot("Max Weight Up I",             "Blue Path Upgrade 2",        5,  4, True),    # 19 Equip_Up
    ManorSlot("Max Weight Up II",            "Blue Path Upgrade 4",       15,  6, True),    # 20 Equip_Up2
    ManorSlot("Max Weight Up III",           "Blue Path Upgrade 5",       25,  7, True),    # 21 Equip_Up3
    # ── Rune weight ──────────────────────────────────────────────────────────
    ManorSlot("Max Rune Weight Up I",        "Pink Path Upgrade 2",        5,  4, True),    # 22 Rune_Equip_Up
    ManorSlot("Max Rune Weight Up II",       "Pink Path Upgrade 4",       15,  6, True),    # 23 Rune_Equip_Up2
    ManorSlot("Max Rune Weight Up III",      "Pink Path Upgrade 5",       15,  7, True),    # 24 Rune_Equip_Up3
    # ── Armor ────────────────────────────────────────────────────────────────
    ManorSlot("Armor Up I",                  "Orange Path Upgrade 2",     15,  6, True),    # 25 Armor_Up
    ManorSlot("Armor Up II",                 "Orange Path Upgrade 4",     25,  8, True),    # 26 Armor_Up2
    ManorSlot("Armor Up III",                "Orange Path Upgrade 6",     35, 10, True),    # 27 Armor_Up3
    # ── Health restore ───────────────────────────────────────────────────────
    ManorSlot("Health Drop Scaling",         "Red Path Upgrade 8",        10,  8, True),    # 28 Potion_Up           TODO: technically max is 0 without Unbreakable Will
    ManorSlot("Boss Health/Mana Restore",    "Orange Path Upgrade 1",      5,  5, True),    # 29 Boss_Health_Restore
    # ── Spin kicks ───────────────────────────────────────────────────────────
    ManorSlot("Spin Kick scales with INT",   "Magenta Path Upgrade 4",     5,  8, True),    # 30 Down_Strike_Up
    # ── Gold & economy ───────────────────────────────────────────────────────
    ManorSlot("Traits Give Gold",            "Red Path Upgrade 1",         1,  1, False),   # 31 Traits_Give_Gold
    ManorSlot("Traits Gold Gain Up",         "Green Path Upgrade 2a",     10,  3, False),   # 32 Traits_Give_Gold_Gain_Mod
    ManorSlot("Ore Drop Chance Up",          "Blue Path Upgrade 4a",       5,  6, False),   # 33 Equipment_Ore_Find_Up
    ManorSlot("Red Aether Drop Chance Up",   "Pink Path Upgrade 6",        5,  8, False),   # 34 Rune_Ore_Find_Up
    ManorSlot("Gold Gain Up I",              "Orange Path Upgrade 6a",    20, 10, False),   # 35 Gold_Gain_Up           TODO: technically max is 0 without Unbreakable Will
    ManorSlot("Ore Gain Up",                 "Orange Path Upgrade 7",     20, 11, False),   # 36 Equipment_Ore_Gain_Up  TODO: technically max is 0 without Absolute Strength
    ManorSlot("Red Aether Gain Up",          "Magenta Path Upgrade 5",    20,  9, False),   # 37 Rune_Ore_Gain_Up       TODO: technically max is 0 without Infinite Knowledge
    # ── Heirs ────────────────────────────────────────────────────────────────
    ManorSlot("Re-Roll Children",            "Pink Path Upgrade 3",        5,  5, False),   # 38 Randomize_Children
    ManorSlot("More Heirs",                  "Purple Path Upgrade 7a",     1, 10, False),   # 39 More_Children          TODO: technically max is 0 without Infinite Knowledge
    # ── Living safe ──────────────────────────────────────────────────────────
    ManorSlot("Living Safe Max Gold Up",     "Green Path Upgrade 3",      20,  4, False),   # 40 Gold_Saved_Cap_Up
    ManorSlot("Living Safe Conversion Up",   "Green Path Upgrade 4",      10,  5, False),   # 41 Gold_Saved_Amount_Saved
    # ── Architect ────────────────────────────────────────────────────────────
    ManorSlot("Architect Unlock",            "Magenta Path Upgrade 1",     1,  5, True),    # 42 Architect
    ManorSlot("Architect Cost Reduction",    "Magenta Path Upgrade 2",     5,  6, False),   # 43 Architect_Cost_Down
    # ── Class unlocks ────────────────────────────────────────────────────────
    ManorSlot("Boxer Class",                 "Red Path Upgrade 6",         1,  6, False),   # 44 BoxingGlove_Class_Unlock
    ManorSlot("Duelist Class",               "Cyan Path Upgrade 3",        1,  6, False),   # 45 Saber_Class_Unlock
    ManorSlot("Assassin Class",              "Cyan Path Upgrade 5",        1,  8, False),   # 46 DualBlades_Class_Unlock
    ManorSlot("Chef Class",                  "Purple Path Upgrade 3",      1,  6, False),   # 47 Ladle_Class_Unlock
    ManorSlot("Barbarian Class",             "Red Path Upgrade 4",         1,  4, False),   # 48 Axe_Class_Unlock
    ManorSlot("Mage Class",                  "Purple Path Upgrade 1",      1,  4, False),   # 49 Wand_Class_Unlock
    ManorSlot("Ranger Class",                "Red Path Upgrade 2",         1,  2, False),   # 50 Bow_Class_Unlock
    ManorSlot("Valkyrie Class",              "Cyan Path Upgrade 1",        1,  4, False),   # 51 Spear_Class_Unlock
    ManorSlot("Gunslinger Class",            "White Path Upgrade 1",       1,  8, False),   # 52 Gun_Class_Unlock
    ManorSlot("Ronin Class",                 "Brown Path Upgrade 3",       1,  8, False),   # 53 Samurai_Class_Unlock
    ManorSlot("Bard Class",                  "Magenta Path Upgrade 3",     1,  7, False),   # 54 Music_Class_Unlock
    ManorSlot("Pirate Class",                "Orange Path Upgrade 5",      1,  9, False),   # 55 Pirate_Class_Unlock
    ManorSlot("Astromancer Class",           "Purple Path Upgrade 6",      1,  9, False),   # 56 Astro_Class_Unlock
    ManorSlot("Dragon Lancer Class",         "Orange Path Upgrade 3",      1,  7, False),   # 57 Lancer_Class_Unlock
    # ── Relics ───────────────────────────────────────────────────────────────
    ManorSlot("Relic Resolve Cost Down",     "Yellow Path Upgrade 2",      5,  7, True),    # 58 Relic_Cost_Down
    ManorSlot("Relic Reroll Up",             "Yellow Path Upgrade 3",     10,  8, False),   # 59 Reroll_Relic
    ManorSlot("Reroll Relic Room Cap Up",    "Yellow Path Upgrade 4",      2,  9, False),   # 60 Reroll_Relic_Room_Cap  TODO: technically max is 0 without Master Smith
    # ── Progression & utility ────────────────────────────────────────────────
    ManorSlot("Encumbrance Limit Up",        "Blue Path Upgrade 3",       10,  5, True),    # 61 Weight_CD_Reduce
    ManorSlot("XP Up",                       "Yellow Path Upgrade 3",     10,  5, False),   # 62 XP_Up
    ManorSlot("Resolve Up",                  "Orange Path Upgrade 1",     20,  6, True),    # 63 Resolve_Up
    ManorSlot("Training Dummy Unlock",       "Blue Path Upgrade 2a",       1,  4, False),   # 64 Unlock_Dummy
    ManorSlot("Mastery Rank Unlock",         "Yellow Path Upgrade 2",      1,  4, True),    # 65 Unlock_Totem
    ManorSlot("Potion Recharge Talent",      "Purple Path Upgrade 4a",     1,  7, False),   # 66 Potion_Recharge_Talent
    ManorSlot("Dash Damage Reduction",       "Brown Path Upgrade 2",       5,  7, True),    # 67 Dash_Strike_Up
    ManorSlot("Charon Donation Bonus Unlock","Green Path Upgrade 2",       1,  3, False),   # 68 Charon_Gold_Stat_Bonus
    # ── NPC unlocks ──────────────────────────────────────────────────────────
    ManorSlot("Living Safe Unlock",          "Green Path Upgrade 1",        1,  2, True),   # 69 Gold_Saved_Unlock
    ManorSlot("Blacksmith Unlock",           "Blue Path Upgrade 1",         1,  3, True),   # 70 Blacksmith
    ManorSlot("Enchantress Unlock",          "Yellow Path Upgrade 1",       1,  3, True),   # 71 Enchantress
]

_CORE_COUNT = 69

CORE_MANOR_UPGRADES:       list[str] = [f"Upgrade: {s.item_name}"    for s in _MANOR_SLOTS[:_CORE_COUNT]]
CORE_MANOR_LOCATION_NAMES: list[str] = [f"Manor - {s.location_name}" for s in _MANOR_SLOTS[:_CORE_COUNT]]
NPC_MANOR_UPGRADES:        list[str] = [f"Upgrade: {s.item_name}"    for s in _MANOR_SLOTS[_CORE_COUNT:]]
NPC_MANOR_LOCATION_NAMES:  list[str] = [f"Manor - {s.location_name}" for s in _MANOR_SLOTS[_CORE_COUNT:]]
STAT_UPGRADE_MANOR_UPGRADES: list[str] = [f"Upgrade: {s.item_name}"  for s in _MANOR_SLOTS if s.is_stat_upgrade]

# Keyed by "Upgrade: {item_name}" - used by create_items() to compute how many
# AP items to generate per slot. Single-level slots are set to 1.
MANOR_MAX_LEVELS: dict[str, int] = {f"Upgrade: {s.item_name}": s.max_level for s in _MANOR_SLOTS}

# The 14 non-Knight class unlock item names, in ManorSlot order (indices 44–57).
# Index 0 = Boxer Class, index 13 = Dragon Lancer Class.
# Used by the randomize_starting_class option: starting_class_index 1–14 maps to
# CLASS_UNLOCK_ITEM_NAMES[index - 1].
CLASS_UNLOCK_ITEM_NAMES: list[str] = [f"Upgrade: {s.item_name}" for s in _MANOR_SLOTS[44:58]]

# Knight Class as an AP item (granted when starting class is not Knight and the player
# finds it in the multiworld). Not a manor slot - has its own item ID at index 72.
KNIGHT_CLASS_ITEM_NAME: str = "Upgrade: Knight Class"

# Keyed by "Upgrade: {item_name}" - depth in the manor unlock tree, used by
# _compute_manor_upgrade_costs() to scale gold costs. Defaults to 1 if absent.
MANOR_UPGRADE_DEPTHS: dict[str, int] = {f"Upgrade: {s.item_name}": s.depth for s in _MANOR_SLOTS}

# Keyed by "Manor - {location_name}" - depth used by the boss-kill gate logic.
MANOR_LOCATION_DEPTHS: dict[str, int] = {f"Manor - {s.location_name}": s.depth for s in _MANOR_SLOTS}

# ---------------------------------------------------------------------------
# Boss and miniboss location/event names
# ---------------------------------------------------------------------------

# Heirloom interaction locations
LOC_ANANKES_SHAWL_STATUE            = "Ananke's Shawl Statue"
LOC_AETHERS_WINGS_STATUE            = "Aether's Wings Statue"
LOC_AESOPS_TOME_STATUE              = "Aesop's Tome Statue"
LOC_ECHOS_BOOTS_STATUE              = "Echo's Boots Statue"
LOC_PALLAS_VOID_BELL_STATUE         = "Pallas' Void Bell Statue"
LOC_THEIAS_SUN_LANTERN_CONVERSATION = "Theia's Sun Lantern Conversation"

# Teleporter purchase locations
LOC_AXIS_MUNDI_TELEPORTER_PURCHASE        = "Axis Mundi Teleporter Purchase"
LOC_KERGUELEN_PLATEAU_TELEPORTER_PURCHASE = "Kerguelen Plateau Teleporter Purchase"
LOC_STYGIAN_STUDY_TELEPORTER_PURCHASE     = "Stygian Study Teleporter Purchase"
LOC_SUN_TOWER_TELEPORTER_PURCHASE         = "Sun Tower Teleporter Purchase"
LOC_PISHON_DRY_LAKE_TELEPORTER_PURCHASE   = "Pishon Dry Lake Teleporter Purchase"

# Boss defeated locations
LOC_ESTUARY_LAMECH_DEFEATED     = "Estuary Lamech Defeated"
LOC_BYARRRITH_HALPHARR_DEFEATED = "Byarrrith and Halpharr Defeated"
LOC_ESTUARY_NAAMAH_DEFEATED     = "Estuary Naamah Defeated"
LOC_ESTUARY_ENOCH_DEFEATED      = "Estuary Enoch Defeated"
LOC_ESTUARY_IRAD_DEFEATED       = "Estuary Irad Defeated"
LOC_ESTUARY_TUBAL_DEFEATED      = "Estuary Tubal Defeated"
LOC_JONAH_DEFEATED              = "Jonah Defeated"

# Boss cleared events
LOC_ESTUARY_LAMECH_CLEARED      = "Estuary Lamech Cleared"
LOC_BYARRRITH_HALPHARR_CLEARED  = "Byarrrith and Halpharr Cleared"
LOC_ESTUARY_NAAMAH_CLEARED      = "Estuary Naamah Cleared"
LOC_ESTUARY_ENOCH_CLEARED       = "Estuary Enoch Cleared"
LOC_ESTUARY_IRAD_CLEARED        = "Estuary Irad Cleared"
LOC_ESTUARY_TUBAL_CLEARED       = "Estuary Tubal Cleared"
LOC_JONAH_CLEARED               = "Jonah Cleared"

# Miniboss defeated locations
LOC_MURMUR_DEFEATED             = "Murmur Miniboss Defeated"
LOC_GONGHEADS_DEFEATED          = "Gongheads Miniboss Defeated"
LOC_BRIAREUS_COTTUS_DEFEATED    = "Briareus and Cottus Minibosses Defeated"
LOC_GYGES_AEGAEON_DEFEATED      = "Gyges and Aegaeon Minibosses Defeated"

# Miniboss cleared events
LOC_MURMUR_CLEARED              = "Murmur Miniboss Cleared"
LOC_GONGHEADS_CLEARED           = "Gongheads Miniboss Cleared"
LOC_BRIAREUS_COTTUS_CLEARED     = "Briareus and Cottus Minibosses Cleared"
LOC_GYGES_AEGAEON_CLEARED       = "Gyges and Aegaeon Minibosses Cleared"
