using RL2Archipelago.Locations;
using RL2Archipelago.UI;

namespace RL2Archipelago.Patches;

/// <summary>
/// What one manor slot on screen currently represents, resolved for whichever manor layout the
/// seed uses. Every display and purchase patch in <see cref="ManorUpgradePatch"/> goes through
/// this so the layout branch lives in exactly one place.
///
/// <para>Under the tree layout a slot is one location for the whole run, and
/// <see cref="IsComplete"/> simply means it has been bought. Under the shop layout a slot is a
/// column: it represents whichever purchase is next in that column, so
/// <see cref="Location"/> advances as the player buys, and the column eventually runs out.</para>
/// </summary>
internal readonly struct ManorSlotView
{
    /// <summary>Whether this slot is an Archipelago manor slot at all. False for an untracked
    /// node, and false for every non-host node under the shop layout.</summary>
    public readonly bool IsTracked;

    /// <summary>The location currently on offer here, or <c>null</c> when there is nothing left
    /// to buy (a bought tree node, or an exhausted shop column).</summary>
    public readonly long? Location;

    /// <summary>The location whose item this slot should show. Same as <see cref="Location"/>
    /// while something is on offer; once nothing is, the last purchase made here, so an
    /// exhausted column keeps showing what it last sold rather than falling back to the
    /// vanilla identity of whichever unimplemented slot happens to host it.</summary>
    public readonly long? DisplayLocation;

    /// <summary>Nothing further can be bought here.</summary>
    public readonly bool IsComplete;

    /// <summary>Purchases made at this slot. 0 or 1 under the tree layout; 0..<see cref="Total"/>
    /// under the shop layout.</summary>
    public readonly int Purchased;

    /// <summary>Purchases this slot will ever offer. 1 under the tree layout, the configured
    /// column depth under the shop layout.</summary>
    public readonly int Total;

    /// <summary>Gold cost of <see cref="Location"/>, or 0 when there is none.</summary>
    public readonly int Cost;

    private ManorSlotView(bool isTracked, long? location, long? displayLocation, bool isComplete,
                          int purchased, int total, int cost)
    {
        IsTracked = isTracked;
        Location = location;
        DisplayLocation = displayLocation;
        IsComplete = isComplete;
        Purchased = purchased;
        Total = total;
        Cost = cost;
    }

    /// <summary>An untracked slot: the base game owns it entirely.</summary>
    public static readonly ManorSlotView None = new(false, null, null, false, 0, 0, 0);

    /// <summary>Resolves the current state of <paramref name="type"/>'s slot.</summary>
    public static ManorSlotView For(SkillTreeType type)
    {
        var checkedLocations = APClient.RunState?.CheckedLocations;
        if (checkedLocations == null) return None;

        if (APClient.IsManorShopLayout)
        {
            var position = ManorShopLayout.PositionOf(type);
            if (position == null) return None;

            int depth = LocationRegistry.ManorShopDepth;
            int purchased = LocationRegistry.ManorShopPurchasedCount(position.Value, checkedLocations);
            long? location = LocationRegistry.NextManorShopLocation(position.Value, checkedLocations);
            return new ManorSlotView(
                isTracked: true,
                location: location,
                displayLocation: location
                    ?? (purchased > 0
                        ? LocationRegistry.ManorShopLocation(position.Value, purchased - 1)
                        : null),
                isComplete: location == null,
                purchased: purchased,
                total: depth,
                cost: location == null ? 0 : ShopCost(location.Value));
        }

        var treeLocation = LocationRegistry.FromSkillTreeType(type);
        if (treeLocation == null) return None;

        bool bought = checkedLocations.Contains(treeLocation.Value);
        return new ManorSlotView(
            isTracked: true,
            // A tree node is its own location for the whole run: it stays set once bought, and
            // IsComplete is what tells the purchase path to refuse.
            location: treeLocation,
            displayLocation: treeLocation,
            isComplete: bought,
            purchased: bought ? 1 : 0,
            total: 1,
            cost: bought ? 0 : TreeCost(type));
    }

    // Both tables are built from slot data on connect and cover every location of their
    // layout, so a miss is a bug rather than a case to degrade around. Reporting 0 makes it
    // visible as a free purchase instead of silently charging a plausible-looking price.
    private static int ShopCost(long location) =>
        APClient.ManorShopCosts.TryGetValue(location, out var cost) ? cost : 0;

    private static int TreeCost(SkillTreeType type) =>
        APClient.ManorUpgradeCosts.TryGetValue(type, out var cost) ? cost : 0;
}
