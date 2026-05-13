import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification, Location, Region
from worlds.generic.Rules import set_rule

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

from .constants import BIOME_NAMES, CORE_MANOR_UPGRADES, NPC_MANOR_UPGRADES
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
    "Estuary Lamech Defeated":              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 0),
    "Byarrrith and Halpharr Defeated":      RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 1),
    "Estuary Naamah Defeated":              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 2),
    "Estuary Enoch Defeated":               RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 3),
    "Estuary Irad Defeated":                RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 4),
    "Estuary Tubal Defeated":               RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 5),
    "Jonah Defeated":                       RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 6),

    # ── Heirloom interactions ────────────────────────────────────────────────
    "Ananke's Shawl Statue":            RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 0),
    "Aether's Wings Statue":            RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 1),
    "Aesop's Tome Statue":              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 2),
    "Echo's Boots Statue":              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 3),
    "Pallas' Void Bell Statue":         RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 4),
    "Theia's Sun Lantern Conversation": RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 5),

    # ── Miniboss kills ───────────────────────────────────────────────────────
    "Gongheads Miniboss Defeated":                  RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 0),
    "Murmur Miniboss Defeated":                     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 1),
    "Briareus and Cottus Minibosses Defeated":      RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 2),
    "Gyges and Aegaeon Minibosses Defeated":        RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 3),

    # ── Pizza girl teleporter purchases ─────────────────────────────────────
    "Axis Mundi Teleporter Purchase":        RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 0),
    "Kerguelen Plateau Teleporter Purchase": RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 1),
    "Stygian Study Teleporter Purchase":     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 2),
    "Sun Tower Teleporter Purchase":         RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 3),
    "Pishon Dry Lake Teleporter Purchase":   RogueLegacy2LocationData(region="Overworld", address=BASE_ID + TELEPORTER_OFFSET + 4),

    # ── Victory event (placed by __init__.py at the Traitor fight) ───────────
    "The Traitor Defeated":         RogueLegacy2LocationData(region="Throne Room", address=None),
}

# Register all manor upgrade locations so location_name_to_id is complete.
# Core (indices 0-68) are always registered; NPC unlocks (69-71) are registered
# unconditionally here but only instantiated in create_regions when the option
# is enabled — same pattern as blueprint/rune pool sizing.
for _i, _name in enumerate(CORE_MANOR_UPGRADES + NPC_MANOR_UPGRADES):
    location_data_table[f"Manor - {_name}"] = RogueLegacy2LocationData(
        region="Manor",
        address=BASE_ID + MANOR_OFFSET + _i,
    )

# Register all possible blueprint locations (max slots) so location_name_to_id
# is stable across different blueprint_checks_per_biome settings.
for _biome_idx, _biome_name in enumerate(BIOME_NAMES):
    for _slot in range(_MAX_BLUEPRINT_CHECKS_PER_BIOME):
        location_data_table[f"{_biome_name} - Blueprint Chest {_slot + 1}"] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + BLUEPRINT_OFFSET + _biome_idx * 16 + _slot,
        )

# Register all possible fairy chest (rune) locations so location_name_to_id
# is stable across different rune_checks_per_biome settings.
for _biome_idx, _biome_name in enumerate(BIOME_NAMES):
    for _slot in range(_MAX_RUNE_CHECKS_PER_BIOME):
        location_data_table[f"{_biome_name} - Fairy Chest {_slot + 1}"] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + RUNE_OFFSET + _biome_idx * 16 + _slot,
        )

# Register all journal/memory locations so location_name_to_id is complete.
# create_regions() instantiates only the subset matching the journal_checks option.
for _bi, _biome_name in enumerate(BIOME_NAMES):
    if _JOURNAL_COUNTS[_bi] > 0:
        location_data_table[f"{_biome_name} - All Journals Read"] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + JOURNAL_GROUPED_OFFSET + _bi,
        )
    if _MEMORY_COUNTS[_bi] > 0:
        location_data_table[f"{_biome_name} - All Memories Read"] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + MEMORY_GROUPED_OFFSET + _bi,
        )
    for _j in range(_JOURNAL_COUNTS[_bi]):
        location_data_table[f"{_biome_name} - Journal Entry {_j + 1}"] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + JOURNAL_INDIVIDUAL_OFFSET + _bi * 16 + _j,
        )
    for _m in range(_MEMORY_COUNTS[_bi]):
        location_data_table[f"{_biome_name} - Memory Fragment {_m + 1}"] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + MEMORY_INDIVIDUAL_OFFSET + _bi * 16 + _m,
        )

