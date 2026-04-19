using System;
using System.Collections;
using HarmonyLib;
using RL_Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RL2Archipelago.UI;

public class ConnectionDialog : MonoBehaviour
{
    // ── Public factory ─────────────────────────────────────────────────────────

    public static ConnectionDialog Show(
        APConnectionData initialData,
        Action<APConnectionData> onComplete,
        Action onCancel,
        string errorMessage = null)
    {
        var existing = FindObjectOfType<ConnectionDialog>();
        if (existing != null) Destroy(existing.gameObject);

        var go = new GameObject("RL2AP_ConnectionDialog");
        DontDestroyOnLoad(go);

        var dialog = go.AddComponent<ConnectionDialog>();
        dialog._onComplete   = onComplete;
        dialog._onCancel     = onCancel;
        dialog._connData     = initialData?.Clone() ?? new APConnectionData();
        dialog._errorMessage = errorMessage;

        dialog._hostPortInput = $"{dialog._connData.Hostname}:{dialog._connData.Port}";
        dialog._slotInput     = dialog._connData.SlotName ?? "";
        dialog._passwordInput = dialog._connData.Password ?? "";

        return dialog;
    }

    // ── Private state ──────────────────────────────────────────────────────────

    private enum Step { HostPort, SlotName, Password }

    private Step   _currentStep;
    private string _hostPortInput = "archipelago.gg:38281";
    private string _slotInput;
    private string _passwordInput;
    private string _errorMessage;
    private string _stepError; // validation error for the current step, e.g. "invalid host:port format"
    private bool   _isVisible;

    private APConnectionData         _connData;
    private Action<APConnectionData> _onComplete;
    private Action                   _onCancel;

    // Guards against overwriting a stored value during a time in which we assign
    //  _inputField.text programmatically (e.g. when switching steps) 
    // These changes would typically fire onValueChanged, and we
    // don't want it to overwrite the target step's input value.
    private bool _suppressValueChange;

    // ── UI references ──────────────────────────────────────────────────────────

    private ConfirmMenuWindowController _ctrl;
    private TMP_InputField              _inputField;    // built-in caret, selection, key repeat, mouse click
    private TMP_Text                    _errorLabel;
    private TMP_Text                    _titleText;
    private TMP_Text                    _descText;
    private bool                        _uiBuilt;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    private IEnumerator Start()
    {
        yield return null; // ensure CameraController.UICamera is ready
        BuildFromConfirmMenu();
        GoToStep(Step.HostPort);
    }

    private void Update()
    {
        if (!_isVisible || !_uiBuilt) return;

        // Enter is routed through TMP_InputField.onSubmit (see MakeInputField).
        // Escape has no built-in handling, and TMP_InputField would just
        // deactivate its own focus on it — we want to cancel the whole dialog.
        if (Input.GetKeyDown(KeyCode.Escape))
            OnCancel();
    }

    // ── Step navigation ────────────────────────────────────────────────────────

    private void GoToStep(Step step)
    {
        _currentStep = step;
        _stepError   = null;
        _isVisible   = true;
        if (_uiBuilt) RefreshStep();
    }

    private void RefreshStep()
    {
        _titleText.text = "ARCHIPELAGO CONNECTION";

        _descText.text = _currentStep switch
        {
            Step.HostPort =>
                "Step 1 / 3  —  Server Address\n\n" +
                "Enter the hostname and port of your Archipelago server.\n" +
                "e.g.  archipelago.gg:12345  or  localhost:38281",
            Step.SlotName =>
                "Step 2 / 3  —  Slot / Player Name\n\n" +
                "Enter your slot / player name exactly as it appears\n" +
                "in the Archipelago multiworld (case-sensitive).",
            Step.Password =>
                "Step 3 / 3  —  Password\n\n" +
                "Enter the room password, or leave blank if there is none.",
            _ => ""
        };

        string errorText = null;
        if (_currentStep == Step.HostPort && !string.IsNullOrEmpty(_errorMessage))
            errorText = $"Connection error:  {_errorMessage}";
        else if (!string.IsNullOrEmpty(_stepError))
            errorText = _stepError;

        _errorLabel.text = errorText ?? "";
        _errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(errorText));

