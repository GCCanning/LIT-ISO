using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Binds successful gameplay actions to the Foundation LitRPG progression state.
    /// The gameplay systems stay authoritative; this component only listens after work succeeds.
    /// </summary>
    public sealed class FoundationProgressionHooks : MonoBehaviour
    {
        const string FirstQuest = "first_flame_first_field";
        const string RoofQuest = "a_roof_before_rain";
        const string CraftQuest = "thread_twig_and_tin";
        const string PathQuest = "fixing_the_south_path";

        FoundationProgression _progression;
        PlayerInteraction _interaction;
        CraftingSystem _crafting;
        PlacementSystem _placement;
        FarmingSystem _farming;
        MobSpawner _mobs;

        public void Init(FoundationProgression progression, PlayerInteraction interaction,
            CraftingSystem crafting, PlacementSystem placement, FarmingSystem farming,
            MobSpawner mobs)
        {
            Unsubscribe();

            _progression = progression;
            _interaction = interaction;
            _crafting = crafting;
            _placement = placement;
            _farming = farming;
            _mobs = mobs;

            // Fix #27: single starter-quest entry point (idempotent; see FoundationProgression).
            _progression?.StartStarterQuests();

            if (_interaction != null) _interaction.ResourceHarvested += HandleResourceHarvested;
            if (_crafting != null) _crafting.Crafted += HandleCrafted;
            if (_placement != null)
            {
                _placement.Placed += HandlePlaced;
                _placement.Removed += HandleRemoved;
            }
            if (_farming != null)
            {
                _farming.SoilTilled += HandleSoilTilled;
                _farming.SeedPlanted += HandleSeedPlanted;
                _farming.CropHarvested += HandleCropHarvested;
            }
            if (_mobs != null)
            {
                _mobs.MobDefeated += HandleMobDefeated;
                _mobs.MobCalmed += HandleMobCalmed;
            }
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        void HandleResourceHarvested(ResourceNodeDefinition node, IReadOnlyList<ItemStack> drops)
        {
            if (_progression == null || node == null) return;

            int wood = Count(drops, "wood");
            int stone = Count(drops, "stone");
            int fiber = Count(drops, "fiber");
            int apple = Count(drops, "apple");
            int copper = Count(drops, "copper_ore");
            int mining = stone + copper;
            int foraging = fiber + apple;
            int total = wood + mining + foraging;

            if (total > 0)
            {
                var skills = new List<string>();
                if (wood > 0) skills.Add("woodcraft");
                if (mining > 0) skills.Add("mining");
                if (foraging > 0) skills.Add("foraging");
                Award(FoundationProgressionActivity.Harvest, 8 + total * 2, skills.ToArray());
            }

            if (wood > 0)
            {
                Evidence("harvest_wood", wood, node.id);
                Advance(FirstQuest, "gather_wood", wood);
            }

            if (stone > 0)
            {
                Evidence("harvest_stone", stone, node.id);
                Advance(CraftQuest, "mine_stone", stone);
            }

            if (fiber > 0)
            {
                Evidence("harvest_forage", fiber, node.id);
                Advance(CraftQuest, "gather_fiber", fiber);
            }

            if (apple > 0)
                Evidence("harvest_forage", apple, node.id);

            if (copper > 0)
                Evidence("harvest_stone", copper, node.id);

            Advance(PathQuest, "clear_node");
        }

        void HandleCrafted(RecipeDefinition recipe)
        {
            if (_progression == null || recipe == null) return;

            Award(FoundationProgressionActivity.Craft, 10, CraftSkillFor(recipe));
            Evidence(EvidenceForRecipe(recipe), 1, recipe.id);

            if (recipe.id == "craft_workbench")
                Advance(FirstQuest, "craft_workbench");

            if (recipe.outputs == null) return;
            foreach (var output in recipe.outputs)
            {
                if (output.IsEmpty) continue;

                if (output.itemId == "stone_path_item")
                    Advance(PathQuest, "craft_path", output.count);

                var item = _crafting?.Content?.Items.Get(output.itemId);
                if (item != null && item.category == ItemCategory.Tool)
                    Advance(CraftQuest, "craft_tool");
            }
        }

        void HandlePlaced(ItemDefinition item, int wx, int wy)
        {
            if (_progression == null || item == null) return;

            Award(FoundationProgressionActivity.Build, 8, "building");
            Evidence(EvidenceForPlacedItem(item), 1, item.id);

            if (item.id == "wood_floor_item")
                Advance(RoofQuest, "place_floor");
            else if (item.id == "lantern_item")
                Advance(RoofQuest, "place_lantern");
            else if (item.id == "chest_item")
                Advance(RoofQuest, "place_chest");
            else if (item.id == "stone_path_item")
                Advance(PathQuest, "place_path");
        }

        void HandleRemoved(string id, int wx, int wy)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Build, 2, "building");
        }

        void HandleSoilTilled(int wx, int wy)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Farm, 8, "farming");
            Evidence("till_soil", 1, "soil");
            Advance(FirstQuest, "till_soil");
        }

        void HandleSeedPlanted(ItemDefinition seed, CropDefinition crop, int wx, int wy)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Farm, 6, "farming");
            Evidence("till_soil", 1, seed != null ? seed.id : "seed");
        }

        void HandleCropHarvested(CropDefinition crop)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Farm, 12, "farming");
            Evidence("crop_harvest", 1, crop != null ? crop.id : "crop");
        }

        void HandleMobDefeated(MobDefinition mob)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Combat, 12, "combat", "warding");
            Evidence("mob_defeated", 1, mob != null ? mob.id : "mob");
        }

        void HandleMobCalmed(MobDefinition mob)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Creature, 8, "creaturecraft");
            Evidence("mob_calmed", 1, mob != null ? mob.id : "mob");
        }

        string CraftSkillFor(RecipeDefinition recipe)
        {
            if (recipe?.outputs != null)
            {
                foreach (var output in recipe.outputs)
                {
                    if (output.IsEmpty) continue;
                    var item = _crafting?.Content?.Items.Get(output.itemId);
                    if (item != null && item.category == ItemCategory.Food)
                        return "cooking";
                }
            }

            return "crafting";
        }

        string EvidenceForRecipe(RecipeDefinition recipe)
        {
            if (recipe == null) return "craft_workbench";
            if (recipe.id == "craft_campfire") return "craft_campfire";
            if (recipe.station == StationType.CookingPot || OutputsFood(recipe)) return "cook_fire_meal";
            return "craft_workbench";
        }

        bool OutputsFood(RecipeDefinition recipe)
        {
            if (recipe?.outputs == null) return false;
            foreach (var output in recipe.outputs)
            {
                if (output.IsEmpty) continue;
                var item = _crafting?.Content?.Items.Get(output.itemId);
                if (item != null && item.category == ItemCategory.Food)
                    return true;
            }
            return false;
        }

        string EvidenceForPlacedItem(ItemDefinition item)
        {
            if (item == null) return "place_path";
            if (item.id == "stone_path_item") return "place_path";
            if (item.id == "lantern_item" || item.id == "campfire_item") return "craft_campfire";
            return "craft_workbench";
        }

        void Award(FoundationProgressionActivity activity, int amount, params string[] skillIds)
        {
            _progression?.AddActivityXp(activity, amount, skillIds);
        }

        void Evidence(string evidenceId, int amount = 1, string sourceId = "")
        {
            _progression?.RecordEvidence(evidenceId, amount, sourceId);
        }

        void Advance(string questId, string objectiveId, int amount = 1)
        {
            if (amount <= 0) return;
            _progression?.AdvanceQuestObjective(questId, objectiveId, amount);
        }

        static int Count(IReadOnlyList<ItemStack> stacks, string itemId)
        {
            if (stacks == null || string.IsNullOrWhiteSpace(itemId)) return 0;
            int total = 0;
            for (int i = 0; i < stacks.Count; i++)
                if (stacks[i].itemId == itemId)
                    total += Mathf.Max(0, stacks[i].count);
            return total;
        }

        void Unsubscribe()
        {
            if (_interaction != null) _interaction.ResourceHarvested -= HandleResourceHarvested;
            if (_crafting != null) _crafting.Crafted -= HandleCrafted;
            if (_placement != null)
            {
                _placement.Placed -= HandlePlaced;
                _placement.Removed -= HandleRemoved;
            }
            if (_farming != null)
            {
                _farming.SoilTilled -= HandleSoilTilled;
                _farming.SeedPlanted -= HandleSeedPlanted;
                _farming.CropHarvested -= HandleCropHarvested;
            }
            if (_mobs != null)
            {
                _mobs.MobDefeated -= HandleMobDefeated;
                _mobs.MobCalmed -= HandleMobCalmed;
            }
        }
    }
}
