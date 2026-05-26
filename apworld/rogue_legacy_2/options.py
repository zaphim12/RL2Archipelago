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


class BronzeChestApChance(Range):
    """Percent chance (1–100) that opening a bronze chest triggers an AP location check.

    At 100, every bronze chest opening in a biome with available locations gives an AP
    check. At 1, bronze chests drop gold 99% of the time. At 50, roughly half of openings
    become AP checks.

    When a biome's blueprint pool is exhausted, chests fall back to gold regardless
    of this setting.
    """
    display_name = "Bronze Chest AP Chance"
    range_start = 1
    range_end = 100
    default = 15


class SilverChestApChance(Range):
    """Percent chance (1–100) that opening a silver chest triggers an AP location check.

    Behaves identically to Bronze Chest AP Chance but applies to silver chests, which
    have a higher vanilla blueprint drop rate. Set independently to tune silver chests
    separately from bronze.
    """
    display_name = "Silver Chest AP Chance"
    range_start = 1
    range_end = 100
    default = 99


class FairyChestApChance(Range):
    """Percent chance (1–100) that opening a fairy chest triggers an AP location check.

    At 100, every fairy chest in a biome with available rune locations gives an AP
    check. At 1, fairy chests drop red aether 99% of the time. When a biome's rune
    pool is exhausted, fairy chests fall back to red aether regardless of this setting.
    """
    display_name = "Fairy Chest AP Chance"
    range_start = 1
    range_end = 100
    default = 100


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


class TrapCount(Range):
    """Total number of NG+ burden trap items placed in the item pool.

    Traps are harmful items that, when received from the multiworld, activate an
    NG+ biome hazard for the rest of the current heir's life. The five available
    traps are:
      - Cannonball Rain    (Burden of Mundi's Flagship)
      - Dragon Lancers     (Burden of Irad's Torment)
      - Automaton Swarm    (Burden of Pishon's Uprising)
      - Giant Snowflakes   (Burden of Kerguelen's Frost)
      - Void Waves         (Burden of the High Scholar's Metamorphosis)

    Items are distributed as evenly as possible across the five possible trap types.
    Set to 0 to disable traps entirely.
    """
    display_name = "Trap Count"
    range_start = 0
    range_end = 25
    default = 3


class ManorCostBase(Range):
    """Base gold multiplier for manor upgrade costs.

    Cost formula: base * depth * random_factor, rounded to the nearest 25 gold.
    'depth' is a per-slot value defined in the apworld that reflects how deep
    in the manor unlock tree that slot sits (all default to 1 until configured).
    The random_factor is determined by the ManorCostMinSubtractFactor and 
    ManorCostMaxAdditiveFactor options.
    """
    display_name = "Manor Cost Base"
    range_start = 1
    range_end = 9999
    default = 100


class ManorCostMinSubtractiveFactor(Range):
    """Maximum downward variance for manor costs, as a percentage (0–100).

    E.g. 20 means costs can be as low as (1 - 0.20) = 80% of the base formula.
    Combined with ManorCostMaxAdditiveFactor, the random factor is drawn uniformly from
    [1 - subtract/100, 1 + add/100]. Set both to 0 for deterministic costs.
    """
    display_name = "Manor Cost Min Subtractive Factor"
    range_start = 0
    range_end = 100
    default = 20


class ManorCostMaxAdditiveFactor(Range):
    """Maximum upward variance for manor costs, as a percentage (0–500).

    E.g. 120 means costs can be as high as (1 + 1.20) = 220% of the base formula.
    Combined with ManorCostMinSubtractiveFactor, the random factor is drawn uniformly
    from [1 - subtract/100, 1 + add/100]. Set both to 0 for deterministic costs.
    """
    display_name = "Manor Cost Max Additive Factor"
    range_start = 0
    range_end = 500
    default = 50

# The below options are all designed to allow tuning of progression gating
# They can be adjusted to smooth out run progression and reduce tedium
# Warning: Altering these settings can easily create generation errors if
# the resulting logical requirements are too tight

