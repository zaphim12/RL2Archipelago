from dataclasses import dataclass

from Options import Choice, DeathLink, PerGameCommonOptions, Range, Toggle

from .constants import MAX_MASTERY_RANK_PER_CLASS, MAX_TOTAL_MASTERY_RANK


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


class ManorLayout(Choice):
    """How the manor is structured.

    tree (0): The vanilla manor tree. Exactly 72 nodes in a fixed branching order.
              Each node can be purchased once, so every run has exactly
              72 manor checks no matter the settings.
    shop (1): Four shop positions, each an independent column ManorShopDepth
              purchases deep. All four positions are purchasable at any time until
              exhausted. Buying one immediately puts the next purchase of that
              column on offer. Total manor checks = 4 * ManorShopDepth, so the
              manor scales to fit the run you want.

    Note: RevealManorUpgrades only applies to the tree layout.
    """
    display_name = "Manor Layout"
    option_tree = 0
    option_shop = 1
    default = 0


class ManorShopDepth(Range):
    """How many purchases deep each of the four shop positions goes. Shop layout only.

    Total manor checks = 4 * this value, so the default of 25 gives 100 manor checks
    (versus the tree layout's fixed 72).

    Cost scales with how deep into a column a purchase sits. Lower ManorCostBase
    alongside a large depth if you want the deepest purchases to stay affordable.
    """
    display_name = "Manor Shop Depth"
    range_start = 1
    range_end = 100
    default = 25


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


class MasteryChecks(Choice):
    """Controls how class mastery ranks give out location checks.

    XP is earned by killing enemies and is only converted to mastery levels when an heir dies.

    In the base game nothing accrues at all until the Sage Totem manor upgrade is bought.
    The mod lifts that so checks can be earned immediately.

    disabled (0):  No mastery checks.
    total (1):     Each overall mastery level (i.e. the sum of every class's rank) is one check.
    per_class (2): Each rank of each of the 15 classes is its own check, so there are
                   15 x MasteryLevelsPerClass of them. 

    MasteryProgressionPercent decides how deep into the chain an important item may be
    placed, and MasteryXpMultiplier affects how fast you earn XP.
    """
    display_name = "Mastery Checks"
    option_disabled  = 0
    option_total     = 1
    option_per_class = 2
    default = 1


class TotalMasteryLevels(Range):
    """How many overall mastery levels are checks. Only used when MasteryChecks is total.

    Each mastery level is 1 check, so this exactly corresponds to how many checks are added.

    The maximum is every playable class at the game's rank cap: 15 classes x rank 50. Reaching
    anything near that is an enormous grind, so treat the upper end of this range as something
    to pair with a high MasteryXpMultiplier and/or a low MasteryProgressionPercent.
    """
    display_name = "Total Mastery Levels"
    range_start = 1
    range_end = MAX_TOTAL_MASTERY_RANK
    default = 50


class MasteryLevelsPerClass(Range):
    """How many ranks of each class are checks. Only used when MasteryChecks is per_class.

    Adds 15 x this many checks, one per rank per class.
    """
    display_name = "Mastery Levels Per Class"
    range_start = 1
    range_end = MAX_MASTERY_RANK_PER_CLASS
    default = 10


class MasteryXpMultiplier(Range):
    """Mastery XP award multiplier, as a percentage. The higher the number, the more XP is earned.

    100 is the base-game rate. 200 doubles every XP award, 50 halves it.

    This setting is useful if you want to have many checks coming from mastery ranks, but
    you don't want to have a massive grind to unlock them. 

    Note that this setting still applies with MasteryChecks disabled. However, in that case
    the base game's rule for earning XP still stands: no mastery XP accrues at all until the
    Sage Totem is found.

    The default setting is actually higher than the base game's rate because vanilla's XP
    earning is too slow for a reasonable Archipelago world.

    The range reaches 100x for chasing the deep end of the rank chain, since rank 50 on one
    class needs 15.8 million XP.
    """
    display_name = "Mastery XP Multiplier"
    range_start = 10
    range_end = 10000
    default = 300


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


class TrapAppearance(Choice):
    """Controls whether traps can be identified before you pay for them.

    Anywhere the game shows you what an unclaimed location holds (the manor upgrade
    tree, the teleporter NPC, heirloom pedestals), a trap would otherwise announce
    itself by name and could trivially be avoided. Disguising traps allows them to
    masquerade as another item so it's harder to avoid them.

    hidden (0): Traps are disguised as a real item taken from elsewhere in this
                multiworld, and are only revealed once purchased.
    visible (1): Traps show their true name and icon before purchase.
    """
    display_name = "Trap Appearance"
    option_hidden  = 0
    option_visible = 1
    default = 0


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

