namespace RL2Archipelago;

public static class APSettings
{
    /// <summary>Null means no user preference - defer to the server value on connect.</summary>
    public static bool? DeathLink { get; set; } = null;
/// <summary>When true, notifications are shown in the compact top-right text log
    /// instead of the full-screen overlay HUD.</summary>
    public static bool UseMinimalistAPLog { get; set; } = false;
}