        ApplyFieldForStep();
    }

    // ── Input helpers ──────────────────────────────────────────────────────────

    private string GetCurrentInput() => _currentStep switch
    {
        Step.HostPort => _hostPortInput,
        Step.SlotName => _slotInput,
        Step.Password => _passwordInput,
        _             => ""
    };

    private void SetCurrentInput(string value)
    {
        switch (_currentStep)
        {
            case Step.HostPort: _hostPortInput = value; break;
            case Step.SlotName: _slotInput     = value; break;
            case Step.Password: _passwordInput = value; break;
        }
    }

    // Loads the current step's stored value into the input field, switches
    // password masking, focuses the field, and puts the caret at end of text.
    private void ApplyFieldForStep()
    {
        if (_inputField == null) return;

        _suppressValueChange = true;
        _inputField.contentType = _currentStep == Step.Password
            ? TMP_InputField.ContentType.Password
            : TMP_InputField.ContentType.Standard;
        _inputField.text = GetCurrentInput();
        _suppressValueChange = false;

        // ActivateInputField sets EventSystem selection + starts the text-input
        // state, bypassing the usual OnSelect-from-pointer-click requirement.
        _inputField.Select();
        _inputField.ActivateInputField();
        int end = _inputField.text.Length;
        _inputField.caretPosition   = end;
        _inputField.stringPosition  = end;
    }

    private void OnInputValueChanged(string newValue)
    {
        if (_suppressValueChange) return;
        SetCurrentInput(newValue);
    }

    // ── Confirm / Cancel ───────────────────────────────────────────────────────

    private void OnConfirm()
    {
        switch (_currentStep)
        {
            case Step.HostPort:
                if (!TryParseHostPort(_hostPortInput, out var host, out var port))
                {
                    _stepError = "Invalid format. Use  host:port  (e.g. archipelago.gg:12345).";
                    RefreshStep();
                    return;
                }
                _connData.Hostname = host;
                _connData.Port     = port;
                _connData.RoomId   = null;
                GoToStep(Step.SlotName);
                break;

            case Step.SlotName:
                if (string.IsNullOrWhiteSpace(_slotInput))
                {
                    _stepError = "Slot name cannot be blank.";
                    RefreshStep();
                    return;
                }
                _connData.SlotName = _slotInput.Trim();
                GoToStep(Step.Password);
                break;

            case Step.Password:
                _connData.Password = _passwordInput;
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

    // ── ConfirmMenu clone ──────────────────────────────────────────────────────

    private void BuildFromConfirmMenu()
    {
        var original = WindowManager.GetWindowController(WindowID.ConfirmMenu) as ConfirmMenuWindowController;
        if (original == null)
        {
            Plugin.Log.LogError("[ConnectionDialog] ConfirmMenu not found in scene — cannot build dialog.");
            return;
        }

        var cloneGO = UnityEngine.Object.Instantiate(original.gameObject);
        cloneGO.transform.SetParent(transform, false);

        _ctrl = cloneGO.GetComponent<ConfirmMenuWindowController>();

        // Sets canvas to ScreenSpaceCamera+UICamera, hides canvas, stores box scale, builds button array.
        _ctrl.Initialize();

        // Show canvas above all WindowManager-managed windows.
        _ctrl.WindowCanvas.gameObject.SetActive(true);
        _ctrl.WindowCanvas.sortingOrder = 500;

        // Access serialized fields — these are correctly remapped to the clone's own objects by Instantiate.
        var tv     = Traverse.Create(_ctrl);
        var fadeBG = tv.Field<CanvasGroup>("m_fadeBGCanvasGroup").Value;
        var boxRT  = tv.Field<RectTransform>("m_confirmBoxBGRectTransform").Value;
        var box    = tv.Field<GameObject>("m_confirmMenuBox").Value;
        var desc   = tv.Field<TMP_Text>("m_descriptionText").Value;
        var title  = tv.Field<TMP_Text>("m_titleText").Value;

        _titleText = title;
        _descText  = desc;

        fadeBG.alpha = 1f;

        // Wire 2 buttons via the controller's existing API.
        _ctrl.SetNumberOfButtons(2);
        _ctrl.GetButtonAtIndex(0).SetButtonText("Confirm", isLocID: false);
        _ctrl.GetButtonAtIndex(0).SetOnClickAction(OnConfirm);
        _ctrl.GetButtonAtIndex(1).SetButtonText("Cancel",  isLocID: false);
        _ctrl.GetButtonAtIndex(1).SetOnClickAction(OnCancel);

        int uiLayer        = desc.gameObject.layer;
        TMP_FontAsset font = title.font;
        float bodySize     = desc.fontSize;

        var buttonArray = tv.Field<ConfirmMenu_Button[]>("m_buttonArray").Value;

        // Expand background height.  Do NOT touch anchoredPosition or pivot — those
        // are shared by m_confirmBoxBGRectTransform and m_confirmMenuBox when they are
        // the same object (or parent/child), and zeroing them detaches the visual frame
        // from the content it contains.
        Vector2 sz      = boxRT.sizeDelta;
        float oldHeight = sz.y;
        sz.y            = 1100f;   // original 865 + ~235 for input(90) + error(60) + gaps(60)
        boxRT.sizeDelta = sz;

        // Pivot != 0.5 means expanding sizeDelta shifts the visual center.
        // Recompute anchoredPosition.y so the box stays visually centered.
        float   pivotY        = boxRT.pivot.y;
        float   visualCenterY = boxRT.anchoredPosition.y + (0.5f - pivotY) * oldHeight;
        Vector2 ap            = boxRT.anchoredPosition;
        ap.y                  = visualCenterY - (0.5f - pivotY) * sz.y;
        boxRT.anchoredPosition = ap;

        LayoutRebuilder.ForceRebuildLayoutImmediate(boxRT);

        // Use GetWorldCorners() — reliable regardless of anchor/pivot/sizeDelta setup.
        var descRT       = (RectTransform)desc.transform;
        var descCorners  = new Vector3[4];
        descRT.GetWorldCorners(descCorners);
        // corners: 0=bottom-left  1=top-left  2=top-right  3=bottom-right  (world space)
        Vector3 descBottomWorld  = (descCorners[0] + descCorners[3]) * 0.5f;
        Vector3 descBottomInBox  = box.transform.InverseTransformPoint(descBottomWorld);
        float   descCenterX      = box.transform.InverseTransformPoint(
                                       descRT.TransformPoint(Vector3.zero)).x;

        float descWorldWidth = Vector3.Distance(descCorners[0], descCorners[3]);
        float contentWidth   = descWorldWidth > 10f
            ? descWorldWidth / _ctrl.WindowCanvas.scaleFactor
            : boxRT.sizeDelta.x * 0.85f;

        // Text display: 20 px gap below description bottom, 90 px tall.
        float inputCenterY = descBottomInBox.y - 20f - 45f;
        _inputField = MakeInputField(box.transform, font, bodySize, uiLayer);
        _inputField.onValueChanged.AddListener(OnInputValueChanged);
        _inputField.onSubmit.AddListener(_ => OnConfirm());
        var inputRT  = (RectTransform)_inputField.transform;
        inputRT.sizeDelta        = new Vector2(contentWidth, 90f);
        inputRT.anchoredPosition = new Vector2(descCenterX, inputCenterY);

        float cancelCenterY = inputCenterY - 45f - 10f - 30f; // fallback; overwritten inside button block
        float btnHeight     = 60f;
        if (buttonArray != null && buttonArray.Length >= 2 &&
            buttonArray[0] != null && buttonArray[1] != null)
        {
            var confirmBtn = buttonArray[0];
            var cancelBtn  = buttonArray[1];

            // Disable any LayoutGroup on the original button container — the ConfirmMenu
            // prefab uses one to lay buttons out side-by-side, which fights manual placement.
            if (confirmBtn.transform.parent != null)
            {
                foreach (var lg in confirmBtn.transform.parent.GetComponents<LayoutGroup>())
                    lg.enabled = false;
            }

            var confirmRT = (RectTransform)confirmBtn.transform;
            var cancelRT  = (RectTransform)cancelBtn.transform;

            // Reparent to box so both buttons share the input field's coordinate space —
            // descCenterX / inputCenterY are computed in box-local coordinates.
            confirmRT.SetParent(box.transform, false);
            cancelRT .SetParent(box.transform, false);

            // Center pivot+anchors so anchoredPosition places the button's center.
            confirmRT.anchorMin = confirmRT.anchorMax = new Vector2(0.5f, 0.5f);
            confirmRT.pivot     = new Vector2(0.5f, 0.5f);
            cancelRT .anchorMin = cancelRT .anchorMax = new Vector2(0.5f, 0.5f);
            cancelRT .pivot     = new Vector2(0.5f, 0.5f);

            btnHeight            = Mathf.Max(confirmRT.rect.height, cancelRT.rect.height, 60f);
            float gap            = 15f;
            float confirmCenterY = inputCenterY - 45f - 10f - btnHeight * 0.5f;
            cancelCenterY        = confirmCenterY - btnHeight - gap;

            confirmRT.anchoredPosition = new Vector2(descCenterX, confirmCenterY);
            cancelRT .anchoredPosition = new Vector2(descCenterX, cancelCenterY);
        }

        // Error label: 20 px gap below cancel button, 60 px tall.
        float errorCenterY = cancelCenterY - btnHeight * 0.5f - 20f - 30f;
        _errorLabel = MakeErrorLabel(box.transform, font, bodySize * 0.85f, uiLayer);
        var errorRT  = (RectTransform)_errorLabel.transform;
        errorRT.sizeDelta        = new Vector2(contentWidth, 60f);
        errorRT.anchoredPosition = new Vector2(descCenterX, errorCenterY);
        _errorLabel.gameObject.SetActive(false);

        _uiBuilt = true;
    }

    // ── UI element factories ───────────────────────────────────────────────────

    // Builds a TMP_InputField with the standard Unity three-object hierarchy:
    //   Root  (Image bg + TMP_InputField)
    //   └─ Text Area  (RectMask2D viewport)
    //       └─ Text   (TextMeshProUGUI that renders the current value)
    // The root GO (Game Object) is built inactive and activated at the end: TMP_InputField's
    // OnEnable creates the caret GameObject once, and only if m_TextComponent
    // is already assigned. AddComponent on an active GO would fire OnEnable
    // *before* we could wire textComponent, silently skipping caret creation.
    private static TMP_InputField MakeInputField(Transform parent, TMP_FontAsset font, float fontSize, int layer)
    {
        var go = new GameObject("APTextField", typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(parent, false);

        var rt       = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(700f, 90f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        // Background — raycastTarget=true so clicks route to the InputField.
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        var viewportGO = new GameObject("Text Area", typeof(RectTransform));
        viewportGO.transform.SetParent(go.transform, false);
        var viewportRT       = (RectTransform)viewportGO.transform;
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(14f, 6f);
        viewportRT.offsetMax = new Vector2(-14f, -6f);
        viewportGO.AddComponent<RectMask2D>();

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(viewportGO.transform, false);
        var textRT       = (RectTransform)textGO.transform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var text                = textGO.AddComponent<TextMeshProUGUI>();
        text.font               = font;
        text.fontSize           = fontSize;
        text.color              = Color.white;
        text.alignment          = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.raycastTarget      = false;

        var input = go.AddComponent<TMP_InputField>();
        input.textViewport     = viewportRT;
        input.textComponent    = text;
        input.fontAsset        = font;
        input.pointSize        = fontSize;
        input.lineType         = TMP_InputField.LineType.SingleLine;
        input.customCaretColor = true;
        input.caretColor       = Color.white;
        input.caretWidth       = 2;
        input.caretBlinkRate   = 1.7f;
        input.selectionColor   = new Color(0.3f, 0.5f, 1f, 0.5f);

        SetLayerRecursive(go, layer);
        go.SetActive(true);
        return input;
    }

    private static TMP_Text MakeErrorLabel(Transform parent, TMP_FontAsset font, float fontSize, int layer)
    {
        var go = new GameObject("APError", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt       = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(700f, 60f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        var text              = go.AddComponent<TextMeshProUGUI>();
        text.font             = font;
        text.fontSize         = fontSize;
        text.color            = new Color(1f, 0.40f, 0.35f);
        text.alignment        = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget    = false;

        SetLayerRecursive(go, layer);
        return text;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static bool TryParseHostPort(string input, out string host, out int port)
    {
        host = null;
        port = 38281;

        if (string.IsNullOrWhiteSpace(input)) return false;

        input = input.Trim();
        var colonIdx = input.LastIndexOf(':');

        if (colonIdx <= 0)
        {
            host = input;
            return true;
        }

        var possiblePort = input.Substring(colonIdx + 1);
        if (!int.TryParse(possiblePort, out var parsedPort) || parsedPort < 1 || parsedPort > 65535)
            return false;

        host = input.Substring(0, colonIdx);
        port = parsedPort;
        return !string.IsNullOrWhiteSpace(host);
    }
}
