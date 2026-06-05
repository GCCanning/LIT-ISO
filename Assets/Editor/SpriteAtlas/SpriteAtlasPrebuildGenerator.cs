// [UNITY-SKILL:SPRITEATLAS]
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using System.IO;
using System.Collections.Generic;

public class SpriteAtlasPrebuildGenerator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("[SpriteAtlas] Starting prebuild atlas generation...");
        
        // Ensure V2 mode is enabled
        EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

        // Define atlases based on project structure
        GenerateAtlasForFolder("Assets/Tilemaps/Isometric/Sprites/Plains", "Assets/Atlases/World_Plains.spriteatlasv2");
        GenerateAtlasForFolder("Assets/Tilemaps/Isometric/Sprites/Desert", "Assets/Atlases/World_Desert.spriteatlasv2");
        GenerateAtlasForFolder("Assets/Tilemaps/Isometric/Sprites/FrozenMountain", "Assets/Atlases/World_Snow.spriteatlasv2");
        GenerateAtlasForFolder("Assets/Resources/Characters/Player", "Assets/Atlases/Characters_Player.spriteatlasv2");
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Generate Sprite Atlases")]
    public static void ManualGenerate()
    {
        new SpriteAtlasPrebuildGenerator().OnPreprocessBuild(null);
    }

    private void GenerateAtlasForFolder(string sourceFolder, string atlasPath)
    {
        if (!Directory.Exists(sourceFolder)) return;

        string dir = Path.GetDirectoryName(atlasPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // Load or create SpriteAtlasAsset
        SpriteAtlasAsset atlasAsset = AssetDatabase.LoadAssetAtPath<SpriteAtlasAsset>(atlasPath);
        if (atlasAsset == null)
        {
            atlasAsset = new SpriteAtlasAsset();
            AssetDatabase.CreateAsset(atlasAsset, atlasPath);
        }

        // Add the folder to packables
        Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(sourceFolder);
        if (folderObj != null)
        {
            atlasAsset.Add(new[] { folderObj });
        }

        // Configure Importer settings
        SpriteAtlasImporter importer = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
        if (importer != null)
        {
            var packingSettings = importer.packingSettings;
            packingSettings.enableRotation = false;
            packingSettings.enableTightPacking = false;
            packingSettings.padding = 4;
            importer.packingSettings = packingSettings;

            var textureSettings = importer.textureSettings;
            textureSettings.filterMode = FilterMode.Point;
            textureSettings.generateMipMaps = false;
            importer.textureSettings = textureSettings;

            importer.includeInBuild = true;
            importer.SaveAndReimport();
        }

        Debug.Log($"[SpriteAtlas] Generated/Updated atlas at: {atlasPath} from {sourceFolder}");
    }
}
