using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

public class IsoWorldChunkManager : MonoBehaviour
{
    public const int HeightLayerCount = 8;

[Header("World")]
    public Grid grid;
    public Transform player;
    public Material worldMaterial;
    public Material decorationMaterial;
    public IsoBiomeDefinition plainsBiome;
public IsoBiomeDefinition[] biomes;
    public TileBase cliffColliderTile;
    public IsoRuntimeRecorder recorder;
    public int seed = 12345;

    [Header("Chunks")]
    public int chunkSize = 32;
    public int activeRadius = 1;
    public bool poolInactiveChunks = true;

    [Header("Biome Blending")]
    public float biomeNoiseScale = 0.0125f;
    public float temperatureNoiseScale = 0.0045f;
    public float moistureNoiseScale = 0.004f;
    public float continentNoiseScale = 0.0035f;
    public int biomeBlendRadius = 3;
    public float biomeBlendNoiseScale = 0.08f;
    [Range(0f, 1f)]
    public float transitionTileChance = 0.35f;
    public BiomeTransitionRule[] transitionRules;

    [Header("Tile Readability")]
    public bool showTileBorders = true;
    public Color tileBorderColor = new Color(0f, 0f, 0f, 0.16f);
    public bool showHeightEdgeShadows = true;
    public Color heightEdgeShadowColor = new Color(0f, 0f, 0f, 0.18f);

    [Tooltip("Soft drop shadow stamped under every decoration prop so trees/rocks/props feel planted on the ground.")]
    public bool showDecorationShadows = true;
    [Tooltip("Daytime shadow colour (dark). Driven dynamically — fades toward the night colour after dusk.")]
    public Color decorationShadowColor = new Color(0f, 0f, 0f, 0.34f);
    [Tooltip("Night-time shadow colour (faint cool blue moonlight).")]
    public Color decorationShadowNightColor = new Color(0.10f, 0.13f, 0.22f, 0.15f);
    [Tooltip("How far decoration shadows slide away from their prop as the sun nears the horizon (world units).")]
    public float decorationShadowMaxOffset = 0.4f;

    [Header("Decoration Occlusion")]
    [Tooltip("Fade tall decoration tiles when the player or a mob is moving behind them so characters stay readable.")]
    public bool fadeOccludingDecorations = true;
    [Tooltip("Include spawned enemies in occlusion fading. Disable if you want the cheapest path first.")]
    public bool fadeForSpawnedMobs = false;
    [Range(0.05f, 1f)] public float fadedDecorationAlpha = 0.35f;
    [Range(0f, 1.5f)] public float decorationOcclusionPaddingX = 0.2f;
    [Range(0f, 2.5f)] public float decorationOcclusionPaddingY = 0.55f;
    [Tooltip("Minimum world-space sprite height before a decoration can occlude actors. Keeps bushes and tiny props opaque.")]
    [Range(0.5f, 4f)] public float occludingDecorationMinWorldHeight = 1.35f;

    [Header("Terrain Height")]
    [Tooltip("Maximum stacked tile height (in cells). 1 = old behavior (flat or raised). " +
             "2-3 = rolling hills. Higher values are experimental cliff/mountain terrain.")]
    [Range(1, 8)]
    public int maxTerrainHeight = 3;

    [Tooltip("Maximum height for lowland overworld biomes.")]
    [Range(1, 3)]
    public int lowlandMaxTerrainHeight = 2;

    [Tooltip("How aggressively noise translates to height. Higher = more dramatic peaks. " +
             "1 = linear, 2 = quadratic (cliffs), 0.5 = compressed (gentle hills).")]
    [Range(0.3f, 3f)]
    public float terrainHeightFalloff = 1.7f;

    [Header("Starter Zone")]
    [Tooltip("Force a safe, defined starting area around the spawn point. The player always " +
             "begins in this biome on flat ground, regardless of the procedural climate.")]
    public bool useStarterZone = true;

    [Tooltip("Cell the starter zone is centred on. Player spawns here (default world origin).")]
    public Vector2Int starterZoneCenter = Vector2Int.zero;

    [Tooltip("Within this radius (cells) the biome is forced to the starter biome — no desert/mountain.")]
    [Min(0)] public int starterZoneRadius = 28;

    [Tooltip("Within this radius (cells) the ground is forced flat (height 0) — a guaranteed safe clearing.")]
    [Min(0)] public int starterZoneFlattenRadius = 12;

    [Tooltip("Cells of distance per +1 height when ramping out of the flat clearing. Higher = gentler slope.")]
    [Min(1)] public int starterZoneRampStep = 5;

    [Tooltip("Which biome the starter zone is. Plains = grassy meadow / forest starter.")]
    public BiomeKind starterZoneBiome = BiomeKind.Plains;

    [Header("Enemy Spawning")]
    public bool spawnSlimesInPlains = true;
    public EnemyDefinition commonSlime;
    public EnemyDefinition rareSlime;
    public EnemyDefinition bossSlime;
    [Range(0f, 0.1f)] public float commonSlimeChance = 0.012f;
    [Range(0f, 0.05f)] public float rareSlimeChance = 0.003f;
    [Range(0f, 0.01f)] public float bossSlimeChance = 0.00035f;
    [Min(0)] public int maxSlimesPerChunk = 8;
    [Min(0)] public int maxRareSlimesPerChunk = 2;
    [Min(0)] public int maxBossSlimesPerChunk = 1;
    [Tooltip("1 means a 3x3 cell area around a boss cannot contain another boss.")]
    [Range(1, 8)] public int bossSlimeExclusionRadius = 1;

    private readonly Dictionary<Vector2Int, ChunkHandle> loadedChunks = new Dictionary<Vector2Int, ChunkHandle>();
    private readonly Stack<ChunkHandle> pooledChunks = new Stack<ChunkHandle>();
    private readonly Dictionary<EnemyDefinition, Stack<GameObject>> enemyPool = new Dictionary<EnemyDefinition, Stack<GameObject>>();
    private readonly List<Transform> occlusionActorBuffer = new List<Transform>(16);
    private readonly Collider2D[] terrainOverlapBuffer = new Collider2D[16];
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    private TileBase tileBorderTile;
    private Sprite selectionSprite;
    private TileBase heightEdgeShadowTile;
    private TileBase decorationShadowTile;
    public int MaxSupportedHeight => HeightLayerCount - 1;

    public readonly struct GroundCellSample
    {
        public readonly Vector3Int Cell;
        public readonly int Height;
        public readonly IsoBiomeDefinition Biome;
        public readonly bool IsTransitionCell;
        public readonly bool IsHeightEdge;

        public GroundCellSample(Vector3Int cell, int height, IsoBiomeDefinition biome, bool isTransitionCell, bool isHeightEdge)
        {
            Cell = cell;
            Height = height;
            Biome = biome;
            IsTransitionCell = isTransitionCell;
            IsHeightEdge = isHeightEdge;
        }
    }

    public readonly struct FootprintMoveEvaluation
    {
        public readonly Vector3Int FromCell;
        public readonly Vector3Int ToCell;
        public readonly int FromHeight;
        public readonly int TargetHeight;
        public readonly int TargetMaxHeight;
        public readonly int HeightDelta;
        public readonly bool IsColliderBlocked;
        public readonly bool IsBlocked;
        public readonly string Reason;

        public FootprintMoveEvaluation(
            Vector3Int fromCell,
            Vector3Int toCell,
            int fromHeight,
            int targetHeight,
            int targetMaxHeight,
            int heightDelta,
            bool isColliderBlocked,
            bool isBlocked,
            string reason)
        {
            FromCell = fromCell;
            ToCell = toCell;
            FromHeight = fromHeight;
            TargetHeight = targetHeight;
            TargetMaxHeight = targetMaxHeight;
            HeightDelta = heightDelta;
            IsColliderBlocked = isColliderBlocked;
            IsBlocked = isBlocked;
            Reason = reason;
        }
    }

    public Sprite GetSelectionSprite()
    {
        if (selectionSprite != null) return selectionSprite;

        const int width = 64;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0, 0, 0, 0);
        for (int i = 0; i < width * height; i++) texture.SetPixel(i % width, i / width, clear);

        // Draw a bright yellow selection border (2 pixels wide for visibility)
        Color line = new Color(1f, 0.92f, 0.016f, 0.8f);
        Vector2Int top = new Vector2Int(width / 2, 0);
        Vector2Int right = new Vector2Int(width - 1, height / 2);
        Vector2Int bottom = new Vector2Int(width / 2, height - 1);
        Vector2Int left = new Vector2Int(0, height / 2);

        DrawTextureLine(texture, top, right, line, 0);
        DrawTextureLine(texture, right, bottom, line, 0);
        DrawTextureLine(texture, bottom, left, line, 0);
        DrawTextureLine(texture, left, top, line, 0);
        
        // Draw second inner ring for thickness
        DrawTextureLine(texture, top + Vector2Int.down, right + Vector2Int.left, line, 0);
        DrawTextureLine(texture, right + Vector2Int.left, bottom + Vector2Int.up, line, 0);
        DrawTextureLine(texture, bottom + Vector2Int.up, left + Vector2Int.right, line, 0);
        DrawTextureLine(texture, left + Vector2Int.right, top + Vector2Int.down, line, 0);

        texture.Apply();

        selectionSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 64f);
        selectionSprite.name = "Runtime Selection Sprite";
        return selectionSprite;
    }

    private class ChunkHandle
    {
        public GameObject gameObject;
        public Tilemap groundTilemap;
        public Tilemap elevationTilemap;
        public Tilemap decorationTilemap;
        public Tilemap decorationShadowTilemap;
        public Tilemap heightShadowTilemap;
        public Tilemap borderTilemap;
        public Tilemap[] heightColliderTilemaps;
        public List<GameObject> spawnedObjects = new List<GameObject>();
        public HashSet<Vector3Int> fadedDecorationCells = new HashSet<Vector3Int>();
        public HashSet<Vector3Int> pendingFadedDecorationCells = new HashSet<Vector3Int>();
        public bool isVisibleToCamera;
        public Bounds worldBounds;
    }

    [System.Serializable]
    public class BiomeTransitionRule
    {
        public BiomeKind fromBiome;
        public BiomeKind toBiome;
        [Range(0f, 1f)] public float primaryTileWeight = 0.75f;
        [Range(0f, 1f)] public float secondaryTileWeight = 0.25f;
        [Range(0f, 1f)] public float decorationMultiplier = 0.45f;
        [Range(0f, 2f)] public float resourceMultiplier = 0.5f;
    }

    private struct ClimateSample
    {
        public float temperature;
        public float moisture;
        public float continentalHeight;
    }

    private struct BiomeSample
    {
        public IsoBiomeDefinition biome;
        public IsoBiomeDefinition secondaryBiome;
        public float blendStrength;
        public bool isTransitionCell;
        public SurfaceKind surfaceKind;
        public int height;
    }

    private enum SurfaceKind
    {
        Primary = 0,
        Transition = 1,
        Secondary = 2
    }

    private struct BiomeData
    {
        public BiomeKind kind;
        public float temperatureMin;
        public float temperatureMax;
        public float moistureMin;
        public float moistureMax;
        public float elevationMin;
        public float elevationMax;
        public float raisedTileThreshold;
        public float heightNoiseScale;
        public float biomeNoiseOffset;
    }

    private struct CellData
    {
        public int height;
        public int primaryBiomeIndex;
        public int secondaryBiomeIndex;
        public float blendStrength;
        public bool isTransition;
    }

    private struct ChunkDataJob : IJobParallelFor
    {
        [ReadOnly] public int startX;
        [ReadOnly] public int startY;
        [ReadOnly] public int chunkSize;
        [ReadOnly] public int seed;
        [ReadOnly] public NativeArray<BiomeData> biomeDefinitions;
        [ReadOnly] public float biomeNoiseScale;
        [ReadOnly] public float temperatureNoiseScale;
        [ReadOnly] public float moistureNoiseScale;
        [ReadOnly] public float continentNoiseScale;
        [ReadOnly] public int biomeBlendRadius;
        [ReadOnly] public float biomeBlendNoiseScale;
        [ReadOnly] public float terrainHeightFalloff;
        [ReadOnly] public int lowlandMaxTerrainHeight;
        [ReadOnly] public int maxTerrainHeight;

        // Starter zone (0 = disabled). starterBiomeIndex is the index into
        // biomeDefinitions of the forced starter biome.
        [ReadOnly] public int starterEnabled;
        [ReadOnly] public int starterCenterX;
        [ReadOnly] public int starterCenterY;
        [ReadOnly] public int starterRadius;
        [ReadOnly] public int starterFlatten;
        [ReadOnly] public int starterRamp;
        [ReadOnly] public int starterBiomeIndex;

        public NativeArray<CellData> results;

        public void Execute(int index)
        {
            int localX = index % chunkSize;
            int localY = index / chunkSize;
            int worldX = startX + localX;
            int worldY = startY + localY;

            ClimateSample climate = SampleClimateStatic(worldX, worldY, seed, temperatureNoiseScale, moistureNoiseScale, continentNoiseScale);
            int primaryIdx = SelectBiomeIndex(climate, biomeDefinitions);

            CellData data = SampleBiomeBlendStatic(worldX, worldY, primaryIdx, climate, seed, biomeBlendRadius, biomeBlendNoiseScale, biomeDefinitions, temperatureNoiseScale, moistureNoiseScale, continentNoiseScale);

            // Starter zone: force the starter biome (no transitions) so the spawn area
            // is always the intended meadow. Height is shaped further down.
            bool inStarter = false;
            float starterDist = 0f;
            if (starterEnabled == 1 && starterBiomeIndex >= 0 && starterBiomeIndex < biomeDefinitions.Length)
            {
                long dx = worldX - starterCenterX;
                long dy = worldY - starterCenterY;
                long d2 = dx * dx + dy * dy;
                if (d2 <= (long)starterRadius * starterRadius)
                {
                    inStarter = true;
                    starterDist = math.sqrt((float)d2);
                    data.primaryBiomeIndex = starterBiomeIndex;
                    data.secondaryBiomeIndex = -1;
                    data.blendStrength = 0;
                    data.isTransition = false;
                }
            }

            BiomeData primaryBiome = biomeDefinitions[data.primaryBiomeIndex];
            float x = (worldX + seed * 0.13f + primaryBiome.biomeNoiseOffset) * primaryBiome.heightNoiseScale;
            float y = (worldY - seed * 0.17f - primaryBiome.biomeNoiseOffset) * primaryBiome.heightNoiseScale;
            float localNoise = noise.snoise(new float2(x, y)) * 0.5f + 0.5f;
            float heightNoise = math.lerp(localNoise, climate.continentalHeight, GetContinentalHeightWeightStatic(primaryBiome));

            int rawHeight = CalculateTerrainHeightStatic(heightNoise, primaryBiome, terrainHeightFalloff, lowlandMaxTerrainHeight, maxTerrainHeight);
            data.height = SmoothHillHeightStatic(worldX, worldY, rawHeight, seed, biomeDefinitions, temperatureNoiseScale, moistureNoiseScale, continentNoiseScale, biomeBlendRadius, biomeBlendNoiseScale, terrainHeightFalloff, lowlandMaxTerrainHeight, maxTerrainHeight);

            // Starter zone height shaping: flat clearing in the centre, gentle ramp outward.
            if (inStarter)
            {
                if (starterDist <= starterFlatten)
                {
                    data.height = 0;
                }
                else
                {
                    int cap = (int)((starterDist - starterFlatten) / math.max(1, starterRamp));
                    data.height = math.min(data.height, math.max(0, cap));
                }
            }

            results[index] = data;
        }

        private static ClimateSample SampleClimateStatic(int worldX, int worldY, int seed, float tScale, float mScale, float cScale)
        {
            return new ClimateSample
            {
                temperature = noise.snoise(new float2((worldX + seed * 0.19f) * tScale, (worldY - seed * 0.23f) * tScale)) * 0.5f + 0.5f,
                moisture = noise.snoise(new float2((worldX - seed * 0.31f) * mScale, (worldY + seed * 0.29f) * mScale)) * 0.5f + 0.5f,
                continentalHeight = noise.snoise(new float2((worldX + seed * 0.41f) * cScale, (worldY - seed * 0.37f) * cScale)) * 0.5f + 0.5f
            };
        }

        private static int SelectBiomeIndex(ClimateSample climate, NativeArray<BiomeData> biomes)
        {
            BiomeKind targetKind;
            if (climate.continentalHeight > 0.74f || climate.temperature < 0.28f) targetKind = BiomeKind.FrozenMountain;
            else if (climate.temperature > 0.58f && climate.moisture < 0.42f) targetKind = BiomeKind.Desert;
            else targetKind = BiomeKind.Plains;

            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i].kind == targetKind) return i;
            }
            return 0;
        }

        private static CellData SampleBiomeBlendStatic(int worldX, int worldY, int primaryIdx, ClimateSample climate, int seed, int radius, float blendNoiseScale, NativeArray<BiomeData> biomes, float tScale, float mScale, float cScale)
        {
            CellData data = new CellData { primaryBiomeIndex = primaryIdx, secondaryBiomeIndex = -1, blendStrength = 0, isTransition = false };
            int strongestNeighbourIdx = -1;
            int strongestCount = 0;
            
            for (int y = -radius; y <= radius; y += radius)
            {
                for (int x = -radius; x <= radius; x += radius)
                {
                    if (x == 0 && y == 0) continue;
                    ClimateSample nClimate = SampleClimateStatic(worldX + x, worldY + y, seed, tScale, mScale, cScale);
                    int nIdx = SelectBiomeIndex(nClimate, biomes);
                    if (nIdx == primaryIdx) continue;

                    int count = 0;
                    for (int ny = -radius; ny <= radius; ny += radius)
                    {
                        for (int nx = -radius; nx <= radius; nx += radius)
                        {
                            if (nx == 0 && ny == 0) continue;
                            if (SelectBiomeIndex(SampleClimateStatic(worldX + nx, worldY + ny, seed, tScale, mScale, cScale), biomes) == nIdx) count++;
                        }
                    }

                    if (count > strongestCount)
                    {
                        strongestCount = count;
                        strongestNeighbourIdx = nIdx;
                    }
                }
            }

            if (strongestNeighbourIdx != -1)
            {
                float neighbourRatio = math.clamp(strongestCount / 8f, 0, 1);
                float edgeNoise = noise.snoise(new float2((worldX + seed * 0.53f) * blendNoiseScale, (worldY - seed * 0.47f) * blendNoiseScale)) * 0.5f + 0.5f;
                float blendStrength = math.clamp((neighbourRatio * 0.75f) + (edgeNoise * 0.25f), 0, 1);
                if (blendStrength >= 0.25f)
                {
                    data.secondaryBiomeIndex = strongestNeighbourIdx;
                    data.blendStrength = blendStrength;
                    data.isTransition = true;
                }
            }
            return data;
        }

        private static int SmoothHillHeightStatic(int worldX, int worldY, int rawHeight, int seed, NativeArray<BiomeData> biomes, float tScale, float mScale, float cScale, int radius, float blendNoiseScale, float falloff, int lowMax, int maxH)
        {
            if (rawHeight <= 1) return rawHeight;
            int minNeighbour = rawHeight;
            minNeighbour = math.min(minNeighbour, CalculateRawTerrainHeightStatic(worldX + 1, worldY, seed, biomes, tScale, mScale, cScale, radius, blendNoiseScale, falloff, lowMax, maxH));
            minNeighbour = math.min(minNeighbour, CalculateRawTerrainHeightStatic(worldX - 1, worldY, seed, biomes, tScale, mScale, cScale, radius, blendNoiseScale, falloff, lowMax, maxH));
            minNeighbour = math.min(minNeighbour, CalculateRawTerrainHeightStatic(worldX, worldY + 1, seed, biomes, tScale, mScale, cScale, radius, blendNoiseScale, falloff, lowMax, maxH));
            minNeighbour = math.min(minNeighbour, CalculateRawTerrainHeightStatic(worldX, worldY - 1, seed, biomes, tScale, mScale, cScale, radius, blendNoiseScale, falloff, lowMax, maxH));
            return math.min(rawHeight, minNeighbour + 1);
        }

        private static int CalculateRawTerrainHeightStatic(int worldX, int worldY, int seed, NativeArray<BiomeData> biomes, float tScale, float mScale, float cScale, int radius, float blendNoiseScale, float falloff, int lowMax, int maxH)
        {
            ClimateSample climate = SampleClimateStatic(worldX, worldY, seed, tScale, mScale, cScale);
            int primaryIdx = SelectBiomeIndex(climate, biomes);
            CellData data = SampleBiomeBlendStatic(worldX, worldY, primaryIdx, climate, seed, radius, blendNoiseScale, biomes, tScale, mScale, cScale);
            BiomeData primaryBiome = biomes[data.primaryBiomeIndex];
            float x = (worldX + seed * 0.13f + primaryBiome.biomeNoiseOffset) * primaryBiome.heightNoiseScale;
            float y = (worldY - seed * 0.17f - primaryBiome.biomeNoiseOffset) * primaryBiome.heightNoiseScale;
            float localNoise = noise.snoise(new float2(x, y)) * 0.5f + 0.5f;
            float heightNoise = math.lerp(localNoise, climate.continentalHeight, GetContinentalHeightWeightStatic(primaryBiome));
            return CalculateTerrainHeightStatic(heightNoise, primaryBiome, falloff, lowMax, maxH);
        }

        private static int CalculateTerrainHeightStatic(float heightNoise, BiomeData biome, float falloff, int lowMax, int maxH)
        {
            int biomeMaxHeight = biome.kind == BiomeKind.FrozenMountain ? maxH : lowMax;
            if (biomeMaxHeight <= 1) return heightNoise >= biome.raisedTileThreshold ? 1 : 0;
            if (heightNoise < biome.raisedTileThreshold) return 0;
            float t = math.clamp((heightNoise - biome.raisedTileThreshold) / math.max(0.01f, 1f - biome.raisedTileThreshold), 0f, 1f);
            t = math.pow(t, falloff);
            int height = 1 + (int)math.floor(t * (biomeMaxHeight - 1 + 0.999f));
            return math.clamp(height, 1, biomeMaxHeight);
        }

        private static float GetContinentalHeightWeightStatic(BiomeData biome)
        {
            if (biome.kind == BiomeKind.FrozenMountain) return 0.65f;
            if (biome.kind == BiomeKind.Desert) return 0.25f;
            return 0.35f;
        }
    }

    private void Awake()
    {
        if (grid == null)
        {
            grid = GetComponent<Grid>();
        }

        if (recorder == null)
        {
            recorder = FindFirstObjectByType<IsoRuntimeRecorder>();
        }

        // Hook into WorldManager if available (set by WelcomeScreenManager)
        WorldManager wm = WorldManager.Instance;
        if (wm != null && !string.IsNullOrEmpty(wm.Seed))
        {
            // Parse the seed as an integer (or use hash of string if non-numeric)
            if (int.TryParse(wm.Seed, out int parsedSeed))
            {
                seed = parsedSeed;
            }
            else
            {
                seed = wm.Seed.GetHashCode();
            }
            Debug.Log($"IsoWorldChunkManager: Using world seed {seed} from WorldManager");
        }

        EnsureBiomeArray();
    }

    private void OnValidate()
    {
        EnsureBiomeArray();
        chunkSize = Mathf.Max(1, chunkSize);
        activeRadius = Mathf.Max(0, activeRadius);
        biomeNoiseScale = Mathf.Max(0.0001f, biomeNoiseScale);
        temperatureNoiseScale = Mathf.Max(0.0001f, temperatureNoiseScale);
        moistureNoiseScale = Mathf.Max(0.0001f, moistureNoiseScale);
        continentNoiseScale = Mathf.Max(0.0001f, continentNoiseScale);
        biomeBlendNoiseScale = Mathf.Max(0.0001f, biomeBlendNoiseScale);
        biomeBlendRadius = Mathf.Clamp(biomeBlendRadius, 1, 8);
        maxTerrainHeight = Mathf.Clamp(maxTerrainHeight, 1, HeightLayerCount);
        lowlandMaxTerrainHeight = Mathf.Clamp(lowlandMaxTerrainHeight, 1, Mathf.Min(3, maxTerrainHeight));
        maxSlimesPerChunk = Mathf.Max(0, maxSlimesPerChunk);
        maxRareSlimesPerChunk = Mathf.Max(0, maxRareSlimesPerChunk);
        maxBossSlimesPerChunk = Mathf.Max(0, maxBossSlimesPerChunk);
        bossSlimeExclusionRadius = Mathf.Clamp(bossSlimeExclusionRadius, 1, 8);
    }

    private void Start()
    {
        EnsureRecorder();
        RefreshChunks(force: true);
    }

    private SunController shadowSun;
    private bool shadowSunSearched;
    private Camera visibilityCamera;
    private float nextVisibleChunkRefreshTime;

    private void Update()
    {
        RefreshChunks(force: false);
        RefreshVisibleChunks();
        UpdateDecorationShadowDirection();
        UpdateDecorationOcclusionFade();
    }

    /// <summary>
    /// Sweeps every decoration shadow with the invisible orbiting sun: shadows slide
    /// out from under their prop as the sun nears the horizon, shrink to nothing at
    /// noon, and fade from dark daytime grey to faint blue moonlight at night.
    /// All shadows share one tilemap layer per chunk, so this is a couple of cheap
    /// transform/colour writes per loaded chunk — no per-prop cost.
    /// </summary>
    private void UpdateDecorationShadowDirection()
    {
        if (!showDecorationShadows || loadedChunks.Count == 0) return;

        if (!shadowSunSearched)
        {
            shadowSun = FindFirstObjectByType<SunController>();
            shadowSunSearched = true;
        }

        Vector3 offset;
        Color tint;
        if (shadowSun != null)
        {
            // Sun direction's horizontal component is ~0 at noon and ±1 at the horizon,
            // so it doubles as both the shadow direction and its length.
            Vector2 shadowDir = shadowSun.GetShadowDirection2D();
            float dayStrength = Mathf.Clamp01(shadowSun.SunAltitude); // 1 noon → 0 horizon/night

            offset = new Vector3(shadowDir.x * decorationShadowMaxOffset, 0f, 0f);
            tint = Color.Lerp(decorationShadowNightColor, decorationShadowColor, dayStrength);
        }
        else
        {
            // No sun in scene — static noon shadow directly under each prop.
            offset = Vector3.zero;
            tint = decorationShadowColor;
        }

        foreach (var kvp in loadedChunks)
        {
            if (!kvp.Value.isVisibleToCamera) continue;
            Tilemap shadowMap = kvp.Value.decorationShadowTilemap;
            if (shadowMap == null) continue;
            shadowMap.transform.localPosition = offset;
            if (shadowMap.color != tint) shadowMap.color = tint;
        }
    }

    private void UpdateDecorationOcclusionFade()
    {
        if (loadedChunks.Count == 0)
            return;

        if (!fadeOccludingDecorations)
        {
            foreach (var kvp in loadedChunks)
                ClearFadedDecorations(kvp.Value);
            return;
        }

        GatherOcclusionActors();
        foreach (var kvp in loadedChunks)
        {
            if (!kvp.Value.isVisibleToCamera)
            {
                ClearFadedDecorations(kvp.Value);
                continue;
            }
            UpdateChunkDecorationOcclusion(kvp.Value);
        }
    }

    private void GatherOcclusionActors()
    {
        occlusionActorBuffer.Clear();
        if (player != null)
            occlusionActorBuffer.Add(player);

        if (!fadeForSpawnedMobs)
            return;

        foreach (var kvp in loadedChunks)
        {
            List<GameObject> spawnedObjects = kvp.Value.spawnedObjects;
            for (int i = 0; i < spawnedObjects.Count; i++)
            {
                GameObject go = spawnedObjects[i];
                if (go != null && go.activeInHierarchy)
                    occlusionActorBuffer.Add(go.transform);
            }
        }
    }

    private void UpdateChunkDecorationOcclusion(ChunkHandle handle)
    {
        if (handle.decorationTilemap == null)
            return;

        HashSet<Vector3Int> nextFadedCells = handle.pendingFadedDecorationCells;
        nextFadedCells.Clear();
        for (int i = 0; i < occlusionActorBuffer.Count; i++)
        {
            Transform actor = occlusionActorBuffer[i];
            if (actor == null)
                continue;

            Vector3Int actorCell = SampleWorldPosition(actor.position).Cell;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector3Int cell = new Vector3Int(actorCell.x + dx, actorCell.y + dy, actorCell.z);
                    if (!HasDecorationTile(handle.decorationTilemap, cell))
                        continue;

                    if (ShouldFadeDecoration(handle.decorationTilemap, cell, actor.position))
                        nextFadedCells.Add(cell);
                }
            }
        }

        foreach (Vector3Int oldCell in handle.fadedDecorationCells)
        {
            if (!nextFadedCells.Contains(oldCell))
                handle.decorationTilemap.SetColor(oldCell, Color.white);
        }

        Color fadedColor = new Color(1f, 1f, 1f, fadedDecorationAlpha);
        foreach (Vector3Int cell in nextFadedCells)
        {
            if (!handle.fadedDecorationCells.Contains(cell))
                handle.decorationTilemap.SetColor(cell, fadedColor);
        }

        handle.fadedDecorationCells.Clear();
        foreach (Vector3Int cell in nextFadedCells)
            handle.fadedDecorationCells.Add(cell);
        nextFadedCells.Clear();
    }

    private void ClearFadedDecorations(ChunkHandle handle)
    {
        if (handle.decorationTilemap == null || handle.fadedDecorationCells.Count == 0)
            return;

        foreach (Vector3Int cell in handle.fadedDecorationCells)
            handle.decorationTilemap.SetColor(cell, Color.white);
        handle.fadedDecorationCells.Clear();
    }

    private bool HasDecorationTile(Tilemap decorationTilemap, Vector3Int cell)
    {
        Vector3Int flatCell = new Vector3Int(cell.x, cell.y, cell.z);
        return decorationTilemap.GetTile(flatCell) != null;
    }

    private bool ShouldFadeDecoration(Tilemap decorationTilemap, Vector3Int cell, Vector3 actorWorldPosition)
    {
        Sprite sprite = decorationTilemap.GetSprite(cell);
        if (sprite != null && sprite.bounds.size.y < occludingDecorationMinWorldHeight)
            return false;

        Vector3 cellCenter = decorationTilemap.GetCellCenterWorld(cell);
        Vector2 extents = sprite != null
            ? new Vector2(
                Mathf.Max(0.35f, sprite.bounds.extents.x + decorationOcclusionPaddingX),
                Mathf.Max(0.55f, sprite.bounds.extents.y + decorationOcclusionPaddingY))
            : new Vector2(0.6f, 1.1f);

        Vector3 delta = actorWorldPosition - cellCenter;
        bool withinX = Mathf.Abs(delta.x) <= extents.x;
        bool behindOrInside = delta.y >= 0.1f && delta.y <= extents.y;
        return withinX && behindOrInside;
    }

    public int GetHeightAtCell(Vector3Int cell)
    {
        return SampleCell(cell.x, cell.y).height;
    }

    public IsoBiomeDefinition GetBiomeAtCell(Vector3Int cell)
    {
        return SampleCell(cell.x, cell.y).biome;
    }

    public bool IsTransitionCell(Vector3Int cell)
    {
        return SampleCell(cell.x, cell.y).isTransitionCell;
    }

    public bool IsHeightEdgeCell(Vector3Int cell)
    {
        BiomeSample sample = SampleCell(cell.x, cell.y);
        return IsHeightEdge(cell.x, cell.y, sample.height);
    }

    public GroundCellSample SampleGroundCell(Vector3Int cell)
    {
        BiomeSample sample = SampleCell(cell.x, cell.y);
        Vector3Int topCell = new Vector3Int(cell.x, cell.y, sample.height);
        return new GroundCellSample(topCell, sample.height, sample.biome, sample.isTransitionCell, IsHeightEdge(cell.x, cell.y, sample.height));
    }

    public GroundCellSample SampleWorldPosition(Vector3 worldPosition, int searchHeight = -1)
    {
        Vector3Int cell = WorldToGroundCell(worldPosition, searchHeight < 0 ? MaxSupportedHeight : searchHeight);
        return SampleGroundCell(cell);
    }

    public Vector3Int WorldToGroundCell(Vector3 worldPosition, int searchHeight = -1)
    {
        if (grid == null)
        {
            return Vector3Int.zero;
        }

        if (searchHeight < 0)
        {
            searchHeight = MaxSupportedHeight;
        }

        // Clamp search height to valid range
        searchHeight = Mathf.Clamp(searchHeight, 0, MaxSupportedHeight);

        // In IsometricZAsY, the world position depends on the height (Z).
        // To find the correct cell from a 2D screen/world position, we must iterate 
        // through possible heights (from top to bottom) and check which one "owns" this space.
        for (int h = searchHeight; h >= 0; h--)
        {
            Vector3 testPos = new Vector3(worldPosition.x, worldPosition.y, h * grid.cellSize.z);
            Vector3Int cell = grid.WorldToCell(testPos);
            int actualHeight = GetHeightAtCell(cell);
            
            // If the terrain at this cell is at or above the height we are testing,
            // then we've hit the surface.
            if (actualHeight >= h)
            {
                cell.z = actualHeight;
                return cell;
            }
        }

        // Fallback to ground level
        Vector3 groundPos = new Vector3(worldPosition.x, worldPosition.y, 0f);
        Vector3Int groundCell = grid.WorldToCell(groundPos);
        groundCell.z = GetHeightAtCell(groundCell);
        return groundCell;
    }

    public Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        if (grid == null)
        {
            return Vector3.zero;
        }

        int height = GetHeightAtCell(cell);
        Vector3 center = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, height));
        center.z = height;
        return center;
    }

    public int GetMaxFootprintHeight(Vector3 footWorldPosition, int searchHeight, float footprintRadius)
    {
        if (grid == null)
        {
            return 0;
        }

        int clampedSearchHeight = Mathf.Clamp(searchHeight, 0, MaxSupportedHeight);
        int maxHeight = SampleWorldPosition(footWorldPosition, clampedSearchHeight).Height;
        float r = Mathf.Max(0f, footprintRadius);
        if (r <= 0f)
        {
            return maxHeight;
        }

        Vector3[] offsets =
        {
            new Vector3(r, 0f, 0f),
            new Vector3(-r, 0f, 0f),
            new Vector3(0f, r, 0f),
            new Vector3(0f, -r, 0f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            maxHeight = Mathf.Max(maxHeight, SampleWorldPosition(footWorldPosition + offsets[i], clampedSearchHeight).Height);
        }

        return maxHeight;
    }

    public FootprintMoveEvaluation EvaluateFootprintMove(Vector3 currentFootWorldPosition, Vector3 nextFootWorldPosition, int currentHeight, int maxStepHeight, float footprintRadius)
    {
        return EvaluateFootprintMove(currentFootWorldPosition, nextFootWorldPosition, currentHeight, maxStepHeight, footprintRadius, null);
    }

    public FootprintMoveEvaluation EvaluateFootprintMove(Vector3 currentFootWorldPosition, Vector3 nextFootWorldPosition, int currentHeight, int maxStepHeight, float footprintRadius, Collider2D ignoreCollider)
    {
        if (grid == null)
        {
            return new FootprintMoveEvaluation(Vector3Int.zero, Vector3Int.zero, 0, 0, 0, 0, false, false, "Grid missing");
        }

        int searchBaseHeight = Mathf.Clamp(currentHeight, 0, MaxSupportedHeight);
        GroundCellSample currentSample = SampleWorldPosition(currentFootWorldPosition, searchBaseHeight);
        GroundCellSample targetSample = SampleWorldPosition(nextFootWorldPosition, searchBaseHeight + Mathf.Max(0, maxStepHeight));
        int targetMaxHeight = GetMaxFootprintHeight(nextFootWorldPosition, searchBaseHeight + Mathf.Max(0, maxStepHeight), footprintRadius);
        int heightDelta = targetSample.Height - currentSample.Height;
        bool blockedByHeight = heightDelta > maxStepHeight;
        bool blockedByCollider = !blockedByHeight &&
            IsFootprintBlockedByTerrain(nextFootWorldPosition, currentSample.Height, footprintRadius, ignoreCollider);
        bool isBlocked = blockedByHeight || blockedByCollider;
        string reason = "None";
        if (blockedByHeight)
        {
            reason = $"Step too high: h{currentSample.Height} -> h{targetSample.Height} (max {maxStepHeight})";
        }
        else if (blockedByCollider)
        {
            reason = $"Terrain blocker on height {currentSample.Height}";
        }

        return new FootprintMoveEvaluation(
            currentSample.Cell,
            targetSample.Cell,
            currentSample.Height,
            targetSample.Height,
            targetMaxHeight,
            heightDelta,
            blockedByCollider,
            isBlocked,
            reason);
    }

    public bool CanMoveBetweenPositions(Vector3 currentWorldPosition, Vector3 nextWorldPosition, int maxJumpHeight)
    {
        if (grid == null)
        {
            return true;
        }

        GroundCellSample currentSample = SampleWorldPosition(currentWorldPosition);
        GroundCellSample nextSample = SampleWorldPosition(nextWorldPosition);
        Vector3Int currentCell = currentSample.Cell;
        Vector3Int nextCell = nextSample.Cell;
        if (currentCell.x == nextCell.x && currentCell.y == nextCell.y)
        {
            return true;
        }

        int currentHeight = currentSample.Height;
        int nextHeight = nextSample.Height;
        bool canMove = Mathf.Abs(nextHeight - currentHeight) <= maxJumpHeight;
        if (!canMove && recorder != null)
        {
            recorder.RecordBlockedMove(currentCell, nextCell, currentHeight, nextHeight);
        }

        return canMove;
    }

    public bool CanMoveFootprint(Vector3 currentFootWorldPosition, Vector3 nextFootWorldPosition, int currentHeight, int maxStepHeight, float footprintRadius)
    {
        return CanMoveFootprint(currentFootWorldPosition, nextFootWorldPosition, currentHeight, maxStepHeight, footprintRadius, null);
    }

    public bool CanMoveFootprint(Vector3 currentFootWorldPosition, Vector3 nextFootWorldPosition, int currentHeight, int maxStepHeight, float footprintRadius, Collider2D ignoreCollider)
    {
        if (grid == null)
        {
            return true;
        }

        FootprintMoveEvaluation evaluation = EvaluateFootprintMove(
            currentFootWorldPosition,
            nextFootWorldPosition,
            currentHeight,
            maxStepHeight,
            footprintRadius,
            ignoreCollider);

        if (evaluation.IsBlocked)
        {
            if (recorder != null)
            {
                recorder.RecordBlockedMove(evaluation.FromCell, evaluation.ToCell, evaluation.FromHeight, evaluation.TargetMaxHeight);
            }

            return false;
        }

        return true;
    }

    public bool IsFootprintBlockedByTerrain(Vector3 footWorldPosition, int collisionHeight, float footprintRadius, Collider2D ignoreCollider = null)
    {
        int clampedHeight = Mathf.Clamp(collisionHeight, 0, MaxSupportedHeight);
        int layerIndex = LayerMask.NameToLayer($"Height_{clampedHeight}");
        if (layerIndex < 0)
        {
            return false;
        }

        var filter = new ContactFilter2D();
        filter.SetLayerMask(1 << layerIndex);
        filter.useTriggers = true;
        int hitCount = Physics2D.OverlapCircle(
            new Vector2(footWorldPosition.x, footWorldPosition.y),
            Mathf.Max(0.05f, footprintRadius),
            filter,
            terrainOverlapBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = terrainOverlapBuffer[i];
            if (hit == null || hit == ignoreCollider)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void RefreshChunks(bool force)
    {
        EnsureRecorder();

        if (grid == null || GetDefaultBiome() == null)
        {
            return;
        }

        Vector2Int playerChunk = GetPlayerChunk();
        if (!force && playerChunk == lastPlayerChunk)
        {
            return;
        }

        lastPlayerChunk = playerChunk;

        HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();
        for (int y = -activeRadius; y <= activeRadius; y++)
        {
            for (int x = -activeRadius; x <= activeRadius; x++)
            {
                Vector2Int coord = new Vector2Int(playerChunk.x + x, playerChunk.y + y);
                neededChunks.Add(coord);

                if (!loadedChunks.ContainsKey(coord))
                {
                    LoadChunk(coord);
                }
            }
        }

        List<Vector2Int> chunksToUnload = new List<Vector2Int>();
        foreach (var kvp in loadedChunks)
        {
            if (!neededChunks.Contains(kvp.Key))
            {
                chunksToUnload.Add(kvp.Key);
            }
        }

        foreach (Vector2Int coord in chunksToUnload)
        {
            UnloadChunk(coord);
        }
    }

    private void RefreshVisibleChunks()
    {
        if (loadedChunks.Count == 0)
            return;

        if (Time.unscaledTime < nextVisibleChunkRefreshTime)
            return;

        if (visibilityCamera == null)
            visibilityCamera = Camera.main;
        if (visibilityCamera == null)
            return;

        Bounds visibleBounds = GetExpandedCameraWorldBounds(visibilityCamera, 1.25f);
        foreach (var kvp in loadedChunks)
        {
            kvp.Value.isVisibleToCamera = kvp.Value.worldBounds.Intersects(visibleBounds);
        }

        nextVisibleChunkRefreshTime = Time.unscaledTime + 0.08f;
    }

    private Bounds GetExpandedCameraWorldBounds(Camera camera, float padding)
    {
        float halfHeight = camera.orthographicSize + padding;
        float halfWidth = halfHeight * camera.aspect + padding;
        Vector3 center = camera.transform.position;
        center.z = 0f;
        Vector3 size = new Vector3(halfWidth * 2f, halfHeight * 2f, 8f);
        return new Bounds(center, size);
    }

    private Vector2Int GetPlayerChunk()
    {
        Vector3 samplePosition = player != null ? player.position : Vector3.zero;
        Vector3Int playerCell = grid.WorldToCell(samplePosition);
        return CellToChunkCoord(playerCell.x, playerCell.y);
    }

    private Vector2Int CellToChunkCoord(int cellX, int cellY)
    {
        return new Vector2Int(FloorDiv(cellX, chunkSize), FloorDiv(cellY, chunkSize));
    }

    private Bounds BuildChunkWorldBounds(Vector2Int coord)
    {
        Vector3Int minCell = new Vector3Int(coord.x * chunkSize, coord.y * chunkSize, 0);
        Vector3Int maxCell = new Vector3Int(coord.x * chunkSize + chunkSize - 1, coord.y * chunkSize + chunkSize - 1, maxTerrainHeight);

        Vector3 a = GetCellCenterWorld(minCell);
        Vector3 b = GetCellCenterWorld(maxCell);
        Vector3 center = (a + b) * 0.5f;
        Vector3 size = new Vector3(
            Mathf.Abs(b.x - a.x) + 3f,
            Mathf.Abs(b.y - a.y) + 3f,
            Mathf.Max(4f, Mathf.Abs(b.z - a.z) + 4f));

        return new Bounds(center, size);
    }

    private int FloorDiv(int value, int divisor)
    {
        if (value >= 0)
        {
            return value / divisor;
        }

        return -((-value + divisor - 1) / divisor);
    }

    private void LoadChunk(Vector2Int coord)
    {
        ChunkHandle handle = poolInactiveChunks && pooledChunks.Count > 0
            ? pooledChunks.Pop()
            : CreateChunkHandle();

        handle.gameObject.name = $"Chunk {coord.x}, {coord.y}";
        handle.gameObject.SetActive(true);
        handle.worldBounds = BuildChunkWorldBounds(coord);
        handle.isVisibleToCamera = true;
        loadedChunks.Add(coord, handle);

        PaintChunk(coord, handle);
        if (recorder != null)
        {
            recorder.RecordChunkLoaded(coord);
        }
    }

    private void UnloadChunk(Vector2Int coord)
    {
        ChunkHandle handle = loadedChunks[coord];
        loadedChunks.Remove(coord);

        ClearChunk(handle);
        if (poolInactiveChunks)
        {
            handle.gameObject.SetActive(false);
            pooledChunks.Push(handle);
        }
        else
        {
            Destroy(handle.gameObject);
        }

        if (recorder != null)
        {
            recorder.RecordChunkUnloaded(coord);
        }
    }

    private ChunkHandle CreateChunkHandle()
    {
        GameObject chunkObject = new GameObject("Chunk");
        chunkObject.transform.SetParent(transform);
        chunkObject.transform.localPosition = Vector3.zero;

        // Sorting orders must be distinct so the renderer resolves depth unambiguously.
        // Ground(0) < Elevation(5) < DecoShadow(8) < Decorations(10) < HeightShadows(15) < Borders(20)
        Tilemap ground = CreateTilemapLayer(chunkObject.transform, "Ground", true, 0);
        Tilemap elevation = CreateTilemapLayer(chunkObject.transform, "Elevation", true, 5);
        Tilemap decoShadows = CreateTilemapLayer(chunkObject.transform, "Decoration Shadows", true, 8);
        Tilemap decorations = CreateTilemapLayer(chunkObject.transform, "Decorations", true, 10, decorationMaterial);
        Tilemap heightShadows = CreateTilemapLayer(chunkObject.transform, "Height Edge Shadows", true, 15);
        Tilemap borders = CreateTilemapLayer(chunkObject.transform, "Tile Borders", true, 20);

        // Create one collider tilemap per supported integer height layer.
        Tilemap[] colliderLayers = new Tilemap[HeightLayerCount];
        for (int i = 0; i < HeightLayerCount; i++)
        {
            colliderLayers[i] = CreateTilemapLayer(chunkObject.transform, $"Collision_H{i}", false, 0);
            colliderLayers[i].transform.localPosition = Vector3.zero;
            colliderLayers[i].gameObject.layer = LayerMask.NameToLayer($"Height_{i}");

            Rigidbody2D colliderRb = colliderLayers[i].gameObject.AddComponent<Rigidbody2D>();
            colliderRb.bodyType = RigidbodyType2D.Static;

            TilemapCollider2D tilemapCollider = colliderLayers[i].gameObject.AddComponent<TilemapCollider2D>();
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            CompositeCollider2D composite = colliderLayers[i].gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        return new ChunkHandle
        {
            gameObject = chunkObject,
            groundTilemap = ground,
            elevationTilemap = elevation,
            decorationTilemap = decorations,
            decorationShadowTilemap = decoShadows,
            heightShadowTilemap = heightShadows,
            borderTilemap = borders,
            heightColliderTilemaps = colliderLayers
        };
    }

    private Tilemap CreateTilemapLayer(Transform parent, string layerName, bool visible, int sortingOrder, Material material = null)
    {
        GameObject layerObject = new GameObject(layerName);
        layerObject.transform.SetParent(parent);
        layerObject.transform.localPosition = Vector3.zero;

        Tilemap tilemap = layerObject.AddComponent<Tilemap>();
        tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);

        TilemapRenderer renderer = layerObject.AddComponent<TilemapRenderer>();
        renderer.enabled = visible;
        renderer.mode = TilemapRenderer.Mode.Individual;
        renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
        renderer.sortingOrder = sortingOrder;

        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
        else if (worldMaterial != null)
        {
            renderer.sharedMaterial = worldMaterial;
        }

        return tilemap;
    }

    private void ClearChunk(ChunkHandle handle)
    {
        handle.fadedDecorationCells.Clear();
        handle.pendingFadedDecorationCells.Clear();
        handle.groundTilemap.ClearAllTiles();
        handle.elevationTilemap.ClearAllTiles();
        handle.decorationTilemap.ClearAllTiles();
        handle.decorationShadowTilemap.ClearAllTiles();
        handle.heightShadowTilemap.ClearAllTiles();
        handle.borderTilemap.ClearAllTiles();
        
        if (handle.heightColliderTilemaps != null)
        {
            foreach (var tm in handle.heightColliderTilemaps)
            {
                if (tm != null) tm.ClearAllTiles();
            }
        }

        for (int i = handle.spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (handle.spawnedObjects[i] != null)
            {
                ReturnEnemyToPool(handle.spawnedObjects[i]);
            }
        }
        handle.spawnedObjects.Clear();
    }

    private void PaintChunk(Vector2Int coord, ChunkHandle handle)
    {
        ClearChunk(handle);

        int startX = coord.x * chunkSize;
        int startY = coord.y * chunkSize;
        int cellCount = chunkSize * chunkSize;

        NativeArray<BiomeData> biomeData = new NativeArray<BiomeData>(biomes.Length, Allocator.TempJob);
        for (int i = 0; i < biomes.Length; i++)
        {
            biomeData[i] = new BiomeData
            {
                kind = biomes[i].EffectiveBiomeKind,
                temperatureMin = biomes[i].temperatureMin,
                temperatureMax = biomes[i].temperatureMax,
                moistureMin = biomes[i].moistureMin,
                moistureMax = biomes[i].moistureMax,
                elevationMin = biomes[i].elevationMin,
                elevationMax = biomes[i].elevationMax,
                raisedTileThreshold = biomes[i].raisedTileThreshold,
                heightNoiseScale = biomes[i].heightNoiseScale,
                biomeNoiseOffset = biomes[i].biomeNoiseOffset
            };
        }

        NativeArray<CellData> jobResults = new NativeArray<CellData>(cellCount, Allocator.TempJob);
        int starterIdx = useStarterZone ? FindBiomeIndex(starterZoneBiome) : -1;

        ChunkDataJob job = new ChunkDataJob
        {
            startX = startX,
            startY = startY,
            chunkSize = chunkSize,
            seed = seed,
            biomeDefinitions = biomeData,
            biomeNoiseScale = biomeNoiseScale,
            temperatureNoiseScale = temperatureNoiseScale,
            moistureNoiseScale = moistureNoiseScale,
            continentNoiseScale = continentNoiseScale,
            biomeBlendRadius = biomeBlendRadius,
            biomeBlendNoiseScale = biomeBlendNoiseScale,
            terrainHeightFalloff = terrainHeightFalloff,
            lowlandMaxTerrainHeight = lowlandMaxTerrainHeight,
            maxTerrainHeight = maxTerrainHeight,
            starterEnabled = (useStarterZone && starterIdx >= 0) ? 1 : 0,
            starterCenterX = starterZoneCenter.x,
            starterCenterY = starterZoneCenter.y,
            starterRadius = starterZoneRadius,
            starterFlatten = starterZoneFlattenRadius,
            starterRamp = starterZoneRampStep,
            starterBiomeIndex = starterIdx,
            results = jobResults
        };

        JobHandle jobHandle = job.Schedule(cellCount, 64);
        jobHandle.Complete();

        Vector3Int[] groundPositions = new Vector3Int[cellCount];
        TileBase[] groundTiles = new TileBase[cellCount];

        List<Vector3Int> elevationPositions = new List<Vector3Int>(cellCount / 3);
        List<TileBase> elevationTiles = new List<TileBase>(cellCount / 3);
        List<Vector3Int> decorationPositions = new List<Vector3Int>();
        List<TileBase> decorationTiles = new List<TileBase>();
        List<Vector3Int> decoShadowPositions = new List<Vector3Int>();
        List<TileBase> decoShadowTiles = new List<TileBase>();
        TileBase decoShadow = showDecorationShadows ? GetDecorationShadowTile() : null;
        List<Vector3Int> shadowPositions = new List<Vector3Int>();
        List<TileBase> shadowTiles = new List<TileBase>();
        List<Vector3Int> borderPositions = new List<Vector3Int>(cellCount);
        List<TileBase> borderTiles = new List<TileBase>(cellCount);
        
        Dictionary<Vector3Int, TileBase>[] colliderTileMaps = new Dictionary<Vector3Int, TileBase>[HeightLayerCount];
        for (int j = 0; j < HeightLayerCount; j++) colliderTileMaps[j] = new Dictionary<Vector3Int, TileBase>();

        TileBase borderTile = showTileBorders ? GetTileBorderTile() : null;
        TileBase shadowTile = showHeightEdgeShadows ? GetHeightEdgeShadowTile() : null;
        int spawnedSlimes = 0;
        int spawnedRareSlimes = 0;
        int spawnedBossSlimes = 0;

        for (int i = 0; i < cellCount; i++)
        {
            CellData data = jobResults[i];
            int worldX = startX + (i % chunkSize);
            int worldY = startY + (i / chunkSize);

            BiomeSample sample = new BiomeSample
            {
                biome = biomes[data.primaryBiomeIndex],
                secondaryBiome = data.secondaryBiomeIndex >= 0 ? biomes[data.secondaryBiomeIndex] : null,
                blendStrength = data.blendStrength,
                isTransitionCell = data.isTransition,
                height = data.height
            };

            groundPositions[i] = new Vector3Int(worldX, worldY, 0);
            groundTiles[i] = PickSurfaceTile(sample, worldX, worldY, raised: false);

            if (borderTile != null)
            {
                borderPositions.Add(new Vector3Int(worldX, worldY, sample.height));
                borderTiles.Add(borderTile);
            }

            if (sample.height > 0)
            {
                // Each z-level picks a tile appropriate for that elevation:
                // z=1 → grass/sand edge, z=2 → rock mid-layer, z=3+ → snow/lava peak.
                // Falls back to raisedRuleTile when mid/peak tiles aren't assigned.
                IsoBiomeDefinition tileBiome = PickTransitionBiome(sample, worldX, worldY) ?? sample.biome;
                if (tileBiome != null)
                {
                    for (int z = 1; z <= sample.height; z++)
                    {
                        TileBase heightTile = tileBiome.GetTileForHeight(z, worldX, worldY, seed);
                        if (heightTile != null)
                        {
                            elevationPositions.Add(new Vector3Int(worldX, worldY, z));
                            elevationTiles.Add(heightTile);
                        }
                    }
                }

                AddCliffColliders(worldX, worldY, sample, colliderTileMaps);

                AddHeightEdgeShadow(worldX, worldY, sample, new Vector2Int(0, -1), shadowTile, shadowPositions, shadowTiles);
                AddHeightEdgeShadow(worldX, worldY, sample, new Vector2Int(-1, 0), shadowTile, shadowPositions, shadowTiles);
                AddHeightEdgeShadow(worldX, worldY, sample, new Vector2Int(0, 1), shadowTile, shadowPositions, shadowTiles);
                AddHeightEdgeShadow(worldX, worldY, sample, new Vector2Int(1, 0), shadowTile, shadowPositions, shadowTiles);

            }

            // Decorations — clustered placement (forest groves + light ground cover +
            // rare rock clumps). Excluded from height-edge cells so nothing drapes over
            // a cliff. Works for flat ground and flat hill-tops alike.
            if (sample.biome != null && !IsHeightEdge(worldX, worldY, sample.height))
            {
                TileBase deco = null;
                if (sample.biome.HasCategorisedDecorations)
                {
                    float densityScale = sample.isTransitionCell ? 0.4f : 1f;
                    deco = sample.biome.GetClusteredDecoration(worldX, worldY, seed, densityScale);
                }
                else if (ShouldPlaceDecoration(sample, worldX, worldY))
                {
                    deco = sample.biome.GetDecorationTile(worldX, worldY, seed);
                }

                if (deco != null)
                {
                    Vector3Int decoPos = new Vector3Int(worldX, worldY, sample.height);
                    decorationPositions.Add(decoPos);
                    decorationTiles.Add(deco);
                    if (decoShadow != null)
                    {
                        decoShadowPositions.Add(decoPos);
                        decoShadowTiles.Add(decoShadow);
                    }
                }
            }

            if (TryPickSlimeSpawn(sample, worldX, worldY, spawnedSlimes, spawnedRareSlimes, spawnedBossSlimes, out EnemyDefinition slimeDefinition))
            {
                SpawnEnemyInChunk(handle, slimeDefinition, new Vector3Int(worldX, worldY, sample.height));
                spawnedSlimes++;
                if (slimeDefinition == rareSlime) spawnedRareSlimes++;
                else if (slimeDefinition == bossSlime) spawnedBossSlimes++;
            }
        }

        biomeData.Dispose();
        jobResults.Dispose();

        handle.groundTilemap.SetTiles(groundPositions, groundTiles);
        handle.elevationTilemap.SetTiles(elevationPositions.ToArray(), elevationTiles.ToArray());
        handle.decorationShadowTilemap.SetTiles(decoShadowPositions.ToArray(), decoShadowTiles.ToArray());
        handle.decorationTilemap.SetTiles(decorationPositions.ToArray(), decorationTiles.ToArray());
        handle.heightShadowTilemap.SetTiles(shadowPositions.ToArray(), shadowTiles.ToArray());
        handle.borderTilemap.SetTiles(borderPositions.ToArray(), borderTiles.ToArray());

        for (int i = 0; i < decorationPositions.Count; i++)
        {
            Vector3Int cell = decorationPositions[i];
            handle.decorationTilemap.SetTileFlags(cell, TileFlags.None);
            handle.decorationTilemap.SetColor(cell, Color.white);
        }
        handle.fadedDecorationCells.Clear();
        handle.pendingFadedDecorationCells.Clear();

        for (int h = 0; h < HeightLayerCount; h++)
        {
            var currentMap = colliderTileMaps[h];
            Vector3Int[] colliderPositions = new Vector3Int[currentMap.Count];
            TileBase[] colliderTiles = new TileBase[currentMap.Count];
            int colliderIndex = 0;
            foreach (var kvp in currentMap)
            {
                colliderPositions[colliderIndex] = kvp.Key;
                colliderTiles[colliderIndex] = kvp.Value;
                colliderIndex++;
            }
            handle.heightColliderTilemaps[h].SetTiles(colliderPositions, colliderTiles);
            
            // Force geometry generation to ensure physics are solid immediately
            var composite = handle.heightColliderTilemaps[h].GetComponent<CompositeCollider2D>();
            if (composite != null)
            {
                composite.GenerateGeometry();
            }
        }
    }

    private void AddCliffColliders(int worldX, int worldY, BiomeSample sample, Dictionary<Vector3Int, TileBase>[] colliderTileMaps)
    {
        if (sample.height <= 0)
        {
            return;
        }

        AddDirectionalCliffCollider(worldX, worldY, sample, new Vector2Int(0, -1), sample.biome != null ? sample.biome.colliderCliffSouth : null, colliderTileMaps);
        AddDirectionalCliffCollider(worldX, worldY, sample, new Vector2Int(-1, 0), sample.biome != null ? sample.biome.colliderCliffWest : null, colliderTileMaps);
        AddDirectionalCliffCollider(worldX, worldY, sample, new Vector2Int(0, 1), sample.biome != null ? sample.biome.colliderCliffNorth : null, colliderTileMaps);
        AddDirectionalCliffCollider(worldX, worldY, sample, new Vector2Int(1, 0), sample.biome != null ? sample.biome.colliderCliffEast : null, colliderTileMaps);
    }

    private void AddDirectionalCliffCollider(
        int worldX,
        int worldY,
        BiomeSample sample,
        Vector2Int neighbourOffset,
        TileBase directionalTile,
        Dictionary<Vector3Int, TileBase>[] colliderTileMaps)
    {
        BiomeSample neighbour = SampleCell(worldX + neighbourOffset.x, worldY + neighbourOffset.y);
        if (neighbour.height >= sample.height)
        {
            return;
        }

        TileBase colliderTile = directionalTile != null ? directionalTile : cliffColliderTile;
        if (colliderTile == null)
        {
            return;
        }

        int startHeight = Mathf.Clamp(neighbour.height, 0, HeightLayerCount - 1);
        int endHeightExclusive = Mathf.Clamp(sample.height, 0, HeightLayerCount);
        for (int h = startHeight; h < endHeightExclusive; h++)
        {
            Vector3Int colliderPos = new Vector3Int(worldX, worldY, h);
            Dictionary<Vector3Int, TileBase> currentMap = colliderTileMaps[h];
            if (currentMap.TryGetValue(colliderPos, out TileBase existing))
            {
                if (existing != colliderTile && cliffColliderTile != null)
                {
                    currentMap[colliderPos] = cliffColliderTile;
                }

                continue;
            }

            currentMap.Add(colliderPos, colliderTile);
        }
    }

    private bool TryPickSlimeSpawn(
        BiomeSample sample,
        int worldX,
        int worldY,
        int spawnedSlimes,
        int spawnedRareSlimes,
        int spawnedBossSlimes,
        out EnemyDefinition slimeDefinition)
    {
        slimeDefinition = null;
        if (!spawnSlimesInPlains || spawnedSlimes >= maxSlimesPerChunk)
        {
            return false;
        }

        if (!IsPlainSlimeEligibleCell(sample))
        {
            return false;
        }

        if (bossSlime != null &&
            spawnedBossSlimes < maxBossSlimesPerChunk &&
            IsBossSlimeSpawnWinner(worldX, worldY))
        {
            slimeDefinition = bossSlime;
            return true;
        }

        if (rareSlime != null &&
            spawnedRareSlimes < maxRareSlimesPerChunk &&
            Hash01(worldX, worldY, seed, 232) < rareSlimeChance)
        {
            slimeDefinition = rareSlime;
            return true;
        }

        if (commonSlime != null && Hash01(worldX, worldY, seed, 231) < commonSlimeChance)
        {
            slimeDefinition = commonSlime;
            return true;
        }

        return false;
    }

    private bool IsPlainSlimeEligibleCell(BiomeSample sample)
    {
        if (sample.biome == null || sample.biome.EffectiveBiomeKind != BiomeKind.Plains)
        {
            return false;
        }

        if (sample.isTransitionCell || sample.height != 0)
        {
            return false;
        }

        return true;
    }

    private bool IsBossSlimeSpawnWinner(int worldX, int worldY)
    {
        if (!IsBossSlimeRawCandidate(worldX, worldY))
        {
            return false;
        }

        int radius = Mathf.Max(1, bossSlimeExclusionRadius);
        float ownPriority = Hash01(worldX, worldY, seed, 234);
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                int neighbourX = worldX + x;
                int neighbourY = worldY + y;
                if (!IsBossSlimeRawCandidate(neighbourX, neighbourY))
                {
                    continue;
                }

                float neighbourPriority = Hash01(neighbourX, neighbourY, seed, 234);
                if (neighbourPriority > ownPriority)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsBossSlimeRawCandidate(int worldX, int worldY)
    {
        if (bossSlime == null || bossSlimeChance <= 0f)
        {
            return false;
        }

        BiomeSample sample = SampleCell(worldX, worldY);
        return IsPlainSlimeEligibleCell(sample) && Hash01(worldX, worldY, seed, 233) < bossSlimeChance;
    }

    private void SpawnEnemyInChunk(ChunkHandle handle, EnemyDefinition definition, Vector3Int cell)
    {
        if (definition == null)
        {
            return;
        }

        GameObject enemy = GetEnemyFromPool(definition);
        enemy.transform.SetParent(handle.gameObject.transform);
        enemy.transform.position = GetCellCenterWorld(cell);
        handle.spawnedObjects.Add(enemy);
    }

    private TileBase GetTileBorderTile()
    {
        if (tileBorderTile != null)
        {
            return tileBorderTile;
        }

        const int width = 64;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = clear;
        }
        texture.SetPixels(pixels);

        Color line = Color.white;
        Vector2Int top = new Vector2Int(width / 2, 0);
        Vector2Int right = new Vector2Int(width - 1, height / 2);
        Vector2Int bottom = new Vector2Int(width / 2, height - 1);
        Vector2Int left = new Vector2Int(0, height / 2);
        DrawTextureLine(texture, top, right, line, 0);
        DrawTextureLine(texture, right, bottom, line, 0);
        DrawTextureLine(texture, bottom, left, line, 0);
        DrawTextureLine(texture, left, top, line, 0);
texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            64f);

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = "Runtime Tile Border";
        tile.sprite = sprite;
        tile.color = tileBorderColor;
        tile.colliderType = Tile.ColliderType.None;
        tileBorderTile = tile;
        return tileBorderTile;
    }

    private TileBase GetHeightEdgeShadowTile()
    {
        if (heightEdgeShadowTile != null)
        {
            return heightEdgeShadowTile;
        }

        const int width = 64;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        texture.SetPixels(pixels);

        Color shadow = Color.white;
        for (int y = height / 2; y < height - 2; y++)
        {
            float halfWidth = Mathf.Lerp(width * 0.48f, 4f, (y - height / 2f) / (height / 2f));
            int minX = Mathf.RoundToInt(width / 2f - halfWidth);
            int maxX = Mathf.RoundToInt(width / 2f + halfWidth);
            for (int x = minX; x <= maxX; x++)
            {
                if (x >= 0 && x < width)
                    texture.SetPixel(x, y, shadow);
            }
        }
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 64f);
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = "Runtime Height Edge Shadow";
        tile.sprite = sprite;
        tile.color = heightEdgeShadowColor;
        tile.colliderType = Tile.ColliderType.None;
        heightEdgeShadowTile = tile;
        return heightEdgeShadowTile;
    }

    /// <summary>
    /// Soft elliptical drop shadow stamped under decoration props. Generated once
    /// and shared. The ellipse has a solid core with a soft alpha falloff at the
    /// edge so props read as "planted" on the ground rather than pasted on top.
    /// </summary>
    private TileBase GetDecorationShadowTile()
    {
        if (decorationShadowTile != null)
        {
            return decorationShadowTile;
        }

        const int width = 96;
        const int height = 48;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear; // soft edges, not point
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float rx = width * 0.46f;
        float ry = height * 0.46f;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / rx;
                float ny = (y - center.y) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny); // 0 at centre, 1 at edge
                float a = 1f - Mathf.SmoothStep(0.55f, 1f, d); // solid core, soft rim
                pixels[y * width + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 64f);
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = "Runtime Decoration Shadow";
        tile.sprite = sprite;
        // White so the per-frame Tilemap.color (set in UpdateDecorationShadowDirection)
        // fully controls the shadow's colour and day/night fade.
        tile.color = Color.white;
        tile.colliderType = Tile.ColliderType.None;
        decorationShadowTile = tile;
        return decorationShadowTile;
    }

    private static void DrawTextureLine(Texture2D texture, Vector2Int start, Vector2Int end, Color color, int brushRadius)
    {
        int x0 = start.x;
        int y0 = start.y;
        int x1 = end.x;
        int y1 = end.y;
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            DrawTextureBrush(texture, x0, y0, brushRadius, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void DrawTextureBrush(Texture2D texture, int centerX, int centerY, int radius, Color color)
    {
        int clampedRadius = Mathf.Max(0, radius);
        for (int y = centerY - clampedRadius; y <= centerY + clampedRadius; y++)
        {
            for (int x = centerX - clampedRadius; x <= centerX + clampedRadius; x++)
            {
                if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
                {
                    continue;
                }

                if (Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY) <= clampedRadius)
                    texture.SetPixel(x, y, color);
            }
        }
    }

    private void AddHeightEdgeShadow(
        int worldX,
        int worldY,
        BiomeSample sample,
        Vector2Int neighbourOffset,
        TileBase shadowTile,
        List<Vector3Int> shadowPositions,
        List<TileBase> shadowTiles)
    {
        if (shadowTile == null)
        {
            return;
        }

        int neighbourX = worldX + neighbourOffset.x;
        int neighbourY = worldY + neighbourOffset.y;
        int neighbourHeight = SampleCell(neighbourX, neighbourY).height;
        if (neighbourHeight >= sample.height)
        {
            return;
        }

        shadowPositions.Add(new Vector3Int(neighbourX, neighbourY, neighbourHeight));
        shadowTiles.Add(shadowTile);
    }

    private BiomeSample SampleCell(int worldX, int worldY)
    {
        ClimateSample climate = SampleClimate(worldX, worldY);
        IsoBiomeDefinition primaryBiome = SelectBiomeFromClimate(climate);
        BiomeSample sample = SampleBiomeBlend(worldX, worldY, primaryBiome, climate);

        // Starter zone override — MUST mirror the Burst job exactly so player
        // collision/height queries agree with the painted chunk tiles.
        bool inStarter = false;
        float starterDist = 0f;
        if (useStarterZone)
        {
            IsoBiomeDefinition starter = GetStarterBiome();
            if (starter != null)
            {
                float dx = worldX - starterZoneCenter.x;
                float dy = worldY - starterZoneCenter.y;
                float d2 = dx * dx + dy * dy;
                if (d2 <= (float)starterZoneRadius * starterZoneRadius)
                {
                    inStarter = true;
                    starterDist = Mathf.Sqrt(d2);
                    sample.biome = starter;
                    sample.secondaryBiome = null;
                    sample.blendStrength = 0f;
                    sample.isTransitionCell = false;
                    sample.surfaceKind = SurfaceKind.Primary;
                }
            }
        }

        float x = (worldX + seed * 0.13f + sample.biome.biomeNoiseOffset) * sample.biome.heightNoiseScale;
        float y = (worldY - seed * 0.17f - sample.biome.biomeNoiseOffset) * sample.biome.heightNoiseScale;
        float localNoise = Mathf.PerlinNoise(x, y);
        float heightNoise = Mathf.Lerp(localNoise, climate.continentalHeight, GetContinentalHeightWeight(sample.biome));

        int rawHeight = CalculateTerrainHeight(heightNoise, sample.biome);
        sample.height = SmoothHillHeight(worldX, worldY, rawHeight);

        if (inStarter)
        {
            if (starterDist <= starterZoneFlattenRadius)
            {
                sample.height = 0;
            }
            else
            {
                int cap = Mathf.FloorToInt((starterDist - starterZoneFlattenRadius) / Mathf.Max(1, starterZoneRampStep));
                sample.height = Mathf.Min(sample.height, Mathf.Max(0, cap));
            }
        }

        return sample;
    }

    private int SmoothHillHeight(int worldX, int worldY, int rawHeight)
    {
        if (rawHeight <= 1)
        {
            return rawHeight;
        }

        int minNeighbour = rawHeight;
        minNeighbour = Mathf.Min(minNeighbour, CalculateRawTerrainHeight(worldX + 1, worldY));
        minNeighbour = Mathf.Min(minNeighbour, CalculateRawTerrainHeight(worldX - 1, worldY));
        minNeighbour = Mathf.Min(minNeighbour, CalculateRawTerrainHeight(worldX, worldY + 1));
        minNeighbour = Mathf.Min(minNeighbour, CalculateRawTerrainHeight(worldX, worldY - 1));
        return Mathf.Min(rawHeight, minNeighbour + 1);
    }

    private int CalculateRawTerrainHeight(int worldX, int worldY)
    {
        ClimateSample climate = SampleClimate(worldX, worldY);
        IsoBiomeDefinition primaryBiome = SelectBiomeFromClimate(climate);
        BiomeSample sample = SampleBiomeBlend(worldX, worldY, primaryBiome, climate);
        float x = (worldX + seed * 0.13f + sample.biome.biomeNoiseOffset) * sample.biome.heightNoiseScale;
        float y = (worldY - seed * 0.17f - sample.biome.biomeNoiseOffset) * sample.biome.heightNoiseScale;
        float localNoise = Mathf.PerlinNoise(x, y);
        float heightNoise = Mathf.Lerp(localNoise, climate.continentalHeight, GetContinentalHeightWeight(sample.biome));
        return CalculateTerrainHeight(heightNoise, sample.biome);
    }

    /// <summary>
    /// Maps a 0-1 noise value to a discrete terrain height.
    ///
    /// Below the biome's raisedTileThreshold: height 0 (flat ground).
    /// Above the threshold: remap [threshold, 1] → [1, maxTerrainHeight].
    /// A falloff exponent gives more granular control over peak shape:
    ///   - 1.0 = linear (uniform distribution of heights)
    ///   - 1.4 = slight bias toward lower terrain (default, looks natural)
    ///   - 2.0 = strong bias toward lower terrain (rare peaks, mostly mid-height)
    ///   - 0.5 = bias toward HIGH terrain (rare valleys, mostly tall)
    /// </summary>
    private int CalculateTerrainHeight(float heightNoise, IsoBiomeDefinition biome)
    {
        int biomeMaxHeight = GetMaxHeightForBiome(biome);
        if (biomeMaxHeight <= 1)
        {
            // Legacy behavior: binary flat / raised
            return heightNoise >= biome.raisedTileThreshold ? 1 : 0;
        }

        float threshold = biome.raisedTileThreshold;
        if (heightNoise < threshold)
        {
            return 0;
        }

        // Map [threshold, 1] → [0, 1] then apply falloff exponent
        float t = Mathf.Clamp01((heightNoise - threshold) / Mathf.Max(0.01f, 1f - threshold));
        t = Mathf.Pow(t, terrainHeightFalloff);

        // Map to integer height [1, biomeMaxHeight]
        int height = 1 + Mathf.FloorToInt(t * (biomeMaxHeight - 1 + 0.999f));
        return Mathf.Clamp(height, 1, biomeMaxHeight);
    }

    private int GetMaxHeightForBiome(IsoBiomeDefinition biome)
    {
        if (biome == null)
        {
            return lowlandMaxTerrainHeight;
        }

        return biome.EffectiveBiomeKind == BiomeKind.FrozenMountain
            ? maxTerrainHeight
            : lowlandMaxTerrainHeight;
    }

    private ClimateSample SampleClimate(int worldX, int worldY)
    {
        return new ClimateSample
        {
            temperature = Mathf.PerlinNoise((worldX + seed * 0.19f) * temperatureNoiseScale, (worldY - seed * 0.23f) * temperatureNoiseScale),
            moisture = Mathf.PerlinNoise((worldX - seed * 0.31f) * moistureNoiseScale, (worldY + seed * 0.29f) * moistureNoiseScale),
            continentalHeight = Mathf.PerlinNoise((worldX + seed * 0.41f) * continentNoiseScale, (worldY - seed * 0.37f) * continentNoiseScale)
        };
    }

    private IsoBiomeDefinition SelectBiomeFromClimate(ClimateSample climate)
    {
        EnsureBiomeArray();
        if (biomes == null || biomes.Length == 0)
        {
            return plainsBiome;
        }

        // V1 overworld is intentionally limited to Plains, Desert, and Frozen Mountain.
        // Cave/Temple remain asset biomes, but they should not leak into natural terrain.
        BiomeKind targetKind;

        if (climate.continentalHeight > 0.74f || climate.temperature < 0.28f)
        {
            targetKind = BiomeKind.FrozenMountain;
        }
        else if (climate.temperature > 0.58f && climate.moisture < 0.42f)
        {
            targetKind = BiomeKind.Desert;
        }
        else
        {
            targetKind = BiomeKind.Plains;
        }

        IsoBiomeDefinition biome = FindBiome(targetKind);
        if (biome != null && biome.MatchesClimate(climate.temperature, climate.moisture, climate.continentalHeight))
        {
            return biome;
        }

        return biome != null ? biome : FindBiome(BiomeKind.Plains) ?? GetDefaultBiome();
    }

    private BiomeSample SampleBiomeBlend(int worldX, int worldY, IsoBiomeDefinition primaryBiome, ClimateSample climate)
    {
        BiomeSample sample = new BiomeSample
        {
            biome = primaryBiome,
            secondaryBiome = null,
            blendStrength = 0f,
            isTransitionCell = false,
            surfaceKind = SurfaceKind.Primary,
            height = 0
        };

        if (primaryBiome == null)
        {
            return sample;
        }

        int radius = Mathf.Max(1, biomeBlendRadius);
        IsoBiomeDefinition strongestNeighbour = null;
        int strongestCount = 0;
        int checkedCells = 0;
        for (int y = -radius; y <= radius; y += radius)
        {
            for (int x = -radius; x <= radius; x += radius)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                ClimateSample neighbourClimate = SampleClimate(worldX + x, worldY + y);
                IsoBiomeDefinition neighbourBiome = SelectBiomeFromClimate(neighbourClimate);
                checkedCells++;
                if (neighbourBiome == null || SameBiome(primaryBiome, neighbourBiome))
                {
                    continue;
                }

                int count = CountNeighbourBiome(worldX, worldY, radius, neighbourBiome);
                if (count > strongestCount)
                {
                    strongestCount = count;
                    strongestNeighbour = neighbourBiome;
                }
            }
        }

        if (strongestNeighbour == null || checkedCells == 0)
        {
            return sample;
        }

        float neighbourRatio = Mathf.Clamp01(strongestCount / 8f);
        float edgeNoise = Mathf.PerlinNoise((worldX + seed * 0.53f) * biomeBlendNoiseScale, (worldY - seed * 0.47f) * biomeBlendNoiseScale);
        float blendStrength = Mathf.Clamp01((neighbourRatio * 0.75f) + (edgeNoise * 0.25f));
        if (blendStrength < 0.25f)
        {
            return sample;
        }

        sample.secondaryBiome = strongestNeighbour;
        sample.blendStrength = blendStrength;
        sample.isTransitionCell = true;
        sample.surfaceKind = blendStrength > 0.65f ? SurfaceKind.Secondary : SurfaceKind.Transition;
        return sample;
    }

    private int CountNeighbourBiome(int worldX, int worldY, int radius, IsoBiomeDefinition targetBiome)
    {
        int count = 0;
        for (int y = -radius; y <= radius; y += radius)
        {
            for (int x = -radius; x <= radius; x += radius)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                IsoBiomeDefinition biome = SelectBiomeFromClimate(SampleClimate(worldX + x, worldY + y));
                if (biome != null && SameBiome(biome, targetBiome))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private TileBase PickSurfaceTile(BiomeSample sample, int worldX, int worldY, bool raised)
    {
        IsoBiomeDefinition tileBiome = PickTransitionBiome(sample, worldX, worldY);
        if (tileBiome == null)
        {
            tileBiome = sample.biome;
        }

        return raised
            ? tileBiome.GetRaisedGroundTile(worldX, worldY, seed)
            : tileBiome.GetFlatGroundTile(worldX, worldY, seed);
    }

    private IsoBiomeDefinition PickTransitionBiome(BiomeSample sample, int worldX, int worldY)
    {
        if (!sample.isTransitionCell || sample.secondaryBiome == null)
        {
            return sample.biome;
        }

        BiomeTransitionRule rule = GetTransitionRule(sample.biome, sample.secondaryBiome);
        float secondaryWeight = transitionTileChance * sample.blendStrength;
        if (rule != null)
        {
            float total = Mathf.Max(0.0001f, rule.primaryTileWeight + rule.secondaryTileWeight);
            secondaryWeight = Mathf.Clamp01(rule.secondaryTileWeight / total) * sample.blendStrength;
        }

        float dither = (float)noise.snoise(new float2(worldX * biomeBlendNoiseScale, worldY * biomeBlendNoiseScale)) * 0.15f;
        return (Hash01(worldX, worldY, seed, 83) + dither) < secondaryWeight
            ? sample.secondaryBiome
            : sample.biome;
    }

    private bool ShouldPlaceDecoration(BiomeSample sample, int worldX, int worldY)
    {
        if (sample.biome == null)
        {
            return false;
        }

        if (IsHeightEdge(worldX, worldY, sample.height))
        {
            return false;
        }

        float chance = sample.biome.GetDecorationDensity(sample.isTransitionCell);
        if (sample.isTransitionCell && sample.secondaryBiome != null)
        {
            BiomeTransitionRule rule = GetTransitionRule(sample.biome, sample.secondaryBiome);
            if (rule != null)
            {
                chance *= rule.decorationMultiplier;
            }
        }

        return sample.biome.ShouldPlaceDecoration(worldX, worldY, seed, chance);
    }

    private bool IsHeightEdge(int worldX, int worldY, int height)
    {
        return SampleCell(worldX + 1, worldY).height != height
            || SampleCell(worldX - 1, worldY).height != height
            || SampleCell(worldX, worldY + 1).height != height
            || SampleCell(worldX, worldY - 1).height != height;
    }

    private BiomeTransitionRule GetTransitionRule(IsoBiomeDefinition primary, IsoBiomeDefinition secondary)
    {
        if (primary == null || secondary == null)
        {
            return null;
        }

        BiomeKind primaryKind = primary.EffectiveBiomeKind;
        BiomeKind secondaryKind = secondary.EffectiveBiomeKind;
        if (transitionRules != null)
        {
            for (int i = 0; i < transitionRules.Length; i++)
            {
                BiomeTransitionRule rule = transitionRules[i];
                if (rule == null)
                {
                    continue;
                }

                bool direct = rule.fromBiome == primaryKind && rule.toBiome == secondaryKind;
                bool reverse = rule.fromBiome == secondaryKind && rule.toBiome == primaryKind;
                if (direct || reverse)
                {
                    return rule;
                }
            }
        }

        return CreateDefaultTransitionRule(primaryKind, secondaryKind);
    }

    private static BiomeTransitionRule CreateDefaultTransitionRule(BiomeKind a, BiomeKind b)
    {
        if (IsPair(a, b, BiomeKind.Plains, BiomeKind.Desert))
        {
            return new BiomeTransitionRule
            {
                fromBiome = BiomeKind.Plains,
                toBiome = BiomeKind.Desert,
                primaryTileWeight = 0.78f,
                secondaryTileWeight = 0.22f,
                decorationMultiplier = 0.35f,
                resourceMultiplier = 0.5f
            };
        }

        if (IsPair(a, b, BiomeKind.Plains, BiomeKind.FrozenMountain))
        {
            return new BiomeTransitionRule
            {
                fromBiome = BiomeKind.Plains,
                toBiome = BiomeKind.FrozenMountain,
                primaryTileWeight = 0.72f,
                secondaryTileWeight = 0.28f,
                decorationMultiplier = 0.25f,
                resourceMultiplier = 0.45f
            };
        }

        if (IsPair(a, b, BiomeKind.Desert, BiomeKind.FrozenMountain))
        {
            return new BiomeTransitionRule
            {
                fromBiome = BiomeKind.Desert,
                toBiome = BiomeKind.FrozenMountain,
                primaryTileWeight = 0.82f,
                secondaryTileWeight = 0.18f,
                decorationMultiplier = 0.18f,
                resourceMultiplier = 0.45f
            };
        }

        return null;
    }

    private static bool IsPair(BiomeKind a, BiomeKind b, BiomeKind first, BiomeKind second)
    {
        return (a == first && b == second) || (a == second && b == first);
    }

    private float GetContinentalHeightWeight(IsoBiomeDefinition biome)
    {
        if (biome == null)
        {
            return 0.35f;
        }

        switch (biome.EffectiveBiomeKind)
        {
            case BiomeKind.FrozenMountain:
                return 0.65f;
            case BiomeKind.Desert:
                return 0.25f;
            default:
                return 0.35f;
        }
    }

    private IsoBiomeDefinition FindBiome(BiomeKind kind)
    {
        if (biomes == null)
        {
            return null;
        }

        for (int i = 0; i < biomes.Length; i++)
        {
            IsoBiomeDefinition biome = biomes[i];
            if (biome != null && biome.EffectiveBiomeKind == kind)
            {
                return biome;
            }
        }

        return null;
    }

    /// <summary>Index of a biome kind within the biomes[] array (matches the job's biomeDefinitions order). -1 if absent.</summary>
    private int FindBiomeIndex(BiomeKind kind)
    {
        if (biomes == null) return -1;
        for (int i = 0; i < biomes.Length; i++)
        {
            if (biomes[i] != null && biomes[i].EffectiveBiomeKind == kind) return i;
        }
        return -1;
    }

    private IsoBiomeDefinition cachedStarterBiome;

    /// <summary>The starter-zone biome definition (cached). Falls back to plainsBiome if not in the array.</summary>
    private IsoBiomeDefinition GetStarterBiome()
    {
        if (cachedStarterBiome != null && cachedStarterBiome.EffectiveBiomeKind == starterZoneBiome)
        {
            return cachedStarterBiome;
        }

        IsoBiomeDefinition found = FindBiome(starterZoneBiome);
        if (found == null && plainsBiome != null && plainsBiome.EffectiveBiomeKind == starterZoneBiome)
        {
            found = plainsBiome;
        }
        cachedStarterBiome = found;
        return found;
    }

    private static bool SameBiome(IsoBiomeDefinition a, IsoBiomeDefinition b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        return a == b || a.EffectiveBiomeKind == b.EffectiveBiomeKind;
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

    private GameObject GetEnemyFromPool(EnemyDefinition definition)
    {
        if (enemyPool.TryGetValue(definition, out Stack<GameObject> pool) && pool.Count > 0)
        {
            GameObject enemy = pool.Pop();
            enemy.SetActive(true);
            return enemy;
        }

        GameObject newEnemy = new GameObject(definition.displayName);
        newEnemy.AddComponent<Rigidbody2D>();
        newEnemy.AddComponent<CircleCollider2D>();

        GameObject spriteObject = new GameObject("SpriteRenderer");
        spriteObject.transform.SetParent(newEnemy.transform);
        spriteObject.transform.localPosition = Vector3.zero;
        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.color = definition.tint;

        SlimeEnemyController controller = newEnemy.AddComponent<SlimeEnemyController>();
        controller.definition = definition;
        controller.target = player;
        controller.world = this;
        controller.maxStepHeight = 0;
        controller.sortingOrderOffset = 5;

        return newEnemy;
    }

    private void ReturnEnemyToPool(GameObject enemy)
    {
        if (enemy == null) return;
        SlimeEnemyController controller = enemy.GetComponent<SlimeEnemyController>();
        if (controller != null && controller.definition != null)
        {
            if (!enemyPool.ContainsKey(controller.definition))
            {
                enemyPool[controller.definition] = new Stack<GameObject>();
            }
            enemy.SetActive(false);
            enemyPool[controller.definition].Push(enemy);
        }
        else
        {
            Destroy(enemy);
        }
    }

    private IsoBiomeDefinition GetDefaultBiome()
{
        if (plainsBiome != null)
        {
            return plainsBiome;
        }

        if (biomes == null)
        {
            return null;
        }

        for (int i = 0; i < biomes.Length; i++)
        {
            if (biomes[i] != null)
            {
                return biomes[i];
            }
        }

        return null;
    }

    private void EnsureBiomeArray()
    {
        if ((biomes == null || biomes.Length == 0) && plainsBiome != null)
        {
            biomes = new[] { plainsBiome };
        }
    }

    private void EnsureRecorder()
    {
        if (recorder == null)
        {
            recorder = IsoRuntimeRecorder.Instance != null
                ? IsoRuntimeRecorder.Instance
                : FindFirstObjectByType<IsoRuntimeRecorder>();
        }
    }
}
