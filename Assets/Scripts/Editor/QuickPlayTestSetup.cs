using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using EthraClone.TrialWeek;

/// <summary>
/// Tools > LIT-ISO > Playtest > Quick Play Test
///
/// One-click tool that wires the currently-open scene for a full play-through.
/// Unlike Create Infinite Plains Prototype, this tool is ADDITIVE — it never
/// destroys or replaces objects, only creates missing ones and wires references.
///
/// Safe to re-run at any time. Each system is found-or-created:
///   1. IsoWorldGrid    — Grid + IsoWorldChunkManager + biome references
///   2. Player          — IsoPlayerController, Rigidbody2D, all gameplay scripts
///   3. IsoRuntimeRecorder
///   4. Tile Selection Marker
///   5. Main Camera     — CameraFollow targeting the Player
///   6. Directional Light
///   7. IsoLightingController (day/night visual)
///   8. DayNightMusicManager
///   9. GameplayHUD     — hotbar, health bar, pickup notifications (via GameplayLayerSetup)
///  10. GraphicsSettings — custom transparency sort for IsometricZAsY
///
/// Also calls "Create Starter Gameplay Assets" so ItemDefinition and
/// ResourceNodeDefinition ScriptableObjects are always present.
/// </summary>
public static class QuickPlayTestSetup
{
    // -------------------------------------------------------------------------
    // Asset paths (load existing — expect them to exist from prior setup runs)
    // -------------------------------------------------------------------------

    private const string PlainsBiomePath    = "Assets/World/Biomes/BiomeDefinition_Plains.asset";
    private const string LightingDayPath    = "Assets/World/Lighting/Lighting_Day.asset";
    private const string LightingDuskPath   = "Assets/World/Lighting/Lighting_Dusk.asset";
    private const string LightingNightPath  = "Assets/World/Lighting/Lighting_Night.asset";
    private const string LightingStormPath  = "Assets/World/Lighting/Lighting_Storm.asset";
    private const string DayMusicPath       = "Assets/Audio/Music/Music_Day_AmbientExploration.flac";
    private const string NightMusicPath     = "Assets/Audio/Music/Music_Night_HarpTheme.flac";
    private const string PlayerWalkSheetPath = "Assets/Resources/Characters/Player/HollowedLight_512x1024.png";
    private const string PlayerIdleSheetPath = "Assets/Resources/Characters/Player/ReferenceKnight_Idle_512x1024.png";
    private const string PlayerIdleAudioPath = "Assets/Resources/Audio/Player/Player_Idle_IceHum.wav";
    private const string CollisionBlockPath = "Assets/Tilemaps/Isometric/Colliders/ColliderTiles/CollisionBlock.asset";

    // -------------------------------------------------------------------------
    // Menu item
    // -------------------------------------------------------------------------

    [MenuItem("Tools/LIT-ISO/Playtest/Quick Play Test", false, 0)]
    public static void RunSetup()
    {
        RunSetup(showDialog: true);
    }

    [MenuItem("Tools/LIT-ISO/Setup/Full Golden Path Setup", false, 99)]
    public static void RunFullGoldenPathSetup()
    {
        // 1. Create MenuScene (this also auto-assigns the background image)
        MenuSceneBuilder.CreateMenuScene();

        // 2. Configure build settings
        BuildSettingsConfigurator.ConfigureBuildSettings();

        // 3. Open SampleScene and run Quick Play Test setup on it
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

        // 4. Run the standard setup on SampleScene
        string log = RunSetup(showDialog: false);

        EditorUtility.DisplayDialog(
            "✅ GOLDEN PATH COMPLETE",
            "✓ MenuScene created with welcome screen\n" +
            "✓ Background image auto-assigned (CampfireMenu.png)\n" +
            "✓ Build settings configured (MenuScene → SampleScene)\n" +
            "✓ SampleScene fully configured with all gameplay systems\n\n" +
            "READY TO TEST:\n" +
            "Press Play (Ctrl+P) or File > Build and Run\n\n" +
            "You will see:\n" +
            "1. MenuScene loads with campfire background\n" +
            "2. Click 'New Game' to create a world\n" +
            "3. SampleScene loads with procedural world\n\n" + log,
            "OK");
    }

