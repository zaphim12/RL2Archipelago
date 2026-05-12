import math
import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

# All non-event item IDs are offset from this base.
# TODO: Register an official base ID with the Archipelago project before publishing.
BASE_ID = 0xBEEF0000

HEIRLOOM_OFFSET   = 0x300
BLUEPRINT_OFFSET  = 0x400
RUNE_OFFSET       = 0x500
MANOR_OFFSET      = 0x600
TELEPORTER_OFFSET = 0x700
FILLER_OFFSET     = 0x800


class RogueLegacy2Item(Item):
    game = "Rogue Legacy 2"


class RogueLegacy2ItemData(NamedTuple):
    code: int | None = None
    classification: ItemClassification = ItemClassification.filler


# ---------------------------------------------------------------------------
# Item table
# Entries with code=None are event items (placed at matching event locations).
# ---------------------------------------------------------------------------
item_data_table: dict[str, RogueLegacy2ItemData] = {
    # ── Heirlooms (progression) ──────────────────────────────────────────────
    "Ananke's Shawl":     RogueLegacy2ItemData(code=BASE_ID + HEIRLOOM_OFFSET + 0, classification=ItemClassification.progression),
    "Aether's Wings":     RogueLegacy2ItemData(code=BASE_ID + HEIRLOOM_OFFSET + 1, classification=ItemClassification.progression),
    "Aesop's Tome":       RogueLegacy2ItemData(code=BASE_ID + HEIRLOOM_OFFSET + 2, classification=ItemClassification.progression),
    "Echo's Boots":       RogueLegacy2ItemData(code=BASE_ID + HEIRLOOM_OFFSET + 3, classification=ItemClassification.progression),
    "Pallas' Void Bell":  RogueLegacy2ItemData(code=BASE_ID + HEIRLOOM_OFFSET + 4, classification=ItemClassification.progression),
    "Theia's Sun Lantern":RogueLegacy2ItemData(code=BASE_ID + HEIRLOOM_OFFSET + 5, classification=ItemClassification.progression),

    # ── Teleporter unlocks (useful) ──────────────────────────────────────────
    # Ordered to match ItemRegistry.TELEPORTER_OFFSET indices in C#.
    "Axis Mundi Teleporter":        RogueLegacy2ItemData(code=BASE_ID + TELEPORTER_OFFSET + 0, classification=ItemClassification.useful),
    "Kerguelen Plateau Teleporter": RogueLegacy2ItemData(code=BASE_ID + TELEPORTER_OFFSET + 1, classification=ItemClassification.useful),
    "Stygian Study Teleporter":     RogueLegacy2ItemData(code=BASE_ID + TELEPORTER_OFFSET + 2, classification=ItemClassification.useful),
    "Sun Tower Teleporter":         RogueLegacy2ItemData(code=BASE_ID + TELEPORTER_OFFSET + 3, classification=ItemClassification.useful),
    "Pishon Dry Lake Teleporter":   RogueLegacy2ItemData(code=BASE_ID + TELEPORTER_OFFSET + 4, classification=ItemClassification.useful),

    # ── Filler ───────────────────────────────────────────────────────────────
    "Gold Coins":                   RogueLegacy2ItemData(code=BASE_ID + FILLER_OFFSET + 0,     classification=ItemClassification.filler),

    # ── Events (no ID; placed by create_items at the matching event location) ─
    "Victory":                      RogueLegacy2ItemData(code=None, classification=ItemClassification.progression),
}

# Blueprint items: one per (EquipmentCategoryType, EquipmentType) pair.
# ID = BASE_ID + BLUEPRINT_OFFSET + categoryIndex * 16 + typeIndex
#   categoryIndex: Weapon=0, Head=1, Chest=2, Cape=3, Trinket=4
#   typeIndex:     Bonus Leather=0 .. Black Root=12
_BLUEPRINT_CATEGORIES = ["Weapon", "Helm", "Chest", "Cape", "Trinket"]
_BLUEPRINT_TYPES = [
    "Leather",    "Scholar",   "Warden",  "Sanguine",
    "Ammonite",   "Crescent",  "Drowned", "Gilded",
    "Obsidian",   "Leviathan", "Kin",
    "White Wood", "Black Root", 
]

