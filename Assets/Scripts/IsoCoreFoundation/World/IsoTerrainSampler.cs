using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// The single, deterministic terrain sampler. Both world generation and every
    /// collision/height query flow through this one code path, so visible tiles can
    /// never disagree with collision/height (the explicit fix for the legacy
    /// dual-Burst/C# sampler risk — Orientation §4/§7).
    /// </summary>
    public class IsoTerrainSampler
    {
        readonly FoundationConfig _cfg;
        readonly IReadOnlyList<BiomeDefinition> _biomes;
        readonly uint _seedHash;
        readonly int _meadowIndex;

        public IsoTerrainSampler(FoundationConfig cfg, FoundationContent content)
        {
            _cfg = cfg;
            _biomes = content.Biomes.All;
            _seedHash = (uint)(cfg.seed * 2654435761u + 0x9e3779b9u);

            _meadowIndex = 0;
            for (int i = 0; i < _biomes.Count; i++)
                if (_biomes[i].id == "meadow") { _meadowIndex = i; break; }
        }

        float Perlin(int wx, int wy, float freq, int saltX, int saltY)
        {
            // Offset coords by seed-derived amounts so the seed actually varies output.
            float ox = (_seedHash % 9973u) + saltX * 131.7f;
            float oy = ((_seedHash / 9973u) % 9973u) + saltY * 71.3f;
            return Mathf.PerlinNoise((wx + ox) * freq, (wy + oy) * freq);
        }

        float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791) ^ _seedHash;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xffffffu) / (float)0x1000000;
            }
        }

        public IsoCell Sample(int wx, int wy)
        {
            var cell = new IsoCell();

            int clearing = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
            bool inClearing = clearing <= _cfg.spawnClearingRadius;

            float temp = Perlin(wx, wy, _cfg.climateFrequency, 1, 2);
            float moist = Perlin(wx, wy, _cfg.climateFrequency, 3, 4);
            float heightNoise = Perlin(wx, wy, _cfg.heightFrequency, 5, 6);

            int biomeIndex = inClearing ? _meadowIndex : SelectBiome(temp, moist);
            var biome = _biomes.Count > 0 ? _biomes[biomeIndex] : null;

            // Height column.
            int height = _cfg.spawnHeight;
            if (!inClearing && biome != null)
                height = Mathf.Clamp(
                    biome.baseHeight + Mathf.RoundToInt(heightNoise * biome.heightVariance),
                    0, Mathf.Min(_cfg.maxHeight, 7)); // 7 == sort-order invariant ceiling

            // Water in low areas away from spawn.
            bool water = !inClearing && heightNoise < 0.16f;
            if (water) height = 0;

            // Surface block.
            string blockId;
            if (water) blockId = "water";
            else if (biome != null && biome.surfaceGroup != null)
            {
                var b = biome.surfaceGroup.GetVariant(Mathf.RoundToInt(Hash01(wx, wy, 7) * 1024));
                blockId = b != null ? b.id : "dirt";
            }
            else blockId = "dirt";

            cell.Height = (byte)height;
            cell.BiomeIndex = (byte)biomeIndex;
            cell.SurfaceBlockId = blockId;
            cell.Water = water;

            // Resource node placement (one per cell), never in the spawn clearing.
            if (!inClearing && !water && biome != null && biome.nodes != null)
            {
                foreach (var ns in biome.nodes)
                {
                    if (ns.node == null) continue;
                    if (Hash01(wx, wy, 11 + ns.node.id.Length) < ns.chancePerCell)
                    {
                        cell.NodeId = ns.node.id;
                        cell.NodeBlocks = ns.node.blocksMovement;
                        break;
                    }
                }
            }

            return cell;
        }

        int SelectBiome(float t, float m)
        {
            if (_biomes.Count == 0) return 0;
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _biomes.Count; i++)
            {
                float d = _biomes[i].ClimateDistance(t, m);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        public BiomeDefinition BiomeAt(int index) =>
            (index >= 0 && index < _biomes.Count) ? _biomes[index] : null;
    }
}
