import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

# All non-event item IDs are offset from this base.
# TODO: Register an official base ID with the Archipelago project before publishing.
BASE_ID = 0xBEEF0000


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
    # ── Progression ─────────────────────────────────────────────────────────
    # Placeholder: heirlooms gate access to later biomes.
    "Progression Item Placeholder": RogueLegacy2ItemData(code=BASE_ID + 1, classification=ItemClassification.progression),

    # ── Useful ──────────────────────────────────────────────────────────────
    "Useful Item Placeholder":      RogueLegacy2ItemData(code=BASE_ID + 2, classification=ItemClassification.useful),

    # ── Filler ──────────────────────────────────────────────────────────────
    "Filler Placeholder":           RogueLegacy2ItemData(code=BASE_ID + 3, classification=ItemClassification.filler),

    # ── Events (no ID, placed by the world at matching event locations) ──────
    "Victory":                      RogueLegacy2ItemData(code=None, classification=ItemClassification.progression),
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
    for name, data in item_data_table.items():
        if data.code is None:
            multiworld.get_location(name, player).place_locked_item(create_item(player, name))

    # Count how many regular (non-event) locations need to be filled.
    unfilled = len(multiworld.get_unfilled_locations(player))

    # Build the pool of non-event items.
    # Repeat Heirlooms and fillers until the pool size matches location count.
    pool: list[RogueLegacy2Item] = []

    # Two progression heirlooms, one useful skill point, then pad with Gold Pouches.
    pool.append(create_item(player, "Heirloom"))
    pool.append(create_item(player, "Heirloom"))
    pool.append(create_item(player, "Skill Point"))

    while len(pool) < unfilled:
        pool.append(create_item(player, "Gold Pouch"))

    multiworld.itempool += pool
