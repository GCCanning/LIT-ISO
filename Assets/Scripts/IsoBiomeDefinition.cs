using UnityEngine;
using UnityEngine.Tilemaps;

public enum BiomeKind
{
    Unknown = 0,
    Plains = 1,
    Desert = 2,
    FrozenMountain = 3,
    FrozenCave = 4,
    Temple = 5,
    Basic = 6,
    Forest = 7
}

[CreateAssetMenu(menuName = "Iso World/Biome Definition", fileName = "BiomeDefinition")]
public class IsoBiomeDefinition : ScriptableObject
{
    public string biomeName = "Plains";
    public BiomeKind biomeKind = BiomeKind.Unknown;

    [Header("Climate Range")]
    [Range(0f, 1f)] public float temperatureMin = 0.25f;
    [Range(0f, 1f)] public float temperatureMax = 0.75f;
    [Range(0f, 1f)] public float moistureMin = 0.35f;
    [Range(0f, 1f)] public float moistureMax = 1f;
    [Range(0f, 1f)] public float elevationMin = 0f;
    [Range(0f, 1f)] public float elevationMax = 0.72f;

    [Header("Height 0 - Flat Ground")]
    [Tooltip("RuleTile or plain tile placed at z=0 across every cell.")]
    public TileBase flatGroundTile;

    [Tooltip("Optional variants used by procedural chunks. If empty, flatGroundTile is used.")]
    public TileBase[] flatGroundVariants;

    [Header("Height 1 - Raised Ground")]
    [Tooltip("RuleTile placed at z=1 for every raised cell.")]
    public TileBase raisedRuleTile;

    [Tooltip("Optional variants used by procedural chunks. If empty, raisedRuleTile is used.")]
    public TileBase[] raisedGroundVariants;

    [Header("Height 2+ - Mid & Peak Elevation")]
    [Tooltip("Tile used at z=2. Adds visible rock/dirt layering mid-way up a hill. " +
             "Leave empty to reuse raisedRuleTile at this level.")]
    public TileBase midElevationTile;

    [Tooltip("Tile used at z=3 and above. Represents the summit — snow cap, bare rock, lava plateau, etc. " +
             "Leave empty to reuse midElevationTile (or raisedRuleTile) at this level.")]
    public TileBase peakTile;

    [Header("Decorations")]
    [Tooltip("Legacy flat decoration list (unused when the categorised system below is populated).")]
    public TileBase[] decorationTiles;

    [Header("Decoration Categories (clustered placement)")]
    [Tooltip("Trees — placed in noise-driven CLUSTERS to form forests/groves rather than uniform scatter.")]
    public TileBase[] treeTiles;
    [Tooltip("Ground cover (flowers, bushes, grass) — scattered lightly and evenly across open ground.")]
    public TileBase[] groundCoverTiles;
    [Tooltip("Rocks — placed rarely, in small noise-driven clumps (outcrops), never blanketing the map.")]
    public TileBase[] rockTiles;

    [Header("Forest Clustering")]
    [Tooltip("Frequency of the forest patch noise. Lower = larger, broader forests; higher = small copses.")]
    public float forestNoiseScale = 0.05f;
    [Tooltip("Forest-noise value above which a cell is 'inside a grove'. Higher = sparser, smaller forests.")]
    [Range(0f, 1f)] public float forestThreshold = 0.52f;
    [Tooltip("Tree spawn chance inside a grove (scaled by how deep into the grove the cell is).")]
    [Range(0f, 1f)] public float treeDensityInForest = 0.4f;
    [Tooltip("Ground-cover spawn chance per open (non-tree) cell.")]
    [Range(0f, 1f)] public float groundCoverChance = 0.06f;
    [Tooltip("Rock-cluster noise threshold — rocks only appear where this coarse noise is high.")]
    [Range(0f, 1f)] public float rockClusterThreshold = 0.7f;
    [Tooltip("Rock spawn chance inside a rock cluster.")]
    [Range(0f, 1f)] public float rockChanceInCluster = 0.12f;

    [Range(0f, 1f)]
    public float decorationChance = 0.015f;

