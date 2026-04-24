from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld, World

from .items import RogueLegacy2Item, all_non_event_items_table, create_item, create_items
from .locations_and_regions import all_non_event_locations_table, create_regions
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

    item_name_to_id = all_non_event_items_table
    location_name_to_id = all_non_event_locations_table

    # ── World generation ─────────────────────────────────────────────────────

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

    def fill_slot_data(self) -> dict:
        return self.options.as_dict("death_link", "blueprint_checks_per_biome")
