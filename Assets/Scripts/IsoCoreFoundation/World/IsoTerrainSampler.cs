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
        readonly int _beachIndex;

        public IsoTerrainSampler(FoundationConfig cfg, FoundationContent content)
        {
            _cfg = cfg;
            _biomes = content.Biomes.All;
            _seedHash = (uint)(cfg.seed * 2654435761u + 0x9e3779b9u);

            _meadowIndex = 0;
            _beachIndex = -1;
            for (int i = 0; i < _biomes.Count; i++)
            {
                if (_biomes[i].id == "meadow") _meadowIndex = i;
                else if (_biomes[i].id == "beach") _beachIndex = i;
            }
        }

        float Perlin(int wx, int wy, float freq, int saltX, int saltY)
        {
            // Offset coords by seed-derived amounts so the seed actually varies output.
            float ox = (_seedHash % 9973u) + saltX * 131.7f;
            float oy = ((_seedHash / 9973u) % 9973u) + saltY * 71.3f;
            return Mathf.PerlinNoise((wx + ox) * freq, (wy + oy) * freq);
        }

        // Float-coordinate Perlin sample, for warped (meandering) river coordinates.
        float PerlinF(float fx, float fy, float freq, int saltX, int saltY)
        {
            float ox = (_seedHash % 9973u) + saltX * 131.7f;
            float oy = ((_seedHash / 9973u) % 9973u) + saltY * 71.3f;
            return Mathf.PerlinNoise((fx + ox) * freq, (fy + oy) * freq);
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

            // Grass prototype world: one uniform walkable surface block everywhere,
            // no water and no resource nodes, with optional Perlin rolling hills. The
            // spawn clearing stays perfectly flat so the player starts on safe ground.
            if (_cfg.flatWorld)
            {
                int gHeight = 0;
                if (_cfg.flatWorldMaxHeight > 0)
                {
                    int gClearing = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
                    if (gClearing > _cfg.spawnClearingRadius)
                    {
                        // Distance past the clearing edge, used to ramp hills up smoothly
                        // over a few cells instead of a wall right at the clearing border.
                        float edgeFade = Mathf.Clamp01((gClearing - _cfg.spawnClearingRadius) / 4f);
                        float hn = Perlin(wx, wy, _cfg.flatWorldHeightFrequency, 5, 6);
                        int ceiling = Mathf.Min(_cfg.flatWorldMaxHeight, 7); // sort-order ceiling
                        gHeight = Mathf.Clamp(
                            Mathf.RoundToInt(hn * ceiling * edgeFade), 0, ceiling);
                    }
                }

                var meadow = (_meadowIndex >= 0 && _meadowIndex < _biomes.Count)
                    ? _biomes[_meadowIndex] : null;

                // Surface block: scatter the meadow grass variants for variety, or use
                // the single configured block when variants are disabled.
                string surfaceId = string.IsNullOrEmpty(_cfg.flatSurfaceBlockId)
                    ? "grass_1" : _cfg.flatSurfaceBlockId;
                if (_cfg.flatWorldUseVariants && meadow != null && meadow.surfaceGroup != null)
                {
                    var b = meadow.surfaceGroup.GetVariant(
                        Mathf.RoundToInt(Hash01(wx, wy, 7) * 1024));
                    if (b != null) surfaceId = b.id;
                }

                cell.Height = (byte)gHeight;
                cell.BiomeIndex = (byte)_meadowIndex;
                cell.SurfaceBlockId = surfaceId;
                cell.Water = false;

                // Place decorations in proper procedural GROUPS (forest groves, rock
                // outcrops, light bush ground-cover) outside the flat spawn clearing — not
                // uniform random scatter. Reuses the meadow biome's node table for stats.
                if (_cfg.flatWorldDecorations && meadow != null && meadow.nodes != null)
                {
                    int dClearing = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
                    if (dClearing > _cfg.spawnClearingRadius)
                    {
                        var picked = PickClusteredDecoration(wx, wy, meadow,
                            Mathf.Clamp01(_cfg.flatWorldDecorationDensity));
                        if (picked.node != null)
                        {
                            cell.NodeId = picked.node.id;
                            cell.NodeBlocks = picked.node.blocksMovement;
                        }
                    }
                }

                return cell;
            }

            // Continent generator: oceans, beaches, biome regions, multi-step cliffs,
            // and winding rivers — one deterministic per-cell function (no global passes,
            // so it streams chunk-by-chunk like the rest of the world).
            if (_cfg.continentWorld)
                return SampleContinent(wx, wy);

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

        /// <summary>
        /// The continent generator (ported from the standalone world-gen prototype, see
        /// Docs/handoff/WORLD_GEN_PROTOTYPE_HANDOFF.md). Pure per-cell function of
        /// (wx, wy, seed): elevation drives an ocean -> shallow -> beach -> land depth
        /// chain plus multi-step cliff heights; a low-frequency climate field keeps
        /// biome regions coherent (no scattered foreign tiles); warped band noise carves
        /// winding rivers with sand banks. No global arrays or cleanup passes, so it
        /// streams exactly like the legacy sampler.
        /// </summary>
        IsoCell SampleContinent(int wx, int wy)
        {
            var cell = new IsoCell();

            int clearing = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
            bool inClearing = clearing <= _cfg.spawnClearingRadius;

            // ---- spawn clearing: guaranteed flat, dry, walkable meadow start ----
            if (inClearing)
            {
                var meadowB = (_meadowIndex >= 0 && _meadowIndex < _biomes.Count) ? _biomes[_meadowIndex] : null;
                cell.Height = (byte)Mathf.Clamp(_cfg.spawnHeight, 0, 7);
                cell.BiomeIndex = (byte)_meadowIndex;
                cell.SurfaceBlockId = SurfaceVariant(meadowB, wx, wy, "grass_1");
                cell.Water = false;
                return cell;
            }

            // ---- elevation: base landmass + medium detail, lifted near the origin so
            //      the spawn region is always solid land (apron around the clearing) ----
            // Smooth elevation: base landmass dominates, a light detail octave adds shape.
            // Keeping the detail weight low means adjacent cells stay close in elevation, so
            // height steps between neighbours are gentle (no sudden multi-level cliff walls).
            float eBase = Perlin(wx, wy, _cfg.continentFrequency, 11, 12);
            float eDetail = Perlin(wx, wy, _cfg.continentFrequency * 3f, 13, 14);
            float e = eBase * 0.80f + eDetail * 0.20f;
            float bias = Mathf.Clamp01(1f - clearing / Mathf.Max(1f, _cfg.continentSpawnLandRadius))
                         * _cfg.continentSpawnLandBias;
            e += bias;

            float temp = Perlin(wx, wy, _cfg.climateFrequency, 1, 2);
            float moist = Perlin(wx, wy, _cfg.climateFrequency, 3, 4);

            // ---- deep / shallow ocean ----
            if (e < _cfg.continentShoreLevel)
            {
                cell.Height = 0;
                cell.BiomeIndex = (byte)Mathf.Max(0, _beachIndex);
                cell.SurfaceBlockId = "water";
                cell.Water = true;
                return cell;
            }

            // ---- rivers: warp the sample point so the band meanders, then carve a thin
            //      water line in valleys (never across peaks). Banks become sand. ----
            float warpX = wx + (Perlin(wx, wy, _cfg.riverWarpFrequency, 51, 52) - 0.5f) * 2f * _cfg.riverWarpAmplitude;
            float warpY = wy + (Perlin(wx, wy, _cfg.riverWarpFrequency, 53, 54) - 0.5f) * 2f * _cfg.riverWarpAmplitude;
            float band = PerlinF(warpX, warpY, _cfg.riverFrequency, 55, 56);
            float riverDist = Mathf.Abs(band - 0.5f);
            bool belowRidge = e < _cfg.riverMaxElevation;
            bool isRiver = belowRidge && riverDist < _cfg.riverHalfWidth;
            bool isBank = belowRidge && !isRiver && riverDist < (_cfg.riverHalfWidth + _cfg.riverBankWidth);

            if (isRiver)
            {
                cell.Height = 0;
                cell.BiomeIndex = (byte)Mathf.Max(0, _beachIndex);
                cell.SurfaceBlockId = "water";
                cell.Water = true;
                return cell;
            }

            // ---- beach ring (coast band just above the shore) and river banks: sand ----
            bool isBeach = e < _cfg.continentBeachLevel;
            if (isBeach || isBank)
            {
                var beachB = (_beachIndex >= 0 && _beachIndex < _biomes.Count) ? _biomes[_beachIndex] : null;
                cell.Height = 0;
                cell.BiomeIndex = (byte)Mathf.Max(0, _beachIndex);
                cell.SurfaceBlockId = SurfaceVariant(beachB, wx, wy, "sand_1");
                cell.Water = false;
                // Sand carries only sparse rock outcrops — no trees/bushes on a beach.
                var beachRock = PickRockOutcrop(wx, wy, beachB);
                if (beachRock.node != null)
                {
                    cell.NodeId = beachRock.node.id;
                    cell.NodeBlocks = beachRock.node.blocksMovement;
                }
                return cell;
            }

            // ---- land: climate picks the biome region; elevation steps the cliff height ----
            int biomeIndex = SelectBiome(temp, moist);
            var biome = (biomeIndex >= 0 && biomeIndex < _biomes.Count) ? _biomes[biomeIndex] : null;

            int height = 1;
            if (e > _cfg.continentTier2Level) height = 2;
            if (e > _cfg.continentTier3Level) height = 3;
            if (e > _cfg.continentTier4Level) height = 4;
            height = Mathf.Clamp(height, 0, Mathf.Min(_cfg.maxHeight, 7));

            cell.Height = (byte)height;
            cell.BiomeIndex = (byte)biomeIndex;
            cell.SurfaceBlockId = SurfaceVariant(biome, wx, wy, "dirt");
            cell.Water = false;

            // Clustered decoration (groves / outcrops / flower patches), reusing the
            // shared grouping logic so nothing scatters uniformly.
            if (biome != null && biome.nodes != null)
            {
                var picked = PickClusteredDecoration(wx, wy, biome, 1f);
                if (picked.node != null)
                {
                    cell.NodeId = picked.node.id;
                    cell.NodeBlocks = picked.node.blocksMovement;
                }
            }

            return cell;
        }

        /// <summary>Picks a surface-block id from a biome's surface group (seeded variant),
        /// falling back to a default block id when the biome/group is missing.</summary>
        string SurfaceVariant(BiomeDefinition biome, int wx, int wy, string fallback)
        {
            if (biome != null && biome.surfaceGroup != null)
            {
                var b = biome.surfaceGroup.GetVariant(Mathf.RoundToInt(Hash01(wx, wy, 7) * 1024));
                if (b != null) return b.id;
            }
            return fallback;
        }

        /// <summary>Rolls a biome's resource-node spawn table for one cell (first hit wins),
        /// used for sparse coastal dressing. No-op when the biome has no nodes.</summary>
        void TryPlaceBiomeNode(ref IsoCell cell, BiomeDefinition biome, int wx, int wy)
        {
            if (biome == null || biome.nodes == null) return;
            foreach (var ns in biome.nodes)
            {
                if (ns.node == null) continue;
                if (Hash01(wx, wy, 11 + ns.node.id.Length) < ns.chancePerCell)
                {
                    cell.NodeId = ns.node.id;
                    cell.NodeBlocks = ns.node.blocksMovement;
                    return;
                }
            }
        }

        /// <summary>
        /// Chooses a decoration for a cell using noise-driven GROUPING rather than uniform
        /// random scatter:
        ///   • Trees cluster into forest groves (low-frequency noise) — dense in the middle,
        ///     thinning toward the grove edges.
        ///   • Bushes are a light, even ground-cover scatter on the open ground between groves.
        ///   • Rocks appear only inside rare coarse-noise clumps (outcrops), never blanketing.
        /// Returns the chosen node (or default with node == null for an empty cell). Only
        /// considers nodes that have art in Resources/Decorations so placeholders never spawn.
        /// </summary>
        BiomeNodeSpawn PickClusteredDecoration(int wx, int wy, BiomeDefinition biome, float density)
        {
            // 1. Trees — forest groves via low-frequency noise. Inside a grove a cell may be
            //    a regular tree or (for variety) a pine; near grove edges an occasional stump
            //    suggests old logging.
            var tree = FindArtNode(biome, "tree");
            var pine = FindArtNode(biome, "pine");
            if (tree.node != null || pine.node != null)
            {
                float forest = Perlin(wx, wy, _cfg.decoForestFrequency, 21, 22);
                if (forest > _cfg.decoForestThreshold)
                {
                    float depth = Mathf.InverseLerp(_cfg.decoForestThreshold, 1f, forest);
                    float chance = _cfg.decoTreeDensityInForest * (0.35f + 0.65f * depth) * density;
                    if (Hash01(wx, wy, 71) < chance)
                    {
                        // Pine appears as a minority species; otherwise a normal tree.
                        bool wantPine = pine.node != null && Hash01(wx, wy, 72) < 0.35f;
                        if (wantPine) return pine;
                        if (tree.node != null) return tree;
                        return pine; // only pine available in this biome
                    }
                    // Rare stump on grove ground (thinned-out look).
                    var stumpN = FindArtNode(biome, "stump");
                    if (stumpN.node != null && Hash01(wx, wy, 73) < 0.02f * density) return stumpN;
                    var logN = FindArtNode(biome, "log");
                    if (logN.node != null && Hash01(wx, wy, 74) < 0.015f * density) return logN;
                }
            }

            // 2. Bushes — light, even scatter on open ground.
            var bush = FindArtNode(biome, "bush");
            if (bush.node != null && Hash01(wx, wy, 81) < _cfg.decoBushChance * density)
                return bush;

            // 2b. Flowers — gentle ground-cover patches via mid-frequency noise so they form
            //     little meadOws rather than uniform speckle.
            var flower = FindArtNode(biome, "flower");
            if (flower.node != null)
            {
                float patch = Perlin(wx, wy, _cfg.decoForestFrequency * 2.3f, 41, 42);
                if (patch > 0.5f && Hash01(wx, wy, 82) < 0.10f * density) return flower;
            }

            // 3. Rocks — rare clumps where a separate coarse noise peaks.
            var rock = FindArtNode(biome, "rock");
            if (rock.node != null)
            {
                float rockNoise = Perlin(wx, wy, _cfg.decoForestFrequency * 1.7f, 31, 32);
                if (rockNoise > _cfg.decoRockClusterThreshold &&
                    Hash01(wx, wy, 91) < _cfg.decoRockChanceInCluster * density)
                    return rock;
            }

            return default;
        }

        /// <summary>Rock-only decoration for sand cells (beach ring, river banks):
        /// rare coarse-noise outcrop clumps, no vegetation — coasts read as mostly
        /// clean sand with occasional stone. Mirrors the rock branch of
        /// PickClusteredDecoration but at half density and with its own noise salts
        /// so beach outcrops don't correlate with inland ones.</summary>
        BiomeNodeSpawn PickRockOutcrop(int wx, int wy, BiomeDefinition biome)
        {
            if (biome == null) return default;
            var rock = FindArtNode(biome, "rock");
            if (rock.node == null) return default;
            float rockNoise = Perlin(wx, wy, _cfg.decoForestFrequency * 1.7f, 33, 34);
            if (rockNoise > _cfg.decoRockClusterThreshold &&
                Hash01(wx, wy, 92) < _cfg.decoRockChanceInCluster * 0.5f)
                return rock;
            return default;
        }

        /// <summary>Finds a biome node by id, but only if it has art in Resources/Decorations
        /// (otherwise it would render as an ugly placeholder box). Result is cached by the resolver.</summary>
        BiomeNodeSpawn FindArtNod