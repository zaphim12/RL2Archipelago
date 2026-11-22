import math
import typing
from typing import NamedTuple

from BaseClasses import (
    Entrance,
    Item,
    ItemClassification,
    Location,
    LocationProgressType,
    Region,
)
from worlds.generic.Rules import add_rule, set_rule

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

from .constants import (
    BIOME_AXIS_MUNDI,
    BIOME_CITADEL_AGARTHA,
    BIOME_KERGUELEN_PLATEAU,
    BIOME_NAMES,
    BIOME_PISHON_DRY_LAKE,
    BIOME_STYGIAN_STUDY,
    BIOME_SUN_TOWER,
    CORE_MANOR_LOCATION_NAMES,
    HEIRLOOM_AESOPS_TOME,
    HEIRLOOM_AETHERS_WINGS,
    HEIRLOOM_ANANKES_SHAWL,
    HEIRLOOM_ECHOS_BOOTS,
    HEIRLOOM_PALLAS_VOID_BELL,
    HEIRLOOM_THEIAS_SUN_LANTERN,
    LOC_AESOPS_TOME_STATUE,
    LOC_AETHERS_WINGS_STATUE,
    LOC_ANANKES_SHAWL_STATUE,
    LOC_AXIS_MUNDI_TELEPORTER_PURCHASE,
    LOC_BRIAREUS_COTTUS_CLEARED,
    LOC_BRIAREUS_COTTUS_DEFEATED,
    LOC_BYARRRITH_HALPHARR_DEFEATED,
    LOC_ECHOS_BOOTS_STATUE,
    LOC_ESTUARY_ENOCH_CLEARED,
    LOC_ESTUARY_ENOCH_DEFEATED,
    LOC_ESTUARY_IRAD_CLEARED,
    LOC_ESTUARY_IRAD_DEFEATED,
    LOC_ESTUARY_LAMECH_CLEARED,
    LOC_ESTUARY_LAMECH_DEFEATED,
    LOC_ESTUARY_NAAMAH_CLEARED,
    LOC_ESTUARY_NAAMAH_DEFEATED,
    LOC_ESTUARY_TUBAL_CLEARED,
    LOC_ESTUARY_TUBAL_DEFEATED,
    LOC_GONGHEADS_CLEARED,
    LOC_GONGHEADS_DEFEATED,
    LOC_GYGES_AEGAEON_CLEARED,
    LOC_GYGES_AEGAEON_DEFEATED,
    LOC_JONAH_CLEARED,
    LOC_JONAH_DEFEATED,
    LOC_KERGUELEN_PLATEAU_TELEPORTER_PURCHASE,
    LOC_MURMUR_CLEARED,
    LOC_MURMUR_DEFEATED,
    LOC_PALLAS_VOID_BELL_STATUE,
    LOC_PISHON_DRY_LAKE_TELEPORTER_PURCHASE,
    LOC_STYGIAN_STUDY_TELEPORTER_PURCHASE,
    LOC_SUN_TOWER_TELEPORTER_PURCHASE,
    LOC_THEIAS_SUN_LANTERN_CONVERSATION,
    LOC_BYARRRITH_HALPHARR_CLEARED,
    MANOR_LOCATION_DEPTHS,
    MANOR_SHOP_ID_STRIDE,
    MANOR_SHOP_NAME_PREFIX,
    MANOR_SHOP_POSITIONS,
    LOGICAL_MASTERY_RANKS_PER_CLASS,
    MASTERY_CLASS_NAMES,
    MASTERY_NAME_PREFIX,
    MAX_CHEST_CHECKS_PER_BIOME,
    MAX_MANOR_SHOP_DEPTH,
    MAX_MASTERY_RANK_PER_CLASS,
    MAX_TOTAL_MASTERY_RANK,
    MAX_TREE_MANOR_DEPTH,
    NPC_MANOR_LOCATION_NAMES,
    NPC_MANOR_UPGRADES,
    STAT_UPGRADE_MANOR_UPGRADES,
    TELEPORTER_KERGUELEN_PLATEAU,
    TELEPORTER_SUN_TOWER,
    TREE_MANOR_LOCATION_NAME_SET,
    class_mastery_location_name,
    manor_shop_location_name,
    total_mastery_location_name,
)
from .options import ManorLayout, MasteryChecks
from .items import BASE_ID

# ---------------------------------------------------------------------------
# Location ID offsets
#
# Location IDs MUST stay in lockstep with the C# mod
# (see Locations/LocationRegistry.cs). Renumbering an ID would invalidate
# existing multiworld seeds.
# ---------------------------------------------------------------------------
BOSS_KILL_OFFSET     = 0x100
MINIBOSS_KILL_OFFSET = 0x200
HEIRLOOM_OFFSET      = 0x300
MANOR_OFFSET         = 0x600
TELEPORTER_OFFSET    = 0x700

# Shop-layout manor locations get their own block rather than extending MANOR_OFFSET: 0xA00-0xBFF.
MANOR_SHOP_OFFSET         = 0xA00  # BASE_ID + offset + position * STRIDE + depthIndex

JOURNAL_GROUPED_OFFSET    = 0x800  # BASE_ID + offset + biomeIndex (0-5)
MEMORY_GROUPED_OFFSET     = 0x808  # BASE_ID + offset + biomeIndex (only 0, 2 active)
JOURNAL_INDIVIDUAL_OFFSET = 0x810  # BASE_ID + offset + biomeIndex * 16 + journalIndex
MEMORY_INDIVIDUAL_OFFSET  = 0x870  # BASE_ID + offset + biomeIndex * 16 + memoryIndex

# Mastery rank locations. The two modes are mutually exclusive in any one seed but get
# separate blocks so that a (mode, level) pair keeps the same ID either way.
#
# Total needs MAX_MASTERY_RANK_PER_CLASS * 15 = 750 consecutive IDs, so its block runs
# 0xC00-0xEED and 0xC00-0xFFF is reserved for it. Per-class needs 15 * STRIDE = 960 and starts
# after that at 0x1000, running to 0x13BF. Both stay far inside a signed 32-bit int once added
# to BASE_ID (0x0BEEF000 + 0x13BF).
MASTERY_TOTAL_OFFSET = 0xC00   # BASE_ID + offset + (level - 1), level 1..MAX_TOTAL_MASTERY_RANK
MASTERY_CLASS_OFFSET = 0x1000  # BASE_ID + offset + classPosition * STRIDE + (level - 1)

