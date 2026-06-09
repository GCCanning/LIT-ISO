using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Tunables for the foundation. Plain serializable class with defaults.</summary>
    [Serializable]
    public class FoundationConfig
    {
        [Header("World")]
        public int seed = 1337;
        public int chunkSize = 12;
        public int viewRadiusChunks = 3;     // chunks streamed around the player (7x7) with a wide off-screen margin
        public int maxHeight = 4;
        public int spawnClearingRadius = 6;  // flat, mob-free safe start (cells)
        public int spawnHeight = 1;

        [Header("Flat prototype world")]
        [Tooltip("When true, the whole world is a single flat, water-free layer of one " +
                 "surface block (flatSurfaceBlockId) with no height, water, or resource nodes. " +
                 "Used while iterating on tile art / connectivity.")]
        public bool flatWorld = true;
        [Tooltip("Block id used for every surface cell when flatWorld is on.")]
        public string flatSurfaceBlockId = "grass_1";
        [Tooltip("Max terrain height for the grass world's rolling hills (0 = perfectly " +
                 "flat). The spawn clearing always stays flat for a safe start.")]
        public int flatWorldMaxHeight = 3;
        [Tooltip("Noise frequency for the grass world's hills. Lower = broader, gentler " +
                 "hills; higher = more frequent bumps.")]
        public float flatWorldHeightFrequency = 0.07f;
        [Tooltip("Scatter the meadow biome's grass variants (grass_1/2/3) across the " +
                 "surface. NOTE: the variant tiles have slightly different cube-cap heights, " +
                 "so enabling this makes same-height ground look bumpy. Off = perfectly " +
                 "flush, uniform ground.")]
        public bool flatWorldUseVariants = false;
        [Tooltip("Scatter the meadow biome's resource nodes (trees, rocks, bushes) across " +
                 "the grass world so it feels alive. The spawn clearing stays empty.")]
        public bool flatWorldDecorations = true;
        [Range(0f, 1f)]
        [Tooltip("Overall density multiplier for the grass world's scattered decorations.")]
        public float flatWorldDecorationDensity = 1f;

        [Header("Decoration clustering (groves, not random scatter)")]
        [Tooltip("Frequency of the forest-grove noise. Lower = larger, broader forests; " +
                 "higher = small copses.")]
        public float decoForestFrequency = 0.06f;
        [Range(0f, 1f)]
        [Tooltip("Forest-noise value above which a cell is 'inside a grove'. Higher = " +
                 "fewer, smaller forests.")]
        public float decoForestThreshold = 0.55f;
        [Range(0f, 1f)]
        [Tooltip("Tree spawn chance deep inside a grove (scaled down toward grove edges).")]
        public float decoTreeDensityInForest = 0.45f;
        [Range(0f, 1f)]
        [Tooltip("Bush spawn chance — light, even ground-cover scatter on open ground.")]
        public float decoBushChance = 0.04f;
        [Range(0f, 1f)]
        [Tooltip("Coarse-noise threshold for rock outcrops — rocks only appear in clumps.")]
        public float decoRockClusterThreshold = 0.72f;
        [Range(0f, 1f)]
        [Tooltip("Rock spawn chance inside a rock outcrop clump.")]
        public float decoRockChanceInCluster = 0.12f;

        [Header("Climate noise")]
        public float climateFrequency = 0.02f;
        public float heightFrequency = 0.06f;

        [Header("Rendering")]
        [Tooltip("Add a Pixel Perfect Camera so the pixel-art tiles stay crisp and the " +
                 "grid does not shimmer as the player moves.")]
        public bool pixelPerfect = true;

        [Header("Player")]
        public float moveSpeed = 2.8f;
        public float interactRange = 1.8f;

        [Header("Mobs")]
        public int mobCap = 8;
        public float mobSpawnInterval = 3f;
        public float mobSpawnRadius = 9f;
        public float mobDespawnRadius = 16f;

        [Header("Starter inventory (itemId : count)")]
        public List<ItemStack> starterItems = new()
        {
            new ItemStack("wood", 12),
            new ItemStack("stone", 8),
            new ItemStack("fiber", 6),
            new ItemStack("workbench_item", 1),
            new ItemStack("campfire_item", 2),
            new ItemStack("stone_block_item", 5),
            new ItemStack("hoe", 1),
            new ItemStack("carrot_seeds", 3),
            new ItemStack("wheat_seeds", 3),
        };
    }
}
