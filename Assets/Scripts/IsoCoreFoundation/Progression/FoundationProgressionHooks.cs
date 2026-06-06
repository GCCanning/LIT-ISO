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

            StartPlayableStarterQuests();

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

        void StartPlayableStarterQuests()
        {
            if (_progression == null) return;
            _progression.StartQuest(FirstQuest);
            _progression.StartQuest(RoofQuest);
            _progression.StartQuest(CraftQuest);
            _progression.StartQuest(PathQuest);
        }

        void HandleResourceHarvested(ResourceNodeDefinition node, IReadOnlyList<ItemStack> drops)
        {
            if (_progression == null || node == null) return;

            int wood = Count(drops, "wood");
            int stone = Count(drops, "stone");
            int fiber = Count(drops, "fiber");
            int copper = Count(drops, "copper_ore");
            int total = wood + stone + fiber + copper;

            if (total > 0)
                Award(FoundationProgressionActivity.Harvest, 8 + total * 2);

            if (wood > 0)
                Advance(FirstQuest, "gather_wood", wood);

            if (stone > 0)
                Advance(CraftQuest, "mine_stone", stone);

            if (fiber > 0)
                Advance(CraftQuest, "gather_fiber", fiber);

            Advance(PathQuest, "clear_node");
        }

        void HandleCrafted(RecipeDefinition recipe)
        {
            if (_progression == null || recipe == null) return;

            Award(FoundationProgressionActivity.Craft, 10);

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

            Award(FoundationProgressionActivity.Build, 8);

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
            Award(FoundationProgressionActivity.Build, 2);
        }

        void HandleSoilTilled(int wx, int wy)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Farm, 8);
            Advance(FirstQuest, "till_soil");
        }

        void HandleSeedPlanted(ItemDefinition seed, CropDefinition crop, int wx, int wy)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Farm, 6);
        }

        void HandleCropHarvested(CropDefinition crop)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Farm, 12);
        }

        void HandleMobDefeated(MobDefinition mob)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Combat, 12);
        }

        void HandleMobCalmed(MobDefinition mob)
        {
            if (_progression == null) return;
            Award(FoundationProgressionActivity.Combat, 8);
        }

        void Award(FoundationProgressionActivity activity, int amount)
        {
            _progression?.AddActivityXp(activity, amount);
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
