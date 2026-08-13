using HarmonyLib;
using System.Text;
using TMPro;
using UnityEngine;

namespace RL2Archipelago.UI;

/// <summary>
/// The bottom-right "[Spin-Kick] / TOGGLE COLOR TREE VIEW" hint on the manor window, without which
/// <see cref="ManorBranchOverlay"/>'s toggle would be undiscoverable.
///
/// <para>RL2 has no button-prompt component to instantiate. A prompt is a small object holding two
/// separate TMP_Texts, styled differently: one carrying a bracketed Rewired action name, which a
/// <c>TextGlyphConverter</c> on the same GameObject rewrites into a sprite tag (the right keyboard
/// key, or the right face button for whichever gamepad brand the player has selected, re-resolved
/// whenever the active controller changes), and a smaller outlined label beneath it. Both are
/// authored on the window prefab inside <c>SkillTreeWindowController.m_navigationObj</c> and never
/// touched from code.</para>
///
/// <para>So rather than build a prompt we clone a whole vanilla one and swap the two strings. That
/// is the only way to match the game's typography exactly: the glyph and the label differ in font
/// size, material (the label is outlined, the glyph is not) and placement, none of which is
/// discoverable from the assembly.</para>
/// </summary>
internal static class ManorBranchPrompt
{
    private const string PromptName = "AP_ManorBranchPrompt";

    /// <summary>The live prompt. Like the overlay's root, this is a child of the window prefab,
    /// which the game destroys and re-instantiates on every scene load; Unity's overloaded
    /// <c>==</c> makes the destroyed object compare null, which is what triggers a rebuild against
    /// the new window.</summary>
    private static GameObject s_prompt;

    /// <summary>The cloned glyph's converter, kept so a rebind can be re-resolved on reopen.</summary>
    private static TextGlyphConverter s_converter;

    /// <summary>Creates the prompt if needed, or re-resolves its glyph if it already exists.</summary>
    public static void Build(SkillTreeWindowController controller)
    {
        var navObj = Traverse.Create(controller).Field<GameObject>("m_navigationObj").Value;
        if (navObj == null)
        {
            Plugin.Log.LogWarning("[ManorBranchPrompt] m_navigationObj not found; prompt skipped.");
            return;
        }

        if (APSettings.DebugKeysEnabled) Dump(navObj.transform);

        // The prompt deliberately does NOT go under m_skillTreeIconsCanvasGroup like the overlay
        // does: castle view fades that group to alpha 0, and the prompt should stay on screen
        // there just as the game's own prompts do. It does not go under m_navigationObj either,
        // since that row positions its children itself, which would fight the explicit
        // bottom-right anchoring below.
        var canvas = navObj.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Plugin.Log.LogWarning("[ManorBranchPrompt] No canvas above m_navigationObj; prompt skipped.");
            return;
        }

        if (s_prompt == null || s_prompt.transform.parent != canvas.transform)
        {
            if (s_prompt != null) Object.Destroy(s_prompt);
            s_prompt = Create(navObj, canvas.transform);
        }

