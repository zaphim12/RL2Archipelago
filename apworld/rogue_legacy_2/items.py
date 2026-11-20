import math
import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

from .constants import (
    BLUEPRINT_CATEGORIES,
    BLUEPRINT_TYPES,
    CLASS_UNLOCK_ITEM_NAMES,
    CORE_MANOR_UPGRADES,
    HEIRLOOM_NAMES,
    KNIGHT_CLASS_ITEM_NAME,
    KNIGHT_CLASS_POSITION,
    MANOR_MAX_LEVELS,
    NPC_MANOR_UPGRADES,
    PLAIN_CLASS_ITEM_NAMES,
    PROGRESSIVE_CLASS_ITEM_NAMES,
    RUNE_NAMES,
    STAT_UPGRADE_MANOR_UPGRADES,
    TELEPORTER_NAMES,
)

# All item and location IDs are offset from this base.
BASE_ID = 0x0BEEF000

HEIRLOOM_OFFSET   = 0x300
BLUEPRINT_OFFSET  = 0x400
RUNE_OFFSET       = 0x500
MANOR_OFFSET      = 0x600
TELEPORTER_OFFSET = 0x700
FILLER_OFFSET     = 0x800
TRAP_OFFSET       = 0x900
PROGRESSIVE_CLASS_OFFSET = 0xA00


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
    "Trap: Cannonball Rain",   # 0 - BridgeBiomeUp (Burden of Mundi's Flagship)
    "Trap: Dragon Lancers",    # 1 - TowerBiomeUp  (Burden of Irad's Torment)
    "Trap: Automaton Swarm",   # 2 - CaveBiomeUp   (Burden of Pishon's Uprising)
    "Trap: Giant Snowflakes",  # 3 - ForestBiomeUp (Burden of Kerguelen's Frost)
    "Trap: Void Waves",        # 4 - StudyBiomeUp  (Burden of the High Scholar's Metamorphosis)
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
# Indices 0-68 are core upgrades; indices 69-71 are NPC unlock slots.
for _i, _name in enumerate(CORE_MANOR_UPGRADES + NPC_MANOR_UPGRADES):
    item_data_table[_name] = RogueLegacy2ItemData(
        code=BASE_ID + MANOR_OFFSET + _i,
        classification=ItemClassification.useful,
    )

# Knight Class unlock item (index 72, after NPC slots 69-71).
# Unlike the other 14 classes Knight has no manor slot, so it needs an ID of its own.
item_data_table[KNIGHT_CLASS_ITEM_NAME] = RogueLegacy2ItemData(
    code=BASE_ID + MANOR_OFFSET + 72,
    classification=ItemClassification.useful,
)