# Per-class stride, the same trick the blueprint and rune pools use: it keeps a class's IDs
# stable at any MasteryLevelsPerClass setting. It must never drop below
# MAX_MASTERY_RANK_PER_CLASS, or one class's deep ranks would collide with the next class's
# shallow ones - which is why raising that cap to 50 took this from 16 to the next power of
# two above it. Mirrors MASTERY_CLASS_ID_STRIDE in the C# mod.
MASTERY_CLASS_ID_STRIDE = 64

# Blueprint and rune chest pools, 6 biomes at CHEST_SLOT_ID_STRIDE each. They start after the
# per-class mastery block (which ends at 0x13BF).
BLUEPRINT_OFFSET = 0x1400  # BASE_ID + offset + biomeIndex * STRIDE + slotIndex, to 0x157F
RUNE_OFFSET      = 0x1600  # BASE_ID + offset + biomeIndex * STRIDE + slotIndex, to 0x177F

_JOURNAL_COUNTS = [4, 4, 4, 6, 7, 7]  # Citadel Agartha, Axis Mundi, Kerguelen Plateau, Stygian Study, Sun Tower, Pishon Dry Lake
_MEMORY_COUNTS  = [4, 0, 5, 0, 0, 0]

# Blueprint and rune IDs use a biome-stride layout so IDs are stable regardless of how many slots
# are enabled:  id = BASE_ID + <pool>_OFFSET + biomeIndex * CHEST_SLOT_ID_STRIDE + slotIndex.
# The stride must never drop below MAX_CHEST_CHECKS_PER_BIOME, or one biome's deep slots would
# collide with the next biome's shallow ones. Mirrors CHEST_SLOT_ID_STRIDE in the C# mod.
CHEST_SLOT_ID_STRIDE = 64


# ---------------------------------------------------------------------------
# Location name helpers
# ---------------------------------------------------------------------------

def fairy_chest_name(biome_name: str, slot: int) -> str:
    return f"{biome_name} - Fairy Chest {slot + 1}"

def blueprint_chest_name(biome_name: str, slot: int) -> str:
    return f"{biome_name} - Blueprint Chest {slot + 1}"

def journal_entry_name(biome_name: str, index: int) -> str:
    return f"{biome_name} - Journal Entry {index + 1}"

def memory_fragment_name(biome_name: str, index: int) -> str:
    return f"{biome_name} - Memory Fragment {index + 1}"

def all_journals_read_name(biome_name: str) -> str:
    return f"{biome_name} - All Journals Read"

def all_memories_read_name(biome_name: str) -> str:
    return f"{biome_name} - All Memories Read"


# ---------------------------------------------------------------------------
# Progression-gating constants
# ---------------------------------------------------------------------------

# Ordered to match the boss encounter sequence used for counting cleared bosses.
_BOSS_CLEARED_EVENTS: list[str] = [
    LOC_ESTUARY_LAMECH_CLEARED,
    LOC_BYARRRITH_HALPHARR_CLEARED,
    LOC_ESTUARY_NAAMAH_CLEARED,
    LOC_ESTUARY_ENOCH_CLEARED,
    LOC_ESTUARY_IRAD_CLEARED,
    LOC_ESTUARY_TUBAL_CLEARED,
]

_BIOME_BOSS_CLEARED_EVENTS: dict[str, str] = {
    BIOME_CITADEL_AGARTHA:   LOC_ESTUARY_LAMECH_CLEARED,
    BIOME_AXIS_MUNDI:        LOC_BYARRRITH_HALPHARR_CLEARED,
    BIOME_KERGUELEN_PLATEAU: LOC_ESTUARY_NAAMAH_CLEARED,
    BIOME_STYGIAN_STUDY:     LOC_ESTUARY_ENOCH_CLEARED,
    BIOME_SUN_TOWER:         LOC_ESTUARY_IRAD_CLEARED,
    BIOME_PISHON_DRY_LAKE:   LOC_ESTUARY_TUBAL_CLEARED,
}

# Stat-upgrade tier multiplier per biome (0 = no gating).
_BIOME_STAT_UPGRADE_TIERS: dict[str, int] = {
    BIOME_CITADEL_AGARTHA:   0,
    BIOME_AXIS_MUNDI:        0,
    BIOME_KERGUELEN_PLATEAU: 1,
    BIOME_STYGIAN_STUDY:     2,
    BIOME_SUN_TOWER:         3,
    BIOME_PISHON_DRY_LAKE:   4,
}

# All manor upgrade item names whose received counts contribute to the stat-upgrade total.
_STAT_UPGRADE_ITEM_NAMES: tuple[str, ...] = tuple(STAT_UPGRADE_MANOR_UPGRADES)

# NPC unlock item names (Living Safe, Blacksmith Unlock, Enchantress Unlock).
_NPC_ITEM_NAMES: tuple[str, ...] = tuple(NPC_MANOR_UPGRADES)

# Required NPC unlock count per biome for chest/journal gating (0 = no gating).
_BIOME_NPC_TIERS: dict[str, int] = {
    BIOME_CITADEL_AGARTHA:   0,
    BIOME_AXIS_MUNDI:        0,
    BIOME_KERGUELEN_PLATEAU: 0,
    BIOME_STYGIAN_STUDY:     1,
    BIOME_SUN_TOWER:         2,
    BIOME_PISHON_DRY_LAKE:   3,
}