class ManorDepthPercentPerBoss(Range):
    """Percentage of the manor's depth that comes into logic per boss killed.

    'Depth' is how far into the manor an upgrade sits: how many upgrades had to be
    bought before it could becomes available for purchase.

    The shallowest x% of depths are in logic from the start, the next x% come into
    logic once 1 boss is cleared, the next x% after 2, and so on. With the default of
    25, a quarter of the manor opens up per boss, so the whole thing is in logic after
    3 bosses.

    Set to 100 to put the entire manor in logic from the start, or 0 to disable this
    gating entirely (identical outcome; 0 just skips the work).
    """
    display_name = "Manor Depth Percent Per Boss Gate"
    range_start = 0
    range_end = 100
    default = 25


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


class MasteryProgressionPercent(Range):
    """Percentage of the mastery chain that is allowed to hold an item worth having.

    Unlike similar options in this list, this is a guarantee, not a nudge. Once you have
    reached x% of the mastery chain, all remaining ranks will only have filler rewards. The
    ranks before this percentage are free to hold any item, progression, useful, or filler.

    This setting exists to ensure that you don't have to potentially grind through every 
    single mastery rank to get a necessary item, since that may take a very long time.
    
    In per_class mode the split is applied within each class.

    100 puts the whole chain in play and 0 makes all of it filler.
    """
    display_name = "Mastery Progression Percent"
    range_start = 0
    range_end = 100
    default = 50


class EarlyNPCUnlocks(Toggle):
    """Whether to shift the NPC unlocks earlier in the run to ensure they aren't gated too far into the game.

    When enabled, the three NPC unlock items (Living Safe, Blacksmith,
    Enchantress) use soft rules to push their locations earlier in the run
    """
    display_name = "Early NPC Unlocks"
    default = 1


class RandomizeStartingClass(Toggle):
    """When enabled, picks a random starting class instead of always starting with Knight.

    The chosen starting class is pre-unlocked at run start and removed from the item pool.
    If the starting class is not Knight, the Knight class becomes a randomizable item in
    the multiworld instead.
    """
    display_name = "Randomize Starting Class"
    default = 1


class FabledWeapons(Choice):
    """How each class's fabled weapon is obtained.

    randomized (0): Fabled weapons are shuffled into the multiworld. Class items become
                    progressive: the first copy unlocks the class, and the second unlocks
                    that class's fabled weapon. The starting class is already one stage
                    ahead, so its one and only copy just unlocks the fabled weapon.
    soul_shop (1):  Class items are plain one-stage unlocks, and the fabled weapons stay
                    purchasable in the Soul Shop.
    unlocked (2):   Every class can use its fabled weapon as soon as that class is unlocked.
                    Class items are plain one-stage unlocks, same as under soul_shop.
    """
    display_name = "Fabled Weapons"
    option_randomized = 0
    option_soul_shop  = 1
    option_unlocked   = 2
    default = 0


class RevealManorUpgrades(Toggle):
    """Show every manor upgrade node from the start instead of only the ones adjacent to a purchase.

    Purchase rules are unchanged: a node still cannot be bought until the previous
    node has been purchased. Revealed-but-locked nodes show a padlock.

    Tree layout only. The shop layout always shows exactly the next purchase of each
    of its four columns, so this setting has no effect there.
    """
    display_name = "Reveal Manor Upgrades"
    default = 0


@dataclass
class RogueLegacy2GameOptions(PerGameCommonOptions):
    death_link: RL2DeathLink
    blueprint_checks_per_biome: BlueprintChecksPerBiome
    rune_checks_per_biome: RuneChecksPerBiome
    bronze_chest_ap_chance: BronzeChestApChance
    silver_chest_ap_chance: SilverChestApChance
    fairy_chest_ap_chance: FairyChestApChance
    manor_layout: ManorLayout
    manor_shop_depth: ManorShopDepth
    manor_upgrade_bundle_size: ManorUpgradeBundleSize
    journal_checks: JournalChecks
    mastery_checks: MasteryChecks
    total_mastery_levels: TotalMasteryLevels
    mastery_levels_per_class: MasteryLevelsPerClass
    mastery_xp_multiplier: MasteryXpMultiplier
    trap_count: TrapCount
    trap_appearance: TrapAppearance
    manor_cost_base: ManorCostBase
    manor_cost_min_subtractive_factor: ManorCostMinSubtractiveFactor
    manor_cost_max_additive_factor: ManorCostMaxAdditiveFactor
    manor_depth_percent_per_boss: ManorDepthPercentPerBoss
    chest_pre_boss_percent: ChestPreBossPercent
    stat_upgrades_per_boss: StatUpgradesPerBoss
    stat_upgrades_per_biome_tier: StatUpgradesPerBiomeTier
    mastery_progression_percent: MasteryProgressionPercent
    early_npc_unlocks: EarlyNPCUnlocks
    randomize_starting_class: RandomizeStartingClass
    fabled_weapons: FabledWeapons
    reveal_manor_upgrades: RevealManorUpgrades
