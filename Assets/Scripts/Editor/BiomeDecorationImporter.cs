using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One-click pipeline that turns the hand-dropped decoration PNGs in
/// Assets/Art/BiomeDecorations/ into properly-configured Unity Tiles and wires
/// them into the matching biome definitions.
///
/// What it does:
///   1. Group A (nature/prop pixel art) — sets correct sprite import settings
///      (Point filter, uncompressed, no mips, Full-Rect mesh, bottom pivot,
///      PPU 64 to match existing decorations), creates a Tile asset for each,
///      and appends it to the target biome(s)' decorationTiles[] (idempotent).
///   2. Group B (pixel buildings) — moved to _Towns/ for the future town generator.
///   3. Group C (512px high-detail "_4x") — moved to _HighDetail_Unused/ (style mismatch).
///
/// Run: Tools > LIT-ISO > World > Import Biome Decorations
/// Safe to run multiple times — tiles are reused, biome lists are de-duplicated.
/// </summary>
public static class BiomeDecorationImporter
{
    private const string DropFolder = "Assets/Art/BiomeDecorations";
    private const string TileFolder = "Assets/Tilemaps/Isometric/Tiles/BiomeDecorations";
    private const string TownsFolder = "Assets/Art/BiomeDecorations/_Towns";
    private const string UnusedFolder = "Assets/Art/BiomeDecorations/_HighDetail_Unused";
    private const string BiomeFolder = "Assets/World/Biomes";

    /// <summary>Maps a dropped PNG → clean tile name → pixels-per-unit (size) → biomes.</summary>
    private struct DecoMap
    {
        public string sourceFile;       // exact filename in the drop folder
        public string cleanTileName;    // resulting Tile asset name
        public int ppu;                 // pixels-per-unit — controls world size of the prop
        public BiomeKind[] biomes;      // which biomes get this decoration

        public DecoMap(string sourceFile, string cleanTileName, int ppu, params BiomeKind[] biomes)
        {
            this.sourceFile = sourceFile;
            this.cleanTileName = cleanTileName;
            this.ppu = ppu;
            this.biomes = biomes;
        }
    }

    // Source sprites are 128px. World cell = 1 unit wide, so:
    //   PPU 128 → 1.0 tile wide (fits a single tile)
    //   PPU 256 → 0.5 tile wide (small ground detail)
    //   PPU  64 → 2.0 tiles wide (long props like logs)
    private static readonly DecoMap[] GroupA =
    {
        // Plains — the overworld variety set
        new DecoMap("isometric_oak_tree (1).png",  "Deco_OakTree_A",  80, BiomeKind.Plains), // tall ~1.6 tiles, canopies merge in groves
        new DecoMap("isometric_oak_tree (2).png",  "Deco_OakTree_B",  80, BiomeKind.Plains),
        new DecoMap("isometric_bush (1).png",      "Deco_Bush_A",    180, BiomeKind.Plains), // ~0.7 tile
        new DecoMap("isometric_bush (2).png",      "Deco_Bush_B",    180, BiomeKind.Plains),
        new DecoMap("isometric_flower.png",        "Deco_Flower",    300, BiomeKind.Plains), // small ~0.43 tile
        new DecoMap("isometric_log.png",           "Deco_Log",        56, BiomeKind.Plains), // long, spans ~2.3 tiles
        new DecoMap("isometric_stump.png",         "Deco_Stump",     160, BiomeKind.Plains), // ~0.8 tile
        new DecoMap("isometric_pixel_art_wheat_crop__golden_stalks__swaying_gently_in_the_wind__32x32_style__no_ground__n.png",
                                                   "Deco_Wheat",     185, BiomeKind.Plains), // ~0.7 tile

        // Pine + rocks span multiple biomes
        new DecoMap("isometric_pine_tree (1).png", "Deco_PineTree",   85, BiomeKind.FrozenMountain, BiomeKind.Plains), // tall, slightly narrower than oak
        new DecoMap("isometric_rock.png",          "Deco_Rock_A",    140, BiomeKind.Plains, BiomeKind.Desert, BiomeKind.FrozenMountain), // ~0.9 tile
        new DecoMap("isometric_gray_rock (1).png", "Deco_Rock_B",    140, BiomeKind.Plains, BiomeKind.Desert, BiomeKind.FrozenMountain),

        // Temple / ruins props
        new DecoMap("isometric_barrel__wooden__pixel_art__transparent_background__no_ground__no_base.png",
                                                   "Deco_Barrel",    170, BiomeKind.Temple), // ~0.75 tile
        new DecoMap("isometric_wooden_loot_chest__iron_bands__pixel_art__transparent_background__no_ground__no_base.png",
                                                   "Deco_Chest",     160, BiomeKind.Temple),
        new DecoMap("isometric_ruined_stone_wall__mossy__pixel_art__transparent_background__no_ground__no_base.png",
                                                   "Deco_RuinedWall",115, BiomeKind.Temple), // ~1.1 tile
        new DecoMap("isometric_stone_pillar__ruined__pixel_art__transparent_background__no_ground__no_base.png",
                                                   "Deco_Pillar",    140, BiomeKind.Temple),
    };

    [MenuItem("Tools/LIT-ISO/World/Import Biome Decorations", false, 110)]
    public static void ImportDecorations()
    {
        RunImport(showDialog: true);
    }

