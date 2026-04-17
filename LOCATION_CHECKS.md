# Rogue Legacy 2 — Archipelago Location Checks

Derived from Assembly-CSharp.dll decompilation. Organized by category and priority tier.
Each entry represents a potential **location** (something the player "checks off") that could
contain a randomized item in the multiworld.

---

## Tier 1 — Core / High-Value Checks

These are the most natural checks for an RL2 randomizer. Every serious run touches them.

### Main Bosses (First-Time Defeat)
Tracked via `PlayerSaveFlag.*_Defeated_FirstTime`. One check per boss per NG cycle, or just first NG. These should keep their normal logic of unlocking the endgame door, but also should count as a location-check.

| ID | Boss Name | Biome |
|----|-----------|-------|
| `CastleBoss_Defeated_FirstTime` | Estuary Lamech | Citadel Agartha |
| `BridgeBoss_Defeated_FirstTime` | Void Beasts Byarrrith and Halpharr | Axis Mundi |
| `ForestBoss_Defeated_FirstTime` | Estuary Naamah | Kerguelen Plateau |
| `StudyBoss_Defeated_FirstTime`  | Estuary Enoch | The Stygian Study |
| `TowerBoss_Defeated_FirstTime`  | Estuary Irad | The Sun Tower |
| `CaveBoss_Defeated_FirstTime`   | Estuary Tubal | Pishon Dry Lake |
| `GardenBoss_Defeated_FirstTime` | Jonah | Garden of Eden |
| `FinalBoss_Defeated_FirstTime`  | The Traitor / Cain | Castle Hamson |


### Heirloom Acquisition
Found in dedicated Heirloom rooms accessed from each biome. Tracked via `PlayerSaveData.HeirloomLevelTable`.
Each heirloom is a major mobility/ability unlock — natural randomizer items *and* locations.
- Note: The post-heirloom collection platforming challenges should probably just be entirely skipped since they essentially function as a tutorial and you also won't necessarily have the item that you're supposed to get from that heirloom location yet to complete it.
  - Instead of opening a dialog to enter the challenge, it should just instead trigger the AP item collection

| Heirloom | Name | Source Biome |
|----------|-----------------|--------------|
| `UnlockAirDash` | Ananke's Shawl | Citadel |
| `UnlockDoubleJump` | Aether's Wings | Plateau |
| `UnlockMemory` | Aesop's Tome | Citadel |
| `UnlockBouncableDownstrike` | Echo's Boots | Axis Mundi |
| `UnlockVoidDash` | Pallas' Void Bell | Study |
| `CaveLantern` | Theia's Sun Lantern | Pishon Dry Lake |

| `UnlockEarthShift` | Gilgamesh's Anchor | Cave | - This likely should not be randomized, as once you can reach the final boss, you should be able to just go win
| `RebelKey` | Mysterious Key | Defeat final boss in NG+ 6 | - This likely should not be randomized, as it would take far too long to encounter
| `Fruit` | Fruit of Life | Beyond the Golden Doors | - This likely should not be randomized, as once you can reach the final boss, you should be able to just go win


### Miniboss Defeats
Tracked via `PlayerSaveFlag.*Miniboss*_Defeated`.

| Check | Miniboss | Location |
|-------|----------|----------|
| `StudyMiniboss_SwordKnight_Defeated` | Void Beast Murmur | The Stygian Study |
| `StudyMiniboss_SpearKnight_Defeated` | Void Beast Gongheads | The Stygian Study |
| `CaveMiniboss_White_Defeated` | Briareus the Hammer and Cottus the Sword | Pishon Dry Lake |
| `CaveMiniboss_Black_Defeated` | Gyges the Axe and Aegaeon the Shield | Pishon Dry Lake |

---

## Tier 2 — Standard Checks

These are reasonably natural for a full randomizer run and add interesting variety. On by default, but can be toggled off by a player

### Fairy Chests (Challenge Chests)
- Fairy chests normally give runes, so replacing them with a check seems valid. 
- In a typical run, 24 runes can be acquired, this comes out to 4 runes per biome. This theoretically means that to mirror a run, we would have 24 randomized checks that drop from fairy chests, and after that subsequent fairy chests will give just Aether and no check.
- However, this value will likely be tweaked from playtesting to find a good feeling value

