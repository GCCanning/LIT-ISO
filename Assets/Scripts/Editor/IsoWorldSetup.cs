using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using EthraClone.TrialWeek;

public static class IsoWorldSetup
{
    private const string PlainsBiomePath = "Assets/World/Biomes/BiomeDefinition_Plains.asset";
    private const string ScenePath = "Assets/Scenes/InfinitePlainsPrototype.unity";
    private const string NeighbourRoot = "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/";
    private const string RandomRoot = "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/";
    private const string ColliderRoot = "Assets/Tilemaps/Isometric/Colliders/ColliderTiles/";
    // Imported prop tiles created by BiomeDecorationImporter. LoadTile() safely skips
    // any that don't exist yet, so referencing them here is harmless on a fresh project.
    private const string DecoRoot = "Assets/Tilemaps/Isometric/Tiles/BiomeDecorations/";
    private const string CollisionBlockPath = "Assets/Tilemaps/Isometric/Colliders/ColliderTiles/CollisionBlock.asset";
    private const string LightingRoot = "Assets/World/Lighting/";
    private const string PlayerWalkSheetPath = "Assets/Resources/Characters/Player/HollowedLight_512x1024.png";
    private const string PlayerIdleSheetPath = "Assets/Resources/Characters/Player/ReferenceKnight_Idle_512x1024.png";
    private const string PlayerIdleAudioPath = "Assets/Resources/Audio/Player/Player_Idle_IceHum.wav";

    [MenuItem("Tools/LIT-ISO/Playtest/Rebuild Full Playtest Scene", false, 20)]
    public static void BuildAndValidateFullPlaytestScene()
    {
        CreateInfinitePlainsPrototype();
        ValidateFullPlaytestScene();
    }

    [MenuItem("Tools/LIT-ISO/World/Create Infinite Plains Prototype", false, 100)]
    public static void CreateInfinitePlainsPrototype()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        IsoBiomeDefinition[] worldBiomes = CreateOrUpdateBiomes();
        IsoBiomeDefinition plainsBiome = System.Array.Find(worldBiomes,
            b => b != null && b.EffectiveBiomeKind == BiomeKind.Plains);
        if (plainsBiome == null && worldBiomes.Length > 0) plainsBiome = worldBiomes[0];

        // Starter world uses Plains + Forest ONLY. The other biomes (Desert/Temple/Frozen)
        // still exist as assets for later, but their tan/purple/cyan tiles are exactly the
        // "garish" blocks we don't want in the grassland starter — so exclude them here.
        IsoBiomeDefinition[] starterBiomes = System.Array.FindAll(worldBiomes,
            b => b != null && (b.EffectiveBiomeKind == BiomeKind.Plains
                            || b.EffectiveBiomeKind == BiomeKind.Forest));
        if (starterBiomes.Length == 0) starterBiomes = worldBiomes;

        GameObject gridObject = new GameObject("IsoWorldGrid");
        Grid grid = gridObject.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
        grid.cellSize = new Vector3(1f, 0.5f, 1f);

        IsoWorldChunkManager world = gridObject.AddComponent<IsoWorldChunkManager>();
        world.grid = grid;
        world.cliffColliderTile = AssetDatabase.LoadAssetAtPath<TileBase>(CollisionBlockPath);
        world.plainsBiome = plainsBiome;
        world.biomes = starterBiomes;   // Plains + Forest only
        world.seed = 12345;
        world.chunkSize = 32;
        world.activeRadius = 2;              // ↑ 5×5 chunks visible (was 1 = 3×3)
        world.poolInactiveChunks = true;
        world.biomeNoiseScale = 0.0125f;
        world.temperatureNoiseScale = 0.008f;     // ↑ More frequent biome variety (was 0.0045)
        world.moistureNoiseScale = 0.007f;        // ↑ More frequent biome variety (was 0.004)
        world.continentNoiseScale = 0.006f;       // ↑ More frequent height variety (was 0.0035)
        world.biomeBlendRadius = 3;
        world.biomeBlendNoiseScale = 0.08f;
        world.transitionTileChance = 0.35f;
        world.transitionRules = new[]
        {
            CreateTransitionRule(BiomeKind.Plains, BiomeKind.Forest, 0.7f, 0.3f, 0.6f, 0.6f)
        };
        world.showTileBorders = true;
        world.tileBorderColor = new Color(0f, 0f, 0f, 0.16f);
        world.showHeightEdgeShadows = true;
        world.heightEdgeShadowColor = new Color(0f, 0f, 0f, 0.18f);
        world.maxTerrainHeight = 3;
        world.lowlandMaxTerrainHeight = 1;   // gentle lowlands — single-step rises, not pillars
        world.terrainHeightFalloff = 2.2f;   // steeper falloff → tall stacks are rare

        // Large flat starter clearing so the opening is walkable, not a cliff maze.
        world.useStarterZone = true;
        world.starterZoneCenter = Vector2Int.zero;
        world.starterZoneBiome = BiomeKind.Plains;
        world.starterZoneRadius = 44;
        world.starterZoneFlattenRadius = 18;
        world.starterZoneRampStep = 9;

        GameObject player = CreateIsoPlayer(grid, world);
        world.player = player.transform;

