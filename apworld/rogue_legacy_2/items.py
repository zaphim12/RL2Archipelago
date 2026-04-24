import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

# All non-event item IDs are offset from this base.
# TODO: Register an official base ID with the Archipelago project before publishing.
BASE_ID = 0xBEEF0000

HEIRLOOM_OFFSET  = 0x300
BLUEPRINT_OFFSET = 0x400


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

    # ── Useful ──────────────────────────────────────────────────────────────
    "Useful Item Placeholder":      RogueLegacy2ItemData(code=BASE_ID + 2, classification=ItemClassification.useful),

    # ── Filler ──────────────────────────────────────────────────────────────
    "Filler Placeholder":           RogueLegacy2ItemData(code=BASE_ID + 3, classification=ItemClassification.filler),

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

HEIRLOOM_ITEM_NAMES = [
    "Ananke's Shawl",
    "Aether's Wings",
    "Aesop's Tome",
    "Echo's Boots",
    "Pallas' Void Bell",
    "Theia's Sun Lantern",
]

# Event items are placed at locations whose names differ from the item name.
# Matches must be kept in sync with the "address=None" entries in locations_and_regions.py.
event_item_to_location: dict[str, str] = {
    "Victory": "Castle Hamson - The Traitor Defeated",
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

    unfilled = len(multiworld.get_unfilled_locations(player))
    blueprint_n = world.options.blueprint_checks_per_biome.value
    pool: list[RogueLegacy2Item] = []
    for name in HEIRLOOM_ITEM_NAMES:
        pool.append(create_item(player, name))
    for name in BLUEPRINT_ITEM_NAMES:
        pool.append(create_item(player, name))
    while len(pool) < unfilled:
        pool.append(create_item(player, "Filler Placeholder"))
    multiworld.itempool += pool