# (location_name, required_npc_count) pairs for main boss defeat/cleared locations.
# Last 4 bosses need 1 NPC, last 3 need 2, last 2 need 3.
_BOSS_NPC_REQS: list[tuple[str, int]] = [
    (LOC_ESTUARY_NAAMAH_DEFEATED,    1),
    (LOC_ESTUARY_NAAMAH_CLEARED,     1),
    (LOC_MURMUR_DEFEATED,            1),
    (LOC_MURMUR_CLEARED,             1),
    (LOC_GONGHEADS_DEFEATED,         1),
    (LOC_GONGHEADS_CLEARED,          1),
    (LOC_ESTUARY_ENOCH_DEFEATED,     2),
    (LOC_ESTUARY_ENOCH_CLEARED,      2),
    (LOC_ESTUARY_IRAD_DEFEATED,      3),
    (LOC_ESTUARY_IRAD_CLEARED,       3),
    (LOC_BRIAREUS_COTTUS_DEFEATED,   3),
    (LOC_BRIAREUS_COTTUS_CLEARED,    3),
    (LOC_GYGES_AEGAEON_DEFEATED,     3),
    (LOC_GYGES_AEGAEON_CLEARED,      3),
    (LOC_ESTUARY_TUBAL_DEFEATED,     3),
    (LOC_ESTUARY_TUBAL_CLEARED,      3),
]


class RogueLegacy2Location(Location):
    game = "Rogue Legacy 2"


class RogueLegacy2LocationData(NamedTuple):
    region: str
    address: int | None = None   # None = event location


# ---------------------------------------------------------------------------
# Location table
#
# All *possible* blueprint locations (up to max per biome) are registered here
# so that location_name_to_id is complete. create_regions() only instantiates
# the subset selected by the blueprint_checks_per_biome option.
# ---------------------------------------------------------------------------
location_data_table: dict[str, RogueLegacy2LocationData] = {
    # ── Boss kills (Tier 1) ──────────────────────────────────────────────────
    LOC_ESTUARY_LAMECH_DEFEATED:     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 0),
    LOC_BYARRRITH_HALPHARR_DEFEATED: RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 1),
    LOC_ESTUARY_NAAMAH_DEFEATED:     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 2),
    LOC_ESTUARY_ENOCH_DEFEATED:      RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 3),
    LOC_ESTUARY_IRAD_DEFEATED:       RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 4),
    LOC_ESTUARY_TUBAL_DEFEATED:      RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 5),
    LOC_JONAH_DEFEATED:              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 6),

    # ── Heirloom interactions ────────────────────────────────────────────────
    LOC_ANANKES_SHAWL_STATUE:            RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 0),
    LOC_AETHERS_WINGS_STATUE:            RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 1),
    LOC_AESOPS_TOME_STATUE:              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 2),
    LOC_ECHOS_BOOTS_STATUE:              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 3),
    LOC_PALLAS_VOID_BELL_STATUE:         RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 4),
    LOC_THEIAS_SUN_LANTERN_CONVERSATION: RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 5),

    # ── Miniboss kills ───────────────────────────────────────────────────────
    LOC_MURMUR_DEFEATED:          RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 0),
    LOC_GONGHEADS_DEFEATED:       RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 1),
    LOC_BRIAREUS_COTTUS_DEFEATED: RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 2),
    LOC_GYGES_AEGAEON_DEFEATED:   RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 3),

    # ── Pizza girl teleporter purchases ─────────────────────────────────────
    LOC_AXIS_MUNDI_TELEPORTER_PURCHASE:        RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 0),
    LOC_KERGUELEN_PLATEAU_TELEPORTER_PURCHASE: RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 1),
    LOC_STYGIAN_STUDY_TELEPORTER_PURCHASE:     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 2),
    LOC_SUN_TOWER_TELEPORTER_PURCHASE:         RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 3),
    LOC_PISHON_DRY_LAKE_TELEPORTER_PURCHASE:   RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 4),

}

# Register all manor upgrade locations so location_name_to_id is complete.
# Core (indices 0-68) and NPC unlocks (69-71) are always registered.
for _i, _loc_name in enumerate(CORE_MANOR_LOCATION_NAMES + NPC_MANOR_LOCATION_NAMES):
    location_data_table[_loc_name] = RogueLegacy2LocationData(
        region="Manor",
        address=BASE_ID + MANOR_OFFSET + _i,
    )

# Register every possible shop-layout manor location, at the maximum depth, so
# location_name_to_id is complete and a given (position, depth) keeps the same ID at
# any manor_shop_depth setting. create_regions() instantiates only the active subset,
# and none of them when the tree layout is selected.
for _pos in range(MANOR_SHOP_POSITIONS):
    for _d in range(MAX_MANOR_SHOP_DEPTH):
        location_data_table[manor_shop_location_name(_pos, _d)] = RogueLegacy2LocationData(
            region="Manor",
            address=BASE_ID + MANOR_SHOP_OFFSET + _pos * MANOR_SHOP_ID_STRIDE + _d,
        )

# Register all possible blueprint locations (max slots) so location_name_to_id
# is stable across different blueprint_checks_per_biome settings.
for _biome_idx, _biome_name in enumerate(BIOME_NAMES):
    for _slot in range(MAX_CHEST_CHECKS_PER_BIOME):
        location_data_table[blueprint_chest_name(_biome_name, _slot)] = RogueLegacy2LocationData(
            region=_biome_name,
            address=BASE_ID + BLUEPRINT_OFFSET + _biome_idx * CHEST_SLOT_ID_STRIDE + _slot,
        )

# Register all possible fairy chest (rune) locations so location_name_to_id
# is stable across different rune_checks_per_biome settings.
for _biome_idx, _biome_name in enumerate(BIOME_NAMES):
    for _slot in range(MAX_CHEST_CHECKS_PER_BIOME):
        location_data_table[fairy_chest_name(_biome_name, _slot)] = RogueLegacy2LocationData(
            region=_biome_name,
            address=BASE_ID + RUNE_OFFSET + _biome_idx * CHEST_SLOT_ID_STRIDE + _slot,
        )

