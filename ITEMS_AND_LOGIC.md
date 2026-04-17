# Rogue Legacy 2 — Archipelago Items and Logic

---

TODO: Figure out if Pallas' Void Bell allows air dashes without Ananke's Shawl (if so, it can replace Ananke's Shawl anywhere else)

## Tier 1 — Key Items

These are items which can unlock new areas/spheres
- Aesop's Tome -> unlocks all memory related checks (in unlocked biomes)
- Ananke's Shawl -> unlocks lamech boss fight, and the echo's boots heirloom location
    - (if Kerguelen unlocked) Unlocks Naamah
- Echo's Boots -> unlocks axis mundi and kerguelen plateau checks, plus aether's wings location
- Aether's Wings -> (if kerguelen unlocked) Unlocks Naamah
- Pallas' Void Bell -> (need to test if this works without ananke's shawl)
    - (if echo's boots) Unlocks sun tower checks
    - Unlocks Stygian minibosses and therefore also Enoch
- Theia's Sun Lantern -> Unlocks Pishon checks
    - (if Pallas) Unlocks Pishon Onyx mini boss and Tubal

## Tier 2 — Included and Potentially Included

### Manor Upgrades
- Not sure how I will display these, that will probably take a lot of work. But essentially, every class unlock must be a manor upgrade, and the other character improvements should also come from checks. They will probably need to be bundled in some way (e.g. +5 strength upgrades, instead of just +1). The number of these and how many upgrades are in each bundle should be configurable

### Portal Unlocks
- These enable new checks, and I like the idea of the biome order being somewhat arbitrary so I think these should probably come as checks
- But maybe it's worth considering an option where beating the boss of an area unlocks that area's portal

### Runes and Blueprints
- Fairly straightforward. This can also be somewhat configurable to allow level 2 runes and such. But by default it should probably just be only level 1 runes and blueprints.

### Mini-boss completion status
- This can be considered, but for now I think that if killing bosses unlocks the final boss, then killing mini-bosses should do the same for their associated boss. 

## Tier 3 — Likely not to be included
- Perhaps it could be worth adding checks for things like locking class or skill. But this feels like it would take away from the randomization and maybe isn't worth adding

## Logic — What is needed to complete each check?
- Note: For now, we will only consider skill items and runes. Character strength upgrades can be considered later

### Boss kills:
* Lamech: 
  * Ananke's Shawl
  * Aether's Wings
* Void Beasts:
  * Kerguelen Plateau teleporter + Ananke's Shawl + Aether's Wings
  * Echo's Boots
* Naamah:
  * Echo's Boots + Aether's Wings
* Enoch:
  * Pallas' Void Bell + Murmur kill + Gongheads kill (i.e. Echo's Boots)
* Irad:
  * Ananke's Shawl + Echo's Boots + Aether's wings + Pallas' Void Bell
* Tubal:
  * Theia's Sun Lantern + Pearl Key Boss kill + Onyx Key Boss kill (i.e. Echo's Boots + Pallas' Void Bell)

### Mini Boss kills:
* Murmur:
  * Echo's Boots + Pallas' Void Bell
* Gongheads:
  * Aether's Wings
  * Pallas' Void Bell
* Onyk Key Boss:
  * Pallas' Void Bell + Theia's Sun Lantern
* Pearl Key Boss:
  * Theia's Sun Lantern + Echo's Boots

### Heirloom Locations
* Aesop's Tome
  * Always accessible
* Ananke's Shawl
  * Always accessible
* Echo's Boots
  * Always accessible
* Aether's Wings
  * Echo's Boots
* Pallas' Void Bell
  * Aether's Wings
  * Pallas' Void Bell
* Theia's Sun Lantern
  * Irad kill

### Teleporter Unlock
* Axis Mundi, Stygian Study, Sun Tower, and Pishon Dry Lake:
  * Accessible after finding Pizza Mundi (aka either Aether's Wings + Ananke's Shawl or Echo's Boots or Sun Tower teleporter unlock)
* Kerguelen Plateau:
  * Pizza Mundi + either Kerguelen Teleporter unlock, Ananke's Shawl + Aether's Wings, or Echo's Boots (for void beasts boss kill)

### Biome Access (Affects Blueprints, Runes, and Memories/Journals)
* Citadel Agartha:
  * Always Accessible
* Axis Mundi:
  * Aether's Wings + Ananke's Shawl
  * Echo's Boots
* Kerguelen Plateau:
  * Teleporter
  * Echo's Boots
* Stygian Study:
  * Aether's Wings
  * Pallas' Void Bell
* Sun Tower:
  * Ananke's Shawl + Echo's Boots + Aether's wings + Pallas' Void Bell
* Pishon Dry Lake:
  * Theia's Sun Lantern