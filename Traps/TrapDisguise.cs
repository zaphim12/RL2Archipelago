using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using RL2Archipelago.Items;
using System;
using System.Collections.Generic;
using System.Text;

namespace RL2Archipelago.Traps;

/// <summary>
/// Conceals traps behind the identity of a real item so the player cannot spot them before
/// obtaining. Without this, a trap in the manor may render as "[AP] Trap: Void Waves" and simply
/// never be purchased, which makes nullifies the point of a trap.
///
/// <para>
/// The disguise is not fabricated text. It is a genuine identity lifted from another scouted
/// location in this same seed, so the render path treats it exactly like any other entry. That
/// also means the disguises reproduce the seed's real composition for free: drawing uniformly
/// from the scouted set yields foreign-looking disguises in the same proportion that foreign
/// items actually occupy the tracked locations, so a trap cannot stand out statistically.
/// </para>
/// </summary>
internal static class TrapDisguise
{
    // Immutable once published. Built on the scout continuation (thread pool) and read on the
    // Unity main thread, so it is swapped in as a whole array rather than mutated in place.
    private static volatile APItemView[] _pool = Array.Empty<APItemView>();

    // Server-provided seed string; combined with the location ID to pick a disguise. Stored
    // separately from the pool because it also has to survive a pool rebuild unchanged.
    private static volatile string _seed = string.Empty;

    /// <summary>
    /// Rebuilds the disguise pool from the scout reply. Safe to call again after a reconnect:
    /// selection is a pure function of seed and location ID, so an identical pool yields
    /// identical disguises and the player sees no change.
    /// </summary>
    internal static void BuildPool(
        IEnumerable<KeyValuePair<long, ScoutedItemInfo>> scouted,
        string seed,
        int ourSlot,
        string ourGame)
    {
        _seed = seed ?? string.Empty;

        // Sort by source location ID: the scout cache is a ConcurrentDictionary whose
        // enumeration order is not guaranteed, and an unsorted pool would shuffle every
        // disguise between sessions.
        var candidates = new List<KeyValuePair<long, ScoutedItemInfo>>(scouted);
        candidates.Sort((a, b) => a.Key.CompareTo(b.Key));

        var pool = new List<APItemView>(candidates.Count);
        foreach (var kv in candidates)
        {
            if (IsTrap(kv.Value, ourGame)) continue;
            pool.Add(BuildView(kv.Value, ourSlot));
        }

        _pool = pool.ToArray();
        Plugin.Log.LogInfo($"[AP] Trap disguise pool built: {_pool.Length} candidate identities.");
    }

    internal static void Clear()
    {
        _pool = Array.Empty<APItemView>();
        _seed = string.Empty;
    }

    /// <summary>
    /// Returns what the player should see for the item at <paramref name="locationId"/>:
    /// the true identity, or a disguise when it is a concealed trap.
    /// </summary>
    internal static APItemView ViewFor(long locationId, ScoutedItemInfo scouted, int ourSlot, string ourGame)
    {
        var trueView = BuildView(scouted, ourSlot);

        if (APClient.TrapAppearanceVisible) return trueView;
        if (!IsTrap(scouted, ourGame)) return trueView;

        // Once purchased the trap has already fired and the "Item Found"/"Item Sent" toast has
        // named it, so holding the disguise would leave the slot contradicting what the player
        // just read. Reveal the truth instead.
        if (APClient.RunState?.CheckedLocations?.Contains(locationId) ?? false)
            return trueView;

        var pool = _pool;
        if (pool.Length == 0) return trueView; // nothing to hide behind; fail open rather than crash

        return pool[(int)(Hash(_seed, locationId) % (ulong)pool.Length)];
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static APItemView BuildView(ScoutedItemInfo scouted, int ourSlot)
    {
        var name = !string.IsNullOrEmpty(scouted.ItemDisplayName)
            ? scouted.ItemDisplayName
            : (scouted.ItemName ?? "Unknown Item");

        return new APItemView
        {
            ItemId      = scouted.ItemId,
            DisplayName = name,
            PlayerSlot  = scouted.Player.Slot,
            PlayerName  = scouted.Player.Slot == ourSlot ? null : ResolvePlayerName(scouted.Player),
            Flags       = scouted.Flags,
        };
    }

    private static string ResolvePlayerName(PlayerInfo player) =>
        !string.IsNullOrEmpty(player.Alias) ? player.Alias : (player.Name ?? $"Player {player.Slot}");

    /// <summary>
    /// The server's trap flag is authoritative and covers every world's traps. The
    /// <see cref="ItemRegistry"/> check is a secondary guard for our own trap IDs, gated on the
    /// item's game because item IDs are only meaningful within the world that owns them.
    /// </summary>
    private static bool IsTrap(ScoutedItemInfo scouted, string ourGame)
    {
        if ((scouted.Flags & ItemFlags.Trap) != 0) return true;

        return !string.IsNullOrEmpty(ourGame)
            && scouted.ItemGame == ourGame
            && ItemRegistry.ToTrapBurdenType(scouted.ItemId).HasValue;
    }

    /// <summary>
    /// FNV-1a over the seed and location ID.
    /// <para>
    /// Deliberately not <see cref="string.GetHashCode"/>: .NET randomizes string hashing per
    /// process, which would re-roll every disguise on each game restart and make the concealment
    /// obvious. This has to stay stable for the life of the multiworld.
    /// </para>
    /// </summary>
    private static ulong Hash(string seed, long locationId)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime       = 1099511628211UL;

        ulong hash = offsetBasis;

        foreach (var b in Encoding.UTF8.GetBytes(seed ?? string.Empty))
        {
            hash ^= b;
            hash *= prime;
        }

        ulong id = unchecked((ulong)locationId);
        for (int i = 0; i < 8; i++)
        {
            hash ^= (byte)(id >> (i * 8));
            hash *= prime;
        }

        return hash;
    }
}
