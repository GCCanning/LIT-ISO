using UnityEditor;
using UnityEngine;

public class AssetForgeImportPostprocessor : AssetPostprocessor
{
    private const float CharacterPixelsPerUnit = 128f;
    private const float TilePixelsPerUnit = 64f;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Generated/", System.StringComparison.Ordinal)
            && !assetPath.StartsWith("Assets/Resources/Characters/Player/AnimationSprites/", System.StringComparison.Ordinal))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        bool isAssetForgeSheet = assetPath.Contains("/AssetForge/")
            && (assetPath.Contains("/actions/")
                || assetPath.EndsWith("/idle.png", System.StringComparison.Ordinal)
                || assetPath.EndsWith("/walk.png", System.StringComparison.Ordinal));
        importer.spriteImportMode = isAssetForgeSheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;

        bool isTile = assetPath.Contains("/Tiles/");
        importer.spritePixelsPerUnit = isTile ? TilePixelsPerUnit : CharacterPixelsPerUnit;

        if (assetPath.Contains("/Characters/") || assetPath.Contains("/AnimationSprites/"))
        {
            importer.spritePivot = new Vector2(0.5f, 0.046875f);
        }
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string asset in importedAssets)
        {
            if (asset.StartsWith("Assets/Generated/", System.StringComparison.Ordinal)
                && asset.EndsWith("/manifest.json", System.StringComparison.Ordinal))
            {
                AssetForgeAutomation.RebuildManifest(asset);
            }
        }
    }
}
