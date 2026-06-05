// LPCImporter.cs - Editor utility to auto-create LPCSpriteSheet assets from PNGs.
//
// Usage in Unity Editor menu: LIT-ISO -> LPC -> Import Sheets from Folder
// Scans Assets/LPC/Sprites/ for PNGs and creates matching LPCSpriteSheet
// ScriptableObject assets in Assets/LPC/Data/.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LITISO.LPC
{
    public static class LPCImporter
    {
        private const string SpritesRoot = "Assets/LPC/Sprites";
        private const string DataRoot    = "Assets/LPC/Data";

        [MenuItem("LIT-ISO/LPC/Import Sheets from Folder")]
        public static void ImportSheets()
        {
            if (!Directory.Exists(SpritesRoot))
            {
                Debug.LogError($"LPC sprites folder not found: {SpritesRoot}");
                return;
            }
            if (!Directory.Exists(DataRoot))
                Directory.CreateDirectory(DataRoot);

            // Subfolder name -> default layer mapping
            var folderToLayer = new System.Collections.Generic.Dictionary<string, LPCLayer>
            {
                { "body",      LPCLayer.Body },
                { "hair",      LPCLayer.Hair },
                { "head",      LPCLayer.Head },
                { "eyes",      LPCLayer.Eyes },
                { "torso",     LPCLayer.Torso },
                { "legs",      LPCLayer.Legs },
                { "feet",      LPCLayer.Feet },
                { "arms",      LPCLayer.Arms },
                { "shoulders", LPCLayer.Shoulders },
                { "shield",    LPCLayer.Shield },
                { "weapon",    LPCLayer.WeaponMain },
                { "neck",      LPCLayer.Neck },
                { "belt",      LPCLayer.Belt },
            };

            int created = 0;
            foreach (var subfolder in folderToLayer.Keys)
            {
                string folder = Path.Combine(SpritesRoot, subfolder);
                if (!Directory.Exists(folder)) continue;

                foreach (string pngPath in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
                {
                    string filename = Path.GetFileNameWithoutExtension(pngPath);
                    string assetPath = pngPath.Replace('\\', '/');

                    // Ensure import settings: pixel art friendly
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Default;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.filterMode = FilterMode.Point;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.alphaIsTransparency = true;
                        importer.mipmapEnabled = false;
                        importer.isReadable = true;   // Required for runtime Sprite.Create
                        importer.SaveAndReimport();
                    }

                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture == null) continue;

                    // Create or update the LPCSpriteSheet asset
                    string sheetAssetPath = $"{DataRoot}/{subfolder}_{filename}.asset";
                    var sheet = AssetDatabase.LoadAssetAtPath<LPCSpriteSheet>(sheetAssetPath);
                    bool isNew = (sheet == null);
                    if (isNew) sheet = ScriptableObject.CreateInstance<LPCSpriteSheet>();

                    sheet.assetId = $"{subfolder}_{filename}";
                    sheet.displayName = filename.Replace('_', ' ');
                    sheet.layer = folderToLayer[subfolder];
                    sheet.texture = texture;
                    // BASE LPC layout animations (all 21-row sheets have these).
                    // Idle is synthesized from Walk[0] in the slicer.
                    sheet.supportedAnimations = new[]
                    {
                        LPCAnimation.Spellcast,
                        LPCAnimation.Thrust,
                        LPCAnimation.Walk,
                        LPCAnimation.Slash,
                        LPCAnimation.Shoot,
                        LPCAnimation.Hurt,
                        LPCAnimation.Idle
                    };

                    if (isNew)
                    {
                        AssetDatabase.CreateAsset(sheet, sheetAssetPath);
                        created++;
                    }
                    else
                    {
                        EditorUtility.SetDirty(sheet);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"LPC Import: created {created} new sprite sheet assets in {DataRoot}");
        }
    }
}
#endif
