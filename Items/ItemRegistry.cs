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

    private const long HEIRLOOM_OFFSET  = 0x300;
    private const long BLUEPRINT_OFFSET = 0x400;
    private const long RUNE_OFFSET      = 0x500;

    // ── Heirloom items ───────────────────────────────────────────────────────

    public const long HeirloomAirDash             = BASE_ID + HEIRLOOM_OFFSET + 0;
    public const long HeirloomDoubleJump          = BASE_ID + HEIRLOOM_OFFSET + 1;
    public const long HeirloomMemory              = BASE_ID + HEIRLOOM_OFFSET + 2;
    public const long HeirloomBouncableDownstrike = BASE_ID + HEIRLOOM_OFFSET + 3;
    public const long HeirloomVoidDash            = BASE_ID + HEIRLOOM_OFFSET + 4;
    public const long HeirloomCaveLantern         = BASE_ID + HEIRLOOM_OFFSET + 5;

    /// <summary>Human-readable name for each item ID, used in logs and UI.</summary>
    public static readonly IReadOnlyDictionary<long, string> Names = BuildNames();

    private static Dictionary<long, string> BuildNames()
    {
        Dictionary<long, string> d = new()
        {
            [HeirloomAirDash]             = "Ananke's Shawl",
            [HeirloomDoubleJump]          = "Aether's Wings",
            [HeirloomMemory]              = "Aesop's Tome",
            [HeirloomBouncableDownstrike] = "Echo's Boots",
            [HeirloomVoidDash]            = "Pallas' Void Bell",
            [HeirloomCaveLantern]         = "Theia's Sun Lantern",
        };

        // Rune item names
        string[] runeNames =
        [
            "Reinforced",   "Dash",        "Vault",    "Bounty",
            "Haste",        "Lifesteal",   "Magnesis", "Retaliation",
            "Siphon",       "Capacity",    "Trick",    "Amplification",
            "Soulsteal",    "Resolve",     "Stone",    "Red",
            "Sharpened",    "Focal",       "Might",    "Eldar",
            "Lucky Roller", "High Stakes", "Folded",   "Quenching",
        ];
        for (int i = 0; i < runeNames.Length; i++)
            d[BASE_ID + RUNE_OFFSET + i] = $"{runeNames[i]} Rune";

        // Blueprint item names
        string[] categoryNames = [ "Weapon", "Helm", "Chest", "Cape", "Trinket" ];
        string[] typeNames =
        [
            "Leather",    "Scholar",   "Warden",  "Sanguine",
            "Ammonite",   "Crescent",  "Drowned", "Gilded",
            "Obsidian",   "Leviathan", "Kin",
            "White Wood", "Black Root", 
        ];
        for (int c = 0; c < 5; c++)
            for (int t = 0; t < 13; t++)
                d[BASE_ID + BLUEPRINT_OFFSET + c * 16 + t] = $"{typeNames[t]} {categoryNames[c]} Blueprint";

        return d;
    }

    // ── Lookup methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Maps an Archipelago item ID to its <see cref="RuneType"/>.
    /// Returns <c>null</c> if the item isn't a tracked rune.
    /// </summary>
    public static RuneType? ToRuneType(long itemId)
    {
        long offset = itemId - BASE_ID - RUNE_OFFSET;
        if (offset < 0 || offset > 23) return null;
        return (int)offset switch
        {
            0  => RuneType.ArmorRegen,
            1  => RuneType.Dash,
            2  => RuneType.DoubleJump,
            3  => RuneType.GoldGain,
            4  => RuneType.Haste,
            5  => RuneType.Lifesteal,
            6  => RuneType.Magnet,
            7  => RuneType.ReturnDamage,
            8  => RuneType.ManaRegen,
            9  => RuneType.MaxMana,
            10 => RuneType.ManaOnSpinKick,
            11 => RuneType.StatusEffectDuration,
            12 => RuneType.SoulSteal,
            13 => RuneType.ResolveGain,
            14 => RuneType.OreGain,
            15 => RuneType.RuneOreGain,
            16 => RuneType.WeaponCritChanceAdd,
            17 => RuneType.MagicCritChanceAdd,
            18 => RuneType.WeaponCritDamageAdd,
            19 => RuneType.MagicCritDamageAdd,
            20 => RuneType.SuperCritChanceAdd,
            21 => RuneType.SuperCritDamageAdd,
            22 => RuneType.ArmorMinBlock,
            23 => RuneType.ArmorHealth,
            _  => null,
        };
    }

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

    /// <summary>
    /// Maps an Archipelago item ID to its <see cref="EquipmentCategoryType"/> and
    /// <see cref="EquipmentType"/> pair. Returns <c>null</c> if the item isn't a
    /// tracked equipment blueprint.
    /// </summary>
    public static (EquipmentCategoryType Category, EquipmentType EquipType)? ToEquipmentBlueprint(long itemId)
    {
        long offset = itemId - BASE_ID - BLUEPRINT_OFFSET;
        if (offset < 0) return null;

        int categoryIndex = (int)(offset / 16);
        int typeIndex     = (int)(offset % 16);
        if (categoryIndex > 4 || typeIndex > 12) return null;

        EquipmentCategoryType category = categoryIndex switch
        {
            0 => EquipmentCategoryType.Weapon,
            1 => EquipmentCategoryType.Head,
            2 => EquipmentCategoryType.Chest,
            3 => EquipmentCategoryType.Cape,
            4 => EquipmentCategoryType.Trinket,
            _ => EquipmentCategoryType.None,
        };
        EquipmentType equipType = typeIndex switch
        {
            0  => EquipmentType.GEAR_BONUS_WEIGHT,
            1  => EquipmentType.GEAR_MAGIC_CRIT,
            2  => EquipmentType.GEAR_STRENGTH_CRIT,
            3  => EquipmentType.GEAR_LIFE_STEAL,
            4  => EquipmentType.GEAR_ARMOR,
            5  => EquipmentType.GEAR_MAGIC_DMG,
            6  => EquipmentType.GEAR_MOBILITY,
            7  => EquipmentType.GEAR_GOLD,
            8  => EquipmentType.GEAR_RETURN_DMG,
            9  => EquipmentType.GEAR_MAG_ON_HIT,
            10 => EquipmentType.GEAR_LIFE_STEAL_2,
            11 => EquipmentType.GEAR_EMPTY_1,
            12 => EquipmentType.GEAR_EMPTY_2,
            _  => EquipmentType.None,
        };
        if (category == EquipmentCategoryType.None || equipType == EquipmentType.None) return null;

        return (category, equipType);
    }
}
