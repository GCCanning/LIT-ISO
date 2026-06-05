using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    [Serializable]
    public struct BiomeNodeSpawn
    {
        public ResourceNodeDefinition node;
        [Range(0f, 1f)] public float chancePerCell;
    }

    [Serializable]
    public struct BiomeMobSpawn
    {
        public MobDefinition mob;
        public float weight;
    }

    /// <summary>
    /// Top tier of the terrain model: maps a climate point (temperature, moisture)
    /// to a surface block group plus resource-node and mob spawn rules.
    /// </summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Biome", fileName = "Biome")]
    public class BiomeDefinition : FoundationDefinition
    {
        [Header("Climate centre (0..1) — nearest biome wins")]
        [Range(0f, 1f)] public float temperature = 0.5f;
        [Range(0f, 1f)] public float moisture = 0.5f;

        [Header("Surface")]
        public BlockGroupDefinition surfaceGroup;

        [Header("Height column")]
        public int baseHeight = 1;
        public int heightVariance = 2;

        [Header("Spawn rules")]
        public BiomeNodeSpawn[] nodes;
        public BiomeMobSpawn[] mobs;

        [Header("Debug")]
        public Color debugTint = Color.white;

        public float ClimateDistance(float t, float m)
        {
            float dt = t - temperature, dm = m - moisture;
            return dt * dt + dm * dm;
        }
    }

    public class BiomeDatabase : Database<BiomeDefinition> { }
}
