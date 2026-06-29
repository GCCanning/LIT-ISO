using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Loads <c>StreamingAssets/worldgen/biome_suite.json</c> and applies it onto the
    /// code-built biome database, making the JSON the SINGLE source of truth for the
    /// climate-selectable biome roster: each listed biome's climate rectangle
    /// (t/m/e), its climate priority (overlap tiebreak), and its mob spawn list.
    ///
    /// Rendering (tile pools, decoration nodes) is left as authored in FoundationContent —
    /// only biome SELECTION + mobs are driven from the file in this pass.
    ///
    /// Safe by design: if the file is missing or unparseable, nothing is changed and
    /// SelectBiome keeps its original nearest-centroid behaviour (see IsoTerrainSampler).
    ///
    /// JSON is parsed with Unity's JsonUtility (no Newtonsoft dependency). The biome map
    /// is a JSON object keyed by id, which JsonUtility cannot map directly, so the per-biome
    /// objects are sliced out by a small brace matcher and each is parsed individually.
    /// </summary>
    public static class BiomeSuiteLoader
    {
        // JSON biome id -> existing code biome id (the suite calls the starter biome
        // "plains"; the code biome is "meadow"). Add aliases here as the file evolves.
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "plains", "meadow" },
        };

        public const string RelPath = "worldgen/biome_suite.json";

        [Serializable] class RootJson { public string fallbackBiome; public string[] priority; }
        [Serializable] class ClimateJson { public float tMin, tMax = 1f, mMin, mMax = 1f, eMin, eMax = 1f; }
        [Serializable] class MobJson { public string enemyId; public float weight = 1f; public float spawnChance = 0.01f; public int minHeight; public int maxHeight; }
        [Serializable] class TilesJson { public string flatGround; public string[] flatGroundVariants; public string raisedGround; public string[] raisedGroundVariants; public string midElevation; public string peak; }
        [Serializable] class BiomeJson { public ClimateJson climate; public TilesJson tiles; public MobJson[] mobs; }

        const float MainTileWeight = 100f;
        const float VariantTileWeight = 30f;

        /// <summary>Applies the suite. Returns the resolved fallback biome id (already
        /// alias-mapped to a code biome). On any failure returns "meadow" and leaves the
        /// database untouched; <paramref name="applied"/> reports whether boxes were set.</summary>
        public static string Apply(BiomeDatabase biomes, MobDatabase mobs, BlockDatabase blocks, out bool applied)
        {
            applied = false;
            string fallback = "meadow";
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, RelPath);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[BiomeSuite] {RelPath} not found at {path} — keeping code-defined biomes.");
                    return fallback;
                }
                string json = File.ReadAllText(path);

                var root = JsonUtility.FromJson<RootJson>(json);
                var priority = (root != null && root.priority != null) ? root.priority : Array.Empty<string>();
                if (priority.Length == 0)
                {
                    Debug.LogWarning("[BiomeSuite] no 'priority' list — keeping code-defined biomes.");
                    return fallback;
                }
                if (root != null && !string.IsNullOrEmpty(root.fallbackBiome))
                    fallback = Resolve(root.fallbackBiome);

                var objects = ExtractBiomeObjects(json);

                // Build the set of code biome ids that ARE in the roster (alias-resolved).
                var roster = new HashSet<string>();
                foreach (var jid in priority) roster.Add(Resolve(jid));

                // Pass 1: any biome NOT in the roster is excluded from climate selection
                // (beach/mountain stay structural; desert is dropped). climatePriority < 0
                // means "never win via the climate table".
                foreach (var b in biomes.All)
                    if (!roster.Contains(b.id)) b.climatePriority = -1;

                // Pass 2: apply each roster biome's rectangle, priority and mobs.
                int n = priority.Length;
                int appliedCount = 0;
                for (int i = 0; i < n; i++)
                {
                    string jid = priority[i];
                    string codeId = Resolve(jid);
                    var biome = biomes.Get(codeId);
                    if (biome == null)
                    {
                        Debug.LogWarning($"[BiomeSuite] roster biome '{jid}' (-> '{codeId}') has no code BiomeDefinition — skipped.");
                        continue;
                    }
                    if (!objects.TryGetValue(jid, out var objJson)) continue;
                    var bj = JsonUtility.FromJson<BiomeJson>(objJson);
                    if (bj == null) continue;

                    if (bj.climate != null)
                    {
                        biome.temperatureRange = new Vector2(bj.climate.tMin, bj.climate.tMax);
                        biome.moistureRange    = new Vector2(bj.climate.mMin, bj.climate.mMax);
                        biome.elevationRange   = new Vector2(bj.climate.eMin, bj.climate.eMax);
                    }
                    // Earlier in the priority list == higher precedence on overlap.
                    biome.climatePriority = n - i;

                    if (bj.mobs != null)
                    {
                        var list = new List<BiomeMobSpawn>(bj.mobs.Length);
                        foreach (var mj in bj.mobs)
                        {
                            if (mj == null || string.IsNullOrEmpty(mj.enemyId)) continue;
                            var mob = mobs.Get(mj.enemyId);
                            if (mob == null)
                            {
                                Debug.LogWarning($"[BiomeSuite] biome '{jid}' references unknown mob '{mj.enemyId}' — skipped.");
                                continue;
                            }
                            list.Add(new BiomeMobSpawn { mob = mob, weight = Mathf.Max(0.0001f, mj.weight) });
                        }
                        biome.mobs = list.ToArray();
                    }

                    // Tiles: register a walkable block per referenced PixelArt tile (sprite
                    // loads from Resources/Tiles/<blockId>), then drive the biome's surface
                    // pools by elevation band. Replaces the code-authored pools so ONLY the
                    // suite's tiles render for this biome.
                    if (bj.tiles != null)
                        ApplyTiles(biome, jid, bj.tiles, blocks);

                    appliedCount++;
                }

                applied = appliedCount > 0;
                Debug.Log($"[BiomeSuite] applied {appliedCount}/{n} biomes from {RelPath}; fallback='{fallback}'.");
                return fallback;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BiomeSuite] failed to apply {RelPath}: {e.Message} — keeping code-defined biomes.");
                applied = false;
                return fallback;
            }
        }

        static string Resolve(string jsonId) =>
            (jsonId != null && Alias.TryGetValue(jsonId, out var code)) ? code : jsonId;

        /// <summary>Registers blocks for the biome's tiles and sets its per-height surface
        /// pools (flat → h0/1, raised → h2/3, mid → h4/5, peak → h6/7). Empty bands inherit
        /// the next-lower band so partial tile sets still render.</summary>
        static void ApplyTiles(BiomeDefinition biome, string jid, TilesJson t, BlockDatabase blocks)
        {
            var flat   = Band(blocks, jid, t.flatGround,   t.flatGroundVariants);
            var raised = Band(blocks, jid, t.raisedGround, t.raisedGroundVariants);
            var mid    = Band(blocks, jid, t.midElevation, null);
            var peak   = Band(blocks, jid, t.peak,         null);
            if (flat.Length == 0) return;                 // nothing usable — keep code pools
            if (raised.Length == 0) raised = flat;
            if (mid.Length == 0)    mid = raised;
            if (peak.Length == 0)   peak = mid;

            biome.surfaceBands = new BiomeTilePoolEntry[8][];
            biome.surfaceBands[0] = biome.surfaceBands[1] = flat;
            biome.surfaceBands[2] = biome.surfaceBands[3] = raised;
            biome.surfaceBands[4] = biome.surfaceBands[5] = mid;
            biome.surfaceBands[6] = biome.surfaceBands[7] = peak;
            biome.surfaceBasePool = flat;
            biome.surfaceAccents = null;   // only the suite's tiles render
            biome.accentRate = 0f;
        }

        static BiomeTilePoolEntry[] Band(BlockDatabase blocks, string jid, string main, string[] variants)
        {
            var list = new List<BiomeTilePoolEntry>();
            if (!string.IsNullOrEmpty(main))
            {
                RegisterTile(blocks, jid, main);
                list.Add(new BiomeTilePoolEntry(BlockId(jid, main), MainTileWeight));
            }
            if (variants != null)
                foreach (var v in variants)
                    if (!string.IsNullOrEmpty(v))
                    {
                        RegisterTile(blocks, jid, v);
                        list.Add(new BiomeTilePoolEntry(BlockId(jid, v), VariantTileWeight));
                    }
            return list.ToArray();
        }

        static void RegisterTile(BlockDatabase blocks, string jid, string path)
        {
            string id = BlockId(jid, path);
            if (blocks.Has(id)) return;
            var blk = ScriptableObject.CreateInstance<BlockDefinition>();
            blk.id = id; blk.name = id; blk.displayName = id;
            blk.collision = CollisionMode.Walkable;
            blocks.Add(blk);
        }

        /// <summary>Deterministic block id for a biome tile. MUST match the import script
        /// (scratchpad/import_biome_tiles.py): "bt_&lt;jsonBiomeId&gt;_&lt;fileBasename&gt;",
        /// non-alphanumerics replaced with '_'. The PNG lives at Resources/Tiles/&lt;id&gt;.</summary>
        static string BlockId(string biomeJsonId, string path)
        {
            string baseName = Path.GetFileNameWithoutExtension(path);
            var sb = new StringBuilder("bt_").Append(biomeJsonId).Append('_').Append(baseName);
            for (int i = 0; i < sb.Length; i++)
            {
                char ch = sb[i];
                if (!(char.IsLetterOrDigit(ch) || ch == '_')) sb[i] = '_';
            }
            return sb.ToString();
        }

        /// <summary>Slices the top-level properties of the "biomes" object into
        /// id -> object-json. Brace-counts only (the file's string values never contain
        /// braces), which is sufficient for this controlled, generated file.</summary>
        static Dictionary<string, string> ExtractBiomeObjects(string json)
        {
            var result = new Dictionary<string, string>();
            int key = json.IndexOf("\"biomes\"", StringComparison.Ordinal);
            if (key < 0) return result;
            int open = json.IndexOf('{', key);
            if (open < 0) return result;

            int i = open + 1;
            while (i < json.Length)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) break;
                if (json[i] == '}') break;            // end of biomes object
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '"') { i++; continue; }

                int ks = i + 1;
                int ke = json.IndexOf('"', ks);
                if (ke < 0) break;
                string biomeId = json.Substring(ks, ke - ks);
                i = ke + 1;

                while (i < json.Length && json[i] != ':') i++;
                i++;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length || json[i] != '{') continue;

                int objStart = i, depth = 0;
                for (; i < json.Length; i++)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') { depth--; if (depth == 0) { i++; break; } }
                }
                result[biomeId] = json.Substring(objStart, i - objStart);
            }
            return result;
        }
    }
}
