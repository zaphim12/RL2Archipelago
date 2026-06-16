using System;

namespace RL2Archipelago.UI;

// A concrete SelectionListOptionItem for a simple ON/OFF boolean toggle.
// Getter/Setter/SettingName are assigned after AddComponent so one class
// covers every AP boolean setting without further subclassing.
public class APBoolOptionItem : SelectionListOptionItem
{
    private static readonly string[] OnOffLocIDs =
        { "LOC_ID_GENERAL_UI_ON_1", "LOC_ID_GENERAL_UI_OFF_1" };

    public string      SettingName { get; set; }
    public string      Description { get; set; }
    public Func<bool>  Getter      { get; set; }
    public Action<bool> Setter     { get; set; }

    // Exposed so the patch can read it without Traverse.
    internal TMPro.TMP_Text TitleText => m_titleText;

    public override void Initialize()
    {
        m_selectionLocIDArray = OnOffLocIDs;
        m_selectedIndex = (Getter != null && Getter()) ? 0 : 1; // 0 = ON, 1 = OFF
        base.Initialize(); // sets m_incrementValueText to current selection string
        if (m_titleText != null) m_titleText.text = SettingName;
    }

    // Refresh index from APSettings every time the panel re-opens so the
    // display stays in sync even if the value was changed programmatically.
    protected override void OnEnable()
    {
        base.OnEnable();
        if (IsInitialized)
        {
            m_selectedIndex = (Getter != null && Getter()) ? 0 : 1;
            m_incrementValueText.SetText(CurrentSelectionString);
        }
    }

    public override void InvokeValueChange() { }

    public override void ConfirmOptionChange()
    {
        bool value = m_selectedIndex == 0;
        Setter?.Invoke(value);
        Plugin.Log.LogDebug($"[APSettings] {SettingName} = {value}");
    }
}
