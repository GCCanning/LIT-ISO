using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Headless integrated gate for MenuScene -> Foundation launch wiring plus the
    /// non-interactive world contracts behind the doc 06 play checklist.
    /// </summary>
    public static class FoundationIntegratedSliceValidator
    {
        const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
        const string FoundationScenePath = "Assets/Scenes/IsoCoreFoundation.unity";
        const string ReportPath = "Docs/IsoCoreFoundation/Integrated_Slice_Validation.md";
        const string TestWorldName = "Codex Integrated Seed Check";
        const string TestSeed = "CozySeed-2026";

        struct Check
        {
            public string name;
            public bool pass;
            public string detail;
        }

        delegate void AddCheck(string name, bool pass, string detail = "");

        public static void Run()
        {
            RunInternal(true);
        }

        public static void RunNoReport()
        {
            RunInternal(false);
        }

        static void RunInternal(bool writeReport)
        {
            var checks = new List<Check>();
            AddCheck add = (name, pass, detail) =>
                checks.Add(new Check { name = name, pass = pass, detail = detail });

            try
            {
                RunChecks(add, writeReport);
            }
            catch (Exception ex)
            {
                add("Validator completed without exception", false, ex.ToString());
            }

            if (writeReport)
                WriteReport(checks);

            int failed = 0;
            foreach (var check in checks)
                if (!check.pass) failed++;

            string reportDetail = writeReport ? $" Report: {ReportPath}" : " Report writing skipped.";
            string summary = $"[FoundationIntegratedSliceValidator] {checks.Count - failed}/{checks.Count} checks passed.{reportDetail}";
            if (failed > 0)
                throw new Exception(summary);

            Debug.Log(summary);
        }

        static void RunChecks(AddCheck add, bool writeReports)
        {
            FoundationBootstrap.ClearLaunchOptions();

            ValidateBuildSettings(add);
            ValidateMenuScene(add);
            ValidateMenuSourceWiring(add);
            ValidateUiSourceWiring(add);
            ValidateLaunchSeedPropagation(add);
            ValidateWorldContracts(add);
            ValidateDungeonGeneration(add);

            string foundationSummary = FoundationValidator.Validate(false, writeReports);
            add("Foundation editor validator passes", foundationSummary.Contains("ALL PASS"), foundationSummary);
        }

        static void ValidateBuildSettings(AddCheck add)
        {
            var scenes = EditorBuildSettings.scenes;
            bool countOk = scenes.Length >= 2;
            add("Build Settings contain menu + foundation slots", countOk, $"{scenes.Length} enabled/disabled entries");
            if (!countOk) return;

            add("Build Settings slot 0 is MenuScene",
                scenes[0].enabled && scenes[0].path == MenuScenePath,
                scenes[0].path);
            add("Build Settings slot 1 is IsoCoreFoundation",
                scenes[1].enabled && scenes[1].path == FoundationScenePath,
                scenes[1].path);
        }

        static void ValidateMenuScene(AddCheck add)
        {
            add("MenuScene asset exists", File.Exists(MenuScenePath), MenuScenePath);
            if (!File.Exists(MenuScenePath)) return;

            var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            Type welcomeType = Type.GetType("WelcomeScreenManager, Assembly-CSharp");
            bool found = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (welcomeType != null && root.GetComponentInChildren(welcomeType, true) != null)
                {
                    found = true;
                    break;
                }
            }

            add("MenuScene contains WelcomeScreenManager", found, welcomeType != null ? welcomeType.FullName : "type not found");
        }

        static void ValidateMenuSourceWiring(AddCheck add)
        {
            string path = "Assets/Scripts/UI/WelcomeScreenManager.cs";
            string source = File.Exists(path) ? File.ReadAllText(path) : "";
            int configure = source.IndexOf("FoundationBootstrap.ConfigureLaunch", StringComparison.Ordinal);
            int load = source.IndexOf("LoadScene(\"IsoCoreFoundation\")", StringComparison.Ordinal);

            add("WelcomeScreenManager source calls ConfigureLaunch", configure >= 0, path);
            add("WelcomeScreenManager loads IsoCoreFoundation", load >= 0, path);
            add("ConfigureLaunch happens before LoadScene", configure >= 0 && load >= 0 && configure < load,
                configure >= 0 && load >= 0 ? $"ConfigureLaunch index {configure}, LoadScene index {load}" : "missing call");
            add("Menu Continue loads Foundation save when present",
                source.Contains("FoundationBootstrap.DefaultSavePathForWorld") &&
                source.Contains("File.Exists(foundationSavePath)") &&
                source.Contains("FoundationBootstrap.ConfigureLoad(foundationSavePath)") &&
                source.Contains("No Foundation save found"),
                "Continue/Load uses save.json when available, seed launch when absent");
            add("Menu launch no longer writes legacy WorldManager",
                !source.Contains("WorldManager.Instance") && !source.Contains("AddComponent<WorldManager>"),
                "FoundationBootstrap.ConfigureLaunch is the canonical launch contract");
            add("Menu has character creation before Foundation launch",
                source.Contains("Screen { MainMenu, CreateWorld, CharacterCreate") &&
                source.Contains("BuildCharacterCreate") &&
                source.Contains("BeginTrial") &&
                source.Contains("characterNameInput") &&
                source.Contains("Begin Trial"),
                "New Trial -> world setup -> character creation -> Begin Trial");
            add("Menu passes character name, Calling, and appearance into Foundation",
                source.Contains("effectiveCalling") &&
                source.Contains("effectiveName") &&
                source.Contains("effectiveAppearance") &&
                source.Contains("FoundationBootstrap.ConfigureLaunch(world.worldName, world.seed, world.difficulty,") &&
                source.Contains("pendingWorld.characterName") &&
                source.Contains("pendingWorld.appearancePreset"),
                "character metadata launch contract");
        }

        static void ValidateUiSourceWiring(AddCheck add)
        {
            string controllerPath = "Assets/Scripts/UI/InGame/GamePanelsController.cs";
            string gameUiPath = "Assets/Scripts/UI/InGame/GameUIController.cs";
            string panelPath = "Assets/Scripts/UI/InGame/CharacterPanelView.cs";
            string initializerPath = "Assets/Scripts/UI/InGame/GameHudInitializer.cs";
            string hudAdapterPath = "Assets/Scripts/UI/InGame/FoundationHudAdapter.cs";
            string craftingAdapterPath = "Assets/Scripts/UI/InGame/FoundationCraftingAdapter.cs";
            string characterAdapterPath = "Assets/Scripts/UI/InGame/FoundationCharacterSheetAdapter.cs";
            string interactionPath = "Assets/Scripts/IsoCoreFoundation/Player/PlayerInteraction.cs";
            string worldControllerPath = "Assets/Scripts/IsoCoreFoundation/World/IsoWorldController.cs";
            string placementPath = "Assets/Scripts/IsoCoreFoundation/Building/PlacementSystem.cs";
            string portalPath = "Assets/Scripts/IsoCoreFoundation/Dungeons/FoundationDungeonPortalSystem.cs";
            string portalInstancePath = "Assets/Scripts/IsoCoreFoundation/Dungeons/FoundationDungeonPortalInstance.cs";
            string portalVisualPath = "Assets/Scripts/IsoCoreFoundation/Dungeons/FoundationPortalVisual.cs";
            string dungeonSpritePath = "Assets/Scripts/IsoCoreFoundation/Dungeons/FoundationDungeonSpriteResolver.cs";
            string instancePath = "Assets/Scripts/IsoCoreFoundation/Building/FoundationInstanceSystem.cs";
            string interiorLayoutPath = "Assets/Scripts/IsoCoreFoundation/Building/FoundationInteriorLayout.cs";
            string interiorResolverPath = "Assets/Scripts/IsoCoreFoundation/Building/FoundationInteriorSpriteResolver.cs";
            string layoutPath = "Assets/Scripts/UI/InGame/PlayerResizableUi.cs";
            string mapPath = "Assets/Scripts/IsoCoreFoundation/UI/FoundationMapOverlay.cs";
            string bootstrapPath = "Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs";
            string showroomPath = "Assets/Scripts/IsoCoreFoundation/Core/FoundationCreationInstanceShowroom.cs";
            string depthPath = "Assets/Scripts/IsoCoreFoundation/World/FoundationDepthPolish.cs";
            string contactShadowPath = "Assets/Scripts/IsoCoreFoundation/World/FoundationContactShadow.cs";
            string weatherPath = "Assets/Scripts/IsoCoreFoundation/World/FoundationWeatherVisuals.cs";
            string ambientPath = "Assets/Scripts/IsoCoreFoundation/World/AmbientLightController.cs";
            string uiBuilderPath = "Assets/Scripts/UI/InGame/UiBuilder.cs";
            string placeablePath = "Assets/Scripts/IsoCoreFoundation/Building/PlaceableInstance.cs";
            string mobPath = "Assets/Scripts/IsoCoreFoundation/Mobs/Mob.cs";
            string campingPath = "Assets/Scripts/IsoCoreFoundation/Survival/FoundationCampingSystem.cs";
            string configPath = "Assets/Scripts/IsoCoreFoundation/Core/FoundationConfig.cs";
            string contentPath = "Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs";
            string placeableDefinitionPath = "Assets/Scripts/IsoCoreFoundation/Building/PlaceableDefinition.cs";
            string mobDefinitionPath = "Assets/Scripts/IsoCoreFoundation/Mobs/MobDefinition.cs";
            string mobSpawnerPath = "Assets/Scripts/IsoCoreFoundation/Mobs/MobSpawner.cs";
            string resourceNodePath = "Assets/Scripts/IsoCoreFoundation/Harvesting/ResourceNode.cs";
            string progressionHooksPath = "Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgressionHooks.cs";
            string tutorialPath = "Assets/Scripts/IsoCoreFoundation/Progression/FoundationTutorialNotifier.cs";
            string trialIntroPath = "Assets/Scripts/UI/InGame/TrialIntroView.cs";
            string controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
            string gameUi = File.Exists(gameUiPath) ? File.ReadAllText(gameUiPath) : "";
            string panel = File.Exists(panelPath) ? File.ReadAllText(panelPath) : "";
            string initializer = File.Exists(initializerPath) ? File.ReadAllText(initializerPath) : "";
            string hudAdapter = File.Exists(hudAdapterPath) ? File.ReadAllText(hudAdapterPath) : "";
            string craftingAdapter = File.Exists(craftingAdapterPath) ? File.ReadAllText(craftingAdapterPath) : "";
            string characterAdapter = File.Exists(characterAdapterPath) ? File.ReadAllText(characterAdapterPath) : "";
            string interaction = File.Exists(interactionPath) ? File.ReadAllText(interactionPath) : "";
            string worldController = File.Exists(worldControllerPath) ? File.ReadAllText(worldControllerPath) : "";
            string placement = File.Exists(placementPath) ? File.ReadAllText(placementPath) : "";
            string portal = File.Exists(portalPath) ? File.ReadAllText(portalPath) : "";
            string portalInstance = File.Exists(portalInstancePath) ? File.ReadAllText(portalInstancePath) : "";
            string portalVisual = File.Exists(portalVisualPath) ? File.ReadAllText(portalVisualPath) : "";
            string dungeonSprite = File.Exists(dungeonSpritePath) ? File.ReadAllText(dungeonSpritePath) : "";
            string instance = File.Exists(instancePath) ? File.ReadAllText(instancePath) : "";
            string interiorLayout = File.Exists(interiorLayoutPath) ? File.ReadAllText(interiorLayoutPath) : "";
            string interiorResolver = File.Exists(interiorResolverPath) ? File.ReadAllText(interiorResolverPath) : "";
            string layout = File.Exists(layoutPath) ? File.ReadAllText(layoutPath) : "";
            string map = File.Exists(mapPath) ? File.ReadAllText(mapPath) : "";
            string bootstrap = File.Exists(bootstrapPath) ? File.ReadAllText(bootstrapPath) : "";
            string showroom = File.Exists(showroomPath) ? File.ReadAllText(showroomPath) : "";
            string depth = File.Exists(depthPath) ? File.ReadAllText(depthPath) : "";
            string contact = File.Exists(contactShadowPath) ? File.ReadAllText(contactShadowPath) : "";
            string weather = File.Exists(weatherPath) ? File.ReadAllText(weatherPath) : "";
            string ambient = File.Exists(ambientPath) ? File.ReadAllText(ambientPath) : "";
            string uiBuilder = File.Exists(uiBuilderPath) ? File.ReadAllText(uiBuilderPath) : "";
            string placeable = File.Exists(placeablePath) ? File.ReadAllText(placeablePath) : "";
            string mob = File.Exists(mobPath) ? File.ReadAllText(mobPath) : "";
            string camping = File.Exists(campingPath) ? File.ReadAllText(campingPath) : "";
            string config = File.Exists(configPath) ? File.ReadAllText(configPath) : "";
            string content = File.Exists(contentPath) ? File.ReadAllText(contentPath) : "";
            string placeableDefinition = File.Exists(placeableDefinitionPath) ? File.ReadAllText(placeableDefinitionPath) : "";
            string mobDefinition = File.Exists(mobDefinitionPath) ? File.ReadAllText(mobDefinitionPath) : "";
            string mobSpawner = File.Exists(mobSpawnerPath) ? File.ReadAllText(mobSpawnerPath) : "";
            string resourceNode = File.Exists(resourceNodePath) ? File.ReadAllText(resourceNodePath) : "";
            string progressionHooks = File.Exists(progressionHooksPath) ? File.ReadAllText(progressionHooksPath) : "";
            string tutorial = File.Exists(tutorialPath) ? File.ReadAllText(tutorialPath) : "";
            string trialIntro = File.Exists(trialIntroPath) ? File.ReadAllText(trialIntroPath) : "";

            add("uGUI uses one canonical tabbed Character panel",
                controller.Contains("CharacterPanelView") &&
                panel.Contains("CharacterPanelTab") &&
                panel.Contains("DrawInventory") &&
                panel.Contains("DrawCrafting") &&
                panel.Contains("DrawSkills") &&
                panel.Contains("DrawQuests") &&
                panel.Contains("DrawSystem"),
                $"{controllerPath} / {panelPath}");
            add("uGUI panel binds Foundation progression and QoL",
                initializer.Contains("BindProgression(progression, bootstrap.QoL)") &&
                panel.Contains("FoundationQoLService") &&
                panel.Contains("CaptureReadState"),
                initializerPath);
            add("Runtime does not create the retired IMGUI FoundationHUD backup",
                bootstrap.Contains("Hud = null") &&
                bootstrap.Contains("uGUI is the canonical runtime UI") &&
                interaction.Contains("CraftingRequested?.Invoke(station)") &&
                !initializer.Contains("DebugImguiHudVisible") &&
                !interaction.Contains("ToggleCrafting") &&
                !interaction.Contains("ToggleInventory"),
                $"{bootstrapPath} / {interactionPath} / {initializerPath}");
            add("Fresh launch plays character Trial intro before normal HUD settles",
                bootstrap.Contains("ActiveCharacterName") &&
                bootstrap.Contains("ActiveLaunchIsLoad") &&
                bootstrap.Contains("appearancePreset") &&
                initializer.Contains("TrialIntroView") &&
                initializer.Contains("_trialIntroView.Play(bootstrap)") &&
                trialIntro.Contains("YOUR TRIAL AWAITS") &&
                trialIntro.Contains("HUD online") &&
                trialIntro.Contains("ActiveLaunchIsLoad"),
                $"{bootstrapPath} / {initializerPath} / {trialIntroPath}");
            add("Crafting panel surfaces every recipe with station and lock reason",
                panel.Contains("Recipes ({count}) - all stations") &&
                panel.Contains("row.disabledReason") &&
                panel.Contains("SelectedRecipeRow") &&
                craftingAdapter.Contains("public int RecipeCount => _content?.Recipes?.Count ?? 0") &&
                craftingAdapter.Contains("station        = StationLabel(r.station)") &&
                craftingAdapter.Contains("disabledReason = disabledReason"),
                $"{panelPath} / {craftingAdapterPath}");
            add("uGUI adapters avoid retired legacy singleton fallbacks",
                !ContainsRetiredRuntimeReference(hudAdapter) &&
                !ContainsRetiredRuntimeReference(characterAdapter),
                $"{hudAdapterPath} / {characterAdapterPath}");
            add("Mouse targeting prefers visible sprite bounds before cell fallback",
                interaction.Contains("TryDecorationUnderCursor") &&
                interaction.Contains("TryNodeUnderCursor") &&
                interaction.Contains("TryPlaceableUnderCursor") &&
                interaction.Contains("TryPortalUnderCursor") &&
                worldController.Contains("TryGetNodeUnderCursor") &&
                placement.Contains("TryGetPlaceableUnderCursor") &&
                portal.Contains("TryGetPortalUnderCursor"),
                $"{interactionPath} / bounds-target providers");
            add("Instance interiors generate wall stacks from layout boundaries",
                instance.Contains("SpawnLayoutWallDressing") &&
                instance.Contains("rel.y + 1") &&
                instance.Contains("rel.x - 1") &&
                instance.Contains("rel.x + 1") &&
                instance.Contains("SpawnWallStack") &&
                !instance.Contains("SpawnTavernDecorations"),
                instancePath);
            add("Tavern interior uses a validated non-rectangular layout mask",
                interiorLayout.Contains("TavernHearthSnug") &&
                interiorLayout.Contains("floorTiles") &&
                interiorLayout.Contains("reservedWalkTiles") &&
                interiorLayout.Contains("FoundationInteriorLayoutValidator") &&
                interiorLayout.Contains("Flood(layout.spawnCell") &&
                instance.Contains("BuildInteriorLayout(FoundationInteriorLayout.TavernHearthSnug(), origin)") &&
                instance.Contains("CopyActiveRenderCells") &&
                worldController.Contains("explicitInstanceCells") &&
                worldController.Contains("CopyActiveRenderCells(_instanceRenderCells"),
                $"{interiorLayoutPath} / {instancePath} / {worldControllerPath}");
            add("Tavern varied layout uses V2 depth-aware decor sprites",
                interiorResolver.Contains("LitIsoDecorV2") &&
                interiorLayout.Contains("tavern_back_bar_v2") &&
                interiorLayout.Contains("tavern_feast_table_v2") &&
                interiorLayout.Contains("tavern_fireplace_v2") &&
                interiorLayout.Contains("wood_bench_row_v2"),
                $"{interiorLayoutPath} / {interiorResolverPath}");
            add("Player HUD layout is authorable and persisted",
                layout.Contains("PlayerResizableUi") &&
                layout.Contains("PlayerPrefs") &&
                layout.Contains("AltHeld") &&
                gameUi.Contains("PlayerResizableUi.Attach") &&
                panel.Contains("PlayerResizableUi.Attach"),
                $"{layoutPath} / uGUI panels");
            add("Map overlay supports saved layout, zoom, pan, and markers",
                map.Contains("SaveRect") &&
                map.Contains("HandleLargeMapNavigation") &&
                map.Contains("DrawPortalMarkers") &&
                map.Contains("DrawLegend") &&
                map.Contains("large.zoom"),
                mapPath);
            add("Map overlay has dungeon inspection mode",
                map.Contains("DrawDungeonMapCells") &&
                map.Contains("DrawDungeonLocalMapCells") &&
                map.Contains("SnapshotActiveDungeonRoomMarkers") &&
                map.Contains("ActiveDungeonExitCell") &&
                map.Contains("RoomColor") &&
                instance.Contains("IsInsideDungeon") &&
                instance.Contains("ActiveDungeonTier") &&
                instance.Contains("SnapshotActiveDungeonRoomMarkers"),
                $"{mapPath} / {instancePath}");
            add("Creation Instance has tier and reroll dungeon lab portals",
                portal.Contains("variantRows") &&
                portal.Contains("tierColumns") &&
                portal.Contains("Reroll Variant") &&
                showroom.Contains("DUNGEON LAB") &&
                showroom.Contains("Press M inside"),
                $"{portalPath} / {showroomPath}");
            add("Dungeon portals use animated dimensional sheet with tier particles",
                File.Exists("Assets/Resources/FoundationPortals/Dimensional_Portal.png") &&
                dungeonSprite.Contains("Dimensional_Portal") &&
                dungeonSprite.Contains("PortalFrames") &&
                portalVisual.Contains("FoundationPortalVisual") &&
                portalVisual.Contains("ParticleSystem") &&
                portalVisual.Contains("ColorForTier") &&
                portalInstance.Contains("FoundationPortalVisual") &&
                instance.Contains("AttachPortalVisual"),
                $"{dungeonSpritePath} / {portalVisualPath} / FoundationPortals/Dimensional_Portal.png");
            add("Camera zoom supports Ctrl plus/minus taps and hold",
                bootstrap.Contains("cameraZoomTapStep") &&
                bootstrap.Contains("KeyCode.Plus") &&
                bootstrap.Contains("KeyCode.KeypadPlus") &&
                bootstrap.Contains("KeyCode.KeypadMinus") &&
                bootstrap.Contains("Input.GetKeyDown") &&
                bootstrap.Contains("_pixelPerfectCamera.enabled = false") &&
                bootstrap.Contains("DisableLegacyCameraZoomControllers"),
                bootstrapPath);
            add("Depth polish is attached across runtime sprite families",
                depth.Contains("FoundationDepthPolish") &&
                contact.Contains("FoundationContactShadow") &&
                bootstrap.Contains("FoundationDepthPolish.Attach(playerGo") &&
                worldController.Contains("FoundationDepthPolish.Attach(go") &&
                placeable.Contains("FoundationDepthPolish.Attach(gameObject") &&
                instance.Contains("FoundationDepthPolish.Attach(go") &&
                portalInstance.Contains("FoundationDepthPolish.Attach(gameObject") &&
                mob.Contains("FoundationDepthPolish.Attach(gameObject"),
                "contact shadows + occlusion fade + long shadows");
            add("Visual weather feeds ambient lighting",
                bootstrap.Contains("FoundationWeatherVisuals") &&
                weather.Contains("FoundationWeatherMood") &&
                weather.Contains("AmbientDimming") &&
                ambient.Contains("ApplyWeather") &&
                ambient.Contains("FoundationWeatherVisuals.Active"),
                $"{weatherPath} / {ambientPath}");
            add("uGUI text readability has default shadow and pixel-perfect canvases",
                uiBuilder.Contains("ApplyTextReadability") &&
                uiBuilder.Contains("Shadow") &&
                uiBuilder.Contains("pixelPerfect = true") &&
                gameUi.Contains("pixelPerfect = true") &&
                gameUi.Contains("effectColor = new Color(0f, 0f, 0f"),
                $"{uiBuilderPath} / {gameUiPath}");
            add("First-hour mechanics tune player speed and held harvest targeting",
                config.Contains("public float moveSpeed = 2.8f") &&
                bootstrap.Contains("config.moveSpeed = Mathf.Clamp") &&
                interaction.Contains("ResourceNode _heldHarvestTarget") &&
                interaction.Contains("HeldHarvestInterval = 0.30f") &&
                resourceNode.Contains("_maxHits = Mathf.Max(1, def.hitsToHarvest)") &&
                resourceNode.Contains("tier + 1"),
                $"{configPath} / {interactionPath} / {resourceNodePath}");
            add("Campfire campsite system is wired into spawn, rest, cook, and tutorial flow",
                File.Exists(campingPath) &&
                placeableDefinition.Contains("isCampsite") &&
                placeableDefinition.Contains("campTier") &&
                placeableDefinition.Contains("campWardRadius") &&
                content.Contains("campfire.isCampsite = true") &&
                content.Contains("fireplace.campTier = 2") &&
                content.Contains("cook_roasted_apple") &&
                content.Contains("rest_at_camp") &&
                bootstrap.Contains("FoundationCampingSystem Camping") &&
                bootstrap.Contains("Camping.Init(Player, Placement, DayNight, Progression, InteractionOverlay)") &&
                bootstrap.Contains("MobSpawner.SetCampingSystem(Camping)") &&
                interaction.Contains("AddCampsiteActions") &&
                interaction.Contains("Rest until dawn") &&
                interaction.Contains("Cook at fire") &&
                tutorial.Contains("Firelight wards weak mobs"),
                $"{campingPath} / camp content wiring");
            add("Camp ward breaches can produce bounded mob aggression",
                mobDefinition.Contains("threatTier") &&
                mobDefinition.Contains("campWardIgnoreChance") &&
                mobDefinition.Contains("contactDamage") &&
                camping.Contains("RollMobSpawnWard") &&
                camping.Contains("breached = Random.value < breachChance") &&
                mobSpawner.Contains("RollMobSpawnWard(def, ground, out breachedWard)") &&
                mobSpawner.Contains("SpawnMob(def, ground, breachedWard") &&
                mob.Contains("SetCombatContext") &&
                mob.Contains("TryAttack") &&
                progressionHooks.Contains("cook_fire_meal"),
                $"{mobDefinitionPath} / {campingPath} / {mobPath}");
        }

        static bool ContainsRetiredRuntimeReference(string source)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            return Regex.IsMatch(source,
                @"\b(PlayerHealth|PlayerMana|PlayerStats|XPSystem|QuestManager|PlayerInventory)\b");
        }

        static void ValidateLaunchSeedPropagation(AddCheck add)
        {
            int expected = FoundationBootstrap.SeedStringToInt(TestSeed);
            int expectedAgain = FoundationBootstrap.SeedStringToInt(TestSeed);
            add("String seed hash is deterministic", expected == expectedAgain, $"{TestSeed} -> {expected}");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            FoundationBootstrap.ConfigureLaunch(TestWorldName, TestSeed, 2, "stonewright");

            var go = new GameObject("FoundationBootstrap_IntegratedValidation");
            var boot = go.AddComponent<FoundationBootstrap>();
            FoundationBootstrap readyBoot = null;
            void OnReady(FoundationBootstrap b) => readyBoot = b;
            FoundationBootstrap.Ready += OnReady;
            try
            {
                typeof(FoundationBootstrap)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(boot, null);
            }
            finally
            {
                FoundationBootstrap.Ready -= OnReady;
            }

            add("ConfigureLaunch seed applied before Foundation world build",
                boot.config != null && boot.config.seed == expected,
                $"expected {expected}, got {(boot.config != null ? boot.config.seed.ToString() : "null config")}");
            add("ConfigureLaunch world name applied",
                boot.ActiveWorldName == TestWorldName,
                boot.ActiveWorldName);
            add("ConfigureLaunch difficulty applied",
                boot.ActiveDifficulty == 2,
                boot.ActiveDifficulty.ToString());
            add("ConfigureLaunch Calling applied before Ready",
                boot.ActiveCallingId == "stonewright" &&
                boot.Progression != null &&
                boot.Progression.CurrentCalling != null &&
                boot.Progression.CurrentCalling.id == "stonewright",
                boot.ActiveCallingId);

            add("Foundation runtime graph creates Player", go.transform.Find("Player") != null);
            add("Foundation runtime graph creates WorldController", go.transform.Find("WorldController") != null);
            add("Foundation runtime graph creates PlacementSystem", go.transform.Find("PlacementSystem") != null);
            add("Foundation runtime graph creates FarmingSystem", go.transform.Find("FarmingSystem") != null);
            add("Foundation runtime graph creates MobSpawner", go.transform.Find("MobSpawner") != null);
            add("Foundation runtime graph creates input router without retired IMGUI HUD",
                go.GetComponent<FoundationHUD>() == null && boot.Hud == null &&
                go.GetComponent<PlayerInteraction>() != null);
            add("Foundation runtime graph creates UI coordinator",
                go.GetComponent<FoundationUiCoordinator>() != null && boot.Ui != null,
                boot.Ui != null ? "uGUI/input ownership ready" : "missing coordinator");
            add("FoundationBootstrap Ready event fires with active instance",
                readyBoot == boot,
                readyBoot != null ? readyBoot.name : "not fired");
            add("FoundationBootstrap exposes Content/World runtime handles",
                boot.Content != null && boot.World != null);
            add("FoundationBootstrap exposes Inventory/Hotbar runtime handles",
                boot.Inventory != null && boot.Hotbar != null &&
                boot.Inventory.SlotCount == boot.inventorySlots && boot.Hotbar.Size == boot.hotbarSlots);
            add("FoundationBootstrap exposes gameplay system handles",
                boot.Player != null && boot.WorldController != null && boot.Placement != null &&
                boot.Instances != null &&
                boot.Farming != null && boot.MobSpawner != null && boot.DayNight != null &&
                boot.Crafting != null && boot.Hud == null && boot.Ui != null && boot.Interaction != null);
            add("FoundationBootstrap exposes mouse interaction overlay and tutorial handles",
                boot.InteractionOverlay != null && boot.TutorialNotifier != null &&
                boot.TutorialNotifier.CurrentStep >= 0);
            add("FoundationBootstrap exposes LitRPG progression handles",
                boot.Progression != null && boot.Stats != null &&
                boot.ProgressionHooks != null &&
                boot.Stats.Health01 >= 0f && boot.Stats.Health01 <= 1f &&
                boot.Stats.Mana01 >= 0f && boot.Stats.Mana01 <= 1f &&
                boot.Stats.Xp01 >= 0f && boot.Stats.Xp01 <= 1f &&
                boot.Stats.Level >= 1 &&
                !string.IsNullOrWhiteSpace(boot.Stats.Class) &&
                !string.IsNullOrWhiteSpace(boot.Stats.Title),
                boot.Stats != null ? $"{boot.Stats.Class}/{boot.Stats.Title} L{boot.Stats.Level}" : "missing stats");
            var qolRead = boot.QoL?.CaptureReadState();
            add("FoundationBootstrap exposes QoL data spine handles",
                boot.QoL != null &&
                qolRead != null &&
                qolRead.feedSettings != null &&
                qolRead.feedSettings.channels != null &&
                qolRead.feedSettings.channels.Length >= 10 &&
                qolRead.inventory != null &&
                qolRead.accessibility.hudScale >= 0.75f,
                qolRead?.feedSettings?.channels != null ? $"channels {qolRead.feedSettings.channels.Length}" : "missing QoL");
            add("FoundationContent includes LitRPG bible seed content",
                boot.Content.Callings.Count >= 7 && boot.Content.Skills.Count >= 12 && boot.Content.Quests.Count >= 5 &&
                boot.Content.EvidenceEvents.Count >= 8 && boot.Content.Titles.Count >= 6 && boot.Content.Affinities.Count >= 7,
                $"Callings:{boot.Content.Callings.Count} Skills:{boot.Content.Skills.Count} Quests:{boot.Content.Quests.Count} Evidence:{boot.Content.EvidenceEvents.Count}");
            add("FoundationProgressionHooks starts playable starter quests",
                boot.Progression.IsQuestActive("first_flame_first_field") &&
                boot.Progression.IsQuestActive("a_roof_before_rain") &&
                boot.Progression.IsQuestActive("thread_twig_and_tin") &&
                boot.Progression.IsQuestActive("fixing_the_south_path"));

            ValidateBootstrapSaveLoad(add, boot, expected);

            var spawner = go.GetComponentInChildren<MobSpawner>();
            bool spawned = TryForceMobSpawn(spawner);
            add("MobSpawner can spawn at least one mob", spawned,
                spawner != null ? $"count {spawner.Count}" : "spawner missing");

            UnityEngine.Object.DestroyImmediate(go);

            var headlessGo = new GameObject("FoundationBootstrap_NoImguiHudValidation");
            var headlessBoot = headlessGo.AddComponent<FoundationBootstrap>();
            typeof(FoundationBootstrap)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(headlessBoot, null);
            add("FoundationBootstrap keeps retired IMGUI HUD uncreated",
                headlessBoot.Hud == null && headlessGo.GetComponent<FoundationHUD>() == null &&
                headlessGo.GetComponent<PlayerInteraction>() != null);
            add("FoundationBootstrap exposes UI binding handles through canonical shell",
                headlessBoot.Inventory != null && headlessBoot.Hotbar != null &&
                headlessBoot.Content != null && headlessBoot.World != null &&
                headlessBoot.Progression != null && headlessBoot.Stats != null &&
                headlessBoot.ProgressionHooks != null && headlessBoot.Instances != null &&
                headlessBoot.InteractionOverlay != null && headlessBoot.TutorialNotifier != null &&
                headlessBoot.QoL != null && headlessBoot.Ui != null && headlessBoot.Interaction != null);
            UnityEngine.Object.DestroyImmediate(headlessGo);

            FoundationBootstrap.ClearLaunchOptions();
        }

        static bool TryForceMobSpawn(MobSpawner spawner)
        {
            if (spawner == null) return false;
            MethodInfo trySpawn = typeof(MobSpawner).GetMethod("TrySpawn", BindingFlags.Instance | BindingFlags.NonPublic);
            if (trySpawn == null) return false;

            for (int i = 0; i < 80 && spawner.Count == 0; i++)
                trySpawn.Invoke(spawner, null);

            return spawner.Count > 0;
        }

        static void ValidateBootstrapSaveLoad(AddCheck add, FoundationBootstrap boot, int expectedSeed)
        {
            if (boot == null || boot.World == null || boot.Inventory == null || boot.Progression == null)
            {
                add("Foundation save/load test has runtime handles", false, "bootstrap incomplete");
                return;
            }

            string path = Path.GetFullPath(Path.Combine("Temp", "FoundationSaveLoadValidation.json"));
            string sparsePath = Path.GetFullPath(Path.Combine("Temp", "FoundationSparseSaveValidation.json"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(sparsePath)) File.Delete(sparsePath);

                int mismatchedSeed = expectedSeed == int.MaxValue ? expectedSeed - 1 : expectedSeed + 1;
                string defaultPath = boot.DefaultSavePath;
                string expectedDefaultPath = FoundationBootstrap.DefaultSavePathForWorld(boot.ActiveWorldName, expectedSeed);
                string seedlessDefaultPath = FoundationBootstrap.DefaultSavePathForWorld(boot.ActiveWorldName);
                string otherSeedPath = FoundationBootstrap.DefaultSavePathForWorld(boot.ActiveWorldName, mismatchedSeed);
                string defaultFolder = Path.GetFileName(Path.GetDirectoryName(defaultPath));
                add("FoundationBootstrap.DefaultSavePath includes active seed",
                    string.Equals(defaultPath, expectedDefaultPath, StringComparison.Ordinal) &&
                    !string.Equals(defaultPath, seedlessDefaultPath, StringComparison.Ordinal) &&
                    !string.Equals(defaultPath, otherSeedPath, StringComparison.Ordinal) &&
                    defaultFolder != null &&
                    defaultFolder.EndsWith($"_{expectedSeed}", StringComparison.Ordinal),
                    defaultFolder ?? defaultPath);

                boot.Inventory.Add("copper_bar", 3);
                boot.Hotbar.Select(2);
                boot.DayNight.SetTime(0.66f);
                boot.Player.SetCell(2, -3);
                boot.Progression.AdvanceQuestObjective("first_flame_first_field", "gather_wood", 2);
                boot.Progression.RecordEvidence("harvest_wood", 2, "validator_tree");
                boot.Progression.SetTrialDay(6);
                boot.Progression.AdvanceTrialDay();
                boot.QoL.SetFeedChannelVisible(FoundationSystemFeedChannel.TrialEvidence, false);
                boot.QoL.PinGoal(FoundationPinnedGoalType.Quest, "first_flame_first_field");
                boot.QoL.SetInventorySlotFlags(0, favorite: true, locked: true);
                boot.QoL.SetAccessibility(1.25f, 6f, 0.75f, reducedMotion: true, highContrast: true);

                var blockCell = FindBuildableCell(boot.World);
                var cropCell = FindBuildableCell(boot.World, blockCell);
                var chestCell = FindBuildableCell(boot.World, blockCell, cropCell);
                bool blockPlaced = blockCell != InvalidCell &&
                    boot.World.TryPlaceBlock(blockCell.x, blockCell.y, boot.Content.Blocks.Get("stone_block"));
                bool tilled = cropCell != InvalidCell && boot.World.TryTill(cropCell.x, cropCell.y);
                if (cropCell != InvalidCell)
                    boot.Farming.RestoreCrops(new[]
                    {
                        new FoundationSavedCrop { cropId = "carrot_crop", x = cropCell.x, y = cropCell.y, stage = 1, stageTimer = 2.5f }
                    });
                if (chestCell != InvalidCell)
                {
                    boot.Placement.RestorePlaceables(new[]
                    {
                        new FoundationSavedPlaceable { placeableId = "chest", x = chestCell.x, y = chestCell.y }
                    });
                    if (boot.Storage != null && boot.Storage.TryGetContainer(chestCell.x, chestCell.y, out var chest))
                        chest.Add("carrot", 4);
                }

                var tavern = boot.Content.Placeables.Get("tavern_door");
                Vector2 exteriorGround = boot.Player.Ground;
                bool enteredInstance = tavern != null &&
                    boot.Instances != null &&
                    boot.Instances.Enter(tavern, boot.Player.CurrentCell);
                Vector2Int instanceCell = boot.Player.CurrentCell;
                add("Tavern instance uses expanded render bounds",
                    enteredInstance &&
                    boot.Instances.ActiveRenderMax.x - boot.Instances.ActiveRenderMin.x + 1 >= 22 &&
                    boot.Instances.ActiveRenderMax.y - boot.Instances.ActiveRenderMin.y + 1 >= 22,
                    enteredInstance ? $"{boot.Instances.ActiveRenderMin}->{boot.Instances.ActiveRenderMax}" : "not entered");
            add("Foundation map overlay exists",
                boot.MapOverlay != null,
                boot.MapOverlay != null ? "minimap + M fullscreen map" : "missing");
            add("Animated campfire sheet resolves",
                FoundationPlaceableSpriteResolver.CampfireFrames().Length >= 6,
                $"frames {FoundationPlaceableSpriteResolver.CampfireFrames().Length}");

            bool saved = boot.Save(path);
                bool metadataOk = FoundationBootstrap.TryReadSaveMetadata(path, out var metadata, out string metadataError);
                var savedDto = File.Exists(path) ? JsonUtility.FromJson<FoundationSaveData>(File.ReadAllText(path)) : null;
                bool dtoQuestProgressOk = false;
                if (savedDto?.progression?.quests != null)
                {
                    for (int i = 0; i < savedDto.progression.quests.Length; i++)
                    {
                        var quest = savedDto.progression.quests[i];
                        if (quest == null || quest.questId != "first_flame_first_field" || quest.objectives == null)
                            continue;
                        for (int j = 0; j < quest.objectives.Length; j++)
                            if (quest.objectives[j].id == "gather_wood" && quest.objectives[j].value == 2)
                                dtoQuestProgressOk = true;
                    }
                }
                add("Foundation save DTO captures runtime state directly",
                    savedDto != null &&
                    savedDto.version == FoundationSaveData.CurrentVersion &&
                    savedDto.worldName == boot.ActiveWorldName &&
                    savedDto.seed == expectedSeed &&
                    savedDto.difficulty == boot.ActiveDifficulty &&
                    savedDto.callingId == boot.ActiveCallingId &&
                    savedDto.hotbarSelected == 2 &&
                    savedDto.progression != null &&
                    savedDto.progression.currentCallingId == boot.ActiveCallingId &&
                    savedDto.progression.trialLifecycle != null &&
                    savedDto.progression.trialLifecycle.completed &&
                    savedDto.qol != null &&
                    savedDto.qol.pinnedGoals != null &&
                    savedDto.qol.pinnedGoals.Length == 1 &&
                    savedDto.exploredMapCells != null &&
                    savedDto.exploredMapCells.Length > 0 &&
                    dtoQuestProgressOk,
                    savedDto != null ? $"v{savedDto.version}, pins {savedDto.qol?.pinnedGoals?.Length ?? 0}, map {savedDto.exploredMapCells?.Length ?? 0}" : "missing DTO");

                FoundationBootstrap.ClearLaunchOptions();
                FoundationBootstrap.ConfigureLoad(path);
                var autoLoadGo = new GameObject("FoundationBootstrap_AutoLoadValidation");
                var autoLoad = autoLoadGo.AddComponent<FoundationBootstrap>();
                bool readySawLoadedSave = false;
                void OnAutoLoadReady(FoundationBootstrap b)
                {
                    if (b != autoLoad) return;
                    readySawLoadedSave =
                        b.Inventory != null &&
                        b.Progression != null &&
                        b.Inventory.Count("copper_bar") == 3 &&
                        b.Progression.GetObjectiveProgress("first_flame_first_field", "gather_wood") == 2 &&
                        b.Instances != null &&
                        b.Instances.IsInsideInstance &&
                        b.ActiveWorldName == boot.ActiveWorldName &&
                        b.config != null &&
                        b.config.seed == expectedSeed;
                }
                FoundationBootstrap.Ready += OnAutoLoadReady;
                try
                {
                    typeof(FoundationBootstrap)
                        .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(autoLoad, null);
                }
                finally
                {
                    FoundationBootstrap.Ready -= OnAutoLoadReady;
                }
                add("FoundationBootstrap.ConfigureLoad applies save before Ready",
                    saved && readySawLoadedSave,
                    $"copper {autoLoad.Inventory?.Count("copper_bar") ?? -1}, seed {(autoLoad.config != null ? autoLoad.config.seed.ToString() : "missing")}");
                UnityEngine.Object.DestroyImmediate(autoLoadGo);

                FoundationBootstrap.ClearLaunchOptions();
                FoundationBootstrap.ConfigureLaunch(boot.ActiveWorldName, mismatchedSeed.ToString(), boot.ActiveDifficulty, boot.ActiveCallingId);
                var mismatchGo = new GameObject("FoundationBootstrap_SaveLoadSeedMismatchValidation");
                var mismatch = mismatchGo.AddComponent<FoundationBootstrap>();
                typeof(FoundationBootstrap)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(mismatch, null);

                int mismatchCopperBefore = mismatch.Inventory != null ? mismatch.Inventory.Count("copper_bar") : -1;
                bool mismatchLoaded = mismatch.Load(path);
                int mismatchCopperAfter = mismatch.Inventory != null ? mismatch.Inventory.Count("copper_bar") : -1;
                int mismatchQuestProgress = mismatch.Progression != null
                    ? mismatch.Progression.GetObjectiveProgress("first_flame_first_field", "gather_wood")
                    : -1;
                add("FoundationBootstrap.Load refuses mismatched save seed",
                    saved &&
                    !mismatchLoaded &&
                    mismatch.config != null &&
                    mismatch.config.seed == mismatchedSeed &&
                    mismatch.Inventory != null &&
                    mismatch.Progression != null &&
                    mismatchCopperAfter == mismatchCopperBefore &&
                    mismatchQuestProgress == 0,
                    $"save seed {expectedSeed}, active seed {(mismatch.config != null ? mismatch.config.seed.ToString() : "missing config")}, loaded {mismatchLoaded}");
                UnityEngine.Object.DestroyImmediate(mismatchGo);

                FoundationBootstrap.ClearLaunchOptions();
                FoundationBootstrap.ConfigureLaunch(boot.ActiveWorldName, expectedSeed.ToString(), boot.ActiveDifficulty, boot.ActiveCallingId);
                var loadedGo = new GameObject("FoundationBootstrap_SaveLoadValidation");
                var loaded = loadedGo.AddComponent<FoundationBootstrap>();
                typeof(FoundationBootstrap)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(loaded, null);

                bool loadedOk = loaded.Load(path);
                var loadedCrops = loaded.Farming.SnapshotCrops();

                add("FoundationBootstrap.Save writes a save file",
                    saved && File.Exists(path),
                    path);
                add("FoundationBootstrap.TryReadSaveMetadata reads menu-safe save summary",
                    metadataOk &&
                    metadata.version == FoundationSaveData.CurrentVersion &&
                    metadata.supported &&
                    metadata.worldName == boot.ActiveWorldName &&
                    metadata.seed == expectedSeed &&
                    metadata.callingId == boot.ActiveCallingId &&
                    metadata.inventoryItemCount >= 3 &&
                    metadata.placedObjectCount == 1 &&
                    metadata.storageContainerCount == 1 &&
                    metadata.cropCount == 1,
                    metadataOk ? $"{metadata.worldName} seed {metadata.seed} level {metadata.level}" : metadataError);
                add("FoundationBootstrap.Load applies save data",
                    loadedOk &&
                    loaded.Inventory.Count("copper_bar") == 3 &&
                    loaded.Hotbar.Selected == 2 &&
                    Mathf.Abs(loaded.DayNight.time - 0.66f) < 0.01f,
                    loadedOk ? $"hotbar {loaded.Hotbar.Selected}, copper {loaded.Inventory.Count("copper_bar")}, time {loaded.DayNight.time:0.00}" : "load failed");
                add("Save/load preserves modified solid block collision",
                    blockPlaced &&
                    loaded.World.IsBlocked(blockCell.x, blockCell.y) &&
                    loaded.World.GetCell(blockCell.x, blockCell.y).SurfaceBlockId == "stone_block",
                    blockCell.ToString());
                add("Save/load preserves placed objects",
                    chestCell != InvalidCell &&
                    loaded.World.GetCell(chestCell.x, chestCell.y).OccupantId == "chest" &&
                    loaded.World.IsBlocked(chestCell.x, chestCell.y),
                    chestCell.ToString());
                bool loadedChest = chestCell != InvalidCell &&
                    loaded.Storage != null &&
                    loaded.Storage.TryGetContainer(chestCell.x, chestCell.y, out var loadedStorage) &&
                    loadedStorage.Count("carrot") == 4;
                add("Save/load preserves storage container contents",
                    loadedChest,
                    chestCell != InvalidCell && loaded.Storage != null && loaded.Storage.TryGetContainer(chestCell.x, chestCell.y, out var detailStorage)
                        ? $"carrot {detailStorage.Count("carrot")}"
                        : "missing storage");
                add("Save/load preserves crops",
                    tilled && loadedCrops.Length == 1 && loadedCrops[0].cropId == "carrot_crop" && loadedCrops[0].stage == 1,
                    loadedCrops.Length > 0 ? $"{loadedCrops[0].cropId} stage {loadedCrops[0].stage}" : "no crops");
                add("Save/load preserves quest progress",
                    loaded.Progression.GetObjectiveProgress("first_flame_first_field", "gather_wood") == 2,
                    loaded.Progression.GetObjectiveProgress("first_flame_first_field", "gather_wood").ToString());
                add("Save/load preserves trial evidence spine",
                    loaded.Progression.GetTrialScore(TrialEvidenceCategory.Gathering) >= 4 &&
                    loaded.Progression.GetXpChannelValue("woodcraft") >= 6 &&
                    loaded.Progression.GetTitleProgress("first_night_survivor") >= 2 &&
                    loaded.Progression.GetAffinityScore("root") >= 2 &&
                    loaded.Progression.SystemFeed.Messages.Count >= 1 &&
                    loaded.Progression.EvidenceLog.Count >= 1 &&
                    loaded.Progression.EvidenceLog[0].eventId == "harvest_wood" &&
                    loaded.Progression.EvidenceLog[0].sourceId == "validator_tree" &&
                    loaded.Progression.TrialCompleted &&
                    loaded.Progression.TrialDay == 7,
                    $"gather {loaded.Progression.GetTrialScore(TrialEvidenceCategory.Gathering)}, log {loaded.Progression.EvidenceLog.Count}, day {loaded.Progression.TrialDay}");
                var loadedQoL = loaded.QoL.CaptureReadState();
                bool trialEvidenceVisible = false;
                if (loadedQoL.visibleMessages != null)
                    for (int i = 0; i < loadedQoL.visibleMessages.Length; i++)
                        if (loadedQoL.visibleMessages[i].channel == FoundationSystemFeedChannel.TrialEvidence)
                            trialEvidenceVisible = true;
                add("Save/load preserves QoL data spine",
                    loadedQoL.feedSettings != null &&
                    !loaded.QoL.IsFeedChannelVisible(FoundationSystemFeedChannel.TrialEvidence) &&
                    loadedQoL.pinnedGoals != null &&
                    loadedQoL.pinnedGoals.Length == 1 &&
                    loadedQoL.pinnedGoals[0].type == FoundationPinnedGoalType.Quest &&
                    loadedQoL.pinnedGoals[0].targetId == "first_flame_first_field" &&
                    loadedQoL.pinnedGoals[0].available &&
                    loadedQoL.inventory != null &&
                    loadedQoL.inventory.slots != null &&
                    loadedQoL.inventory.slots.Length > 0 &&
                    loadedQoL.inventory.slots[0].favorite &&
                    loadedQoL.inventory.slots[0].locked &&
                    Math.Abs(loadedQoL.accessibility.hudScale - 1.25f) < 0.01f &&
                    loadedQoL.accessibility.reducedMotion &&
                    loadedQoL.accessibility.highContrast &&
                    !trialEvidenceVisible,
                    $"pins {loadedQoL.pinnedGoals?.Length ?? 0}, hud {loadedQoL.accessibility.hudScale:0.00}");
                var loadedMapCells = loaded.MapOverlay != null
                    ? loaded.MapOverlay.SnapshotExploredCells()
                    : Array.Empty<FoundationSavedMapCell>();
                add("Save/load preserves explored map cells",
                    savedDto?.exploredMapCells != null &&
                    savedDto.exploredMapCells.Length > 0 &&
                    loadedMapCells.Length >= savedDto.exploredMapCells.Length,
                    $"saved {savedDto?.exploredMapCells?.Length ?? 0}, loaded {loadedMapCells.Length}");
                add("Save/load preserves player cell",
                    enteredInstance && loaded.Player.CurrentCell == instanceCell,
                    loaded.Player.CurrentCell.ToString());
                bool loadedInstance = loaded.Instances != null &&
                    loaded.Instances.IsInsideInstance &&
                    loaded.Instances.ActiveInstanceId == "tavern_common_room";
                add("Save/load preserves active building instance",
                    loadedInstance,
                    loaded.Instances != null ? loaded.Instances.ActiveInstanceId : "missing instance system");
                bool exitedInstance = loadedInstance && loaded.Instances.Exit();
                add("Building instance exit returns to exterior",
                    exitedInstance &&
                    !loaded.Instances.IsInsideInstance &&
                    Vector2.Distance(loaded.Player.Ground, exteriorGround) < 0.01f,
                    exitedInstance ? loaded.Player.CurrentCell.ToString() : "exit failed");

                FoundationBootstrap.ClearLaunchOptions();
                FoundationBootstrap.ConfigureLaunch(boot.ActiveWorldName, expectedSeed.ToString(), boot.ActiveDifficulty, boot.ActiveCallingId);
                var sparseGo = new GameObject("FoundationBootstrap_SparseSaveValidation");
                var sparse = sparseGo.AddComponent<FoundationBootstrap>();
                typeof(FoundationBootstrap)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(sparse, null);

                bool sparseSaved = sparse.Save(sparsePath);
                bool sparseLoaded = loaded.Load(sparsePath);
                bool staleBlockCleared = blockCell != InvalidCell &&
                    !loaded.World.GetCell(blockCell.x, blockCell.y).SolidBlock &&
                    loaded.World.IsWalkable(blockCell.x, blockCell.y);
                bool staleChestCleared = chestCell != InvalidCell &&
                    !loaded.World.GetCell(chestCell.x, chestCell.y).HasOccupant &&
                    (loaded.Storage == null || !loaded.Storage.TryGetContainer(chestCell.x, chestCell.y, out _));
                bool staleCropCleared = loaded.Farming.SnapshotCrops().Length == 0;
                add("FoundationBootstrap.Load clears stale same-session world state",
                    sparseSaved && sparseLoaded && staleBlockCleared && staleChestCleared && staleCropCleared,
                    $"block {staleBlockCleared}, chest {staleChestCleared}, crops {staleCropCleared}");
                UnityEngine.Object.DestroyImmediate(sparseGo);

                string futurePath = Path.GetFullPath(Path.Combine("Temp", "FoundationFutureSaveValidation.json"));
                var futureData = new FoundationSaveData
                {
                    version = FoundationSaveData.CurrentVersion + 1,
                    worldName = boot.ActiveWorldName,
                    seed = expectedSeed,
                    savedUtc = DateTime.UtcNow.ToString("o"),
                };
                File.WriteAllText(futurePath, JsonUtility.ToJson(futureData, true));
                bool futureMetadataOk = FoundationBootstrap.TryReadSaveMetadata(futurePath, out var futureMetadata, out string futureError);
                bool futureLoaded = loaded.Load(futurePath);
                add("Foundation save metadata rejects future save versions",
                    !futureMetadataOk &&
                    futureMetadata != null &&
                    !futureMetadata.supported &&
                    futureError.Contains("newer"),
                    futureError);
                add("FoundationBootstrap.Load rejects future save versions",
                    !futureLoaded,
                    $"loaded {futureLoaded}");
                if (File.Exists(futurePath)) File.Delete(futurePath);

                UnityEngine.Object.DestroyImmediate(loadedGo);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(sparsePath)) File.Delete(sparsePath);
                string futurePath = Path.GetFullPath(Path.Combine("Temp", "FoundationFutureSaveValidation.json"));
                if (File.Exists(futurePath)) File.Delete(futurePath);
                FoundationBootstrap.ClearLaunchOptions();
            }
        }

        static void ValidateWorldContracts(AddCheck add)
        {
            var content = FoundationContent.BuildDefault();
            var cfg = new FoundationConfig { seed = FoundationBootstrap.SeedStringToInt(TestSeed) };
            var world = new IsoWorld(new IsoTerrainSampler(cfg, content), content, cfg.chunkSize);

            var sameCfg = new FoundationConfig { seed = FoundationBootstrap.SeedStringToInt(TestSeed) };
            var sameWorld = new IsoWorld(new IsoTerrainSampler(sameCfg, content), content, sameCfg.chunkSize);
            var a = world.GetCell(17, -9);
            var b = sameWorld.GetCell(17, -9);
            add("Same seed samples the same terrain cell",
                a.Height == b.Height && a.SurfaceBlockId == b.SurfaceBlockId && a.BiomeIndex == b.BiomeIndex,
                $"cell(17,-9): {a.SurfaceBlockId}/h{a.Height}/b{a.BiomeIndex}");

            Vector2Int buildCell = FindBuildableCell(world);
            add("Found a buildable walkable cell", buildCell != InvalidCell, buildCell.ToString());
            if (buildCell != InvalidCell)
            {
                bool tilled = world.TryTill(buildCell.x, buildCell.y);
                add("Farming contract tills walkable soil", tilled && world.GetCell(buildCell.x, buildCell.y).SurfaceBlockId == "soil");
            }

            Vector2Int blockCell = FindBuildableCell(world, buildCell);
            var stoneBlock = content.Blocks.Get("stone_block");
            bool placedBlock = blockCell != InvalidCell && world.TryPlaceBlock(blockCell.x, blockCell.y, stoneBlock);
            add("Placement contract places a solid block", placedBlock, blockCell.ToString());
            add("Placed solid block blocks movement query",
                placedBlock && world.IsBlocked(blockCell.x, blockCell.y),
                placedBlock ? world.GetCell(blockCell.x, blockCell.y).SurfaceBlockId : "not placed");
            add("Placed solid block can be removed without soft-lock",
                placedBlock && world.RemoveSolidBlock(blockCell.x, blockCell.y) && world.IsWalkable(blockCell.x, blockCell.y));

            Vector2Int fullInventoryBlockCell = FindBuildableCell(world, buildCell, blockCell);
            bool fullInventoryBlockPlaced = fullInventoryBlockCell != InvalidCell &&
                world.TryPlaceBlock(fullInventoryBlockCell.x, fullInventoryBlockCell.y, stoneBlock);
            var fullInvPlacementGo = new GameObject("Placement_FullInventoryValidation");
            var fullInvPlacement = fullInvPlacementGo.AddComponent<PlacementSystem>();
            var fullBlockInv = new Inventory(1, content);
            fullBlockInv.Add("wood", 99);
            fullInvPlacement.Init(world, content, fullBlockInv, new Hotbar(fullBlockInv, 1), Camera.main, null);
            string fullBlockMessage = "";
            bool blockedRefund = fullInventoryBlockPlaced &&
                !fullInvPlacement.TryRemoveAtCell(fullInventoryBlockCell.x, fullInventoryBlockCell.y, out fullBlockMessage) &&
                world.GetCell(fullInventoryBlockCell.x, fullInventoryBlockCell.y).SolidBlock;
            add("Full inventory blocks solid-block removal before refund loss",
                blockedRefund,
                fullBlockMessage ?? "");
            if (fullInventoryBlockPlaced)
                world.RemoveSolidBlock(fullInventoryBlockCell.x, fullInventoryBlockCell.y);
            UnityEngine.Object.DestroyImmediate(fullInvPlacementGo);

            Vector2Int placeableCell = FindBuildableCell(world, buildCell, blockCell);
            var workbench = content.Placeables.Get("workbench");
            bool placedOccupant = placeableCell != InvalidCell && world.TryPlaceOccupant(placeableCell.x, placeableCell.y, workbench.id, workbench.blocksMovement);
            add("Placeable occupancy writes to world", placedOccupant, placeableCell.ToString());
            add("Blocking placeable blocks movement query",
                placedOccupant && world.IsBlocked(placeableCell.x, placeableCell.y));
            add("Placeable occupancy clears cleanly",
                placedOccupant && world.ClearOccupant(placeableCell.x, placeableCell.y) && world.IsWalkable(placeableCell.x, placeableCell.y));

            Vector2Int fullInventoryPlaceableCell = FindBuildableCell(world, buildCell, blockCell, placeableCell, fullInventoryBlockCell);
            bool fullInventoryPlaceablePlaced = fullInventoryPlaceableCell != InvalidCell &&
                world.TryPlaceOccupant(fullInventoryPlaceableCell.x, fullInventoryPlaceableCell.y, workbench.id, workbench.blocksMovement);
            var fullInvPlaceableGo = new GameObject("Placement_FullInventoryPlaceableValidation");
            var fullInvPlaceable = fullInvPlaceableGo.AddComponent<PlacementSystem>();
            var fullPlaceableInv = new Inventory(1, content);
            fullPlaceableInv.Add("stone", 99);
            fullInvPlaceable.Init(world, content, fullPlaceableInv, new Hotbar(fullPlaceableInv, 1), Camera.main, null);
            if (fullInventoryPlaceablePlaced)
                fullInvPlaceable.RestorePlaceables(new[]
                {
                    new FoundationSavedPlaceable { placeableId = "workbench", x = fullInventoryPlaceableCell.x, y = fullInventoryPlaceableCell.y }
                });
            string fullPlaceableMessage = "";
            bool blockedPlaceableRefund = fullInventoryPlaceablePlaced &&
                !fullInvPlaceable.TryRemoveAtCell(fullInventoryPlaceableCell.x, fullInventoryPlaceableCell.y, out fullPlaceableMessage) &&
                world.GetCell(fullInventoryPlaceableCell.x, fullInventoryPlaceableCell.y).OccupantId == "workbench";
            add("Full inventory blocks placeable removal before refund loss",
                blockedPlaceableRefund,
                fullPlaceableMessage ?? "");
            if (fullInventoryPlaceablePlaced)
                world.ClearOccupant(fullInventoryPlaceableCell.x, fullInventoryPlaceableCell.y);
            UnityEngine.Object.DestroyImmediate(fullInvPlaceableGo);

            ValidateHarvest(add, content, world);
            ValidateCrafting(add, content);
            ValidateFarmingData(add, content);
            ValidateProgressionContracts(add, content);
        }

        static readonly Vector2Int InvalidCell = new(int.MinValue, int.MinValue);

        static Vector2Int FindBuildableCell(IsoWorld world, params Vector2Int[] exclude)
        {
            for (int y = -16; y <= 16; y++)
            for (int x = -16; x <= 16; x++)
            {
                var c = new Vector2Int(x, y);
                bool skipped = false;
                foreach (var ex in exclude)
                    if (ex == c) { skipped = true; break; }
                if (skipped) continue;

                var cell = world.GetCell(x, y);
                if (!cell.Blocked && !cell.HasNode && !cell.HasOccupant && !cell.Water && !cell.SolidBlock)
                    return c;
            }
            return InvalidCell;
        }

        static void ValidateHarvest(AddCheck add, FoundationContent content, IsoWorld world)
        {
            Vector2Int nodeCell = InvalidCell;
            for (int y = -48; y <= 48 && nodeCell == InvalidCell; y++)
            for (int x = -48; x <= 48; x++)
            {
                if (world.GetCell(x, y).HasNode)
                {
                    nodeCell = new Vector2Int(x, y);
                    break;
                }
            }

            add("Found a harvestable resource node", nodeCell != InvalidCell, nodeCell.ToString());
            if (nodeCell == InvalidCell) return;

            var cell = world.GetCell(nodeCell.x, nodeCell.y);
            var def = content.Nodes.Get(cell.NodeId);
            var inv = new Inventory(12, content);
            var go = new GameObject("ResourceNode_IntegratedValidation");
            var node = go.AddComponent<ResourceNode>();
            node.Init(def, world, nodeCell.x, nodeCell.y);

            bool depleted = false;
            bool blockedFull = false;
            for (int i = 0; i < 8 && !depleted; i++)
                depleted = node.Harvest(inv, ToolType.None, 1, out blockedFull);

            add("Harvesting depletes a node without full-inventory block", depleted && !blockedFull, def != null ? def.id : "missing def");
            add("Harvest drops enter inventory", inv.Count("wood") + inv.Count("stone") + inv.Count("fiber") + inv.Count("apple") + inv.Count("copper_ore") > 0);
            add("Harvest clears node occupancy", !world.GetCell(nodeCell.x, nodeCell.y).HasNode);

            UnityEngine.Object.DestroyImmediate(go);

            var fullInv = new Inventory(1, content);
            fullInv.Add("stone", 99);
            var blockedGo = new GameObject("ResourceNode_FullInventoryValidation");
            var blockedNode = blockedGo.AddComponent<ResourceNode>();
            blockedNode.Init(def, world, nodeCell.x, nodeCell.y);
            int beforeHits = blockedNode.RemainingHits;
            bool blockedDepleted = blockedNode.Harvest(fullInv, def.requiredTool, 99, out bool blockedHarvestFull);
            add("Full inventory blocks harvest depletion before drop loss",
                !blockedDepleted && blockedHarvestFull && blockedNode.RemainingHits == beforeHits,
                $"blocked {blockedHarvestFull}, hits {beforeHits}->{blockedNode.RemainingHits}");
            UnityEngine.Object.DestroyImmediate(blockedGo);
        }

        static void ValidateCrafting(AddCheck add, FoundationContent content)
        {
            var inv = new Inventory(12, content);
            inv.Add("wood", 8);
            var crafting = new CraftingSystem(content, inv);
            var recipe = content.Recipes.Get("craft_workbench");
            bool crafted = crafting.TryCraft(recipe);
            add("Hand crafting consumes inputs and creates output", crafted && inv.Count("workbench_item") == 1 && inv.Count("wood") == 3);

            var tightInv = new Inventory(1, content);
            tightInv.Add("wood", 5);
            var tightCrafting = new CraftingSystem(content, tightInv);
            bool tightCrafted = tightCrafting.TryCraft(recipe);
            add("Crafting output can use a slot freed by recipe inputs",
                tightCrafted && tightInv.Count("workbench_item") == 1 && tightInv.Count("wood") == 0);

            inv.Add("stone", 3);
            crafting.StationAvailable = st => st == StationType.Workbench;
            var stationRecipe = content.Recipes.Get("craft_stone_block");
            bool stationCrafted = crafting.TryCraft(stationRecipe);
            add("Station-gated recipe crafts when station is available", stationCrafted && inv.Count("stone_block_item") == 1);

            var toolInv = new Inventory(4, content);
            toolInv.Add("wood_axe", 1);
            var axe = content.Items.Get("wood_axe");
            var stack = toolInv.GetSlot(0);
            bool damaged = toolInv.DamageSlot(0, 1);
            add("Tool durability initializes and damages selected slot",
                axe != null && axe.maxDurability > 1 && stack.durability == axe.maxDurability &&
                !damaged && toolInv.GetSlot(0).durability == axe.maxDurability - 1,
                axe != null ? $"{toolInv.GetSlot(0).durability}/{axe.maxDurability}" : "missing axe");

            var tavernRecipe = content.Recipes.Get("craft_tavern_building");
            var tavernItem = content.Items.Get("tavern_building_item");
            var tavernPlaceable = content.Placeables.Get("tavern_building");
            add("Craftable tavern building enters tavern instance",
                tavernRecipe != null && tavernItem != null && tavernPlaceable != null &&
                tavernPlaceable.interaction == InteractionKind.Entrance &&
                tavernPlaceable.destinationId == "tavern_common_room" &&
                tavernPlaceable.FootprintWidth == 3 &&
                tavernPlaceable.FootprintHeight == 3,
                tavernPlaceable != null ? $"{tavernPlaceable.FootprintWidth}x{tavernPlaceable.FootprintHeight}" : "missing tavern");

            var tavernPlotRecipe = content.Recipes.Get("craft_tavern_plot");
            var tavernPlotItem = content.Items.Get("tavern_plot_item");
            var tavernPlot = content.Placeables.Get("tavern_plot");
            bool tavernPlotOk = tavernPlotRecipe != null &&
                tavernPlotItem != null &&
                tavernPlot != null &&
                tavernPlot.interaction == InteractionKind.Construction &&
                tavernPlot.constructionResultPlaceableId == "tavern_building" &&
                tavernPlot.FootprintWidth == 3 &&
                tavernPlot.FootprintHeight == 3 &&
                tavernPlot.constructionCost != null &&
                tavernPlot.constructionCost.Length >= 3;
            add("Tavern build plot upgrades into tavern building",
                tavernPlotOk,
                tavernPlot != null ? $"{tavernPlot.constructionResultPlaceableId} {tavernPlot.FootprintWidth}x{tavernPlot.FootprintHeight}" : "missing plot");

            var fireplaceRecipe = content.Recipes.Get("craft_fireplace");
            var fireplaceItem = content.Items.Get("fireplace_item");
            var fireplace = content.Placeables.Get("fireplace");
            add("Craftable fireplace uses animated fire prop",
                fireplaceRecipe != null &&
                fireplaceItem != null &&
                fireplace != null &&
                fireplace.emitsLight &&
                fireplace.blocksMovement &&
                FoundationPlaceableSpriteResolver.Resolve("fireplace") != null,
                fireplace != null ? $"light {fireplace.lightRadius:0.0}" : "missing fireplace");

            var libraryPlotRecipe = content.Recipes.Get("craft_library_plot");
            var libraryPlotItem = content.Items.Get("library_plot_item");
            var libraryPlot = content.Placeables.Get("library_plot");
            var libraryPlaceable = content.Placeables.Get("library_building");
            bool libraryPlotOk = libraryPlotRecipe != null &&
                libraryPlotItem != null &&
                libraryPlot != null &&
                libraryPlaceable != null &&
                libraryPlot.interaction == InteractionKind.Construction &&
                libraryPlot.constructionResultPlaceableId == "library_building" &&
                libraryPlaceable.interaction == InteractionKind.Entrance &&
                libraryPlaceable.destinationId == "library_archive" &&
                libraryPlot.constructionCost != null &&
                libraryPlot.constructionCost.Length >= 3;
            add("Library build plot upgrades into enterable library",
                libraryPlotOk,
                libraryPlaceable != null ? libraryPlaceable.destinationId : "missing library");
        }

        static void ValidateDungeonGeneration(AddCheck add)
        {
            var content = FoundationContent.BuildDefault();
            var origin = new Vector2Int(61000, 61000);
            var entrance = new Vector2Int(18, -9);
            var a = FoundationDungeonGenerator.Generate(content, "rootcellar_starter",
                "Validation Rootcellar", 1337, entrance, origin, 3);
            var b = FoundationDungeonGenerator.Generate(content, "rootcellar_starter",
                "Validation Rootcellar", 1337, entrance, origin, 3);

            var portalFrames = FoundationDungeonSpriteResolver.PortalFrames();
            add("Dungeon animated portal art resolves",
                FoundationDungeonSpriteResolver.Portal() != null &&
                portalFrames.Length >= 6,
                $"Resources/FoundationPortals/Dimensional_Portal frames {portalFrames.Length}");
            add("Kenney dungeon floor art resolves",
                FoundationDungeonSpriteResolver.Decoration("stoneTile_S") != null,
                "Resources/FoundationDungeon/Kenney/stoneTile_S");
            add("PixelArt dungeon floor kit resolves",
                PixelArtDungeonFloorAssetsPresent() &&
                TileSpriteResolver.Resolve(content.Blocks.Get("dungeon_floor_1")) != null,
                "Resources/Tiles/dungeon_floor_1..5");
            add("Dungeon generation is deterministic",
                a.layoutSeed == b.layoutSeed &&
                a.cells.Length == b.cells.Length &&
                a.roomMarkers.Length == b.roomMarkers.Length &&
                a.spawnCell == b.spawnCell &&
                a.exitCell == b.exitCell,
                $"seed {a.layoutSeed}");
            int dungeonWidth = a.renderMax.x - a.renderMin.x + 1;
            int dungeonHeight = a.renderMax.y - a.renderMin.y + 1;
            int spawnExitDistance = Mathf.Abs(a.exitCell.x - a.spawnCell.x) + Mathf.Abs(a.exitCell.y - a.spawnCell.y);
            add("Dungeon render bounds support movement-scale combat",
                dungeonWidth >= 60 &&
                dungeonHeight >= 60,
                $"{a.renderMin}->{a.renderMax}");
            add("Dungeon walkable footprint supports spell movement",
                a.renderCells != null &&
                a.renderCells.Length >= 900,
                $"{a.renderCells?.Length ?? 0} walkable render cells");
            add("Dungeon generation uses PixelArt floor block family",
                HasDungeonFloorCell(a),
                "walkable cells use dungeon_floor_* block ids");
            add("Dungeon spawn-to-exit path spans a real delve",
                spawnExitDistance >= 32,
                $"distance {spawnExitDistance}");
            add("Dungeon generator emits map-readable room roles",
                a.roomMarkers != null &&
                a.roomMarkers.Length >= 8 &&
                HasDungeonMarker(a, FoundationDungeonRoomKind.Spawn) &&
                HasDungeonMarker(a, FoundationDungeonRoomKind.Exit) &&
                HasDungeonMarker(a, FoundationDungeonRoomKind.Combat) &&
                HasDungeonMarker(a, FoundationDungeonRoomKind.Junction),
                $"room markers {a.roomMarkers?.Length ?? 0}");
            add("Dungeon spawn and exit are distinct walkable cells",
                a.spawnCell != a.exitCell &&
                IsDungeonCellWalkable(a, a.spawnCell) &&
                IsDungeonCellWalkable(a, a.exitCell),
                $"spawn {a.spawnCell}, exit {a.exitCell}");
            add("Dungeon boundary blocks escape",
                IsDungeonCellBlocked(a, new Vector2Int(a.renderMin.x - 1, a.renderMin.y - 1)) &&
                IsDungeonCellBlocked(a, new Vector2Int(a.renderMax.x + 1, a.renderMax.y + 1)),
                "outer ring solid");
            add("Dungeon focuses on generated tiles with portal exit only",
                a.mobs.Length >= 6 &&
                a.decorations.Length == 1 &&
                a.decorations[0].spriteKey == FoundationDungeonDecoration.ExitPortalSpriteKey &&
                a.decorations[0].x == a.exitCell.x &&
                a.decorations[0].y == a.exitCell.y,
                $"mobs {a.mobs.Length}, decor {a.decorations.Length}");

            string saveSource = File.Exists("Assets/Scripts/IsoCoreFoundation/Core/FoundationSaveData.cs")
                ? File.ReadAllText("Assets/Scripts/IsoCoreFoundation/Core/FoundationSaveData.cs")
                : "";
            string portalSource = File.Exists("Assets/Scripts/IsoCoreFoundation/Dungeons/FoundationDungeonPortalSystem.cs")
                ? File.ReadAllText("Assets/Scripts/IsoCoreFoundation/Dungeons/FoundationDungeonPortalSystem.cs")
                : "";
            string portalInstanceSource = File.Exists("Assets/Scripts/IsoCoreFoundation/Dungeons/FoundationDungeonPortalInstance.cs")
                ? File.ReadAllText("Assets/Scripts/IsoCoreFoundation/Dungeons/FoundationDungeonPortalInstance.cs")
                : "";
            add("Dungeon save layer tracks reward and completion state",
                saveSource.Contains("FoundationSavedDungeon") &&
                saveSource.Contains("FoundationSavedDungeonHistory") &&
                saveSource.Contains("rewardOpened") &&
                saveSource.Contains("completed") &&
                portalSource.Contains("OpenReward") &&
                portalSource.Contains("CompleteAndExit") &&
                portalSource.Contains("CaptureHistory") &&
                portalSource.Contains("RestoreHistory") &&
                portalSource.Contains("ApplyDungeonResult") &&
                portalInstanceSource.Contains("SetHistoryState"),
                "FoundationSavedDungeon + persistent portal history");
        }

        static bool IsDungeonCellWalkable(FoundationDungeonBuild build, Vector2Int cell)
        {
            foreach (var c in build.cells)
                if (c.x == cell.x && c.y == cell.y)
                    return !c.solidBlock && !c.water && !c.occupantBlocks && !c.nodeBlocks;
            return false;
        }

        static bool HasDungeonFloorCell(FoundationDungeonBuild build)
        {
            if (build?.cells == null)
                return false;

            foreach (var c in build.cells)
            {
                if (!c.solidBlock &&
                    !string.IsNullOrEmpty(c.surfaceBlockId) &&
                    c.surfaceBlockId.StartsWith("dungeon_floor_", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        static bool PixelArtDungeonFloorAssetsPresent()
        {
            for (int i = 1; i <= 5; i++)
            {
                string path = $"Assets/Resources/Tiles/dungeon_floor_{i}.png";
                if (!File.Exists(path))
                    return false;
            }

            return true;
        }

        static bool IsDungeonCellBlocked(FoundationDungeonBuild build, Vector2Int cell)
        {
            foreach (var c in build.cells)
                if (c.x == cell.x && c.y == cell.y)
                    return c.solidBlock || c.water || c.occupantBlocks || c.nodeBlocks;
            return false;
        }

        static bool HasDungeonMarker(FoundationDungeonBuild build, FoundationDungeonRoomKind kind)
        {
            if (build?.roomMarkers == null)
                return false;

            foreach (var marker in build.roomMarkers)
                if (marker.kind == kind)
                    return true;
            return false;
        }

        static void ValidateFarmingData(AddCheck add, FoundationContent content)
        {
            var seed = content.Items.Get("carrot_seeds");
            var crop = seed != null ? content.Crops.Get(seed.plantCropId) : null;
            add("Seed item resolves to crop definition", seed != null && crop != null, seed != null ? seed.plantCropId : "missing seed");
            add("Crop has harvest outputs", crop != null && crop.harvest != null && crop.harvest.Length > 0);
        }

        static void ValidateProgressionContracts(AddCheck add, FoundationContent content)
        {
            var progression = new FoundationProgression(content);
            int beforeXp = progression.Stats.Experience;
            int rewardEvents = 0;
            progression.RewardUnlocked += reward =>
            {
                if (reward.type == FoundationRewardType.Recipe && reward.id == "craft_campfire")
                    rewardEvents++;
            };
            bool wood = progression.AdvanceQuestObjective("first_flame_first_field", "gather_wood", 5);
            bool bench = progression.AdvanceQuestObjective("first_flame_first_field", "craft_workbench");
            bool soil = progression.AdvanceQuestObjective("first_flame_first_field", "till_soil");
            add("Starter quest completes and grants XP",
                wood && bench && soil &&
                progression.IsQuestCompleted("first_flame_first_field") &&
                progression.Stats.Experience > beforeXp,
                $"xp {beforeXp}->{progression.Stats.Experience}");
            add("Quest completion emits reward unlock event",
                rewardEvents == 1 &&
                progression.HasUnlockedReward(FoundationRewardType.Recipe, "craft_campfire"),
                $"events {rewardEvents}");

            var read = progression.CaptureReadState();
            var firstQuestRead = progression.CaptureQuestReadState("first_flame_first_field");
            add("Progression read state exposes Calling, skills, quests, and rewards",
                read.calling.hasCalling &&
                read.skills != null && read.skills.Length >= content.Skills.Count &&
                read.quests != null && read.quests.Length >= 1 &&
                read.unlockedRewards != null && read.unlockedRewards.Length >= 1,
                $"skills {read.skills?.Length ?? 0}, quests {read.quests?.Length ?? 0}, rewards {read.unlockedRewards?.Length ?? 0}");
            add("Quest read state exposes objective progress",
                firstQuestRead.completed &&
                firstQuestRead.objectives != null &&
                firstQuestRead.objectives.Length == content.Quests.Get("first_flame_first_field").objectives.Length &&
                firstQuestRead.progress01 >= 0.99f,
                firstQuestRead.progress01.ToString("0.00"));
            var pinnedProgression = new FoundationProgression(content);
            pinnedProgression.AdvanceQuestObjective("first_flame_first_field", "gather_wood", 2);
            var pinnedService = new FoundationQoLService();
            pinnedService.Init(content, pinnedProgression, new Inventory(6, content));
            bool pinned = pinnedService.PinGoal(FoundationPinnedGoalType.Quest, "first_flame_first_field");
            var pinnedRead = pinnedService.CaptureReadState();
            var pinnedQuest = pinnedRead.pinnedGoals != null && pinnedRead.pinnedGoals.Length > 0
                ? pinnedRead.pinnedGoals[0]
                : default;
            add("QoL pinned quest goal resolves live progress",
                pinned &&
                pinnedQuest.available &&
                pinnedQuest.type == FoundationPinnedGoalType.Quest &&
                pinnedQuest.targetId == "first_flame_first_field" &&
                pinnedQuest.progress01 > 0.39f &&
                pinnedQuest.progress01 < 0.41f &&
                !pinnedQuest.completed,
                $"{pinnedQuest.displayName} {pinnedQuest.progress01:0.00}");
            bool fallbackPinned = pinnedService.PinGoal(FoundationPinnedGoalType.Quest, "missing_quest", 1);
            var fallbackRead = pinnedService.CaptureReadState();
            var fallbackGoal = fallbackRead.pinnedGoals != null && fallbackRead.pinnedGoals.Length > 1
                ? fallbackRead.pinnedGoals[1]
                : default;
            add("QoL pinned goal missing content is safe",
                fallbackPinned &&
                !fallbackGoal.available &&
                fallbackGoal.detail == "Quest unavailable",
                fallbackGoal.detail);

            var focusedProgression = new FoundationProgression(content);
            focusedProgression.AddActivityXp(FoundationProgressionActivity.Harvest, 10, "woodcraft");
            focusedProgression.AddActivityXp(FoundationProgressionActivity.Creature, 8, "creaturecraft");
            add("Focused activity XP advances targeted skills only",
                focusedProgression.GetSkillXp("woodcraft") == 10 &&
                focusedProgression.GetSkillXp("foraging") == 0 &&
                focusedProgression.GetSkillXp("mining") == 0 &&
                focusedProgression.GetSkillXp("creaturecraft") == 8 &&
                focusedProgression.GetSkillXp("warding") == 0,
                $"woodcraft {focusedProgression.GetSkillXp("woodcraft")}, creaturecraft {focusedProgression.GetSkillXp("creaturecraft")}");

            var trialProgression = new FoundationProgression(content);
            int evidenceEvents = 0;
            int titleEvents = 0;
            int affinityEvents = 0;
            int feedEvents = 0;
            trialProgression.TrialEvidenceAdded += (_, __) => evidenceEvents++;
            trialProgression.TitleAcquired += _ => titleEvents++;
            trialProgression.AffinityAwakened += _ => affinityEvents++;
            trialProgression.SystemFeed.Queued += _ => feedEvents++;
            bool evidenceRecorded = trialProgression.RecordEvidence("harvest_wood", 5, "validator_tree");
            var trialRead = trialProgression.CaptureReadState();
            add("Trial evidence records Action -> Evidence -> XP/Title/Affinity -> System feed",
                evidenceRecorded &&
                evidenceEvents == 1 &&
                feedEvents >= 1 &&
                trialProgression.GetTrialScore(TrialEvidenceCategory.Gathering) >= 10 &&
                trialProgression.GetXpChannelValue("woodcraft") >= 15 &&
                trialProgression.HasTitle("first_night_survivor") &&
                trialProgression.GetAffinityScore("root") >= 5 &&
                trialRead.trial.totalScore > 0 &&
                trialRead.trial.evidenceLog != null &&
                trialRead.trial.evidenceLog.Length == 1 &&
                trialRead.trial.evidenceLog[0].sequence == 1 &&
                trialRead.trial.evidenceLog[0].eventId == "harvest_wood" &&
                trialRead.trial.evidenceLog[0].amount == 5 &&
                trialRead.trial.evidenceLog[0].scoreDeltas != null &&
                trialRead.trial.evidenceLog[0].scoreDeltas.Length > 0 &&
                trialRead.trial.trialDay == 1 &&
                trialRead.trial.trialDurationDays == 7 &&
                trialRead.systemMessages != null && trialRead.systemMessages.Length >= 1,
                $"score {trialProgression.TotalTrialScore}, title events {titleEvents}, affinity events {affinityEvents}");
            bool affinityAwakened = trialProgression.RecordEvidence("craft_campfire", 2, "validator_campfire");
            add("Trial evidence can awaken affinities",
                affinityAwakened &&
                affinityEvents >= 1 &&
                trialProgression.GetAffinityScore("hearth") >= 11,
                $"hearth {trialProgression.GetAffinityScore("hearth")}, events {affinityEvents}");
            bool completedTrial = trialProgression.SetTrialDay(7);
            var completedRead = trialProgression.CaptureReadState();
            add("Seven-day trial completion snapshots grade and offers",
                completedTrial &&
                completedRead.trial.completed &&
                completedRead.trial.trialDay == 7 &&
                completedRead.trial.gradeSnapshot == trialProgression.GradeForecast &&
                completedRead.trial.classOffers != null && completedRead.trial.classOffers.Length > 0 &&
                completedRead.trial.professionOffers != null && completedRead.trial.professionOffers.Length > 0 &&
                !string.IsNullOrWhiteSpace(completedRead.trial.selectedClassId) &&
                !string.IsNullOrWhiteSpace(completedRead.trial.selectedProfessionId),
                $"grade {completedRead.trial.gradeSnapshot}, class {completedRead.trial.selectedClassId}, profession {completedRead.trial.selectedProfessionId}");

            var dungeonProgression = new FoundationProgression(content);
            var result = content.DungeonResults.Get("rootcellar_first_return");
            bool resultApplied = dungeonProgression.ApplyDungeonResult(result, 2);
            add("Dungeon result applies XP/title/affinity/System feedback once per call",
                resultApplied &&
                dungeonProgression.GetXpChannelValue("rootcellar_clearance") >= 40 &&
                dungeonProgression.GetXpChannelValue("adventurer_rank") >= 16 &&
                dungeonProgression.GetTitleProgress("returned_for_them") >= 2 &&
                dungeonProgression.GetAffinityScore("root") >= 4 &&
                dungeonProgression.SystemFeed.Messages.Count >= 1,
                $"clearance {dungeonProgression.GetXpChannelValue("rootcellar_clearance")}, feed {dungeonProgression.SystemFeed.Messages.Count}");

            var inv = new Inventory(12, content);
            inv.Add("wood", 5);
            var crafting = new CraftingSystem(content, inv);
            var hookGo = new GameObject("ProgressionHooks_IntegratedValidation");
            var hookedProgression = new FoundationProgression(content);
            var hooks = hookGo.AddComponent<FoundationProgressionHooks>();
            hooks.Init(hookedProgression, null, crafting, null, null, null);

            bool crafted = crafting.TryCraft(content.Recipes.Get("craft_workbench"));
            hookedProgression.SkillXp.TryGetValue("crafting", out int craftingXp);
            add("Crafting event advances quest and skill XP",
                crafted &&
                hookedProgression.GetObjectiveProgress("first_flame_first_field", "craft_workbench") >= 1 &&
                craftingXp > 0,
                $"crafting xp {craftingXp}");
            add("Crafting hook records trial evidence and System message",
                hookedProgression.GetTrialScore(TrialEvidenceCategory.Crafting) >= 3 &&
                hookedProgression.GetXpChannelValue("crafting") >= 4 &&
                hookedProgression.SystemFeed.Messages.Count >= 1,
                $"craft score {hookedProgression.GetTrialScore(TrialEvidenceCategory.Crafting)}, messages {hookedProgression.SystemFeed.Messages.Count}");

            UnityEngine.Object.DestroyImmediate(hookGo);
        }

        static void WriteReport(List<Check> checks)
        {
            int passed = 0;
            foreach (var check in checks)
                if (check.pass) passed++;

            var sb = new StringBuilder();
            sb.AppendLine("# Integrated Slice Validation");
            sb.AppendLine();
            sb.AppendLine($"> Generated by `{nameof(FoundationIntegratedSliceValidator)}.{nameof(Run)}`.");
            sb.AppendLine($"> Result: **{passed}/{checks.Count}** automated checks passed.");
            sb.AppendLine($"> Test seed: `{TestSeed}` -> `{FoundationBootstrap.SeedStringToInt(TestSeed)}`.");
            sb.AppendLine();
            sb.AppendLine("## Checks");
            sb.AppendLine();
            sb.AppendLine("| Check | Result | Detail |");
            sb.AppendLine("|---|---|---|");
            foreach (var check in checks)
                sb.AppendLine($"| {Escape(check.name)} | {(check.pass ? "PASS" : "FAIL")} | {Escape(check.detail)} |");
            sb.AppendLine();
            sb.AppendLine("## Scope");
            sb.AppendLine();
            sb.AppendLine("- Verifies MenuScene and Build Settings point to the canonical Foundation scene.");
            sb.AppendLine("- Verifies `WelcomeScreenManager` calls `FoundationBootstrap.ConfigureLaunch(...)` before loading `IsoCoreFoundation`.");
            sb.AppendLine("- Verifies the string seed is deterministically converted and applied before Foundation world construction.");
            sb.AppendLine("- Exercises the underlying contracts for terrain determinism, blocking, placement, harvesting, crafting, farming data, runtime graph creation, and mob spawning.");
            sb.AppendLine("- Does not replace a human feel pass for keyboard/mouse ergonomics or visual polish.");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
        }

        static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