BLUEPRINT_ITEM_NAMES: list[str] = []
for _c, _cat in enumerate(_BLUEPRINT_CATEGORIES):
    for _t, _type in enumerate(_BLUEPRINT_TYPES):
        _item_name = f"{_type} {_cat} Blueprint"
        item_data_table[_item_name] = RogueLegacy2ItemData(
            code=BASE_ID + BLUEPRINT_OFFSET + _c * 16 + _t,
            classification=ItemClassification.useful,
        )
        BLUEPRINT_ITEM_NAMES.append(_item_name)

# Rune items: one per RuneType, ordered to match the C# ToRuneType() index mapping.
# ID = BASE_ID + RUNE_OFFSET + runeIndex  (0–23)
_RUNE_NAMES = [
    "Reinforced",   "Dash",        "Vault",    "Bounty",
    "Haste",        "Lifesteal",   "Magnesis", "Retaliation",
    "Siphon",       "Capacity",    "Trick",    "Amplification",
    "Soulsteal",    "Resolve",     "Stone",    "Red",
    "Sharpened",    "Focal",       "Might",    "Eldar",
    "Lucky Roller", "High Stakes", "Folded",   "Quenching",
]

RUNE_ITEM_NAMES: list[str] = []
for _i, _rune in enumerate(_RUNE_NAMES):
    _item_name = f"{_rune} Rune"
    item_data_table[_item_name] = RogueLegacy2ItemData(
        code=BASE_ID + RUNE_OFFSET + _i,
        classification=ItemClassification.useful,
    )
    RUNE_ITEM_NAMES.append(_item_name)

