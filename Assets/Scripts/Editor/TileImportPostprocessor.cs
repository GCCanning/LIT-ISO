#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-applies pixel-art import settings to any PNG dropped into
/// Assets/Resources/Tiles/. Saves a manual Sprite Editor pass every time the owner
/// drops in new terrain art.
///
/// Settings applied:
///   - Texture Type: Sprite (2D and UI)
///   - Filter Mode: Point (no blur)
///   - Compression: None
///   - Mip Maps: off
///   - sRGB: on (matches the rest of the UI pipeline)
///
/// Triggered by Unity automatically when it imports/re-imports a matching file.
/// Existing files inherit the settings on next reimport (Assets → Reimport All, or
/// right-click the Tiles folder → Reimport).
/// </summary>
internal sealed class TileImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (string.IsNullOrEmpty(assetPath)) return;
        // Only touch files under Resources/Tiles/. Leave everything else alone.
        if (!assetPath.Contains("/Resources/Tiles/")) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.sRGBTexture = true;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        // Sprite settings.
        ti.spritePixelsPerUnit = 64f;  // matches PlaceholderArt.PPU so tiles align with the placeholder cubes
        ti.spritePivot = new Vector2(0.5f, 0.5f);
    }
}
#endif
