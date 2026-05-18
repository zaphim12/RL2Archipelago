 # Rogue Legacy 2 Archipelago Randomizer
This is a mod for Rogue Legacy 2 which integrates the game with the [Archipelago project](https://archipelago.gg)

## Gameplay and Randomization Features / Changes
The run is completed as normal, by defeating Cain. All biome bosses must be defeated to unlock the golden doors as normal. 
All collectables in the run are randomized:
* All manor upgrades (class unlocks, stat increases, etc.), heirlooms, blueprints, runes, teleporter unlocks, are shuffled into randomized locations
* Locations where items can be found are the following:
    * Fairy chests
    * Normal and Silver chests (items drop with the same probability as blueprints would drop in normal gameplay)
    * Boss and mini-boss kills
    * Journals and memories (configurable)
    * Manor upgrades
    * Pizza girl purchases
    * Heirloom statues (and Johann's conversation in Pishon Dry Lake after defeating Irad)

## Configurable options (i.e. The player YAML)
There are several things that can be customized for each run using the Archipelago YAML file

### Deathlink (death_link)
Can be enabled or disabled

Kills your character when another player with deathlink enabled dies. Triggers deaths for other players with deathlink enabled when you die.

### Number of Normal/Silver chest checks per biome (blueprint_checks_per_biome)
Can be set to any integer between 0 and 16. Default 11 (11 checks with 6 biomes (66 checks) approximately matches the total number of blueprints available in the base game, 65)

Normal and silver chests have a random chance to roll into an item (same probability as the base game, 15% for a normal chest, 99% for a silver chest). This setting controls how many AP checks you can get from opening chests in each biome.
Once all checks have been completed in a biome, all chests will only give gold, like the base game.

### Number of Fairy chest checks per biome (rune_checks_per_biome)
Can be set to any integer between 0 and 16. Default 4 (4 checks with 6 biomes (24 checks) matches the total number of runes available in the base game, 24)

When a fairy chest is opened, an AP item will be given every time until the pool is exhausted. Then chests will just drop red aether as normal.

Normal and silver chests have a random chance to roll into an item (same probability as the base game, 15% for a normal chest, 99% for a silver chest). This setting controls how many AP checks you can get from opening chests in each biome.

### Manor Upgrade Bundle Size (manor_upgrade_bundle_size)
Can be set to any number between 0 and 35. Default 5

Many manor upgrades have multiple levels available. In the randomizer, granting only one level at a time would make progression feel too slow for most players. So instead, these multi-level upgrades are granted in bundles. 
Checking one AP location can grant a bundle which gives 'n' levels to a manor upgrade (e.g. +5 dexterity instead of +1). 

This setting controls how big this bundle size is. This also dictates how many AP items will be created which correspond to manor upgrades. If there are 20 total levels for a particular upgrade, then a bundle size of '5' will 
result in 4 AP items being randomized. A bundle size of '10' will only result in 2 AP items being randomized. The total number of levels for a particular slot is dictated by the max-level without any NG+ purchases from the soul shop.
Generally, increasing this number will make the run easier, and decreasing it will make it more difficult. 

If the max level for a particular skill is not divisible by the bundle-size, then the last bundle received will get capped to the max level and will grant fewer levels than the previous bundles.


### Manor Useful Count (manor_useful_count)
Can be set to any number between 0 and 35. Default 1

This is related to the previous config. Within Archipelago, items can be classified as "useful" or "filler". Filler items are considered not important and can be ignored if there are too few locations to fit them. 
In addition, "useful" items are shuffled such that they are spread throughout a run to help the player progress at a reasonable rate. This can be modified if a player has customized their [progression_balancing](https://archipelago.gg/tutorial/Archipelago/advanced_settings_en#game-options)
to make sure that a higher number of "useful" items appear earlier or later in the run to alter the difficulty.

This setting controls how many bundles per manor upgrade are considered "useful". All single-level upgrades are always considered "useful". Setting this config to "0" will mean that all multi-level manor upgrades will be considered "filler".

### Journal/Memory Checks (journal_checks)
Can be set to three different values, "disabled" "individual" or "grouped". Default is "grouped"

This controls how AP checks are awarded from reading journals and memories. When disabled, no AP checks are awarded for journals/memories. When set to "individual" a check is completed for every unique journal or memory that the player reads.
When set to "grouped" one check is awarded after reading all journals in a given biome, and one check is awarded after reading all memories in a given biome. 

There are 41 total memories and journals to read. And there are 8 groups (since some biomes have no memories and therefore no check for a group).

### Randomize NPC Unlocks (randomize_npc_unlocks)
Can be set to true or false. Default is true

There are three NPCs which are very valuable. The living safe, the enchantress, and the blacksmith. Without these NPCs, the player is significantly weakened. When they are randomized, they may appear very late into a run, making 
progress much more difficult. 

This setting allows the NPCs to remain at their default unlock locations to ensure that they can be encountered and purchased early on to prevent this difficulty.


### Trap Count (trap_count)
Can be set to any integer between 0 and 25. Default is 3

Traps are a feature in Archipelago where checking a location may actually give the player a negative reward which impedes them. 
In Rogue Legacy 2, the traps cause a random biome environmental burden to activate for the remainder of the current heir's life. This burden will occur in all valid rooms, not just the biome where it is intended to be active.
There are 5 traps currently: Cannonball Rain (Burden of Mundi's Flagship), Dragon Lancers (Burden of Irad's Torment), Automaton Swarm (Burden of Pishon's Uprising), Giant Snowflakes (Burden of Kerguelen's Frost), Void Waves (Burden of the High Scholar's Metamorphosis)

This setting controls the number of traps that are randomized among locations in a given run.


### Manor Upgrade Costs (manor_cost_base, manor_cost_min_subtractive_factor, manor_cost_max_additive_factor)
Manor Cost Base can be set to any integer between 1 and 9999 Default is 100. 
Manor Cost Min Subtractive Factor can be set to any integer between 0 and 100. Default is 20. 
Manor Cost Max Additive Factor can be set to any integer between 0 and 500 Default is 50. 

The typical manor upgrade costs don't scale well in an Archipelago run. Therefore, a custom pricing formula was created to assign costs per upgrade. This formula includes 4 variables. The three where are set by the player, and a
fourth which is the "depth". The depth is determined by how many manor upgrades were required to be purchased before this slot was unlocked. Each time a manor slot is purchased, 1-3 adjacent manor slots become available for purchase. 
We use this to assign "depth". The first available slot is depth 1. Any slots which appear available after the first purchase are depth 2, and so on up to a maximum depth of 11.

The formula is: cost = base * depth * random_factor

The random_factor is determined by the two configurable factors. It is a random decimal chosen uniformly between (1 - subtractive_factor/100) and (1 + additive_factor/100). 
So by default, the cost of an upgrade is base*depth. And then we randomly modify this to make it up to 20% cheaper or 50% more expensive. 


## Installation

You must firstly have Rogue Legacy 2 already installed on your computer. 

I have not tested compatibility on Mac or Linux, but it likely should work fine.

All files needed can be downloaded from the Github releases page. Just look at the most recent release and scroll down to Assets to find what you need.

### Installing the Mod
1. Download and Install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html). This is essentially the injector which allows the mod to modify the gameplay without altering the game files themselves.
  a. You don't need a specific version. The latest stable release should work fine. The installation instructions above should provide a link to the Github page hosting the installer. Scroll down to find "Assets" and download the one for your operating system.
  b. Extract the BepInEx files into the game's directory. On Windows your extracted directory would typically be: C:\Program Files (x86)\Steam\steamapps\common\Rogue Legacy 2\BepInEx
  c. Run the game once so BepInEx can generate any files needed.
2. Download and extract the Rogue Legacy 2 Archipelago mod files into BepInEx's plugin folder.
  a. If you are just playing the game and not generating the Archipelago world, you only need the RL2Archipelago.zip file.
  b. If the plugins folder doesn't exist within the BepInEx directory, create it
  c. Your final directory after extracting should be (on Windows): C:\Program Files (x86)\Steam\steamapps\common\Rogue Legacy 2\BepInEx\plugins\RL2Archipelago

### Generating the Archipelago
3. You need to make sure that the person generating the Archipelago has a player YAML and the .apworld file
  a. There a two easy ways to get a player YAML
    i. You can download the RL2_template.YAML file from the Github releases page. You can then modify it to customize any aspects of your run. You can rename this file to anything you want, as well. 
    ii. If you have the Archipelago launcher, you can use first "Install APWorld" to add the Rogue Legacy 2 APWorld to your launcher. Then you can use the "Options Creator" to create an player YAML.
  b. Whoever generates the Archipelago will need to add the .apworld to their Archipelago launcher installation. 

## Disclaimers

This mod requires a legitimate copy of Rogue Legacy 2.
This is an unnofficial mod, and is not affiliated with Cellar Door Games.

## Contact Info

You can feel free to create any issues here on Github for questions/bugs related to the mod. 
You can also message on Discord, @zaphim12. I am in the [Archipelago discord sever](https://discord.gg/8Z65BR2) server. You can message me directly or use the `#rogue-legacy-2` channel to chat.