# Manor upgrade items: one per SkillTreeType slot.
# ID = BASE_ID + MANOR_OFFSET + listIndex
# Indices 0-68 are core upgrades; indices 69-71 are NPC unlock slots
# (active only when randomize_npc_unlocks=true).
# IMPORTANT: order here must stay in lockstep with ItemRegistry.s_skillTreeTypes in C#.
_CORE_MANOR_UPGRADES: list[str] = [
    "Mess Hall (Vitality Up I)",                                # 0 SkillTreeType.Health_Up
    "Fruit Juice Bar (Vitality Up II)",                         # 1 SkillTreeType.Health_Up2
    "Meteora Gym (Vitality Up III)",                            # 2 SkillTreeType.Health_Up3
    # "Veterinarian Clinic (Revive Chance Up)",                 
    "Institute of Gastronomy (Health Drop Scaling)",            # 3 SkillTreeType.Potion_Up
    # "Stadium (Invulnerability Up)",                           
    "Arsenal (Strength Up I)",                                  # 4 SkillTreeType.Attack_Up
    "Sauna (Strength Up II)",                                   # 5 SkillTreeType.Attack_Up2
    "Rock Climbing Wall (Strength Up III)",                     # 6 SkillTreeType.Attack_Up3
    "Bamboo Garden (Spin Kick scales with INT)",                # 7 SkillTreeType.Down_Strike_Up
    "Gym (Dexterity Up I)",                                     # 8 SkillTreeType.Dexterity_Add1
    "Yoga Class (Dexterity Up II)",                             # 9 SkillTreeType.Dexterity_Add2
    "Flower Shop (Dexterity Up III)",                           # 10 SkillTreeType.Dexterity_Add3
    "The Laundromat (Weapon Crit Damage Up)",                   # 11 SkillTreeType.Crit_Damage_Up
    "Study Hall (Intelligence Up I)",                           # 12 SkillTreeType.Magic_Attack_Up
    "Math Club (Intelligence Up II)",                           # 13 SkillTreeType.Magic_Attack_Up2
    "University (Intelligence Up III)",                         # 14 SkillTreeType.Magic_Attack_Up3
    "Library (Focus Up I)",                                     # 15 SkillTreeType.Focus_Up1
    "Hall of Wisdom (Focus Up II)",                             # 16 SkillTreeType.Focus_Up2
    "Court of the Wise (Focus Up III)",                         # 17 SkillTreeType.Focus_Up3
    "The Lodge (Magic Crit Damage Up)",                         # 18 SkillTreeType.Magic_Crit_Damage_Up
    # "The Thaumaturgy (Cooldown Reduction)",                   
    "Fashion Chambers (Max Weight Up I)",                       # 19 SkillTreeType.Equip_Up
    "Tailors (Max Weight Up II)",                               # 20 SkillTreeType.Equip_Up2
    "Artisan (Max Weight Up III)",                              # 21 SkillTreeType.Equip_Up3
    "Etching Chambers (Max Rune Weight Up I)",                  # 22 SkillTreeType.Rune_Equip_Up
    "Pillow Mill (Max Rune Weight Up II)",                      # 23 SkillTreeType.Rune_Equip_Up2
    "Bed Mill (Max Rune Weight Up III)",                        # 24 SkillTreeType.Rune_Equip_Up3
    "Foundry (Armor Up I)",                                     # 25 SkillTreeType.Armor_Up
    "Blast Furnace (Armor Up II)",                              # 26 SkillTreeType.Armor_Up2
    "Some Kind of Kiln (Armor Up III)",                         # 27 SkillTreeType.Armor_Up3
    "Universal Health Stair (Traits Give Gold)",                # 28 SkillTreeType.Traits_Give_Gold
    "Repurposed Mining Shaft (Traits Gold Gain Up)",            # 39 SkillTreeType.Traits_Give_Gold_Gain_Mod
    "Geologist's Camp (Ore Drop Chance Up)",                    # 30 SkillTreeType.Equipment_Ore_Find_Up
    "Dowsing Center (Red Aether Drop Chance Up)",               # 31 SkillTreeType.Rune_Ore_Find_Up
    "Massive Vault (Gold Gain Up I)",                           # 32 SkillTreeType.Gold_Gain_Up
    # "Gold Gain Up II",                                        
    # "Gold Gain Up III",                                       
    # "Sky Bridge (Gold Gain Up IV)",                           
    # "Tree Bridge (Gold Gain Up V)",                           
    "Career Center (Re-Roll Children)",                         # 33 SkillTreeType.Randomize_Children
    "Aerobics Classroom (Encumbrance Limit Up)",                # 34 SkillTreeType.Weight_CD_Reduce
    # "The Fissary (Mana Cost Down)",                           
    "Courthouse (Living Safe Max Gold Up)",                     # 35 SkillTreeType.Gold_Saved_Cap_Up
    "Scribe's Office (Living Safe Conversion Up)",              # 36 SkillTreeType.Gold_Saved_Amount_Saved
    "Fighting Ring (Boxer Class)",                              # 37 SkillTreeType.BoxingGlove_Class_Unlock
    "Dance Hall (Duelist Class)",                               # 38 SkillTreeType.Saber_Class_Unlock
    "Guild of Dark Arts (Assassin Class)",                      # 39 SkillTreeType.DualBlades_Class_Unlock
    "Drill Store (Architect Cost Reduction)",                   # 40 SkillTreeType.Architect_Cost_Down
    # "Genesis Pool (Polymorph Class)",                         
    "Adoption Center (More Heirs)",                             # 41 SkillTreeType.More_Children
    "The Kitchen (Chef Class)",                                 # 42 SkillTreeType.Ladle_Class_Unlock
    # "Forest Village (Chakram Class)",                         
    # "Martial Arts School (Tonfa Class)",                      
    # "The Pilgrim's Steps (Knight Class)",                     
    "Butcher's Shoppe (Barbarian Class)",                       # 43 SkillTreeType.Axe_Class_Unlock
    "Academy (Mage Class)",                                     # 44 SkillTreeType.Wand_Class_Unlock
    "Archery Range (Ranger Class)",                             # 45 SkillTreeType.Bow_Class_Unlock
    "Sand Pits (Valkyrie Class)",                               # 46 SkillTreeType.Spear_Class_Unlock
    # "Hidden Dojo (Ninja Class)",                              
    # "Ancestral Plot (Lich Class)",                            
    # "Miner's Camp (Spelunker Class)",                         
    "Saltpeter Mines (Gunslinger Class)",                       # 47 SkillTreeType.Gun_Class_Unlock
    "Ryokan (Ronin Class)",                                     # 48 SkillTreeType.Samurai_Class_Unlock
    "The Tavern (Bard Class)",                                  # 49 SkillTreeType.Music_Class_Unlock
    "The Flying Docks (Pirate Class)",                          # 50 SkillTreeType.Pirate_Class_Unlock
    "The Astral Gardens (Astromancer Class)",                   # 51 SkillTreeType.Astro_Class_Unlock
    # "Weapon Master Upgrade",                                  
    # "Knight Upgrade",                                         
    "The Aviary (Dragon Lancer Class)",                         # 52 SkillTreeType.Lancer_Class_Unlock
    "Trophy Room (XP Up)",                                      # 53 SkillTreeType.XP_Up
    "Jeweler (Ore Gain Up)",                                    # 54 SkillTreeType.Equipment_Ore_Gain_Up
    "Buried Tomb (Red Aether Gain Up)",                         # 55 SkillTreeType.Rune_Ore_Gain_Up
    # "Herb Garden (Free Cast Up)",                             
    "Dummy (Training Dummy Unlock)",                            # 56 SkillTreeType.Unlock_Dummy
    "Meditation Studies (Boss Health/Mana Restore)",            # 57 SkillTreeType.Boss_Health_Restore
    "Sage Totem (Mastery Rank Unlock)",                         # 58 SkillTreeType.Unlock_Totem
    "Archaeology Camp (Relic Resolve Cost Down)",               # 59 SkillTreeType.Relic_Cost_Down
    "Medieval Forgery (Relic Reroll Up)",                       # 60 SkillTreeType.Reroll_Relic
    "Alchemy Lab (Potion Recharge Talent)",                     # 61 SkillTreeType.Potion_Recharge_Talent
    "Psychiatrist (Resolve Up)",                                # 62 SkillTreeType.Resolve_Up
    "Jousting Studies (Dash Damage Reduction)",                 # 63 SkillTreeType.Dash_Strike_Up
    "Charity Dungeon (Charon Donation Bonus Unlock)",           # 64 SkillTreeType.Charon_Gold_Stat_Bonus
    "The Dicer's Den (Weapon Crit Chance Up)",                  # 65 SkillTreeType.Crit_Chance_Flat_Up
    "The Quantum Observatory (Magic Crit Chance Up)",           # 66 SkillTreeType.Magic_Crit_Chance_Flat_Up
    "The Bizarre Bazaar (Reroll Relic Room Cap Up)",            # 67 SkillTreeType.Reroll_Relic_Room_Cap
    "Screw Distillery (Architect Unlock)",                      # 68 SkillTreeType.Architect
]

