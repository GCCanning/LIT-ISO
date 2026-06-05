using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Generates the Starter Isle — a small pre-defined zone where new players begin.
///
/// Unlike the procedural IsoWorldChunkManager, this generator creates a fixed,
/// hand-tuned layout: grass clearing → forest grove → river → ruined watchtower.
/// Enemy spawns are limited to F-rank mobs. No weather, no world bosses.
///
/// Usage:
///   1. Add this component to an empty GameObject in your scene.
///   2. Assign the grid, tilemaps, and biome references.
///   3. Assign starter enemy definitions (F-rank only).
///   4. On Start() it auto-generates and starts the TutorialSequence.
///
/// Extending the layout:
///   Edit GenerateLayout() — the zone is built from a simple 2D array pattern.
///   Add new tile types by extending the TileType enum.
/// </summary>
public class StarterZoneGenerator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Grid / Tilemaps")]
    public Grid grid;
    public Tilemap groundTilemap;
    public Tilemap elevationTilemap;

    [Header("Tiles")]
    [Tooltip("Base grass tile for the starter zone.")]
    public TileBase grassTile;
    [Tooltip("Dirt/path tile.")]
    public TileBase pathTile;
    [Tooltip("Water tile for the river.")]
    public TileBase waterTile;
    [Tooltip("Raised ground tile (watchtower hill).")]
    public TileBase raisedTile;

    [Header("Zone Size")]
    [Tooltip("Half-width in cells. The zone spans -zoneRadius..+zoneRadius on each axis.")]
    [Min(8)] public int zoneRadius = 24;

    [Header("Enemy Spawning")]
    [Tooltip("Slime definition (F-rank) for the tutorial zone.")]
    public EnemyDefinition starterSlime;
    [Tooltip("Goblin definition (F-rank) for the tutorial zone.")]
    public EnemyDefinition starterGoblin;
    [Tooltip("How many starter enemies to spawn.")]
    [Range(4, 20)] public int enemyCount = 10;
    public GameObject enemyPrefab;

    [Header("References")]
    [Tooltip("The TutorialSequence component to start after generation.")]
    public TutorialSequence tutorialSequence;

    [Tooltip("Optional: player spawn point. If null, uses Vector3.zero.")]
    public Transform playerSpawnPoint;

    // -------------------------------------------------------------------------
    // Internal layout — extend TileType to add new terrain types
    // -------------------------------------------------------------------------

    private enum TileType { Grass, Path, Water, Raised, Empty }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        Generate();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Generate()
    {
        if (groundTilemap == null || grid == null)
        {
            Debug.LogError("[StarterZone] Missing Grid or Tilemap references.");
            return;
        }

        groundTilemap.ClearAllTiles();
        if (elevationTilemap != null) elevationTilemap.ClearAllTiles();

        PaintTerrain();
        SpawnEnemies();
        PositionPlayer();

        StartCoroutine(BeginTutorialNextFrame());

        Debug.Log("[StarterZone] Starter Isle generated.");
    }

    // -------------------------------------------------------------------------
    // Terrain generation
    // -------------------------------------------------------------------------

    private void PaintTerrain()
    {
        int r = zoneRadius;

        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                TileType type = ClassifyCell(x, y, r);
                TileBase tile = TileForType(type);
                if (tile == null) continue;

                var cell = new Vector3Int(x, y, 0);
                groundTilemap.SetTile(cell, tile);

                // Raise the watchtower hill
                if (type == TileType.Raised && elevationTilemap != null && raisedTile != null)
                    elevationTilemap.SetTile(cell, raisedTile);
            }
        }
    }

    /// <summary>
    /// Returns the terrain type for a given cell coordinate.
    /// Edit this method to reshape the starter zone.
    /// </summary>
    private TileType ClassifyCell(int x, int y, int r)
    {
        // Border ring — empty outside usable area
        if (Mathf.Abs(x) > r || Mathf.Abs(y) > r) return TileType.Empty;

        // River — vertical strip near x = r/2
        int riverX = r / 2;
        if (x >= riverX - 1 && x <= riverX + 1) return TileType.Water;

        // Watchtower hill — raised area in NE quadrant
        if (x >= r - 8 && x <= r - 2 && y >= r - 8 && y <= r - 2) return TileType.Raised;

        // Central path — horizontal strip at y = 0
        if (y >= -1 && y <= 1 && x < riverX) return TileType.Path;

        // Vertical path connecting to watchtower
        if (x >= riverX + 2 && x <= riverX + 4 && y >= 0 && y <= r - 2) return TileType.Path;

        // Everything else is grass
        return TileType.Grass;
    }

    private TileBase TileForType(TileType type)
    {
        return type switch
        {
            TileType.Grass  => grassTile,
            TileType.Path   => pathTile  ?? grassTile,
            TileType.Water  => waterTile ?? grassTile,
            TileType.Raised => grassTile,   // Ground under the raised tile
            _               => null,
        };
    }

    // -------------------------------------------------------------------------
    // Enemy spawning
    // -------------------------------------------------------------------------

    private void SpawnEnemies()
    {
        if (enemyPrefab == null) return;

        int half = zoneRadius - 4;
        int riverX = zoneRadius / 2;

        for (int i = 0; i < enemyCount; i++)
        {
            // Keep enemies on the grass side (west of river)
            float ex = Random.Range(-half, riverX - 3);
            float ey = Random.Range(-half, half);
            Vector3 worldPos = grid.CellToWorld(new Vector3Int(Mathf.RoundToInt(ex), Mathf.RoundToInt(ey), 0));

            GameObject go = Instantiate(enemyPrefab, worldPos, Quaternion.identity, transform);
            var ctrl = go.GetComponent<SlimeEnemyController>();
            if (ctrl != null)
            {
                // Alternate between slime and goblin
                ctrl.definition = (i % 3 == 0 && starterGoblin != null) ? starterGoblin : starterSlime;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Player positioning
    // -------------------------------------------------------------------------

    private void PositionPlayer()
    {
        if (playerSpawnPoint == null) return;

        // Spawn in the centre-left clearing
        Vector3 spawnCell  = new Vector3(-zoneRadius / 4f, 0f, 0f);
        Vector3 spawnWorld = grid.CellToWorld(new Vector3Int(Mathf.RoundToInt(spawnCell.x), 0, 0));
        playerSpawnPoint.position = spawnWorld;
    }

    // -------------------------------------------------------------------------
    // Tutorial kick-off
    // -------------------------------------------------------------------------

    private IEnumerator BeginTutorialNextFrame()
    {
        yield return null;   // let Start() finish on all objects first

        if (tutorialSequence != null)
            tutorialSequence.StartTutorial();
        else
            Debug.LogWarning("[StarterZone] No TutorialSequence assigned — tutorial won't start.");
    }
}
