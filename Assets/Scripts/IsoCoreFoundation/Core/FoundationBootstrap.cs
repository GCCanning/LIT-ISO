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
            var sampler = new IsoTerrainSampler(config, Content);
            World = new IsoWorld(sampler, Content, config.chunkSize);

            // Player.
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            Player = playerGo.AddComponent<IsoFoundationPlayer>();
            Player.Init(World, config);
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
        }

        void LateUpdate()
        {
            if (_cam != null && _playerT != null)
            {
                var p = _playerT.position;
                _cam.transform.position = new Vector3(p.x, p.y, -10f);
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
