import math
import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification, Location, Region
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
    NPC_MANOR_LOCATION_NAMES,
    NPC_MANOR_UPGRADES,
    STAT_UPGRADE_MANOR_UPGRADES,
    TELEPORTER_KERGUELEN_PLATEAU,
    TELEPORTER_SUN_TOWER,
)
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
BLUEPRINT_OFFSET     = 0x400
RUNE_OFFSET          = 0x500
MANOR_OFFSET         = 0x600
TELEPORTER_OFFSET    = 0x700

JOURNAL_GROUPED_OFFSET    = 0x800  # BASE_ID + offset + biomeIndex (0-5)
MEMORY_GROUPED_OFFSET     = 0x808  # BASE_ID + offset + biomeIndex (only 0, 2 active)
JOURNAL_INDIVIDUAL_OFFSET = 0x810  # BASE_ID + offset + biomeIndex * 16 + journalIndex
MEMORY_INDIVIDUAL_OFFSET  = 0x870  # BASE_ID + offset + biomeIndex * 16 + memoryIndex

_JOURNAL_COUNTS = [4, 4, 4, 6, 7, 7]  # Citadel Agartha, Axis Mundi, Kerguelen Plateau, Stygian Study, Sun Tower, Pishon Dry Lake
_MEMORY_COUNTS  = [4, 0, 5, 0, 0, 0]

# Blueprint IDs use a biome-stride layout so IDs are stable regardless of how
# many slots are enabled:  id = BASE_ID + BLUEPRINT_OFFSET + biomeIndex * 16 + slotIndex
# This mirrors LocationRegistry._checksPerBiome in the C# mod.
_MAX_BLUEPRINT_CHECKS_PER_BIOME = 16  # upper bound of the BlueprintChecksPerBiome option
_MAX_RUNE_CHECKS_PER_BIOME      = 16  # upper bound of the RuneChecksPerBiome option


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

# Register all possible blueprint locations (max slots) so location_name_to_id
# is stable across different blueprint_checks_per_biome settings.
for _biome_idx, _biome_name in enumerate(BIOME_NAMES):
    for _slot in range(_MAX_BLUEPRINT_CHECKS_PER_BIOME):
        location_data_table[blueprint_chest_name(_biome_name, _slot)] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + BLUEPRINT_OFFSET + _biome_idx * 16 + _slot,
        )

# Register all possible fairy chest (rune) locations so location_name_to_id
# is stable across different rune_checks_per_biome settings.
for _biome_idx, _biome_name in enumerate(BIOME_NAMES):
    for _slot in range(_MAX_RUNE_CHECKS_PER_BIOME):
        location_data_table[fairy_chest_name(_biome_name, _slot)] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + RUNE_OFFSET + _biome_idx * 16 + _slot,
        )

