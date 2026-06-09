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
            return Validate(showDialog, true);
        }

        public static string ValidateNoReport()
        {
            return Validate(false, false);
        }

        public static string Validate(bool showDialog, bool writeReport)
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
            Add("LitRPG Calling database", c.Callings.Count >= 7, $"{c.Callings.Count} callings");
            Add("LitRPG Skill database", c.Skills.Count >= 12, $"{c.Skills.Count} skills");
            Add("LitRPG Quest database", c.Quests.Count >= 5, $"{c.Quests.Count} quests");
            Add("LitRPG Trial data spine databases",
                c.EvidenceEvents.Count >= 8 &&
                c.XPChannels.Count >= 8 &&
                c.Titles.Count >= 6 &&
                c.Affinities.Count >= 7 &&
                c.Abilities.Count >= 6 &&
                c.Classes.Count >= 8 &&
                c.Professions.Count >= 8 &&
                c.Dungeons.Count >= 1 &&
                c.DungeonResults.Count >= 1 &&
                c.GuildBoardEntries.Count >= 1 &&
                c.WorldEvents.Count >= 4,
                $"Evidence:{c.EvidenceEvents.Count} XP:{c.XPChannels.Count} Titles:{c.Titles.Count} Affinities:{c.Affinities.Count} Abilities:{c.Abilities.Count} Classes:{c.Classes.Count} Professions:{c.Professions.Count}");

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

            bool footprintOk = true; string footprintDetail = "";
            foreach (var p in c.Placeables.All)
            {
                if (p.FootprintWidth < 1 || p.FootprintHeight < 1)
                {
                    footprintOk = false;
                    footprintDetail += $"{p.id}:{p.FootprintWidth}x{p.FootprintHeight} ";
                }
            }
            Add("Placeable footprints valid", footprintOk, footprintOk ? "all >= 1x1" : footprintDetail);

            bool entranceOk = true; string entranceDetail = "";
            int entranceCount = 0;
            foreach (var p in c.Placeables.All)
            {
                if (p.interaction != InteractionKind.Entrance) continue;
                entranceCount++;
                if (string.IsNullOrWhiteSpace(p.destinationId))
                {
                    entranceOk = false;
                    entranceDetail += $"{p.id}:missing_destination ";
                }
            }
            Add("Entrance placeables have destinations",
                entranceOk && entranceCount >= 1,
                entranceOk ? $"{entranceCount} entrances" : entranceDetail);

            bool constructionOk = true; string constructionDetail = "";
            int constructionCount = 0;
            foreach (var p in c.Placeables.All)
            {
                if (p.interaction != InteractionKind.Construction) continue;
                constructionCount++;

                if (string.IsNullOrWhiteSpace(p.constructionResultPlaceableId) ||
                    !c.Placeables.Has(p.constructionResultPlaceableId))
                {
                    constructionOk = false;
                    constructionDetail += $"{p.id}:result:{p.constructionResultPlaceableId} ";
                }

                if (p.constructionCost == null || p.constructionCost.Length == 0)
                {
                    constructionOk = false;
                    constructionDetail += $"{p.id}:empty_cost ";
                }
                else
                {
                    foreach (var cost in p.constructionCost)
                    {
                        if (string.IsNullOrWhiteSpace(cost.itemId) || cost.count <= 0 || !c.Items.Has(cost.itemId))
                        {
                            constructionOk = false;
                            constructionDetail += $"{p.id}:cost:{cost.itemId} ";
                        }
                    }
                }
            }
            Add("Construction plots resolve result buildings and materials",
                constructionOk && constructionCount >= 1,
                constructionOk ? $"{constructionCount} plots" : constructionDetail);

            bool abilitiesOk = true; string abilityDetail = "";
            bool hasStaminaSkill = false, hasNeutralSpell = false, hasElementSpell = false;
            foreach (var ability in c.Abilities.All)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.id))
                {
                    abilitiesOk = false;
                    abilityDetail += "missing_id ";
                    continue;
                }

                if (ability.kind == FoundationAbilityKind.Skill && ability.resource == FoundationAbilityResource.Stamina)
                    hasStaminaSkill = true;
                if (ability.kind == FoundationAbilityKind.Spell && ability.resource == FoundationAbilityResource.Mana &&
                    ability.element == FoundationAbilityElement.Neutral && string.IsNullOrWhiteSpace(ability.affinityId))
                    hasNeutralSpell = true;
                if (ability.kind == FoundationAbilityKind.Spell && ability.resource == FoundationAbilityResource.Mana &&
                    !string.IsNullOrWhiteSpace(ability.affinityId))
                    hasElementSpell = true;

                if (!string.IsNullOrWhiteSpace(ability.evidenceId) && !c.EvidenceEvents.Has(ability.evidenceId))
                {
                    abilitiesOk = false;
                    abilityDetail += $"{ability.id}:evidence:{ability.evidenceId} ";
                }

                if (!string.IsNullOrWhiteSpace(ability.affinityId) && !c.Affinities.Has(ability.affinityId))
                {
                    abilitiesOk = false;
                    abilityDetail += $"{ability.id}:affinity:{ability.affinityId} ";
                }

                if (ability.skillIds != null)
                {
                    foreach (var skillId in ability.skillIds)
                    {
                        if (string.IsNullOrWhiteSpace(skillId) || c.Skills.Has(skillId)) continue;
                        abilitiesOk = false;
                        abilityDetail += $"{ability.id}:skill:{skillId} ";
                    }
                }
            }
            Add("Ability definitions resolve evidence, skills, and affinities",
                abilitiesOk && hasStaminaSkill && hasNeutralSpell && hasElementSpell,
                abilitiesOk ? $"abilities {c.Abilities.Count}" : abilityDetail);

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

            bool callingOk = true; string callingDetail = "";
            foreach (var calling in c.Callings.All)
            {
                if (calling.starterSkillIds == null || calling.starterSkillIds.Length == 0)
                { callingOk = false; callingDetail += $"{calling.id}:no_skills "; continue; }

                foreach (var skillId in calling.starterSkillIds)
                    if (!c.Skills.Has(skillId))
                    { callingOk = false; callingDetail += $"{calling.id}->{skillId} "; }
            }
            Add("Calling starter skill references valid", callingOk, callingOk ? "all resolve" : callingDetail);

            bool questOk = true; string questDetail = "";
            foreach (var quest in c.Quests.All)
            {
                if (quest.objectives == null || quest.objectives.Length == 0)
                { questOk = false; questDetail += $"{quest.id}:no_objectives "; }
                if (quest.rewards == null || quest.rewards.Length == 0)
                { questOk = false; questDetail += $"{quest.id}:no_rewards "; }
            }
            Add("Quest definitions have objectives and rewards", questOk, questOk ? "all populated" : questDetail);

            bool trialRefsOk = true; string trialRefsDetail = "";
            foreach (var evidence in c.EvidenceEvents.All)
            {
                if (evidence.evidenceWeights == null || evidence.evidenceWeights.Length == 0)
                { trialRefsOk = false; trialRefsDetail += $"{evidence.id}:no_weights "; }
                if (evidence.titleProgress != null)
                    foreach (var title in evidence.titleProgress)
                        if (!c.Titles.Has(title.titleId))
                        { trialRefsOk = false; trialRefsDetail += $"{evidence.id}->title:{title.titleId} "; }
                if (evidence.affinityProgress != null)
                    foreach (var affinity in evidence.affinityProgress)
                        if (!c.Affinities.Has(affinity.affinityId))
                        { trialRefsOk = false; trialRefsDetail += $"{evidence.id}->affinity:{affinity.affinityId} "; }
                if (evidence.xpGrants != null)
                    foreach (var xp in evidence.xpGrants)
                        if (!string.IsNullOrWhiteSpace(xp.id) && !c.XPChannels.Has(xp.id) && !c.Skills.Has(xp.id) && !c.Professions.Has(xp.id))
                        { trialRefsOk = false; trialRefsDetail += $"{evidence.id}->xp:{xp.id} "; }
            }
            foreach (var cls in c.Classes.All)
                if (cls.weights == null || cls.weights.Length == 0)
                { trialRefsOk = false; trialRefsDetail += $"{cls.id}:no_class_weights "; }
            foreach (var profession in c.Professions.All)
                if (profession.progressionSkillIds == null || profession.progressionSkillIds.Length == 0)
                { trialRefsOk = false; trialRefsDetail += $"{profession.id}:no_skills "; }
                else foreach (var skillId in profession.progressionSkillIds)
                    if (!c.Skills.Has(skillId))
                    { trialRefsOk = false; trialRefsDetail += $"{profession.id}->{skillId} "; }
            foreach (var dungeon in c.Dungeons.All)
            {
                if (!c.DungeonResults.Has(dungeon.resultId))
                { trialRefsOk = false; trialRefsDetail += $"{dungeon.id}->result:{dungeon.resultId} "; }
                if (dungeon.recommendedSupplyItemIds != null)
                    foreach (var itemId in dungeon.recommendedSupplyItemIds)
                        if (!c.Items.Has(itemId))
                        { trialRefsOk = false; trialRefsDetail += $"{dungeon.id}->item:{itemId} "; }
            }
            foreach (var entry in c.GuildBoardEntries.All)
            {
                if (!string.IsNullOrWhiteSpace(entry.questId) && !c.Quests.Has(entry.questId))
                { trialRefsOk = false; trialRefsDetail += $"{entry.id}->quest:{entry.questId} "; }
                if (!string.IsNullOrWhiteSpace(entry.worldEventId) && !c.WorldEvents.Has(entry.worldEventId))
                { trialRefsOk = false; trialRefsDetail += $"{entry.id}->event:{entry.worldEventId} "; }
            }
            Add("Trial data spine references resolve", trialRefsOk, trialRefsOk ? "all resolve" : trialRefsDetail);

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
            if (writeReport)
                WriteReport(checks, passed, c);

            string summary = $"[ISO-Core] Validation: {passed}/{checks.Count} checks passed " +
                             $"({(allPass ? "ALL PASS" : "see report")}). " +
                             (writeReport ? "Report: Docs/IsoCoreFoundation/06_Validation_Report.md" : "Report writing skipped");
            if (!allPass)
            {
                var failed = new StringBuilder();
                foreach (var ch in checks)
                    if (!ch.pass)
                        failed.Append($" | FAIL: {ch.name} :: {ch.detail}");
                summary += failed.ToString();
            }
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
            sb.AppendLine($"- Callings: {c.Callings.Count}, Skills: {c.Skills.Count}, Quests: {c.Quests.Count}");
            sb.AppendLine($"- Evidence: {c.EvidenceEvents.Count}, XP Channels: {c.XPChannels.Count}, Titles: {c.Titles.Count}, Affinities: {c.Affinities.Count}");
            sb.AppendLine($"- Classes: {c.Classes.Count}, Professions: {c.Professions.Count}, Dungeons: {c.Dungeons.Count}, Board Entries: {c.GuildBoardEntries.Count}, World Events: {c.WorldEvents.Count}");
            sb.AppendLine();
            sb.AppendLine("## Manual play-mode checklist");
            sb.AppendLine();
            sb.AppendLine("Open `Assets/Scenes/IsoCoreFoundation.unity` and press Play, then confirm:");
            sb.AppendLine();
            sb.AppendLine("- [ ] Procedural isometric terrain renders around spawn (multiple biome colours visible while walking out).");
            sb.AppendLine("- [ ] Player moves with WASD and **cannot** walk through trees/rocks/water (collision).");
            sb.AppendLine("- [ ] `LMB` on a tree/rock/bush breaks it; a durability bar appears and drops enter the hotbar/inventory (`I`).");
            sb.AppendLine("- [ ] Select the hoe and `LMB` a walkable cell to till soil; select seeds and `LMB` tilled soil to plant.");
            sb.AppendLine("- [ ] Crops grow through visible stages; `LMB` on a mature crop harvests produce without deleting it when inventory is full.");
            sb.AppendLine("- [ ] Select `workbench` (or stone block) on the hotbar (1-9); a green/red ghost shows at the cursor.");
            sb.AppendLine("- [ ] `LMB` places a block / placeable; the item count decrements; `RMB` opens context options for blocks/placeables.");
            sb.AppendLine("- [ ] Placed solid block blocks player movement (collision refresh).");
            sb.AppendLine("- [ ] `RMB` on a placed workbench offers a crafting action; a recipe crafts and consumes inputs.");
            sb.AppendLine("- [ ] At least one mob (deer/slime) wanders nearby and despawns when far away.");
            sb.AppendLine();
            sb.AppendLine("> Note: legacy movement uses the classic `Input` axes. Ensure Project Settings > Player >");
            sb.AppendLine("> Active Input Handling is `Both` or `Input Manager (Old)` for WASD to work.");
            File.WriteAllText(Path.Combine(FoundationPaths.DocsDir, "06_Validation_Report.md"), sb.ToString());
        }
    }
}
