import math
import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

from .constants import (
    BLUEPRINT_CATEGORIES,
    BLUEPRINT_TYPES,
    CORE_MANOR_UPGRADES,
    HEIRLOOM_NAMES,
    MANOR_MAX_LEVELS,
    NPC_MANOR_UPGRADES,
    RUNE_NAMES,
    TELEPORTER_NAMES,
)

# All item IDs are offset from this base.
# TODO: Register an official base ID with the Archipelago project before publishing.
BASE_ID = 0xBEEF0000

HEIRLOOM_OFFSET   = 0x300
BLUEPRINT_OFFSET  = 0x400
RUNE_OFFSET       = 0x500
MANOR_OFFSET      = 0x600
TELEPORTER_OFFSET = 0x700
FILLER_OFFSET     = 0x800
TRAP_OFFSET       = 0x900


class RogueLegacy2Item(Item):
    game = "Rogue Legacy 2"


class RogueLegacy2ItemData(NamedTuple):
    code: int | None = None
    classification: ItemClassification = ItemClassification.filler


# ---------------------------------------------------------------------------
# Item table
# ---------------------------------------------------------------------------
item_data_table: dict[str, RogueLegacy2ItemData] = {
    # ── Filler ───────────────────────────────────────────────────────────────
    "Gold Coins": RogueLegacy2ItemData(code=BASE_ID + FILLER_OFFSET + 0, classification=ItemClassification.filler),

}

# Trap items: NG+ burden hazards activated as Archipelago traps.
# IDs MUST stay in lockstep with ItemRegistry.TRAP_OFFSET indices in C#.
_TRAP_NAMES = [
    "Trap: Cannonball Rain",   # 0 — BridgeBiomeUp (Burden of Mundi's Flagship)
    "Trap: Dragon Lancers",    # 1 — TowerBiomeUp  (Burden of Irad's Torment)
    "Trap: Automaton Swarm",   # 2 — CaveBiomeUp   (Burden of Pishon's Uprising)
    "Trap: Giant Snowflakes",  # 3 — ForestBiomeUp (Burden of Kerguelen's Frost)
    "Trap: Void Waves",        # 4 — StudyBiomeUp  (Burden of the High Scholar's Metamorphosis)
]

TRAP_ITEM_NAMES: list[str] = []
for _i, _name in enumerate(_TRAP_NAMES):
    item_data_table[_name] = RogueLegacy2ItemData(
        code=BASE_ID + TRAP_OFFSET + _i,
        classification=ItemClassification.trap,
    )
    TRAP_ITEM_NAMES.append(_name)

# Heirloom items (progression)
HEIRLOOM_ITEM_NAMES: list[str] = []
for _i, _name in enumerate(HEIRLOOM_NAMES):
    item_data_table[_name] = RogueLegacy2ItemData(
        code=BASE_ID + HEIRLOOM_OFFSET + _i,
        classification=ItemClassification.progression,
    )
    HEIRLOOM_ITEM_NAMES.append(_name)

# Teleporter unlock items (useful)
# Ordered to match ItemRegistry.TELEPORTER_OFFSET indices in C#.
TELEPORTER_ITEM_NAMES: list[str] = []
for _i, _name in enumerate(TELEPORTER_NAMES):
    item_data_table[_name] = RogueLegacy2ItemData(
        code=BASE_ID + TELEPORTER_OFFSET + _i,
        classification=ItemClassification.useful,
    )
    TELEPORTER_ITEM_NAMES.append(_name)

# Blueprint items: one per (EquipmentCategoryType, EquipmentType) pair.
# ID = BASE_ID + BLUEPRINT_OFFSET + categoryIndex * 16 + typeIndex
#   categoryIndex: Weapon=0, Head=1, Chest=2, Cape=3, Trinket=4
#   typeIndex:     Leather=0 .. Black Root=12
BLUEPRINT_ITEM_NAMES: list[str] = []
for _c, _cat in enumerate(BLUEPRINT_CATEGORIES):
    for _t, _type in enumerate(BLUEPRINT_TYPES):
        _item_name = f"{_type} {_cat} Blueprint"
        item_data_table[_item_name] = RogueLegacy2ItemData(
            code=BASE_ID + BLUEPRINT_OFFSET + _c * 16 + _t,
            classification=ItemClassification.useful,
        )
        BLUEPRINT_ITEM_NAMES.append(_item_name)

# Rune items: one per RuneType, ordered to match the C# ToRuneType() index mapping.
# ID = BASE_ID + RUNE_OFFSET + runeIndex  (0–23)
RUNE_ITEM_NAMES: list[str] = []
for _i, _rune in enumerate(RUNE_NAMES):
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
MANOR_ITEM_NAMES: list[str] = []
for _i, _name in enumerate(CORE_MANOR_UPGRADES + NPC_MANOR_UPGRADES):
    _item_name = f"Manor: {_name}"
    item_data_table[_item_name] = RogueLegacy2ItemData(
        code=BASE_ID + MANOR_OFFSET + _i,
        classification=ItemClassification.useful,
    )
    MANOR_ITEM_NAMES.append(_item_name)

# Convenience: name→ID dict used by World.item_name_to_id
items_table: dict[str, int] = {
    name: data.code
    for name, data in item_data_table.items()
    if data.code is not None
}


def create_item(player: int, name: str) -> RogueLegacy2Item:
    data = item_data_table[name]
    return RogueLegacy2Item(name, data.classification, data.code, player)


def create_items(world: "RogueLegacy2World") -> None:
    """Fill the multiworld item pool to match the number of available locations."""
    multiworld = world.multiworld
    player = world.player

    randomize_npc = bool(world.options.randomize_npc_unlocks.value)

    # When NPC unlocks are not randomized, lock each NPC item to its home
    # location so the server always returns it there on check.
    if not randomize_npc:
        for npc_name in NPC_MANOR_UPGRADES:
            loc = multiworld.get_location(f"Manor - {npc_name}", player)
            loc.place_locked_item(create_item(player, f"Manor: {npc_name}"))

    unfilled = len(multiworld.get_unfilled_locations(player))

    bundle_size = max(1, world.options.manor_upgrade_bundle_size.value)
    useful_count = world.options.manor_useful_count.value
    core_names = MANOR_ITEM_NAMES[:len(CORE_MANOR_UPGRADES)]
    npc_names  = MANOR_ITEM_NAMES[len(CORE_MANOR_UPGRADES):]

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

    # Traps: placed immediately after priority items, before useful items.
    # Distribute evenly across the 5 trap types by cycling through the list.
    raw_trap_count = world.options.trap_count.value
    trap_items: list[RogueLegacy2Item] = []
    if raw_trap_count > 0:
        shuffled_traps = world.random.sample(TRAP_ITEM_NAMES, len(TRAP_ITEM_NAMES))
        trap_cycle = (shuffled_traps * math.ceil(raw_trap_count / len(TRAP_ITEM_NAMES)))[:raw_trap_count]
        trap_items = [create_item(player, n) for n in trap_cycle]

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

    # Traps are guaranteed slots; subtract them from remaining before the useful/filler split.
    remaining    = unfilled - len(priority_items) - len(trap_items)
    total_useful = len(blueprint_pool) + len(rune_pool) + len(useful_manor_pool)

    world.random.shuffle(filler_manor_pool)

    if total_useful <= remaining:
        # All useful items fit; fill leftover slots with filler manor items.
        pool = priority_items + trap_items + blueprint_pool + rune_pool + useful_manor_pool
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
            + trap_items
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
