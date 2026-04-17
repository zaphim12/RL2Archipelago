import typing
from typing import NamedTuple

from BaseClasses import Location, Region

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

from .items import BASE_ID
BIOME_ID_OFFSET = 0x0
BIOME_ID_BASE = BASE_ID + BIOME_ID_OFFSET


class RogueLegacy2Location(Location):
    game = "Rogue Legacy 2"


class RogueLegacy2LocationData(NamedTuple):
    region: str
    address: int | None = None   # None = event location


# ---------------------------------------------------------------------------
# Location table
# Each biome-clear is one placeholder check per biome.
# "Victory" is an event (no address) placed in the Throne Room.
# ---------------------------------------------------------------------------
location_data_table: dict[str, RogueLegacy2LocationData] = {
    # ── Biome clears (one check each, all accessible from Overworld) ─────────
    "Castle Hamson - Clear":        RogueLegacy2LocationData(region="Overworld", address=BIOME_ID_BASE + 1),
    "Garden of Bloodlines - Clear": RogueLegacy2LocationData(region="Overworld", address=BIOME_ID_BASE + 2),
    "Axis of Envy - Clear":         RogueLegacy2LocationData(region="Overworld", address=BIOME_ID_BASE + 3),
    "Pishon Dry Lake - Clear":      RogueLegacy2LocationData(region="Overworld", address=BIOME_ID_BASE + 4),
    "Kerguelen Plateau - Clear":    RogueLegacy2LocationData(region="Overworld", address=BIOME_ID_BASE + 5),

    # ── Victory event (placed by __init__.py) ────────────────────────────────
    "Victory":                      RogueLegacy2LocationData(region="Throne Room", address=None),
}

# Convenience: name→ID dict used by World.location_name_to_id
all_non_event_locations_table: dict[str, int] = {
    name: data.address
    for name, data in location_data_table.items()
    if data.address is not None
}


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

    # ── Wire up region connections ───────────────────────────────────────────
    # Menu is always the entry point; everything else is freely accessible
    # for now.  Add access rules here as real gameplay logic is implemented.
    regions["Menu"].connect(regions["Overworld"])
    regions["Overworld"].connect(regions["Throne Room"])