# Convenience: name→ID dict used by World.location_name_to_id
all_non_event_locations_table: dict[str, int] = {
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
        f"{biome_name} - Blueprint Chest {slot + 1}"
        for biome_name in BIOME_NAMES
        for slot in range(blueprint_n)
    }
    active_rune_names = {
        f"{biome_name} - Fairy Chest {slot + 1}"
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
    _add_event(regions["Overworld"], player, "Stygian Study - Murmur Miniboss Cleared")
    _add_event(regions["Overworld"], player, "Stygian Study - Gongheads Miniboss Cleared")
    _add_event(regions["Overworld"], player, "Pishon Dry Lake - Briareus and Cottus Minibosses Cleared")
    _add_event(regions["Overworld"], player, "Pishon Dry Lake - Gyges and Aegaeon Minibosses Cleared")

    # ── Boss completion events ───────────────────────────────────────────────
    _add_event(regions["Overworld"], player, "Citadel Agartha - Estuary Lamech Cleared")
    _add_event(regions["Overworld"], player, "Axis Mundi - Void Beasts Cleared")
    _add_event(regions["Overworld"], player, "Kerguelen Plateau - Estuary Naamah Cleared")
    _add_event(regions["Overworld"], player, "Stygian Study - Estuary Enoch Cleared")
    _add_event(regions["Overworld"], player, "Sun Tower - Estuary Irad Cleared")
    _add_event(regions["Overworld"], player, "Pishon Dry Lake - Estuary Tubal Cleared")
    _add_event(regions["Overworld"], player, "Garden of Eden - Jonah Cleared")

    # ── Helper states ────────────────────────────────────────────────────────
    def _all_six_bosses_cleared(state) -> bool:
        return (
            state.has("Citadel Agartha - Estuary Lamech Cleared", player) and
            state.has("Axis Mundi - Void Beasts Cleared", player) and
            state.has("Kerguelen Plateau - Estuary Naamah Cleared", player) and
            state.has("Stygian Study - Estuary Enoch Cleared", player) and
            state.has("Sun Tower - Estuary Irad Cleared", player) and
            state.has("Pishon Dry Lake - Estuary Tubal Cleared", player)
        )

    # ── Access rules ─────────────────────────────────────────────────────────

    # Heirloom locations
    set_rule(
        multiworld.get_location("Aether's Wings Statue", player),
        lambda state: state.has("Echo's Boots", player),
    )
    set_rule(
        multiworld.get_location("Pallas' Void Bell Statue", player),
        lambda state: state.has("Aether's Wings", player) or state.has("Pallas' Void Bell", player),
    )
    set_rule(
        multiworld.get_location("Theia's Sun Lantern Conversation", player),
        lambda state: state.has("Sun Tower - Estuary Irad Cleared", player),
    )

    # Boss kills
    set_rule(
        multiworld.get_location("Estuary Lamech Defeated", player),
        lambda state: state.has("Ananke's Shawl", player) or state.has("Aether's Wings", player),
    )
    set_rule(
        multiworld.get_location("Byarrrith and Halpharr Defeated", player),
        lambda state: state.has("Echo's Boots", player),
    )
    set_rule(
        multiworld.get_location("Estuary Naamah Defeated", player),
        lambda state: state.has("Echo's Boots", player) and state.has("Aether's Wings", player),
    )
    set_rule(
        multiworld.get_location("Estuary Enoch Defeated", player),
        lambda state: (
            state.has("Pallas' Void Bell", player) and
            state.has("Stygian Study - Murmur Miniboss Cleared", player) and
            state.has("Stygian Study - Gongheads Miniboss Cleared", player)
        ),
    )
    set_rule(
        multiworld.get_location("Estuary Irad Defeated", player),
        lambda state: (
            state.has("Ananke's Shawl", player) and
            state.has("Echo's Boots", player) and
            state.has("Aether's Wings", player) and
            state.has("Pallas' Void Bell", player)
        ),
    )
    set_rule(
        multiworld.get_location("Estuary Tubal Defeated", player),
        lambda state: (
            state.has("Theia's Sun Lantern", player) and
            state.has("Pishon Dry Lake - Briareus and Cottus Minibosses Cleared", player) and
            state.has("Pishon Dry Lake - Gyges and Aegaeon Minibosses Cleared", player)
        ),
    )
    set_rule(
        multiworld.get_location("Jonah Defeated", player),
        _all_six_bosses_cleared,
    )
    set_rule(
        multiworld.get_location("The Traitor Defeated", player),
        lambda state: state.has("Garden of Eden - Jonah Cleared", player),
    )

    # Miniboss Defeated checks
    set_rule(
        multiworld.get_location("Murmur Miniboss Defeated", player),
        lambda state: state.has("Echo's Boots", player) and state.has("Pallas' Void Bell", player),
    )
    set_rule(
        multiworld.get_location("Gongheads Miniboss Defeated", player),
        lambda state: state.has("Aether's Wings", player) or state.has("Pallas' Void Bell", player),
    )
    set_rule(
        multiworld.get_location("Briareus and Cottus Minibosses Defeated", player),
        lambda state: state.has("Theia's Sun Lantern", player) and state.has("Echo's Boots", player),
    )
    set_rule(
        multiworld.get_location("Gyges and Aegaeon Minibosses Defeated", player),
        lambda state: state.has("Pallas' Void Bell", player) and state.has("Theia's Sun Lantern", player),
    )

    # Miniboss/boss Cleared events — same requirements as their Defeated checks.
    set_rule(
        multiworld.get_location("Stygian Study - Murmur Miniboss Cleared", player),
        lambda state: state.has("Echo's Boots", player) and state.has("Pallas' Void Bell", player),
    )
    set_rule(
        multiworld.get_location("Stygian Study - Gongheads Miniboss Cleared", player),
        lambda state: state.has("Aether's Wings", player) or state.has("Pallas' Void Bell", player),
    )
    set_rule(
        multiworld.get_location("Pishon Dry Lake - Briareus and Cottus Minibosses Cleared", player),
        lambda state: state.has("Theia's Sun Lantern", player) and state.has("Echo's Boots", player),
    )
    set_rule(
        multiworld.get_location("Pishon Dry Lake - Gyges and Aegaeon Minibosses Cleared", player),
        lambda state: state.has("Pallas' Void Bell", player) and state.has("Theia's Sun Lantern", player),
    )
    set_rule(
        multiworld.get_location("Citadel Agartha - Estuary Lamech Cleared", player),
        lambda state: state.has("Ananke's Shawl", player) or state.has("Aether's Wings", player),
    )
    set_rule(
        multiworld.get_location("Axis Mundi - Void Beasts Cleared", player),
        lambda state: state.has("Echo's Boots", player),
    )
    set_rule(
        multiworld.get_location("Kerguelen Plateau - Estuary Naamah Cleared", player),
        lambda state: state.has("Echo's Boots", player) and state.has("Aether's Wings", player),
    )
    set_rule(
        multiworld.get_location("Stygian Study - Estuary Enoch Cleared", player),
        lambda state: (
            state.has("Pallas' Void Bell", player) and
            state.has("Stygian Study - Murmur Miniboss Cleared", player) and
            state.has("Stygian Study - Gongheads Miniboss Cleared", player)
        ),
    )
    set_rule(
        multiworld.get_location("Sun Tower - Estuary Irad Cleared", player),
        lambda state: (
            state.has("Ananke's Shawl", player) and
            state.has("Echo's Boots", player) and
            state.has("Aether's Wings", player) and
            state.has("Pallas' Void Bell", player)
        ),
    )
    set_rule(
        multiworld.get_location("Pishon Dry Lake - Estuary Tubal Cleared", player),
        lambda state: (
            state.has("Theia's Sun Lantern", player) and
            state.has("Pishon Dry Lake - Briareus and Cottus Minibosses Cleared", player) and
            state.has("Pishon Dry Lake - Gyges and Aegaeon Minibosses Cleared", player)
        ),
    )
    set_rule(
        multiworld.get_location("Garden of Eden - Jonah Cleared", player),
        _all_six_bosses_cleared,
    )

    # ── Pizza girl teleporter purchase access rules ──────────────────────────
    set_rule(
        multiworld.get_location("Kerguelen Plateau Teleporter Purchase", player),
        lambda state, p=player: (
            state.has("Echo's Boots", p) or 
            state.has("Kerguelen Plateau Teleporter", p)
        )
    )
    set_rule(
        multiworld.get_location("Sun Tower Teleporter Purchase", player),
        lambda state, p=player: (
            state.has("Echo's Boots", p) or
            (state.has("Ananke's Shawl", p) and state.has("Aether's Wings", p)) or
            state.has("Sun Tower Teleporter", p)
        ),
    )

    # ── Biome access rules (shared by blueprints, runes, journals, memories) ───
    _biome_access_rules = {
        "Citadel Agartha":   None,  # always accessible
        "Axis Mundi":        lambda state, p=player: (
            state.has("Echo's Boots", p) or
            (state.has("Ananke's Shawl", p) and state.has("Aether's Wings", p))
        ),
        "Kerguelen Plateau": lambda state, p=player: (
            state.has("Echo's Boots", p) or
            state.has("Kerguelen Plateau Teleporter", p)
        ),
        "Stygian Study":     lambda state, p=player: (
            state.has("Aether's Wings", p) and state.has("Pallas' Void Bell", p)
        ),
        "Sun Tower":         lambda state, p=player: (
            state.has("Ananke's Shawl", p) and state.has("Echo's Boots", p) and
            state.has("Aether's Wings", p) and state.has("Pallas' Void Bell", p)
        ),
        "Pishon Dry Lake":   lambda state, p=player: state.has("Theia's Sun Lantern", p),
    }

    # ── Blueprint chest access rules ─────────────────────────────────────────
    if blueprint_n > 0:
        for biome_name in BIOME_NAMES:
            rule = _biome_access_rules[biome_name]
            if rule is None:
                continue
            for slot in range(blueprint_n):
                set_rule(
                    multiworld.get_location(f"{biome_name} - Blueprint Chest {slot + 1}", player),
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
                    multiworld.get_location(f"{biome_name} - Fairy Chest {slot + 1}", player),
                    rule,
                )

    # ── Journal/memory access rules ───────────────────────────────────────────
    if journal_mode != 0:
        for bi, biome_name in enumerate(BIOME_NAMES):
            biome_rule = _biome_access_rules[biome_name]
            # Memories additionally require Aesop's Tome regardless of biome.
            if biome_rule is None:
                mem_rule = lambda state, p=player: state.has("Aesop's Tome", p)
            else:
                mem_rule = lambda state, p=player, r=biome_rule: r(state, p) and state.has("Aesop's Tome", p)
            if journal_mode == 1:  # individual entries
                if biome_rule is not None:
                    for j in range(_JOURNAL_COUNTS[bi]):
                        set_rule(
                            multiworld.get_location(f"{biome_name} - Journal Entry {j + 1}", player),
                            biome_rule,
                        )
                for m in range(_MEMORY_COUNTS[bi]):
                    set_rule(
                        multiworld.get_location(f"{biome_name} - Memory Fragment {m + 1}", player),
                        mem_rule,
                    )
            else:  # journal_mode == 2, grouped
                if biome_rule is not None and _JOURNAL_COUNTS[bi] > 0:
                    set_rule(
                        multiworld.get_location(f"{biome_name} - All Journals Read", player),
                        biome_rule,
                    )
                if _MEMORY_COUNTS[bi] > 0:
                    set_rule(
                        multiworld.get_location(f"{biome_name} - All Memories Read", player),
                        mem_rule,
                    )

    # ── Wire up region connections ───────────────────────────────────────────
    regions["Menu"].connect(regions["Overworld"])
    regions["Menu"].connect(regions["Manor"])
    regions["Overworld"].connect(regions["Throne Room"])