### Biome Teleporter Unlocks
Pizza Girl unlocks teleporters per biome. Tracked via `PlayerSaveData.TeleporterUnlockTable`.
One check per biome teleporter (up to 5 biomes).

### Journal / Memories
Journals are scattered randomly throughout biomes. Tracked via `PlayerSaveData.JournalsReadTable`.
Memories are in always in a particular room/location

- Memories can reasonably be translated 1 to 1 with checks.
- Journals could technically be 1 to 1 as well, but since they don't take any skill/pre-requisites to uncover and it would be unclear how many you've gotten so far and how many are available, and they appear randomly. I feel it makes more sense to just have 1 check for journal entries per biome. 
- Alternatively, the check could require uncovering all journal entries in a biome for the single check. But this seems boring and harsh for a single check.

| Biome Category | # Journals | # Memories
|----------------|--------------|
| `Castle` | 4 | 4 |
| `Bridge` | 4 | 0 |
| `Forest` | 4 | 5 |
| `Study`  | 6 | 0 |
| `Tower`  | 7 | 0 |
| `Cave`   | 7 | 0 |

> Note: Achievement flags `StoryJournalsCastle/Bridge/Forest/Study/Tower/Cave` suggest
> grouping all journals in a biome as a single check may be simpler.

### Skill Tree Node Purchases
The full skill tree has 72 purchasable nodes (see `SkillTreeType` enum). Options:

- **Option A**: One check per node (high location count, very granular)
- **Option B**: One check per major category (Health, Attack, Magic, Gold, etc.)
- **Option C**: Milestone checks (e.g., "unlock 10 skills", "unlock all class skills")

- Option A seems best. Just replace a node (even if the node has tiered purchases) with just a single purchase that counts as a check.
- There will likely need to be balancing done to make sure gold costs are reasonable.

### Regular Chest Openings (Bronze / Silver / Gold)
Every room can have a Bronze, Silver, or Gold chest. These are reset each run.
Tracked per-room via `StageSaveData.ChestTrackerDataList` and `RoomSaveData.ChestStates`.
- For bronze and silver chests, if these chests would roll into an item, then we intercept that and instead give an AP check. This will be limited to a certain number of checks available in chests per biome. Maybe around 10? But it should also be configurable by the player
- There are 13 armor sets with 5 pieces each which translates to 65 blueprints. This corresponds to ~10 checks per biome which seems like a good number, but it can be tweaked during playtesting.
- For silver and gold chests, since empathies are likely to be useless in a run, maybe they can be modified to give a location hint or give a mastery level up on the current character if they are going to drop an empathy (up to a certain number of times)

---

## Tier 3 — Granular / Optional Checks

These are valid checks but don't seem suitable for a default run. If I decide to implement them, they can be enabled by player choice

### Boss Prime (Advanced) — First-Time Defeat
Tracked via `PlayerSaveFlag.*_Prime_Defeated_FirstTime`. Harder NG+ versions of each boss. These likely should be disabled for randomization by default, since a typical run should not require NG+.

| Check |
|-------|
| `CastleBoss_Prime_Defeated_FirstTime` |
| `ForestBoss_Prime_Defeated_FirstTime` |
| `BridgeBoss_Prime_Defeated_FirstTime` |
| `StudyBoss_Prime_Defeated_FirstTime` |
| `TowerBoss_Prime_Defeated_FirstTime` |
| `CaveBoss_Prime_Defeated_FirstTime` |
| `GardenBoss_Prime_Defeated_FirstTime` |
| `FinalBoss_Prime_Defeated_FirstTime` |

### Class Mastery Ranks
Each of the 15 classes has a mastery rank earned by playing that class.
Tracked via `PlayerSaveData.MasteryXPTable`. Could check at:
- Reaching mastery rank milestones (up to some reasonable limit- maybe level 10?)
- This doesn't seem like it'd make for interesting gameplay. Instead it would be grindy and also could block checks behind classes that you haven't been offered in a long time. Plus unlocking mastery can be seen as progressing your run by simply making you stronger and more capable of completing other checks

### Unlocking the ability to find required relics
It could be that the locations which normally have the Onyx/Pearl Keys and Lilies can have normal relics until a check is unlocked which begins spawning these keys
- This seems like it could be just an unnecessary braking force, so I think I'll leave it out for now 

### Boss Insights
The boss insight memories could be a location. However, it's easy to miss these and it's basically a memorization test for the player, so it seems silly to have these as checks

