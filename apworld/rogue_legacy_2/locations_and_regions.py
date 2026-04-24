import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification, Location, Region
from worlds.generic.Rules import set_rule

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

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

# Blueprint IDs use a biome-stride layout so IDs are stable regardless of how
# many slots are enabled:  id = BASE_ID + BLUEPRINT_OFFSET + biomeIndex * 16 + slotIndex
# This mirrors LocationRegistry._checksPerBiome in the C# mod.
_BIOME_NAMES = [
    "Citadel Agartha",
    "Axis Mundi",
    "Kerguelen Plateau",
    "Stygian Study",
    "Sun Tower",
    "Pishon Dry Lake",
]
_MAX_BLUEPRINT_CHECKS_PER_BIOME = 16  # upper bound of the BlueprintChecksPerBiome option


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
    "Citadel Agartha - Estuary Lamech Defeated":    RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 0),
    "Axis Mundi - Void Beasts Defeated":            RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 1),
    "Kerguelen Plateau - Estuary Naamah Defeated":  RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 2),
    "Stygian Study - Estuary Enoch Defeated":       RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 3),
    "Sun Tower - Estuary Irad Defeated":            RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 4),
    "Pishon Dry Lake - Estuary Tubal Defeated":     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 5),
    "Garden of Eden - Jonah Defeated":              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + BOSS_KILL_OFFSET + 6),

    # ── Heirloom interactions ────────────────────────────────────────────────
    "Citadel Agartha - Ananke's Shawl":       RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 0),
    "Kerguelen Plateau - Aether's Wings":     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 1),
    "Citadel Agartha - Aesop's Tome":         RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 2),
    "Axis Mundi - Echo's Boots":              RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 3),
    "Stygian Study - Pallas' Void Bell":      RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 4),
    "Pishon Dry Lake - Theia's Sun Lantern":  RogueLegacy2LocationData(region="Overworld", address=BASE_ID + HEIRLOOM_OFFSET + 5),

    # ── Miniboss kills ───────────────────────────────────────────────────────
    "Stygian Study - Gongheads Miniboss Defeated":  RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 0),
    "Stygian Study - Murmur Miniboss Defeated":     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 1),
    "Pishon Dry Lake - Briareus and Cottus Minibosses Defeated":      RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 2),
    "Pishon Dry Lake - Gyges and Aegaeon Minibosses Defeated":        RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 3),

    # ── Victory event (placed by __init__.py at the Traitor fight) ───────────
    "Castle Hamson - The Traitor Defeated":         RogueLegacy2LocationData(region="Throne Room", address=None),
}

