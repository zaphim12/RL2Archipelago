from dataclasses import dataclass

from Options import Choice, DeathLink, PerGameCommonOptions, Range, Toggle


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


class ManorUpgradeBundleSize(Range):
    """Number of skill tree levels granted when receiving a manor upgrade item.

    This option controls how many levels are granted for multi-level skills
    when an AP item corresponding for that manor slot is received from the server.

    For example, with the default of 5, receiving "Manor: Strength Up I" grants
    5 strength levels (instead of the 1 level like the base-game purchase gives)

    Note: This does not allow exceeding the maximum level for a particular skill
    that the base game imposes. So the final granted bundle may provide less levels
    if the player is too close to the given skill's cap.
    """
    display_name = "Manor Upgrade Bundle Size"
    range_start = 1
    range_end = 35
    default = 5


class ManorUsefulCount(Range):
    """How many copies of each multi-level manor upgrade are classified as 'useful'.

    When a manor upgrade has more than one AP item copy (because its max level exceeds
    the bundle size), the first N copies are marked 'useful' and the remainder are
    'filler'. 'Useful' and 'Filler' are Archipelago terms which determine how items 
    are randomized in the world.

    The first bundle (the only bundle for single-level upgrades like class unlocks, NPC unlocks, 
    etc.) are always 'useful' regardless of this setting.

    Set to 0 to make all extra copies filler. Set to a large number (e.g. 99) to
    keep every copy useful.
    """
    display_name = "Manor Useful Upgrade Count"
    range_start = 0
    range_end = 35
    default = 1


class JournalChecks(Choice):
    """Controls how journals and memories give out location checks.

    disabled (0): No journal or memory checks.
    individual (1): Each journal entry and each memory fragment is its own check. 41 total.
    grouped (2): One check when all journals in a biome are read; one check when all
                 memories in a biome are read. 8 total checks.
    """
    display_name = "Journal Checks"
    option_disabled   = 0
    option_individual = 1
    option_grouped    = 2
    default = 2


class RandomizeNpcUnlocks(Toggle):
    """Whether to randomize NPC unlock slots.

    When enabled (default), the three NPC upgrade slots (Living Safe, Smithy, Enchantress)
    are added as randomized Archipelago locations whose unlocks can be placed anywhere.
    When disabled, these unlock slots will always be placed in their original location. 
    This allows them to be unlocked early instead of potentially gated far into a run.
    """
    display_name = "Randomize NPC Unlock Locations"
    default = 1


@dataclass
class RogueLegacy2GameOptions(PerGameCommonOptions):
    death_link: RL2DeathLink
    blueprint_checks_per_biome: BlueprintChecksPerBiome
    rune_checks_per_biome: RuneChecksPerBiome
    manor_upgrade_bundle_size: ManorUpgradeBundleSize
    manor_useful_count: ManorUsefulCount
    randomize_npc_unlocks: RandomizeNpcUnlocks
    journal_checks: JournalChecks
