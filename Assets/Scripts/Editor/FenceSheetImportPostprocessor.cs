#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Locks the pixel-art import contract for the individual settlement fence segment PNGs
/// under Assets/Resources/Decorations/fences (rope / wood / stone), which are sliced from
/// the source 12px sheets by Tools/BiomeSketch-style preprocessing. Each segment is a single
/// 12x12 sprite, point-filtered, bottom-center pivot, so it loads like every other prop.
/// </summary>
internal sealed class FenceSheetImportPostprocessor : AssetPostprocessor
{
    // 12px segments at PPU 24 -> ~0.5 world units, a believable fence height next to the
    // ~1.1-unit player. Tune here if fences read too tall/short in the iso view.
    const float FencePixelsPerUnit = 24f;
    const string FenceSegmentDir = "/Decorations/fences/";

    void OnPreprocessTexture()
    {
        string path = string.IsNullOrEmpty(assetPath) ? "" : assetPath.Replace('\\', '/');
        if (!path.Contains(FenceSegmentDir) || !path.EndsWith(".png")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePixelsPerUnit = FencePixelsPerUnit;
        importer.SetTextureSettings(settings);
    }
}
#endif
