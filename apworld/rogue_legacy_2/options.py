from dataclasses import dataclass

from Options import DeathLink, PerGameCommonOptions, Range


class RL2DeathLink(DeathLink):
    """When you die, everyone dies. Of course the reverse is true too.

    When enabled, dying in Rogue Legacy 2 will send a death to all other
    DeathLink-enabled players in the multiworld, and receiving a death will
    kill your current heir.
    """


class BlueprintChecksPerBiome(Range):
    """Number of blueprint location checks available per biome.

    Each biome's chests can award this many Archipelago checks when they would
    normally drop a blueprint. Once a biome's pool is exhausted, extra blueprint
    rolls drop as normal vanilla loot instead.

    With 6 biomes and the default of 11, the total blueprint check count is 66.
    This closely mirrors the total of 65 unique blueprints in the game
    """
    display_name = "Blueprint Checks Per Biome"
    range_start = 0
    range_end = 16
    default = 11


@dataclass
class RogueLegacy2GameOptions(PerGameCommonOptions):
    death_link: RL2DeathLink
    blueprint_checks_per_biome: BlueprintChecksPerBiome
