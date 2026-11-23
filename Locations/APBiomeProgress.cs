namespace RL2Archipelago.Locations;

/// <summary>
/// One tracked category's standing in a biome. Plain numbers, so the overlay does no arithmetic
/// while drawing.
///
/// <para><c>Collected &lt;= InLogic &lt;= Total</c> always holds, and all three only ever move up:
/// <see cref="Total"/> is the configured slot count and does not move at all.</para>
/// </summary>
internal readonly struct APCategoryProgress
{
    /// <summary>
    /// Slots in this category that are checked off, or entries read in grouped journal/memory
    /// mode.
    ///
    /// <para>A pooled slot the <em>server</em> already had checked (released by another player, a
    /// <c>/collect</c>, an admin command) counts here rather than coming off
    /// <see cref="Total"/>. The slot is settled either way and the player has nothing left to do
    /// for it, so counting it as progress keeps the figures monotone; taking it off the total
    /// instead would make the panel appear to lose ground when another player helped.</para>
    /// </summary>
    public readonly int Collected;

    /// <summary>
    /// How many are reachable in logic, counting <see cref="Collected"/>. Only the chest pools can
    /// land strictly between the other two figures; for journals and memories logic is a whole
    /// category boolean, so this is either <see cref="Collected"/> or <see cref="Total"/>, which is
    /// why their rows show two numbers rather than three.
    /// </summary>
    public readonly int InLogic;

    /// <summary>
    /// Every slot this category has in this biome. Zero means the category does not apply here.
    /// </summary>
    public readonly int Total;

    /// <summary>True when nothing in this category is in logic yet; the row renders dimmed.</summary>
    public readonly bool Blocked;

    /// <summary>True when nothing in this category is left to do.</summary>
    public readonly bool Complete;

    /// <summary>
    /// Whether this category gets a row. Categories that do not apply to this biome are omitted,
    /// and so are finished ones. The panel lists what is left to do here, and empties out
    /// entirely once the biome is done with.
    /// </summary>
    public bool Present => Total > 0 && !Complete;

    public APCategoryProgress(int collected, int inLogic, int total, bool blocked, bool complete)
        => (Collected, InLogic, Total, Blocked, Complete)
         = (collected, inLogic, total, blocked, complete);
}