    public static string RunSetup(bool showDialog)
    {
        var log = new StringBuilder();
        log.AppendLine("=== Quick Play Test Setup ===\n");

        // ------------------------------------------------------------------
        // 0a. Ensure WorldManager exists for world seed/difficulty
        // ------------------------------------------------------------------
        SetupWorldManager(log);

        // ------------------------------------------------------------------
        // 0. Ensure starter ScriptableObject assets exist
        // ------------------------------------------------------------------
        EnsureStarterAssets(log);

        // ------------------------------------------------------------------
        // 1. IsoWorldGrid
        // ------------------------------------------------------------------
        GameObject gridObj = SetupWorldGrid(log);
        Grid grid                   = gridObj.GetComponent<Grid>();
        IsoWorldChunkManager world  = gridObj.GetComponent<IsoWorldChunkManager>();

        // ------------------------------------------------------------------
        // 2. Player
        // ------------------------------------------------------------------
        GameObject player = SetupPlayer(grid, world, log);

        // Wire world → player
        world.player = player.transform;

        // ------------------------------------------------------------------
        // 3. IsoRuntimeRecorder
        // ------------------------------------------------------------------
        IsoRuntimeRecorder recorder = SetupRecorder(world, player, log);
        world.recorder = recorder;

        // ------------------------------------------------------------------
        // 4. Tile Selection Marker
        // ------------------------------------------------------------------
        GameObject marker = SetupSelectionMarker(log);

        // ------------------------------------------------------------------
        // 5. Main Camera
        // ------------------------------------------------------------------
        GameObject cameraObj = SetupCamera(player.transform, log);
        Camera cam = cameraObj.GetComponent<Camera>();

        // ------------------------------------------------------------------
        // 6. Wire IsoPlayerController references
        // ------------------------------------------------------------------
        WirePlayerController(player, cam, recorder, marker, log);

        // ------------------------------------------------------------------
        // 7. Directional Light + IsoLightingController
        // ------------------------------------------------------------------
        Light dirLight = SetupDirectionalLight(log);
        SetupLightingController(cam, dirLight, log);

        // ------------------------------------------------------------------
        // 8. DayNightMusicManager
        // ------------------------------------------------------------------
        DayNightMusicManager musicMgr = SetupDayNightMusic(log);

        // ------------------------------------------------------------------
        // 8b. SunController (invisible orbital sun + dynamic lighting)
        // ------------------------------------------------------------------
        SetupSunController(musicMgr, dirLight, log);

        // ------------------------------------------------------------------
        // 9. Gameplay Layer (inventory, hotbar, health bar, notifications)
        // ------------------------------------------------------------------
        SetupGameplayLayer(log);

        // ------------------------------------------------------------------
        // 9b. Enemy spawning (data-driven slime variants)
        // ------------------------------------------------------------------
        SlimeEnemySetup.ConfigureWorldSlimeSpawns(world, log);

        // ------------------------------------------------------------------
        // 10. Graphics settings for IsometricZAsY
        // ------------------------------------------------------------------
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, -0.26f);
        log.AppendLine("✓  Graphics transparency sort set to IsometricZAsY axis.");

        // ------------------------------------------------------------------
        // Done
        // ------------------------------------------------------------------
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string sceneName = string.IsNullOrEmpty(scene.path) ? "(unsaved scene)" : scene.name;
        log.AppendLine($"\nScene: '{sceneName}'");
        log.AppendLine("Press  ▶ Play  to test.\n");
        log.AppendLine("Tip: Assign sprite art to ItemDefinition assets in Assets/World/Items/");