_NPC_MANOR_UPGRADES: list[str] = [
    "Offshore Bank Account (Living Safe)",          # 69 SkillTreeType.Gold_Saved_Unlock
    "Foundation (Smithy Unlock)",                   # 70 SkillTreeType.Smithy
    "Enchantress' Quarters (Enchantress Unlock)",   # 71 SkillTreeType.Enchantress
    # "Banker Unlock",                              
]

# Max level for each manor upgrade slot.
# Used by create_items() to determine how many AP items to generate per slot:
#   copies = ceil(max_level / bundle_size)
# Single-level slots (class/NPC unlocks, binary toggles) are set to 1.
MANOR_MAX_LEVELS: dict[str, int] = {
    # ── Vitality ─────────────────────────────────────────────────────────────
    "Manor: Mess Hall (Vitality Up I)":                         10,
    "Manor: Fruit Juice Bar (Vitality Up II)":                  20,
    "Manor: Meteora Gym (Vitality Up III)":                     30,
    # ── Misc survival ────────────────────────────────────────────────────────
    "Manor: Institute of Gastronomy (Health Drop Scaling)":     10, # TODO technically max is 0 without Unbreakable Will, should this be handled differently?
    # ── Strength ─────────────────────────────────────────────────────────────
    "Manor: Arsenal (Strength Up I)":                           10,
    "Manor: Sauna (Strength Up II)":                            20,
    "Manor: Rock Climbing Wall (Strength Up III)":              30,
    # ── Spin Kicks ───────────────────────────────────────────────────────────
    "Manor: Bamboo Garden (Spin Kick scales with INT)":         5,
    # ── Dexterity ────────────────────────────────────────────────────────────
    "Manor: Gym (Dexterity Up I)":                              10,
    "Manor: Yoga Class (Dexterity Up II)":                      25,
    "Manor: Flower Shop (Dexterity Up III)":                    35,
    # ── Weapon Crit Damage ───────────────────────────────────────────────────
    "Manor: The Laundromat (Weapon Crit Damage Up)":            10,
    # ── Intelligence ─────────────────────────────────────────────────────────
    "Manor: Study Hall (Intelligence Up I)":                    10,
    "Manor: Math Club (Intelligence Up II)":                    20,
    "Manor: University (Intelligence Up III)":                  30,
    # ── Focus ────────────────────────────────────────────────────────────────
    "Manor: Library (Focus Up I)":                              10,
    "Manor: Hall of Wisdom (Focus Up II)":                      25,
    "Manor: Court of the Wise (Focus Up III)":                  25,
    # ── Spell Crit Damage ────────────────────────────────────────────────────
    "Manor: The Lodge (Magic Crit Damage Up)":                  10,
    # ── Equipment weight ─────────────────────────────────────────────────────
    "Manor: Fashion Chambers (Max Weight Up I)":                5,
    "Manor: Tailors (Max Weight Up II)":                        15,
    "Manor: Artisan (Max Weight Up III)":                       25,
    # ── Rune weight ──────────────────────────────────────────────────────────
    "Manor: Etching Chambers (Max Rune Weight Up I)":           5,
    "Manor: Pillow Mill (Max Rune Weight Up II)":               15,
    "Manor: Bed Mill (Max Rune Weight Up III)":                 15,
    # ── Armor ────────────────────────────────────────────────────────────────
    "Manor: Foundry (Armor Up I)":                              15,
    "Manor: Blast Furnace (Armor Up II)":                       25,
    "Manor: Some Kind of Kiln (Armor Up III)":                  35,
    # ── Gold & economy ───────────────────────────────────────────────────────
    "Manor: Universal Health Stair (Traits Give Gold)":         1,
    "Manor: Repurposed Mining Shaft (Traits Gold Gain Up)":     10,
    "Manor: Geologist's Camp (Ore Drop Chance Up)":             5,
    "Manor: Dowsing Center (Red Aether Drop Chance Up)":        5,
    "Manor: Massive Vault (Gold Gain Up I)":                    20, # TODO technically max is 0 without Unbreakable Will, should this be handled differently?
    # ── Rerolls ──────────────────────────────────────────────────────────────
    "Manor: Career Center (Re-Roll Children)":                  5,
    "Manor: Adoption Center (More Heirs)":                      1,  # TODO technically max is 0 without Infinite Knowledge, should this be handled differently?
    # ── Encumbrance ──────────────────────────────────────────────────────────
    "Manor: Aerobics Classroom (Encumbrance Limit Up)":         10,
    # ── Living safe ──────────────────────────────────────────────────────────
    "Manor: Courthouse (Living Safe Max Gold Up)":              20,
    "Manor: Scribe's Office (Living Safe Conversion Up)":       10,
    # ── Architect ────────────────────────────────────────────────────────────
    "Manor: Drill Store (Architect Cost Reduction)":            5,
    # ── Class unlocks ────────────────────────────────────────────────────────
    "Manor: Fighting Ring (Boxer Class)":                       1,
    "Manor: Dance Hall (Duelist Class)":                        1,
    "Manor: Guild of Dark Arts (Assassin Class)":               1,
    "Manor: The Kitchen (Chef Class)":                          1,
    "Manor: Butcher's Shoppe (Barbarian Class)":                1,
    "Manor: Academy (Mage Class)":                              1,
    "Manor: Archery Range (Ranger Class)":                      1,
    "Manor: Sand Pits (Valkyrie Class)":                        1,
    "Manor: Saltpeter Mines (Gunslinger Class)":                1,
    "Manor: Ryokan (Ronin Class)":                              1,
    "Manor: The Tavern (Bard Class)":                           1,
    "Manor: The Flying Docks (Pirate Class)":                   1,
    "Manor: The Astral Gardens (Astromancer Class)":            1,
    "Manor: The Aviary (Dragon Lancer Class)":                  1,
    # ── Progression & utility ────────────────────────────────────────────────
    "Manor: Trophy Room (XP Up)":                               10,
    "Manor: Jeweler (Ore Gain Up)":                             20, # TODO technically max is 0 without Absolute Strength, should this be handled differently?
    "Manor: Buried Tomb (Red Aether Gain Up)":                  20, # TODO technically max is 0 without Infinite Knowledge, should this be handled differently?
    "Manor: Dummy (Training Dummy Unlock)":                     1,
    "Manor: Meditation Studies (Boss Health/Mana Restore)":     5,
    "Manor: Sage Totem (Mastery Rank Unlock)":                  1,
    "Manor: Archaeology Camp (Relic Resolve Cost Down)":        5,
    "Manor: Medieval Forgery (Relic Reroll Up)":                10,
    "Manor: Alchemy Lab (Potion Recharge Talent)":              1,
    "Manor: Psychiatrist (Resolve Up)":                         20,
    "Manor: Jousting Studies (Dash Damage Reduction)":          5,
    "Manor: Charity Dungeon (Charon Donation Bonus Unlock)":    1,
    "Manor: The Dicer's Den (Weapon Crit Chance Up)":           20, # TODO technically max is 0 without Absolute Strength, should this be handled differently?
    "Manor: The Quantum Observatory (Magic Crit Chance Up)":    20, # TODO technically max is 0 without Infinite Knowledge, should this be handled differently?
    "Manor: The Bizarre Bazaar (Reroll Relic Room Cap Up)":     2,  # TODO technically max is 0 without Master Smith, should this be handled differently?
    "Manor: Screw Distillery (Architect Unlock)":               1,
    # ── NPC unlocks ──────────────────────────────────────────────────────────
    "Manor: Offshore Bank Account (Living Safe)":               1,
    "Manor: Foundation (Smithy Unlock)":                        1,
    "Manor: Enchantress' Quarters (Enchantress Unlock)":        1,
}

