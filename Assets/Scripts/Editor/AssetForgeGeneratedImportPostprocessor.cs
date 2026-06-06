#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Locks import settings for generated Asset Forge review/approval PNGs.
/// This prevents Unity's default importer from drifting generated tiles/props away
/// from the pixel-art contract every time the editor refreshes.
/// </summary>
internal sealed class AssetForgeGeneratedImportPostprocessor : AssetPostprocessor
{
    private const float TerrainPixelsPerUnit = 32f;
    private const float PropPixelsPerUnit = 128f;

    void OnPreprocessTexture()
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".png"))
        {
            return;
        }

        string path = assetPath.Replace('\\', '/');
        bool isReviewAsset = path.Contains("/Generated/_Review/");
        bool isReviewDecoration = isReviewAsset && path.Contains("/Decorations/");
        bool isReviewPreview = isReviewAsset && path.Contains("/_Preview/");
        bool isGeneratedTerrain = path.Contains("/Generated/Tiles/")
            || (isReviewAsset && !isReviewDecoration && !isReviewPreview);
        bool isGeneratedProp = path.Contains("/Generated/Props/")
            || isReviewDecoration;

        if (!isGeneratedTerrain && !isGeneratedProp)
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 1;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePixelsPerUnit = isGeneratedTerrain ? TerrainPixelsPerUnit : PropPixelsPerUnit;
        settings.spritePivot = isGeneratedTerrain ? new Vector2(0.5f, 0.75f) : new Vector2(0.5f, 0f);
        importer.SetTextureSettings(settings);
    }

    [MenuItem("Tools/Asset Forge/Reimport Generated Assets", false, 160)]
    public static void ReimportGeneratedAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Generated" });
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".png"))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[AssetForgeGeneratedImportPostprocessor] Reimported {guids.Length} generated texture assets.");
    }
}
#endif
