from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld, World

from .constants import (
    CORE_MANOR_UPGRADES,
    MANOR_SHOP_POSITIONS,
    MANOR_UPGRADE_DEPTHS,
    NPC_MANOR_UPGRADES,
)
from .items import RogueLegacy2Item, items_table, create_item, create_items
from .locations_and_regions import locations_table, create_regions
from .options import ManorLayout, RogueLegacy2GameOptions


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

    def create_regions(self) -> None:
        create_regions(self)

    def create_items(self) -> None:
        create_items(self)

    def create_item(self, name: str) -> RogueLegacy2Item:
        return create_item(self.player, name)

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
