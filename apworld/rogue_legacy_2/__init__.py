from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld, World

from .constants import (
    ACADEMY, ADOPTION_CENTER, AEROBICS_CLASSROOM, ALCHEMY_LAB, ARCHAEOLOGY_CAMP,
    ARCHERY_RANGE, ARSENAL, ARTISAN, ASTRAL_GARDENS, AVIARY,
    BAMBOO_GARDEN, BED_MILL, BIZARRE_BAZAAR, BLAST_FURNACE, BURIED_TOMB,
    BUTCHERS_SHOPPE, CAREER_CENTER, CHARITY_DUNGEON, CORE_MANOR_UPGRADES,
    COURT_OF_THE_WISE, COURTHOUSE, DANCE_HALL, DICERS_DEN, DOWSING_CENTER,
    DRILL_STORE, DUMMY, ENCHANTRESS_QUARTERS, ETCHING_CHAMBERS, FASHION_CHAMBERS,
    FIGHTING_RING, FLOWER_SHOP, FLYING_DOCKS, FOUNDATION, FOUNDRY,
    FRUIT_JUICE_BAR, GEOLOGISTS_CAMP, GUILD_OF_DARK_ARTS, GYM,
    HALL_OF_WISDOM, INSTITUTE_OF_GASTRONOMY, JEWELER, JOUSTING_STUDIES,
    KITCHEN, LAUNDROMAT, LIBRARY, LODGE, MASSIVE_VAULT,
    MATH_CLUB, MEDITATION_STUDIES, MEDIEVAL_FORGERY, MESS_HALL,
    METEORA_GYM, NPC_MANOR_UPGRADES, OFFSHORE_BANK_ACCOUNT, PILLOW_MILL,
    PSYCHIATRIST, QUANTUM_OBSERVATORY, REPURPOSED_MINING_SHAFT, ROCK_CLIMBING_WALL,
    RYOKAN, SAGE_TOTEM, SALTPETER_MINES, SAND_PITS, SAUNA,
    SCRIBES_OFFICE, SCREW_DISTILLERY, SOME_KIND_OF_KILN, STUDY_HALL,
    TAILORS, TAVERN, TROPHY_ROOM, UNIVERSAL_HEALTH_STAIR, UNIVERSITY,
    YOGA_CLASS,
)
from .items import RogueLegacy2Item, all_non_event_items_table, create_item, create_items
from .locations_and_regions import all_non_event_locations_table, create_regions
from .options import RogueLegacy2GameOptions

# Tree depth for each manor upgrade slot. Depth 1 = available at the start of the game;
# higher values = deeper in the unlock tree, resulting in higher gold costs.
# Any upgrade missing from this dict defaults to depth 1.
MANOR_UPGRADE_DEPTHS: dict[str, int] = {
    # ── Vitality ─────────────────────────────────────────────────────────────
    MESS_HALL:               3,
    FRUIT_JUICE_BAR:         5,
    METEORA_GYM:             7,
    # ── Misc survival ────────────────────────────────────────────────────────
    INSTITUTE_OF_GASTRONOMY: 8,
    # ── Strength ─────────────────────────────────────────────────────────────
    ARSENAL:                 5,
    SAUNA:                   6,
    ROCK_CLIMBING_WALL:      9,
    # ── Spin kicks ───────────────────────────────────────────────────────────
    BAMBOO_GARDEN:           8,
    # ── Dexterity ────────────────────────────────────────────────────────────
    GYM:                     7,
    YOGA_CLASS:              9,
    FLOWER_SHOP:             11,
    # ── Weapon crit ──────────────────────────────────────────────────────────
    LAUNDROMAT:              10,
    # ── Intelligence ─────────────────────────────────────────────────────────
    STUDY_HALL:              5,
    MATH_CLUB:               8,
    UNIVERSITY:              10,
    # ── Focus ────────────────────────────────────────────────────────────────
    LIBRARY:                 7,
    HALL_OF_WISDOM:          9,
    COURT_OF_THE_WISE:       11,
    # ── Magic crit damage ────────────────────────────────────────────────────
    LODGE:                   10,
    # ── Equipment weight ─────────────────────────────────────────────────────
    FASHION_CHAMBERS:        4,
    TAILORS:                 6,
    ARTISAN:                 7,
    # ── Rune weight ──────────────────────────────────────────────────────────
    ETCHING_CHAMBERS:        4,
    PILLOW_MILL:             6,
    BED_MILL:                7,
    # ── Armor ────────────────────────────────────────────────────────────────
    FOUNDRY:                 6,
    BLAST_FURNACE:           8,
    SOME_KIND_OF_KILN:       10,
    # ── Gold & economy ───────────────────────────────────────────────────────
    UNIVERSAL_HEALTH_STAIR:  1,
    REPURPOSED_MINING_SHAFT: 3,
    GEOLOGISTS_CAMP:         6,
    DOWSING_CENTER:          8,
    MASSIVE_VAULT:           10,
    # ── Rerolls ──────────────────────────────────────────────────────────────
    CAREER_CENTER:           5,
    AEROBICS_CLASSROOM:      5,
    # ── Living safe ──────────────────────────────────────────────────────────
    COURTHOUSE:              4,
    SCRIBES_OFFICE:          5,
    # ── Class unlocks ────────────────────────────────────────────────────────
    FIGHTING_RING:           6,
    DANCE_HALL:              6,
    GUILD_OF_DARK_ARTS:      8,
    DRILL_STORE:             6,
    ADOPTION_CENTER:         10,
    KITCHEN:                 6,
    BUTCHERS_SHOPPE:         4,
    ACADEMY:                 4,
    ARCHERY_RANGE:           2,
    SAND_PITS:               4,
    SALTPETER_MINES:         8,
    RYOKAN:                  8,
    TAVERN:                  7,
    FLYING_DOCKS:            9,
    ASTRAL_GARDENS:          9,
    AVIARY:                  7,
    # ── Progression & utility ────────────────────────────────────────────────
    TROPHY_ROOM:             5,
    JEWELER:                 11,
    BURIED_TOMB:             9,
    DUMMY:                   4,
    MEDITATION_STUDIES:      5,
    SAGE_TOTEM:              4,
    ARCHAEOLOGY_CAMP:        7,
    MEDIEVAL_FORGERY:        8,
    ALCHEMY_LAB:             7,
    PSYCHIATRIST:            6,
    JOUSTING_STUDIES:        7,
    CHARITY_DUNGEON:         3,
    DICERS_DEN:              10,
    QUANTUM_OBSERVATORY:     11,
    BIZARRE_BAZAAR:          9,
    SCREW_DISTILLERY:        5,
    # ── NPC unlocks ──────────────────────────────────────────────────────────
    OFFSHORE_BANK_ACCOUNT:   2,
    FOUNDATION:              3,
    ENCHANTRESS_QUARTERS:    3,
}


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

    def _compute_manor_upgrade_costs(self) -> list[int]:
        """Compute a gold cost for each manor upgrade slot using the seeded world RNG.

        Costs are baked into slot data at generation time so the client always
        receives pre-computed values — reconnecting to the same session yields
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
            "manor_useful_count",
            "randomize_npc_unlocks",
            "journal_checks",
            "manor_cost_base",
            "manor_cost_min_subtractive_factor",
            "manor_cost_max_additive_factor",
        )
        data["manor_upgrade_costs"] = self._compute_manor_upgrade_costs()
        return data
