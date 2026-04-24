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


class RuneChecksPerBiome(Range):
    """Number of fairy chest location checks available per biome.

    Each biome's fairy chests can award this many Archipelago checks when they
    would normally drop a rune. Once a biome's pool is exhausted, extra fairy
    chest rolls drop red aether instead.

    With 6 biomes and the default of 4, the total fairy chest check count is 24.
    This mirrors the typical number of rune drops available in a standard run.
    """
    display_name = "Rune Checks Per Biome"
    range_start = 0
    range_end = 16
    default = 4


@dataclass
class RogueLegacy2GameOptions(PerGameCommonOptions):
    death_link: RL2DeathLink
    blueprint_checks_per_biome: BlueprintChecksPerBiome
    rune_checks_per_biome: RuneChecksPerBiome