---

## Tier 4 — Theoretically could be checks, but are being discluded

### Boss Chests
Each boss room spawns a chest on defeat. Tracked via `ChestObj.BossID` / `ChestSpawnController.BossID`.
One check per boss (8 total).
- Discluded because clearing the boss is already a check itself. So this would be redundant. Plus boss chests don't normally drop items, so they don't seem suitable for replacing with AP items.


### Scar Challenge Completions
Scar challenges are the named challenge rooms (tracked via `Challenge_EV.ScarUnlockTable`, `InsightType.ScarChallenges_Complete`).
- Discluded because scar challenges can be totally ignored in a normal playthrough. They are only really useful when completing many playthroughs and are generally unrelated to progression through a run. So they don't seem like a good choice for randomizing 
- It may even be worth disabling scars and soul stones entirely if this doesn't get randomized, since they wouldn't make sense as part of an AP run

**Named Scars / Challenges (ChallengeType):**
- `TwinMech` — Two Masters
- `PlatformRanger` —  Narrow Praxis
- `BrotherAndSister` — Bladed Rose
- `SmallChest` — Closed Space
- `PlatformBoat` — Preserver of Life
- `PlatformAxe` — Heavy Weapons
- `IntroCombat` — Simple Start
- `PlatformKatana` — The Rebels' Road
- `TwoLovers` — The Two Lovers
- `NightmareKhidr` — Nightmare Premonitions
- `PlatformClimb` — The Atlantis Spire
- `BigBattle` — The Armada
- `SubBossBattle` — Spreading Poison
- `FourHands` — Automatons
- `TwoRebels` — Divergent Dimensions
- `DragonAspectFight` — DREAM: Dragon Flight
- `PlatformSurf` — DREAM: Boogie Days
- `QuinnFight` — Training Daze

### Soul Shop Purchases
Permanent meta-progression unlocks bought with Souls. Tracked via `ModeSaveData.SoulShopTable`.
- Discluded for the same reason as the above. They're not really tied to run-progression and they'd be slow to grind for and uninteresting since checks can already be bought with coins anyways.

**Notable Soul Shop entries:**
- `BaseStats01-04MaxUp` — Permanent stat increases
- `ChooseYourClass` / `ChooseYourSpell` — Lineage customization
- `MaxMasteryFlat` — Mastery cap increase
- `MaxCharonDonationFlat` — Gold carry increase
- `ReduceBurdenFlat` / `BurdenOverload` — Burden/NG+ management
- `MaxEquipmentDrops` / `MaxRuneDrops` — Better drop rates
- Fabled Weapon variants (ArcherVariant, AxeVariant, BoxerVariant, etc.) — alternate class kits
- `ForceRandomizeKit` / `UnlockJukebox` / `OreAetherSwap` / `UnlockOverload`

### Eggplant Hunt (4-Stage Collectible Chain)
- Disculded for being very difficult to reach (without house rules) and kinda random. Also the tutorial eggplant can be permanently missed on a run 

- `FoundEggplant_Basic`
- `FoundEggplant_Advanced`
- `FoundEggplant_Expert`
- `FoundEggplant_Miniboss` 

### Dragon Keys, Lilies, Relics
- Discluded because these are fundamentally impermanent and easy to come by. Would dilute the pool and not make sense to receive as checks either. What would you do if you receive one of the keys and then die? Does it persist with you as a permanent drain on resolve?
- `DragonKeyBlack`
- `DragonKeyWhite`
- `Lily1`, `Lily2`, `Lily3`


---


## Implementation Notes

### Order of implementation (~200 checks)

1. **8** Main boss kills
3. **6** Heirlooms
4. **4** Miniboss kills (Study ×2, Pishon ×2)
5. **<=24** Fairy Chest Runes
9. **60** Bronze + Silver Chest Blueprints
6. **15** Journal + Memories
7. **5** Biome Teleporter Unlocks
8. **72** Skill Tree Nodes

### Progression-Gating Considerations
These items are natural progression gates (give as randomized items):
- Heirlooms (especially Dash, Double Jump, Void Dash, Earth Shift) — gate platforming
- Cave Lantern — gates Cave navigation
- Rebel Key — gates Rebel Door area
- Teleporter unlocks — gate fast travel
- Class unlocks — gate available playstyles
- Skill tree nodes — gate stats
