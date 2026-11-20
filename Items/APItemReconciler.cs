using RL2Archipelago.Locations;
using RL2Archipelago.Patches;
using RL2Archipelago.Traps;
using System;
using System.Collections.Generic;

namespace RL2Archipelago.Items;

/// <summary>Outcome of one <see cref="APItemReconciler.Reconcile"/> pass.</summary>
internal readonly struct ReconcileResult
{
    /// <summary>
    /// Number of pieces of <em>game save</em> state that were below their target and had to be
    /// raised. Drives the forced profile save, so it deliberately excludes work that touches only
    /// in-memory state (trap flags) or only the run state.
    /// </summary>
    public int CorrectionsApplied { get; }

    /// <summary>True if the pass mutated <see cref="APRunState"/> and it needs persisting.</summary>
    public bool RunStateChanged { get; }

    /// <summary>True if any single item threw. The rest of the pass still ran.</summary>
    public bool AnyFailure { get; }

    public ReconcileResult(int correctionsApplied, bool runStateChanged, bool anyFailure)
    {
        CorrectionsApplied = correctionsApplied;
        RunStateChanged = runStateChanged;
        AnyFailure = anyFailure;
    }

    /// <summary>True when something was written to the game save and should be flushed to disk.</summary>
    public bool ChangedSaveData => CorrectionsApplied > 0;
}

/// <summary>
/// Brings the game save up to the state the multiworld says this slot is owed, by deriving an
/// <em>absolute</em> target for every received item and raising anything that falls short.
///
/// <para>Two properties make this safe to run repeatedly:</para>
/// <list type="bullet">
/// <item><b>Absolute targets.</b> Every write is "set to N", never "add one", so running twice is the
/// same as running once.</item>
/// <item><b>Monotone up.</b> A value is only ever raised, never lowered. That keeps the reconciler
/// from fighting any vanilla grant path the mod has not intercepted.</item>
/// </list>
/// </summary>
internal static class APItemReconciler
{
    /// <summary>
    /// Safe to write save data. Deliberately does <em>not</em> require being in-game: the game's
    /// accessors read the <c>SaveManager</c> statics directly, so as long as every write passes
    /// <c>runEvents: false</c> this can run at the main menu - which is where the important repair
    /// happens, before the player ever presses Start.
    /// </summary>
    internal static bool CanApplySaveData =>
        APClient.RunState != null
        && SaveManager.IsRunning
        && !SaveManager.LoadingFailed
        && SaveManager.PlayerSaveData != null
        && SaveManager.EquipmentSaveData != null
        && SaveManager.ModeSaveData != null   // fabled weapons live in ModeSaveData.SoulShopTable
        && !ManorUpgradePatch.IsRemappingLevels;

    /// <summary>
    /// Safe to touch live world objects (ability refresh, trap pooling, gold HUD events). Requires an
    /// actual built world - <c>IsInGame</c> alone is not enough, because it flips the moment the Start
    /// button is clicked, long before <c>PlayerManager</c> exists.
    /// </summary>
    internal static bool CanApplyWorldEffects =>
        CanApplySaveData
        && APClient.IsInGame
        && PlayerManager.IsInstantiated
        && (!WorldBuilder.IsInstantiated || WorldBuilder.State == BiomeBuildStateID.Complete);

