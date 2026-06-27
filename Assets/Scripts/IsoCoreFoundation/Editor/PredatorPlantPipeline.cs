#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Import + slice + bake pipeline for the CraftPix predator-plant sheets.
    ///
    /// Sheets live at Assets/Art/Mobs/PredatorPlants/Plant{1,2,3}/&lt;State&gt;/PlantN_&lt;State&gt;_full.png.
    /// Each _full sheet is a grid of 64x64 frames: 4 rows = directions (front/back/left/right),
    /// columns = frames (Idle 4, Walk 6, Run 8, Attack 7, Hurt 5, Death 10, ~150ms/frame).
    ///
    /// - The AssetPostprocessor forces point-filtered, uncompressed, Multiple-mode import for the
    ///   _full sheets so they slice cleanly.
    /// - "Slice + Bake" cuts each sheet into named sub-sprites (Plant_State_dir_frame) and writes
    ///   the runtime Resources/Mobs/MobAnimationLibrary.asset that Mob.cs loads.
    ///
    /// Licensing: these are CraftPix "free" assets (NOT original/PixelLab art) — confirm the free
    /// license terms (attribution, no redistribution) before any public build (see AGENTS.md).
    /// </summary>
    public sealed class PredatorPlantSheetImporter : AssetPostprocessor
    {
        public const int FrameSize = 64;
        public const int Rows = 4; // front, back, left, right
        const string Root = "Assets/Art/Mobs/PredatorPlants/";

        static bool IsPlantSheet(string path) =>
            path.Replace('\\', '/').StartsWith(Root) && path.EndsWith("_full.png");

        void OnPreprocessTexture()
        {
            if (!IsPlantSheet(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = FrameSize; // 1 frame = 1 world unit
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
        }
    }

    public static class PredatorPlantBaker
    {
        const string Root = "Assets/Art/Mobs/PredatorPlants/";
        const string LibraryDir = "Assets/Resources/Mobs";
        const string LibraryPath = LibraryDir + "/MobAnimationLibrary.asset";

        static readonly string[] Plants = { "Plant1", "Plant2", "Plant3" };
        static readonly string[] States = { "Idle", "Walk", "Run", "Attack", "Hurt", "Death" };

        [MenuItem("Foundation/Predator Plants/Slice + Bake Animation Library")]
        public static void SliceAndBake()
        {
            int sliced = 0;
            foreach (var plant in Plants)
                foreach (var state in States)
                {
                    string path = $"{Root}{plant}/{state}/{plant}_{state}_full.png";
                    if (SliceSheet(path, plant, state)) sliced++;
                }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BakeLibrary();
            Debug.Log($"[PredatorPlants] Sliced {sliced} sheet(s) and baked {LibraryPath}.");
        }

        static bool SliceSheet(string path, string plant, string state)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (tex == null || importer == null)
            {
                Debug.LogWarning($"[PredatorPlants] Missing sheet: {path}");
                return false;
            }

            int fs = PredatorPlantSheetImporter.FrameSize;
            int cols = Mathf.Max(1, tex.width / fs);
            int rows = Mathf.Max(1, tex.height / fs);

            var metas = new List<SpriteMetaData>();
            // Texture origin is bottom-left; sheet row 0 (front) is the TOP row.
            for (int row = 0; row < rows; row++)
            {
                int dir = row; // 0 front, 1 back, 2 left, 3 right (top -> bottom)
                int yTop = tex.height - (row + 1) * fs;
                for (int col = 0; col < cols; col++)
                {
                    metas.Add(new SpriteMetaData
                    {
                        name = $"{plant}_{state}_{dir}_{col}",
                        rect = new Rect(col * fs, yTop, fs, fs),
                        alignment = (int)SpriteAlignment.BottomCenter,
                        pivot = new Vector2(0.5f, 0f),
                    });
                }
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 0618
            importer.spritesheet = metas.ToArray();
#pragma warning restore 0618
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return true;
        }

        [MenuItem("Foundation/Predator Plants/Bake Animation Library (no re-slice)")]
        public static void BakeLibrary()
        {
            Directory.CreateDirectory(LibraryDir);
            var lib = AssetDatabase.LoadAssetAtPath<MobAnimationLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<MobAnimationLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            var entries = new List<MobAnimEntry>();
            for (int i = 0; i < Plants.Length; i++)
            {
                var entry = new MobAnimEntry { mobId = Plants[i], secondsPerFrame = 0.15f };
                foreach (var state in States)
                {
                    string path = $"{Root}{Plants[i]}/{state}/{Plants[i]}_{state}_full.png";
                    var frames = entry.State(ToState(state));
                    LoadDirections(path, Plants[i], state, frames);
                }
                entries.Add(entry);
            }

            lib.entries = entries.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void LoadDirections(string path, string plant, string state, MobDirectionFrames dst)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            var byDir = new Dictionary<int, List<(int frame, Sprite sprite)>>();
            foreach (var obj in all)
            {
                if (obj is not Sprite sp) continue;
                // name = Plant_State_dir_frame
                var parts = sp.name.Split('_');
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[^1], out int frame)) continue;
                if (!int.TryParse(parts[^2], out int dir)) continue;
                if (!byDir.TryGetValue(dir, out var list)) { list = new(); byDir[dir] = list; }
                list.Add((frame, sp));
            }

            dst.front = OrderDir(byDir, 0);
            dst.back = OrderDir(byDir, 1);
            dst.left = OrderDir(byDir, 2);
            dst.right = OrderDir(byDir, 3);
        }

        static Sprite[] OrderDir(Dictionary<int, List<(int frame, Sprite sprite)>> byDir, int dir)
        {
            if (!byDir.TryGetValue(dir, out var list)) return System.Array.Empty<Sprite>();
            list.Sort((a, b) => a.frame.CompareTo(b.frame));
            var arr = new Sprite[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list[i].sprite;
            return arr;
        }

        static MobAnimState ToState(string state) => state switch
        {
            "Walk" => MobAnimState.Walk,
            "Run" => MobAnimState.Run,
            "Attack" => MobAnimState.Attack,
            "Hurt" => MobAnimState.Hurt,
            "Death" => MobAnimState.Death,
            _ => MobAnimState.Idle,
        };
    }
}
#endif