        GameObject recorderObject = new GameObject("Iso Runtime Recorder");
        IsoRuntimeRecorder recorder = recorderObject.AddComponent<IsoRuntimeRecorder>();
        recorder.world = world;
        recorder.player = player.transform;
        world.recorder = recorder;

        GameObject selectionMarker = CreateSelectionMarker();
        IsoPlayerController playerController = player.GetComponent<IsoPlayerController>();
        playerController.selectionMarker = selectionMarker.transform;
        playerController.recorder = recorder;

        GameObject cameraObject = CreateCamera(player.transform);
        playerController.inputCamera = cameraObject.GetComponent<Camera>();

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);

        GameObject lightingObject = new GameObject("Iso Lighting Controller");
        IsoLightingController lighting = lightingObject.AddComponent<IsoLightingController>();
        lighting.targetCamera = cameraObject.GetComponent<Camera>();
        lighting.directionalLight = light;
        lighting.profiles = CreateOrUpdateLightingProfiles();
        lighting.profileIndex = 0;

        // Day/Night music manager — syncs to the world cycle and crossfades the two tracks.
        GameObject musicObject = new GameObject("Day Night Music");
        DayNightMusicManager musicManager = musicObject.AddComponent<DayNightMusicManager>();
        musicManager.dayMusicClip   = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/Music/Music_Day_AmbientExploration.flac");
        musicManager.nightMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/Music/Music_Night_HarpTheme.flac");
        musicManager.dayLengthMinutes   = 15f;
        musicManager.nightLengthMinutes = 15f;
        musicManager.crossfadeDuration  = 30f;
        musicManager.masterVolume       = 0.75f;

        // Sun controller — invisible orbital sun that drives directional light rotation,
        // intensity, color, and lighting profile auto-selection.
        GameObject sunObject = new GameObject("Sun");
        SunController sunController = sunObject.AddComponent<SunController>();
        sunController.cycleManager = musicManager;
        sunController.directionalLight = light;
        sunController.lightingController = lighting;
        sunController.orbitRadius = 50f;
        sunController.orbitCenter = Vector3.zero;
        sunController.orbitTiltDegrees = 60f;
        sunController.orbitYawDegrees = 0f;
        sunController.maxLightIntensity = 1.2f;
        sunController.minLightIntensity = 0.15f;
        sunController.lightBlendSpeed = 2.5f;
        sunController.autoSelectLightingProfile = true;
        sunController.dayProfileIndex = 0;   // Day
        sunController.duskProfileIndex = 1;  // Dusk
        sunController.nightProfileIndex = 2; // Night

        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, -0.26f);

        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created infinite Plains prototype at {ScenePath}");
    }

    [MenuItem("Tools/LIT-ISO/Diagnostics/Validate Current Full Playtest Scene", false, 300)]
    public static void ValidateFullPlaytestScene()
    {
        List<string> missing = new List<string>();
        List<string> warnings = new List<string>();

        Grid grid = Object.FindFirstObjectByType<Grid>();
        IsoWorldChunkManager world = Object.FindFirstObjectByType<IsoWorldChunkManager>();
        IsoPlayerController player = Object.FindFirstObjectByType<IsoPlayerController>();
        Camera camera = Camera.main;
        IsoLightingController lighting = Object.FindFirstObjectByType<IsoLightingController>();
        IsoRuntimeRecorder recorder = Object.FindFirstObjectByType<IsoRuntimeRecorder>();
        AudioListener audioListener = Object.FindFirstObjectByType<AudioListener>();
        SceneValidator sceneValidator = Object.FindFirstObjectByType<SceneValidator>();
        SunController sunController = Object.FindFirstObjectByType<SunController>();
        DayNightMusicManager musicMgr = Object.FindFirstObjectByType<DayNightMusicManager>();

        if (grid == null) missing.Add("Grid");
        if (world == null) missing.Add("IsoWorldChunkManager");
        if (player == null) missing.Add("IsoPlayerController");
        if (camera == null) missing.Add("Main Camera");
        if (lighting == null) missing.Add("IsoLightingController");
        if (recorder == null) missing.Add("IsoRuntimeRecorder");
        if (audioListener == null) warnings.Add("AudioListener (audio may not work)");
        if (sceneValidator == null) warnings.Add("SceneValidator (runtime checks disabled)");
        if (sunController == null) warnings.Add("SunController (dynamic sun/lighting disabled)");
        if (musicMgr == null) warnings.Add("DayNightMusicManager (cycle timing unavailable)");

        if (sunController != null)
        {
            if (sunController.cycleManager == null) warnings.Add("SunController.cycleManager unwired");
            if (sunController.directionalLight == null) warnings.Add("SunController.directionalLight unwired");
        }

        if (world != null)
        {
            if (world.grid == null) missing.Add("IsoWorldChunkManager.grid");
            if (world.player == null) missing.Add("IsoWorldChunkManager.player");
            if (world.biomes == null || world.biomes.Length == 0) missing.Add("IsoWorldChunkManager.biomes");
        }

        if (player != null)
        {
            if (player.grid == null) missing.Add("IsoPlayerController.grid");
            if (player.world == null) missing.Add("IsoPlayerController.world");
            if (player.footSampleRadius <= 0f) missing.Add("IsoPlayerController.footSampleRadius");
        }

        if (lighting != null && (lighting.profiles == null || lighting.profiles.Length == 0))
        {
            missing.Add("IsoLightingController.profiles");
        }

        if (missing.Count > 0)
        {
            Debug.LogError("Full playtest scene validation failed. Missing: " + string.Join(", ", missing));
            return;
        }

        if (warnings.Count > 0)
        {
            Debug.LogWarning("Full playtest scene has warnings: " + string.Join(", ", warnings));
        }

        Debug.Log("✅ Full playtest scene ready! Press Play, use WASD to move, Space to jump, F6 or 1-4 to test lighting profiles.");
    }

    [MenuItem("Tools/LIT-ISO/Assets/Create Or Update Biome Definitions", false, 200)]
    public static void CreateOrUpdateBiomeDefinitions()
    {
        CreateOrUpdateBiomes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created or updated procedural isometric biome definitions.");
    }

    [MenuItem("Tools/LIT-ISO/Assets/Create Or Update Lighting Profiles", false, 201)]
    public static void CreateOrUpdateLightingProfileAssets()
    {
        CreateOrUpdateLightingProfiles();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created or updated isometric lighting profiles.");
    }

    private static IsoBiomeDefinition[] CreateOrUpdateBiomes()
    {
        EnsureFolder("Assets/World");
        EnsureFolder("Assets/World/Biomes");

        TileBase cliffSouth = LoadTile(ColliderRoot + "tile-border-left.asset");
        TileBase cliffWest = LoadTile(ColliderRoot + "tile-border-right.asset");
        TileBase cliffNorth = LoadTile(ColliderRoot + "tile-border-top.asset");
        TileBase cliffEast = LoadTile(ColliderRoot + "tile-border-vertical.asset");

        IsoBiomeDefinition[] allBiomes = new[]
        {
            CreateOrUpdateBiome(
                PlainsBiomePath,
                "Plains",
                NeighbourRoot + "NeighbourTile_Plains_FlatGrass.asset",
                // Raised tops reuse the GREEN flat-grass tile. The dedicated raised-grass
                // slices (plains-sliced_57..62) are broken palette swatches that render as
                // garish pink/cyan/purple blocks, so we use the proven flat grass on tops.
                NeighbourRoot + "NeighbourTile_Plains_FlatGrass.asset",
                new[] { RandomRoot + "RandomTile_Plains_FlatGrass.asset" },
                new[] { RandomRoot + "RandomTile_Plains_FlatGrass.asset" },
                // Plains decorations: plants (flowers/mushrooms from random plant tile)
                new[]
                {
                    RandomRoot + "RandomTile_Plains_Plants.asset",
                    DecoRoot + "Deco_OakTree_A.asset",
                    DecoRoot + "Deco_OakTree_B.asset",
                    DecoRoot + "Deco_PineTree.asset",
                    DecoRoot + "Deco_Bush_A.asset",
                    DecoRoot + "Deco_Bush_B.asset",
                    DecoRoot + "Deco_Flower.asset",
                    DecoRoot + "Deco_Log.asset",
                    DecoRoot + "Deco_Stump.asset",
                    DecoRoot + "Deco_Wheat.asset",
                    DecoRoot + "Deco_Rock_A.asset",
                    DecoRoot + "Deco_Rock_B.asset"
                },
                cliffSouth,
                cliffWest,
                cliffNorth,
                cliffEast,
                BiomeKind.Plains,
                0f,      // temperature: full range (open grassland anywhere)
                1f,
                0f,      // moisture: DRY half — Forest takes the wet half
                0.58f,
                0f,      // elevation: full lowland range
                1f,
                0.78f,   // raisedThreshold: high → mostly flat, hills are the exception
                0.045f,  // heightNoiseScale: low → broad, gentle hills (not spiky)
                0.07f,   // ↑ Denser plant decorations (was 0.03)
                0.45f,
                0f),
            // FOREST — the wet sibling of Plains. Same grass ground, but the climate
            // split (high moisture) plus dense tree clustering (configured below) turns
            // these regions into proper woodland. Decoration categories set in
            // ConfigureDecorationCategories().
            CreateOrUpdateBiome(
                "Assets/World/Biomes/BiomeDefinition_Forest.asset",
                "Forest",
                NeighbourRoot + "NeighbourTile_Plains_FlatGrass.asset",
                // Raised tops reuse the GREEN flat-grass tile. The dedicated raised-grass
                // slices (plains-sliced_57..62) are broken palette swatches that render as
                // garish pink/cyan/purple blocks, so we use the proven flat grass on tops.
                NeighbourRoot + "NeighbourTile_Plains_FlatGrass.asset",
                new[] { RandomRoot + "RandomTile_Plains_FlatGrass.asset" },
                new[] { RandomRoot + "RandomTile_Plains_FlatGrass.asset" },
                // Legacy decoration list (overridden by categorised system); forest-leaning.
                new[]
                {
                    DecoRoot + "Deco_PineTree.asset",
                    DecoRoot + "Deco_OakTree_A.asset",
                    DecoRoot + "Deco_OakTree_B.asset",
                    DecoRoot + "Deco_Bush_A.asset",
                    DecoRoot + "Deco_Bush_B.asset",
                    DecoRoot + "Deco_Log.asset",
                    DecoRoot + "Deco_Stump.asset",
                    DecoRoot + "Deco_Rock_A.asset",
                    DecoRoot + "Deco_Rock_B.asset"
                },
                cliffSouth,
                cliffWest,
                cliffNorth,
                cliffEast,
                BiomeKind.Forest,
                0f,      // temperature: full range
                1f,
                0.58f,   // moisture: WET half — this is what makes it forest
                1f,
                0f,      // elevation: full lowland range
                1f,
                0.78f,   // raisedThreshold: high → mostly flat forest floor
                0.045f,  // heightNoiseScale: low → broad, gentle rises
                0.10f,   // dense decorations
                0.5f,
                53f),    // unique biome noise offset
            CreateOrUpdateBiome(
                "Assets/World/Biomes/BiomeDefinition_Desert.asset",
                "Desert",
                NeighbourRoot + "NeighbourTile_Desert_FlatSand.asset",
                NeighbourRoot + "NeighbourTile_Desert_RaisedSand.asset",
                new[]
                {
                    RandomRoot + "RandomTile_Desert_FlatSand.asset",
                    NeighbourRoot + "NeighbourTile_Desert_FlatSandWave.asset"
                },
                new[]
                {
                    RandomRoot + "RandomTile_Desert_RaisedSand.asset",
                    NeighbourRoot + "NeighbourTile_Desert_RaisedSandWave.asset"
                },
                // Desert decorations: coral formations + scattered wooden planks + rocks
                new[]
                {
                    RandomRoot + "RandomTile_Desert_Coral.asset",
                    RandomRoot + "RandomTile_Desert_Planks.asset",
                    DecoRoot + "Deco_Rock_A.asset",
                    DecoRoot + "Deco_Rock_B.asset"
                },
                cliffSouth,
                cliffWest,
                cliffNorth,
                cliffEast,
                BiomeKind.Desert,
                0.58f,
                1f,
                0f,
                0.42f,
                0f,
                0.7f,
                0.72f,
                0.065f,
                0.04f,
                0.25f,
                101f,
                // z=2: wavy sand dune ridgeline
                midElevationPath: NeighbourRoot + "NeighbourTile_Desert_RaisedSandWave.asset"),
            CreateOrUpdateBiome(
                "Assets/World/Biomes/BiomeDefinition_FrozenMountain.asset",
                "Frozen Mountain",
                NeighbourRoot + "NeighbourTile_FrozenMountain_FlatSnow.asset",
                NeighbourRoot + "NeighbourTile_FrozenMountain_RaisedSnow.asset",
                new[]
                {
                    RandomRoot + "RandomTile_FrozenMountain_FlatSnow.asset",
                    RandomRoot + "RandomTile_FrozenMountain_FlatGround.asset"
                },
                new[] { RandomRoot + "RandomTile_FrozenMountain_RaisedSnow.asset" },
                // Frozen Mountain decorations: rocks, snow-capped rocks, AND the
                // legendary Sword In The Stone landmark (rare encounter!)
                new[]
                {
                    "Assets/Tilemaps/Isometric/Tiles/FrozenMountain/decoration_rock_1.asset",
                    "Assets/Tilemaps/Isometric/Tiles/FrozenMountain/decoration_rock_2.asset",
                    "Assets/Tilemaps/Isometric/Tiles/FrozenMountain/decoration_rock_NW.asset",
                    "Assets/Tilemaps/Isometric/Tiles/FrozenMountain/decoration_snowcap_rock_1.asset",
                    "Assets/Tilemaps/Isometric/Tiles/FrozenMountain/decoration_snowcap_rock_2.asset",
                    "Assets/Tilemaps/Isometric/Tiles/FrozenMountain/decoration_sword_in_the_stone.asset",
                    DecoRoot + "Deco_PineTree.asset",
                    DecoRoot + "Deco_Rock_A.asset",
                    DecoRoot + "Deco_Rock_B.asset"
                },
                cliffSouth,
                cliffWest,
                cliffNorth,
                cliffEast,
                BiomeKind.FrozenMountain,
                0f,
                0.34f,
                0f,
                1f,
                0.58f,
                1f,
                0.58f,
                0.095f,
                0.035f,
                0.35f,
                211f,
                // z=2: bare rocky ground (mountain mid-section under the snow)
                midElevationPath: RandomRoot + "RandomTile_FrozenMountain_FlatGround.asset",
                // z=3+: snow-capped peak (bright white summit)
                peakPath: NeighbourRoot + "NeighbourTile_FrozenMountain_FlatSnow.asset"),
            CreateOrUpdateBiome(
                "Assets/World/Biomes/BiomeDefinition_FrozenCave.asset",
                "Frozen Cave",
                NeighbourRoot + "NeighbourTile_FrozenCave_RaisedFloor.asset",
                NeighbourRoot + "NeighbourTile_FrozenCave_RaisedWall.asset",
                null,
                new[] { RandomRoot + "RandomTile_FrozenCave_RaisedFloor.asset" },
                // Frozen Cave decorations: ice/crystal coral formations
                new[] { RandomRoot + "RandomTile_FrozenCave_Coral.asset" },
                cliffSouth,
                cliffWest,
                cliffNorth,
                cliffEast,
                BiomeKind.FrozenCave,
                0f,
                0.4f,
                0.3f,
                1f,
                0f,
                1f,
                0.62f,
                0.075f,
                0.03f,   // ↑ More crystal formations (was 0.012)
                0.5f,
                307f),
            CreateOrUpdateBiome(
                "Assets/World/Biomes/BiomeDefinition_Temple.asset",
                "Temple",
                NeighbourRoot + "NeighbourTile_Temple_AnimatedLava.asset",
                NeighbourRoot + "NeighbourTile_Temple_LavaWall.asset",
                new[] { RandomRoot + "RandomTile_Temple_FlatStone.asset" },
                new[]
                {
                    RandomRoot + "RandomTile_Temple_RaisedStone.asset",
                    RandomRoot + "RandomTile_Temple_RaisedBlueStone.asset"
                },
                // Temple decorations: purple blocks, blue stairs + imported ruin props
                new[]
                {
                    "Assets/Tilemaps/Isometric/Tiles/Temple/temple-purpleblock.asset",
                    "Assets/Tilemaps/Isometric/Tiles/Temple/temple-stairs-blue.asset",
                    DecoRoot + "Deco_Barrel.asset",
                    DecoRoot + "Deco_Chest.asset",
                    DecoRoot + "Deco_RuinedWall.asset",
                    DecoRoot + "Deco_Pillar.asset"
                },
                cliffSouth,
                cliffWest,
                cliffNorth,
                cliffEast,
                BiomeKind.Temple,
                0.45f,
                1f,
                0f,
                0.65f,
                0f,
                1f,
                0.7f,
                0.07f,
                0.025f,
                0.25f,
                419f,
                // z=2: stone mid-level
                midElevationPath: RandomRoot + "RandomTile_Temple_RaisedStone.asset",
                // z=3+: lava-topped peak (animated lava on tall temple formations)
                peakPath: NeighbourRoot + "NeighbourTile_Temple_AnimatedLava.asset"),
            CreateOrUpdateBiome(
                "Assets/World/Biomes/BiomeDefinition_Basic.asset",
                "Basic",
                NeighbourRoot + "NeighbourTile_Basic_FlatFloor.asset",
                NeighbourRoot + "NeighbourTile_Basic_RaisedFloor.asset",
                null,
                null,
                null,
                cliffSouth,
                cliffWest,
                cliffNorth,
                cliffEast,
                BiomeKind.Basic,
                0f,
                1f,
                0f,
                1f,
                0f,
                1f,
                0.66f,
                0.08f,
                0f,
                0.25f,
                523f)
        };

        // Clustered decoration system. For now ONLY the Plains/forest starter biome has
        // decorations — everything else is intentionally cleared while we polish the
        // starter area. Plains gets tree groves, light ground cover, and rare rock clumps.
        ConfigureDecorationCategories(allBiomes);
        return allBiomes;
    }

    /// <summary>
    /// Assigns the categorised (clustered) decoration sets per biome and clears the
    /// legacy flat decoration list. Only Plains is populated for now.
    /// </summary>
    private static void ConfigureDecorationCategories(IsoBiomeDefinition[] biomes)
    {
        foreach (IsoBiomeDefinition biome in biomes)
        {
            if (biome == null) continue;

            // Clear everything first so re-runs are deterministic.
            biome.decorationTiles = new TileBase[0];
            biome.treeTiles = new TileBase[0];
            biome.groundCoverTiles = new TileBase[0];
            biome.rockTiles = new TileBase[0];

            BiomeKind kind = biome.EffectiveBiomeKind;

            if (kind == BiomeKind.Plains)
            {
                // OPEN GRASSLAND: mostly negative space so the meadow can breathe.
                // Deciduous trees only (no conifers), in small sparse copses. The life
                // comes from flowers + wheat scattered lightly across open ground.
                biome.treeTiles = LoadTiles(new[]
                {
                    DecoRoot + "Deco_OakTree_A.asset",
                    DecoRoot + "Deco_OakTree_B.asset"
                });
                biome.groundCoverTiles = LoadTiles(new[]
                {
                    DecoRoot + "Deco_Flower.asset",
                    DecoRoot + "Deco_Wheat.asset",
                    DecoRoot + "Deco_Bush_A.asset",
                    RandomRoot + "RandomTile_Plains_Plants.asset"
                });
                biome.rockTiles = LoadTiles(new[]
                {
                    DecoRoot + "Deco_Rock_A.asset",
                    DecoRoot + "Deco_Rock_B.asset"
                });

                // Small, sparse groves; generous open ground; flowers provide the colour.
                biome.forestNoiseScale = 0.06f;     // smaller copses
                biome.forestThreshold = 0.62f;      // less area is "grove" → more open meadow
                biome.treeDensityInForest = 0.26f;  // sparse trees even inside a copse
                biome.groundCoverChance = 0.09f;    // lots of flowers/wheat
                biome.rockClusterThreshold = 0.76f; // rocks are rare
                biome.rockChanceInCluster = 0.07f;
            }
            else if (kind == BiomeKind.Forest)
            {
                // DENSE WOODLAND: broad forests dominated by conifers + oaks, with a
                // thick undergrowth of bushes/ferns and mossy rock outcrops. Logs and
                // stumps add forest-floor texture.
                biome.treeTiles = LoadTiles(new[]
                {
                    DecoRoot + "Deco_PineTree.asset",
                    DecoRoot + "Deco_PineTree.asset",   // weighted: conifers dominate
                    DecoRoot + "Deco_OakTree_A.asset",
                    DecoRoot + "Deco_OakTree_B.asset"
                });
                biome.groundCoverTiles = LoadTiles(new[]
                {
                    DecoRoot + "Deco_Bush_A.asset",
                    DecoRoot + "Deco_Bush_B.asset",
                    DecoRoot + "Deco_Flower.asset",
                    DecoRoot + "Deco_Stump.asset",
                    DecoRoot + "Deco_Log.asset"
                });
                biome.rockTiles = LoadTiles(new[]
                {
                    DecoRoot + "Deco_Rock_A.asset",
                    DecoRoot + "Deco_Rock_B.asset"
                });

                // Broad, dense forests with heavy undergrowth.
                biome.forestNoiseScale = 0.04f;     // larger, sweeping forests
                biome.forestThreshold = 0.40f;      // most of the biome is canopy
                biome.treeDensityInForest = 0.55f;  // dense trunks
                biome.groundCoverChance = 0.13f;    // thick undergrowth
                biome.rockClusterThreshold = 0.68f; // mossy outcrops more common
                biome.rockChanceInCluster = 0.10f;
            }

            EditorUtility.SetDirty(biome);
        }
    }

    private static IsoWorldChunkManager.BiomeTransitionRule[] CreateDefaultTransitionRules()
    {
        return new[]
        {
            CreateTransitionRule(BiomeKind.Plains, BiomeKind.Desert, 0.78f, 0.22f, 0.35f, 0.5f),
            CreateTransitionRule(BiomeKind.Plains, BiomeKind.FrozenMountain, 0.72f, 0.28f, 0.25f, 0.45f),
            CreateTransitionRule(BiomeKind.Desert, BiomeKind.FrozenMountain, 0.82f, 0.18f, 0.18f, 0.45f)
        };
    }

    private static IsoWorldChunkManager.BiomeTransitionRule CreateTransitionRule(
        BiomeKind from,
        BiomeKind to,
        float primaryWeight,
        float secondaryWeight,
        float decorationMultiplier,
        float resourceMultiplier)
    {
        return new IsoWorldChunkManager.BiomeTransitionRule
        {
            fromBiome = from,
            toBiome = to,
            primaryTileWeight = primaryWeight,
            secondaryTileWeight = secondaryWeight,
            decorationMultiplier = decorationMultiplier,
            resourceMultiplier = resourceMultiplier
        };
    }

    private static IsoLightingProfile[] CreateOrUpdateLightingProfiles()
    {
        EnsureFolder("Assets/World");
        EnsureFolder("Assets/World/Lighting");

        return new[]
        {
            CreateOrUpdateLightingProfile(
                LightingRoot + "Lighting_Day.asset",
                "Day",
                new Color(0.18f, 0.24f, 0.28f, 1f),
                new Color(0.82f, 0.86f, 0.8f, 1f),
                new Color(1f, 0.96f, 0.86f, 1f),
                1.05f,
                Color.white,
                Color.white),
            CreateOrUpdateLightingProfile(
                LightingRoot + "Lighting_Dusk.asset",
                "Dusk",
                new Color(0.24f, 0.16f, 0.2f, 1f),
                new Color(0.62f, 0.48f, 0.58f, 1f),
                new Color(1f, 0.62f, 0.36f, 1f),
                0.75f,
                new Color(0.95f, 0.78f, 0.7f, 1f),
                new Color(1f, 0.86f, 0.76f, 1f)),
            CreateOrUpdateLightingProfile(
                LightingRoot + "Lighting_Night.asset",
                "Night",
                new Color(0.08f, 0.1f, 0.16f, 1f),
                new Color(0.28f, 0.34f, 0.52f, 1f),
                new Color(0.42f, 0.52f, 0.9f, 1f),
                0.35f,
                new Color(0.52f, 0.62f, 0.88f, 1f),
                new Color(0.7f, 0.78f, 1f, 1f)),
            CreateOrUpdateLightingProfile(
                LightingRoot + "Lighting_Storm.asset",
                "Storm",
                new Color(0.11f, 0.14f, 0.15f, 1f),
                new Color(0.42f, 0.48f, 0.48f, 1f),
                new Color(0.58f, 0.68f, 0.72f, 1f),
                0.55f,
                new Color(0.7f, 0.82f, 0.78f, 1f),
                new Color(0.82f, 0.9f, 0.88f, 1f))
        };
    }

    private static IsoLightingProfile CreateOrUpdateLightingProfile(
        string path,
        string profileName,
        Color cameraBackground,
        Color ambientLight,
        Color directionalLightColor,
        float directionalLightIntensity,
        Color tilemapTint,
        Color spriteTint)
    {
        IsoLightingProfile profile = AssetDatabase.LoadAssetAtPath<IsoLightingProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<IsoLightingProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        profile.profileName = profileName;
        profile.cameraBackground = cameraBackground;
        profile.ambientLight = ambientLight;
        profile.directionalLightColor = directionalLightColor;
        profile.directionalLightIntensity = directionalLightIntensity;
        profile.tilemapTint = tilemapTint;
        profile.spriteTint = spriteTint;

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static IsoBiomeDefinition CreateOrUpdateBiome(
        string path,
        string biomeName,
        string flatPath,
        string raisedPath,
        string[] flatVariantPaths,
        string[] raisedVariantPaths,
        string[] decorationPaths,
        TileBase cliffSouth,
        TileBase cliffWest,
        TileBase cliffNorth,
        TileBase cliffEast,
        BiomeKind biomeKind,
        float temperatureMin,
        float temperatureMax,
        float moistureMin,
        float moistureMax,
        float elevationMin,
        float elevationMax,
        float raisedThreshold,
        float heightScale,
        float decorationChance,
        float transitionDecorationMultiplier,
        float biomeNoiseOffset,
        string midElevationPath = null,
        string peakPath = null)
    {
        IsoBiomeDefinition biome = AssetDatabase.LoadAssetAtPath<IsoBiomeDefinition>(path);
        if (biome == null)
        {
            biome = ScriptableObject.CreateInstance<IsoBiomeDefinition>();
            AssetDatabase.CreateAsset(biome, path);
        }

        biome.biomeName = biomeName;
        biome.biomeKind = biomeKind;
        biome.temperatureMin = temperatureMin;
        biome.temperatureMax = temperatureMax;
        biome.moistureMin = moistureMin;
        biome.moistureMax = moistureMax;
        biome.elevationMin = elevationMin;
        biome.elevationMax = elevationMax;
        biome.flatGroundTile = LoadTile(flatPath);
        biome.raisedRuleTile = LoadTile(raisedPath);
        biome.flatGroundVariants = LoadTiles(flatVariantPaths);
        biome.raisedGroundVariants = LoadTiles(raisedVariantPaths);
        biome.decorationTiles = LoadTiles(decorationPaths);
        biome.decorationChance = decorationChance;
        biome.baseDecorationDensity = decorationChance;
        biome.transitionDecorationMultiplier = transitionDecorationMultiplier;
        biome.colliderCliffSouth = cliffSouth;
        biome.colliderCliffWest = cliffWest;
        biome.colliderCliffNorth = cliffNorth;
        biome.colliderCliffEast = cliffEast;
        biome.raisedTileThreshold = raisedThreshold;
        biome.heightNoiseScale = heightScale;
        biome.biomeNoiseOffset = biomeNoiseOffset;
        biome.midElevationTile = string.IsNullOrEmpty(midElevationPath) ? null : LoadTile(midElevationPath);
        biome.peakTile = string.IsNullOrEmpty(peakPath) ? null : LoadTile(peakPath);

        EditorUtility.SetDirty(biome);
        return biome;
    }

    private static TileBase LoadTile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (tile == null)
        {
            Debug.LogWarning($"Tile asset not found: {path}");
        }

        return tile;
    }

    private static TileBase[] LoadTiles(string[] paths)
    {
        if (paths == null || paths.Length == 0)
        {
            return new TileBase[0];
        }

        List<TileBase> tiles = new List<TileBase>();
        foreach (string path in paths)
        {
            TileBase tile = LoadTile(path);
            if (tile != null)
            {
                tiles.Add(tile);
            }
        }

        return tiles.ToArray();
    }

    private static GameObject CreateIsoPlayer(Grid grid, IsoWorldChunkManager world)
    {
        GameObject player = new GameObject("Player");
        player.layer = LayerMask.NameToLayer("Player");
        player.transform.position = grid.CellToWorld(Vector3Int.zero);

        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Trigger-only foot volume. Movement/collision against terrain is
        // controlled by IsoPlayerController so hidden cliff colliders cannot
        // push the player away from the sampled elevation position.
        CircleCollider2D footCollider = player.AddComponent<CircleCollider2D>();
        footCollider.radius = 0.2f;
        footCollider.offset = new Vector2(0f, 0f);
        footCollider.isTrigger = true;

        player.AddComponent<SortingGroup>();
        CreatePlayerSpriteRenderer(player.transform);

        IsoPlayerController playerController = player.AddComponent<IsoPlayerController>();
        playerController.movementSpeed = 5f;
        playerController.acceleration = 34f;
        playerController.deceleration = 48f;
        playerController.wallStopDeceleration = 90f;
        playerController.allowWallSlide = false;
        playerController.useCameraRelativeInput = true;
        playerController.maxWalkStepHeight = 0;  // Cannot walk up cliffs (must jump)
        playerController.footSampleRadius = 0.35f;
        playerController.maxJumpHeight = 1;       
        playerController.spriteHeightOffsetPerLevel = 0.25f;
        playerController.jumpEdgeForgivenessDistance = 0.36f;
        playerController.jumpEdgeSearchSteps = 4;
        playerController.jumpDuration = 0.40f;
        playerController.jumpArcHeight = 0.90f;
        playerController.landingLockoutDuration = 0.03f;
        playerController.jumpMomentumDistance = 1.20f;
        playerController.jumpMinimumDistance = 0.80f;
        playerController.jumpMomentumSpeedScale = 0.18f;
        playerController.visualScale = 1f;
        playerController.spriteGroundLift = 0.06f;
        playerController.grid = grid;
        playerController.world = world;
        AssignPlayerWalkSheet(playerController);

        // Drop shadow that follows the sun's direction. Generates its own sprite
        // procedurally — no external asset needed.
        DropShadowCaster shadowCaster = player.AddComponent<DropShadowCaster>();
        shadowCaster.shadowWidth = 1.0f;
        shadowCaster.shadowHeight = 0.45f;
        shadowCaster.maxOpacity = 0.7f;
        shadowCaster.minOpacity = 0.15f;
        shadowCaster.shadowStretchAmount = 0.8f;
        shadowCaster.maxLateralOffset = 0.5f;
        shadowCaster.groundYOffset = -0.25f;     // Position shadow below player's center
        shadowCaster.sortingOrderOffset = -10;   // Render well below player sprite

        return player;
    }

    private static SpriteRenderer CreatePlayerSpriteRenderer(Transform playerRoot)
    {
        Transform existing = playerRoot.Find("SpriteRenderer");
        if (existing != null)
        {
            return existing.GetComponent<SpriteRenderer>() ?? existing.gameObject.AddComponent<SpriteRenderer>();
        }

        GameObject spriteObject = new GameObject("SpriteRenderer");
        spriteObject.transform.SetParent(playerRoot);
        spriteObject.transform.localPosition = Vector3.zero;
        spriteObject.transform.localRotation = Quaternion.identity;
        spriteObject.transform.localScale = Vector3.one;
        SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        return spriteRenderer;
    }

    private static void AssignPlayerWalkSheet(IsoPlayerController playerController)
    {
        playerController.walkSpriteSheet = LoadTexture(PlayerWalkSheetPath);
        playerController.walkSheetColumns = 4;
        playerController.walkSheetRows = 8;
        playerController.walkSpritePixelsPerUnit = 128f;
        playerController.animateWalkFrames = false;
        playerController.idleSpriteSheet = LoadTexture(PlayerIdleSheetPath);
        playerController.idleSheetColumns = 4;
        playerController.idleSheetRows = 8;
        playerController.idleFrameDuration = 0.18f;
        playerController.animateIdleFrames = true;
        playerController.idleAudioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(PlayerIdleAudioPath);
        playerController.idleAudioVolume = 0.22f;
        playerController.useWalkBob = true;
        playerController.walkBobHeight = 0.035f;
        playerController.walkBobFrequency = 8f;
    }

    private static Texture2D LoadTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning($"Player texture asset not found: {path}");
        }

        return texture;
    }

    private static GameObject CreateSelectionMarker()
    {
        GameObject marker = new GameObject("Tile Selection Marker");
        marker.SetActive(false);

        LineRenderer line = marker.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = 4;
        line.widthMultiplier = 0.035f;
        line.sortingOrder = 50;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(1f, 0.9f, 0.25f, 1f);
        line.endColor = new Color(1f, 0.9f, 0.25f, 1f);
        line.SetPosition(0, new Vector3(0f, 0.25f, 0f));
        line.SetPosition(1, new Vector3(0.5f, 0f, 0f));
        line.SetPosition(2, new Vector3(0f, -0.25f, 0f));
        line.SetPosition(3, new Vector3(-0.5f, 0f, 0f));

        return marker;
    }

    private static GameObject CreateCamera(Transform target)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;  // Zoomed in (was 10) — closer view of the action
        camera.backgroundColor = new Color(0.18f, 0.24f, 0.28f, 1f);
        camera.transparencySortMode = TransparencySortMode.CustomAxis;
        camera.transparencySortAxis = new Vector3(0f, 1f, -0.26f);
        camera.allowHDR = false;       // HDR not needed for 2D sprites
        camera.allowMSAA = false;      // MSAA creates seams between pixel tiles — keep off

        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<AudioListenerEnsurer>();
        cameraObject.AddComponent<SceneValidator>();

        // Smoother, more dynamic camera follow with lookahead
        CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
        follow.target = target;
        follow.offset = new Vector3(0f, 0f, -10f);
        follow.useSmoothDamp = true;
        follow.smoothDampTime = 0.22f;
        follow.lookaheadDistance = 0.45f;
        follow.lookaheadResponseSpeed = 3f;
        follow.lookaheadReverseSmoothTime = 0.35f;

        // Graphics polish: vignette, atmospheric particles, color grading
        GraphicsEnhancer enhancer = cameraObject.AddComponent<GraphicsEnhancer>();
        enhancer.targetCamera = camera;
        enhancer.enableVignette = true;
        enhancer.vignetteStrength = 0.45f;
        enhancer.vignetteRadius = 0.85f;
        enhancer.enableAtmosphericParticles = true;
        enhancer.particleCount = 60;
        enhancer.particleSpawnRadius = 8f;

        return cameraObject;
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
            {
                sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
        string folder = Path.GetFileName(folderPath);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
