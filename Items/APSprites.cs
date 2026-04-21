using RL2Archipelago.Locations;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RL2Archipelago.Items;

/// <summary>
/// Resolves the in-world sprite used to represent an AP item at a location.
/// Used by props (e.g. heirloom pedestals) to show what will drop there: the
/// native RL2 icon when the item belongs to this world, otherwise a generic
/// Archipelago logo.
/// </summary>
internal static class APSprites
{
    // "<RootNamespace>.<path-with-dots>.<filename>" — set by the EmbeddedResource
    // entry in the csproj. Renaming the asset or changing RootNamespace breaks this.
    private const string AP_LOGO_RESOURCE = "RL2Archipelago.Assets.ap_logo.png";

    // Chosen so the 512x512 PNG renders at roughly the same world-space size as a
    // vanilla heirloom icon (~1 Unity unit tall).
    private const float AP_LOGO_PPU = 512f;

    private static Sprite _apLogoSprite;

    /// <summary>
    /// Returns the sprite that should appear on the in-world prop for the item at
    /// <paramref name="locationId"/>. Falls back to the generic AP logo when the
    /// scout hasn't returned yet or the item has no native graphic.
    /// </summary>
    public static Sprite GetSpriteForLocation(long locationId)
    {
        var scouted = APClient.GetScoutedItem(locationId);
        if (scouted == null) return GetAPLogoSprite();

        // Items belonging to this slot ("Rogue Legacy 2") may have a native sprite
        // we can reuse. Only heirlooms are mapped for now; extend as more item
        // categories with visible props are added.
        var heirloomType = ItemRegistry.ToHeirloomType(scouted.ItemId);
        if (heirloomType.HasValue)
        {
            var sprite = IconLibrary.GetHeirloomSprite(heirloomType.Value);
            if (sprite != null) return sprite;
        }

        return GetAPLogoSprite();
    }

    /// <summary>
    /// Lazily decodes the embedded AP logo PNG into a <see cref="Sprite"/>.
    /// Returns <c>null</c> if the resource is missing or PNG decoding fails;
    /// callers should handle that by leaving the vanilla sprite in place.
    /// </summary>
    public static Sprite GetAPLogoSprite()
    {
        if (_apLogoSprite != null) return _apLogoSprite;

        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(AP_LOGO_RESOURCE);
            if (stream == null)
            {
                Plugin.Log.LogError($"[AP] Embedded resource '{AP_LOGO_RESOURCE}' not found.");
                return null;
            }

            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);

            // mipChain=false + Bilinear filter keeps the crisp-but-soft look of the
            // vanilla UI icons at various camera zooms. HideFlags stops Unity's
            // resource cleanup from unloading it between scenes.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (!ImageConversion.LoadImage(tex, bytes))
            {
                Plugin.Log.LogError("[AP] Failed to decode embedded AP logo PNG.");
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            var rect = new Rect(0, 0, tex.width, tex.height);
            _apLogoSprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), AP_LOGO_PPU);
            _apLogoSprite.hideFlags = HideFlags.HideAndDontSave;
            return _apLogoSprite;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AP] Exception loading AP logo: {ex.Message}");
            return null;
        }
    }
}