# Register all possible blueprint locations (max slots) so location_name_to_id
# is stable across different blueprint_checks_per_biome settings.
for _biome_idx, _biome_name in enumerate(_BIOME_NAMES):
    for _slot in range(_MAX_BLUEPRINT_CHECKS_PER_BIOME):
        location_data_table[f"{_biome_name} - Blueprint Chest {_slot + 1}"] = RogueLegacy2LocationData(
            region="Overworld",
            address=BASE_ID + BLUEPRINT_OFFSET + _biome_idx * 16 + _slot,
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

    # The set of blueprint location names active for this world.
    active_blueprint_names = {
        f"{biome_name} - Blueprint Chest {slot + 1}"
        for biome_name in _BIOME_NAMES
        for slot in range(blueprint_n)
    }

    # ── Build regions ────────────────────────────────────────────────────────
    region_names = {"Menu", "Overworld", "Throne Room"}
    regions: dict[str, Region] = {}
    for name in region_names:
        region = Region(name, player, multiworld)
        multiworld.regions.append(region)
        regions[name] = region

    # ── Assign locations to their regions ────────────────────────────────────
    for location_name, location_data in location_data_table.items():
        # Blueprint locations: only instantiate those within the configured limit.
        if "Blueprint Chest" in location_name and location_name not in active_blueprint_names:
            continue
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
        multiworld.get_location("Kerguelen Plateau - Aether's Wings", player),
        lambda state: state.has("Echo's Boots", player),
    )
    set_rule(
        multiworld.get_location("Stygian Study - Pallas' Void Bell", player),
        lambda state: state.has("Aether's Wings", player) or state.has("Pallas' Void Bell", player),
    )
    set_rule(
        multiworld.get_location("Pishon Dry Lake - Theia's Sun Lantern", player),
        lambda state: state.has("Sun Tower - Estuary Irad Cleared", player),
    )

    # Boss kills
    set_rule(
        multiworld.get_location("Citadel Agartha - Estuary Lamech Defeated", player),
        lambda state: state.has("Ananke's Shawl", player) or state.has("Aether's Wings", player),
    )
    set_rule(
        multiworld.get_location("Axis Mundi - Void Beasts Defeated", player),
        lambda state: state.has("Echo's Boots", player),
    )
    set_rule(
        multiworld.get_location("Kerguelen Plateau - Estuary Naamah Defeated", player),
        lambda state: state.has("Echo's Boots", player) and state.has("Aether's Wings", player),
    )
    set_rule(
        multiworld.get_location("Stygian Study - Estuary Enoch Defeated", player),
        lambda state: (
            state.has("Pallas' Void Bell", player) and
            state.has("Stygian Study - Murmur Miniboss Cleared", player) and
            state.has("Stygian Study - Gongheads Miniboss Cleared", player)
        ),
    )
    set_rule(
        multiworld.get_location("Sun Tower - Estuary Irad Defeated", player),
        lambda state: (
            state.has("Ananke's Shawl", player) and
            state.has("Echo's Boots", player) and
            state.has("Aether's Wings", player) and
            state.has("Pallas' Void Bell", player)
        ),
    )
    set_rule(
        multiworld.get_location("Pishon Dry Lake - Estuary Tubal Defeated", player),
        lambda state: (
            state.has("Theia's Sun Lantern", player) and
            state.has("Pishon Dry Lake - Briareus and Cottus Minibosses Cleared", player) and
            state.has("Pishon Dry Lake - Gyges and Aegaeon Minibosses Cleared", player)
        ),
    )
    set_rule(
        multiworld.get_location("Garden of Eden - Jonah Defeated", player),
        _all_six_bosses_cleared,
    )
    set_rule(
        multiworld.get_location("Castle Hamson - The Traitor Defeated", player),
        lambda state: state.has("Garden of Eden - Jonah Cleared", player),
    )

    # Miniboss Defeated checks
    set_rule(
        multiworld.get_location("Stygian Study - Murmur Miniboss Defeated", player),
        lambda state: state.has("Echo's Boots", player) and state.has("Pallas' Void Bell", player),
    )
    set_rule(
        multiworld.get_location("Stygian Study - Gongheads Miniboss Defeated", player),
        lambda state: state.has("Aether's Wings", player) or state.has("Pallas' Void Bell", player),
    )
    set_rule(
        multiworld.get_location("Pishon Dry Lake - Briareus and Cottus Minibosses Defeated", player),
        lambda state: state.has("Theia's Sun Lantern", player) and state.has("Echo's Boots", player),
    )
    set_rule(
        multiworld.get_location("Pishon Dry Lake - Gyges and Aegaeon Minibosses Defeated", player),
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

    # ── Blueprint chest access rules (biome access) ──────────────────────────
    # Kerguelen teleporter is not yet implemented as an AP item;
    # Echo's Boots is used as the access proxy in the meantime.
    _blueprint_biome_rules = {
        "Citadel Agartha":   None,  # always accessible
        "Axis Mundi":        lambda state, p=player: (
            state.has("Echo's Boots", p) or
            (state.has("Ananke's Shawl", p) and state.has("Aether's Wings", p))
        ),
        "Kerguelen Plateau": lambda state, p=player: state.has("Echo's Boots", p),
        "Stygian Study":     lambda state, p=player: (
            state.has("Aether's Wings", p) and state.has("Pallas' Void Bell", p)
        ),
        "Sun Tower":         lambda state, p=player: (
            state.has("Ananke's Shawl", p) and state.has("Echo's Boots", p) and
            state.has("Aether's Wings", p) and state.has("Pallas' Void Bell", p)
        ),
        "Pishon Dry Lake":   lambda state, p=player: state.has("Theia's Sun Lantern", p),
    }
    if blueprint_n > 0:
        for biome_name in _BIOME_NAMES:
            rule = _blueprint_biome_rules[biome_name]
            if rule is None:
                continue
            for slot in range(blueprint_n):
                set_rule(
                    multiworld.get_location(f"{biome_name} - Blueprint Chest {slot + 1}", player),
                    rule,
                )

    # ── Wire up region connections ───────────────────────────────────────────
    regions["Menu"].connect(regions["Overworld"])
    regions["Overworld"].connect(regions["Throne Room"])