MANOR_ITEM_NAMES: list[str] = []
for _i, _name in enumerate(_CORE_MANOR_UPGRADES + _NPC_MANOR_UPGRADES):
    _item_name = f"Manor: {_name}"
    item_data_table[_item_name] = RogueLegacy2ItemData(
        code=BASE_ID + MANOR_OFFSET + _i,
        classification=ItemClassification.useful,
    )
    MANOR_ITEM_NAMES.append(_item_name)

HEIRLOOM_ITEM_NAMES = [
    "Ananke's Shawl",
    "Aether's Wings",
    "Aesop's Tome",
    "Echo's Boots",
    "Pallas' Void Bell",
    "Theia's Sun Lantern",
]

TELEPORTER_ITEM_NAMES = [
    "Axis Mundi Teleporter",
    "Kerguelen Plateau Teleporter",
    "Stygian Study Teleporter",
    "Sun Tower Teleporter",
    "Pishon Dry Lake Teleporter",
]

# Event items are placed at locations whose names differ from the item name.
# Matches must be kept in sync with the "address=None" entries in locations_and_regions.py.
event_item_to_location: dict[str, str] = {
    "Victory": "The Traitor Defeated",
}

# Convenience: name→ID dict used by World.item_name_to_id
all_non_event_items_table: dict[str, int] = {
    name: data.code
    for name, data in item_data_table.items()
    if data.code is not None
}


