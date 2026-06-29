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
        public int maxHeight = 7;
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

        [Header("Continent world (non-flat)")]
        [Tooltip("When true (and flatWorld is off), the terrain sampler generates a " +
                 "continent: oceans, beaches, moisture/temperature biome regions, " +
                 "multi-step cliffs, and winding rivers — all as one deterministic " +
                 "per-cell function. When false, the legacy simple non-flat path is used.")]
        public bool continentWorld = true;
        [Tooltip("Frequency of the base landmass elevation noise. Lower = larger " +
                 "continents and oceans; higher = broken, islandy terrain.")]
        public float continentFrequency = 0.0045f;
        [Tooltip("Elevation below this is deep ocean (water).")]
        [Range(0f, 1f)] public float continentDeepLevel = 0.34f;
        [Tooltip("Elevation below this (but above deep) is shallow coastal water.")]
        [Range(0f, 1f)] public float continentShoreLevel = 0.42f;
        [Tooltip("Land below this elevation renders as a beach (sand) ring at the coast.")]
        [Range(0f, 1f)] public float continentBeachLevel = 0.46f;
        [Tooltip("Land elevation thresholds at which the height column steps up one level " +
                 "(stacked dirt body + surface cap). Six thresholds = up to h7. " +
                 "Tiers 5-7 only activate when maxHeight >= their target height.")]
        [Range(0f, 1f)] public float continentTier2Level = 0.58f;
        [Range(0f, 1f)] public float continentTier3Level = 0.68f;
        [Range(0f, 1f)] public float continentTier4Level = 0.76f;
        [Range(0f, 1f)] public float continentTier5Level = 0.82f;
        [Range(0f, 1f)] public float continentTier6Level = 0.88f;
        [Range(0f, 1f)] public float continentTier7Level = 0.94f;
        [Tooltip("Elevation added near the world origin so the spawn region is always " +
                 "solid land, fading to 0 over continentSpawnLandRadius cells.")]
        [Range(0f, 1f)] public float continentSpawnLandBias = 0.34f;
        [Tooltip("Radius (cells) over which the spawn land bias fades to zero.")]
        public float continentSpawnLandRadius = 48f;
        [Header("Continent rivers")]
        [Tooltip("Frequency of the winding river band noise. Lower = longer, broader rivers.")]
        public float riverFrequency = 0.025f;
        [Tooltip("Half-width of a river in noise units — larger = wider rivers.")]
        [Range(0f, 0.1f)] public float riverHalfWidth = 0.035f;   // wider rivers (was 0.014 — read as thin trickles)
        [Tooltip("Extra width past the water where the bank is sand/dirt.")]
        [Range(0f, 0.1f)] public float riverBankWidth = 0.012f;
        [Tooltip("Coordinate-warp amplitude (cells) that makes rivers meander instead of " +
                 "running straight.")]
        public float riverWarpAmplitude = 22f;
        [Tooltip("Frequency of the river coordinate-warp noise.")]
        public float riverWarpFrequency = 0.02f;
        [Tooltip("Rivers only carve land below this elevation, so they sit in valleys " +
                 "and never run across the highest peaks.")]
        [Range(0f, 1f)] public float riverMaxElevation = 0.72f;

        [Header("Rendering")]
        [Tooltip("Add a Pixel Perfect Camera so the pixel-art tiles stay crisp and the " +
                 "grid does not shimmer as the player moves.")]
        public bool pixelPerfect = true;

        [Header("Player")]
        public float moveSpeed = 2.8f;
        public float interactRange = 1.8f;

        // ---- Movement: jump & sprint (owner-approved addition) ----
        // Walking keeps the maxWalkStepHeight=0 invariant; only an active jump may ascend.
        [Header("Movement: jump & sprint")]
        [Tooltip("Max height steps the player may ascend by walking alone (invariant: 1 — " +
                 "walk up one step; jump stacks for taller cliffs).")]
        public int maxWalkStepHeight = 1;
        [Tooltip("Horizontal tile range of a directional leap jump (hold direction + Space). " +
                 "0 disables leaping.")]
        public float jumpLeapTiles = 2f;
        [Tooltip("How far (in world units) the player's collision point may overlap a blocked " +
                 "cell before being pushed back. Lets the player hug walls smoothly.")]
        public float wallCollisionInset = 0.22f;
        [Tooltip("Seconds a jump hop lasts (visual arc + the window in which one height step may be climbed).")]
        public float jumpDuration = 0.35f;
        [Tooltip("Peak visual lift of the hop in world units. Visual only — never changes cell/height queries.")]
        public float jumpHeightUnits = 0.22f;
        [Tooltip("Height steps the player may ascend during a single jump (walking stays at 0).")]
        public int jumpClimbSteps = 1;
        [Tooltip("Move-speed multiplier while sprinting (hold Left Shift).")]
        public float sprintMultiplier = 1.6f;
        [Tooltip("Stamina drained per second while sprint-moving; sprint stops at 0 stamina.")]
        public float sprintStaminaPerSecond = 6f;
        [Tooltip("Stamina regained per second while not sprinting.")]
        public float sprintStaminaRegenPerSecond = 4f;
        [Tooltip("After running dry, sprint stays locked out until stamina regenerates back " +
                 "above this value (prevents on/off flicker at the 0 boundary).")]
        public float sprintRecoverStamina = 8f;

        [Header("Mobs")]
        public int mobCap = 8;
        public float mobSpawnInterval = 3f;
        public float mobSpawnRadius = 9f;
        public float mobDespawnRadius = 16f;

        [Header("Night danger multipliers")]
        [Tooltip("Damage dealt by night-buffed mobs is multiplied by this.")]
        public float nightMobDamageMultiplier = 1.5f;
        [Tooltip("Move speed of night-buffed mobs is multiplied by this.")]
        public float nightMobSpeedMultiplier = 1.25f;
        [Tooltip("Aggro detection range of night-buffed mobs is multiplied by this.")]
        public float nightAggroRangeMultiplier = 1.5f;

        [Header("Campfire")]
        [Tooltip("Fallback safe-radius (cells) used when the campfire prop does not define " +
                 "its own campWardRadius.")]
        public float campfireSafeRadius = 4f;
        [Tooltip("0–1 strength of the campfire ward. 1 = full protection against same-tier " +
                 "mobs; lower values let mobs breach more easily.")]
        [Range(0f, 1f)]
        public float campfireWardStrength = 1f;

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