    [Tooltip("Biome-local decoration density used by climate generation. Falls back to decorationChance when zero.")]
    [Range(0f, 1f)]
    public float baseDecorationDensity = 0.015f;

    [Tooltip("Multiplier applied when this biome is used inside a transition band.")]
    [Range(0f, 1f)]
    public float transitionDecorationMultiplier = 0.45f;

    [Header("Collision - Cliff Wall Borders")]
    [Tooltip("Physics-only tile for the South cliff face, where the y-1 neighbour is lower.")]
    public TileBase colliderCliffSouth;

    [Tooltip("Physics-only tile for the West cliff face, where the x-1 neighbour is lower.")]
    public TileBase colliderCliffWest;

    [Tooltip("Physics-only tile for the North cliff face, where the y+1 neighbour is lower.")]
    public TileBase colliderCliffNorth;

    [Tooltip("Physics-only tile for the East cliff face, where the x+1 neighbour is lower.")]
    public TileBase colliderCliffEast;

    [Header("Generation")]
    [Range(0f, 1f)]
    [Tooltip("Perlin noise value at or above which a cell becomes raised.")]
    public float raisedTileThreshold = 0.55f;

    [Tooltip("Frequency of the height noise. Lower means broader hills; higher means jagged terrain.")]
    public float heightNoiseScale = 0.08f;

    [Tooltip("Offset used when blending multiple biomes in one infinite world.")]
    public float biomeNoiseOffset = 0f;