# Progressive class items: the fabled_weapons = enabled counterpart to the 15 plain class
# items above. A seed generates one set or the other, never both, but Archipelago's 
# name->ID map is global and cannot vary per slot, so both are always registered here.
# ID = BASE_ID + PROGRESSIVE_CLASS_OFFSET + position, position 0-13 = ManorSlots 44-57,
# position 14 = Knight.
for _i, _name in enumerate(PROGRESSIVE_CLASS_ITEM_NAMES):
    item_data_table[_name] = RogueLegacy2ItemData(
        code=BASE_ID + PROGRESSIVE_CLASS_OFFSET + _i,
        classification=ItemClassification.useful,
    )

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

    unfilled = len(multiworld.get_unfilled_locations(player))

    bundle_size = max(1, world.options.manor_upgrade_bundle_size.value)
    core_names = CORE_MANOR_UPGRADES
    npc_names  = NPC_MANOR_UPGRADES

    # Stat-relevant Manor upgrades are used to adhere to stat-gated access rules for 
    # biomes/bosses to be considered in-logic. They must be marked as
    # progression_skip_balancing (not merely useful) so the fill algorithm places
    # them before the inaccessible_location_rules sweep; otherwise stat-gated
    # locations are locked to filler-only and can't be filled. When no stat gating
    # is configured, plain useful is fine and avoids unnecessary fill pressure.
    stat_gating_enabled = (
        world.options.stat_upgrades_per_biome_tier.value > 0
        or world.options.stat_upgrades_per_boss.value > 0
    )
    stat_upgrade_names = frozenset(STAT_UPGRADE_MANOR_UPGRADES)

    def manor_classification(name: str) -> ItemClassification:
        if stat_gating_enabled and name in stat_upgrade_names:
            return ItemClassification.progression_skip_balancing
        return ItemClassification.useful

    # starting_class_index: 0 = Knight, 1–14 = one of CLASS_UNLOCK_ITEM_NAMES. Translated into a
    # position in the 15-entry class lists, where Knight sits last rather than first.
    starting_class_idx = getattr(world, '_starting_class_index', 0)
    starting_class_pos = KNIGHT_CLASS_POSITION if starting_class_idx == 0 else starting_class_idx - 1

    # Which of the two parallel class item sets this seed uses. See constants.py.
    fabled_weapons = bool(world.options.fabled_weapons)
    class_item_names = PROGRESSIVE_CLASS_ITEM_NAMES if fabled_weapons else PLAIN_CLASS_ITEM_NAMES

    # The 14 class slots are handled by the dedicated block below, not by the generic
    # single-level manor loop, since which item represents a class depends on the option.
    class_slot_names = frozenset(CLASS_UNLOCK_ITEM_NAMES)

    # Priority: included ahead of every optional pool, and split into two tiers so that a
    # world with very few locations sheds the right items first (see the trim below).
    #
    # mandatory: items whose absence would make an access rule unsatisfiable. Heirlooms gate
    #            bosses and biomes outright, and the NPC unlocks do the same via the
    #            early_npc_unlocks count rules.
    # optional:  everything else that is normally guaranteed - teleporters (always an
    #            alternative to an heirloom, never the only way in), class unlocks, and the
    #            remaining single-level manor upgrades.
    early_npcs = world.options.early_npc_unlocks.value

    mandatory_items: list[RogueLegacy2Item] = [
        create_item(player, n) for n in HEIRLOOM_ITEM_NAMES
    ]
    optional_priority: list[RogueLegacy2Item] = [
        create_item(player, n) for n in TELEPORTER_ITEM_NAMES
    ]

    for n in npc_names:
        data = item_data_table[n]
        if early_npcs:
            mandatory_items.append(
                RogueLegacy2Item(n, ItemClassification.progression, data.code, player)
            )
        else:
            optional_priority.append(
                RogueLegacy2Item(n, manor_classification(n), data.code, player)
            )

    for name in core_names:
        if MANOR_MAX_LEVELS.get(name, 1) <= 1:
            if name in class_slot_names:
                continue  # see the class item block below
            data = item_data_table[name]
            optional_priority.append(
                RogueLegacy2Item(name, manor_classification(name), data.code, player)
            )

    # ── Class items ──────────────────────────────────────────────────────────
    #
    # One copy per class unlocks it. The starting class is pre-unlocked at run start,
    # so it contributes no unlock copy at all.
    #
    # With fabled_weapons on, each class gets one further copy that unlocks its fabled
    # weapon, including the starting class, whose single copy is therefore the weapon
    # rather than the class.
    for position, name in enumerate(class_item_names):
        copies = 1 if fabled_weapons else 0
        if position != starting_class_pos:
            copies += 1
        optional_priority.extend(create_item(player, name) for _ in range(copies))

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
    #  type by percentages (currently 25% blueprints, 25% runes, 50% manor)
    blueprint_pool: list[RogueLegacy2Item] = [create_item(player, n) for n in BLUEPRINT_ITEM_NAMES]
    rune_pool:      list[RogueLegacy2Item] = [create_item(player, n) for n in RUNE_ITEM_NAMES]
    manor_pool: list[RogueLegacy2Item] = []
    for name in core_names:
        max_level = MANOR_MAX_LEVELS.get(name, 1)
        if max_level <= 1:
            continue
        copies = max(1, math.ceil(max_level / bundle_size))
        data = item_data_table[name]
        classification = manor_classification(name)
        for _ in range(copies):
            manor_pool.append(RogueLegacy2Item(name, classification, data.code, player))

    # ── Fit the guaranteed items into the available locations ────────────────
    #
    # Everything above this point is "guaranteed": normally there are far more locations
    # than guaranteed items and nothing has to give. A very small world can invert that,
    # handing Archipelago more items than it has room for and surfacing an opaque FillError
    # deep inside Fill.py. Shed the least load-bearing items instead, in order: traps,
    # then the optional priority tier.
    if len(mandatory_items) > unfilled:
        raise Exception(
            f"Rogue Legacy 2: this YAML leaves only {unfilled} locations, but "
            f"{len(mandatory_items)} items are required to keep the seed completable "
            f"(6 heirlooms"
            + (" and 3 NPC unlocks, which early_npc_unlocks makes progression" if early_npcs else "")
            + "). Raise manor_shop_depth, blueprint_checks_per_biome, rune_checks_per_biome "
            f"or journal_checks."
        )

    overflow = len(mandatory_items) + len(optional_priority) + len(trap_items) - unfilled
    if overflow > 0:
        dropped_traps = min(overflow, len(trap_items))
        if dropped_traps:
            trap_items = trap_items[: len(trap_items) - dropped_traps]
            overflow -= dropped_traps
    if overflow > 0:
        # Float the stat upgrades to the front so the survivors are the ones the
        # stat_upgrades_* gates count, then drop from the back. The shuffle keeps which
        # class items are lost from being the same every seed, and mixes class unlocks
        # and fabled weapons together rather than always losing one kind first.
        world.random.shuffle(optional_priority)
        if stat_gating_enabled:
            optional_priority.sort(key=lambda item: item.name not in stat_upgrade_names)
        optional_priority = optional_priority[: len(optional_priority) - overflow]

    priority_items = mandatory_items + optional_priority

    # Traps are guaranteed slots; subtract them from remaining before the useful/filler split.
    remaining    = unfilled - len(priority_items) - len(trap_items)
    total_useful = len(blueprint_pool) + len(rune_pool) + len(manor_pool)

    if total_useful <= remaining:
        # All useful items fit.
        pool = priority_items + trap_items + blueprint_pool + rune_pool + manor_pool
        padding_candidates = []
    else:
        # Truncation: distribute remaining slots 25% blueprints / 25% runes / 50% manor.
        blueprint_slots = remaining // 4
        rune_slots      = remaining // 4
        manor_slots     = remaining - blueprint_slots - rune_slots

        world.random.shuffle(blueprint_pool)
        world.random.shuffle(rune_pool)
        world.random.shuffle(manor_pool)

        # Truncation must not cut stat-upgrade copies below what the stat gates
        # require, or generation fails outright. Float them to the front; the 
        # stable sort keeps the shuffled order within each group.
        if stat_gating_enabled:
            manor_pool.sort(key=lambda item: item.name not in stat_upgrade_names)

        pool = (
            priority_items
            + trap_items
            + blueprint_pool[:blueprint_slots]
            + rune_pool[:rune_slots]
            + manor_pool[:manor_slots]
        )
        # padding_candidates: leftover useful items not yet in pool
        padding_candidates = (
            blueprint_pool[blueprint_slots:]
            + rune_pool[rune_slots:]
            + manor_pool[manor_slots:]
        )

    # Fill any remaining slots using candidates that are not already in the pool.
    for item in padding_candidates:
        if len(pool) >= unfilled:
            break
        pool.append(RogueLegacy2Item(item.name, item.classification, item.code, player))

    # If we have used all actual item candidates, and still have open slots, fill the rest with "Gold Coins" grants
    while len(pool) < unfilled:
        pool.append(create_item(player, "Gold Coins"))

    # The trim above makes this a no-op; it is here so the "exactly one item per location"
    # contract is enforced at the one place that matters rather than inferred from the
    # arithmetic of every branch feeding into it.
    assert len(pool) == unfilled, f"item pool is {len(pool)} for {unfilled} locations"

    multiworld.itempool += pool
