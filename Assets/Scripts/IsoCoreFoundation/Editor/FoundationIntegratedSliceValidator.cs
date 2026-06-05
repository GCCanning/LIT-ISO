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
            var checks = new List<Check>();
            AddCheck add = (name, pass, detail) =>
                checks.Add(new Check { name = name, pass = pass, detail = detail });

            try
            {
                RunChecks(add);
            }
            catch (Exception ex)
            {
                add("Validator completed without exception", false, ex.ToString());
            }

            WriteReport(checks);

            int failed = 0;
            foreach (var check in checks)
                if (!check.pass) failed++;

            string summary = $"[FoundationIntegratedSliceValidator] {checks.Count - failed}/{checks.Count} checks passed. Report: {ReportPath}";
            if (failed > 0)
                throw new Exception(summary);

            Debug.Log(summary);
        }

        static void RunChecks(AddCheck add)
        {
            FoundationBootstrap.ClearLaunchOptions();

            ValidateBuildSettings(add);
            ValidateMenuScene(add);
            ValidateMenuSourceWiring(add);
            ValidateLaunchSeedPropagation(add);
            ValidateWorldContracts(add);

            string foundationSummary = FoundationValidator.Validate(false);
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
            FoundationBootstrap.ConfigureLaunch(TestWorldName, TestSeed, 2);

            var go = new GameObject("FoundationBootstrap_IntegratedValidation");
            var boot = go.AddComponent<FoundationBootstrap>();
            typeof(FoundationBootstrap)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(boot, null);

            add("ConfigureLaunch seed applied before Foundation world build",
                boot.config != null && boot.config.seed == expected,
                $"expected {expected}, got {(boot.config != null ? boot.config.seed.ToString() : "null config")}");
            add("ConfigureLaunch world name applied",
                boot.ActiveWorldName == TestWorldName,
                boot.ActiveWorldName);
            add("ConfigureLaunch difficulty applied",
                boot.ActiveDifficulty == 2,
                boot.ActiveDifficulty.ToString());

            add("Foundation runtime graph creates Player", go.transform.Find("Player") != null);
            add("Foundation runtime graph creates WorldController", go.transform.Find("WorldController") != null);
            add("Foundation runtime graph creates PlacementSystem", go.transform.Find("PlacementSystem") != null);
            add("Foundation runtime graph creates FarmingSystem", go.transform.Find("FarmingSystem") != null);
            add("Foundation runtime graph creates MobSpawner", go.transform.Find("MobSpawner") != null);
            add("Foundation runtime graph creates HUD and input router",
                go.GetComponent<FoundationHUD>() != null && go.GetComponent<PlayerInteraction>() != null);

            var spawner = go.GetComponentInChildren<MobSpawner>();
            bool spawned = TryForceMobSpawn(spawner);
            add("MobSpawner can spawn at least one mob", spawned,
                spawner != null ? $"count {spawner.Count}" : "spawner missing");

            UnityEngine.Object.DestroyImmediate(go);
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