        // UpdateText dereferences a TMP_Text cached in Awake, and IsInitialized is only set once
        // Start has run. Before that there is nothing to do anyway: Initialize() ends in an
        // UpdateText(forceUpdate: true) of its own, and OnEnable's UpdateText notices the text no
        // longer matches what it last converted and re-converts regardless.
        if (s_converter != null && s_converter.IsInitialized) s_converter.UpdateText(forceUpdate: true);
    }

    // ── Construction ─────────────────────────────────────────────────────────

    private static GameObject Create(GameObject navObj, Transform parent)
    {
        // Defensive sweep: destroy any prompt left behind that s_prompt no longer tracks (a
        // plugin hot reload, say) so the canvas can never accumulate duplicates.
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name == PromptName) Object.Destroy(child.gameObject);
        }

        s_converter = null;

        if (!FindTemplate(navObj, out var entry, out int glyphIndex, out int labelIndex))
        {
            Plugin.Log.LogWarning("[ManorBranchPrompt] No vanilla prompt to copy; prompt skipped.");
            return null;
        }

        var clone = Object.Instantiate(entry.gameObject, parent);
        clone.name = PromptName;

        StripTextOverrides(clone);

        // Instantiate preserves hierarchy order exactly, so the indices found on the template
        // address the same two objects in the copy.
        var texts = clone.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        var glyph = texts[glyphIndex];
        var label = labelIndex >= 0 ? texts[labelIndex] : null;

        glyph.text = $"[{Rewired_RL.Action_Downstrike}]";

        if (label != null)
        {
            label.text = ManorBranchLayout.PromptLabel;

            // Our label is longer than any the prefab was authored around, so let it spill out of
            // its box rather than wrap onto a second line. Everything else about it - font, size,
            // outline material, color, alignment - is inherited untouched, which is the whole
            // point of cloning.
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }
        else
        {
            // No separate label on the template, so the prompt is a single string. Fall back to
            // putting the text on the same line as the glyph.
            glyph.text += " " + ManorBranchLayout.PromptLabel;
        }

        // Anchored to the bottom-right corner. sizeDelta is deliberately left as authored: the
        // glyph and label are positioned against this rect, so resizing it would pull them apart.
        var rt = (RectTransform)clone.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = ManorBranchLayout.PromptOffset;
        rt.localRotation = Quaternion.identity;

        s_converter = glyph.GetComponent<TextGlyphConverter>();

        return clone;
    }

    /// <summary>Locates a vanilla prompt to clone: the smallest object under the navigation row
    /// that contains both a glyph-bearing label and a plain one. Indices are into that object's
    /// <c>GetComponentsInChildren&lt;TMP_Text&gt;</c>, so they survive the clone.</summary>
    private static bool FindTemplate(GameObject navObj, out Transform entry, out int glyphIndex, out int labelIndex)
    {
        entry = null;
        glyphIndex = -1;
        labelIndex = -1;

        foreach (var glyph in navObj.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            if (glyph.GetComponent<TextGlyphConverter>() == null) continue;

            // Walk outward from the glyph, stopping at the first ancestor that also holds a plain
            // label. Stopping early matters: the navigation row itself holds every prompt, so
            // going one level too far would clone all of them.
            for (var candidate = glyph.transform.parent;
                 candidate != null && candidate != navObj.transform;
                 candidate = candidate.parent)
            {
                var texts = candidate.GetComponentsInChildren<TMP_Text>(includeInactive: true);

                for (int i = 0; i < texts.Length; i++)
                {
                    // The keyboard glyph's letter ("E", "SHIFT") is itself a TMP_Text the game
                    // spawns underneath the glyph, so anything below the glyph is not the label.
                    if (texts[i].transform.IsChildOf(glyph.transform)) continue;
                    if (texts[i].GetComponent<TextGlyphConverter>() != null) continue;

                    entry = candidate;
                    glyphIndex = System.Array.IndexOf(texts, glyph);
                    labelIndex = i;
                    return true;
                }
            }

            // A prompt with no separate label. Usable, just not the preferred shape.
            entry = glyph.transform;
            glyphIndex = System.Array.IndexOf(
                glyph.GetComponentsInChildren<TMP_Text>(includeInactive: true), glyph);
            return true;
        }

        return false;
    }

    /// <summary>Destroys, throughout the clone, only the components that would write over the two
    /// strings we set: the localization binding and the string-replacement pass. Everything else
    /// the prefab carries is left alone, since some of it is what positions the label under the
    /// glyph.</summary>
    private static void StripTextOverrides(GameObject go)
    {
        // Immediate, so a cloned LocalizationItem never gets a Start() in which to re-apply the
        // original locale string.
        foreach (var loc in go.GetComponentsInChildren<LocalizationItem>(includeInactive: true))
            Object.DestroyImmediate(loc);

        foreach (var replacement in go.GetComponentsInChildren<StringReplacementUtility>(includeInactive: true))
            Object.DestroyImmediate(replacement);
    }

    // ── Debug dump ───────────────────────────────────────────────────────────

    /// <summary>Logs the navigation row's hierarchy with each label's typography, which is what
    /// makes the cloned prompt checkable against the vanilla ones without a Unity editor.</summary>
    private static void Dump(Transform root)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[ManorBranchPrompt] Navigation row under \"{root.name}\":");
        AppendNode(sb, root, 1);
        Plugin.Log.LogInfo(sb.ToString());
    }

    private static void AppendNode(StringBuilder sb, Transform node, int depth)
    {
        sb.Append(' ', depth * 2).Append(node.name);

        if (node is RectTransform rt)
            sb.Append($"  rect({rt.sizeDelta.x:F0}x{rt.sizeDelta.y:F0} @ {rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0})");

        if (node.TryGetComponent<TMP_Text>(out var tmp))
            sb.Append($"  TMP(size={tmp.fontSize:F1} font={tmp.font?.name} mat={tmp.fontSharedMaterial?.name}" +
                      $" align={tmp.alignment} color=#{ColorUtility.ToHtmlStringRGBA(tmp.color)} text=\"{tmp.text}\")");

        foreach (var comp in node.GetComponents<Component>())
            if (comp != null && comp is not Transform && comp is not TMP_Text)
                sb.Append("  +").Append(comp.GetType().Name);

        sb.AppendLine();

        for (int i = 0; i < node.childCount; i++)
            AppendNode(sb, node.GetChild(i), depth + 1);
    }
}