    /// <summary>
    /// Raises every piece of save state that is below what the ledger says it should be. Cheap when
    /// there is nothing to do: one read per received item.
    /// </summary>
    internal static ReconcileResult Reconcile()
    {
        var runState = APClient.RunState;
        if (runState == null) return default;

        bool worldReady = CanApplyWorldEffects;
        int corrections = 0;
        bool anyFailure = false;
        bool refreshAbilities = false;
        bool runStateChanged = false;

        var startingClassSlot = ParseStartingClassSlot(runState.StartingClassSkill);

        foreach (var entry in runState.ReceivedItemCounts)
        {
            if (entry.Value <= 0) continue;

            try
            {
                corrections += ApplyItem(
                    entry.Key, entry.Value, runState, startingClassSlot,
                    ref refreshAbilities, ref runStateChanged);
            }
            catch (Exception ex)
            {
                anyFailure = true;
                Plugin.Log.LogError(
                    $"[APSync] Failed to reconcile '{DisplayName(entry.Key)}' (ID {entry.Key}): {ex.Message}");
            }
        }

        // The loop above only visits slots that appear in the ledger, so a randomized starting class
        // whose slot has received no manor items of its own would never be applied.
        try
        {
            corrections += ApplyStartingClassSlot(runState, startingClassSlot);
        }
        catch (Exception ex)
        {
            anyFailure = true;
            Plugin.Log.LogError($"[APSync] Failed to reconcile the starting class slot: {ex.Message}");
        }

        // Traps are per-life, not cumulative: PlayerDeathPatch clears both TrapManager's flags and
        // RunState.ActiveTraps on death. So they are reconciled against ActiveTraps, never against the
        // received ledger, which would resurrect every trap the player had ever been sent.
        if (worldReady)
            SyncActiveTraps(runState);

        if (refreshAbilities && worldReady)
        {
            try
            {
                PlayerManager.GetPlayerController()?.InitializeAbilities();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[APSync] Could not refresh player abilities: {ex.Message}");
            }
        }

        if (corrections > 0)
            Plugin.Log.LogInfo(
                $"[APSync] Reconciled {corrections} item effect(s) against the multiworld ledger.");

        return new ReconcileResult(corrections, runStateChanged, anyFailure);
    }

    /// <summary>
    /// Dispatches one ledger entry. The Knight Class item sits just past the end of the manor slot 
    /// range and must be matched before <see cref="ItemRegistry.ToSkillTreeType"/> gets a look at it.
    /// </summary>
    private static int ApplyItem(
        long itemId,
        int count,
        APRunState runState,
        SkillTreeType? startingClassSlot,
        ref bool refreshAbilities,
        ref bool runStateChanged)
    {
        var heirloom = ItemRegistry.ToHeirloomType(itemId);
        if (heirloom.HasValue)
            return ApplyHeirloom(itemId, heirloom.Value, ref refreshAbilities);

        var equipBlueprint = ItemRegistry.ToEquipmentBlueprint(itemId);
        if (equipBlueprint.HasValue)
            return ApplyEquipmentBlueprint(itemId, equipBlueprint.Value, count);

        var rune = ItemRegistry.ToRuneType(itemId);
        if (rune.HasValue)
            return ApplyRuneBlueprint(itemId, rune.Value, count);

        // ── Class items: only this seed's set may take effect ────────────────
        //
        // Class items use one of two sets depending on the fabled_weapons setting.
        // The two sets cover the same 15 classes and differ only in how many stages they have, so
        // a seed generates one or the other. Both are in the global item table though, which means
        // the wrong one can still arrive (e.g. from the cheat cnosole). Honouring it would hand over
        // something the seed never offered: a fabled weapon in a run that disabled them, or a class
        // unlock that was supposed to come from the progressive item. Both are checked explicitly
        // rather than inferred from copy counts.
        var progressiveClass = ItemRegistry.ToProgressiveClassPosition(itemId);
        if (progressiveClass.HasValue)
        {
            if (!APClient.FabledWeapons) return IgnoreWrongClassItem(itemId, "fabled weapons are off");

            return ApplyProgressiveClass(
                progressiveClass.Value, count, runState, startingClassSlot, ref runStateChanged);
        }

        if (APClient.FabledWeapons && ItemRegistry.IsPlainClassItem(itemId))
            return IgnoreWrongClassItem(itemId, "this seed unlocks classes through the progressive item");

        if (itemId == ItemRegistry.KnightClassItem)
            return ApplyKnightClass(runState, ref runStateChanged);

        var skill = ItemRegistry.ToSkillTreeType(itemId);
        if (skill.HasValue)
            return ApplyManorUpgrade(itemId, skill.Value, count, startingClassSlot);

        var teleporter = ItemRegistry.ToTeleporterBiomeType(itemId);
        if (teleporter.HasValue)
            return ApplyTeleporter(itemId, teleporter.Value);

        // Gold is consumable and cannot be read back out of the save, so it is granted by ordinal in
        // APClient rather than reconciled. Traps are handled per-life via SyncActiveTraps.
        if (itemId == ItemRegistry.GoldCoins) return 0;
        if (ItemRegistry.ToTrapBurdenType(itemId).HasValue) return 0;

        Plugin.Log.LogError($"[APSync] No handler for item '{DisplayName(itemId)}' (ID {itemId}).");
        return 0;
    }

