using Archipelago.MultiClient.Net.Enums;

namespace RL2Archipelago.Items;

/// <summary>
/// The item identity shown to the player for a scouted location.
///
/// <para>
/// Every display surface reads this rather than the raw <c>ScoutedItemInfo</c> so that a
/// single substitution point (<see cref="APClient.GetItemView"/>) governs what the player
/// sees. For an ordinary location the fields mirror the scout reply verbatim; for a
/// concealed trap they carry a disguise, and the rendering code cannot tell the difference
/// because there is no separate code path for it to take.
/// </para>
/// </summary>
internal sealed class APItemView
{
    /// <summary>
    /// The item ID to resolve graphics from. For a disguised trap this is the <em>disguise's</em>
    /// ID, so <see cref="APSprites.GetSpriteForLocation"/> resolves the disguise's icon: a
    /// foreign disguise fails every <see cref="ItemRegistry"/> lookup and falls through to the
    /// generic AP logo, exactly as a genuine foreign item does.
    /// </summary>
    public long ItemId;

    /// <summary>Name rendered in titles, descriptions and dialogue.</summary>
    public string DisplayName;

    /// <summary>Slot of the player this item belongs to.</summary>
    public int PlayerSlot;

    /// <summary>
    /// Display name of the owning player, or <c>null</c> when the item belongs to us.
    /// Callers branch on null to decide whether to append a "for {player}" suffix.
    /// </summary>
    public string PlayerName;

    /// <summary>
    /// Significance flags for the item as shown. For a disguised trap these are the
    /// <em>disguise's</em> flags, so <see cref="FlavorText"/> describes what the player appears
    /// to be looking at. 
    /// </summary>
    public ItemFlags Flags;

    /// <summary>
    /// A hint at how much the item matters to its owner, appended to descriptions. Empty for
    /// filler, and empty for our own items: a player knows the value of their own items, so the
    /// hint is only worth showing for another world's items.
    ///
    /// <para>
    /// This still cannot expose a concealed trap. It keys off the view, so a disguised trap
    /// reports its disguise's flags and never <see cref="ItemFlags.Trap"/>, and whether the line
    /// appears at all tracks the owner. which <see cref="PlayerName"/> already reveals.
    /// The trap line therefore only ever appears with visible traps opted in.
    /// </para>
    /// </summary>
    public string FlavorText
    {
        get
        {
            if (PlayerName == null) return string.Empty;

            // Priority order, most significant first. Deliberately not a series of independent
            // assignments: an item can be flagged both Advancement and NeverExclude, and the
            // stronger signal should win rather than whichever check happens to run last.
            if ((Flags & ItemFlags.Trap) != 0)
                return "\nIt looks like something they need?";

            if ((Flags & ItemFlags.Advancement) != 0)
                return "\nIt looks like something they need";

            if ((Flags & ItemFlags.NeverExclude) != 0)
                return "\nIt looks like something that could be of use";

            return string.Empty;
        }
    }
}