# Register all journal/memory locations so location_name_to_id is complete.
# create_regions() instantiates only the subset matching the journal_checks option.
for _bi, _biome_name in enumerate(BIOME_NAMES):
    if _JOURNAL_COUNTS[_bi] > 0:
        location_data_table[all_journals_read_name(_biome_name)] = RogueLegacy2LocationData(
            region=_biome_name,
            address=BASE_ID + JOURNAL_GROUPED_OFFSET + _bi,
        )
    if _MEMORY_COUNTS[_bi] > 0:
        location_data_table[all_memories_read_name(_biome_name)] = RogueLegacy2LocationData(
            region=_biome_name,
            address=BASE_ID + MEMORY_GROUPED_OFFSET + _bi,
        )
    for _j in range(_JOURNAL_COUNTS[_bi]):
        location_data_table[journal_entry_name(_biome_name, _j)] = RogueLegacy2LocationData(
            region=_biome_name,
            address=BASE_ID + JOURNAL_INDIVIDUAL_OFFSET + _bi * 16 + _j,
        )
    for _m in range(_MEMORY_COUNTS[_bi]):
        location_data_table[memory_fragment_name(_biome_name, _m)] = RogueLegacy2LocationData(
            region=_biome_name,
            address=BASE_ID + MEMORY_INDIVIDUAL_OFFSET + _bi * 16 + _m,
        )

# Register every possible mastery location, at the maximum level count, so
# location_name_to_id is complete and a given rank keeps the same ID at any
# TotalMasteryLevels / MasteryLevelsPerClass setting. create_regions() instantiates only the
# subset the chosen mode and level count call for, and none of them when mastery is disabled.
for _level in range(1, MAX_TOTAL_MASTERY_RANK + 1):
    location_data_table[total_mastery_location_name(_level)] = RogueLegacy2LocationData(
        region="Mastery",
        address=BASE_ID + MASTERY_TOTAL_OFFSET + _level - 1,
    )

for _pos in range(len(MASTERY_CLASS_NAMES)):
    for _level in range(1, MAX_MASTERY_RANK_PER_CLASS + 1):
        location_data_table[class_mastery_location_name(_pos, _level)] = RogueLegacy2LocationData(
            region="Mastery",
            address=BASE_ID + MASTERY_CLASS_OFFSET + _pos * MASTERY_CLASS_ID_STRIDE + _level - 1,
        )

# Convenience: name→ID dict used by World.location_name_to_id
locations_table: dict[str, int] = {
    name: data.address
    for name, data in location_data_table.items()
    if data.address is not None
}


def _add_event(region: Region, player: int, name: str) -> None:
    """Create an event location with a matching locked progression item."""
    loc = RogueLegacy2Location(player, name, None, region)
    loc.place_locked_item(Item(name, ItemClassification.progression, None, player))
    region.locations.append(loc)