        string logText = log.ToString();
        Debug.Log("[QuickPlayTestSetup] " + logText);
        if (showDialog)
            EditorUtility.DisplayDialog("Quick Play Test Ready", logText, "OK");
        return logText;
    }

    // =========================================================================
    // 0. Starter assets
    // =========================================================================

    private static void EnsureStarterAssets(StringBuilder log)
    {
        bool hasItems = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
            "Assets/World/Items/Item_Wood.asset") != null;
        if (!hasItems)
        {
            GameplayLayerSetup.CreateStarterAssets();
            log.AppendLine("✓  Created starter Item/Node assets.");
        }
        else
        {
            log.AppendLine("·  Starter assets already exist.");
        }
    }

    // =========================================================================
    // 0a. WorldManager (persistent world config: seed, difficulty)
    // =========================================================================

    private static void SetupWorldManager(StringBuilder log)
    {
        // Ensure WorldManager exists and persists across scenes
        WorldManager existing = Object.FindFirstObjectByType<WorldManager>();
        if (existing != null)
        {
            log.AppendLine("·  WorldManager already exists.");
            return;
        }

        // Create WorldManager with development defaults
        GameObject wmGO = new GameObject("WorldManager");
        WorldManager wm = wmGO.AddComponent<WorldManager>();
        wm.SetWorld("Development World", "12345", 1);  // Seed, Difficulty (0=easy, 1=normal, 2=hard)

        log.AppendLine("✓  WorldManager created (dev defaults: seed=12345, difficulty=normal).");
    }

    // =========================================================================
    // 1. IsoWorldGrid
    // =========================================================================

    private static GameObject SetupWorldGrid(StringBuilder log)
    {
        IsoWorldChunkManager existing = Object.FindFirstObjectByType<IsoWorldChunkManager>();
        if (existing != null)
        {
            log.AppendLine($"·  IsoWorldGrid found: '{existing.gameObject.name}'.");
            EnsureGridBiomes(existing, log);
            return existing.gameObject;
        }

        // Create fresh grid
        GameObject gridObj = new GameObject("IsoWorldGrid");
        Grid grid = gridObj.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
        grid.cellSize   = new Vector3(1f, 0.5f, 1f);

        IsoWorldChunkManager world = gridObj.AddComponent<IsoWorldChunkManager>();
        world.grid             = grid;
        world.cliffColliderTile = AssetDatabase.LoadAssetAtPath<TileBase>(CollisionBlockPath);
        world.seed             = 12345;
        world.chunkSize        = 32;
        world.activeRadius     = 1;
        world.poolInactiveChunks = true;
        world.biomeNoiseScale  = 0.0125f;
        world.maxTerrainHeight = 3;
        world.lowlandMaxTerrainHeight = 2;
        world.terrainHeightFalloff = 1.7f;
        ConfigureClimateGeneration(world);
        world.showTileBorders  = true;
        world.tileBorderColor  = new Color(0f, 0f, 0f, 0.16f);
        world.showHeightEdgeShadows = true;
        world.heightEdgeShadowColor = new Color(0f, 0f, 0f, 0.18f);

        EnsureGridBiomes(world, log);

        log.AppendLine("✓  Created IsoWorldGrid.");
        return gridObj;
    }

    private static void EnsureGridBiomes(IsoWorldChunkManager world, StringBuilder log)
    {
        world.showTileBorders = true;
        world.tileBorderColor = new Color(0f, 0f, 0f, 0.16f);
        world.showHeightEdgeShadows = true;
        world.heightEdgeShadowColor = new Color(0f, 0f, 0f, 0.18f);
        world.maxTerrainHeight = 3;
        world.lowlandMaxTerrainHeight = 1;   // gentle lowlands — single-step rises, not pillars
        world.terrainHeightFalloff = 2.2f;   // steeper falloff → tall stacks are rare

        // Starter zone: player always spawns at cell (0,0) in a LARGE flat Plains meadow
        // that fans out gently into procedural terrain. Generous flat area + slow ramp so
        // the opening is fully walkable, not a cliff maze.
        world.useStarterZone = true;
        world.starterZoneCenter = Vector2Int.zero;
        world.starterZoneBiome = BiomeKind.Plains;
        world.starterZoneRadius = 44;        // was 28 — bigger safe opening
        world.starterZoneFlattenRadius = 18; // was 12 — larger guaranteed-flat core
        world.starterZoneRampStep = 9;       // was 5 — gentler climb out of the clearing

        ConfigureClimateGeneration(world);
        EditorUtility.SetDirty(world);

        // Step 1: convert the dropped decoration PNGs into Tile assets FIRST, so the
        // biome definitions below can bake them in by path. Also tidies building /
        // high-detail sprites into their holding folders. Idempotent.
        int decoratedBiomes = BiomeDecorationImporter.RunImport(showDialog: false);
        log.AppendLine($"✓  Prepared biome decoration tiles ({decoratedBiomes} biome group(s)).");

        // Step 2: build the biome definitions. These reference the Deco_ tiles by
        // path, so trees/props are wired into decorationTiles[] permanently on every
        // rebuild — no fragile append-after-rebuild step.
        IsoWorldSetup.CreateOrUpdateBiomeDefinitions();

        IsoBiomeDefinition plains = AssetDatabase.LoadAssetAtPath<IsoBiomeDefinition>(PlainsBiomePath);
        if (plains == null)
        {
            IsoWorldSetup.CreateOrUpdateBiomeDefinitions();
            plains = AssetDatabase.LoadAssetAtPath<IsoBiomeDefinition>(PlainsBiomePath);
        }

        if (plains != null)
        {
            // Starter world is intentionally Plains + Forest ONLY. The other biomes
            // (Desert, Frozen, Temple) still exist as assets for later, but are excluded
            // from the active set so the starter experience is a cohesive grassland/woodland.
            IsoBiomeDefinition forest = AssetDatabase.LoadAssetAtPath<IsoBiomeDefinition>(
                "Assets/World/Biomes/BiomeDefinition_Forest.asset");

            world.plainsBiome = plains;
            world.biomes = forest != null
                ? new[] { plains, forest }
                : new[] { plains };
            EditorUtility.SetDirty(world);
            log.AppendLine(forest != null
                ? "✓  Biome references wired: Plains + Forest starter world."
                : "⚠  Forest biome not found — wired Plains only.");
        }
        else
        {
            log.AppendLine("⚠  Could not find/create Plains biome — run 'Create Infinite Plains Prototype' first.");
        }
    }

    private static void ConfigureClimateGeneration(IsoWorldChunkManager world)
    {
        world.temperatureNoiseScale = 0.008f;     // More frequent biome variety
        world.moistureNoiseScale = 0.007f;
        world.continentNoiseScale = 0.006f;
        world.biomeBlendRadius = 3;
        world.biomeBlendNoiseScale = 0.08f;
        world.transitionTileChance = 0.35f;
        world.transitionRules = new[]
        {
            // Plains ↔ Forest: soft edges where meadow gives way to woodland.
            CreateTransitionRule(BiomeKind.Plains, BiomeKind.Forest, 0.7f, 0.3f, 0.6f, 0.6f)
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

    // =========================================================================
    // 2. Player
    // =========================================================================

    private static GameObject SetupPlayer(Grid grid, IsoWorldChunkManager world, StringBuilder log)
    {
        IsoPlayerController existing = Object.FindFirstObjectByType<IsoPlayerController>();
        if (existing != null)
        {
            log.AppendLine($"·  Player found: '{existing.gameObject.name}'.");
            EnsurePlayerComponents(existing.gameObject, grid, world, log);
            return existing.gameObject;
        }

        // Create player from scratch
        GameObject player = new GameObject("Player");
        player.layer = LayerMask.NameToLayer("Player");
        player.transform.position = grid != null ? grid.CellToWorld(Vector3Int.zero) : Vector3.zero;

        Rigidbody2D rb       = player.AddComponent<Rigidbody2D>();
        rb.gravityScale      = 0;
        rb.constraints       = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation     = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D foot = player.AddComponent<CircleCollider2D>();
        foot.radius  = 0.2f;
        foot.offset  = Vector2.zero;
        foot.isTrigger = true;

        player.AddComponent<SortingGroup>();
        CreatePlayerSpriteRenderer(player.transform);

        IsoPlayerController ctrl = player.AddComponent<IsoPlayerController>();
        AssignPlayerWalkSheet(ctrl);
        ctrl.movementSpeed      = 5f;
        ctrl.acceleration       = 34f;
        ctrl.deceleration       = 48f;
        ctrl.wallStopDeceleration = 90f;
        ctrl.allowWallSlide     = true;  // slide along cliffs instead of sticking
        ctrl.useCameraRelativeInput = true;
        ctrl.maxWalkStepHeight  = 0;   // Cannot walk up cliffs — must jump
        ctrl.footSampleRadius   = 0.28f;
        ctrl.maxJumpHeight      = 1;   
        ctrl.spriteHeightOffsetPerLevel = 0.25f;
        ctrl.jumpEdgeForgivenessDistance = 0.36f;
        ctrl.jumpEdgeSearchSteps = 4;
        ctrl.jumpDuration       = 0.40f;
        ctrl.jumpArcHeight      = 0.90f;
        ctrl.landingLockoutDuration = 0.03f;
        ctrl.jumpMomentumDistance = 1.20f;
        ctrl.jumpMinimumDistance = 0.80f;
        ctrl.jumpMomentumSpeedScale = 0.18f;
        ctrl.visualScale        = 1f;
        ctrl.spriteGroundLift   = 0.06f;
        ctrl.grid               = grid;
        ctrl.world              = world;

        EnsurePlayerComponents(player, grid, world, log);

        log.AppendLine("✓  Created Player.");
        return player;
    }

    private static void EnsurePlayerComponents(GameObject player,
                                                Grid grid,
                                                IsoWorldChunkManager world,
                                                StringBuilder log)
    {
        // Wire Grid / World references if missing (idempotent)
        IsoPlayerController ctrl = player.GetComponent<IsoPlayerController>();
        if (player.layer == 0) player.layer = LayerMask.NameToLayer("Player");
        if (ctrl != null)
        {
            if (ctrl.grid  == null && grid  != null) { ctrl.grid  = grid;  EditorUtility.SetDirty(ctrl); }
            if (ctrl.world == null && world != null) { ctrl.world = world; EditorUtility.SetDirty(ctrl); }
            ctrl.acceleration = 34f;
            ctrl.deceleration = 48f;
            ctrl.wallStopDeceleration = 90f;
            ctrl.allowWallSlide = true;   // slide along cliff edges instead of catching/sticking
            ctrl.useCameraRelativeInput = true;
            ctrl.maxWalkStepHeight = 0;
            ctrl.footSampleRadius = 0.28f; // slightly smaller footprint → less edge-catching
            ctrl.spriteHeightOffsetPerLevel = 0.25f;
            ctrl.spriteHeightOffsetPerLevel = 0.25f;
            ctrl.maxJumpHeight = 1;
            ctrl.jumpEdgeForgivenessDistance = 0.36f;
            ctrl.jumpEdgeSearchSteps = 4;
            ctrl.landingLockoutDuration = 0.03f;
            EditorUtility.SetDirty(ctrl);
            AssignPlayerWalkSheet(ctrl);
        }

        CreatePlayerSpriteRenderer(player.transform);

        // Rigidbody2D
        if (player.GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D rb  = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
        }

        CircleCollider2D foot = player.GetComponent<CircleCollider2D>();
        if (foot == null)
        {
            foot = player.AddComponent<CircleCollider2D>();
            foot.radius = 0.2f;
            foot.offset = Vector2.zero;
        }
        foot.isTrigger = true;
        EditorUtility.SetDirty(foot);

        // Drop shadow — auto-finds SunController on Start
        if (player.GetComponent<DropShadowCaster>() == null)
        {
            DropShadowCaster shadow = player.AddComponent<DropShadowCaster>();
            shadow.shadowWidth = 1.0f;
            shadow.shadowHeight = 0.45f;
            shadow.maxOpacity = 0.7f;
            shadow.minOpacity = 0.15f;
            shadow.shadowStretchAmount = 0.8f;
            shadow.maxLateralOffset = 0.5f;
            shadow.groundYOffset = -0.25f;
            shadow.sortingOrderOffset = -10;
            log.AppendLine("✓  Added DropShadowCaster to Player.");
        }

        RemoveComponentIfPresent<Animator>(player);
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

    private static void AssignPlayerWalkSheet(IsoPlayerController ctrl)
    {
        ctrl.walkSpriteSheet = LoadTex(PlayerWalkSheetPath);
        ctrl.walkSheetColumns = 4;
        ctrl.walkSheetRows = 8;
        ctrl.walkSpritePixelsPerUnit = 128f;
        ctrl.animateWalkFrames = false;
        ctrl.idleSpriteSheet = LoadTex(PlayerIdleSheetPath);
        ctrl.idleSheetColumns = 4;
        ctrl.idleSheetRows = 8;
        ctrl.idleFrameDuration = 0.18f;
        ctrl.animateIdleFrames = true;
        ctrl.idleAudioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(PlayerIdleAudioPath);
        ctrl.idleAudioVolume = 0.22f;
        ctrl.useWalkBob = true;
        ctrl.walkBobHeight = 0.035f;
        ctrl.walkBobFrequency = 8f;
        EditorUtility.SetDirty(ctrl);
    }

    private static void RemoveComponentIfPresent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            Object.DestroyImmediate(component);
        }
    }

    // =========================================================================
    // 3. IsoRuntimeRecorder
    // =========================================================================

    private static IsoRuntimeRecorder SetupRecorder(IsoWorldChunkManager world,
                                                     GameObject player,
                                                     StringBuilder log)
    {
        IsoRuntimeRecorder existing = Object.FindFirstObjectByType<IsoRuntimeRecorder>();
        if (existing != null)
        {
            existing.world  = world;
            existing.player = player.transform;
            EditorUtility.SetDirty(existing);
            log.AppendLine($"·  IsoRuntimeRecorder found and re-wired.");
            return existing;
        }

        GameObject recObj   = new GameObject("Iso Runtime Recorder");
        IsoRuntimeRecorder r = recObj.AddComponent<IsoRuntimeRecorder>();
        r.world  = world;
        r.player = player.transform;
        log.AppendLine("✓  Created IsoRuntimeRecorder.");
        return r;
    }

    // =========================================================================
    // 4. Selection Marker
    // =========================================================================

    private static GameObject SetupSelectionMarker(StringBuilder log)
    {
        // Look for existing by name
        GameObject existing = GameObject.Find("Tile Selection Marker");
        if (existing != null)
        {
            log.AppendLine("·  Selection Marker found.");
            return existing;
        }

        GameObject marker = new GameObject("Tile Selection Marker");
        marker.SetActive(false);

        LineRenderer lr = marker.AddComponent<LineRenderer>();
        lr.loop             = true;
        lr.useWorldSpace    = false;
        lr.positionCount    = 4;
        lr.widthMultiplier  = 0.035f;
        lr.sortingOrder     = 50;
        lr.material         = new Material(Shader.Find("Sprites/Default"));
        lr.startColor       = new Color(1f, 0.9f, 0.25f, 1f);
        lr.endColor         = new Color(1f, 0.9f, 0.25f, 1f);
        lr.SetPosition(0, new Vector3( 0f,    0.25f, 0f));
        lr.SetPosition(1, new Vector3( 0.5f,  0f,    0f));
        lr.SetPosition(2, new Vector3( 0f,   -0.25f, 0f));
        lr.SetPosition(3, new Vector3(-0.5f,  0f,    0f));

        log.AppendLine("✓  Created Tile Selection Marker.");
        return marker;
    }

    // =========================================================================
    // 5. Main Camera
    // =========================================================================

    private static GameObject SetupCamera(Transform playerTransform, StringBuilder log)
    {
        // Use existing main camera if present
        Camera main = Camera.main;
        if (main != null)
        {
            CameraFollow follow = main.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = main.gameObject.AddComponent<CameraFollow>();
                follow.offset      = new Vector3(0f, 0f, -10f);
            }
            follow.useSmoothDamp = true;
            follow.smoothDampTime = 0.22f;
            follow.lookaheadDistance = 0.45f;
            follow.lookaheadResponseSpeed = 3f;
            follow.lookaheadReverseSmoothTime = 0.35f;
            if (follow.target == null)
                follow.target = playerTransform;

            // Ensure safety scripts are present
            if (main.gameObject.GetComponent<AudioListener>() == null)
                main.gameObject.AddComponent<AudioListener>();
            if (main.gameObject.GetComponent<AudioListenerEnsurer>() == null)
                main.gameObject.AddComponent<AudioListenerEnsurer>();
            if (main.gameObject.GetComponent<SceneValidator>() == null)
                main.gameObject.AddComponent<SceneValidator>();
            if (main.gameObject.GetComponent<GraphicsEnhancer>() == null)
            {
                GraphicsEnhancer enhancerExisting = main.gameObject.AddComponent<GraphicsEnhancer>();
                enhancerExisting.targetCamera = main;
                enhancerExisting.enableVignette = true;
                enhancerExisting.vignetteStrength = 0.45f;
                enhancerExisting.enableAtmosphericParticles = true;
                enhancerExisting.particleCount = 60;
            }

            // Zoom in if currently zoomed out
            if (main.orthographicSize > 8f)
            {
                main.orthographicSize = 6f;
                log.AppendLine("·  Zoomed Main Camera in (orthographicSize=6).");
            }

            log.AppendLine($"·  Main Camera found: '{main.gameObject.name}', CameraFollow wired.");
            return main.gameObject;
        }

        // Create one
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(0f, 0f, -10f);

        Camera cam             = camObj.AddComponent<Camera>();
        cam.orthographic       = true;
        cam.orthographicSize   = 6f;   // Zoomed in (was 10) — closer view
        cam.backgroundColor    = new Color(0.18f, 0.24f, 0.28f, 1f);
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = new Vector3(0f, 1f, -0.26f);
        cam.allowHDR           = false;  // not needed for 2D sprites
        cam.allowMSAA          = false;  // MSAA creates seams between pixel tiles — keep off

        camObj.AddComponent<AudioListener>();
        camObj.AddComponent<AudioListenerEnsurer>();
        camObj.AddComponent<SceneValidator>();

        CameraFollow cf = camObj.AddComponent<CameraFollow>();
        cf.target      = playerTransform;
        cf.offset      = new Vector3(0f, 0f, -10f);
        cf.useSmoothDamp = true;
        cf.smoothDampTime = 0.22f;
        cf.lookaheadDistance = 0.45f;
        cf.lookaheadResponseSpeed = 3f;
        cf.lookaheadReverseSmoothTime = 0.35f;

        // Graphics polish
        GraphicsEnhancer enhancer = camObj.AddComponent<GraphicsEnhancer>();
        enhancer.targetCamera = cam;
        enhancer.enableVignette = true;
        enhancer.vignetteStrength = 0.45f;
        enhancer.vignetteRadius = 0.85f;
        enhancer.enableAtmosphericParticles = true;
        enhancer.particleCount = 60;
        enhancer.particleSpawnRadius = 8f;

        log.AppendLine("✓  Created Main Camera with GraphicsEnhancer.");
        return camObj;
    }

    // =========================================================================
    // 6. Wire IsoPlayerController references
    // =========================================================================

    private static void WirePlayerController(GameObject player,
                                              Camera cam,
                                              IsoRuntimeRecorder recorder,
                                              GameObject marker,
                                              StringBuilder log)
    {
        IsoPlayerController ctrl = player.GetComponent<IsoPlayerController>();
        if (ctrl == null) return;

        bool changed = false;
        if (ctrl.inputCamera == null && cam != null)   { ctrl.inputCamera     = cam;              changed = true; }
        if (ctrl.recorder    == null && recorder != null) { ctrl.recorder      = recorder;         changed = true; }
        if (ctrl.selectionMarker == null && marker != null) { ctrl.selectionMarker = marker.transform; changed = true; }

        if (changed)
        {
            EditorUtility.SetDirty(ctrl);
            log.AppendLine("✓  IsoPlayerController references wired (camera, recorder, marker).");
        }
        else
        {
            log.AppendLine("·  IsoPlayerController references already wired.");
        }
    }

    // =========================================================================
    // 7. Directional Light + IsoLightingController
    // =========================================================================

    private static Light SetupDirectionalLight(StringBuilder log)
    {
        // Find any existing directional light
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
            if (l.type == LightType.Directional)
            {
                log.AppendLine($"·  Directional Light found: '{l.gameObject.name}'.");
                return l;
            }

        GameObject lightObj = new GameObject("Directional Light");
        Light light         = lightObj.AddComponent<Light>();
        light.type          = LightType.Directional;
        light.color         = new Color(1f, 0.96f, 0.86f, 1f);
        light.intensity     = 1.05f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        log.AppendLine("✓  Created Directional Light.");
        return light;
    }

    private static void SetupLightingController(Camera cam, Light dirLight, StringBuilder log)
    {
        IsoLightingController existing = Object.FindFirstObjectByType<IsoLightingController>();
        if (existing != null)
        {
            if (existing.targetCamera    == null) existing.targetCamera    = cam;
            if (existing.directionalLight == null) existing.directionalLight = dirLight;
            EditorUtility.SetDirty(existing);
            log.AppendLine("·  IsoLightingController found and re-wired.");
            return;
        }

        // Load lighting profiles
        IsoLightingProfile[] profiles = LoadLightingProfiles();
        if (profiles == null || profiles.Length == 0)
        {
            // Force creation via IsoWorldSetup
            IsoWorldSetup.CreateOrUpdateLightingProfileAssets();
            profiles = LoadLightingProfiles();
        }

        GameObject lc = new GameObject("Iso Lighting Controller");
        IsoLightingController ctrl = lc.AddComponent<IsoLightingController>();
        ctrl.targetCamera     = cam;
        ctrl.directionalLight = dirLight;
        ctrl.profiles         = profiles;
        ctrl.profileIndex     = 0;

        log.AppendLine("✓  Created IsoLightingController.");
    }

    private static IsoLightingProfile[] LoadLightingProfiles()
    {
        var list = new System.Collections.Generic.List<IsoLightingProfile>();
        foreach (string path in new[]
        {
            LightingDayPath, LightingDuskPath, LightingNightPath, LightingStormPath
        })
        {
            var p = AssetDatabase.LoadAssetAtPath<IsoLightingProfile>(path);
            if (p != null) list.Add(p);
        }
        return list.ToArray();
    }

    // =========================================================================
    // 8. DayNightMusicManager
    // =========================================================================

    private static DayNightMusicManager SetupDayNightMusic(StringBuilder log)
    {
        DayNightMusicManager existing = Object.FindFirstObjectByType<DayNightMusicManager>();
        if (existing != null)
        {
            log.AppendLine("·  DayNightMusicManager found.");
            return existing;
        }

        GameObject musicObj          = new GameObject("Day Night Music");
        DayNightMusicManager music   = musicObj.AddComponent<DayNightMusicManager>();
        music.dayMusicClip           = AssetDatabase.LoadAssetAtPath<AudioClip>(DayMusicPath);
        music.nightMusicClip         = AssetDatabase.LoadAssetAtPath<AudioClip>(NightMusicPath);
        music.dayLengthMinutes       = 15f;
        music.nightLengthMinutes     = 15f;
        music.crossfadeDuration      = 30f;
        music.masterVolume           = 0.75f;

        if (music.dayMusicClip   == null) log.AppendLine($"  ⚠  Day music not found at {DayMusicPath}");
        if (music.nightMusicClip == null) log.AppendLine($"  ⚠  Night music not found at {NightMusicPath}");

        log.AppendLine("✓  Created DayNightMusicManager.");
        return music;
    }

    // =========================================================================
    // 8b. SunController (invisible orbital sun)
    // =========================================================================

    private static void SetupSunController(DayNightMusicManager musicMgr, Light dirLight, StringBuilder log)
    {
        // Find or create the SunController GameObject
        SunController existing = Object.FindFirstObjectByType<SunController>();
        if (existing != null)
        {
            // Rewire references if missing
            if (existing.cycleManager == null && musicMgr != null) existing.cycleManager = musicMgr;
            if (existing.directionalLight == null && dirLight != null) existing.directionalLight = dirLight;
            if (existing.lightingController == null)
                existing.lightingController = Object.FindFirstObjectByType<IsoLightingController>();
            EditorUtility.SetDirty(existing);
            log.AppendLine("·  SunController found and re-wired.");
            return;
        }

        GameObject sunObj = new GameObject("Sun");
        SunController sun = sunObj.AddComponent<SunController>();
        sun.cycleManager = musicMgr;
        sun.directionalLight = dirLight;
        sun.lightingController = Object.FindFirstObjectByType<IsoLightingController>();
        sun.orbitRadius = 50f;
        sun.orbitCenter = Vector3.zero;
        sun.orbitTiltDegrees = 60f;
        sun.orbitYawDegrees = 0f;
        sun.maxLightIntensity = 1.2f;
        sun.minLightIntensity = 0.15f;
        sun.lightBlendSpeed = 2.5f;
        sun.autoSelectLightingProfile = true;
        sun.dayProfileIndex = 0;
        sun.duskProfileIndex = 1;
        sun.nightProfileIndex = 2;

        log.AppendLine("✓  Created SunController (invisible orbital sun + dynamic lighting).");
    }

    // =========================================================================
    // 9. Gameplay layer
    // =========================================================================

    private static void SetupGameplayLayer(StringBuilder log)
    {
        // GameplayLayerSetup.SetupGameplayLayer() finds the player via
        // FindFirstObjectByType<IsoPlayerController> — player is already
        // present at this point so the call is safe.
        GameplayLayerSetup.SetupGameplayLayer();
        log.AppendLine("✓  Gameplay layer (inventory / hotbar / health bar) configured.");

        // Time + Zoom HUD overlay
        TimeAndZoomHUDSetup.SetupTimeAndZoomHUD();
        log.AppendLine("✓  Time display + Zoom HUD configured.");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Texture2D LoadTex(string path) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(path);
}
