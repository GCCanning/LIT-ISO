using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    // ---- Enums (shared vocabulary across the foundation) ----

    public enum CollisionMode { Walkable, Solid, Water, Decorative }

    public enum ItemCategory { Resource, Block, Tool, Food, Placeable, Misc }

    public enum ToolType { None, Axe, Pickaxe, Shovel, Hoe, Sword }

    public enum StationType { None, Hand, Workbench, Furnace, CookingPot }

    public enum InteractionKind { None, CraftingStation, Container, Decoration, Entrance }

    public enum MobBehavior { Passive, Skittish, Hostile }

    public enum FoundationStatType { STR, DEX, INT, VIT, DEF, LUCK }

    public enum FoundationCallingTier { Novice, Adept, Artisan, Luminary, Mythwarm }

    public enum FoundationSkillNodeKind { Ease, Yield, Insight, Expression, Utility, Harmony }

    public enum FoundationQuestType { Hearth, Craft, Field, Path, Creature, Neighbor, Lore, Civic, System, Exploration }

    public enum FoundationRewardType { Recipe, Pattern, TraitSeed, LandmarkPermit, NeighborBond, RegionShift, MemoryPage, CallingToken, Item, Xp }

    public enum FoundationProgressionActivity { Harvest, Craft, Build, Farm, Explore, Creature, Combat, Trade, Lore }

    // ---- Small serializable data structs ----

    [Serializable]
    public struct ItemStack
    {
        public string itemId;
        public int count;
        public ItemStack(string itemId, int count) { this.itemId = itemId; this.count = count; }
        public bool IsEmpty => string.IsNullOrEmpty(itemId) || count <= 0;
    }

    /// <summary>A weighted drop entry: yields [min,max] of itemId at the given chance.</summary>
    [Serializable]
    public struct ItemDrop
    {
        public string itemId;
        public int min;
        public int max;
        [Range(0f, 1f)] public float chance;

        public ItemDrop(string itemId, int min, int max, float chance = 1f)
        {
            this.itemId = itemId; this.min = min; this.max = max; this.chance = chance;
        }
    }

    [Serializable]
    public struct RecipeIngredient
    {
        public string itemId;
        public int count;
        public RecipeIngredient(string itemId, int count) { this.itemId = itemId; this.count = count; }
    }

    [Serializable]
    public struct FoundationStatBonus
    {
        public FoundationStatType stat;
        public int amount;

        public FoundationStatBonus(FoundationStatType stat, int amount)
        {
            this.stat = stat;
            this.amount = amount;
        }
    }

    [Serializable]
    public struct FoundationQuestObjective
    {
        public string id;
        public string text;
        public int required;

        public FoundationQuestObjective(string id, string text, int required = 1)
        {
            this.id = id;
            this.text = text;
            this.required = required;
        }
    }

    [Serializable]
    public struct FoundationQuestReward
    {
        public FoundationRewardType type;
        public string id;
        public int amount;

        public FoundationQuestReward(FoundationRewardType type, string id, int amount = 1)
        {
            this.type = type;
            this.id = id;
            this.amount = amount;
        }
    }

    [Serializable]
    public struct FoundationRewardUnlock
    {
        public FoundationRewardType type;
        public string id;
        public int amount;

        public FoundationRewardUnlock(FoundationRewardType type, string id, int amount = 1)
        {
            this.type = type;
            this.id = id;
            this.amount = amount;
        }
    }

    /// <summary>Implemented by world objects the player can interact with.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(GameObject interactor);
    }
}
