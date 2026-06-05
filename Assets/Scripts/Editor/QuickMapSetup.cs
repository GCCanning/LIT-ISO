using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using EthraClone.TrialWeek;

public static class QuickMapSetup
{
    [MenuItem("Tools/LIT-ISO/Legacy/Procedural Generator/Full Setup - Create Complete Procedural Test Scene", false, 900)]
    public static void CreateCompleteProceduralTestScene()
    {
        // Step 1: Create new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Step 2: Create IsoGrid with Grid
        GameObject gridObj = new GameObject("IsoGrid");
        Grid grid = gridObj.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
        grid.cellSize = new Vector3(1f, 0.5f, 1f);

        // Step 3: Create Ground tilemap
        GameObject groundMapObj = new GameObject("Ground");
        groundMapObj.transform.SetParent(gridObj.transform);
        Tilemap groundTilemap = groundMapObj.AddComponent<Tilemap>();
        TilemapRenderer renderer = groundMapObj.AddComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Individual;
        renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;

        // Step 4: Setup Main Camera
        GameObject cameraObj = new GameObject("Main Camera");
        Camera camera = cameraObj.AddComponent<Camera>();
        cameraObj.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.backgroundColor = new Color(0.2f, 0.3f, 0.4f, 1f);
        camera.transparencySortMode = TransparencySortMode.CustomAxis;
        camera.transparencySortAxis = new Vector3(0f, 1f, -0.26f);
        cameraObj.transform.position = new Vector3(8, 8, -10);

        // Add camera follow component (will be wired after player is created)
        CameraFollow camFollow = cameraObj.AddComponent<CameraFollow>();
        camFollow.smoothSpeed = 5f;
        camFollow.offset = new Vector3(0, 0, -10);

        // Directional Light
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Step 5: Setup Graphics settings
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, -0.26f);

        // Step 6: Add ProceduralIsoTilemapGenerator
        ProceduralIsoTilemapGenerator mapGen = gridObj.AddComponent<ProceduralIsoTilemapGenerator>();
        mapGen.grid = grid;
        mapGen.groundTilemap = groundTilemap;
        mapGen.mapSize = new Vector3Int(32, 32, 1);
        mapGen.selectedBiome = ProceduralIsoTilemapGenerator.BiomeType.Plains;
        mapGen.plainsThreshold = 0.4f;
        mapGen.desertThreshold = 0.6f;
        mapGen.frozenThreshold = 0.3f;
        mapGen.smoothingPasses = 3;
        mapGen.noiseScale = 0.08f;
        mapGen.randomSeed = 0;

        // Step 7: Load tiles and assign to terrain types
        LoadAndAssignPlainsRuleTiles(mapGen);

        // Step 8: Create neutral player prefab
        GameObject playerObj = new GameObject("PlayerPrefab");
        Rigidbody2D rb = playerObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerObj.AddComponent<SortingGroup>();

        IsoPlayerController playerController = playerObj.AddComponent<IsoPlayerController>();
        playerController.movementSpeed = 5f;
        playerController.acceleration = 22f;
        playerController.deceleration = 30f;
        playerController.allowWallSlide = false;
        playerController.jumpDuration = 0.34f;
        playerController.jumpArcHeight = 0.68f;
        playerController.landingLockoutDuration = 0.07f;
        playerController.jumpMomentumDistance = 1.15f;
        playerController.jumpMinimumDistance = 0.75f;
        playerController.jumpMomentumSpeedScale = 0.16f;
        playerController.visualScale = 1f;
        playerController.spriteGroundLift = 0.06f;
        AssignPlayerSpriteSheets(playerController);

        playerObj.SetActive(false);

        // Step 9: Assign player prefab and wire camera
        mapGen.playerPrefab = playerObj;
        mapGen.playerStartPos = new Vector3Int(16, 16, 0);

        // Note: camera target will be set when player is spawned at runtime

