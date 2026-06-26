using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Loads <c>StreamingAssets/worldgen/biome_variants.json</c> at runtime and
    /// exposes stable per-province variant selection for <see cref="IsoTerrainSampler"/>.
    ///
    /// One variant is selected per Voronoi province using a seeded hash of the
    /// province grid cell <c>(gi, gj)</c> so the same province always produces the
    /// same variant regardless of order of traversal.
    /// </summary>
    public class BiomeVariantRuntime
    {
        // ─── public data types ──────────────────────────────────────────────────

        [Serializable]
        public class TileWeight
        {
            public string tile;
            public float w;
        }

        [Serializable]
        public class HeightBand
        {
            public int h;
            public TileWeight[] tiles;
        }

        [Serializable]
        public class VariantEntry
        {
            public string id;
            public string biomeId;
            public string name;
            public float accentRate;
            public TileWeight[] baseTiles;
            public TileWeight[] accentTiles;
            public HeightBand[] heightBands;
        }

        // JsonUtility root wrapper
        [Serializable]
        class Root
        {
            public VariantEntry[] variants;
        }

        // ─── internal index ─────────────────────────────────────────────────────

        readonly Dictionary<string, VariantEntry[]> _byBiome =
            new Dictionary<string, VariantEntry[]>(StringComparer.Ordinal);

        readonly uint _seedContrib;

        // ─── factory ────────────────────────────────────────────────────────────

        BiomeVariantRuntime(uint seedContrib) { _seedContrib = seedContrib; }

        /// <summary>
        /// Loads from <c>Application.streamingAssetsPath/worldgen/biome_variants.json</c>.
        /// Returns a no-op instance (all lookups return null) when the file is absent or
        /// malformed — the sampler falls back to its existing per-biome pool in that case.
        /// </summary>
        public static BiomeVariantRuntime Load(uint seedContrib)
        {
            var instance = new BiomeVariantRuntime(seedContrib);
            string path = Path.Combine(Application.streamingAssetsPath, "worldgen", "biome_variants.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[BiomeVariantRuntime] biome_variants.json not found at {path}");
                return instance;
            }

            Root root;
            try
            {
                root = JsonUtility.FromJson<Root>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BiomeVariantRuntime] Failed to parse biome_variants.json: {ex.Message}");
                return instance;
            }

            if (root?.variants == null)
                return instance;

            // Index by biomeId
            var tmp = new Dictionary<string, List<VariantEntry>>(StringComparer.Ordinal);
            foreach (var v in root.variants)
            {
                if (string.IsNullOrEmpty(v?.biomeId)) continue;
                if (!tmp.TryGetValue(v.biomeId, out var list))
                    tmp[v.biomeId] = list = new List<VariantEntry>();
                list.Add(v);
            }
            foreach (var kv in tmp)
                instance._byBiome[kv.Key] = kv.Value.ToArray();

            return instance;
        }

        // ─── public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the variant assigned to the province at grid cell <c>(gi, gj)</c>
        /// for the given biome, or <c>null</c> if no variants are registered for that biome.
        /// The selection is deterministic: same (gi, gj, biomeId) always returns the same entry.
        /// </summary>
        public VariantEntry GetVariantForProvince(string biomeId, int gi, int gj)
        {
            if (string.IsNullOrEmpty(biomeId) || !_byBiome.TryGetValue(biomeId, out var arr) || arr.Length == 0)
                return null;
            int idx = (int)(ProvinceHash(gi, gj) % (uint)arr.Length);
            return arr[idx];
        }

        /// <summary>Returns all registered variants for a biome (null if none).</summary>
        public VariantEntry[] GetVariantsForBiome(string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId)) return null;
            _byBiome.TryGetValue(biomeId, out var arr);
            return arr;
        }

        /// <summary>
        /// Picks a tile ID from a <see cref="TileWeight"/> pool using a per-cell hash.
        /// Falls back to <paramref name="fallback"/> when the pool is empty.
        /// </summary>
        public static string PickWeighted(TileWeight[] pool, float t, string fallback)
        {
            if (pool == null || pool.Length == 0) return fallback;
            float total = 0f;
            foreach (var e in pool) total += Mathf.Max(0f, e.w);
            if (total <= 0f) return fallback;
            float cursor = t * total;
            foreach (var e in pool)
            {
                cursor -= Mathf.Max(0f, e.w);
                if (cursor <= 0f) return e.tile ?? fallback;
            }
            return pool[pool.Length - 1].tile ?? fallback;
        }

        /// <summary>
        /// For a variant with heightBands, returns the tile for the given height using
        /// the band whose <c>h</c> field is the highest threshold ≤ <paramref name="height"/>.
        /// Falls back to baseTiles then <paramref name="fallback"/>.
        /// </summary>
        public static string PickHeightBand(VariantEntry v, int height, float t, string fallback)
        {
            if (v == null) return fallback;
            if (v.heightBands != null && v.heightBands.Length > 0)
            {
                TileWeight[] best = null;
                int bestH = -1;
                foreach (var band in v.heightBands)
                {
                    if (band.h <= height && band.h > bestH)
                    {
                        best = band.tiles;
                        bestH = band.h;
                    }
                }
                if (best != null)
                    return PickWeighted(best, t, fallback);
            }
            return PickWeighted(v.baseTiles, t, fallback);
        }

        // ─── internal helpers ───────────────────────────────────────────────────

        uint ProvinceHash(int gi, int gj)
        {
            unchecked
            {
                uint h = (uint)(gi * 73856093) ^ (uint)(gj * 19349663) ^ _seedContrib;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return h;
            }
        }
    }
}