    /// <summary>Item IDs already reported by <see cref="IgnoreWrongClassItem"/> this session.</summary>
    private static readonly HashSet<long> s_reportedWrongClassItems = [];

    /// <summary>
    /// Drops a class item belonging to the set this seed did not generate.
    ///
    /// <para>Logged once per item rather than every pass: the reconciler re-derives the whole
    /// ledger on every run and this branch, unlike the others, never changes anything, so it would
    /// otherwise repeat for as long as the item sits in the ledger.</para>
    /// </summary>
    private static int IgnoreWrongClassItem(long itemId, string reason)
    {
        if (s_reportedWrongClassItems.Add(itemId))
            Plugin.Log.LogWarning(
                $"[APSync] Ignoring '{DisplayName(itemId)}': {reason}. " +
                "It belongs to the class item set this seed does not use.");

        return 0;
    }

    /// <summary>Clears session-scoped state. Called when a session is torn down.</summary>
    internal static void Reset() => s_reportedWrongClassItems.Clear();

    private static int ApplyHeirloom(long itemId, HeirloomType heirloom, ref bool refreshAbilities)
    {
        if (SaveManager.PlayerSaveData.GetHeirloomLevel(heirloom) >= 1) return 0;

        SaveManager.PlayerSaveData.SetHeirloomLevel(heirloom, 1, additive: false, broadcast: false);
        refreshAbilities = true;
        Plugin.Log.LogDebug($"[APSync] Restored heirloom: {DisplayName(itemId)}");
        return 1;
    }

    /// <summary>
    /// The first copy of an equipment blueprint unlocks the item; each copy after that adds one
    /// upgrade blueprint. So N copies means found-state set and <c>N-1</c> upgrade blueprints.
    /// </summary>
    private static int ApplyEquipmentBlueprint(
        long itemId, (EquipmentCategoryType Category, EquipmentType Type) blueprint, int count)
    {
        var (category, equipType) = blueprint;

        var equipment = EquipmentManager.GetEquipment(category, equipType);
        if (equipment == null) return 0;

        int corrections = 0;

        // overrideValues stays false: FoundState is stored *in* UpgradeLevel, so overriding would
        // stamp the found-state value over a real purchase level and erase the player's upgrades.
        if (EquipmentManager.GetFoundState(category, equipType) < FoundState.FoundButNotViewed)
        {
            EquipmentManager.SetFoundState(
                category, equipType, FoundState.FoundButNotViewed, overrideValues: false, runEvents: false);
            corrections++;
        }

        // Clamp ourselves: SetUpgradeBlueprintsFound refuses (and writes nothing) past MaxLevel.
        int target = Math.Min(count - 1, equipment.MaxLevel);
        if (EquipmentManager.GetUpgradeBlueprintsFound(category, equipType) < target)
        {
            EquipmentManager.SetUpgradeBlueprintsFound(
                category, equipType, target, additive: false, runEvents: false);
            corrections++;
        }

        if (corrections > 0)
            Plugin.Log.LogDebug($"[APSync] Restored blueprint: {DisplayName(itemId)} (x{count})");

        return corrections;
    }

    /// <summary>
    /// Runes are asymmetric with equipment: every copy adds an upgrade blueprint, so N copies means N
    /// blueprints. That is the existing balance, so it is preserved here rather than "fixed".
    /// </summary>
    private static int ApplyRuneBlueprint(long itemId, RuneType rune, int count)
    {
        var runeObj = RuneManager.GetRune(rune);
        if (runeObj == null) return 0;

        int corrections = 0;

        // Set found-state explicitly first. SetUpgradeBlueprintsFound would otherwise do it itself
        // with runEvents left at its default of true, which dereferences the RuneManager singleton -
        // fine in-game, an NRE at the main menu.
        if (RuneManager.GetFoundState(rune) < FoundState.FoundButNotViewed)
        {
            RuneManager.SetFoundState(
                rune, FoundState.FoundButNotViewed, overrideValues: false, runEvents: false);
            corrections++;
        }

        int target = Math.Min(count, runeObj.MaxLevel);
        if (runeObj.UpgradeBlueprintsFound < target)
        {
            RuneManager.SetUpgradeBlueprintsFound(rune, target, additive: false, runEvents: false);
            corrections++;
        }

        if (corrections > 0)
            Plugin.Log.LogDebug($"[APSync] Restored rune blueprint: {DisplayName(itemId)} (x{count})");

        return corrections;
    }

