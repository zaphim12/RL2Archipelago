from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld, World

from .constants import CORE_MANOR_UPGRADES, MANOR_UPGRADE_DEPTHS, NPC_MANOR_UPGRADES
from .items import RogueLegacy2Item, items_table, create_item, create_items
from .locations_and_regions import locations_table, create_regions
from .options import RogueLegacy2GameOptions


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

    def _compute_manor_upgrade_costs(self) -> list[int]:
        """Compute a gold cost for each manor upgrade slot using the seeded world RNG.

        Costs are baked into slot data at generation time so the client always
        receives pre-computed values. Reconnecting to the same session yields
        identical costs without any client-side RNG.

        Formula per slot: base * depth * factor, rounded to the nearest 25 gold.
        factor is drawn uniformly from [1 - subtract_factor, 1 + add_factor].
        """
        base = self.options.manor_cost_base.value
        min_factor = 1.0 - self.options.manor_cost_min_subtractive_factor.value / 100.0
        max_factor = 1.0 + self.options.manor_cost_max_additive_factor.value / 100.0
        costs: list[int] = []
        for upgrade in CORE_MANOR_UPGRADES + NPC_MANOR_UPGRADES:
            depth = MANOR_UPGRADE_DEPTHS.get(upgrade, 1)
            factor = self.random.uniform(min_factor, max_factor)
            raw = base * depth * factor
            costs.append(max(25, round(raw / 25) * 25))
        return costs

    def fill_slot_data(self) -> dict:
        data = self.options.as_dict(
            "death_link",
            "blueprint_checks_per_biome",
            "rune_checks_per_biome",
            "manor_upgrade_bundle_size",
            "journal_checks",
            "trap_appearance",
            "manor_cost_base",
            "manor_cost_min_subtractive_factor",
            "manor_cost_max_additive_factor",
            "bronze_chest_ap_chance",
            "silver_chest_ap_chance",
            "fairy_chest_ap_chance",
            "manor_depths_per_boss",
            "chest_pre_boss_percent",
            "stat_upgrades_per_boss",
            "stat_upgrades_per_biome_tier",
            "early_npc_unlocks",
            "reveal_manor_upgrades",
        )
        data["manor_upgrade_costs"] = self._compute_manor_upgrade_costs()
        data["randomize_starting_class"] = int(self.options.randomize_starting_class.value)
        data["starting_class_index"] = self._starting_class_index
        return data