    public BiomeKind EffectiveBiomeKind
    {
        get
        {
            if (biomeKind != BiomeKind.Unknown)
            {
                return biomeKind;
            }

            string normalized = (biomeName ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
            if (normalized.Contains("desert")) return BiomeKind.Desert;
            if (normalized.Contains("frozenmountain")) return BiomeKind.FrozenMountain;
            if (normalized.Contains("frozencave")) return BiomeKind.FrozenCave;
            if (normalized.Contains("temple")) return BiomeKind.Temple;
            if (normalized.Contains("basic")) return BiomeKind.Basic;
            if (normalized.Contains("forest")) return BiomeKind.Forest;
            if (normalized.Contains("plains")) return BiomeKind.Plains;
            return BiomeKind.Unknown;
        }
    }

    public TileBase GetFlatGroundTile(int worldX, int worldY, int seed)
    {
        return PickTile(flatGroundVariants, flatGroundTile, worldX, worldY, seed, 17);
    }

    public TileBase GetRaisedGroundTile(int worldX, int worldY, int seed)
    {
        return PickTile(raisedGroundVariants, raisedRuleTile, worldX, worldY, seed, 31);
    }

    /// <summary>
    /// Returns the correct tile for a specific elevation z-level:
    ///   z=1 → raisedRuleTile (grass edge, sand dune, etc.)
    ///   z=2 → midElevationTile if assigned, otherwise raisedRuleTile
    ///   z=3+ → peakTile if assigned, otherwise midElevationTile, otherwise raisedRuleTile
    /// This allows biomes to show rock mid-sections and snow/lava peaks on tall terrain.
    /// </summary>
    public TileBase GetTileForHeight(int z, int worldX, int worldY, int seed)
    {
        if (z <= 1)
        {
            return GetRaisedGroundTile(worldX, worldY, seed);
        }
        if (z == 2)
        {
            return midElevationTile != null
                ? midElevationTile
                : GetRaisedGroundTile(worldX, worldY, seed);
        }
        // z >= 3: peak
        if (peakTile != null) return peakTile;
        if (midElevationTile != null) return midElevationTile;
        return GetRaisedGroundTile(worldX, worldY, seed);
    }

    public TileBase GetDecorationTile(int worldX, int worldY, int seed)
    {
        return PickTile(decorationTiles, null, worldX, worldY, seed, 47);
    }

    /// <summary>True if this biome uses the new categorised/clustered decoration system.</summary>
    public bool HasCategorisedDecorations =>
        (treeTiles != null && treeTiles.Length > 0) ||
        (groundCoverTiles != null && groundCoverTiles.Length > 0) ||
        (rockTiles != null && rockTiles.Length > 0);

    /// <summary>
    /// Picks a decoration for a cell using clustered placement:
    ///   • Trees appear in noise-driven forest groves (dense in the middle, thinning at edges).
    ///   • Ground cover (flowers/bushes) scatters lightly on open ground between groves.
    ///   • Rocks appear only inside rare coarse-noise clumps, never blanketing the map.
    /// Returns null when the cell should stay empty. densityScale (0..1) lets callers
    /// fade decorations out (e.g. inside biome transition bands).
    /// </summary>
    public TileBase GetClusteredDecoration(int worldX, int worldY, int seed, float densityScale = 1f)
    {
        // 1. Trees — forest groves via low-frequency noise.
        if (treeTiles != null && treeTiles.Length > 0)
        {
            float forest = Mathf.PerlinNoise(
                (worldX + seed * 0.7f) * forestNoiseScale,
                (worldY - seed * 0.3f) * forestNoiseScale);
            if (forest > forestThreshold)
            {
                float depth = Mathf.InverseLerp(forestThreshold, 1f, forest); // 0 at grove edge, 1 deep inside
                float chance = treeDensityInForest * (0.35f + 0.65f * depth) * densityScale;
                if (Hash01(worldX, worldY, seed, 71) < chance)
                {
                    return PickTile(treeTiles, null, worldX, worldY, seed, 73);
                }
            }
        }

        // 2. Ground cover — light even scatter on open ground.
        if (groundCoverTiles != null && groundCoverTiles.Length > 0)
        {
            if (Hash01(worldX, worldY, seed, 81) < groundCoverChance * densityScale)
            {
                return PickTile(groundCoverTiles, null, worldX, worldY, seed, 83);
            }
        }

        // 3. Rocks — rare clumps only where a coarse noise peaks.
        if (rockTiles != null && rockTiles.Length > 0)
        {
            float rockNoise = Mathf.PerlinNoise(
                (worldX - seed * 0.61f) * (forestNoiseScale * 1.7f),
                (worldY + seed * 0.43f) * (forestNoiseScale * 1.7f));
            if (rockNoise > rockClusterThreshold &&
                Hash01(worldX, worldY, seed, 91) < rockChanceInCluster * densityScale)
            {
                return PickTile(rockTiles, null, worldX, worldY, seed, 93);
            }
        }

        return null;
    }

    public bool ShouldPlaceDecoration(int worldX, int worldY, int seed)
    {
        return ShouldPlaceDecoration(worldX, worldY, seed, GetDecorationDensity(false));
    }

    public bool ShouldPlaceDecoration(int worldX, int worldY, int seed, float chance)
    {
        if (decorationTiles == null || decorationTiles.Length == 0 || chance <= 0f)
        {
            return false;
        }

        return Hash01(worldX, worldY, seed, 61) < chance;
    }

    public float GetDecorationDensity(bool isTransition)
    {
        float density = baseDecorationDensity > 0f ? baseDecorationDensity : decorationChance;
        return isTransition ? density * transitionDecorationMultiplier : density;
    }

    public bool MatchesClimate(float temperature, float moisture, float elevation)
    {
        return temperature >= temperatureMin && temperature <= temperatureMax
            && moisture >= moistureMin && moisture <= moistureMax
            && elevation >= elevationMin && elevation <= elevationMax;
    }

    private static TileBase PickTile(TileBase[] variants, TileBase fallback, int worldX, int worldY, int seed, int salt)
    {
        if (variants == null || variants.Length == 0)
        {
            return fallback;
        }

        int index = Mathf.FloorToInt(Hash01(worldX, worldY, seed, salt) * variants.Length);
        index = Mathf.Clamp(index, 0, variants.Length - 1);
        return variants[index] != null ? variants[index] : fallback;
    }

    private static float Hash01(int x, int y, int seed, int salt)
    {
        unchecked
        {
            uint hash = (uint)seed;
            hash ^= (uint)(x * 374761393);
            hash = (hash << 13) | (hash >> 19);
            hash ^= (uint)(y * 668265263);
            hash += (uint)(salt * 1442695041);
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
