using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Editor-side validation (no play mode needed): content databases + cross-refs,
    /// plus scene/bootstrap/camera presence. Writes 06_Validation_Report.md.
    /// </summary>
    public static class FoundationValidator
    {
        struct Check { public string name; public bool pass; public string detail; }

        public static string Validate(bool showDialog)
        {
            var checks = new List<Check>();
            void Add(string n, bool p, string d = "") => checks.Add(new Check { name = n, pass = p, detail = d });

            var c = FoundationContent.BuildDefault();

            // ---- Databases exist / non-empty ----
            Add("Block database", c.Blocks.Count > 0, $"{c.Blocks.Count} blocks");
            Add("Block group database", c.BlockGroups.Count > 0, $"{c.BlockGroups.Count} groups");
            Add("Item database", c.Items.Count > 0, $"{c.Items.Count} items");
            Add("Placeable database", c.Placeables.Count > 0, $"{c.Placeables.Count} placeables");
            Add("Recipe database", c.Recipes.Count > 0, $"{c.Recipes.Count} recipes");
            Add("Biome database (multiple biomes)", c.Biomes.Count >= 2, $"{c.Biomes.Count} biomes");
            Add("Harvestable resource nodes", c.Nodes.Count >= 1, $"{c.Nodes.Count} node types");
            Add("Mob database", c.Mobs.Count >= 1, $"{c.Mobs.Count} mobs");
            Add("Crop database", c.Crops.Count >= 1, $"{c.Crops.Count} crops");

            // ---- Block groups have variants ----
            bool groupsOk = true; string groupDetail = "";
            foreach (var g in c.BlockGroups.All)
                if (g.variants == null || g.variants.Count == 0 || g.variants.Exists(v => v == null))
                { groupsOk = false; groupDetail += $"{g.id} "; }
            Add("Block groups have valid variants", groupsOk, groupsOk ? "all populated" : "empty/null: " + groupDetail);

            // ---- Biome references ----
            bool biomeOk = true; string biomeDetail = "";
            foreach (var b in c.Biomes.All)
            {
                if (b.surfaceGroup == null || b.surfaceGroup.variants.Count == 0)
                { biomeOk = false; biomeDetail += $"{b.id}:group "; }
            }
            Add("Biome surface groups valid", biomeOk, biomeOk ? "all biomes map to a non-empty group" : biomeDetail);

            // ---- Item placement references ----
            bool itemRefOk = true; string itemRefDetail = "";
            int blockItems = 0, placeItems = 0;
            foreach (var it in c.Items.All)
            {
                if (it.PlacesBlock) { blockItems++; if (!c.Blocks.Has(it.placeBlockId)) { itemRefOk = false; itemRefDetail += $"{it.id}->{it.placeBlockId} "; } }
                if (it.PlacesPlaceable) { placeItems++; if (!c.Placeables.Has(it.placeableId)) { itemRefOk = false; itemRefDetail += $"{it.id}->{it.placeableId} "; } }
            }
            Add("Item placement references valid", itemRefOk, itemRefOk ? "all resolve" : itemRefDetail);
            Add("At least one block-placing item", blockItems >= 1, $"{blockItems} block items");
            Add("At least one placeable-placing item", placeItems >= 1, $"{placeItems} placeable items");

            // ---- Placeable required-item references ----
            bool placeReqOk = true; string placeReqDetail = "";
            foreach (var p in c.Placeables.All)
                if (!string.IsNullOrEmpty(p.requiredItemId) && !c.Items.Has(p.requiredItemId))
                { placeReqOk = false; placeReqDetail += $"{p.id}->{p.requiredItemId} "; }
            Add("Placeable required-item references valid", placeReqOk, placeReqOk ? "all resolve" : placeReqDetail);

            // ---- Seed / crop references ----
            bool seedOk = true; string seedDetail = "";
            foreach (var it in c.Items.All)
                if (it.IsSeed && !c.Crops.Has(it.plantCropId)) { seedOk = false; seedDetail += $"{it.id}->{it.plantCropId} "; }
            Add("Seed plant references valid", seedOk, seedOk ? "all resolve" : seedDetail);

            bool cropOk = true; string cropDetail = "";
            foreach (var cr in c.Crops.All)
                if (cr.harvest != null)
                    foreach (var d in cr.harvest)
                        if (!c.Items.Has(d.itemId)) { cropOk = false; cropDetail += $"{cr.id}:{d.itemId} "; }
            Add("Crop harvest references valid", cropOk, cropOk ? "all resolve" : cropDetail);

            // ---- Recipe references ----
            bool recipeOk = true; string recipeDetail = "";
            foreach (var r in c.Recipes.All)
            {
                if (r.inputs != null)
                    foreach (var i in r.inputs)
                        if (!c.Items.Has(i.itemId)) { recipeOk = false; recipeDetail += $"{r.id}:in:{i.itemId} "; }
                if (r.outputs != null)
                    foreach (var o in r.outputs)
                        if (!c.Items.Has(o.itemId)) { recipeOk = false; recipeDetail += $"{r.id}:out:{o.itemId} "; }
            }
            Add("Recipe item references valid", recipeOk, recipeOk ? "all resolve" : recipeDetail);

            // ---- Crafting station exists for station-bound recipes ----
            bool stationOk = true;
            foreach (var r in c.Recipes.All)
                if (r.station != StationType.None && r.station != StationType.Hand)
                {
                    bool found = false;
                    foreach (var p in c.Placeables.All) if (p.stationType == r.station) { found = true; break; }
                    if (!found) { stationOk = false; }
                }
            Add("Station-bound recipes have a placeable station", stationOk);

            // ---- Starter items reference valid items ----
            bool starterOk = true; string starterDetail = "";
            var cfg = new FoundationConfig();
            foreach (var s in cfg.starterItems)
                if (!c.Items.Has(s.itemId)) { starterOk = false; starterDetail += s.itemId + " "; }
            Add("Starter inventory items valid", starterOk, starterOk ? "all resolve" : starterDetail);

            // ---- Scene / bootstrap / camera ----
            string fullScene = Path.Combine(FoundationPaths.ProjectRoot, FoundationPaths.ScenePath);
            bool sceneExists = File.Exists(fullScene);
            Add("Foundation scene exists", sceneExists, FoundationPaths.ScenePath);

            bool hasBoot = false, hasCam = false;
            if (sceneExists)
            {
                var opened = EditorSceneManager.OpenScene(FoundationPaths.ScenePath, OpenSceneMode.Additive);
                try
                {
                    foreach (var go in opened.GetRootGameObjects())
                    {
                        if (go.GetComponentInChildren<FoundationBootstrap>(true)) hasBoot = true;
                        if (go.GetComponentInChildren<Camera>(true)) hasCam = true;
                    }
                }
                finally { EditorSceneManager.CloseScene(opened, true); }
            }
            Add("Scene has FoundationBootstrap (player+world+inventory at runtime)", hasBoot);
            Add("Scene has a Camera", hasCam);

            // ---- Reference inventory present (research, not wired) ----
            Add("ISO-CORE reference inventory present (research-only)",
                File.Exists(FoundationPaths.ReferenceInventoryJson),
                "Docs/IsoCoreFoundation/iso_core_reference_inventory.json");

            // ---- Report ----
            int passed = 0; foreach (var ch in checks) if (ch.pass) passed++;
            bool allPass = passed == checks.Count;
            WriteReport(checks, passed, c);

            string summary = $"[ISO-Core] Validation: {passed}/{checks.Count} checks passed " +
                             $"({(allPass ? "ALL PASS" : "see report")}). " +
                             "Report: Docs/IsoCoreFoundation/06_Validation_Report.md";
            if (showDialog) EditorUtility.DisplayDialog("ISO-Core Foundation - Validation",
                $"{passed}/{checks.Count} checks passed.\n\n" +
                (allPass ? "All editor-side checks passed." : "Some checks failed - see 06_Validation_Report.md."), "OK");
            Debug.Log(summary);
            return summary;
        }

        static void WriteReport(List<Check> checks, int passed, FoundationContent c)
        {
            FoundationPaths.EnsureDocsDir();
            var sb = new StringBuilder();
            sb.AppendLine("# ISO-Core Foundation - 06: Validation Report");
            sb.AppendLine();
            sb.AppendLine($"> Generated by `Tools/LIT-ISO/ISO-Core Foundation/Validate Foundation`.");
            sb.AppendLine($"> Result: **{passed}/{checks.Count}** editor-side checks passed.");
            sb.AppendLine();
            sb.AppendLine("## Automated checks");
            sb.AppendLine();
            sb.AppendLine("| Check | Result | Detail |");
            sb.AppendLine("|---|---|---|");
            foreach (var ch in checks)
                sb.AppendLine($"| {ch.name} | {(ch.pass ? "PASS" : "FAIL")} | {ch.detail} |");
            sb.AppendLine();
            sb.AppendLine("## Content summary");
            sb.AppendLine();
            sb.AppendLine($"- Blocks: {c.Blocks.Count}, Block groups: {c.BlockGroups.Count}");
            sb.AppendLine($"- Biomes: {c.Biomes.Count}, Items: {c.Items.Count}, Placeables: {c.Placeables.Count}");
            sb.AppendLine($"- Recipes: {c.Recipes.Count}, Resource nodes: {c.Nodes.Count}, Mobs: {c.Mobs.Count}, Crops: {c.Crops.Count}");
            sb.AppendLine();
            sb.AppendLine("## Manual play-mode checklist");
            sb.AppendLine();
            sb.AppendLine("Open `Assets/Scenes/IsoCoreFoundation.unity` and press Play, then confirm:");
            sb.AppendLine();
            sb.AppendLine("- [ ] Procedural isometric terrain renders around spawn (multiple biome colours visible while walking out).");
            sb.AppendLine("- [ ] Player moves with WASD and **cannot** walk through trees/rocks/water (collision).");
            sb.AppendLine("- [ ] `E` near a tree/rock harvests it; item appears in the hotbar/inventory (`I`).");
            sb.AppendLine("- [ ] Select the hoe and `LMB` a walkable cell to till soil; select seeds and `LMB` tilled soil to plant.");
            sb.AppendLine("- [ ] Crops grow through visible stages; `E` near a mature crop harvests produce without deleting it when inventory is full.");
            sb.AppendLine("- [ ] Select `workbench` (or stone block) on the hotbar (1-9); a green/red ghost shows at the cursor.");
            sb.AppendLine("- [ ] `LMB` places a block / placeable; the item count decrements; `RMB` removes a placeable.");
            sb.AppendLine("- [ ] Placed solid block blocks player movement (collision refresh).");
            sb.AppendLine("- [ ] `E` next to the placed workbench opens the crafting panel; a recipe crafts and consumes inputs.");
            sb.AppendLine("- [ ] At least one mob (deer/slime) wanders nearby and despawns when far away.");
            sb.AppendLine();
            sb.AppendLine("> Note: legacy movement uses the classic `Input` axes. Ensure Project Settings > Player >");
            sb.AppendLine("> Active Input Handling is `Both` or `Input Manager (Old)` for WASD to work.");
            File.WriteAllText(Path.Combine(FoundationPaths.DocsDir, "06_Validation_Report.md"), sb.ToString());
        }
    }
}