    /// <summary>
    /// Core import. Call with showDialog:false from automated setup (golden path)
    /// so it runs silently. Returns the number of biomes that received decorations.
    /// Idempotent — safe to run on every scene rebuild.
    /// </summary>
    public static int RunImport(bool showDialog)
    {
        EnsureFolder(TileFolder);

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Biome Decoration Import ===");

        // 1. Build tiles from Group A and collect per-biome additions.
        Dictionary<BiomeKind, List<TileBase>> biomeAdditions = new Dictionary<BiomeKind, List<TileBase>>();
        int tilesCreated = 0, tilesReused = 0, missing = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (DecoMap map in GroupA)
            {
                string pngPath = $"{DropFolder}/{map.sourceFile}";
                if (!File.Exists(pngPath))
                {
                    report.AppendLine($"  ⚠ MISSING: {map.sourceFile}");
                    missing++;
                    continue;
                }

                ConfigureSpriteImport(pngPath, map.ppu);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                if (sprite == null)
                {
                    report.AppendLine($"  ⚠ No sprite loaded for {map.sourceFile} (reimport may be pending)");
                    missing++;
                    continue;
                }

                string tilePath = $"{TileFolder}/{map.cleanTileName}.asset";
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, tilePath);
                    tilesCreated++;
                }
                else
                {
                    tile.sprite = sprite;
                    EditorUtility.SetDirty(tile);
                    tilesReused++;
                }

                foreach (BiomeKind kind in map.biomes)
                {
                    if (!biomeAdditions.TryGetValue(kind, out List<TileBase> list))
                    {
                        list = new List<TileBase>();
                        biomeAdditions[kind] = list;
                    }
                    list.Add(tile);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // 2. Wire tiles into biome decorationTiles[] (idempotent).
        foreach (var kvp in biomeAdditions)
        {
            IsoBiomeDefinition biome = LoadBiome(kvp.Key);
            if (biome == null)
            {
                report.AppendLine($"  ⚠ Biome asset not found for {kvp.Key}");
                continue;
            }

            List<TileBase> deco = new List<TileBase>();
            if (biome.decorationTiles != null) deco.AddRange(biome.decorationTiles);

            int added = 0;
            foreach (TileBase t in kvp.Value)
            {
                if (!deco.Contains(t))
                {
                    deco.Add(t);
                    added++;
                }
            }

            biome.decorationTiles = deco.ToArray();
            // Ensure decorations actually spawn (give a sensible floor density).
            if (biome.baseDecorationDensity < 0.03f) biome.baseDecorationDensity = 0.05f;
            if (biome.decorationChance < 0.03f) biome.decorationChance = 0.05f;
            EditorUtility.SetDirty(biome);
            report.AppendLine($"  ✓ {kvp.Key}: +{added} decorations (total {deco.Count})");
        }

        // 3. Tidy Group B (buildings) and Group C (_4x high-detail) out of the way.
        int movedTowns = MoveMatching(
            UnusedOrTowns.Towns,
            new[] { "cottage", "tavern", "blacksmith", "well", "market", "guard_tower" },
            TownsFolder, report);
        int movedUnused = MoveMatching(
            UnusedOrTowns.Unused,
            new[] { "_4x" },
            UnusedFolder, report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine($"\nTiles created: {tilesCreated}, reused: {tilesReused}, missing: {missing}");
        report.AppendLine($"Buildings moved to _Towns: {movedTowns}");
        report.AppendLine($"High-detail moved to _HighDetail_Unused: {movedUnused}");
        Debug.Log(report.ToString());

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Biome Decorations Imported",
                $"Created {tilesCreated} new tiles ({tilesReused} updated).\n\n" +
                $"Wired into biomes: {biomeAdditions.Count}.\n" +
                $"Buildings parked in _Towns: {movedTowns}.\n" +
                $"High-detail parked in _HighDetail_Unused: {movedUnused}.\n\n" +
                "Regenerate or replay the scene to see decorations populate the biomes.",
                "OK");
        }
        return biomeAdditions.Count;
    }

    private enum UnusedOrTowns { Towns, Unused }

    private static int MoveMatching(UnusedOrTowns _, string[] keywords, string destFolder, System.Text.StringBuilder report)
    {
        EnsureFolder(destFolder);
        int moved = 0;
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DropFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Don't touch files already inside a subfolder.
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            if (dir != DropFolder) continue;

            string file = Path.GetFileName(path);
            string lower = file.ToLowerInvariant();
            bool match = false;
            foreach (string kw in keywords)
            {
                if (lower.Contains(kw)) { match = true; break; }
            }
            if (!match) continue;

            string dest = $"{destFolder}/{file}";
            string error = AssetDatabase.MoveAsset(path, dest);
            if (string.IsNullOrEmpty(error)) { moved++; }
            else report.AppendLine($"  ⚠ Could not move {file}: {error}");
        }
        return moved;
    }

    private static void ConfigureSpriteImport(string pngPath, int ppu)
    {
        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = ppu;
        importer.alphaIsTransparency = true;

        TextureImporterSettings s = new TextureImporterSettings();
        importer.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteExtrude = 1;
        s.spriteAlignment = (int)SpriteAlignment.BottomCenter; // props stand on the cell
        importer.SetTextureSettings(s);

        importer.SaveAndReimport();
    }

    private static IsoBiomeDefinition LoadBiome(BiomeKind kind)
    {
        // Biome assets follow BiomeDefinition_<Name>.asset naming.
        string name = kind switch
        {
            BiomeKind.Plains => "BiomeDefinition_Plains",
            BiomeKind.Desert => "BiomeDefinition_Desert",
            BiomeKind.FrozenMountain => "BiomeDefinition_FrozenMountain",
            BiomeKind.FrozenCave => "BiomeDefinition_FrozenCave",
            BiomeKind.Temple => "BiomeDefinition_Temple",
            BiomeKind.Basic => "BiomeDefinition_Basic",
            _ => null
        };
        if (name == null) return null;
        return AssetDatabase.LoadAssetAtPath<IsoBiomeDefinition>($"{BiomeFolder}/{name}.asset");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
