using System;
using System.IO;
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

        /// <summary>
        /// Raised when gameplay wants the DontDestroyOnLoad HUD shell torn down before
        /// leaving gameplay (e.g. PauseMenu's Quit-to-Menu). IsoCore.Foundation cannot
        /// reference the LitIso.UI.InGame HUD assembly directly (asmdef ordering), so
        /// PauseMenu raises this event via <see cref="RequestHudShutdown"/> and
        /// GameHudInitializer subscribes to it to call ShutdownHud().
        /// </summary>
        public static event Action HudShutdownRequested;

        /// <summary>Requests that any listening HUD shell tear itself down. See <see cref="HudShutdownRequested"/>.</summary>
        public static void RequestHudShutdown() => HudShutdownRequested?.Invoke();

        static bool s_HasLaunchOptions;
        static LaunchOptions s_LaunchOptions;

        public FoundationConfig config = new();
        [Tooltip("Inventory slot count.")] public int inventorySlots = 36;
        [Tooltip("Hotbar slot count.")] public int hotbarSlots = 9;
        [Tooltip("Slot count for placed storage containers such as chests.")] public int storageSlots = 18;
        // Retired serialized field kept so older scenes deserialize without churn.
        [HideInInspector] public bool createImguiHud = false;
        public float cameraSize = 6f;
        public float cameraMinSize = 6f;    // ISO-CORE zoom-in limit
        public float cameraMaxSize = 30f;   // ISO-CORE zoom-out limit
        public float cameraZoomUnitsPerSecond = 5f;
        public float cameraZoomTapStep = 0.75f;
        public float cameraScrollStep = 1f;   // ISO-CORE: 1.0 per scroll tick
        public const string CameraZoomSensitivityPrefKey = "camera.zoom.sensitivity";

        public string ActiveWorldName { get; private set; } = DefaultWorldName;
        public int ActiveDifficulty { get; private set; } = 1;
        public string ActiveCallingId { get; private set; } = "greenhand";
        public FoundationCharacterAppearanceSaveData ActiveCharacterAppearance { get; private set; }
        public FoundationLaunchMode ActiveLaunchMode { get; private set; } = FoundationLaunchMode.Standard;
        public FoundationContent Content { get; private set; }
        public IsoWorld World { get; private set; }
        public Inventory Inventory { get; private set; }
        public Hotbar Hotbar { get; private set; }
        public StorageSystem Storage { get; private set; }
        public EquipmentLoadout Equipment { get; private set; }
        public LootSystem Loot { get; private set; }
        public IsoFoundationPlayer Player { get; private set; }
        public IsoWorldController WorldController { get; private set; }
        public PlacementSystem Placement { get; private set; }
        public FoundationInstanceSystem Instances { get; private set; }
        public FarmingSystem Farming { get; private set; }
        public MobSpawner MobSpawner { get; private set; }
        public DayNightSystem DayNight { get; private set; }
        public FoundationCampingSystem Camping { get; private set; }
        public FoundationDeathSystem Death { get; private set; }
        public CraftingSystem Crafting { get; private set; }
        public FoundationProgression Progression { get; private set; }
        public FoundationAbilitySystem Abilities { get; private set; }
        public FoundationAbilityDispatcher AbilityDispatcher { get; private set; }
        public FoundationProgressionHooks ProgressionHooks { get; private set; }
        public FoundationQoLService QoL { get; private set; }
        public FoundationUiCoordinator Ui { get; private set; }
        public FoundationInteractionOverlay InteractionOverlay { get; private set; }
        public FoundationDungeonPortalSystem DungeonPortals { get; private set; }
        public FoundationTutorialNotifier TutorialNotifier { get; private set; }
        public FoundationMapOverlay MapOverlay { get; private set; }
        public FoundationPlayerStats Stats => Progression?.Stats;
        // FoundationHUD (retired IMGUI fallback) removed 2026-06. uGUI is the canonical runtime UI.
        public PlayerInteraction Interaction { get; private set; }
        public string DefaultSavePath => DefaultSavePathForWorld(ActiveWorldName, config != null ? config.seed : 1337);

        Camera _cam;
        UnityEngine.U2D.PixelPerfectCamera _pixelPerfectCamera;
        Transform _playerT;

        /// <summary>
        /// Call before loading IsoCoreFoundation.unity to hand menu/world settings into
        /// the isolated Foundation scene without coupling it to the legacy WorldManager.
        /// </summary>
        public static void ConfigureLaunch(string worldName, string seed, int difficulty = 1, string callingId = null,
            FoundationCharacterAppearanceSaveData appearance = null)
        {
            s_LaunchOptions = new LaunchOptions(
                NormalizeWorldName(worldName),
                SeedStringToInt(seed),
                Mathf.Clamp(difficulty, 0, 2),
                NormalizeCallingId(callingId),
                null,
                FoundationLaunchMode.Standard,
                FoundationCharacterAppearanceCatalog.ResolveSaveData(appearance));
            s_HasLaunchOptions = true;
        }

        /// <summary>
        /// Boots a flat, non-save test world for reviewing portals, buildings, resources,
        /// interiors, and placed systems without mutating a real player world.
        /// </summary>
        public static void ConfigureCreationInstanceLaunch(string callingId = null)
        {
            s_LaunchOptions = new LaunchOptions(
                FoundationCreationInstanceShowroom.WorldName,
                FoundationCreationInstanceShowroom.Seed,
                0,
                NormalizeCallingId(callingId),
                null,
                FoundationLaunchMode.CreationInstance,
                FoundationCharacterAppearanceCatalog.Default.ToSaveData());
            s_HasLaunchOptions = true;
        }

        /// <summary>
        /// Call before loading IsoCoreFoundation.unity to boot directly from a save.
        /// The save metadata seeds the world before the runtime graph is built, then the
        /// full save is applied before Ready fires.
        /// </summary>
        public static void ConfigureLoad(string savePath)
        {
            if (TryReadSaveMetadata(savePath, out var metadata, out string error))
            {
                s_LaunchOptions = new LaunchOptions(
                    NormalizeWorldName(metadata.worldName),
                    metadata.seed,
                    Mathf.Clamp(metadata.difficulty, 0, 2),
                    NormalizeCallingId(metadata.callingId),
                    savePath,
                    FoundationLaunchMode.Standard,
                    null);
            }
            else
            {
                Debug.LogWarning($"[FoundationBootstrap] ConfigureLoad could not read metadata ({error}). Booting default world, then attempting load.");
                s_LaunchOptions = new LaunchOptions(DefaultWorldName, 1337, 1, null, savePath,
                    FoundationLaunchMode.Standard,
                    null);
            }
            s_HasLaunchOptions = true;
        }

        /// <summary>Explicit load handoff when the menu already knows the slot metadata.</summary>
        public static void ConfigureLoad(string worldName, string seed, int difficulty, string savePath, string callingId = null,
            FoundationCharacterAppearanceSaveData appearance = null)
        {
            s_LaunchOptions = new LaunchOptions(
                NormalizeWorldName(worldName),
                SeedStringToInt(seed),
                Mathf.Clamp(difficulty, 0, 2),
                NormalizeCallingId(callingId),
                savePath,
                FoundationLaunchMode.Standard,
                FoundationCharacterAppearanceCatalog.ResolveSaveData(appearance));
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
            ApplyLaunchCalling();
            Abilities = new FoundationAbilitySystem();
            Abilities.Init(Content, Progression, Progression.Stats);
            var sampler = new IsoTerrainSampler(config, Content);
            World = new IsoWorld(sampler, Content, config.chunkSize);
            if (ActiveLaunchMode == FoundationLaunchMode.CreationInstance)
                FoundationCreationInstanceShowroom.PrepareWorld(World, Content);

            // Player.
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            Player = playerGo.AddComponent<IsoFoundationPlayer>();
            // Stats wired in so sprint meters against the LitRPG Stamina pool (HUD-bound).
            Player.Init(World, config, Progression?.Stats);
            // Render the knight sheet over the placeholder box (added after Init so it owns
            // the SpriteRenderer): directional facing + walk animation.
            var animator = playerGo.AddComponent<PlayerAnimator>();
            animator.SetAppearance(ActiveCharacterAppearance);
            // NOTE: the LPC layered wardrobe (LitIso.CharacterCreator, Assembly-CSharp)
            // can't be referenced from this assembly (IsoCore.Foundation). It hooks
            // itself onto the player via the Ready event instead — see
            // LayeredCharacterPlayerHook.cs.
            FoundationDepthPolish.Attach(playerGo, fadeWhenOccluding: false, castLongShadow: false,
                contactScale: 0.72f, contactAlpha: 0.32f);
            _playerT = playerGo.transform;

            // World streaming controller.
            var controllerGo = new GameObject("WorldController");
            controllerGo.transform.SetParent(transform, false);
            WorldController = controllerGo.AddComponent<IsoWorldController>();
            WorldController.Init(World, Content, config, _playerT);

            // Inventory + hotbar + starter items.
            Inventory = new Inventory(inventorySlots, Content);
            Hotbar = new Hotbar(Inventory, hotbarSlots);
            Storage = new StorageSystem(Content, storageSlots);
            // Phase 1 equipment: per-slot loadout that feeds StatBonuses into player stats and
            // raises EquipmentChanged for the (Assembly-CSharp) LPC visual hook to re-bake gear.
            Equipment = new EquipmentLoadout(Content, Progression?.Stats);
            if (config.starterItems != null)
                foreach (var s in config.starterItems) Inventory.Add(s.itemId, s.count);
            var heldTool = playerGo.AddComponent<PlayerHeldTool>();
            heldTool.Init(Player, Inventory, Hotbar, Content);

            // Placement.
            var placementGo = new GameObject("PlacementSystem");
            placementGo.transform.SetParent(transform, false);
            Placement = placementGo.AddComponent<PlacementSystem>();

            // Camera (before placement init — it needs the camera).
            SetupCamera();
            Placement.Init(World, Content, Inventory, Hotbar, _cam, Player, Storage);

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
            MobSpawner.Init(World, Content, config, Player, Progression?.Stats);

            // Phase 2 loot: rolls mob drops into the inventory on defeat, and fills loot-table
            // chests the first time they're opened. Subscribes to MobSpawner + Storage events.
            Loot = new LootSystem(Content, Inventory, Storage);
            Loot.Init(MobSpawner);

            // Ability executor: turns a successful TryUseAbility into a real in-world
            // effect + VFX (blink / projectile / heal). The ability wheel VM casts through
            // Abilities.TryUseAbility, then calls AbilityDispatcher.Execute.
            AbilityDispatcher = new FoundationAbilityDispatcher();
            AbilityDispatcher.Init(Player, MobSpawner, Content, Progression?.Stats);

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

            // Void ash motes: fade in only while the player is near "void" cells
            // (dungeon space outside the room/corridor + wall-ring layout).
            var voidParticlesGo = new GameObject("VoidAmbientParticles", typeof(ParticleSystem));
            voidParticlesGo.transform.SetParent(transform, false);
            var voidParticles = voidParticlesGo.AddComponent<VoidAmbientParticles>();
            voidParticles.world = World;
            voidParticles.cam = _cam;
            voidParticles.follow = _playerT;

            // Audio: ensure a listener, prime the SFX pool, and start the day/night music bed.
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null && _cam != null)
                _cam.gameObject.AddComponent<AudioListener>();
            SfxManager.Ensure();
            var worldAudio = gameObject.AddComponent<WorldAudioController>();
            worldAudio.dayNight = DayNight;
            worldAudio.SetBiomeSource(World, Player, config.seed);

            // Pause / settings overlay (Esc) with volume sliders + control hints.
            Ui = gameObject.AddComponent<FoundationUiCoordinator>();
            // PauseMenu lives in Assembly-CSharp (needs LitIsoTheme); add via reflection.
            var pauseMenuType = System.Type.GetType("IsoCore.Foundation.PauseMenu, Assembly-CSharp");
            if (pauseMenuType != null) gameObject.AddComponent(pauseMenuType);

            // Lightweight Foundation overlay for right-click world options and tutorial
            // notifications. uGUI is the canonical HUD/panel shell.
            InteractionOverlay = gameObject.AddComponent<FoundationInteractionOverlay>();

            Camping = gameObject.AddComponent<FoundationCampingSystem>();
            Camping.Init(Player, Placement, DayNight, Progression, InteractionOverlay, config);

            Instances = new FoundationInstanceSystem();
            Instances.Init(World, Player, Content, InteractionOverlay);
            WorldController.SetInstanceSystem(Instances);
            MobSpawner.SetInstanceSystem(Instances);
            MobSpawner.SetCampingSystem(Camping);

            // Settlement townsfolk: passive NPCs that populate town streets while the player is
            // in a settlement and clear out when they leave. Reuses MobSpawner for spawning.
            var townsfolkGo = new GameObject("SettlementTownsfolkSpawner");
            townsfolkGo.transform.SetParent(transform, false);
            townsfolkGo.AddComponent<SettlementTownsfolkSpawner>()
                .Init(World, Player, MobSpawner, Content, Instances);

            var portalGo = new GameObject("DungeonPortalSystem");
            portalGo.transform.SetParent(transform, false);
            DungeonPortals = portalGo.AddComponent<FoundationDungeonPortalSystem>();
            DungeonPortals.Init(World, Content, config, Player, Instances, MobSpawner, InteractionOverlay,
                Progression, ActiveLaunchMode);

            var weatherGo = new GameObject("FoundationWeatherVisuals", typeof(ParticleSystem));
            weatherGo.transform.SetParent(transform, false);
            var weather = weatherGo.AddComponent<FoundationWeatherVisuals>();
            weather.Init(DayNight, _cam, World, Player, Instances, config.seed);

            // The old IMGUI FoundationHUD backup is intentionally not created.
            // uGUI is the canonical runtime UI; GameHudInitializer spawns it when Ready fires.

            // Input router.
            Interaction = gameObject.AddComponent<PlayerInteraction>();
            Interaction.Init(Player, WorldController, Content, config, Inventory, Hotbar, Placement, Farming,
                Storage, _cam, InteractionOverlay, Instances, DungeonPortals, heldTool, Camping,
                Progression?.Stats, MobSpawner);

            // Death + soft respawn (audit rec #3): 0 HP fades out and wakes the player
            // at their campfire (or the spawn clearing) with half Health. No item loss.
            Death = gameObject.AddComponent<FoundationDeathSystem>();
            Death.Init(Player, World, Progression, Placement, Instances, DungeonPortals, InteractionOverlay);

            // LitRPG progression hooks. Gameplay systems emit success events; this component
            // converts them into activity XP and starter quest progress.
            ProgressionHooks = gameObject.AddComponent<FoundationProgressionHooks>();
            ProgressionHooks.Init(Progression, Interaction, Crafting, Placement, Farming, MobSpawner);

            TutorialNotifier = gameObject.AddComponent<FoundationTutorialNotifier>();
            TutorialNotifier.Init(InteractionOverlay, Progression, Hotbar, Interaction, Crafting, Placement, Farming);

            MapOverlay = gameObject.AddComponent<FoundationMapOverlay>();
            MapOverlay.Init(World, Player, Instances, DungeonPortals);

            QoL = new FoundationQoLService();
            QoL.Init(Content, Progression, Inventory);

            if (ActiveLaunchMode == FoundationLaunchMode.CreationInstance)
                FoundationCreationInstanceShowroom.BuildShowroom(this);

            ApplyLaunchSave();

            Ready?.Invoke(this);

            Debug.Log($"[FoundationBootstrap] Ready. Blocks:{Content.Blocks.Count} Items:{Content.Items.Count} " +
                      $"Placeables:{Content.Placeables.Count} Recipes:{Content.Recipes.Count} " +
                      $"Nodes:{Content.Nodes.Count} Mobs:{Content.Mobs.Count} Biomes:{Content.Biomes.Count} " +
                      $"Callings:{Content.Callings.Count} Skills:{Content.Skills.Count} Abilities:{Content.Abilities.Count} Quests:{Content.Quests.Count} " +
                      $"World:'{ActiveWorldName}' Seed:{config.seed} Difficulty:{ActiveDifficulty} Calling:{ActiveCallingId}");
        }

        void ApplyLaunchOptions()
        {
            if (config == null)
                config = new FoundationConfig();
            config.viewRadiusChunks = Mathf.Max(3, config.viewRadiusChunks);
            config.moveSpeed = Mathf.Clamp(config.moveSpeed <= 0f ? 2.8f : config.moveSpeed, 1.5f, 2.8f);

            // The standard play/test world uses the continent generator (oceans, beaches,
            // biome regions, multi-step cliffs, rivers). The CreationInstance showroom
            // overrides this back to a flat review grid in its own ApplyConfig below.
            config.flatWorld = false;

            ActiveWorldName = DefaultWorldName;
            ActiveDifficulty = 1;
            ActiveCallingId = "greenhand";
            ActiveCharacterAppearance = FoundationCharacterAppearanceCatalog.Default.ToSaveData();
            ActiveLaunchMode = FoundationLaunchMode.Standard;

            if (!s_HasLaunchOptions)
                return;

            ActiveWorldName = s_LaunchOptions.worldName;
            ActiveDifficulty = s_LaunchOptions.difficulty;
            ActiveLaunchMode = s_LaunchOptions.launchMode;
            ActiveCharacterAppearance = FoundationCharacterAppearanceCatalog.ResolveSaveData(s_LaunchOptions.appearance);
            config.seed = s_LaunchOptions.seed;
            if (ActiveLaunchMode == FoundationLaunchMode.CreationInstance)
                FoundationCreationInstanceShowroom.ApplyConfig(this);
        }

        void ApplyLaunchCalling()
        {
            if (Progression == null)
                return;

            string requested = s_HasLaunchOptions ? s_LaunchOptions.callingId : null;
            if (!string.IsNullOrWhiteSpace(requested) && !Progression.SelectCalling(requested))
                Debug.LogWarning($"[FoundationBootstrap] Unknown launch Calling '{requested}', keeping {Progression.CurrentCalling?.id ?? "default"}.");

            ActiveCallingId = Progression.CurrentCalling?.id ?? "greenhand";
        }

        void ApplyLaunchSave()
        {
            if (!s_HasLaunchOptions || string.IsNullOrWhiteSpace(s_LaunchOptions.savePath))
                return;

            if (!Load(s_LaunchOptions.savePath))
                Debug.LogWarning($"[FoundationBootstrap] Launch save load failed: {s_LaunchOptions.savePath}");
        }

        public bool Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                path = DefaultSavePath;

            try
            {
                var data = CaptureSaveData();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                AtomicWriteAllText(path, JsonUtility.ToJson(data, true));
                Debug.Log($"[FoundationBootstrap] Saved Foundation world to {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationBootstrap] Save failed: {ex.Message}");
                return false;
            }
        }

        public bool Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                path = DefaultSavePath;

            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[FoundationBootstrap] Save file not found: {path}");
                    return false;
                }

                var data = JsonUtility.FromJson<FoundationSaveData>(File.ReadAllText(path));
                if (data == null || data.version <= 0 || data.version > FoundationSaveData.CurrentVersion)
                {
                    Debug.LogWarning($"[FoundationBootstrap] Save file is invalid or unsupported: {path}");
                    return false;
                }

                if (config != null && data.seed != 0 && data.seed != config.seed)
                {
                    Debug.LogWarning($"[FoundationBootstrap] Refusing to load save seed {data.seed} into active world seed {config.seed}. Call ConfigureLaunch with the save seed before loading the scene.");
                    return false;
                }

                ApplySaveData(data);
                Debug.Log($"[FoundationBootstrap] Loaded Foundation world from {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationBootstrap] Load failed: {ex.Message}");
                return false;
            }
        }

        FoundationSaveData CaptureSaveData()
        {
            var cell = Player != null ? Player.CurrentCell : Vector2Int.zero;
            var ground = Player != null ? Player.Ground : Vector2.zero;
            return new FoundationSaveData
            {
                version = FoundationSaveData.CurrentVersion,
                savedUtc = DateTime.UtcNow.ToString("o"),
                worldName = ActiveWorldName,
                seed = config != null ? config.seed : 1337,
                difficulty = ActiveDifficulty,
                callingId = ActiveCallingId,
                characterAppearance = ActiveCharacterAppearance != null
                    ? ActiveCharacterAppearance.Clone()
                    : FoundationCharacterAppearanceCatalog.Default.ToSaveData(),
                player = new FoundationSavedPlayer
                {
                    cellX = cell.x,
                    cellY = cell.y,
                    groundX = ground.x,
                    groundY = ground.y,
                },
                inventorySlots = Inventory != null ? Inventory.SnapshotSlots() : Array.Empty<ItemStack>(),
                equipment = Equipment != null ? Equipment.CaptureState() : null,
                hotbarSelected = Hotbar != null ? Hotbar.Selected : 0,
                progression = Progression != null ? Progression.CaptureState() : null,
                qol = QoL != null ? QoL.CaptureState() : null,
                modifiedCells = World != null ? World.SnapshotModifiedCells() : Array.Empty<FoundationSavedCell>(),
                placedObjects = Placement != null ? Placement.SnapshotPlaceables() : Array.Empty<FoundationSavedPlaceable>(),
                storageContainers = Storage != null ? Storage.CaptureState() : Array.Empty<FoundationSavedStorageContainer>(),
                crops = Farming != null ? Farming.SnapshotCrops() : Array.Empty<FoundationSavedCrop>(),
                instance = Instances != null ? Instances.CaptureState() : default,
                dungeon = DungeonPortals != null ? DungeonPortals.CaptureState() : default,
                dungeonHistory = DungeonPortals != null ? DungeonPortals.CaptureHistory() : Array.Empty<FoundationSavedDungeonHistory>(),
                exploredMapCells = MapOverlay != null ? MapOverlay.SnapshotExploredCells() : Array.Empty<FoundationSavedMapCell>(),
                dayNightTime = DayNight != null ? DayNight.time : 0.30f,
                mobs = MobSpawner != null ? MobSpawner.SnapshotMobs() : Array.Empty<FoundationSavedMob>(),
                regionShifts = Progression != null ? ToArray(Progression.RegionShifts) : Array.Empty<string>(),
            };
        }

        void ApplySaveData(FoundationSaveData data)
        {
            if (data == null) return;

            ActiveWorldName = NormalizeWorldName(data.worldName);
            ActiveDifficulty = Mathf.Clamp(data.difficulty, 0, 2);
            ActiveCharacterAppearance = FoundationCharacterAppearanceCatalog.ResolveSaveData(data.characterAppearance);

            if (Progression != null && data.progression != null)
                Progression.RestoreState(data.progression);
            ActiveCallingId = Progression?.CurrentCallingId ?? (string.IsNullOrWhiteSpace(data.callingId) ? "greenhand" : data.callingId);

            Inventory?.RestoreSlots(data.inventorySlots);
            // Equipment restores AFTER progression/stats (RestoreState zeroes equip offsets, then
            // the loadout re-applies the equipped bonuses and re-raises EquipmentChanged so the
            // visual layer re-bakes the restored gear).
            Equipment?.RestoreState(data.equipment);
            if (Hotbar != null) Hotbar.Select(data.hotbarSelected);
            QoL?.RestoreState(data.qol);

            World?.ResetModifiedCells();
            World?.RestoreModifiedCells(data.modifiedCells);
            Placement?.RestorePlaceables(data.placedObjects);
            Storage?.RestoreState(data.storageContainers, entry =>
                Placement != null && Placement.HasContainerPlaceable(entry.x, entry.y, entry.placeableId));
            // Phase 2: a saved container has already had its loot rolled — mark it filled so
            // first-open after load doesn't re-roll into the player's stored contents.
            if (Loot != null && data.storageContainers != null)
                foreach (var sc in data.storageContainers)
                    Loot.MarkContainerFilled(sc.x, sc.y);
            Farming?.RestoreCrops(data.crops);
            DungeonPortals?.RestoreHistory(data.dungeonHistory);
            bool restoringDungeon = data.dungeon.active && !string.IsNullOrWhiteSpace(data.dungeon.portalId);
            if (restoringDungeon)
                DungeonPortals?.RestoreState(data.dungeon, data.instance);
            else
                Instances?.RestoreState(data.instance);
            MapOverlay?.RestoreExploredCells(data.exploredMapCells);
            DayNight?.SetTime(data.dayNightTime);
            MobSpawner?.RestoreMobs(data.mobs);

            if (Player != null)
                Player.SetGround(new Vector2(data.player.groundX, data.player.groundY));

            var animator = Player != null ? Player.GetComponent<PlayerAnimator>() : null;
            animator?.SetAppearance(ActiveCharacterAppearance);
        }

        public static string DefaultSavePathForWorld(string worldName)
        {
            return Path.Combine(Application.persistentDataPath, SanitizePathPart(NormalizeWorldName(worldName)), "save.json");
        }

        public static string DefaultSavePathForWorld(string worldName, string seed)
        {
            return DefaultSavePathForWorld(worldName, SeedStringToInt(seed));
        }

        public static string DefaultSavePathForWorld(string worldName, int seed)
        {
            string folder = SanitizePathPart($"{NormalizeWorldName(worldName)}_{seed}");
            return Path.Combine(Application.persistentDataPath, folder, "save.json");
        }

        public static bool TryReadSaveMetadata(string path, out FoundationSaveMetadata metadata)
        {
            return TryReadSaveMetadata(path, out metadata, out _);
        }

        public static bool TryReadSaveMetadata(string path, out FoundationSaveMetadata metadata, out string error)
        {
            metadata = null;
            error = "";

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Save path is empty.";
                return false;
            }

            try
            {
                if (!File.Exists(path))
                {
                    error = $"Save file not found: {path}";
                    return false;
                }

                var data = JsonUtility.FromJson<FoundationSaveData>(File.ReadAllText(path));
                if (data == null || data.version <= 0)
                {
                    error = $"Save file is invalid: {path}";
                    return false;
                }

                metadata = data.ToMetadata();
                if (!metadata.supported)
                {
                    error = $"Save version {metadata.version} is newer than supported version {FoundationSaveData.CurrentVersion}.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static string SanitizePathPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "world";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            value = value.Trim();
            return string.IsNullOrWhiteSpace(value) ? "world" : value;
        }

        static string[] ToArray(System.Collections.Generic.IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return Array.Empty<string>();
            var result = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                result[i] = values[i];
            return result;
        }

        static void AtomicWriteAllText(string path, string contents)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            string bak = path + ".bak";
            File.WriteAllText(tmp, contents);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, bak, true);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FoundationBootstrap] Atomic replace failed, falling back to copy: {ex.Message}");
                    File.Copy(path, bak, true);
                    File.Copy(tmp, path, true);
                    File.Delete(tmp);
                    return;
                }
            }

            File.Move(tmp, path);
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
            DisableLegacyCameraZoomControllers();

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
            _pixelPerfectCamera = pp;

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

            cameraSize = _cam.orthographicSize;
        }

        Vector3 _camVel;
        bool _camPanning;
        Vector3 _panGrabWorld;
        [SerializeField] float cameraFollowSmoothTime = 0.15f;
        // Dead-zone follow (ISO-CORE parity). Camera holds still until the player leaves
        // this box (world-unit half-extents), then eases so they sit at the edge. Ratio
        // ~2.4:1 mirrors ISO-CORE's 6.0 x 2.5 box, scaled to our ortho size (6).
        [SerializeField] float cameraDeadZoneX = 2.75f;
        [SerializeField] float cameraDeadZoneY = 1.15f;

        void LateUpdate()
        {
            HandleCameraZoom();
            Crafting?.Tick(Time.deltaTime);

            if (_cam != null && _playerT != null)
            {
                // Right-drag to pan; suspends dead-zone follow while held (ISO-CORE parity).
                if (Input.GetMouseButtonDown(1)) { _panGrabWorld = _cam.ScreenToWorldPoint(Input.mousePosition); _camPanning = true; }
                else if (!Input.GetMouseButton(1)) _camPanning = false;

                if (_camPanning)
                {
                    Vector3 now = _cam.ScreenToWorldPoint(Input.mousePosition);
                    Vector3 d = _panGrabWorld - now; d.z = 0f;
                    Vector3 np = _cam.transform.position + d;
                    _cam.transform.position = new Vector3(np.x, np.y, -10f);
                }
                else
                {
                    var p = _playerT.position;
                    Vector3 pos = _cam.transform.position;
                    // Dead-zone follow: hold still until the player leaves the box, then ease
                    // so they sit at the box edge. Pixel Perfect Camera keeps the image crisp.
                    float dx = p.x - pos.x, dy = p.y - pos.y;
                    float tx = pos.x, ty = pos.y;
                    if (dx > cameraDeadZoneX) tx = p.x - cameraDeadZoneX;
                    else if (dx < -cameraDeadZoneX) tx = p.x + cameraDeadZoneX;
                    if (dy > cameraDeadZoneY) ty = p.y - cameraDeadZoneY;
                    else if (dy < -cameraDeadZoneY) ty = p.y + cameraDeadZoneY;
                    var target = new Vector3(tx, ty, -10f);
                    _cam.transform.position = Vector3.SmoothDamp(pos, target, ref _camVel, cameraFollowSmoothTime);
                }
            }
        }

        void HandleCameraZoom()
        {
            if (_cam == null || !_cam.orthographic)
                return;

            // Scroll-wheel zoom, clamped to the ISO-CORE range (no modifier needed).
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                if (_pixelPerfectCamera != null && _pixelPerfectCamera.enabled) _pixelPerfectCamera.enabled = false;
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * cameraScrollStep, cameraMinSize, cameraMaxSize);
                cameraSize = _cam.orthographicSize;
            }

            float zoomSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(CameraZoomSensitivityPrefKey, 1f), 0.35f, 2.5f);
            float zoomSpeed = cameraZoomUnitsPerSecond * zoomSensitivity;
            float zoomStep = cameraZoomTapStep * zoomSensitivity;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl)
                return;

            bool zoomIn =
                Input.GetKey(KeyCode.Equals) ||
                Input.GetKey(KeyCode.Plus) ||
                Input.GetKey(KeyCode.KeypadPlus);
            bool zoomOut =
                Input.GetKey(KeyCode.Minus) ||
                Input.GetKey(KeyCode.KeypadMinus);

            if (!zoomIn && !zoomOut)
                return;

            if (_pixelPerfectCamera != null && _pixelPerfectCamera.enabled)
            {
                _pixelPerfectCamera.enabled = false;
            }

            float delta = 0f;
            if (zoomIn)
                delta -= zoomSpeed * Time.unscaledDeltaTime;
            if (zoomOut)
                delta += zoomSpeed * Time.unscaledDeltaTime;

            bool zoomInTap =
                Input.GetKeyDown(KeyCode.Equals) ||
                Input.GetKeyDown(KeyCode.Plus) ||
                Input.GetKeyDown(KeyCode.KeypadPlus);
            bool zoomOutTap =
                Input.GetKeyDown(KeyCode.Minus) ||
                Input.GetKeyDown(KeyCode.KeypadMinus);

            if (zoomInTap)
                delta -= zoomStep;
            if (zoomOutTap)
                delta += zoomStep;

            float next = _cam.orthographicSize + delta;
            _cam.orthographicSize = Mathf.Clamp(next, cameraMinSize, cameraMaxSize);
            cameraSize = _cam.orthographicSize;
        }

        void DisableLegacyCameraZoomControllers()
        {
            if (_cam == null)
                return;

            var behaviours = _cam.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour == this)
                    continue;

                if (behaviour.GetType().Name == "ZoomController")
                    behaviour.enabled = false;
            }
        }

        struct LaunchOptions
        {
            public readonly string worldName;
            public readonly int seed;
            public readonly int difficulty;
            public readonly string callingId;
            public readonly string savePath;
            public readonly FoundationLaunchMode launchMode;
            public readonly FoundationCharacterAppearanceSaveData appearance;

            public LaunchOptions(string worldName, int seed, int difficulty, string callingId, string savePath,
                FoundationLaunchMode launchMode, FoundationCharacterAppearanceSaveData appearance)
            {
                this.worldName = worldName;
                this.seed = seed;
                this.difficulty = difficulty;
                this.callingId = callingId;
                this.savePath = savePath;
                this.launchMode = launchMode;
                this.appearance = appearance;
            }
        }

        static string NormalizeWorldName(string worldName)
        {
            return string.IsNullOrWhiteSpace(worldName) ? DefaultWorldName : worldName.Trim();
        }

        static string NormalizeCallingId(string callingId)
        {
            return string.IsNullOrWhiteSpace(callingId) ? null : callingId.Trim();
        }
    }
}