def create_item(player: int, name: str) -> RogueLegacy2Item:
    data = item_data_table[name]
    return RogueLegacy2Item(name, data.classification, data.code, player)


def create_items(world: "RogueLegacy2World") -> None:
    """Fill the multiworld item pool to match the number of non-event locations."""
    multiworld = world.multiworld
    player = world.player

    # Place every event item at its matching event location.
    for item_name, location_name in event_item_to_location.items():
        multiworld.get_location(location_name, player).place_locked_item(create_item(player, item_name))

    randomize_npc = bool(world.options.randomize_npc_unlocks.value)

    # When NPC unlocks are not randomized, lock each NPC item to its home
    # location so the server always returns it there on check.
    if not randomize_npc:
        for npc_name in _NPC_MANOR_UPGRADES:
            loc = multiworld.get_location(f"Manor - {npc_name}", player)
            loc.place_locked_item(create_item(player, f"Manor: {npc_name}"))

    unfilled = len(multiworld.get_unfilled_locations(player))

    bundle_size = max(1, world.options.manor_upgrade_bundle_size.value)
    useful_count = world.options.manor_useful_count.value
    core_names = MANOR_ITEM_NAMES[:len(_CORE_MANOR_UPGRADES)]
    npc_names  = MANOR_ITEM_NAMES[len(_CORE_MANOR_UPGRADES):]

    # Priority: always included regardless of location count.
    # NPC items are in the free pool only when randomize_npc=True; when False they
    # were already locked to their home locations above.
    # Heirlooms, Teleporters, and single-level manor upgrades are always priority items
    priority_items: list[RogueLegacy2Item] = []
    priority_items += [create_item(player, n) for n in HEIRLOOM_ITEM_NAMES]
    priority_items += [create_item(player, n) for n in TELEPORTER_ITEM_NAMES]
    if randomize_npc:
        priority_items += [create_item(player, n) for n in npc_names]
    for name in core_names:
        if MANOR_MAX_LEVELS.get(name, 1) <= 1:
            priority_items.append(create_item(player, name))

    # Optional pools, in preference order. These fill up remaining slots after the priority pool.
    # If there aren't enough slots remaining for all useful items, they are allocated from each item
    #  type be percentages (currently 25% blueprints, 25% runes, 50% manor) 
    # useful_manor_pool: first useful_count copies of multi-level upgrades.
    # filler_manor_pool: remaining copies; used only after all useful items are placed.
    blueprint_pool: list[RogueLegacy2Item] = [create_item(player, n) for n in BLUEPRINT_ITEM_NAMES]
    rune_pool:      list[RogueLegacy2Item] = [create_item(player, n) for n in RUNE_ITEM_NAMES]
    useful_manor_pool: list[RogueLegacy2Item] = []
    filler_manor_pool: list[RogueLegacy2Item] = []
    for name in core_names:
        max_level = MANOR_MAX_LEVELS.get(name, 1)
        if max_level <= 1:
            continue
        copies = max(1, math.ceil(max_level / bundle_size))
        data = item_data_table[name]
        for copy_idx in range(copies):
            if copy_idx < useful_count:
                useful_manor_pool.append(RogueLegacy2Item(name, ItemClassification.useful, data.code, player))
            else:
                filler_manor_pool.append(RogueLegacy2Item(name, ItemClassification.filler, data.code, player))

    remaining   = unfilled - len(priority_items)
    total_useful = len(blueprint_pool) + len(rune_pool) + len(useful_manor_pool)

    world.random.shuffle(filler_manor_pool)

    if total_useful <= remaining:
        # All useful items fit; fill leftover slots with filler manor items.
        pool = priority_items + blueprint_pool + rune_pool + useful_manor_pool
        leftover = remaining - total_useful
        pool += filler_manor_pool[:leftover]
        padding_candidates = []
    else:
        # Truncation: distribute remaining slots 25% blueprints / 25% runes / 50% useful manor.
        blueprint_slots = remaining // 4
        rune_slots      = remaining // 4
        manor_slots     = remaining - blueprint_slots - rune_slots

        world.random.shuffle(blueprint_pool)
        world.random.shuffle(rune_pool)
        world.random.shuffle(useful_manor_pool)

        pool = (
            priority_items
            + blueprint_pool[:blueprint_slots]
            + rune_pool[:rune_slots]
            + useful_manor_pool[:manor_slots]
        )
        # padding_candidates: leftover useful items (not yet in pool) before filler
        padding_candidates = (
            blueprint_pool[blueprint_slots:]
            + rune_pool[rune_slots:]
            + useful_manor_pool[manor_slots:]
            + filler_manor_pool
        )

    # Fill any remaining slots using candidates that are not already in the pool.
    for item in padding_candidates:
        if len(pool) >= unfilled:
            break
        pool.append(RogueLegacy2Item(item.name, item.classification, item.code, player))

    # If we have used all actual item candidates, and still have open slots, fill the rest with "Gold Coins" grants
    while len(pool) < unfilled:
        pool.append(create_item(player, "Gold Coins"))

    multiworld.itempool += pool
