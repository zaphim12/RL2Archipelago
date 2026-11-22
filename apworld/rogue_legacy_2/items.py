import math
import typing
from typing import NamedTuple

from BaseClasses import Item, ItemClassification, LocationProgressType

if typing.TYPE_CHECKING:
    from . import RogueLegacy2World

from .constants import (
    BLUEPRINT_CATEGORIES,
    BLUEPRINT_TYPES,
    CLASS_UNLOCK_ITEM_NAMES,
    CORE_MANOR_UPGRADES,
    HEIRLOOM_NAMES,
    KNIGHT_CLASS_ITEM_NAME,
    MANOR_MAX_LEVELS,
    NPC_MANOR_UPGRADES,
    PROGRESSIVE_CLASS_ITEM_NAMES,
    RUNE_NAMES,
    STAT_UPGRADE_MANOR_UPGRADES,
    TELEPORTER_NAMES,
)
from .options import FabledWeapons

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

    # Class unlocks are the only items a mastery rule requires in order to be fulfilled,
    # and a named item has to be marked as progression for Archipelago to enforce a rule
    # during generation logic. We ensure that we only provide the shortest list of classes
    # that need to be marked as progression to satisfy its rules (see 
    # RogueLegacy2World._pick_mastery_required_classes). So in total mode
    # this is usually one or two classes rather than all fourteen.
    #
    # The Sage Totem is deliberately not among them. It gates mastery XP accrual in the base game,
    # but the mod lifts that (Patches/MasteryXPPatch.cs) so the category does not hang off one manor
    # item, which leaves it an ordinary upgrade here.
    mastery_required_classes = frozenset(world.mastery_required_classes)

    def manor_classification(name: str) -> ItemClassification:
        if stat_gating_enabled and name in stat_upgrade_names:
            return ItemClassification.progression_skip_balancing
        return ItemClassification.useful

    starting_class_pos = world.starting_class_position
    class_item_names = world.class_item_names

    # Whether this seed's class items are the two-stage progressive ones, which is also the
    # only mode that puts a fabled weapon in the pool.
    fabled_weapons = world.options.fabled_weapons == FabledWeapons.option_randomized

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
    #
    # Exactly one copy of a class a mastery rule names is progression, because the rule is a
    # state.has() that asks for one. The fabled weapon copy gates nothing at all, and neither does
    # anything the starting class holds, since its ranks are reachable from the start. Which object
    # carries the classification does not matter: in game the player's first received copy unlocks
    # the class whichever one it was, and logic only waits on the copy it can see.
    for position, name in enumerate(class_item_names):
        copies = 1 if fabled_weapons else 0
        if position != starting_class_pos:
            copies += 1

        data = item_data_table[name]
        unlock_is_progression = (
            position != starting_class_pos and name in mastery_required_classes
        )
        for copy_index in range(copies):
            optional_priority.append(RogueLegacy2Item(
                name,
                ItemClassification.progression_skip_balancing
                if copy_index == 0 and unlock_is_progression
                else ItemClassification.useful,
                data.code,
                player,
            ))

    # ── Filler for the excluded mastery ranks ────────────────────────────────
    #
    # distribute_items_restrictive fills excluded locations from the filler pool alone, and
    # raises "Not enough filler items for excluded locations" if it runs short - so marking
    # the deep end of the mastery chain filler-only obliges this world to supply that much
    # filler. Counted off the locations themselves rather than recomputed from the options,
    # so the two can't disagree.
    excluded_count = sum(
        1 for location in multiworld.get_unfilled_locations(player)
        if location.progress_type == LocationProgressType.EXCLUDED
    )
    reserved_filler_items: list[RogueLegacy2Item] = [
        create_item(player, "Gold Coins") for _ in range(excluded_count)
    ]

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
    # then the optional priority tier. The reserved filler is never shed: dropping it would
    # trade this clear error for the FillError it exists to prevent.
    if len(mandatory_items) + len(reserved_filler_items) > unfilled:
        raise Exception(
            f"Rogue Legacy 2: this YAML leaves only {unfilled} locations, but "
            f"{len(mandatory_items) + len(reserved_filler_items)} items are required to keep the seed "
            f"completable (6 heirlooms"
            + (", 3 NPC unlocks, which early_npc_unlocks makes progression" if early_npcs else "")
            + (f" and {len(reserved_filler_items)} filler items for the mastery ranks that "
               f"mastery_progression_percent excludes" if reserved_filler_items else "")
            + "). Raise manor_shop_depth, blueprint_checks_per_biome, rune_checks_per_biome "
            f"or journal_checks."
        )

    overflow = (
        len(mandatory_items) + len(optional_priority) + len(trap_items) + len(reserved_filler_items)
        - unfilled
    )
    if overflow > 0:
        dropped_traps = min(overflow, len(trap_items))
        if dropped_traps:
            trap_items = trap_items[: len(trap_items) - dropped_traps]
            overflow -= dropped_traps
    if overflow > 0:
        # Float the items the access rules actually count to the front so the survivors are
        # those, then drop from the back. The shuffle keeps which class items are lost from
        # being the same every seed, and mixes class unlocks and fabled weapons together
        # rather than always losing one kind first.
        #
        # Protection is by name, so a named class keeps its fabled weapon copy too. Only the
        # unlock copy has to survive, but the two are indistinguishable by name and sparing both
        # is cheaper than the bookkeeping to tell them apart.
        protected_names = frozenset(
            (stat_upgrade_names if stat_gating_enabled else frozenset())
            | mastery_required_classes
        )
        world.random.shuffle(optional_priority)
        if protected_names:
            optional_priority.sort(key=lambda item: item.name not in protected_names)
        optional_priority = optional_priority[: len(optional_priority) - overflow]

    priority_items = mandatory_items + optional_priority

    # Traps and reserved filler are guaranteed slots; subtract them from remaining before
    # the useful/filler split.
    remaining    = unfilled - len(priority_items) - len(trap_items) - len(reserved_filler_items)
    total_useful = len(blueprint_pool) + len(rune_pool) + len(manor_pool)

    if total_useful <= remaining:
        # All useful items fit.
        pool = priority_items + trap_items + reserved_filler_items + blueprint_pool + rune_pool + manor_pool
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
            + reserved_filler_items
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
