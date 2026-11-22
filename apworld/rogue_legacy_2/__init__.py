import math

from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld, World

from .constants import (
    CORE_MANOR_UPGRADES,
    KNIGHT_CLASS_POSITION,
    LOGICAL_MASTERY_RANKS_PER_CLASS,
    MANOR_SHOP_POSITIONS,
    MANOR_UPGRADE_DEPTHS,
    NPC_MANOR_UPGRADES,
    PLAIN_CLASS_ITEM_NAMES,
    PROGRESSIVE_CLASS_ITEM_NAMES,
)
from .items import RogueLegacy2Item, items_table, create_item, create_items
from .locations_and_regions import locations_table, create_regions
from .options import FabledWeapons, ManorLayout, MasteryChecks, RogueLegacy2GameOptions


class RogueLegacy2WebWorld(WebWorld):
    theme = "stone"
    tutorials = [
        Tutorial(
            tutorial_name="Setup Guide",
            description="A guide to setting up the Rogue Legacy 2 Archipelago randomizer.",
            language="English",
            file_name="guide_en.md",
            link="guide/en",
            authors=["zaphim12"],
        )
    ]


class RogueLegacy2World(World):
    game = "Rogue Legacy 2"
    web = RogueLegacy2WebWorld()

    # ── AP bookkeeping ───────────────────────────────────────────────────────
    options_dataclass = RogueLegacy2GameOptions
    options: RogueLegacy2GameOptions

    item_name_to_id = items_table
    location_name_to_id = locations_table

    # ── World generation ─────────────────────────────────────────────────────

    def generate_early(self) -> None:
        if self.options.randomize_starting_class:
            # 0 = Knight (vanilla), 1–14 = one of the 14 non-Knight classes in
            # CLASS_UNLOCK_ITEM_NAMES order (ManorSlots[44]–ManorSlots[57]).
            self._starting_class_index: int = self.random.randint(0, 14)
        else:
            self._starting_class_index = 0

        # Decided here, once, because create_regions() writes the rules that name these classes
        # and create_items() decides their classification from the same list. Recomputing it in
        # both would draw from the world RNG twice and could disagree.
        self._mastery_required_classes: list[str] = self._pick_mastery_required_classes()

    def _pick_mastery_required_classes(self) -> list[str]:
        """The class items a mastery access rule will actually name.

        Naming a class item is what obliges it to be progression - state.has() only ever sees
        advancement items - so this list is deliberately as short as the rules allow.

        per_class mode asks about each class individually, so every class with ranks of its own is
        named. total mode only ever asks "how many classes?", and never more than
        ceil(levels / LOGICAL_MASTERY_RANKS_PER_CLASS) of them, so a random subset of that size is
        enough and can leave the rest as ordinary useful items. Picking a subset is conservative
        rather than wrong: the rule accepts only these classes where the real game would accept
        any, so anything it calls reachable genuinely is.

        Note that this counts the levels the seed *instantiates*, not just the ones
        mastery_progression_percent leaves open. An excluded location still has to be reachable
        under `accessibility: full` (MultiWorld.fulfills_accessibility treats every location of a
        full-accessibility player as relevant, whatever its progress_type), so the deep ranks'
        rules have to be satisfiable even though they can only ever hold filler.
        """
        others = [
            name for position, name in enumerate(self.class_item_names)
            if position != self.starting_class_position
        ]

        if self.options.mastery_checks == MasteryChecks.option_per_class:
            return others

        if self.options.mastery_checks == MasteryChecks.option_total:
            # Minus one: the starting class is already unlocked and covers the first
            # LOGICAL_MASTERY_RANKS_PER_CLASS levels by itself. The min() is what gives way when
            # the level count outruns the assumed budget across every class. Past that point
            # there is nothing left to name, and the budget rises by necessity.
            needed = math.ceil(
                self.options.total_mastery_levels.value / LOGICAL_MASTERY_RANKS_PER_CLASS) - 1
            if needed > 0:
                return self.random.sample(others, min(needed, len(others)))

        return []

    @property
    def mastery_required_classes(self) -> list[str]:
        """See _pick_mastery_required_classes. Empty when no mastery rule names a class."""
        return getattr(self, "_mastery_required_classes", [])

    @property
    def starting_class_position(self) -> int:
        """Position of this seed's starting class within the 15-entry class item lists.

        Translates _starting_class_index (0 = Knight, 1-14 = CLASS_UNLOCK_ITEM_NAMES order)
        into the list order used by both class item sets, where Knight sits last rather
        than first.
        """
        index = getattr(self, "_starting_class_index", 0)
        return KNIGHT_CLASS_POSITION if index == 0 else index - 1

    @property
    def class_item_names(self) -> list[str]:
        """Which of the two parallel class item sets this seed uses. See constants.py.

        Only the randomized mode puts fabled weapons in the pool at all; soul_shop and
        unlocked both leave class items as plain one-stage unlocks.
        """
        return (
            PROGRESSIVE_CLASS_ITEM_NAMES
            if self.options.fabled_weapons == FabledWeapons.option_randomized
            else PLAIN_CLASS_ITEM_NAMES
        )

    def create_regions(self) -> None:
        create_regions(self)

    def create_items(self) -> None:
        create_items(self)

    def create_item(self, name: str) -> RogueLegacy2Item:
        return create_item(self.player, name)

    def get_filler_item_name(self) -> str:
        """Gold Coins is the only true filler item this world has.

        Spelled out rather than left to the base class, which picks a random name out of
        item_name_to_id and would happily hand back an heirloom. It matters because the
        excluded tail of the mastery chain can only be filled from the filler pool.
        """
        return "Gold Coins"

    def set_rules(self) -> None:
        # Victory requires having the "Victory" event item, which is placed at
        # the "Victory" event location in the Throne Room region.
        self.multiworld.completion_condition[self.player] = \
            lambda state: state.has("Victory", self.player)

    def _manor_cost_at_depth(self, depth: int) -> int:
        """Draw one manor gold cost for the given tree/column depth.

        Formula: base * depth * factor, rounded to the nearest 25 gold, where factor
        is drawn uniformly from [1 - subtract_factor, 1 + add_factor]. Uses the seeded
        world RNG, so costs are baked into slot data at generation time and reconnecting
        to the same session yields identical costs without any client-side RNG.
        """
        base = self.options.manor_cost_base.value
        min_factor = 1.0 - self.options.manor_cost_min_subtractive_factor.value / 100.0
        max_factor = 1.0 + self.options.manor_cost_max_additive_factor.value / 100.0
        raw = base * depth * self.random.uniform(min_factor, max_factor)
        return max(25, round(raw / 25) * 25)

    def _compute_manor_upgrade_costs(self) -> list[int]:
        """Tree layout: one cost per manor slot, ordered to match the C# slot list."""
        return [
            self._manor_cost_at_depth(MANOR_UPGRADE_DEPTHS.get(upgrade, 1))
            for upgrade in CORE_MANOR_UPGRADES + NPC_MANOR_UPGRADES
        ]

    def _compute_manor_shop_costs(self) -> list[int]:
        """Shop layout: one cost per purchase, flat and position-major.

        Index = position * manor_shop_depth + depth_index, which is how the C# client
        indexes it. A purchase's depth is its one-based position in its column, so the
        two layouts price identically at equal depth.
        """
        depth = self.options.manor_shop_depth.value
        return [
            self._manor_cost_at_depth(depth_index + 1)
            for _position in range(MANOR_SHOP_POSITIONS)
            for depth_index in range(depth)
        ]

    def fill_slot_data(self) -> dict:
        data = self.options.as_dict(
            "death_link",
            "blueprint_checks_per_biome",
            "rune_checks_per_biome",
            "manor_layout",
            "manor_shop_depth",
            "manor_upgrade_bundle_size",
            "journal_checks",
            "mastery_checks",
            "total_mastery_levels",
            "mastery_levels_per_class",
            "mastery_xp_multiplier",
            "mastery_progression_percent",
            "trap_appearance",
            "manor_cost_base",
            "manor_cost_min_subtractive_factor",
            "manor_cost_max_additive_factor",
            "bronze_chest_ap_chance",
            "silver_chest_ap_chance",
            "fairy_chest_ap_chance",
            "manor_depth_percent_per_boss",
            "chest_pre_boss_percent",
            "stat_upgrades_per_boss",
            "stat_upgrades_per_biome_tier",
            "early_npc_unlocks",
            "fabled_weapons",
            "reveal_manor_upgrades",
        )
        # Only the active layout's cost array is computed, so the unused one does not
        # consume world RNG. The client reads whichever one its layout calls for.
        if self.options.manor_layout.value == ManorLayout.option_shop:
            data["manor_upgrade_costs"] = []
            data["manor_shop_costs"] = self._compute_manor_shop_costs()
        else:
            data["manor_upgrade_costs"] = self._compute_manor_upgrade_costs()
            data["manor_shop_costs"] = []
        data["randomize_starting_class"] = int(self.options.randomize_starting_class.value)
        data["starting_class_index"] = self._starting_class_index
        return data
