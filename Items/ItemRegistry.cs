using System.Collections.Generic;

namespace RL2Archipelago.Items;

/// <summary>
/// Central registry of Archipelago item IDs and their display names.
///
/// IDs MUST stay in lockstep with the Python .apworld
/// (<c>apworld/rogue_legacy_2/items.py</c>). Items and locations occupy
/// separate ID namespaces in Archipelago, so the same numeric offset can
/// safely appear in both registries.
/// </summary>
public static class ItemRegistry
{
    public const long BASE_ID = 0xBEEF0000L;

    private const long HEIRLOOM_OFFSET = 0x300;

    // ── Heirloom items ───────────────────────────────────────────────────────

    public const long HeirloomAirDash             = BASE_ID + HEIRLOOM_OFFSET + 0;
    public const long HeirloomDoubleJump          = BASE_ID + HEIRLOOM_OFFSET + 1;
    public const long HeirloomMemory              = BASE_ID + HEIRLOOM_OFFSET + 2;
    public const long HeirloomBouncableDownstrike = BASE_ID + HEIRLOOM_OFFSET + 3;
    public const long HeirloomVoidDash            = BASE_ID + HEIRLOOM_OFFSET + 4;
    public const long HeirloomCaveLantern         = BASE_ID + HEIRLOOM_OFFSET + 5;

    /// <summary>Human-readable name for each item ID, used in logs and UI.</summary>
    public static readonly IReadOnlyDictionary<long, string> Names = new Dictionary<long, string>
    {
        [HeirloomAirDash]             = "Ananke's Shawl",
        [HeirloomDoubleJump]          = "Aether's Wings",
        [HeirloomMemory]              = "Aesop's Tome",
        [HeirloomBouncableDownstrike] = "Echo's Boots",
        [HeirloomVoidDash]            = "Pallas' Void Bell",
        [HeirloomCaveLantern]         = "Theia's Sun Lantern",
    };

    /// <summary>
    /// Maps an Archipelago item ID to its <see cref="HeirloomType"/>.
    /// Returns <c>null</c> if the item isn't a tracked heirloom.
    /// </summary>
    public static HeirloomType? ToHeirloomType(long itemId) => itemId switch
    {
        HeirloomAirDash             => HeirloomType.UnlockAirDash,
        HeirloomDoubleJump          => HeirloomType.UnlockDoubleJump,
        HeirloomMemory              => HeirloomType.UnlockMemory,
        HeirloomBouncableDownstrike => HeirloomType.UnlockBouncableDownstrike,
        HeirloomVoidDash            => HeirloomType.UnlockVoidDash,
        HeirloomCaveLantern         => HeirloomType.CaveLantern,
        _ => null,
    };
}
