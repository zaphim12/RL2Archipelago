from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld, World

from .items import RogueLegacy2Item, _CORE_MANOR_UPGRADES, _NPC_MANOR_UPGRADES, all_non_event_items_table, create_item, create_items
from .locations_and_regions import all_non_event_locations_table, create_regions
from .options import RogueLegacy2GameOptions

# Tree depth for each manor upgrade slot. Depth 1 = available at the start of the game;
# higher values = deeper in the unlock tree, resulting in higher gold costs.
# Any upgrade missing from this dict defaults to depth 1.
MANOR_UPGRADE_DEPTHS: dict[str, int] = {
    # ── Vitality ─────────────────────────────────────────────────────────────
    "Mess Hall (Vitality Up I)":                        3,
    "Fruit Juice Bar (Vitality Up II)":                 5,
    "Meteora Gym (Vitality Up III)":                    7,
    # ── Misc survival ────────────────────────────────────────────────────────
    "Institute of Gastronomy (Health Drop Scaling)":    8,
    # ── Strength ─────────────────────────────────────────────────────────────
    "Arsenal (Strength Up I)":                          5,
    "Sauna (Strength Up II)":                           6,
    "Rock Climbing Wall (Strength Up III)":             9,
    # ── Spin kicks ───────────────────────────────────────────────────────────
    "Bamboo Garden (Spin Kick scales with INT)":        8,
    # ── Dexterity ────────────────────────────────────────────────────────────
    "Gym (Dexterity Up I)":                             7,
    "Yoga Class (Dexterity Up II)":                     9,
    "Flower Shop (Dexterity Up III)":                   11,
    # ── Weapon crit ──────────────────────────────────────────────────────────
    "The Laundromat (Weapon Crit Damage Up)":           10,
    # ── Intelligence ─────────────────────────────────────────────────────────
    "Study Hall (Intelligence Up I)":                   5,
    "Math Club (Intelligence Up II)":                   8,
    "University (Intelligence Up III)":                 10,
    # ── Focus ────────────────────────────────────────────────────────────────
    "Library (Focus Up I)":                             7,
    "Hall of Wisdom (Focus Up II)":                     9,
    "Court of the Wise (Focus Up III)":                 11,
    # ── Magic crit damage ────────────────────────────────────────────────────
    "The Lodge (Magic Crit Damage Up)":                 10,
    # ── Equipment weight ─────────────────────────────────────────────────────
    "Fashion Chambers (Max Weight Up I)":               4,
    "Tailors (Max Weight Up II)":                       6,
    "Artisan (Max Weight Up III)":                      7,
    # ── Rune weight ──────────────────────────────────────────────────────────
    "Etching Chambers (Max Rune Weight Up I)":          4,
    "Pillow Mill (Max Rune Weight Up II)":              6,
    "Bed Mill (Max Rune Weight Up III)":                7,
    # ── Armor ────────────────────────────────────────────────────────────────
    "Foundry (Armor Up I)":                             6,
    "Blast Furnace (Armor Up II)":                      8,
    "Some Kind of Kiln (Armor Up III)":                 10,
    # ── Gold & economy ───────────────────────────────────────────────────────
    "Universal Health Stair (Traits Give Gold)":        1,
    "Repurposed Mining Shaft (Traits Gold Gain Up)":    3,
    "Geologist's Camp (Ore Drop Chance Up)":            6,
    "Dowsing Center (Red Aether Drop Chance Up)":       8,
    "Massive Vault (Gold Gain Up I)":                   10,
    # ── Rerolls ──────────────────────────────────────────────────────────────
    "Career Center (Re-Roll Children)":                 5,
    "Aerobics Classroom (Encumbrance Limit Up)":        5,
    # ── Living safe ──────────────────────────────────────────────────────────
    "Courthouse (Living Safe Max Gold Up)":             4,
    "Scribe's Office (Living Safe Conversion Up)":      5,
    # ── Class unlocks ────────────────────────────────────────────────────────
    "Fighting Ring (Boxer Class)":                      6,
    "Dance Hall (Duelist Class)":                       6,
    "Guild of Dark Arts (Assassin Class)":              8,
    "Drill Store (Architect Cost Reduction)":           6,
    "Adoption Center (More Heirs)":                     10,
    "The Kitchen (Chef Class)":                         6,
    "Butcher's Shoppe (Barbarian Class)":               4,
    "Academy (Mage Class)":                             4,
    "Archery Range (Ranger Class)":                     2,
    "Sand Pits (Valkyrie Class)":                       4,
    "Saltpeter Mines (Gunslinger Class)":               8,
    "Ryokan (Ronin Class)":                             8,
    "The Tavern (Bard Class)":                          7,
    "The Flying Docks (Pirate Class)":                  9,
    "The Astral Gardens (Astromancer Class)":           9,
    "The Aviary (Dragon Lancer Class)":                 7,
    # ── Progression & utility ────────────────────────────────────────────────
    "Trophy Room (XP Up)":                              5,
    "Jeweler (Ore Gain Up)":                            11,
    "Buried Tomb (Red Aether Gain Up)":                 9,
    "Dummy (Training Dummy Unlock)":                    4,
    "Meditation Studies (Boss Health/Mana Restore)":    5,
    "Sage Totem (Mastery Rank Unlock)":                 4,
    "Archaeology Camp (Relic Resolve Cost Down)":       7,
    "Medieval Forgery (Relic Reroll Up)":               8,
    "Alchemy Lab (Potion Recharge Talent)":             7,
    "Psychiatrist (Resolve Up)":                        6,
    "Jousting Studies (Dash Damage Reduction)":         7,
    "Charity Dungeon (Charon Donation Bonus Unlock)":   3,
    "The Dicer's Den (Weapon Crit Chance Up)":          10,
    "The Quantum Observatory (Magic Crit Chance Up)":   11,
    "The Bizarre Bazaar (Reroll Relic Room Cap Up)":    9,
    "Screw Distillery (Architect Unlock)":              5,
    # ── NPC unlocks ──────────────────────────────────────────────────────────
    "Offshore Bank Account (Living Safe)":              2,
    "Foundation (Smithy Unlock)":                       3,
    "Enchantress' Quarters (Enchantress Unlock)":       3,
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
        for upgrade in _CORE_MANOR_UPGRADES + _NPC_MANOR_UPGRADES:
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