/// <summary>
/// A snapshot of the four biome-scoped categories for one biome, taken fresh on each recount so
/// every row the overlay draws in a frame is answered from one consistent state. That matters
/// because <c>APClient.AbsorbRemoteChecks</c> runs on the same main-thread tick and can change the
/// totals underneath a half-finished read.
///
/// <para>All reads are main-thread only: <see cref="APRunState.CheckedLocations"/> is mutated and
/// read on the main thread, exactly as the chest patches already do, so no locking is needed
/// here.</para>
/// </summary>
internal readonly struct APBiomeProgress
{
    public readonly APCategoryProgress Blueprints;
    public readonly APCategoryProgress FairyChests;
    public readonly APCategoryProgress Journals;
    public readonly APCategoryProgress Memories;

    /// <summary>
    /// True when no category has a row, so the panel draws nothing at all (i.e. the player
    /// has finished every check in the biome or we're not in a biome currently).
    /// </summary>
    public bool IsEmpty =>
        !Blueprints.Present && !FairyChests.Present && !Journals.Present && !Memories.Present;

    private APBiomeProgress(
        APCategoryProgress blueprints, APCategoryProgress fairyChests,
        APCategoryProgress journals, APCategoryProgress memories)
        => (Blueprints, FairyChests, Journals, Memories)
         = (blueprints, fairyChests, journals, memories);

    /// <summary>Counts everything the tracker shows for one biome.</summary>
    public static APBiomeProgress For(int biomeIndex, APRunState runState)
    {
        bool biomeInLogic    = APLogicSnapshot.IsBiomeInLogic(biomeIndex, runState);
        bool memoriesInLogic = biomeInLogic && APLogicSnapshot.HasAesopsTome(runState);

        int blueprintSlots = LocationRegistry.BlueprintChecksPerBiome;
        int runeSlots      = LocationRegistry.RuneChecksPerBiome;

        var blueprints = CountChestPool(
            biomeIndex, blueprintSlots, biomeInLogic,
            APLogicSnapshot.InLogicChestSlots(biomeIndex, blueprintSlots, runState),
            LocationRegistry.BlueprintChestLocation, runState);

        var fairyChests = CountChestPool(
            biomeIndex, runeSlots, biomeInLogic,
            APLogicSnapshot.InLogicChestSlots(biomeIndex, runeSlots, runState),
            LocationRegistry.RuneChestLocation, runState);

        APCategoryProgress journals, memories;
        switch (APClient.JournalChecksMode)
        {
            case JournalChecksMode.Individual:
                journals = CountJournalPool(
                    biomeIndex, LocationRegistry.JournalCount(biomeIndex), biomeInLogic,
                    LocationRegistry.IndividualJournalLocation, runState);
                memories = CountJournalPool(
                    biomeIndex, LocationRegistry.MemoryCount(biomeIndex), memoriesInLogic,
                    LocationRegistry.IndividualMemoryLocation, runState);
                break;

            case JournalChecksMode.Grouped:
                journals = CountGrouped(
                    biomeIndex, JournalType.Journal, LocationRegistry.JournalCount(biomeIndex),
                    biomeInLogic, LocationRegistry.GroupedJournalLocation(biomeIndex), runState);
                memories = CountGrouped(
                    biomeIndex, JournalType.MemoryFragment, LocationRegistry.MemoryCount(biomeIndex),
                    memoriesInLogic, LocationRegistry.GroupedMemoryLocation(biomeIndex), runState);
                break;

            default: // Disabled - no journal or memory checks exist, so neither row applies.
                journals = default;
                memories = default;
                break;
        }

        return new APBiomeProgress(blueprints, fairyChests, journals, memories);
    }

    /// <summary>
    /// Counts a chest pool by walking its slots and classifying each as settled or still free.
    /// <see cref="APRunState.IsPooledSlotConsumed"/> is the settled test, so a slot the server
    /// already had counts as collected (see <see cref="APCategoryProgress.Collected"/>).
    ///
    /// <para>Classifying per slot index rather than deriving the figures from a count is needed.
    /// The apworld gates the <em>highest</em> slots on the biome boss while
    /// <see cref="LocationRegistry.NextBlueprintChestLocation"/> hands out the <em>lowest</em> live
    /// one, which is self-consistent for a lone player. But a server-consumed low slot pushes the
    /// next pickup up into possibly-gated territory, and only per-slot classification gets that
    /// right.</para>
    /// </summary>
    private static APCategoryProgress CountChestPool(
        int biomeIndex, int slotCount, bool biomeInLogic, int inLogicSlots,
        System.Func<int, int, long> locationFor, APRunState runState)
    {
        int collected = 0, inLogic = 0, free = 0;

        for (int slot = 0; slot < slotCount; slot++)
        {
            long id = locationFor(biomeIndex, slot);

            if (runState.IsPooledSlotConsumed(id)) { collected++; continue; }

            free++;
            if (biomeInLogic && slot < inLogicSlots) inLogic++;
        }

        return new APCategoryProgress(
            collected, collected + inLogic, slotCount,   // collected + free == slotCount
            // Dimming means "you cannot get into this biome yet", not "you have taken everything
            // currently available here," that second state is what the middle figure already
            // says, and dimming a row the player has finished with would read as inaccessible.
            blocked: !biomeInLogic && free > 0,
            complete: slotCount > 0 && free == 0);
    }

    /// <summary>
    /// Counts an individual-mode journal or memory pool. Same pooled classification as the chest
    /// pools, but with no per-slot rule: the whole category is in logic or none of it is.
    /// </summary>
    private static APCategoryProgress CountJournalPool(
        int biomeIndex, int slotCount, bool inLogic,
        System.Func<int, int, long> locationFor, APRunState runState)
    {
        int collected = 0;

        for (int slot = 0; slot < slotCount; slot++)
            if (runState.IsPooledSlotConsumed(locationFor(biomeIndex, slot)))
                collected++;

        return new APCategoryProgress(
            collected, inLogic ? slotCount : collected, slotCount,
            blocked: !inLogic && collected < slotCount,
            complete: slotCount > 0 && collected == slotCount);
    }

    /// <summary>
    /// Counts a grouped-mode journal or memory category. There is one check per biome, earned by
    /// reading everything in it, so the figures shown are the progress towards completion.
    /// </summary>
    private static APCategoryProgress CountGrouped(
        int biomeIndex, JournalType journalType, int total, bool inLogic,
        long? groupLocation, APRunState runState)
    {
        if (total == 0 || groupLocation is null) return default;

        int read = ReadJournalCount(biomeIndex, journalType);
        bool complete = runState.CheckedLocations.Contains(groupLocation.Value);

        return new APCategoryProgress(
            read, inLogic ? total : read, total,
            blocked: !inLogic && !complete,
            complete: complete);
    }

    /// <summary>
    /// How many journals or memories of a biome the save records as read, or 0 when the save is not
    /// available to ask.
    ///
    /// <para><c>PlayerSaveData.GetJournalsRead</c> throws <c>KeyNotFoundException</c> for a category
    /// its table has no row for, rather than returning zero, so the lookup is guarded: this runs
    /// every frame, and an exception here would bury the BepInEx log a player attaches to a bug
    /// report. The category comes from <see cref="LocationRegistry.ToJournalCategory"/> rather than
    /// the game's own <c>JournalType_RL.ConvertBiomeToJournalCategoryType</c>, which returns
    /// <c>None</c> for <c>CaveMiddle</c> and <c>CaveBottom</c> and would make the row vanish in
    /// most of Pishon Dry Lake.</para>
    /// </summary>
    private static int ReadJournalCount(int biomeIndex, JournalType journalType)
    {
        var saveData = SaveManager.PlayerSaveData;
        if (saveData?.JournalsReadTable == null) return 0;

        var category = LocationRegistry.ToJournalCategory(biomeIndex);
        return saveData.JournalsReadTable.ContainsKey(category)
            ? saveData.GetJournalsRead(category, journalType)
            : 0;
    }
}
