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


class RogueLegacy2Location(Location):
    game = "Rogue Legacy 2"


class RogueLegacy2LocationData(NamedTuple):
    region: str
    address: int | None = None   # None = event location


# ---------------------------------------------------------------------------
# Location table
#
# For now, the randomizer's only real checks are the 8 main boss defeats.
# "Victory" is an event (no address) placed at the final boss defeat.
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

    # ── Miniboss kills ───────────────────────────────────────────────────────
    "Stygian Study - Gongheads Miniboss Defeated":  RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 0),
    "Stygian Study - Murmur Miniboss Defeated":     RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 1),
    "Pishon Dry Lake - Briareus and Cottus Minibosses Defeated":      RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 2),
    "Pishon Dry Lake - Gyges and Aegaeon Minibosses Defeated":        RogueLegacy2LocationData(region="Overworld", address=BASE_ID + MINIBOSS_KILL_OFFSET + 3),

    # ── Victory event (placed by __init__.py at the Traitor fight) ───────────
    "Castle Hamson - The Traitor Defeated":         RogueLegacy2LocationData(region="Throne Room", address=None),
}

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

    # ── Build regions ────────────────────────────────────────────────────────
    region_names = {"Menu", "Overworld", "Throne Room"}
    regions: dict[str, Region] = {}
    for name in region_names:
        region = Region(name, player, multiworld)
        multiworld.regions.append(region)
        regions[name] = region

    # ── Assign locations to their regions ────────────────────────────────────
    for location_name, location_data in location_data_table.items():
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

    # ── Access rules ─────────────────────────────────────────────────────────
    # Estuary Enoch is locked behind both Study miniboss doors.
    set_rule(
        multiworld.get_location("Stygian Study - Estuary Enoch Defeated", player),
        lambda state: (
            state.has("Stygian Study - Murmur Miniboss Cleared", player) and
            state.has("Stygian Study - Gongheads Miniboss Cleared", player)
        ),
    )
    # Estuary Tubal is locked behind both Cave miniboss doors.
    set_rule(
        multiworld.get_location("Pishon Dry Lake - Estuary Tubal Defeated", player),
        lambda state: (
            state.has("Pishon Dry Lake - Briareus and Cottus Minibosses Cleared", player) and
            state.has("Pishon Dry Lake - Gyges and Aegaeon Minibosses Cleared", player)
        ),
    )

    # ── Wire up region connections ───────────────────────────────────────────
    regions["Menu"].connect(regions["Overworld"])
    regions["Overworld"].connect(regions["Throne Room"])
