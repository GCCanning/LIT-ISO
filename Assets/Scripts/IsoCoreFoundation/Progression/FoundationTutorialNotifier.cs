using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Starter-zone tutorial prompts driven by real gameplay events. The sequence is
    /// intentionally tolerant: each event re-checks progression state so players can
    /// do steps out of order without trapping the tutorial.
    /// </summary>
    public sealed class FoundationTutorialNotifier : MonoBehaviour
    {
        FoundationInteractionOverlay _overlay;
        FoundationProgression _progression;
        Hotbar _hotbar;
        PlayerInteraction _interaction;
        CraftingSystem _crafting;
        PlacementSystem _placement;
        FarmingSystem _farming;
        int _step;

        public int CurrentStep => _step;

        public void Init(FoundationInteractionOverlay overlay, FoundationProgression progression,
            Hotbar hotbar, PlayerInteraction interaction, CraftingSystem crafting,
            PlacementSystem placement, FarmingSystem farming)
        {
            Unsubscribe();

            _overlay = overlay;
            _progression = progression;
            _hotbar = hotbar;
            _interaction = interaction;
            _crafting = crafting;
            _placement = placement;
            _farming = farming;

            if (_hotbar != null) _hotbar.OnSelectionChanged += HandleChanged;
            if (_interaction != null)
            {
                _interaction.ResourceHarvested += HandleResourceHarvested;
                _interaction.CraftingRequested += HandleCraftingRequested;
                _interaction.ContainerOpened += HandleContainerOpened;
                _interaction.ContextActionUsed += HandleContextActionUsed;
            }
            if (_crafting != null) _crafting.Crafted += HandleCrafted;
            if (_placement != null) _placement.Placed += HandlePlaced;
            if (_farming != null)
            {
                _farming.SoilTilled += HandleSoilTilled;
                _farming.SeedPlanted += HandleSeedPlanted;
                _farming.CropHarvested += HandleCropHarvested;
            }
            if (_progression != null)
            {
                _progression.Changed += HandleChanged;
                _progression.QuestCompleted += HandleQuestCompleted;
                _progression.RewardUnlocked += HandleRewardUnlocked;
            }

            ShowCurrent();
        }

        void OnDestroy() => Unsubscribe();

        void HandleChanged() => AdvanceWhileComplete();
        void HandleResourceHarvested(ResourceNodeDefinition node, System.Collections.Generic.IReadOnlyList<ItemStack> drops)
        {
            _earlyGameplayStarted = true;
            AdvanceWhileComplete();
        }
        void HandleCraftingRequested(StationType station)
        {
            _earlyGameplayStarted = true;
            AdvanceWhileComplete();
        }
        void HandleContainerOpened(StorageContainer container)
        {
            _containerOpened = true;
            AdvanceWhileComplete();
        }
        void HandleContextActionUsed(string actionId, string targetId)
        {
            _earlyGameplayStarted = true;
            if (actionId == "open_container") _containerOpened = true;
            AdvanceWhileComplete();
        }
        void HandleCrafted(RecipeDefinition recipe)
        {
            _earlyGameplayStarted = true;
            if (recipe != null)
            {
                if (recipe.id == "craft_wood_floor") _floorCrafted = true;
                else if (recipe.id == "craft_chest") _chestCrafted = true;
                else if (recipe.id == "craft_lantern") _lanternCrafted = true;
                else if (recipe.id == "craft_campfire") _campfireCrafted = true;
            }
            AdvanceWhileComplete();
        }

        void HandlePlaced(ItemDefinition item, int wx, int wy)
        {
            _earlyGameplayStarted = true;
            if (item != null && item.id == "workbench_item") _workbenchPlaced = true;
            if (item != null && (item.id == "campfire_item" || item.id == "fireplace_item")) _campfirePlaced = true;
            AdvanceWhileComplete();
        }

        void HandleSoilTilled(int wx, int wy)
        {
            _earlyGameplayStarted = true;
            AdvanceWhileComplete();
        }
        void HandleSeedPlanted(ItemDefinition seed, CropDefinition crop, int wx, int wy)
        {
            _earlyGameplayStarted = true;
            _seedPlanted = true;
            AdvanceWhileComplete();
        }

        void HandleQuestCompleted(FoundationQuestDefinition quest) => AdvanceWhileComplete();
        void HandleRewardUnlocked(FoundationRewardUnlock reward) => AdvanceWhileComplete();
        void HandleCropHarvested(CropDefinition crop)
        {
            _earlyGameplayStarted = true;
            _overlay?.Tutorial("First harvest. The field gives back.", 5f);
            AdvanceWhileComplete();
        }

        void AdvanceWhileComplete()
        {
            int guard = 0;
            while (guard++ < 8 && StepComplete(_step))
                _step++;
            ShowCurrent();
        }

        bool StepComplete(int step)
        {
            switch (step)
            {
                case 0: return true;
                case 1: return (_hotbar != null && _hotbar.Selected != 0) || _earlyGameplayStarted;
                case 2: return QuestProgress("fixing_the_south_path", "clear_node") > 0 ||
                               QuestProgress("first_flame_first_field", "gather_wood") > 0;
                case 3: return QuestProgress("first_flame_first_field", "gather_wood") >= 5;
                case 4: return QuestProgress("first_flame_first_field", "craft_workbench") > 0;
                case 5: return HasPlaced("workbench_item");
                case 6: return _campfireCrafted || _campfirePlaced;
                case 7: return _campfirePlaced;
                case 8: return QuestProgress("first_flame_first_field", "till_soil") > 0;
                case 9: return _seedPlanted;
                case 10: return _floorCrafted;
                case 11: return QuestProgress("a_roof_before_rain", "place_floor") >= 4;
                case 12: return _chestCrafted;
                case 13: return _containerOpened || QuestProgress("a_roof_before_rain", "place_chest") > 0;
                case 14: return _lanternCrafted;
                case 15: return QuestProgress("a_roof_before_rain", "place_lantern") > 0;
                case 16: return QuestProgress("fixing_the_south_path", "craft_path") >= 4;
                case 17: return QuestProgress("fixing_the_south_path", "place_path") >= 4;
                default: return false;
            }
        }

        bool _seedPlanted;
        bool _floorCrafted;
        bool _chestCrafted;
        bool _lanternCrafted;
        bool _workbenchPlaced;
        bool _campfireCrafted;
        bool _campfirePlaced;
        bool _containerOpened;
        bool _earlyGameplayStarted;

        bool HasPlaced(string itemId) => itemId == "workbench_item" && _workbenchPlaced;

        int QuestProgress(string questId, string objectiveId) =>
            _progression != null ? _progression.GetObjectiveProgress(questId, objectiveId) : 0;

        void ShowCurrent()
        {
            string text = TextForStep(_step);
            if (!string.IsNullOrWhiteSpace(text))
                _overlay?.Tutorial(text, _step == 0 ? 5f : 7f);
        }

        string TextForStep(int step)
        {
            switch (step)
            {
                case 0: return $"Mosswake Meadow. Calling awakened: {_progression?.Stats?.Class ?? "Greenhand"}.";
                case 1: return "Move with WASD. Choose a held item with 1-9 or the mouse wheel.";
                case 2: return "Left-click a bush, tree, or rock to break it. The bar shows its remaining durability.";
                case 3: return "First Flame: gather 5 wood. Trees, logs, and stumps count.";
                case 4: return "Open Crafting with C. Craft a workbench from wood.";
                case 5: return "Select the workbench in your hotbar, then left-click a clear tile to place it.";
                case 6: return "Craft a campfire. Firelight wards weak mobs and protects recovery at night.";
                case 7: return "Place the campfire, then right-click it to rest, cook, or inspect its ward.";
                case 8: return "Select the hoe and left-click grass to till your first soil.";
                case 9: return "Plant carrot or wheat seeds in fresh soil.";
                case 10: return "Stand near the workbench and craft wood floor tiles.";
                case 11: return "Place four wood floor tiles. Shelter starts underfoot.";
                case 12: return "Craft a chest at the workbench.";
                case 13: return "Place the chest, then right-click it to open its options.";
                case 14: return "Craft a lantern from wood and stone.";
                case 15: return "Place the lantern. The camp has a warm center now.";
                case 16: return "Craft stone path pieces at the workbench.";
                case 17: return "Place four path pieces to mark a safer route.";
                default: return "";
            }
        }

        void Unsubscribe()
        {
            if (_hotbar != null) _hotbar.OnSelectionChanged -= HandleChanged;
            if (_interaction != null)
            {
                _interaction.ResourceHarvested -= HandleResourceHarvested;
                _interaction.CraftingRequested -= HandleCraftingRequested;
                _interaction.ContainerOpened -= HandleContainerOpened;
                _interaction.ContextActionUsed -= HandleContextActionUsed;
            }
            if (_crafting != null) _crafting.Crafted -= HandleCrafted;
            if (_placement != null) _placement.Placed -= HandlePlaced;
            if (_farming != null)
            {
                _farming.SoilTilled -= HandleSoilTilled;
                _farming.SeedPlanted -= HandleSeedPlanted;
                _farming.CropHarvested -= HandleCropHarvested;
            }
            if (_progression != null)
            {
                _progression.Changed -= HandleChanged;
                _progression.QuestCompleted -= HandleQuestCompleted;
                _progression.RewardUnlocked -= HandleRewardUnlocked;
            }
        }
    }
}
