using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Tunables for the foundation. Plain serializable class with defaults.</summary>
    [Serializable]
    public class FoundationConfig
    {
        [Header("World")]
        public int seed = 1337;
        public int chunkSize = 12;
        public int viewRadiusChunks = 1;     // chunks streamed around the player (3x3)
        public int maxHeight = 4;
        public int spawnClearingRadius = 6;  // flat, mob-free safe start (cells)
        public int spawnHeight = 1;

        [Header("Climate noise")]
        public float climateFrequency = 0.02f;
        public float heightFrequency = 0.06f;

        [Header("Player")]
        public float moveSpeed = 4.0f;
        public float interactRange = 1.8f;

        [Header("Mobs")]
        public int mobCap = 8;
        public float mobSpawnInterval = 3f;
        public float mobSpawnRadius = 9f;
        public float mobDespawnRadius = 16f;

        [Header("Starter inventory (itemId : count)")]
        public List<ItemStack> starterItems = new()
        {
            new ItemStack("wood", 12),
            new ItemStack("stone", 8),
            new ItemStack("fiber", 6),
            new ItemStack("workbench_item", 1),
            new ItemStack("stone_block_item", 5),
            new ItemStack("hoe", 1),
            new ItemStack("carrot_seeds", 3),
            new ItemStack("wheat_seeds", 3),
        };
    }
}
