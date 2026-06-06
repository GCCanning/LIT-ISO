using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// The single scene object. On Awake it builds the entire runtime graph (content,
    /// world, player, systems, UI) and wires it together, so the scene is trivially
    /// regenerable and there is exactly one world/player/inventory (no duplication).
    /// </summary>
    [DisallowMultipleComponent]
    public class FoundationBootstrap : MonoBehaviour
    {
        public const string DefaultWorldName = "Untitled World";

        public static event Action<FoundationBootstrap> Ready;

        static bool s_HasLaunchOptions;
        static LaunchOptions s_LaunchOptions;

        public FoundationConfig config = new();
        [Tooltip("Inventory slot count.")] public int inventorySlots = 36;
        [Tooltip("Hotbar slot count.")] public int hotbarSlots = 9;
        [Tooltip("Create the temporary IMGUI FoundationHUD. Disable when an external uGUI HUD binds to this bootstrap.")]
        public bool createImguiHud = true;
        public float cameraSize = 6f;

        public string ActiveWorldName { get; private set; } = DefaultWorldName;
        public int ActiveDifficulty { get; private set; } = 1;
        public FoundationContent Content { get; private set; }
        public IsoWorld World { get; private set; }
        public Inventory Inventory { get; private set; }
        public Hotbar Hotbar { get; private set; }
        public IsoFoundationPlayer Player { get; private set; }
        public IsoWorldController WorldController { get; private set; }
        public PlacementSystem Placement { get; private set; }
        public FarmingSystem Farming { get; private set; }
        public MobSpawner MobSpawner { get; private set; }
        public DayNightSystem DayNight { get; private set; }
        public CraftingSystem Crafting { get; private set; }
        public FoundationProgression Progression { get; private set; }
        public FoundationPlayerStats Stats => Progression?.Stats;
        public FoundationHUD Hud { get; private set; }

        Camera _cam;
        Transform _playerT;

        /// <summary>
        /// Call before loading IsoCoreFoundation.unity to hand menu/world settings into
        /// the isolated Foundation scene without coupling it to the legacy WorldManager.
        /// </summary>
        public static void ConfigureLaunch(string worldName, string seed, int difficulty = 1)
        {
            s_LaunchOptions = new LaunchOptions(
                NormalizeWorldName(worldName),
                SeedStringToInt(seed),
                Mathf.Clamp(difficulty, 0, 2));
            s_HasLaunchOptions = true;
        }

        /// <summary>Clears any pending menu handoff; useful for editor tests and scene rebuilds.</summary>
        public static void ClearLaunchOptions()
        {
            s_HasLaunchOptions = false;
            s_LaunchOptions = default;
        }

        public static int SeedStringToInt(string seed)
        {
            if (string.IsNullOrWhiteSpace(seed))
                return 1337;

            seed = seed.Trim();
            if (int.TryParse(seed, out int parsed))
                return parsed;

            unchecked
            {
                const uint fnvOffset = 2166136261u;
                const uint fnvPrime = 16777619u;
                uint hash = fnvOffset;
                for (int i = 0; i < seed.Length; i++)
                    hash = (hash ^ seed[i]) * fnvPrime;
                return (int)hash;
            }
        }

        void Awake()
        {
            ApplyLaunchOptions();

            Content = FoundationContent.BuildDefault();
            Progression = new FoundationProgression(Content);
            var sampler = new IsoTerrainSampler(config, Content);
            World = new IsoWorld(sampler, Content, config.chunkSize);

            // Player.
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            Player = playerGo.AddComponent<IsoFoundationPlayer>();
            Player.Init(World, config);
            // Render the knight sheet over the placeholder box (added after Init so it owns
            // the SpriteRenderer): directional facing + walk animation.
            playerGo.AddComponent<PlayerAnimator>();
            _playerT = playerGo.transform;

            // World streaming controller.
            var controllerGo = new GameObject("WorldController");
            controllerGo.transform.SetParent(transform, false);
            WorldController = controllerGo.AddComponent<IsoWorldController>();
            WorldController.Init(World, Content, config, _playerT);

            // Inventory + hotbar + starter items.
            Inventory = new Inventory(inventorySlots, Content);
            Hotbar = new Hotbar(Inventory, hotbarSlots);
            if (config.starterItems != null)
                foreach (var s in config.starterItems) Inventory.Add(s.itemId, s.count);

            // Placement.
            var placementGo = new GameObject("PlacementSystem");
            placementGo.transform.SetParent(transform, false);
            Placement = placementGo.AddComponent<PlacementSystem>();

            // Camera (before placement init — it needs the camera).
            SetupCamera();
            Placement.Init(World, Content, Inventory, Hotbar, _cam, Player);

            // Crafting (station proximity via placement).
            Crafting = new CraftingSystem(Content, Inventory);
            Crafting.StationAvailable = st =>
                Placement.IsStationInRange(_playerT.position, config.interactRange * 1.5f, st);

            // Farming.
            var farmingGo = new GameObject("FarmingSystem");
            farmingGo.transform.SetParent(transform, false);
            Farming = farmingGo.AddComponent<FarmingSystem>();
            Farming.Init(World, Content, Inventory, Hotbar, _cam);

            // Mob spawner.
            var spawnerGo = new GameObject("MobSpawner");
            spawnerGo.transform.SetParent(transform, false);
            MobSpawner = spawnerGo.AddComponent<MobSpawner>();
            MobSpawner.Init(World, Content, config, Player);

            // Day/night clock.
            DayNight = gameObject.AddComponent<DayNightSystem>();

            // World-wide day/night tint (ground + props + player) via the global ambient.
            var ambient = gameObject.AddComponent<AmbientLightController>();
            ambient.dayNight = DayNight;

            // Atmospheric motes: pollen by day, fireflies by night, following the camera.
            var particlesGo = new GameObject("AmbientParticles", typeof(ParticleSystem));
            particlesGo.transform.SetParent(transform, false);
            var particles = particlesGo.AddComponent<AmbientParticles>();
            particles.dayNight = DayNight;
            particles.cam = _cam;

            // Audio: ensure a listener, prime the SFX pool, and start the day/night music bed.
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null && _cam != null)
                _cam.gameObject.AddComponent<AudioListener>();
            SfxManager.Ensure();
            var worldAudio = gameObject.AddComponent<WorldAudioController>();
            worldAudio.dayNight = DayNight;

            // Pause / settings overlay (Esc) with volume sliders + control hints.
            gameObject.AddComponent<PauseMenu>();

            // HUD.
            if (createImguiHud)
            {
                Hud = gameObject.AddComponent<FoundationHUD>();
                Hud.Init(Inventory, Hotbar, Content, Crafting, Player, World, DayNight);
            }

            // Input router.
            var interaction = gameObject.AddComponent<PlayerInteraction>();
            interaction.Init(Player, WorldController, Content, config, Inventory, Hotbar, Placement, Farming, Hud);

            Ready?.Invoke(this);

            Debug.Log($"[FoundationBootstrap] Ready. Blocks:{Content.Blocks.Count} Items:{Content.Items.Count} " +
                      $"Placeables:{Content.Placeables.Count} Recipes:{Content.Recipes.Count} " +
                      $"Nodes:{Content.Nodes.Count} Mobs:{Content.Mobs.Count} Biomes:{Content.Biomes.Count} " +
                      $"Callings:{Content.Callings.Count} Skills:{Content.Skills.Count} Quests:{Content.Quests.Count} " +
                      $"World:'{ActiveWorldName}' Seed:{config.seed} Difficulty:{ActiveDifficulty}");
        }

        void ApplyLaunchOptions()
        {
            if (config == null)
                config = new FoundationConfig();

            ActiveWorldName = DefaultWorldName;
            ActiveDifficulty = 1;

            if (!s_HasLaunchOptions)
                return;

            ActiveWorldName = s_LaunchOptions.worldName;
            ActiveDifficulty = s_LaunchOptions.difficulty;
            config.seed = s_LaunchOptions.seed;
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                _cam = camGo.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = cameraSize;
            _cam.clearFlags = CameraClearFlags.SolidColor; // else a runtime-made camera clears to skybox
            _cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            _cam.transform.position = new Vector3(0, 0, -10);

            if (config != null && config.pixelPerfect)
                SetupPixelPerfect();
        }

        // Pixel Perfect Camera (Built-in standalone, com.unity.2d.pixel-perfect). Keeps
        // the 32px ground tiles crisp and free of shimmer as the camera follows the
        // player. assetsPPU matches the tile art (32). Reference resolution 640x360
        // (16:9) gives a view close to the previous ortho size while snapping render to
        // the pixel grid. stretchFill fills the window instead of hard black bars.
        void SetupPixelPerfect()
        {
            const int assetsPPU = 32;
            const int refX = 640, refY = 360;

            var pp = _cam.GetComponent<UnityEngine.U2D.PixelPerfectCamera>()
                  ?? _cam.gameObject.AddComponent<UnityEngine.U2D.PixelPerfectCamera>();

            _cam.orthographicSize = (refY / 2f) / assetsPPU; // 5.625
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.allowDynamicResolution = false;
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            pp.assetsPPU = assetsPPU;
            pp.refResolutionX = refX;
            pp.refResolutionY = refY;
            pp.pixelSnapping = true;   // snap sprites to the pixel grid at render time
            pp.upscaleRT = false;      // keep post/UI compatible
            pp.cropFrameX = true;
            pp.cropFrameY = true;
            pp.stretchFill = true;     // fill the window (both crop flags + stretchFill)
        }

        Vector3 _camVel;
        [SerializeField] float cameraFollowSmoothTime = 0.15f;

        void LateUpdate()
        {
            if (_cam != null && _playerT != null)
            {
                var p = _playerT.position;
                var target = new Vector3(p.x, p.y, -10f);
                // Damped follow — feels smoother than a hard snap. The Pixel Perfect Camera
                // still snaps the rendered image to the pixel grid, so this stays crisp.
                _cam.transform.position = Vector3.SmoothDamp(
                    _cam.transform.position, target, ref _camVel, cameraFollowSmoothTime);
            }
        }

        struct LaunchOptions
        {
            public readonly string worldName;
            public readonly int seed;
            public readonly int difficulty;

            public LaunchOptions(string worldName, int seed, int difficulty)
            {
                this.worldName = worldName;
                this.seed = seed;
                this.difficulty = difficulty;
            }
        }

        static string NormalizeWorldName(string worldName)
        {
            return string.IsNullOrWhiteSpace(worldName) ? DefaultWorldName : worldName.Trim();
        }
    }
}
