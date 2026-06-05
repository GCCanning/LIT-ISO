using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fixes texture import settings on all isometric tile sprites so they render
/// crisply without the vertical banding / seam artifacts that plague pixel-art
/// tilemaps.
///
/// The problems this fixes:
///   1. Texture COMPRESSION (DXT/BC) — creates 4x4 block color banding that,
///      when tiled across the world, reads as repeating vertical/diagonal bands.
///   2. TIGHT sprite mesh — wraps the mesh tightly around opaque pixels, leaving
///      sub-pixel gaps between adjacent diamond tiles ("floating island" seams).
///   3. Bilinear FILTERING — softens tile edges and bleeds neighbouring pixels.
///   4. MIPMAPS — at non-integer zoom, a softened mip level shimmers.
///
/// Correct settings for tile sprites:
///   - filterMode = Point (crisp pixels)
///   - textureCompression = Uncompressed (no block banding)
///   - mipmaps = off
///   - spriteMeshType = FullRect (tiles share exact edges, no seams)
///   - extrudeEdges = 1 (pads the sprite rect so atlas bleed can't show through)
///
/// Run via: Tools > LIT-ISO > Fixes > Fix Tile Import Settings
/// </summary>
public static class TileImportFixer
{
    // Folders whose PNGs are treated as world tiles.
    private static readonly string[] TileFolders =
    {
        "Assets/Tilemaps/Isometric"
    };

    [MenuItem("Tools/LIT-ISO/Fixes/Fix Tile Import Settings", false, 200)]
    public static void FixTileImports()
    {
        List<string> texturePaths = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", TileFolders);
        foreach (string guid in guids)
        {
            texturePaths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        if (texturePaths.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Fix Tile Imports",
                "No tile textures found under:\n" + string.Join("\n", TileFolders),
                "OK");
            return;
        }

        int changed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < texturePaths.Count; i++)
            {
                string path = texturePaths[i];
                EditorUtility.DisplayProgressBar(
                    "Fixing Tile Imports",
                    path,
                    (float)i / texturePaths.Count);

                if (ApplyTileSettings(path))
                {
                    changed++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        // Also fix the live project + open scene so the result is visible without
        // a full scene rebuild.
        int fixedSettings = FixRenderSettings();

        Debug.Log($"[TileImportFixer] Updated import settings on {changed} of {texturePaths.Count} tile textures. " +
                  $"Fixed {fixedSettings} render setting(s).");
        EditorUtility.DisplayDialog(
            "Fix Tile Imports",
            $"Done.\n\nUpdated {changed} of {texturePaths.Count} tile textures.\n\n" +
            "Settings applied: Point filter, Uncompressed, No mipmaps, Full-Rect mesh, Extrude 1.\n\n" +
            "Also disabled MSAA + anisotropic filtering and fixed the open scene's camera.\n\n" +
            "Press Play to see crisp, seamless tiles — no rebuild required.",
            "OK");
    }

    /// <summary>
    /// Disables MSAA/anisotropic globally and fixes the open scene's main camera,
    /// so the seam/banding fix is visible immediately in the editor play session.
    /// </summary>
    private static int FixRenderSettings()
    {
        int count = 0;

        if (QualitySettings.antiAliasing != 0)
        {
            QualitySettings.antiAliasing = 0;
            count++;
        }
        if (QualitySettings.anisotropicFiltering != AnisotropicFiltering.Disable)
        {
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            count++;
        }

        Camera main = Camera.main;
        if (main != null && main.allowMSAA)
        {
            main.allowMSAA = false;
            count++;
        }

        // Also catch any non-main cameras in the open scene.
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.allowMSAA)
            {
                cam.allowMSAA = false;
                count++;
            }
        }

        return count;
    }

    private static bool ApplyTileSettings(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        bool dirty = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            dirty = true;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }
        if (importer.spritePixelsPerUnit > 0 && importer.spriteImportMode != SpriteImportMode.None)
        {
            // Force FullRect mesh + 1px extrude so tiles share exact edges.
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect || settings.spriteExtrude != 1)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteExtrude = 1;
                importer.SetTextureSettings(settings);
                dirty = true;
            }
        }

        if (dirty)
        {
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
        return dirty;
    }
}
