namespace RL2Archipelago;

public class APConnectionData
{
    public string Hostname { get; set; } = "archipelago.gg";
    public int Port { get; set; } = 38281;
    public string SlotName { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>
    /// The AP server's room seed, used to differentiate save data between different multiworld sessions.  Populated on successful connection.
    /// </summary>
    public string RoomId { get; set; } = null;

    public APConnectionData Clone() => new()
    {
        Hostname = Hostname,
        Port = Port,
        SlotName = SlotName,
        Password = Password,
        RoomId = RoomId,
    };
}

public class APSaveData
{
    public APConnectionData apConnectionData { get; set; } = new();

    // public Dictionary<Location, bool> LocationsChecked { get; set; } = new();
    // public Dictionary<Item, uint> ItemsAcquired { get; set; } = new();
    // public Dictionary<string, string[]> HintsGenerated { get; set; } = new();
}