# Register all journal/memory locations so location_name_to_id is complete.
# create_regions() instantiates only the subset matching the journal_checks option.
for _bi, _biome_name in enumerate(BIOME_NAMES):
    if _JOURNAL_COUNTS[_bi] > 0:
        location_data_table[all_journals_read_name(_biome_name)] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + JOURNAL_GROUPED_OFFSET + _bi,
        )
    if _MEMORY_COUNTS[_bi] > 0:
        location_data_table[all_memories_read_name(_biome_name)] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + MEMORY_GROUPED_OFFSET + _bi,
        )
    for _j in range(_JOURNAL_COUNTS[_bi]):
        location_data_table[journal_entry_name(_biome_name, _j)] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + JOURNAL_INDIVIDUAL_OFFSET + _bi * 16 + _j,
        )
    for _m in range(_MEMORY_COUNTS[_bi]):
        location_data_table[memory_fragment_name(_biome_name, _m)] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + MEMORY_INDIVIDUAL_OFFSET + _bi * 16 + _m,
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
    region_names = {"Menu", "Overworld", "Throne Room", "Manor"}
    regions: dict[str, Region] = {}
    for name in region_names:
        region = Region(name, player, multiworld)
        multiworld.regions.append(region)
        regions[name] = region

    # ── Assign locations to their regions ────────────────────────────────────
    for location_name, location_data in location_data_table.items():
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
        region = regions[location_data.region]
        location = RogueLegacy2Location(
            player,
            location_name,
            location_data.address,
            region,
        )
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

    # ── Biome access rules (shared by blueprints, runes, journals, memories) ───
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

    # ── Blueprint chest access rules ─────────────────────────────────────────
    if blueprint_n > 0:
        for biome_name in BIOME_NAMES:
            rule = _biome_access_rules[biome_name]
            if rule is None:
                continue
            for slot in range(blueprint_n):
                set_rule(
                    multiworld.get_location(blueprint_chest_name(biome_name, slot), player),
                    rule,
                )

    # ── Fairy chest (rune) access rules ──────────────────────────────────────
    if rune_n > 0:
        for biome_name in BIOME_NAMES:
            rule = _biome_access_rules[biome_name]
            if rule is None:
                continue
            for slot in range(rune_n):
                set_rule(
                    multiworld.get_location(fairy_chest_name(biome_name, slot), player),
                    rule,
                )

    # ── Journal/memory access rules ───────────────────────────────────────────
    if journal_mode != 0:
        for bi, biome_name in enumerate(BIOME_NAMES):
            biome_rule = _biome_access_rules[biome_name]
            # Memories additionally require Aesop's Tome regardless of biome.
            if biome_rule is None:
                mem_rule = lambda state, p=player: state.has(HEIRLOOM_AESOPS_TOME, p)
            else:
                mem_rule = lambda state, p=player, r=biome_rule: r(state, p) and state.has(HEIRLOOM_AESOPS_TOME, p)
            if journal_mode == 1:  # individual entries
                if biome_rule is not None:
                    for j in range(_JOURNAL_COUNTS[bi]):
                        set_rule(
                            multiworld.get_location(journal_entry_name(biome_name, j), player),
                            biome_rule,
                        )
                for m in range(_MEMORY_COUNTS[bi]):
                    set_rule(
                        multiworld.get_location(memory_fragment_name(biome_name, m), player),
                        mem_rule,
                    )
            else:  # journal_mode == 2, grouped
                if biome_rule is not None and _JOURNAL_COUNTS[bi] > 0:
                    set_rule(
                        multiworld.get_location(all_journals_read_name(biome_name), player),
                        biome_rule,
                    )
                if _MEMORY_COUNTS[bi] > 0:
                    set_rule(
                        multiworld.get_location(all_memories_read_name(biome_name), player),
                        mem_rule,
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
    # Each group of 'x' depth tiers is gated behind one additional boss cleared.
    # Depths '1' thru 'x' are always in logic; depths 'x+1' thru '2x' require 1 boss; etc.
    manor_depths_per_boss = world.options.manor_depths_per_boss.value
    if manor_depths_per_boss > 0:
        for location in list(regions["Manor"].locations):
            if location.address is None:
                continue
            depth = MANOR_LOCATION_DEPTHS.get(location.name, 1)
            bosses_req = min((depth - 1) // manor_depths_per_boss, len(_BOSS_CLEARED_EVENTS))
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

    # ── [4] Biome chest and journal locations gated by stat-upgrade count ─────
    # Kerguelen=1x, Stygian=2x, Sun Tower=3x, Pishon=4x; Citadel and Axis Mundi
    # have no gating (tier 0). Applies to all active blueprint, rune, and
    # journal/memory locations in the affected biomes.
    stat_per_biome_tier = world.options.stat_upgrades_per_biome_tier.value
    if stat_per_biome_tier > 0:
        for biome_idx, biome_name in enumerate(BIOME_NAMES):
            tier = _BIOME_STAT_UPGRADE_TIERS[biome_name]
            if tier == 0:
                continue
            stat_req  = tier * stat_per_biome_tier
            stat_rule = lambda state, n=stat_req: _count_stat_upgrades(state) >= n
            if blueprint_n > 0:
                for slot in range(blueprint_n):
                    add_rule(
                        multiworld.get_location(blueprint_chest_name(biome_name, slot), player),
                        stat_rule,
                    )
            if rune_n > 0:
                for slot in range(rune_n):
                    add_rule(
                        multiworld.get_location(fairy_chest_name(biome_name, slot), player),
                        stat_rule,
                    )
            if journal_mode == 1:  # individual entries
                for j in range(_JOURNAL_COUNTS[biome_idx]):
                    add_rule(
                        multiworld.get_location(journal_entry_name(biome_name, j), player),
                        stat_rule,
                    )
                for m in range(_MEMORY_COUNTS[biome_idx]):
                    add_rule(
                        multiworld.get_location(memory_fragment_name(biome_name, m), player),
                        stat_rule,
                    )
            elif journal_mode == 2:  # grouped
                if _JOURNAL_COUNTS[biome_idx] > 0:
                    add_rule(
                        multiworld.get_location(all_journals_read_name(biome_name), player),
                        stat_rule,
                    )
                if _MEMORY_COUNTS[biome_idx] > 0:
                    add_rule(
                        multiworld.get_location(all_memories_read_name(biome_name), player),
                        stat_rule,
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

        for biome_idx, biome_name in enumerate(BIOME_NAMES):
            npc_req = _BIOME_NPC_TIERS[biome_name]
            if npc_req == 0:
                continue
            npc_rule = lambda state, n=npc_req: _count_npcs_unlocked(state) >= n
            if blueprint_n > 0:
                for slot in range(blueprint_n):
                    add_rule(
                        multiworld.get_location(blueprint_chest_name(biome_name, slot), player),
                        npc_rule,
                    )
            if rune_n > 0:
                for slot in range(rune_n):
                    add_rule(
                        multiworld.get_location(fairy_chest_name(biome_name, slot), player),
                        npc_rule,
                    )
            if journal_mode == 1:  # individual
                for j in range(_JOURNAL_COUNTS[biome_idx]):
                    add_rule(
                        multiworld.get_location(journal_entry_name(biome_name, j), player),
                        npc_rule,
                    )
                for m in range(_MEMORY_COUNTS[biome_idx]):
                    add_rule(
                        multiworld.get_location(memory_fragment_name(biome_name, m), player),
                        npc_rule,
                    )
            elif journal_mode == 2:  # grouped
                if _JOURNAL_COUNTS[biome_idx] > 0:
                    add_rule(
                        multiworld.get_location(all_journals_read_name(biome_name), player),
                        npc_rule,
                    )
                if _MEMORY_COUNTS[biome_idx] > 0:
                    add_rule(
                        multiworld.get_location(all_memories_read_name(biome_name), player),
                        npc_rule,
                    )

    # ── Wire up region connections ───────────────────────────────────────────
    regions["Menu"].connect(regions["Overworld"])
    regions["Menu"].connect(regions["Manor"])
    regions["Overworld"].connect(regions["Throne Room"])
