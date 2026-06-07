using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
            ValidateLaunchSeedPropagation(add);
            ValidateWorldContracts(add);

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
            add("Foundation runtime graph creates HUD and input router",
                go.GetComponent<FoundationHUD>() != null && go.GetComponent<PlayerInteraction>() != null);
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
                boot.Farming != null && boot.MobSpawner != null && boot.DayNight != null &&
                boot.Crafting != null && boot.Hud != null);
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
            add("FoundationContent includes LitRPG bible seed content",
                boot.Content.Callings.Count >= 7 && boot.Content.Skills.Count >= 12 && boot.Content.Quests.Count >= 5,
                $"Callings:{boot.Content.Callings.Count} Skills:{boot.Content.Skills.Count} Quests:{boot.Content.Quests.Count}");
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
            headlessBoot.createImguiHud = false;
            typeof(FoundationBootstrap)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(headlessBoot, null);
            add("FoundationBootstrap can skip temporary IMGUI HUD",
                headlessBoot.Hud == null && headlessGo.GetComponent<FoundationHUD>() == null &&
                headlessGo.GetComponent<PlayerInteraction>() != null);
            add("FoundationBootstrap exposes UI binding handles without IMGUI HUD",
                headlessBoot.Inventory != null && headlessBoot.Hotbar != null &&
                headlessBoot.Content != null && headlessBoot.World != null &&
                headlessBoot.Progression != null && headlessBoot.Stats != null &&
                headlessBoot.ProgressionHooks != null);
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
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path)) File.Delete(path);

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

                bool saved = boot.Save(path);
                bool metadataOk = FoundationBootstrap.TryReadSaveMetadata(path, out var metadata, out string metadataError);

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
                add("Save/load preserves player cell",
                    loaded.Player.CurrentCell == new Vector2Int(2, -3),
                    loaded.Player.CurrentCell.ToString());

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

            Vector2Int placeableCell = FindBuildableCell(world, buildCell, blockCell);
            var workbench = content.Placeables.Get("workbench");
            bool placedOccupant = placeableCell != InvalidCell && world.TryPlaceOccupant(placeableCell.x, placeableCell.y, workbench.id, workbench.blocksMovement);
            add("Placeable occupancy writes to world", placedOccupant, placeableCell.ToString());
            add("Blocking placeable blocks movement query",
                placedOccupant && world.IsBlocked(placeableCell.x, placeableCell.y));
            add("Placeable occupancy clears cleanly",
                placedOccupant && world.ClearOccupant(placeableCell.x, placeableCell.y) && world.IsWalkable(placeableCell.x, placeableCell.y));

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
        }

        static void ValidateCrafting(AddCheck add, FoundationContent content)
        {
            var inv = new Inventory(12, content);
            inv.Add("wood", 8);
            var crafting = new CraftingSystem(content, inv);
            var recipe = content.Recipes.Get("craft_workbench");
            bool crafted = crafting.TryCraft(recipe);
            add("Hand crafting consumes inputs and creates output", crafted && inv.Count("workbench_item") == 1 && inv.Count("wood") == 3);

            inv.Add("stone", 3);
            crafting.StationAvailable = st => st == StationType.Workbench;
            var stationRecipe = content.Recipes.Get("craft_stone_block");
            bool stationCrafted = crafting.TryCraft(stationRecipe);
            add("Station-gated recipe crafts when station is available", stationCrafted && inv.Count("stone_block_item") == 1);
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
