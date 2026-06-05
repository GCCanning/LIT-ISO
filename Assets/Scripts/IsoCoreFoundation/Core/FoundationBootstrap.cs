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
        public FoundationConfig config = new();
        [Tooltip("Inventory slot count.")] public int inventorySlots = 36;
        [Tooltip("Hotbar slot count.")] public int hotbarSlots = 9;
        public float cameraSize = 6f;

        Camera _cam;
        Transform _playerT;

        void Awake()
        {
            var content = FoundationContent.BuildDefault();
            var sampler = new IsoTerrainSampler(config, content);
            var world = new IsoWorld(sampler, content, config.chunkSize);

            // Player.
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            var player = playerGo.AddComponent<IsoFoundationPlayer>();
            player.Init(world, config);
            _playerT = playerGo.transform;

            // World streaming controller.
            var controllerGo = new GameObject("WorldController");
            controllerGo.transform.SetParent(transform, false);
            var controller = controllerGo.AddComponent<IsoWorldController>();
            controller.Init(world, content, config, _playerT);

            // Inventory + hotbar + starter items.
            var inventory = new Inventory(inventorySlots, content);
            var hotbar = new Hotbar(inventory, hotbarSlots);
            if (config.starterItems != null)
                foreach (var s in config.starterItems) inventory.Add(s.itemId, s.count);

            // Placement.
            var placementGo = new GameObject("PlacementSystem");
            placementGo.transform.SetParent(transform, false);
            var placement = placementGo.AddComponent<PlacementSystem>();

            // Camera (before placement init — it needs the camera).
            SetupCamera();
            placement.Init(world, content, inventory, hotbar, _cam, player);

            // Crafting (station proximity via placement).
            var crafting = new CraftingSystem(content, inventory);
            crafting.StationAvailable = st =>
                placement.IsStationInRange(_playerT.position, config.interactRange * 1.5f, st);

            // Farming.
            var farmingGo = new GameObject("FarmingSystem");
            farmingGo.transform.SetParent(transform, false);
            var farming = farmingGo.AddComponent<FarmingSystem>();
            farming.Init(world, content, inventory, hotbar, _cam);

            // Mob spawner.
            var spawnerGo = new GameObject("MobSpawner");
            spawnerGo.transform.SetParent(transform, false);
            var spawner = spawnerGo.AddComponent<MobSpawner>();
            spawner.Init(world, content, config, player);

            // Day/night clock.
            var dayNight = gameObject.AddComponent<DayNightSystem>();

            // HUD.
            var hud = gameObject.AddComponent<FoundationHUD>();
            hud.Init(inventory, hotbar, content, crafting, player, world, dayNight);

            // Input router.
            var interaction = gameObject.AddComponent<PlayerInteraction>();
            interaction.Init(player, controller, content, config, inventory, hotbar, placement, farming, hud);

            Debug.Log($"[FoundationBootstrap] Ready. Blocks:{content.Blocks.Count} Items:{content.Items.Count} " +
                      $"Placeables:{content.Placeables.Count} Recipes:{content.Recipes.Count} " +
                      $"Nodes:{content.Nodes.Count} Mobs:{content.Mobs.Count} Biomes:{content.Biomes.Count}");
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
    }
}