    /// <summary>
    /// Knight Class is the one item whose entire effect lives in the run state rather than the game
    /// save, so it reports through <paramref name="runStateChanged"/> instead of as a save correction
    /// - otherwise the flag would be set in memory, never persisted, and re-applied on every connect.
    /// </summary>
    private static int ApplyKnightClass(APRunState runState, ref bool runStateChanged)
    {
        if (runState.KnightClassReceived && APClient.KnightClassReceived) return 0;

        if (!runState.KnightClassReceived)
        {
            runState.KnightClassReceived = true;
            runStateChanged = true;
        }

        APClient.KnightClassReceived = true;
        Plugin.Log.LogDebug("[APSync] Restored Knight Class unlock.");
        return 0;
    }

    /// <summary>
    /// Applies a progressive class item: the first copy unlocks the class, the second unlocks its
    /// fabled weapon. Only generated when the fabled_weapons option is on. With it off the seed
    /// carries the plain one-stage class items instead, which never reach this path.
    ///
    /// <para>The starting class is unlocked for free at run start, outside the ledger, so for that
    /// one class the first copy received is already the second stage.</para>
    /// </summary>
    private static int ApplyProgressiveClass(
        int position,
        int count,
        APRunState runState,
        SkillTreeType? startingClassSlot,
        ref bool runStateChanged)
    {
        var skill = ItemRegistry.ProgressiveClassSkill(position);

        bool isStartingClass = skill.HasValue
            ? startingClassSlot == skill.Value
            : APClient.StartingClassIndex == 0;   // no backing slot means Knight

        int stages = count + (isStartingClass ? 1 : 0);
        if (stages <= 0) return 0;

        int corrections = 0;

        // Stage 1: the class itself. Knight has no manor slot, it is gated by a run state flag
        // because vanilla hardcodes it into the heir pool and bypasses IsClassUnlocked entirely.
        if (skill.HasValue)
            corrections += RaiseSkillLevel(skill.Value, stages);
        else
            corrections += ApplyKnightClass(runState, ref runStateChanged);

        // Stage 2: the fabled weapon.
        if (stages >= 2)
        {
            var className = DisplayName(ItemRegistry.ProgressiveClassItem(position));
            corrections += ApplyFabledWeapon(ItemRegistry.ProgressiveClassVariant(position), className);
        }

        return corrections;
    }

    /// <summary>
    /// Unlocks a class's fabled weapon by owning and equipping its Soul Shop entry, which is the
    /// single thing <c>LineageWindowController.IsFabledWeaponUnlocked</c> consults. Mirrors the
    /// game's own grant in <c>SpecialMode_EV</c>. Owned must be set first, because
    /// <c>SetEquippedLevel</c> clamps to the owned level.
    ///
    /// <para>Idempotent and monotone up, like every other write in this file: already-unlocked is a
    /// no-op reporting no correction, and nothing here can lower a level.</para>
    /// </summary>
    private static int ApplyFabledWeapon(SoulShopType variant, string className)
    {
        var entry = SaveManager.ModeSaveData?.GetSoulShopObj(variant);
        if (entry == null || entry.CurrentEquippedLevel > 0) return 0;

        entry.SetOwnedLevel(1, additive: false, clampToMaxLevel: true);
        entry.SetEquippedLevel(1, additive: false);

        Plugin.Log.LogDebug($"[APSync] Restored fabled weapon for {className} ({variant}).");
        return 1;
    }

    /// <summary>
    /// Manor levels are owned entirely by Archipelago items: buying a slot in an AP run deliberately
    /// skips the vanilla level grant (see <see cref="ManorUpgradePatch"/>), so an absolute set is
    /// correct and cannot clobber player progress.
    /// </summary>
    private static int ApplyManorUpgrade(
        long itemId, SkillTreeType skill, int count, SkillTreeType? startingClassSlot)
    {
        var skillObj = SkillTreeManager.GetSkillTreeObj(skill);
        if (skillObj == null) return 0;

        // The randomized starting class grants one free bundle on this slot, outside the ledger.
        int bundles = count + (startingClassSlot == skill ? 1 : 0);

        int corrections = RaiseSkillLevel(skill, bundles);
        if (corrections > 0)
            Plugin.Log.LogDebug($"[APSync] Restored manor upgrade: {DisplayName(itemId)}");

        return corrections;
    }

