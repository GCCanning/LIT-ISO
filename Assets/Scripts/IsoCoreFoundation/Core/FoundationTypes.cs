using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    // ---- Enums (shared vocabulary across the foundation) ----

    public enum CollisionMode { Walkable, Solid, Water, Decorative }

    public enum ItemCategory { Resource, Block, Tool, Food, Placeable, Misc }

    public enum ToolType { None, Axe, Pickaxe, Shovel, Hoe, Sword }

    public enum StationType { None, Hand, Workbench, Furnace, CookingPot }

    public enum InteractionKind { None, CraftingStation, Container, Decoration }

    public enum MobBehavior { Passive, Skittish, Hostile }

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

    /// <summary>Implemented by world objects the player can interact with (E key).</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(GameObject interactor);
    }
}