class ManorDepthsPerBoss(Range):
    """Number of manor depth tiers that are considered 'in-logic' per boss killed.

    'Depth' is a value assigned to each manor upgrade that reflects how many upgrades
    were required beforehand to reach that upgrade in the manor tree. 

    The first 'x' depth tiers are always in logic; each additional boss killed
    opens the next x tiers. With the default of 3: depths 1–3
    are available from the start, depths 4–6 require 1 boss cleared, depths 7–9
    require 2, and so on. 

    Set to 0 to disable this gating entirely.
    """
    display_name = "Manor Depths Per Boss Gate"
    range_start = 0
    range_end = 11
    default = 3


class ChestPreBossPercent(Range):
    """Percentage of each biome's chests considered in logic before the biome boss is killed.

    This setting can be used to reduce the likelihood of a tedious grind being required
    where all chests-related checks in a biome need to be cleared in order to unlock 
    the items needed to beat the boss.
    
    This percentage of blueprint chest and fairy chest locations are considered 
    'in-logic' before the biome's boss is killed. The remaining chests
    are only considered 'in-logic' after the boss is defeated. 
    """
    display_name = "Chest Pre-Boss Percent"
    range_start = 50
    range_end = 100
    default = 75


class StatUpgradesPerBoss(Range):
    """Number of received stat upgrade items logically required for killing a boss 
    to be considered 'in-logic'. Each subsequent boss requires an additional multiple
    of this number of upgrades.

    With the default of 5: Estuary Lamech requires 5 upgrades, Byarrrith and
    Halpharr require 10, Estuary Naamah requires 15, Estuary Enoch requires 20,
    Estuary Irad requires 25, and Estuary Tubal requires 30. Gongheads and Murmur
    minibosses use the same threshold as Naamah; Pishon Dry Lake minibosses use the
    same threshold as Irad. 

    Set to 0 to disable stat-upgrade gating for bosses.
    """
    display_name = "Stat Upgrades Per Boss"
    range_start = 0
    range_end = 10
    default = 5


class StatUpgradesPerBiomeTier(Range):
    """Base number of received manor upgrade items logically required per biome tier.

    The latter four biomes each require progressively more upgrades before their
    chest and journal/memory locations are considered in logic: Kerguelen Plateau
    requires x, Stygian Study requires 2x, Sun Tower requires 3x, and Pishon Dry
    Lake requires 4x. Citadel Agartha and Axis Mundi are never gated. 

    Set to 0 to disable stat-upgrade gating for biomes.
    """
    display_name = "Stat Upgrades Per Biome Tier"
    range_start = 0
    range_end = 10
    default = 5


class EarlyNPCUnlocks(Toggle):
    """Whether to shift the NPC unlocks earlier in the run to ensure they aren't gated too far into the game.

    When enabled, the three NPC unlock items (Living Safe, Blacksmith,
    Enchantress) use soft rules to push their locations earlier in the run
    """
    display_name = "Early NPC Unlocks"
    default = 1


@dataclass
class RogueLegacy2GameOptions(PerGameCommonOptions):
    death_link: RL2DeathLink
    blueprint_checks_per_biome: BlueprintChecksPerBiome
    rune_checks_per_biome: RuneChecksPerBiome
    bronze_chest_ap_chance: BronzeChestApChance
    silver_chest_ap_chance: SilverChestApChance
    fairy_chest_ap_chance: FairyChestApChance
    manor_upgrade_bundle_size: ManorUpgradeBundleSize
    journal_checks: JournalChecks
    trap_count: TrapCount
    manor_cost_base: ManorCostBase
    manor_cost_min_subtractive_factor: ManorCostMinSubtractiveFactor
    manor_cost_max_additive_factor: ManorCostMaxAdditiveFactor
    manor_depths_per_boss: ManorDepthsPerBoss
    chest_pre_boss_percent: ChestPreBossPercent
    stat_upgrades_per_boss: StatUpgradesPerBoss
    stat_upgrades_per_biome_tier: StatUpgradesPerBiomeTier
    early_npc_unlocks: EarlyNPCUnlocks
