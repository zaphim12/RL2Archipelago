using System;
using UnityEngine;

namespace RL2Archipelago.UI;

/// <summary>
/// A modal IMGUI dialog for the player to provide connection info which then fires a callback with the result.
///
/// IMGUI is chosen deliberately for the barebones scaffold: it requires no Unity
/// scene assets, no font references, and no prefab duplication — it Just Works on
/// top of any Unity game.  We can replace it with a proper TMP overlay later.
///
/// Usage:
///   ConnectionDialog.Show(existingData, onComplete, onCancel);
///
/// The dialog attaches itself to a persistent hidden GameObject so it survives
/// scene transitions and is cleaned up automatically when dismissed.
/// </summary>
public class ConnectionDialog : MonoBehaviour
{
    // ── Public factory ─────────────────────────────────────────────────────────

    public static ConnectionDialog Show(
        APConnectionData initialData,
        Action<APConnectionData> onComplete,
        Action onCancel,
        string errorMessage = null)
    {
        // Destroy any lingering dialog first.
        var existing = FindObjectOfType<ConnectionDialog>();
        if (existing != null)
            Destroy(existing.gameObject);

        var go = new GameObject("RL2AP_ConnectionDialog");
        DontDestroyOnLoad(go);

        var dialog = go.AddComponent<ConnectionDialog>();
        dialog._onComplete    = onComplete;
        dialog._onCancel      = onCancel;
        dialog._connData      = initialData?.Clone() ?? new APConnectionData();
        dialog._errorMessage  = errorMessage;

        // Pre-populate fields with existing data so the player can make small edits.
        dialog._hostPortInput = $"{dialog._connData.Hostname}:{dialog._connData.Port}";
        dialog._slotInput     = dialog._connData.SlotName ?? "";
        dialog._passwordInput = dialog._connData.Password ?? "";

        dialog.GoToStep(Step.HostPort);

        return dialog;
    }

    // ── Private state ──────────────────────────────────────────────────────────

    private enum Step { HostPort, SlotName, Password }

    private Step   _currentStep;
    private string _hostPortInput = "archipelago.gg:38281";
    private string _slotInput     = "";
    private string _passwordInput = "";
    private string _errorMessage;
    private string _stepError;           // validation error shown inline
    private bool   _isVisible;

    private APConnectionData         _connData;
    private Action<APConnectionData> _onComplete;
    private Action                   _onCancel;

    // IMGUI style cache
    private GUIStyle _windowStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _inputStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _errorStyle;
    private bool     _stylesBuilt;

    // Dialog dimensions (pixels).  Scales with screen height so it looks
    // reasonable at common resolutions.
    private Rect DialogRect => new(
        (Screen.width  - DialogW) / 2f,
        (Screen.height - DialogH) / 2f,
        DialogW, DialogH);