def create_regions(world: "RogueLegacy2World") -> None:
    """Create all regions, add their locations, and wire up connections."""
    multiworld = world.multiworld
    player = world.player
    blueprint_n = world.options.blueprint_checks_per_biome.value
    rune_n = world.options.rune_checks_per_biome.value
    journal_mode = world.options.journal_checks.value

    # Manor layout: exactly one of the two manor location families is instantiated.
    shop_layout = world.options.manor_layout.value == ManorLayout.option_shop
    shop_depth = world.options.manor_shop_depth.value
    active_shop_names = {
        manor_shop_location_name(position, depth_index)
        for position in range(MANOR_SHOP_POSITIONS)
        for depth_index in range(shop_depth)
    } if shop_layout else set()

    # Mastery: which rank locations exist, and which of them are filler-only.
    #
    # The exclusion is the whole gating mechanism for this category. Nothing in the chain has
    # a requirement, so logic cannot express "rank 15 is thirty hours
    # of farming away from rank 1" - but LocationProgressType.EXCLUDED can promise that the
    # deep end never holds anything the player needs, and the fill algorithm enforces it
    # rather than the logic sweep. create_items() reserves one filler item per excluded
    # location, which is what makes that promise guaranteed affordable.
    mastery_mode = world.options.mastery_checks.value
    mastery_progression_pct = world.options.mastery_progression_percent.value
    active_mastery_names: set[str] = set()
    excluded_mastery_names: set[str] = set()

    if mastery_mode == MasteryChecks.option_total:
        mastery_levels = world.options.total_mastery_levels.value
        mastery_groups: list[list[str]] = [
            [total_mastery_location_name(level) for level in range(1, mastery_levels + 1)]
        ]
    elif mastery_mode == MasteryChecks.option_per_class:
        mastery_levels = world.options.mastery_levels_per_class.value
        mastery_groups = [
            [class_mastery_location_name(position, level) for level in range(1, mastery_levels + 1)]
            for position in range(len(MASTERY_CLASS_NAMES))
        ]
    else:
        mastery_levels = 0
        mastery_groups = []

    # Applied per group so that in per_class mode every class keeps the same shallow stretch
    # of live ranks, rather than the whole back half of the class list going filler-only.
    open_mastery_levels = math.floor(mastery_levels * mastery_progression_pct / 100)
    for group in mastery_groups:
        active_mastery_names.update(group)
        excluded_mastery_names.update(group[open_mastery_levels:])

    # The set of blueprint and rune location names active for this world.
    active_blueprint_names = {
        blueprint_chest_name(biome_name, slot)
        for biome_name in BIOME_NAMES
        for slot in range(blueprint_n)
    }
    active_rune_names = {
        fairy_chest_name(biome_name, slot)
        for biome_name in BIOME_NAMES
        for slot in range(rune_n)
    }

    # ── Build regions ────────────────────────────────────────────────────────
    region_names = {"Menu", "Overworld", "Throne Room", "Manor", "Mastery"} | set(BIOME_NAMES)
    regions: dict[str, Region] = {}
    for name in region_names:
        region = Region(name, player, multiworld)
        multiworld.regions.append(region)
        regions[name] = region

    # ── Assign locations to their regions ────────────────────────────────────
    for location_name, location_data in location_data_table.items():
        # Manor locations: the tree's 72 nodes and the shop's columns are mutually
        # exclusive, and the shop only instantiates down to the configured depth.
        if location_name in TREE_MANOR_LOCATION_NAME_SET and shop_layout:
            continue
        if location_name.startswith(MANOR_SHOP_NAME_PREFIX) and location_name not in active_shop_names:
            continue
        # Blueprint/rune locations: only instantiate those within configured limits.
        if "Blueprint Chest" in location_name and location_name not in active_blueprint_names:
            continue
        if "Fairy Chest" in location_name and location_name not in active_rune_names:
            continue
        # Journal/memory locations: only instantiate those matching the chosen mode.
        if "All Journals Read"  in location_name and journal_mode != 2: continue
        if "All Memories Read"  in location_name and journal_mode != 2: continue
        if "Journal Entry"      in location_name and journal_mode != 1: continue
        if "Memory Fragment"    in location_name and journal_mode != 1: continue
        # Mastery locations: the two modes are mutually exclusive, and each instantiates
        # only down to its configured level count.
        if location_name.startswith(MASTERY_NAME_PREFIX) and location_name not in active_mastery_names:
            continue
        region = regions[location_data.region]
        location = RogueLegacy2Location(
            player,
            location_name,
            location_data.address,
            region,
        )
        if location_name in excluded_mastery_names:
            location.progress_type = LocationProgressType.EXCLUDED
        region.locations.append(location)

    # ── Miniboss completion events ───────────────────────────────────────────
    # Events are address=None locations with a locked item placed on them.
    # They let the generator know that of any prerequisites which are not tied
    # the player having access to a particular item. The generator places the
    # locked location in a later sphere than its required events, so progression
    # items are never locked behind a check that requires them to already be cleared.
    _add_event(regions["Overworld"], player, LOC_MURMUR_CLEARED)
    _add_event(regions["Overworld"], player, LOC_GONGHEADS_CLEARED)
    _add_event(regions["Overworld"], player, LOC_BRIAREUS_COTTUS_CLEARED)
    _add_event(regions["Overworld"], player, LOC_GYGES_AEGAEON_CLEARED)

    # ── Boss completion events ───────────────────────────────────────────────
    _add_event(regions["Overworld"], player, LOC_ESTUARY_LAMECH_CLEARED)
    _add_event(regions["Overworld"], player, LOC_BYARRRITH_HALPHARR_CLEARED)
    _add_event(regions["Overworld"], player, LOC_ESTUARY_NAAMAH_CLEARED)
    _add_event(regions["Overworld"], player, LOC_ESTUARY_ENOCH_CLEARED)
    _add_event(regions["Overworld"], player, LOC_ESTUARY_IRAD_CLEARED)
    _add_event(regions["Overworld"], player, LOC_ESTUARY_TUBAL_CLEARED)
    _add_event(regions["Overworld"], player, LOC_JONAH_CLEARED)

    # ── Victory event ────────────────────────────────────────────────────────
    _add_event(regions["Throne Room"], player, "Victory")

    # ── Helper states ────────────────────────────────────────────────────────
    def _all_six_bosses_cleared(state) -> bool:
        return (
            state.has(LOC_ESTUARY_LAMECH_CLEARED, player) and
            state.has(LOC_BYARRRITH_HALPHARR_CLEARED, player) and
            state.has(LOC_ESTUARY_NAAMAH_CLEARED, player) and
            state.has(LOC_ESTUARY_ENOCH_CLEARED, player) and
            state.has(LOC_ESTUARY_IRAD_CLEARED, player) and
            state.has(LOC_ESTUARY_TUBAL_CLEARED, player)
        )

    # ── Access rules ─────────────────────────────────────────────────────────

    # Heirloom locations
    set_rule(
        multiworld.get_location(LOC_AETHERS_WINGS_STATUE, player),
        lambda state: state.has(HEIRLOOM_ECHOS_BOOTS, player),
    )
    set_rule(
        multiworld.get_location(LOC_PALLAS_VOID_BELL_STATUE, player),
        lambda state: state.has(HEIRLOOM_AETHERS_WINGS, player) or state.has(HEIRLOOM_PALLAS_VOID_BELL, player),
    )
    set_rule(
        multiworld.get_location(LOC_THEIAS_SUN_LANTERN_CONVERSATION, player),
        lambda state: state.has(LOC_ESTUARY_IRAD_CLEARED, player),
    )

    # Boss kills
    set_rule(
        multiworld.get_location(LOC_ESTUARY_LAMECH_DEFEATED, player),
        lambda state: state.has(HEIRLOOM_ANANKES_SHAWL, player) or state.has(HEIRLOOM_AETHERS_WINGS, player),
    )
    set_rule(
        multiworld.get_location(LOC_BYARRRITH_HALPHARR_DEFEATED, player),
        lambda state: state.has(HEIRLOOM_ECHOS_BOOTS, player),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_NAAMAH_DEFEATED, player),
        lambda state: state.has(HEIRLOOM_ECHOS_BOOTS, player) and state.has(HEIRLOOM_AETHERS_WINGS, player),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_ENOCH_DEFEATED, player),
        lambda state: (
            state.has(HEIRLOOM_PALLAS_VOID_BELL, player) and
            state.has(LOC_MURMUR_CLEARED, player) and
            state.has(LOC_GONGHEADS_CLEARED, player)
        ),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_IRAD_DEFEATED, player),
        lambda state: (
            state.has(HEIRLOOM_ANANKES_SHAWL, player) and
            state.has(HEIRLOOM_ECHOS_BOOTS, player) and
            state.has(HEIRLOOM_AETHERS_WINGS, player) and
            state.has(HEIRLOOM_PALLAS_VOID_BELL, player)
        ),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_TUBAL_DEFEATED, player),
        lambda state: (
            state.has(HEIRLOOM_THEIAS_SUN_LANTERN, player) and
            state.has(LOC_BRIAREUS_COTTUS_CLEARED, player) and
            state.has(LOC_GYGES_AEGAEON_CLEARED, player)
        ),
    )
    set_rule(
        multiworld.get_location(LOC_JONAH_DEFEATED, player),
        _all_six_bosses_cleared,
    )
    set_rule(
        multiworld.get_location("Victory", player),
        lambda state: state.has(LOC_JONAH_CLEARED, player),
    )

    # Miniboss Defeated checks
    set_rule(
        multiworld.get_location(LOC_MURMUR_DEFEATED, player),
        lambda state: state.has(HEIRLOOM_ECHOS_BOOTS, player) and state.has(HEIRLOOM_PALLAS_VOID_BELL, player),
    )
    set_rule(
        multiworld.get_location(LOC_GONGHEADS_DEFEATED, player),
        lambda state: state.has(HEIRLOOM_AETHERS_WINGS, player) or state.has(HEIRLOOM_PALLAS_VOID_BELL, player),
    )
    set_rule(
        multiworld.get_location(LOC_BRIAREUS_COTTUS_DEFEATED, player),
        lambda state: state.has(HEIRLOOM_THEIAS_SUN_LANTERN, player) and state.has(HEIRLOOM_ECHOS_BOOTS, player),
    )
    set_rule(
        multiworld.get_location(LOC_GYGES_AEGAEON_DEFEATED, player),
        lambda state: state.has(HEIRLOOM_PALLAS_VOID_BELL, player) and state.has(HEIRLOOM_THEIAS_SUN_LANTERN, player),
    )

    # Miniboss/boss Cleared events - same requirements as their Defeated checks.
    set_rule(
        multiworld.get_location(LOC_MURMUR_CLEARED, player),
        lambda state: state.has(HEIRLOOM_ECHOS_BOOTS, player) and state.has(HEIRLOOM_PALLAS_VOID_BELL, player),
    )
    set_rule(
        multiworld.get_location(LOC_GONGHEADS_CLEARED, player),
        lambda state: state.has(HEIRLOOM_AETHERS_WINGS, player) or state.has(HEIRLOOM_PALLAS_VOID_BELL, player),
    )
    set_rule(
        multiworld.get_location(LOC_BRIAREUS_COTTUS_CLEARED, player),
        lambda state: state.has(HEIRLOOM_THEIAS_SUN_LANTERN, player) and state.has(HEIRLOOM_ECHOS_BOOTS, player),
    )
    set_rule(
        multiworld.get_location(LOC_GYGES_AEGAEON_CLEARED, player),
        lambda state: state.has(HEIRLOOM_PALLAS_VOID_BELL, player) and state.has(HEIRLOOM_THEIAS_SUN_LANTERN, player),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_LAMECH_CLEARED, player),
        lambda state: state.has(HEIRLOOM_ANANKES_SHAWL, player) or state.has(HEIRLOOM_AETHERS_WINGS, player),
    )
    set_rule(
        multiworld.get_location(LOC_BYARRRITH_HALPHARR_CLEARED, player),
        lambda state: state.has(HEIRLOOM_ECHOS_BOOTS, player),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_NAAMAH_CLEARED, player),
        lambda state: state.has(HEIRLOOM_ECHOS_BOOTS, player) and state.has(HEIRLOOM_AETHERS_WINGS, player),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_ENOCH_CLEARED, player),
        lambda state: (
            state.has(HEIRLOOM_PALLAS_VOID_BELL, player) and
            state.has(LOC_MURMUR_CLEARED, player) and
            state.has(LOC_GONGHEADS_CLEARED, player)
        ),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_IRAD_CLEARED, player),
        lambda state: (
            state.has(HEIRLOOM_ANANKES_SHAWL, player) and
            state.has(HEIRLOOM_ECHOS_BOOTS, player) and
            state.has(HEIRLOOM_AETHERS_WINGS, player) and
            state.has(HEIRLOOM_PALLAS_VOID_BELL, player)
        ),
    )
    set_rule(
        multiworld.get_location(LOC_ESTUARY_TUBAL_CLEARED, player),
        lambda state: (
            state.has(HEIRLOOM_THEIAS_SUN_LANTERN, player) and
            state.has(LOC_BRIAREUS_COTTUS_CLEARED, player) and
            state.has(LOC_GYGES_AEGAEON_CLEARED, player)
        ),
    )
    set_rule(
        multiworld.get_location(LOC_JONAH_CLEARED, player),
        _all_six_bosses_cleared,
    )

    # ── Pizza girl teleporter purchase access rules ──────────────────────────
    set_rule(
        multiworld.get_location(LOC_KERGUELEN_PLATEAU_TELEPORTER_PURCHASE, player),
        lambda state, p=player: (
            state.has(HEIRLOOM_ECHOS_BOOTS, p) or
            state.has(TELEPORTER_KERGUELEN_PLATEAU, p)
        )
    )
    set_rule(
        multiworld.get_location(LOC_SUN_TOWER_TELEPORTER_PURCHASE, player),
        lambda state, p=player: (
            state.has(HEIRLOOM_ECHOS_BOOTS, p) or
            (state.has(HEIRLOOM_ANANKES_SHAWL, p) and state.has(HEIRLOOM_AETHERS_WINGS, p)) or
            state.has(TELEPORTER_SUN_TOWER, p)
        ),
    )
    _can_reach_pizza_girl = lambda state, p=player: (
        state.has(TELEPORTER_SUN_TOWER, p) or
        state.has(HEIRLOOM_ECHOS_BOOTS, p) or
        (state.has(HEIRLOOM_ANANKES_SHAWL, p) and state.has(HEIRLOOM_AETHERS_WINGS, p))
    )
    for _tp_name in [
        LOC_AXIS_MUNDI_TELEPORTER_PURCHASE,
        LOC_KERGUELEN_PLATEAU_TELEPORTER_PURCHASE,
        LOC_STYGIAN_STUDY_TELEPORTER_PURCHASE,
        LOC_SUN_TOWER_TELEPORTER_PURCHASE,
        LOC_PISHON_DRY_LAKE_TELEPORTER_PURCHASE,
    ]:
        add_rule(multiworld.get_location(_tp_name, player), _can_reach_pizza_girl)

    # ── Biome entrance rules (heirloom requirements to access each biome) ───────
    _biome_access_rules = {
        BIOME_CITADEL_AGARTHA:   None,  # always accessible
        BIOME_AXIS_MUNDI:        lambda state, p=player: (
            state.has(HEIRLOOM_ECHOS_BOOTS, p) or
            (state.has(HEIRLOOM_ANANKES_SHAWL, p) and state.has(HEIRLOOM_AETHERS_WINGS, p))
        ),
        BIOME_KERGUELEN_PLATEAU: lambda state, p=player: (
            state.has(HEIRLOOM_ECHOS_BOOTS, p) or
            state.has(TELEPORTER_KERGUELEN_PLATEAU, p)
        ),
        BIOME_STYGIAN_STUDY:     lambda state, p=player: (
            state.has(HEIRLOOM_AETHERS_WINGS, p) and state.has(HEIRLOOM_PALLAS_VOID_BELL, p)
        ),
        BIOME_SUN_TOWER:         lambda state, p=player: (
            state.has(HEIRLOOM_ANANKES_SHAWL, p) and state.has(HEIRLOOM_ECHOS_BOOTS, p) and
            state.has(HEIRLOOM_AETHERS_WINGS, p) and state.has(HEIRLOOM_PALLAS_VOID_BELL, p)
        ),
        BIOME_PISHON_DRY_LAKE:   lambda state, p=player: state.has(HEIRLOOM_THEIAS_SUN_LANTERN, p),
    }
    biome_entrances: dict[str, Entrance] = {}
    for biome_name in BIOME_NAMES:
        entrance = regions["Overworld"].connect(
            regions[biome_name],
            name=f"To {biome_name}",
            rule=_biome_access_rules[biome_name],
        )
        biome_entrances[biome_name] = entrance

    # ── Memory locations additionally require Aesop's Tome ───────────────────
    # Biome access is handled by the entrance rule above; this is the only
    # per-location rule needed on top of that.
    if journal_mode == 1:
        for bi, biome_name in enumerate(BIOME_NAMES):
            for m in range(_MEMORY_COUNTS[bi]):
                add_rule(
                    multiworld.get_location(memory_fragment_name(biome_name, m), player),
                    lambda state, p=player: state.has(HEIRLOOM_AESOPS_TOME, p),
                )
    elif journal_mode == 2:
        for bi, biome_name in enumerate(BIOME_NAMES):
            if _MEMORY_COUNTS[bi] > 0:
                add_rule(
                    multiworld.get_location(all_memories_read_name(biome_name), player),
                    lambda state, p=player: state.has(HEIRLOOM_AESOPS_TOME, p),
                )

    # ── Mastery rank requirements ────────────────────────────────────────────
    #
    # The region itself is unconditional. In the base game the Sage Totem gates mastery XP
    # accrual outright (EnemyHitResponse tests SkillTreeLogicHelper.IsTotemUnlocked before
    # awarding anything), but the mod lifts that so the whole category does not hang off one
    # manor item (see Patches/MasteryXPPatch.cs).
    #
    # That leaves the class requirements below as the only real logic on the chain, and
    # MasteryProgressionPercent as the only thing keeping an important item out of its far end.
    regions["Menu"].connect(regions["Mastery"], name="To Mastery")

    if mastery_mode == MasteryChecks.option_per_class:
        # A class's ranks need that class. The starting class is pre-unlocked, so its ranks
        # ride on the entrance rule alone.
        for position in range(len(MASTERY_CLASS_NAMES)):
            if position == world.starting_class_position:
                continue
            class_item = world.class_item_names[position]
            for level in range(1, mastery_levels + 1):
                add_rule(
                    multiworld.get_location(class_mastery_location_name(position, level), player),
                    lambda state, n=class_item, p=player: state.has(n, p),
                )

    elif mastery_mode == MasteryChecks.option_total:
        # An overall level needs enough classes to supply it, counting only
        # LOGICAL_MASTERY_RANKS_PER_CLASS from each rather than the 50 one class could technically
        # reach (see that constant for why logic refuses to expect a deep single-class grind).
        # Which classes is immaterial to the game, hence a count, and the count is taken over the
        # handful of class items the world picked for this job rather than all fourteen, so only
        # those have to be progression. See RogueLegacy2World._pick_mastery_required_classes.
        required_class_items = world.mastery_required_classes

        def _count_classes_unlocked(state) -> int:
            return 1 + sum(1 for name in required_class_items if state.has(name, player))

        # The subset was sized for exactly the deepest requirement, so this clamp is normally a
        # no-op. It bites once the level count passes what the assumed per-class budget can cover
        # across all 15 classes, at which point every class is named and the budget has to give.
        reachable_classes = 1 + len(required_class_items)

        for level in range(1, mastery_levels + 1):
            classes_req = min(
                math.ceil(level / LOGICAL_MASTERY_RANKS_PER_CLASS), reachable_classes)
            if classes_req <= 1:
                continue
            add_rule(
                multiworld.get_location(total_mastery_location_name(level), player),
                lambda state, n=classes_req: _count_classes_unlocked(state) >= n,
            )

    # The below rules aren't technical requirements, but they help ensure that
    # spheres are reasonably sized and the player isn't forced into technically
    # achievable tasks that would be unreasonably difficult

    # ── Progression-gating helpers ───────────────────────────────────────────
    def _count_bosses_cleared(state) -> int:
        return sum(1 for ev in _BOSS_CLEARED_EVENTS if state.has(ev, player))

    def _count_stat_upgrades(state) -> int:
        return sum(state.count(name, player) for name in _STAT_UPGRADE_ITEM_NAMES)

    # ── [1] Manor upgrade locations gated by boss-kill count ─────────────────
    # A percentage of the manor's full depth comes into logic per boss cleared.
    # The shallowest x% of depths are always in logic, the next x%
    # require 1 boss, and so on, capped at the number of bosses there are.
    manor_percent = world.options.manor_depth_percent_per_boss.value
    if manor_percent > 0:
        if shop_layout:
            max_depth = shop_depth
            manor_depths = {
                manor_shop_location_name(position, depth_index): depth_index + 1
                for position in range(MANOR_SHOP_POSITIONS)
                for depth_index in range(shop_depth)
            }
        else:
            max_depth = MAX_TREE_MANOR_DEPTH
            manor_depths = MANOR_LOCATION_DEPTHS

        # At least one tier per boss, so a small percentage on a shallow manor still
        # advances rather than dividing by zero.
        depths_per_boss = max(1, round(max_depth * manor_percent / 100))

        for location in list(regions["Manor"].locations):
            if location.address is None:
                continue
            depth = manor_depths.get(location.name, 1)
            bosses_req = min((depth - 1) // depths_per_boss, len(_BOSS_CLEARED_EVENTS))
            if bosses_req > 0:
                add_rule(location, lambda state, n=bosses_req: _count_bosses_cleared(state) >= n)

    # ── [2] Post-boss chest locations gated by biome boss kill ───────────────
    # The first floor(N * pct/100) blueprint/rune slots per biome are freely in
    # logic; the remaining slots aren't in logic until the biome's boss is cleared.
    # This is designed to prevent the player from potentially being forced to acquire
    # a large number of chest locations from a biome before its boss, reducing tedium
    # and splitting up the biome into better sized spheres.
    chest_pre_boss_pct = world.options.chest_pre_boss_percent.value
    if chest_pre_boss_pct < 100:
        for biome_name in BIOME_NAMES:
            boss_event    = _BIOME_BOSS_CLEARED_EVENTS[biome_name]
            biome_boss_rule = lambda state, ev=boss_event: state.has(ev, player)
            if blueprint_n > 0:
                pre_boss_bp = math.floor(blueprint_n * chest_pre_boss_pct / 100)
                for slot in range(pre_boss_bp, blueprint_n):
                    add_rule(
                        multiworld.get_location(blueprint_chest_name(biome_name, slot), player),
                        biome_boss_rule,
                    )
            if rune_n > 0:
                pre_boss_rune = math.floor(rune_n * chest_pre_boss_pct / 100)
                for slot in range(pre_boss_rune, rune_n):
                    add_rule(
                        multiworld.get_location(fairy_chest_name(biome_name, slot), player),
                        biome_boss_rule,
                    )

    # ── [3] Boss/miniboss defeat locations and cleared events gated by stat-upgrade count ──
    # Each boss requires a cumulative multiple of stat_upgrades_per_boss received
    # manor upgrade items. Minibosses use a threshold, one less than their area's main boss.
    # Cleared events are gated alongside their Defeated counterparts so that Jonah's
    # accessibility in logic matches the actual in-game requirement to defeat all bosses.
    stat_per_boss = world.options.stat_upgrades_per_boss.value
    if stat_per_boss > 0:
        _boss_stat_req_pairs: list[tuple[str, int]] = [
            (LOC_ESTUARY_LAMECH_DEFEATED,     1 * stat_per_boss),
            (LOC_ESTUARY_LAMECH_CLEARED,      1 * stat_per_boss),
            (LOC_BYARRRITH_HALPHARR_DEFEATED, 2 * stat_per_boss),
            (LOC_BYARRRITH_HALPHARR_CLEARED,  2 * stat_per_boss),
            (LOC_ESTUARY_NAAMAH_DEFEATED,     3 * stat_per_boss),
            (LOC_ESTUARY_NAAMAH_CLEARED,      3 * stat_per_boss),
            (LOC_MURMUR_DEFEATED,             3 * stat_per_boss),
            (LOC_MURMUR_CLEARED,              3 * stat_per_boss),
            (LOC_GONGHEADS_DEFEATED,          3 * stat_per_boss),
            (LOC_GONGHEADS_CLEARED,           3 * stat_per_boss),
            (LOC_ESTUARY_ENOCH_DEFEATED,      4 * stat_per_boss),
            (LOC_ESTUARY_ENOCH_CLEARED,       4 * stat_per_boss),
            (LOC_ESTUARY_IRAD_DEFEATED,       5 * stat_per_boss),
            (LOC_ESTUARY_IRAD_CLEARED,        5 * stat_per_boss),
            (LOC_BRIAREUS_COTTUS_DEFEATED,    5 * stat_per_boss),
            (LOC_BRIAREUS_COTTUS_CLEARED,     5 * stat_per_boss),
            (LOC_GYGES_AEGAEON_DEFEATED,      5 * stat_per_boss),
            (LOC_GYGES_AEGAEON_CLEARED,       5 * stat_per_boss),
            (LOC_ESTUARY_TUBAL_DEFEATED,      6 * stat_per_boss),
            (LOC_ESTUARY_TUBAL_CLEARED,       6 * stat_per_boss),
        ]
        for loc_name, req in _boss_stat_req_pairs:
            add_rule(
                multiworld.get_location(loc_name, player),
                lambda state, n=req: _count_stat_upgrades(state) >= n,
            )

    # ── [4] Biome entrances gated by stat-upgrade count ──────────────────────
    # Kerguelen=1x, Stygian=2x, Sun Tower=3x, Pishon=4x; Citadel and Axis Mundi
    # have no gating (tier 0). Gating the entrance covers all blueprint, rune,
    # and journal/memory locations in the biome automatically.
    stat_per_biome_tier = world.options.stat_upgrades_per_biome_tier.value
    if stat_per_biome_tier > 0:
        for biome_name in BIOME_NAMES:
            tier = _BIOME_STAT_UPGRADE_TIERS[biome_name]
            if tier == 0:
                continue
            stat_req = tier * stat_per_biome_tier
            add_rule(
                biome_entrances[biome_name],
                lambda state, n=stat_req: _count_stat_upgrades(state) >= n,
            )

    # ── [5] Late-game bosses and biome checks gated by NPC unlock count ──────
    # Used to impose soft rules that will encourage NPC unlocks to be earlier in the run
    if world.options.early_npc_unlocks.value:
        def _count_npcs_unlocked(state) -> int:
            return sum(1 for name in _NPC_ITEM_NAMES if state.has(name, player))

        for loc_name, req in _BOSS_NPC_REQS:
            add_rule(
                multiworld.get_location(loc_name, player),
                lambda state, n=req: _count_npcs_unlocked(state) >= n,
            )

        for biome_name in BIOME_NAMES:
            npc_req = _BIOME_NPC_TIERS[biome_name]
            if npc_req == 0:
                continue
            add_rule(
                biome_entrances[biome_name],
                lambda state, n=npc_req: _count_npcs_unlocked(state) >= n,
            )

    # ── Wire up region connections ───────────────────────────────────────────
    regions["Menu"].connect(regions["Overworld"])
    regions["Menu"].connect(regions["Manor"])
    regions["Overworld"].connect(regions["Throne Room"])
