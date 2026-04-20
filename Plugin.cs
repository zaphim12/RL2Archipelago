using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;

namespace RL2Archipelago;

[BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance { get; private set; }

    // Static accessor so every file can write Plugin.Log.LogInfo(...) without grabbing Instance.
    public static ManualLogSource Log { get; private set; }

    public static APConnectionData ConnectionData { get; private set; }

    private static readonly string ConnectionDataFilePath = Path.Combine(
        Paths.ConfigPath, "RL2Archipelago", "connection-data.json");

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        LoadConnectionData();

        Log.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} loaded.");
    }

    // ── Connection data ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads AP connection data from disk, if it exists.  This is used to pre-populate
    /// the connection UI with the last-connected server and slot. 
    /// </summary>
    public static void LoadConnectionData()
    {
        try
        {
            if (File.Exists(ConnectionDataFilePath))
            {
                var json = File.ReadAllText(ConnectionDataFilePath);
                ConnectionData = JsonConvert.DeserializeObject<APConnectionData>(json);
                Log.LogInfo("Connection data loaded.");
            }
            else
            {
                Log.LogDebug("No existing AP connection data found.");
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to load connection data: {ex.Message}");
        }
    }

    public static void WriteConnectionData()
    {
        if (ConnectionData == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConnectionDataFilePath));
            var json = JsonConvert.SerializeObject(ConnectionData, Formatting.Indented);
            File.WriteAllText(ConnectionDataFilePath, json);
            Log.LogDebug("Connection data written.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to write connection data: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a fresh APConnectionData and persists it to disk.
    /// </summary>
    public static void InitNewConnectionData(APConnectionData connData)
    {
        ConnectionData = connData;
        WriteConnectionData();
    }
}
