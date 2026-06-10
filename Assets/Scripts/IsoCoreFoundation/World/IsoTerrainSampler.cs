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
            float e = ContinentElevation(wx, wy);

            float temp = Perlin(wx, wy, _cfg.climateFrequency, 1, 2);
            float moist = Perlin(wx, wy, _cfg.climateFrequency, 3, 4);

            // ---- deep / shallow ocean (pack palette: navy deep field, light shallow
            //      ring) with foam-footed shore stones hugging the land edge ----
            if (e < _cfg.continentShoreLevel)
            {
                bool deep = e < _cfg.continentDeepLevel;
                cell.Height = 0;
                cell.BiomeIndex = (byte)Mathf.Max(0, _beachIndex);
                // One seamless navy field for the whole ocean (the light family's edge
                // highlights read as a grid when tiled - it stays reserved for rivers).
                cell.SurfaceBlockId = "water_deep";
                cell.Water = true;
                // Shore stones: only in the shallow ring, only against land, sparse.
                // Hash gate first so the 4-neighbour elevation probe stays rare.
                if (!deep && Hash01(wx, wy, 95) < 0.07f &&
                    (ContinentElevation(wx + 1, wy) >= _cfg.continentShoreLevel ||
                     ContinentElevation(wx - 1, wy) >= _cfg.continentShoreLevel ||
                     ContinentElevation(wx, wy + 1) >= _cfg.continentShoreLevel ||
                     ContinentElevation(wx, wy - 1) >= _cfg.continentShoreLevel))
                {
                    cell.NodeId = "shore_stone";
                    cell.NodeBlocks = true;
                }
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
                // The river surface sits ONE step below the local terrain tier instead
                // of at sea level, so a stream crossing higher ground reads as water in
                // a shallow channel rather than a black slot under the cliff edge.
                int landH = 1;
                if (e > _cfg.continentTier2Level) landH = 2;
                if (e > _cfg.continentTier3Level) landH = 3;
                if (e > _cfg.continentTier4Level) landH = 4;
                landH = Mathf.Clamp(landH, 1, Mathf.Min(_cfg.maxHeight, 7));
                cell.Height = (byte)(landH - 1);
                cell.BiomeIndex = (byte)Mathf.Max(0, _beachIndex);
                cell.SurfaceBlockId = "water";
                cell.Water = true;
                // Occasional foam-footed stone breaking the stream surface - LOWLAND
                // rivers only (water props in an elevated channel read as misplaced).
                if (landH == 1 && Hash01(wx, wy, 96) < 0.05f)
                {
                    cell.NodeId = "shore_stone";
                    cell.NodeBlocks = true;
                }
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
            // Minecraft-style rule: beach/sand exists only against water (the beach ring
            // and river banks above). If climate picks "beach" for an interior cell,
            // it becomes meadow instead - no sand patches popping up inland.
            if (biomeIndex == _beachIndex) biomeIndex = _meadowIndex;
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

            // Forest interiors: dense hedge/canopy blocks tile into forest MASS (the
            // pack's design - these are terrain, not props). Uses the same grove noise
            // as trees so canopy clumps wrap the tree clusters. Leafy 029 dominates;
            // striped 027/028 are accents. Canopy cells carry no props - they ARE the
            // vegetation. Lowland only; crag tiers stay bare.
            bool canopyCell = false;
            if (biome != null && biome.id == "forest" && height < 3)
            {
                float canopyN = Perlin(wx, wy, _cfg.decoForestFrequency, 21, 22);
                if (canopyN > 0.72f)
                {
                    float cr = Hash01(wx, wy, 97);
                    cell.SurfaceBlockId = cr < 0.70f ? "canopy_1" : (cr < 0.85f ? "canopy_2" : "canopy_3");
                    canopyCell = true;
                }
            }

            // Clustered decoration (groves / outcrops / flower patches), reusing the
            // shared grouping logic so nothing scatters uniformly. Vegetation respects
            // the terrain: crag tops (tier 3+) are bare stone with only rock outcrops,
            // and trees never stand on a cliff lip where their canopy would float
            // over the stone face below.
            if (!canopyCell && biome != null && biome.nodes != null)
            {
                if (height >= 3)
                {
                    // Bare crag: rock outcrops only - no trees/bushes on stone.
                    var crag = PickRockOutcrop(wx, wy, biome);
                    if (crag.node != null)
                    {
                        cell.NodeId = crag.node.id;
                        cell.NodeBlocks = crag.node.blocksMovement;
                    }
                }
                else
                {
                    var picked = PickClusteredDecoration(wx, wy, biome, 1f);
                    if (picked.node != null &&
                        (picked.node.id == "tree" || picked.node.id == "pine"))
                    {
                        // Cliff-lip check (lazy: only when a tree was actually rolled,
                        // so the extra neighbour sampling stays rare). A neighbour one
                        // or more steps down means this is a ledge - swap the tree for
                        // a bush so the lip still reads vegetated, never overhanging.
                        bool lip = ContinentTier(wx + 1, wy) < height ||
                                   ContinentTier(wx - 1, wy) < height ||
                                   ContinentTier(wx, wy + 1) < height ||
                                   ContinentTier(wx, wy - 1) < height;
                        if (lip)
                            picked = FindArtNode(biome, "bush");
                    }
                    if (picked.node != null)
                    {
                        cell.NodeId = picked.node.id;
                        cell.NodeBlocks = picked.node.blocksMovement;
                    }
                }
            }

            return cell;
        }

        /// <summary>Continent elevation at a cell (base landmass + light detail octave
        /// + spawn-apron lift). Shared by SampleContinent and its neighbour probes so
        /// every caller sees the exact same field.</summary>
        float ContinentElevation(int wx, int wy)
        {
            int clearing = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
            float eBase = Perlin(wx, wy, _cfg.continentFrequency, 11, 12);
            float eDetail = Perlin(wx, wy, _cfg.continentFrequency * 3f, 13, 14);
            float e = eBase * 0.80f + eDetail * 0.20f;
            e += Mathf.Clamp01(1f - clearing / Mathf.Max(1f, _cfg.continentSpawnLandRadius))
                 * _cfg.continentSpawnLandBias;
            return e;
        }

        /// <summary>Continent height tier from elevation alone (no river/beach carve)
        /// - cheap neighbour probe used to detect cliff lips when gating tall
        /// vegetation. Mirrors the tier thresholds in SampleContinent.</summary>
        int ContinentTier(int wx, int wy)
        {
            int clearing = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
            if (clearing <= _cfg.spawnClearingRadius)
                return Mathf.Clamp(_cfg.spawnHeight, 0, 7);
            float e = ContinentElevation(wx, wy);
            if (e < _cfg.continentBeachLevel) return 0;
            int h = 1;
            if (e > _cfg.continentTier2Level) h = 2;
            if (e > _cfg.continentTier3Level) h = 3;
            if (e > _cfg.continentTier4Level) h = 4;
            return Mathf.Clamp(h, 0, Mathf.Min(_cfg.maxHeight, 7));
        }

        /// <summary>Picks a surface-block id from a biome's surface group (seeded variant),
        /// falling back to a default block id when the biome/group is missing.</summary>
        string SurfaceVariant(BiomeDefinition biome