        // Save scene
        if (!Directory.Exists("Assets/Scenes"))
            Directory.CreateDirectory("Assets/Scenes");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ProceduralTest.unity");
        EditorUtility.SetDirty(mapGen);
        EditorUtility.SetDirty(playerController);
        EditorUtility.SetDirty(camFollow);

        Debug.Log("Complete procedural test scene created.");
        Debug.Log("Biome: Plains | neutral isometric player with WASD controls | camera follows player");
        Debug.Log("Ready to hit Play.");
    }

    private static void LoadAndAssignPlainsRuleTiles(ProceduralIsoTilemapGenerator mapGen)
    {
        mapGen.tilePalettes.Clear();

        // Load sprite textures and create Tile assets from them
        string[] flatSpriteGuids = AssetDatabase.FindAssets("base", new[] { "Assets/tilemaps/isometric/Sprites/Basic/Flat" });
        string[] raisedSpriteGuids = AssetDatabase.FindAssets("cube", new[] { "Assets/tilemaps/isometric/Sprites/Basic/Raised" });

        // Load flat sprites
        foreach (string guid in flatSpriteGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            // Create a tile from the sprite
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = sprite.name;

            var entry = new ProceduralIsoTilemapGenerator.TilePaletteEntry();
            entry.tile = tile;
            entry.weight = 2;  // Flat tiles more common
            entry.name = "Flat";
            entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Grass;

            mapGen.tilePalettes.Add(entry);
        }

        // Load raised sprites
        foreach (string guid in raisedSpriteGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = sprite.name;

            var entry = new ProceduralIsoTilemapGenerator.TilePaletteEntry();
            entry.tile = tile;
            entry.weight = 1;
            entry.name = "Raised";
            entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Grass;

            mapGen.tilePalettes.Add(entry);
        }

        Debug.Log($"✓ Loaded {mapGen.tilePalettes.Count} Plains grass sprites as tiles");
    }

    private static void LoadAndAssignTiles(ProceduralIsoTilemapGenerator mapGen)
    {
        mapGen.tilePalettes.Clear();

        // Load Rule Tiles from isometric folder
        string[] allTileGuids = AssetDatabase.FindAssets("t:Tile", new[] { "Assets/tilemaps/isometric/RuleTiles" });

        foreach (string guid in allTileGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null) continue;

            string filename = Path.GetFileNameWithoutExtension(path).ToLower();

            var entry = new ProceduralIsoTilemapGenerator.TilePaletteEntry();
            entry.tile = tile;
            entry.weight = 1;

            // Map Rule Tiles by biome/type
            if (filename.Contains("plains") || filename.Contains("grass"))
            {
                entry.name = "Grass";
                entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Grass;
            }
            else if (filename.Contains("desert") || filename.Contains("sand"))
            {
                entry.name = "Stone";
                entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Stone;
            }
            else if (filename.Contains("frozen") || filename.Contains("snow") || filename.Contains("ice"))
            {
                entry.name = "Water";
                entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Water;
            }
            else if (filename.Contains("temple") || filename.Contains("lava"))
            {
                entry.name = "Dirt";
                entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Dirt;
            }
            else if (filename.Contains("basic") || filename.Contains("floor"))
            {
                entry.name = "Grass";
                entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Grass;
            }
            else
            {
                entry.name = "Grass";
                entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Grass;
            }

            mapGen.tilePalettes.Add(entry);
        }

        Debug.Log($"✓ Loaded {mapGen.tilePalettes.Count} Rule Tiles from Assets/tilemaps/isometric");
    }

    private static Sprite[] LoadSpritesFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Folder not found: {folderPath}");
            return new Sprite[0];
        }

        string[] pngFiles = Directory.GetFiles(folderPath, "*.png");
        System.Array.Sort(pngFiles);
        List<Sprite> sprites = new List<Sprite>();

        foreach (string file in pngFiles)
        {
            string assetPath = file.Replace("\\", "/").Replace(Application.dataPath, "Assets");
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites.ToArray();
    }

    private static void AssignPlayerSpriteSheets(IsoPlayerController playerController)
    {
        playerController.walkSpriteSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Resources/Characters/Player/HollowedLight_512x1024.png");
        playerController.walkSheetColumns = 4;
        playerController.walkSheetRows = 8;
        playerController.walkSpritePixelsPerUnit = 128f;
        playerController.animateWalkFrames = false;
        playerController.useWalkBob = true;
        playerController.walkBobHeight = 0.035f;
        playerController.walkBobFrequency = 8f;
    }

    [MenuItem("Tools/LIT-ISO/Legacy/Procedural Generator/1. Create Procedural Map Scene", false, 901)]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject gridObj = new GameObject("IsoGrid");
        Grid grid = gridObj.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
        grid.cellSize = new Vector3(1f, 0.5f, 1f);

        GameObject groundMapObj = new GameObject("Ground");
        groundMapObj.transform.SetParent(gridObj.transform);
        groundMapObj.AddComponent<Tilemap>();
        TilemapRenderer renderer = groundMapObj.AddComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Individual;
        renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;

        GameObject cameraObj = new GameObject("Main Camera");
        Camera camera = cameraObj.AddComponent<Camera>();
        cameraObj.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.backgroundColor = new Color(0.2f, 0.3f, 0.4f, 1f);
        camera.transparencySortMode = TransparencySortMode.CustomAxis;
        camera.transparencySortAxis = new Vector3(0f, 1f, -0.26f);
        cameraObj.transform.position = new Vector3(8, 8, -10);

        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        gridObj.AddComponent<ProceduralIsoTilemapGenerator>();

        GameObject playerObj = new GameObject("PlayerPrefab");
        playerObj.AddComponent<Rigidbody2D>().gravityScale = 0;
        playerObj.AddComponent<SortingGroup>();
        playerObj.AddComponent<AdventurerPlayerSetup>();
        playerObj.SetActive(false);

        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, -0.26f);

        if (!Directory.Exists("Assets/Scenes"))
            Directory.CreateDirectory("Assets/Scenes");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ProceduralIsoMap.unity");
        Debug.Log("Scene created!");
    }

    [MenuItem("Tools/LIT-ISO/Legacy/Procedural Generator/2. Load Tile Assets", false, 902)]
    public static void LoadTiles()
    {
        GameObject gridObj = GameObject.Find("IsoGrid");
        if (gridObj == null)
        {
            EditorUtility.DisplayDialog("Error", "Run step 1 first", "OK");
            return;
        }

        ProceduralIsoTilemapGenerator mapGen = gridObj.GetComponent<ProceduralIsoTilemapGenerator>();
        string[] allTileGuids = AssetDatabase.FindAssets("t:Tile", new[] { "Assets/_External" });
        mapGen.tilePalettes.Clear();

        foreach (string guid in allTileGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null) continue;

            var entry = new ProceduralIsoTilemapGenerator.TilePaletteEntry();
            entry.tile = tile;
            entry.weight = 1;
            entry.name = Path.GetFileNameWithoutExtension(path);
            entry.terrainType = ProceduralIsoTilemapGenerator.TerrainType.Grass;

            mapGen.tilePalettes.Add(entry);
        }

        EditorUtility.SetDirty(mapGen);
        Debug.Log($"Loaded {mapGen.tilePalettes.Count} tiles");
    }

    [MenuItem("Tools/LIT-ISO/Legacy/Procedural Generator/3. Assign Player", false, 903)]
    public static void AssignPlayer()
    {
        GameObject gridObj = GameObject.Find("IsoGrid");
        GameObject playerObj = GameObject.Find("PlayerPrefab");

        if (gridObj == null || playerObj == null)
        {
            EditorUtility.DisplayDialog("Error", "Run steps 1-2 first", "OK");
            return;
        }

        ProceduralIsoTilemapGenerator mapGen = gridObj.GetComponent<ProceduralIsoTilemapGenerator>();
        mapGen.playerPrefab = playerObj;

        EditorUtility.SetDirty(mapGen);
        Debug.Log("Ready to play!");
    }
}
