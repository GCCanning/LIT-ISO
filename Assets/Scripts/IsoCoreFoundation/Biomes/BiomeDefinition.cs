using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    [Serializable]
    public struct BiomeNodeSpawn
    {
        public ResourceNodeDefinition node;
        [Range(0f, 1f)] public float chancePerCell;
    }

    [Serializable]
    public struct BiomeMobSpawn
    {
        public MobDefinition mob;
        public float weight;
    }

    [Serializable]
    public class BiomePropProfile
    {
        [Header("Substrate")]
        public TerrainSubstrate vegetationSubstrates = TerrainSubstrate.Organic;
        public TerrainSubstrate geologySubstrates = TerrainSubstrate.Natural;

        [Header("Elevation")]
        public int maxTreeHeight = 2;
        public int maxGroundCoverHeight = 2;
        public int minGeologyHeight = 0;
        public int maxGeologyHeight = 7;

        [Header("Natural grouping")]
        public float treePatchFrequency = 0.05f;
        [Range(0f, 1f)] public float treePatchThreshold = 0.52f;
        public float groundCoverPatchFrequency = 0.10f;
        [Range(0f, 1f)] public float groundCoverPatchThreshold = 0.46f;
        public float geologyPatchFrequency = 0.08f;
        [Range(0f, 1f)] public float geologyPatchThreshold = 0.64f;

        [Header("Site modifiers")]
        [Min(1f)] public float nearWaterVegetationMultiplier = 1f;
        [Range(0, 3)] public int blockingPropEdgeClearance = 1;
    }

    /// <summary>One weighted tile entry in a surface base/accent pool (Phase 1 B2/B3).</summary>
    [Serializable]
    public struct BiomeTilePoolEntry
    {
        public string tileId;
        public float weight;

        public BiomeTilePoolEntry(string tileId, float weight)
        {
            this.tileId = tileId;
            this.weight = weight;
        }
    }

    /// <summary>Accent roll-chance lerp across a border band (Phase 1 B3).</summary>
    [Serializable]
    public struct BiomeAccentRamp
    {
        [Range(0f, 1f)] public float ownAccentWeightAtNear;
        [Range(0f, 1f)] public float ownAccentWeightAtFar;
        [Range(0f, 1f)] public float otherAccentWeightAtNear;
        [Range(0f, 1f)] public float otherAccentWeightAtFar;
    }

    /// <summary>Feature/grove (or rock-prop for mountain) density lerp across a border
    /// band (Phase 1 B4). For mountain's transitionTo entries this carries
    /// rockPropDensityAtNear/AtFar in the same two fields.</summary>
    [Serializable]
    public struct BiomeFeatureDensityRamp
    {
        public float densityAtNear;
        public float densityAtFar;
    }

    /// <summary>One transitionTo.&lt;otherBiome&gt; entry (Phase 1 B1-B4): the border
    /// blend band toward a specific neighbour biome.</summary>
    [Serializable]
    public struct BiomeTransition
    {
        public string otherBiomeId;

        [Tooltip("[minCells, maxCells] - band width in cells of lateral distance to the " +
                 "other biome. Index 0 = fully 'own' (t=1), index 1 = the border (t=0).")]
        public int cellsMin;
        public int cellsMax;

        public BiomeAccentRamp accentRamp;
        public BiomeFeatureDensityRamp featureDensityRamp;

        [Tooltip("Optional own-side 'thaw'/transition accent tiles that take priority " +
                 "over the normal surfaceAccents pool when otherAccentW rolls near the " +
                 "far edge of the band (snow.json's transitionTo.meadow/forest only).")]
        public string[] thawTiles;
    }

    /// <summary>
    /// Top tier of the terrain model: maps a climate point (temperature, moisture)
    /// to a surface block group plus resource-node and mob spawn rules.
    /// </summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Biome", fileName = "Biome")]
    public class BiomeDefinition : FoundationDefinition
    {
        [Header("Climate centre (0..1) — nearest-centroid FALLBACK only (A3)")]
        [Range(0f, 1f)] public float temperature = 0.5f;
        [Range(0f, 1f)] public float moisture = 0.5f;

        [Header("Climate table rectangle (A3) — primary SelectBiome lookup")]
        [Tooltip("[tMin, tMax]. SelectBiome(t,m) matches when t is inside this range " +
                 "AND m is inside moistureRange. Mirrors <biome>.json climate.temperatureRange.")]
        public Vector2 temperatureRange = new Vector2(0f, 1f);
        [Tooltip("[mMin, mMax]. Mirrors <biome>.json climate.moistureRange.")]
        public Vector2 moistureRange = new Vector2(0f, 1f);
        [Tooltip("[eMin, eMax]. Mirrors biome_suite.json climate.eMin/eMax. Cell elevation (0..1) " +
                 "must fall inside this band for the climate-rectangle to match.")]
        public Vector2 elevationRange = new Vector2(0f, 1f);
        [Tooltip("Tiebreaker when 2+ climate rectangles match the same (t,m): highest " +
                 "priority wins. beach/mountain use -1 (never win via the climate table; " +
                 "they're gated separately by SampleContinent).")]
        public int climatePriority = 0;

        [Header("Elevation gating / lapse rate (A4)")]
        [Tooltip("effectiveTemp = temp - lapseRatePerHeightStep * height, applied BEFORE " +
                 "SelectBiome. 0 = no lapse (snow/beach).")]
        public float lapseRatePerHeightStep = 0f;
        [Tooltip("-1 = not elevation-gated. mountain = 3: any cell whose height tier is " +
                 ">= this value becomes this biome regardless of climate.")]
        public int elevationGateMinHeight = -1;

        [Header("Surface")]
        public BlockGroupDefinition surfaceGroup;

        [Header("Phase 1 procedural blending (B1-B4)")]
        [Tooltip("Weighted base-pool tile ids for SurfaceVariant's B2 crossfade. Empty = " +
                 "fall back to surfaceGroup's variant pool as today.")]
        /// <summary>Per-height-tier tile pools (index = cell height 0-7). When a slot is non-null it overrides surfaceBasePool for that height.
        /// Set in code only — Unity cannot serialize a jagged array.</summary>
        [NonSerialized] public BiomeTilePoolEntry[][] surfaceBands;
        public BiomeTilePoolEntry[] surfaceBasePool;
        [Tooltip("Weighted accent tile ids rolled at accentRate (B3).")]
        public BiomeTilePoolEntry[] surfaceAccents;
        [Range(0f, 1f)] public float accentRate = 0f;
        [Tooltip("Per-neighbour-biome border blend bands. Empty = no blending toward that biome.")]
        public BiomeTransition[] transitions;

        public BiomeTransition? FindTransition(string otherBiomeId)
        {
            if (transitions == null) return null;
            for (int i = 0; i < transitions.Length; i++)
                if (transitions[i].otherBiomeId == otherBiomeId) return transitions[i];
            return null;
        }

        /// <summary>Weighted pick from a BiomeTilePoolEntry[] using a 0..1 roll.</summary>
        public static string PickWeighted(BiomeTilePoolEntry[] pool, float roll01, string fallback)
        {
            if (pool == null || pool.Length == 0) return fallback;
            float total = 0f;
            for (int i = 0; i < pool.Length; i++) total += Mathf.Max(0f, pool[i].weight);
            if (total <= 0f) return fallback;
            float target = roll01 * total;
            float acc = 0f;
            for (int i = 0; i < pool.Length; i++)
            {
                acc += Mathf.Max(0f, pool[i].weight);
                if (target < acc) return pool[i].tileId;
            }
            return pool[pool.Length - 1].tileId;
        }

        [Header("Height column")]
        public int baseHeight = 1;
        public int heightVariance = 2;

        [Header("Spawn rules")]
        public BiomeNodeSpawn[] nodes;
        public BiomeMobSpawn[] mobs;
        [Tooltip("Substrate, elevation, clustering, and edge rules evaluated before node chance.")]
        public BiomePropProfile propProfile = new();

        [Header("Debug")]
        public Color debugTint = Color.white;

        public float ClimateDistance(float t, float m)
        {
            float dt = t - temperature, dm = m - moisture;
            return dt * dt + dm * dm;
        }

        /// <summary>True when a climate point (t, m, e) all fall inside this biome's
        /// climate rectangle. Primary SelectBiome test when the biome suite is applied;
        /// climatePriority breaks ties between overlapping rectangles.</summary>
        public bool MatchesClimate(float t, float m, float e) =>
            t >= temperatureRange.x && t <= temperatureRange.y &&
            m >= moistureRange.x && m <= moistureRange.y &&
            e >= elevationRange.x && e <= elevationRange.y;
    }

    public class BiomeDatabase : Database<BiomeDefinition> { }
}
