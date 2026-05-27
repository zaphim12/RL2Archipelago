namespace RL2Archipelago;

public static class APSettings
{
    /// <summary>Null means no user preference — defer to the server value on connect.</summary>
    public static bool? DeathLink { get; set; } = null;
    public static bool ShowItemNames { get; set; } = true;
}
