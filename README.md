# Rogue Legacy 2 Archipelago Randomizer
This is a mod for Rogue Legacy 2 which integrates the game with the [Archipelago project](https://archipelago.gg)

## Gameplay and Randomization Features / Changes

The run is completed as normal by defeating Cain. All biome bosses must be defeated to unlock the golden doors as normal. All collectables in the run are randomized:

- All manor upgrades (class unlocks, stat increases, etc.), heirlooms, blueprints, runes, and teleporter unlocks are shuffled into randomized locations
- Locations where items can be found:
  - Fairy chests
  - Normal and Silver chests (items drop with the same probability as blueprints would in normal gameplay)
  - Boss and mini-boss kills
  - Journals and memories (configurable)
  - Manor upgrades
  - Pizza girl purchases
  - Heirloom statues (and Johann's conversation in Pishon Dry Lake after defeating Irad)

## Configurable options (i.e. The player YAML)

There are several things that can be customized for each run using the Archipelago YAML file

<details>
<summary>Expand to see the YAML configuration options</summary>

---

### Deathlink (`death_link`)

**Values:** enabled / disabled

Kills your character when another player with deathlink enabled dies. Triggers deaths for other players with deathlink enabled when you die.

---

### Normal/Silver Chest Checks per Biome (`blueprint_checks_per_biome`)

**Range:** 0–16 | **Default:** 11 *(11 checks × 6 biomes = 66 checks, approximately matching the 65 total blueprints in the base game)*

Normal and silver chests have a random chance to roll into an AP item (same probability as the base game: 15% for a normal chest, 99% for a silver chest). This setting controls how many AP checks you can get from opening chests in each biome. Once all checks have been completed in a biome, all chests will only give gold, like the base game.

---

### Fairy Chest Checks per Biome (`rune_checks_per_biome`)

**Range:** 0–16 | **Default:** 4 *(4 checks × 6 biomes = 24 checks, matching the 24 total runes in the base game)*

When a fairy chest is opened, an AP item will be given every time until the pool is exhausted. Then chests will just drop red aether as normal.

---

### Manor Upgrade Bundle Size (`manor_upgrade_bundle_size`)

**Range:** 0–35 | **Default:** 5

Many manor upgrades have multiple levels available. In the randomizer, granting only one level at a time would make progression feel too slow for most players. So instead, these multi-level upgrades are granted in bundles; checking one AP location can grant a bundle which gives *n* levels to a manor upgrade (e.g. +5 dexterity instead of +1).

This setting controls how big each bundle is, and therefore how many AP items are created for manor upgrades. For example, if an upgrade has 20 total levels, a bundle size of 5 results in 4 AP items; a bundle size of 10 results in only 2. The total levels for a slot are based on the max level excluding NG+ soul shop purchases. Generally, increasing this number makes the run easier and decreasing it makes it harder.

If the max level for a particular skill is not divisible by the bundle size, the last bundle will be capped to the max level and grant fewer levels than the others.

---

### Manor Useful Count (`manor_useful_count`)

**Range:** 0–35 | **Default:** 1

Within Archipelago, items can be classified as "useful" or "filler". Filler items are considered unimportant and can be omitted if there are too few locations to fit them. "Useful" items are spread throughout the run to help the player progress at a reasonable rate. This spread of "useful" items can be tuned via Archipelago's [progression_balancing](https://archipelago.gg/tutorial/Archipelago/advanced_settings_en#game-options) setting to weight "useful" items towards the beginning or end of a run.

This setting controls how many bundles per multi-level manor upgrade are classified as "useful". All single-level upgrades are always considered "useful". Setting this to 0 means all multi-level manor upgrades will be treated as "filler".

---

### Journal/Memory Checks (`journal_checks`)

**Values:** `disabled` / `individual` / `grouped` | **Default:** `grouped`

Controls how AP checks are awarded from reading journals and memories.

- **disabled** - no AP checks for journals or memories
- **individual** - one check per unique journal or memory read
- **grouped** - one check after reading all journals in a biome, and one after reading all memories in a biome

There are 41 total memories and journals, and 8 groups (some biomes have no memories and therefore no group check).

---

### Randomize NPC Unlocks (`randomize_npc_unlocks`)

**Values:** true / false | **Default:** true

The living safe, the enchantress, and the blacksmith are three highly valuable NPCs. When randomized, they may not appear until very late in a run, significantly weakening the player. Setting this to false keeps these NPCs at their default unlock locations so they can be purchased early.

---

### Trap Count (`trap_count`)

**Range:** 0–25 | **Default:** 3

Traps are a feature in Archipelago where checking a location may give the player a negative reward. In Rogue Legacy 2, traps activate a random biome environmental burden for the remainder of the current heir's life. This effect will occur in all valid rooms, not just the intended biome.

There are 5 available traps:
- Cannonball Rain *(Burden of Mundi's Flagship)*
- Dragon Lancers *(Burden of Irad's Torment)*
- Automaton Swarm *(Burden of Pishon's Uprising)*
- Giant Snowflakes *(Burden of Kerguelen's Frost)*
- Void Waves *(Burden of the High Scholar's Metamorphosis)*

---

### Manor Upgrade Costs (`manor_cost_base`, `manor_cost_min_subtractive_factor`, `manor_cost_max_additive_factor`)

| Setting | Range | Default |
|---|---|---|
| `manor_cost_base` | 1–9999 | 100 |
| `manor_cost_min_subtractive_factor` | 0–100 | 20 |
| `manor_cost_max_additive_factor` | 0–500 | 50 |

The typical manor upgrade costs don't scale well in an Archipelago run, so a custom pricing formula is used. It incorporates a "depth" value which is determined by how many prior manor purchases were required to unlock a given slot. The first available slot is depth 1; slots unlocked after the first purchase are depth 2; and so on, up to a maximum of depth 11.

The formula is:

```
cost = base × depth × random_factor
```

`random_factor` is a random decimal chosen uniformly between `(1 - subtractive_factor/100)` and `(1 + additive_factor/100)`. With the defaults, this means each upgrade costs `base × depth`, then randomly adjusted to be up to 20% cheaper or 50% more expensive.

</details>


## Installation

Rogue Legacy 2 must already be installed. All files needed can be downloaded from the [GitHub releases page](https://github.com/zaphim12/RL2Archipelago/releases). Look at the most recent release and scroll down to Assets.

> **Note:** Mac and Linux compatibility has not been tested, but should likely work fine.

### Installing the Mod

1. Download and install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html). BepInEx is the mod injector that allows the mod to modify gameplay without altering game files.
   - Any latest stable release will work. The BepInEx installation page links to their GitHub releases. Scroll to Assets and download the version for your OS.
   - Extract the BepInEx files into the game's root directory. On Windows this is typically: `C:\Program Files (x86)\Steam\steamapps\common\Rogue Legacy 2\`
   - Run the game once so BepInEx can generate its required files.

2. Download and extract the RL2Archipelago mod files into BepInEx's plugins folder.
   - If you are only playing (not generating the Archipelago world), you only need `RL2Archipelago.zip`.
   - If the `plugins` folder doesn't exist within the BepInEx directory, create it.
   - Your final path after extracting should be (on Windows): `C:\Program Files (x86)\Steam\steamapps\common\Rogue Legacy 2\BepInEx\plugins\RL2Archipelago`

### Generating the Archipelago

1. Make sure the person generating the Archipelago has a player YAML and the `.apworld` file.
   - There are two easy ways to get a player YAML:
     - Download `RL2_template.yaml` from the GitHub releases page, then edit it to customize your run. You can rename it to anything you like.
     - Use the Archipelago launcher: first install the APWorld via "Install APWorld", then use the "Options Creator" to generate a player YAML.
   - Whoever generates the Archipelago will need to add the `.apworld` to their Archipelago launcher installation.

## Disclaimers

- This mod requires a legitimate copy of Rogue Legacy 2.
- This is an unofficial mod and is not affiliated with Cellar Door Games.

## Contact Info

Feel free to open an issue here on GitHub for any questions or bugs. You can also reach me on Discord at @zaphim12. I'm in the [Archipelago Discord server](https://discord.gg/8Z65BR2) and can be messaged directly or via the `#rogue-legacy-2` channel.
