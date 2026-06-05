using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class ProceduralIsoTilemapGenerator : MonoBehaviour
{
    [System.Serializable]
    public class TilePaletteEntry
    {
        public string name;
        public Tile tile;
        public TerrainType terrainType;
        public int weight = 1;
    }

    public enum TerrainType
    {
        Water,
        Grass,
        Stone,
        Dirt
    }

    public enum BiomeType
    {
        Plains,
        Desert,
        FrozenMountain,
        FrozenCave,
        Temple,
        Basic
    }

    [Header("Grid & Tilemap")]
    public Grid grid;
    public Tilemap groundTilemap;
    public Vector3Int mapSize = new Vector3Int(32, 32, 1);

    [Header("Biome")]
    public IsoBiomeDefinition activeBiome;

    [Header("Tile Palettes")]
    public List<TilePaletteEntry> tilePalettes = new List<TilePaletteEntry>();

    [Header("Biome Generation")]
    public BiomeType selectedBiome = BiomeType.Plains;
    public float plainsThreshold = 0.4f;
    public float desertThreshold = 0.6f;
    public float frozenThreshold = 0.3f;
    public int smoothingPasses = 3;
    public float noiseScale = 0.08f;
    public int randomSeed = 0;

    [Header("Player")]
    public GameObject playerPrefab;
    public Vector3Int playerStartPos = new Vector3Int(16, 16, 0);

    private Dictionary<TerrainType, List<TilePaletteEntry>> terrainTileMap = new Dictionary<TerrainType, List<TilePaletteEntry>>();
    private TerrainType[,] terrainGrid;
    private int[,] heightGrid;

    private void Start()
    {
        GenerateMap();
        SpawnPlayer();
    }

    public void GenerateMap()
    {
        if (grid == null || groundTilemap == null)
        {
            Debug.LogError("Grid or Tilemap not assigned!");
            return;
        }

        SetupTerrainTileMap();
        GenerateTerrainGrid();
        ApplySmoothing();
        PaintTilemapWithOrdering();

        Debug.Log("Map generated successfully with proper isometric ordering");
    }

    private void SetupTerrainTileMap()
    {
        terrainTileMap.Clear();

        foreach (var entry in tilePalettes)
        {
            if (entry == null || entry.tile == null || entry.weight <= 0)
            {
                continue;
            }

            if (!terrainTileMap.ContainsKey(entry.terrainType))
            {
                terrainTileMap[entry.terrainType] = new List<TilePaletteEntry>();
            }
            terrainTileMap[entry.terrainType].Add(entry);
        }
    }

    private void GenerateTerrainGrid()
    {
        terrainGrid = new TerrainType[mapSize.x, mapSize.y];
        heightGrid  = new int[mapSize.x, mapSize.y];
        Random.InitState(randomSeed);

        float hScale     = activeBiome != null ? activeBiome.heightNoiseScale    : 0.08f;
        float hThreshold = activeBiome != null ? activeBiome.raisedTileThreshold : 0.55f;
        // Use a different seed offset so height noise doesn't mirror terrain noise.
        float hSeedX = randomSeed * 0.73f;
        float hSeedY = randomSeed * 0.31f;

        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                float noiseValue = Mathf.PerlinNoise(x * noiseScale + randomSeed * 0.1f, y * noiseScale + randomSeed * 0.1f);
                terrainGrid[x, y] = DetermineTerrainType(noiseValue);

                float hNoise = Mathf.PerlinNoise(x * hScale + hSeedX, y * hScale + hSeedY);
                heightGrid[x, y] = hNoise >= hThreshold ? 1 : 0;
            }
        }
    }

    private TerrainType DetermineTerrainType(float noiseValue)
    {
        // For Plains biome, use simple grass variation
        switch (selectedBiome)
        {
            case BiomeType.Plains:
                // All grass for plains
                return TerrainType.Grass;

            case BiomeType.Desert:
                if (noiseValue < 0.3f) return TerrainType.Water;
                return TerrainType.Stone;

            case BiomeType.FrozenMountain:
            case BiomeType.FrozenCave:
                if (noiseValue < frozenThreshold) return TerrainType.Water;
                return TerrainType.Dirt;

            case BiomeType.Temple:
                if (noiseValue < 0.4f) return TerrainType.Water;
                return TerrainType.Dirt;

            default:
                return TerrainType.Grass;
        }
    }

    private void ApplySmoothing()
    {
        for (int pass = 0; pass < smoothingPasses; pass++)
        {
            TerrainType[,] newGrid = (TerrainType[,])terrainGrid.Clone();

            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y < mapSize.y; y++)
                {
                    Dictionary<TerrainType, int> neighborCounts = new Dictionary<TerrainType, int>();

                    // Check 8 neighbors
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx >= 0 && nx < mapSize.x && ny >= 0 && ny < mapSize.y)
                            {
                                TerrainType neighborType = terrainGrid[nx, ny];
                                if (!neighborCounts.ContainsKey(neighborType))
                                    neighborCounts[neighborType] = 0;
                                neighborCounts[neighborType]++;
                            }
                        }
                    }

                    TerrainType mostCommon = terrainGrid[x, y];
                    int maxCount = 0;

                    foreach (var kvp in neighborCounts)
                    {
                        if (kvp.Value > maxCount)
                        {
                            maxCount = kvp.Value;
                            mostCommon = kvp.Key;
                        }
                    }

                    // Only change if neighbors strongly suggest it (5+ neighbors of same type)
                    if (maxCount >= 5)
                    {
                        newGrid[x, y] = mostCommon;
                    }
                }
            }

            terrainGrid = newGrid;
        }
    }

    private void PaintTilemapWithOrdering()
    {
        groundTilemap.ClearAllTiles();

        // Pass 1 — flat ground at z=0 across every cell.
        // Gives cliff walls a solid base to sit against; prevents holes at height transitions.
        for (int y = 0; y < mapSize.y; y++)
        {
            for (int x = 0; x < mapSize.x; x++)
            {
                TileBase baseTile = activeBiome != null && activeBiome.flatGroundTile != null
                    ? activeBiome.flatGroundTile
                    : (TileBase)SelectTileForTerrain(terrainGrid[x, y], x, y);

                if (baseTile != null)
                    groundTilemap.SetTile(new Vector3Int(x, y, 0), baseTile);
            }
        }

        // Pass 2 — stamp the raised RuleTile at z=1 where height > 0.
        // No manual neighbour checking needed: Unity evaluates the RuleTile's built-in
        // neighbour rules at paint time and selects the correct cliff-edge sprite automatically.
        if (activeBiome == null || activeBiome.raisedRuleTile == null) return;

        TileBase raised = activeBiome.raisedRuleTile;
        for (int y = 0; y < mapSize.y; y++)
        {
            for (int x = 0; x < mapSize.x; x++)
            {
                if (heightGrid[x, y] >= 1)
                    groundTilemap.SetTile(new Vector3Int(x, y, 1), raised);
            }
        }
    }

    private Tile SelectTileForTerrain(TerrainType terrainType, int x, int y)
    {
        if (!terrainTileMap.ContainsKey(terrainType))
            return null;

        List<TilePaletteEntry> palette = terrainTileMap[terrainType];
        if (palette.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (var entry in palette)
        {
            if (entry.tile != null && entry.weight > 0)
            {
                totalWeight += entry.weight;
            }
        }
        if (totalWeight <= 0)
            return null;

        // Weighted random selection, but seeded per position for consistency.
        System.Random positionRandom = new System.Random(randomSeed + x * 1000 + y);
        int randomValue = positionRandom.Next(0, totalWeight);
        int currentWeight = 0;

        foreach (var entry in palette)
        {
            if (entry.tile == null || entry.weight <= 0)
                continue;

            currentWeight += entry.weight;
            if (randomValue < currentWeight)
            {
                return entry.tile;
            }
        }

        return palette[0].tile;
    }

    // Helper: Check if neighbors exist for depth calculation
    public TerrainType GetTerrainAtPos(int x, int y)
    {
        if (terrainGrid == null || x < 0 || x >= mapSize.x || y < 0 || y >= mapSize.y)
            return TerrainType.Grass;
        return terrainGrid[x, y];
    }

    // Helper: Get neighbor count of same terrain type (for transition detection)
    public int GetSameTerrainNeighborCount(int x, int y, TerrainType type)
    {
        int count = 0;

        // Check isometric neighbors (down-left, down-right, up-left, up-right for visibility)
        int[][] neighbors = new int[][]
        {
            new int[] { x - 1, y },      // Left
            new int[] { x + 1, y },      // Right
            new int[] { x, y - 1 },      // Up
            new int[] { x, y + 1 }       // Down
        };

        foreach (var neighbor in neighbors)
        {
            int nx = neighbor[0];
            int ny = neighbor[1];

            if (nx >= 0 && nx < mapSize.x && ny >= 0 && ny < mapSize.y)
            {
                if (terrainGrid[nx, ny] == type)
                    count++;
            }
        }

        return count;
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("Player prefab not assigned");
            return;
        }

        Vector3 spawnWorldPos = grid.CellToWorld(playerStartPos);
        GameObject playerInstance = Instantiate(playerPrefab, spawnWorldPos, Quaternion.identity);
        playerInstance.name = "Player";
        playerInstance.SetActive(true);

        if (playerInstance.GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D rb = playerInstance.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (playerInstance.GetComponent<SortingGroup>() == null)
        {
            playerInstance.AddComponent<SortingGroup>();
        }

        // Wire camera follow
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            CameraFollow camFollow = mainCamera.GetComponent<CameraFollow>();
            if (camFollow != null)
            {
                camFollow.target = playerInstance.transform;
            }
        }

        Debug.Log($"Player spawned at {playerStartPos}");
    }
}