    private float DialogW => Mathf.Clamp(Screen.width  * 0.45f, 480f, 720f);
    private float DialogH => Mathf.Clamp(Screen.height * 0.42f, 300f, 480f);

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_isVisible) return;

        // Build styles lazily (requires GUI skin to be active, i.e. inside OnGUI).
        if (!_stylesBuilt) BuildStyles();

        // Dim the background with a semi-transparent overlay.
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Draw the dialog window.
        GUILayout.BeginArea(DialogRect, _windowStyle);
        DrawDialogContent();
        GUILayout.EndArea();

        // Consume all events while the dialog is visible so the game menu doesn't
        // also react to keyboard/mouse input.
        if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
            Event.current.Use();
    }

    // ── Step navigation ────────────────────────────────────────────────────────

    private void GoToStep(Step step)
    {
        _currentStep = step;
        _stepError   = null;
        _isVisible   = true;
        // Keyboard focus is requested inside DrawDialogContent on the Layout event,
        // which is safe because it runs within OnGUI.
    }

    private void DrawDialogContent()
    {
        float pad = 20f;
        GUILayout.Space(pad);

        // ── Title ──────────────────────────────────────────────────────────────
        GUILayout.Label("ARCHIPELAGO CONNECTION", _titleStyle);
        GUILayout.Space(8f);

        // ── Step indicator ────────────────────────────────────────────────────
        int stepNum    = (int)_currentStep + 1;
        string stepTag = _currentStep switch
        {
            Step.HostPort => "1 / 3 — Server Address",
            Step.SlotName => "2 / 3 — Slot / Player Name",
            Step.Password => "3 / 3 — Password",
            _             => ""
        };
        GUILayout.Label($"Step {stepTag}", _labelStyle);
        GUILayout.Space(6f);

        // ── Description ───────────────────────────────────────────────────────
        string desc = _currentStep switch
        {
            Step.HostPort =>
                "Enter the hostname and port of your Archipelago server.\n" +
                "e.g.  \"archipelago.gg:12345\"  or  \"localhost:38281\"",
            Step.SlotName =>
                "Enter your slot / player name exactly as it appears in\n" +
                "the Archipelago multiworld (case-sensitive).",
            Step.Password =>
                "Enter the room password, or leave blank if there is none.",
            _ => ""
        };
        GUILayout.Label(desc, _labelStyle);
        GUILayout.Space(10f);

        // ── Connection-level error (shown on step 1 if a previous attempt failed) ──
        if (_currentStep == Step.HostPort && !string.IsNullOrEmpty(_errorMessage))
        {
            GUILayout.Label($"Connection error:\n{_errorMessage}", _errorStyle);
            GUILayout.Space(6f);
        }

        // ── Input field ───────────────────────────────────────────────────────
        GUI.SetNextControlName(InputControlName);
        string currentInput = _currentStep switch
        {
            Step.HostPort => _hostPortInput,
            Step.SlotName => _slotInput,
            Step.Password => _passwordInput,
            _             => ""
        };
        string newInput = GUILayout.TextField(currentInput, _inputStyle, GUILayout.Height(36f));
        switch (_currentStep)
        {
            case Step.HostPort: _hostPortInput = newInput; break;
            case Step.SlotName: _slotInput     = newInput; break;
            case Step.Password: _passwordInput = newInput; break;
        }

        // Auto-focus the text field when the step opens.
        if (Event.current.type == EventType.Layout)
            GUI.FocusControl(InputControlName);

        // Inline validation error
        if (!string.IsNullOrEmpty(_stepError))
        {
            GUILayout.Space(4f);
            GUILayout.Label(_stepError, _errorStyle);
        }

        GUILayout.FlexibleSpace();

        // ── Buttons ───────────────────────────────────────────────────────────
        GUILayout.BeginHorizontal();
        GUILayout.Space(pad);

        bool pressedEnter = Event.current.type == EventType.KeyDown &&
                            (Event.current.keyCode == KeyCode.Return ||
                             Event.current.keyCode == KeyCode.KeypadEnter);

        if (GUILayout.Button("Confirm", _buttonStyle, GUILayout.Height(40f)) || pressedEnter)
            OnConfirm();

        GUILayout.Space(12f);

        if (GUILayout.Button("Cancel", _buttonStyle, GUILayout.Height(40f)))
            OnCancel();

        GUILayout.Space(pad);
        GUILayout.EndHorizontal();

        GUILayout.Space(pad);
    }

    // ── Confirm / Cancel logic ─────────────────────────────────────────────────

    private const string InputControlName = "APInputField";

    private void OnConfirm()
    {
        switch (_currentStep)
        {
            case Step.HostPort:
                if (!TryParseHostPort(_hostPortInput, out var host, out var port))
                {
                    _stepError = "Invalid format. Use  host:port  (e.g. archipelago.gg:12345).";
                    return;
                }
                _connData.Hostname = host;
                _connData.Port     = port;
                _connData.RoomId   = null; // treat as a fresh connection attempt
                GoToStep(Step.SlotName);
                break;

            case Step.SlotName:
                if (string.IsNullOrWhiteSpace(_slotInput))
                {
                    _stepError = "Slot name cannot be blank.";
                    return;
                }
                _connData.SlotName = _slotInput.Trim();
                GoToStep(Step.Password);
                break;

            case Step.Password:
                _connData.Password = _passwordInput; // blank is valid
                Close();
                _onComplete?.Invoke(_connData);
                break;
        }
    }

    private void OnCancel()
    {
        Close();
        _onCancel?.Invoke();
    }

    private void Close()
    {
        _isVisible = false;
        Destroy(gameObject);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a "host:port" string.  The port is optional and defaults to 38281.
    /// </summary>
    private static bool TryParseHostPort(string input, out string host, out int port)
    {
        host = null;
        port = 38281;

        if (string.IsNullOrWhiteSpace(input)) return false;

        input = input.Trim();
        var colonIdx = input.LastIndexOf(':');

        if (colonIdx <= 0)
        {
            // No colon → treat the whole string as a hostname with default port.
            host = input;
            return true;
        }

        var possiblePort = input.Substring(colonIdx + 1);
        if (!int.TryParse(possiblePort, out var parsedPort) || parsedPort < 1 || parsedPort > 65535)
        {
            // Colon present but not followed by a valid port — probably a bare IPv6
            // address or a typo.  Reject so the player gets a clear error.
            return false;
        }

        host = input.Substring(0, colonIdx);
        port = parsedPort;
        return !string.IsNullOrWhiteSpace(host);
    }

    // ── IMGUI style setup ──────────────────────────────────────────────────────

    private void BuildStyles()
    {
        // Window / panel background
        var panelTex = MakeSolidTexture(new Color(0.08f, 0.08f, 0.12f, 1f));

        _windowStyle = new GUIStyle(GUI.skin.box)
        {
            padding  = new RectOffset(0, 0, 0, 0),
            margin   = new RectOffset(0, 0, 0, 0),
            normal   = { background = panelTex },
            border   = new RectOffset(4, 4, 4, 4),
        };

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.95f, 0.80f, 0.30f) }, // AP gold
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize    = 13,
            wordWrap    = true,
            normal      = { textColor = new Color(0.88f, 0.88f, 0.88f) },
            padding     = new RectOffset(20, 20, 0, 0),
        };

        _inputStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 15,
            margin   = new RectOffset(20, 20, 0, 0),
            padding  = new RectOffset(8, 8, 6, 6),
            normal   = { background = MakeSolidTexture(new Color(0.15f, 0.15f, 0.20f)),
                         textColor  = Color.white },
            focused  = { background = MakeSolidTexture(new Color(0.20f, 0.20f, 0.28f)),
                         textColor  = Color.white },
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            normal    = { background = MakeSolidTexture(new Color(0.25f, 0.25f, 0.35f)),
                          textColor  = Color.white },
            hover     = { background = MakeSolidTexture(new Color(0.40f, 0.35f, 0.20f)),
                          textColor  = new Color(1f, 0.9f, 0.5f) },
            active    = { background = MakeSolidTexture(new Color(0.55f, 0.45f, 0.15f)),
                          textColor  = Color.white },
        };

        _errorStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal   = { textColor = new Color(1f, 0.40f, 0.35f) },
            padding  = new RectOffset(20, 20, 0, 0),
        };

        _stylesBuilt = true;
    }

    private static Texture2D MakeSolidTexture(Color colour)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
        tex.SetPixel(0, 0, colour);
        tex.Apply();
        return tex;
    }
}