    /// <summary>
    /// Raises a manor skill to the level <paramref name="bundles"/> received bundles entitle it to.
    /// Shared by plain manor upgrades and progressive class items.
    /// </summary>
    private static int RaiseSkillLevel(SkillTreeType skill, int bundles)
    {
        var skillObj = SkillTreeManager.GetSkillTreeObj(skill);
        if (skillObj == null) return 0;

        // Clamp against MaxLevel so a maxed slot doesn't report a correction on every pass.
        int target = Math.Min(bundles * APClient.ManorUpgradeBundleSize, skillObj.MaxLevel);
        if (SkillTreeManager.GetSkillObjLevel(skill) >= target) return 0;

        // runEvents: false keeps UnlockConnectedSkillSlots from revealing adjacent manor slots, which
        // should only happen when the player physically buys the prerequisite.
        SkillTreeManager.SetSkillObjLevel(skill, target, additive: false, runEvents: false);
        return 1;
    }

    private static int ApplyTeleporter(long itemId, BiomeType biome)
    {
        if (SaveManager.PlayerSaveData.GetTeleporterIsUnlocked(biome)) return 0;

        SaveManager.PlayerSaveData.SetTeleporterIsUnlocked(biome, state: true);
        Plugin.Log.LogDebug($"[APSync] Restored teleporter unlock: {DisplayName(itemId)}");
        return 1;
    }

    /// <summary>
    /// Applies the free bundle the randomized starting class grants on its manor slot. Idempotent and
    /// deliberately overlapping with <see cref="ApplyManorUpgrade"/>: when that slot does appear in
    /// the ledger both compute the same absolute target, so whichever runs second is a no-op.
    /// </summary>
    private static int ApplyStartingClassSlot(APRunState runState, SkillTreeType? slot)
    {
        if (!slot.HasValue) return 0;

        var skillObj = SkillTreeManager.GetSkillTreeObj(slot.Value);
        if (skillObj == null) return 0;

        int received = 0;
        var itemId = ItemRegistry.FromSkillTreeType(slot.Value);
        if (itemId.HasValue) received = runState.ReceivedCount(itemId.Value);

        int target = Math.Min((received + 1) * APClient.ManorUpgradeBundleSize, skillObj.MaxLevel);
        if (SkillTreeManager.GetSkillObjLevel(slot.Value) >= target) return 0;

        SkillTreeManager.SetSkillObjLevel(slot.Value, target, additive: false, runEvents: false);
        Plugin.Log.LogDebug($"[APSync] Restored starting class slot '{slot.Value}' (level {target})");
        return 1;
    }

    /// <summary>
    /// Makes <see cref="TrapManager"/>'s in-memory flags match the traps persisted for this life.
    /// <c>ActivateTrap</c> is already idempotent and designed to be replayed, so this doubles as the
    /// once-per-session restore.
    ///
    /// <para>Not counted as a save correction: trap state is static flags plus the already-persisted
    /// <see cref="APRunState.ActiveTraps"/>, so nothing in the game save changed and there is nothing
    /// to flush. It does fire on every reconnect, because tearing a session down clears the flags.</para>
    /// </summary>
    private static void SyncActiveTraps(APRunState runState)
    {
        foreach (var trap in runState.ActiveTraps)
        {
            try
            {
                if (TrapManager.IsTrapActive(trap)) continue;
                TrapManager.ActivateTrap(trap);
                Plugin.Log.LogDebug($"[APSync] Re-armed active trap for this life: {trap}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[APSync] Could not restore trap '{trap}': {ex.Message}");
            }
        }
    }

    private static SkillTreeType? ParseStartingClassSlot(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return Enum.TryParse(name, out SkillTreeType parsed) ? parsed : null;
    }

    private static string DisplayName(long itemId) =>
        ItemRegistry.Names.TryGetValue(itemId, out var name) ? name : itemId.ToString();
}
