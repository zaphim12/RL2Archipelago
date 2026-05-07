## General

[x] Have the Archipelago connection dialog box appear as if it's a typical settings menu from Rogue Legacy 2
  - If this proves intractable, then at least make sure main menu input isn't still enabled while the box is displaying on top of it
[x] Implement save-data specific to the Archipelago
  [x] Make sure that items being received are saved to the save file as they should be
  [x] Make sure that the Archipelago loads this save file if the game is closed and then the run is later resumed
  [x] I think we can have the save-data for each archipelago run be tied to that archipelago's seed. 
[x] See if it's possible to display any received items using existing text overlays in the game (for example, some text is shown when doors are unsealed or things like that)
  - Worst case scenario, this can just be some manual text overlay that appears in a corner of the screen
[ ] Figure out what to do as filler items if we run out of slots
  - This is probably not an issue, as manor upgrades already occupy this
[ ] Decide how to truncate when there are too many items and not enough locations
  - Perhaps first fill in all items that aren't manor upgrades, blueprints, or runes. Then do some math to allocate items based on giving for example 25% of remaining to blueprints, 25% to runes, and 50% to manor upgrades. Which items are allocated should be random
  - When filling in manor upgrades, we should only allocate 'x' number of bundles per upgrade, but randomize among remaining slots. So for example, if 'x' is 2 then you may end up with 2 strength, 1 focus, and 0 dexterity depending on how it is randomized
  - Alternatively, I could try and add more location checks for misc. tasks (like kill 'x' enemies) to try and make up for the manor giving so many items but not enough checks

## Receiving Items:
[x] Heirlooms
  [x] Ensure that when an heirloom is collected, its corresponding non-randomizer location still lets you complete an AP check
  [ ] Ensure that statues display the respective item they're supposed to grant, not their original item
    [x] Display correct randomized heirloom on the statue
    [ ] Display correct randomized runes/blueprints on the statue
    [ ] Display correct randomized manor upgrade on the statue
  [x] Ensure that the statue remains empty for this and future runs
  [x] Ensure Johan in Pishon Dry Lake only gives location check one time, after Irad is defeated
  [x] Ensure that for Citadel Agartha rooms, relics spawn on future runs
[ ] Portal Unlocks
  [ ] Once implemented, make sure that biome access checks are opened by Kerguelen Plateau teleporter
[x] Blueprints
[x] Runes
[ ] Manor upgrades
  [ ] See if unlocking the Knight class works, and if so try to randomize the starting class and allow Knights to be unlocked later
  [x] Give rewards in a bundle of configurable size (e.g. 5 strength upgrades per item)
  [x] Have some number of rewards be useful and the rest be filler
  [ ] Allow setting a maximum number of total bundles

## Locations:
[x] Boss kills
[x] Miniboss kills
[x] Heirloom pick-ups
  [x] Ensure that the post-heirloom-collection platforming challenge does not trigger, and location check counts regardless
  [x] Ensure that the heirloom pick-up rooms are replaced with relic choice rooms when appropriate if they've been picked up before
[ ] Pizza girl teleporters fee paid
[x] Runes received (Open fairy chest)
  [x] Have a configurable number of rune drop locations per biome (0-16 currently, 4 by default)
[x] Blueprints received (Bronze/Silver chest rolls into blueprint)
  [x] Have a configurable number of blueprint drop locations per biome (0-16 currently, 11 by default)
[ ] Journals and Memories
[ ] Skill tree node purchases
  [x] Config for blacksmith, enchantress, and living safe to be non-randomized
[ ] Charon tribute rewards maybe
  - Maybe charity donation should be in effect by default?