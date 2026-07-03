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
        readonly BlockDatabase _blocks;
        readonly uint _seedHash;
        readonly int _meadowIndex;
        readonly int _beachIndex;
        readonly int _mountainIndex;
        static readonly string[] StarterBiomeIds =
        {
            "forest", "marsh", "grotto", "sunspool",
            "badlands", "snow", "frozenmountain", "mountain"
        };
        readonly int[] _starterBiomeIndices;
        // Climate-rectangle selection (driven by biome_suite.json). When false we keep the
        // original nearest-centroid SelectBiome so a missing/!applied suite is a safe no-op.
        readonly bool _useBoxes;
        readonly int _fallbackIndex;

        public IsoTerrainSampler(FoundationConfig cfg, FoundationContent content)
        {
            _cfg = cfg;
            _biomes = content.Biomes.All;
            _blocks = content.Blocks;
            _seedHash = (uint)(cfg.seed * 2654435761u + 0x9e3779b9u);

            _meadowIndex = 0;
            _beachIndex = -1;
            _mountainIndex = -1;
            _fallbackIndex = -1;
            _starterBiomeIndices = new int[StarterBiomeIds.Length];
            for (int i = 0; i < _starterBiomeIndices.Length; i++)
                _starterBiomeIndices[i] = -1;
            for (int i = 0; i < _biomes.Count; i++)
            {
                if (_biomes[i].id == "meadow") _meadowIndex = i;
                else if (_biomes[i].id == "beach") _beachIndex = i;
                else if (_biomes[i].id == "mountain") _mountainIndex = i;
                if (_biomes[i].id == content.fallbackBiomeId) _fallbackIndex = i;
                for (int starter = 0; starter < StarterBiomeIds.Length; starter++)
                    if (_biomes[i].id == StarterBiomeIds[starter])
                        _starterBiomeIndices[starter] = i;
            }
            if (_fallbackIndex < 0) _fallbackIndex = _meadowIndex;
            _useBoxes = content.biomeSuiteApplied;
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
            var sampled = SampleCore(wx, wy);

            // ---- spawn safety guard (playtest 2026-07-02, task #2) ----
            // No hazard surface tile (lava / fire_trap) may ever appear within ~12
            // cells past the spawn clearing, regardless of which generator branch
            // produced the cell. Defense-in-depth: no current branch emits hazards
            // on the overworld, but this clamp keeps that true as biomes evolve.
            int guard = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
            if (guard <= _cfg.spawnClearingRadius + 12 &&
                sampled.SurfaceBlockId != null &&
                IsoFoundationPlayer.HazardSurfaceBlocks.Contains(sampled.SurfaceBlockId))
            {
                var meadowB = (_meadowIndex >= 0 && _meadowIndex < _biomes.Count)
                    ? _biomes[_meadowIndex] : null;
                sampled.SurfaceBlockId = SurfaceVariant(meadowB, wx, wy, "grass_1", sampled.Height);
                sampled.Water = false;
            }
            return sampled;
        }

        IsoCell SampleCore(int wx, int wy)
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
                            Mathf.Clamp01(_cfg.flatWorldDecorationDensity), gHeight, surfaceId);
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
                cell.SurfaceBlockId = SurfaceVariant(meadowB, wx, wy, "grass_1", _cfg.spawnHeight);
                cell.Water = false;
                return cell;
            }

            // ---- elevation: base landmass + medium detail, lifted near the origin so
            //      the spawn region is always solid land (apron around the clearing) ----
            float e = ContinentElevation(wx, wy);

            float temp = Perlin(wx, wy, _cfg.climateFrequency, 1, 2);
            float moist = Perlin(wx, wy, _cfg.climateFrequency, 3, 4);
            bool inStarterPatch = TrySelectStarterBiome(wx, wy, out int starterBiomeIndex);
            bool starterMountain = inStarterPatch && starterBiomeIndex == _mountainIndex;

            // ---- ocean: one seamless navy field (the light family's edge highlights
            //      read as a grid when tiled - it stays reserved for rivers), with
            //      rulebook texture: speckled variants <= 15%, wave swells on the rim
            //      band, and foam-footed shore stones hugging the land edge ----
            if (!inStarterPatch && e < _cfg.continentShoreLevel)
            {
                bool deep = e < _cfg.continentDeepLevel;
                cell.Height = 0;
                cell.BiomeIndex = (byte)Mathf.Max(0, _beachIndex);
                float wr = Hash01(wx, wy, 98);
                bool rimBand = e > _cfg.continentShoreLevel - 0.025f;
                if (rimBand && wr < 0.12f)
                    cell.SurfaceBlockId = wr < 0.06f ? "water_swell_1" : "water_swell_2";
                else if (wr < 0.85f)
                    cell.SurfaceBlockId = "water_deep";
                else
                    cell.SurfaceBlockId = wr < 0.925f ? "water_deep_2" : "water_deep_3";
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
            // Rivers never carve through the spawn apron - water is impassable, so a
            // stream ringing the clearing would wall the player in at minute zero.
            bool outsideApron = clearing > _cfg.spawnClearingRadius + 6;
            bool isRiver = !inStarterPatch && belowRidge && outsideApron && riverDist < _cfg.riverHalfWidth;
            bool isBank = !inStarterPatch && belowRidge && !isRiver && riverDist < (_cfg.riverHalfWidth + _cfg.riverBankWidth);

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
                cell.SurfaceBlockId = "water_deep";   // use the darker ocean tile for rivers (owner request)
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
            bool isBeach = !inStarterPatch && e < _cfg.continentBeachLevel;
            if (isBeach || isBank)
            {
                var beachB = (_beachIndex >= 0 && _beachIndex < _biomes.Count) ? _biomes[_beachIndex] : null;
                cell.Height = 0;
                cell.BiomeIndex = (byte)Mathf.Max(0, _beachIndex);
                cell.SurfaceBlockId = SurfaceVariant(beachB, wx, wy, "sand_1", 1);
                cell.Water = false;
                // Sand carries only sparse rock outcrops — no trees/bushes on a beach.
                var beachRock = PickClusteredDecoration(wx, wy, beachB, 0.5f, 0, cell.SurfaceBlockId);
                if (beachRock.node != null && HasStableNodeGround(wx, wy, 0, beachRock.node, beachB))
                {
                    cell.NodeId = beachRock.node.id;
                    cell.NodeBlocks = beachRock.node.blocksMovement;
                }
                return cell;
            }

            // ---- land: climate picks the biome region; elevation steps the cliff height ----
            // Biome selection uses a SMOOTH low-frequency climate-elevation (not the detailed
            // terrain elevation e, which varies on every hill and made biomes flip into small
            // patches). Smooth t/m/climateElev fields => large coherent biome regions with
            // clean borders. Terrain HEIGHT/cliffs still use e (below) and the height>=3
            // mountain override is unchanged.
            float climateElev = Perlin(wx, wy, _cfg.climateFrequency, 17, 18);
            int biomeIndex = SelectBiome(temp, moist, climateElev);
            if (inStarterPatch)
                biomeIndex = starterBiomeIndex;
            // Minecraft-style rule: beach/sand exists only against water (the beach ring
            // and river banks above). If climate picks "beach" for an interior cell,
            // it becomes meadow instead - no sand patches popping up inland.
            if (biomeIndex == _beachIndex) biomeIndex = _meadowIndex;
            var biome = (biomeIndex >= 0 && biomeIndex < _biomes.Count) ? _biomes[biomeIndex] : null;

            int height = 1;
            if (e > _cfg.continentTier2Level) height = 2;
            if (e > _cfg.continentTier3Level) height = 3;
            if (e > _cfg.continentTier4Level) height = 4;
            if (e > _cfg.continentTier5Level) height = 5;
            if (e > _cfg.continentTier6Level) height = 6;
            if (e > _cfg.continentTier7Level) height = 7;
            height = Mathf.Clamp(height, 0, Mathf.Min(_cfg.maxHeight, 7));

            // Spawn apron: the land bias lifts elevation near the origin, which can
            // ring the flat clearing with tier-2+ cliff walls the player can't climb
            // (maxWalkStepHeight stays 0). Ramp the cap gently instead: first ring is
            // flush with the clearing, then at most one (jumpable) step per two cells.
            int pastClearing = clearing - _cfg.spawnClearingRadius;
            if (pastClearing > 0 && pastClearing <= 8)
            {
                int cap = Mathf.Clamp(_cfg.spawnHeight, 0, 7) + pastClearing / 2;
                if (height > cap) height = cap;
            }
            // The tour patches must remain readable even when a seed puts a ridge through
            // the ring. Climate patches stay traversable; the dedicated mountain patch is
            // raised to a proper rocky tier.
            if (inStarterPatch)
                height = starterMountain ? Mathf.Max(3, height) : Mathf.Min(2, height);
            // Mountain elevation gate (after apron clamp): height >= 3 overrides the
            // climate biome so peaks always look rocky regardless of temp/moisture.
            if (!inStarterPatch && height >= 3 && _mountainIndex >= 0)
            {
                biomeIndex = _mountainIndex;
                biome = _biomes[_mountainIndex];
            }

            cell.Height = (byte)height;
            cell.BiomeIndex = (byte)biomeIndex;
            cell.SurfaceBlockId = SurfaceVariant(biome, wx, wy, "dirt", height);
            string nativeSurfaceBlockId = cell.SurfaceBlockId;
            cell.Water = false;

            // Subtle border blend (thin 2-cell band, dithered — NOT a fuzzy gradient): if a
            // cell a couple of tiles away in x or y belongs to a different climate biome,
            // occasionally borrow one of that neighbour's flat tiles so the seam softens into
            // a clean transition instead of a hard line. Lowland only; two cheap neighbour
            // samples (E then S).
            if (_useBoxes && biome != null && height < 3)
            {
                const int blendBand = 2;
                int other = BiomeIndexAt(wx + blendBand, wy);
                if (other == biomeIndex) other = BiomeIndexAt(wx, wy + blendBand);
                if (other != biomeIndex && other >= 0 && other < _biomes.Count &&
                    _biomes[other].climatePriority >= 0 && Hash01(wx, wy, 41) < 0.22f)
                {
                    cell.SurfaceBlockId = SurfaceVariant(_biomes[other], wx, wy, cell.SurfaceBlockId, height);
                }
            }

            // Forest interiors: dense hedge/canopy blocks tile into forest MASS (the
            // pack's design - these are terrain, not props). Uses the same grove noise
            // as trees so canopy clumps wrap the tree clusters. Leafy 029 dominates;
            // striped 027/028 are accents. Canopy cells carry no props - they ARE the
            // vegetation. Lowland only; crag tiers stay bare.
            // Forest "canopy mass" tiles (canopy_1/2/3) were never imported as sprites, so
            // these cells rendered as GREEN placeholder cubes. Forest now reads through its
            // normal biome_suite tiles + tree props, so the canopy-tile override is disabled.
            bool canopyCell = false;

            // Clustered decoration (groves / outcrops / flower patches), reusing the
            // shared grouping logic so nothing scatters uniformly. Vegetation respects
            // the terrain: crag tops (tier 3+) are bare stone with only rock outcrops,
            // and trees never stand on a cliff lip where their canopy would float
            // over the stone face below.
            if (!canopyCell && biome != null && biome.nodes != null)
            {
                // Evaluate the tile the player will actually see after biome-edge blending.
                // A snow-biome rock must not be approved against hidden snow metadata when
                // the rendered transition tile is organic plains ground.
                var picked = PickClusteredDecoration(wx, wy, biome, 1f, height, cell.SurfaceBlockId);
                if (picked.node != null && HasStableNodeGround(wx, wy, height, picked.node, biome))
                {
                    cell.NodeId = picked.node.id;
                    cell.NodeBlocks = picked.node.blocksMovement;
                    if (!IsGeologyNode(picked.node.id))
                        cell.SurfaceBlockId = nativeSurfaceBlockId;
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
            // 3-octave FBM for large-scale continent shape
            float eBase   = Perlin(wx, wy, _cfg.continentFrequency,       11, 12);
            float eDetail = Perlin(wx, wy, _cfg.continentFrequency * 3f,  13, 14);
            float eFine   = Perlin(wx, wy, _cfg.continentFrequency * 7f,  15, 16);
            float e = eBase * 0.70f + eDetail * 0.20f + eFine * 0.10f;
            e += Mathf.Clamp01(1f - clearing / Mathf.Max(1f, _cfg.continentSpawnLandRadius))
                 * _cfg.continentSpawnLandBias;
            // Domain warp: twist the ridge sample coordinates so ridgelines meander
            float wFreq = _cfg.continentFrequency * 1.4f;
            float warpX = (Perlin(wx, wy, wFreq, 71, 72) - 0.5f) * 30f;
            float warpY = (Perlin(wx, wy, wFreq, 73, 74) - 0.5f) * 30f;
            // Ridged noise: narrow peaks, broad valleys — classic mountain silhouette
            float rRaw  = Perlin(wx + (int)warpX, wy + (int)warpY, _cfg.continentFrequency * 0.65f, 75, 76);
            float ridge = 1f - Mathf.Abs(rRaw * 2f - 1f);
            ridge = ridge * ridge; // sharpen the peak
            // Blend ridge in only where base elevation is already high (avoids flat-land spikes)
            float mountainBlend = Mathf.Clamp01((e - 0.52f) / 0.23f);
            e += ridge * mountainBlend * 0.30f;
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
            if (e > _cfg.continentTier5Level) h = 5;
            if (e > _cfg.continentTier6Level) h = 6;
            if (e > _cfg.continentTier7Level) h = 7;
            h = Mathf.Clamp(h, 0, Mathf.Min(_cfg.maxHeight, 7));
            int pastClearing = clearing - _cfg.spawnClearingRadius;
            if (pastClearing > 0 && pastClearing <= 8)
                h = Mathf.Min(h, Mathf.Clamp(_cfg.spawnHeight, 0, 7) + pastClearing / 2);
            return h;
        }

        bool TrySelectStarterBiome(int wx, int wy, out int biomeIndex)
        {
            biomeIndex = -1;
            if (!_cfg.guaranteeStarterBiomes || _starterBiomeIndices.Length == 0)
                return false;

            float ring = Mathf.Max(
                _cfg.starterBiomeRingRadius,
                _cfg.spawnClearingRadius + _cfg.starterBiomePatchRadius + 4f);
            float patchRadius = Mathf.Max(4f, _cfg.starterBiomePatchRadius);
            float rotation = (_seedHash & 1023u) / 1024f * Mathf.PI * 2f;
            for (int i = 0; i < _starterBiomeIndices.Length; i++)
            {
                int candidate = _starterBiomeIndices[i];
                if (candidate < 0) continue;
                float angle = rotation + i * Mathf.PI * 2f / _starterBiomeIndices.Length;
                float cx = Mathf.Cos(angle) * ring;
                float cy = Mathf.Sin(angle) * ring;
                float distance = Vector2.Distance(new Vector2(wx, wy), new Vector2(cx, cy));
                float edgeWarp = (Perlin(wx, wy, 0.045f, 111 + i * 2, 112 + i * 2) - 0.5f)
                                 * 2f * Mathf.Max(0f, _cfg.starterBiomeEdgeWarp);
                if (distance <= patchRadius + edgeWarp)
                {
                    biomeIndex = candidate;
                    return true;
                }
            }
            return false;
        }

        bool HasStableNodeGround(int wx, int wy, int height, ResourceNodeDefinition node,
            BiomeDefinition biome)
        {
            if (node == null) return false;
            bool needsClearance = node.blocksMovement ||
                                  node.FootprintWidth > 1 ||
                                  node.FootprintHeight > 1 ||
                                  node.widthUnits > 0.9f ||
                                  node.heightUnits > 0.9f;
            if (!needsClearance) return true;

            int clearance = Mathf.Max(1, biome?.propProfile?.blockingPropEdgeClearance ?? 1);
            for (int dy = -clearance; dy <= clearance; dy++)
            {
                for (int dx = -clearance; dx <= clearance; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = wx + dx;
                    int ny = wy + dy;
                    if (IsContinentWater(nx, ny) || ContinentTier(nx, ny) != height)
                        return false;
                }
            }
            return true;
        }

        bool IsContinentWater(int wx, int wy)
        {
            int clearing = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
            if (clearing <= _cfg.spawnClearingRadius)
                return false;

            float e = ContinentElevation(wx, wy);
            if (e < _cfg.continentShoreLevel)
                return true;

            float warpX = wx + (Perlin(wx, wy, _cfg.riverWarpFrequency, 51, 52) - 0.5f)
                          * 2f * _cfg.riverWarpAmplitude;
            float warpY = wy + (Perlin(wx, wy, _cfg.riverWarpFrequency, 53, 54) - 0.5f)
                          * 2f * _cfg.riverWarpAmplitude;
            float riverDist = Mathf.Abs(PerlinF(warpX, warpY, _cfg.riverFrequency, 55, 56) - 0.5f);
            return e < _cfg.riverMaxElevation &&
                   clearing > _cfg.spawnClearingRadius + 6 &&
                   riverDist < _cfg.riverHalfWidth;
        }

        static bool IsGeologyNode(string nodeId) =>
            nodeId == "rock" || nodeId == "copper_vein" || nodeId == "shore_stone";

        /// <summary>Picks a surface-block id. Priority order: accent clump →
        /// surfaceBands[height] (height-specific pool) → surfaceBasePool → surfaceGroup → fallback.</summary>
        string SurfaceVariant(BiomeDefinition biome, int wx, int wy, string fallback, int height = 0)
        {
            if (biome == null) return fallback;
            // Accent clumping: spatially grouped via low-freq noise; boosted 4x inside pockets.
            if (biome.accentRate > 0f &&
                biome.surfaceAccents != null && biome.surfaceAccents.Length > 0)
            {
                float clumpN = Perlin(wx, wy, 0.07f, 31, 32);
                float chance = biome.accentRate * (clumpN > 0.55f ? 4f : 0.15f);
                if (Hash01(wx, wy, 33) < chance)
                {
                    string acc = BiomeDefinition.PickWeighted(
                        biome.surfaceAccents, Hash01(wx, wy, 34), null);
                    if (acc != null) return acc;
                }
            }
            // Height band: per-tier pool overrides base pool when set
            if (biome.surfaceBands != null && height >= 0 && height < biome.surfaceBands.Length)
            {
                var band = biome.surfaceBands[height];
                if (band != null && band.Length > 0)
                    return BiomeDefinition.PickWeighted(band, Hash01(wx, wy, 7), fallback);
            }
            // Base pool: weighted random from the biome's curated tile set
            if (biome.surfaceBasePool != null && biome.surfaceBasePool.Length > 0)
                return BiomeDefinition.PickWeighted(biome.surfaceBasePool, Hash01(wx, wy, 7), fallback);
            // Legacy fallback: surfaceGroup variant
            if (biome.surfaceGroup != null)
            {
                var b = biome.surfaceGroup.GetVariant(Mathf.RoundToInt(Hash01(wx, wy, 7) * 1024));
                if (b != null) return b.id;
            }
            return fallback;
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
        BiomeNodeSpawn PickClusteredDecoration(int wx, int wy, BiomeDefinition biome,
            float density, int height, string surfaceBlockId)
        {
            if (biome?.nodes == null || biome.propProfile == null)
                return default;

            var surface = _blocks.Get(surfaceBlockId);
            TerrainSubstrate substrate = surface != null ? surface.substrate : TerrainSubstrate.None;
            var profile = biome.propProfile;
            bool nearWater = profile.nearWaterVegetationMultiplier > 1.001f &&
                             HasWaterWithin(wx, wy, 2);

            foreach (var spawn in biome.nodes)
            {
                var node = spawn.node;
                if (node == null || DecorationSpriteResolver.Resolve(node) == null)
                    continue;

                int category = PropCategory(node.id);
                TerrainSubstrate allowed = category == 2
                    ? profile.geologySubstrates
                    : profile.vegetationSubstrates;
                if ((allowed & substrate) == 0)
                    continue;

                float frequency;
                float threshold;
                if (category == 2)
                {
                    if (height < profile.minGeologyHeight || height > profile.maxGeologyHeight)
                        continue;
                    frequency = profile.geologyPatchFrequency;
                    threshold = profile.geologyPatchThreshold;
                }
                else if (category == 1)
                {
                    if (height > profile.maxTreeHeight)
                        continue;
                    frequency = profile.treePatchFrequency;
                    threshold = profile.treePatchThreshold;
                }
                else
                {
                    if (height > profile.maxGroundCoverHeight)
                        continue;
                    frequency = profile.groundCoverPatchFrequency;
                    threshold = profile.groundCoverPatchThreshold;
                }

                int salt = StableSalt(node.id);
                float group = Perlin(wx, wy, Mathf.Max(0.001f, frequency),
                    121 + salt % 211, 337 + salt % 197);
                if (group < threshold)
                    continue;

                float depth = Mathf.InverseLerp(threshold, 1f, group);
                float chance = Mathf.Max(0f, spawn.chancePerCell) * density *
                               Mathf.Lerp(0.45f, 1.8f, depth);
                if (category != 2 && nearWater)
                    chance *= profile.nearWaterVegetationMultiplier;

                if (Hash01(wx, wy, 557 + salt % 997) < Mathf.Clamp01(chance))
                    return spawn;
            }

            return default;
        }

        static int PropCategory(string nodeId)
        {
            if (IsGeologyNode(nodeId)) return 2;
            if (nodeId == "tree" || nodeId == "pine" || nodeId == "cactus" ||
                nodeId == "log" || nodeId == "stump") return 1;
            return 0;
        }

        static int StableSalt(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash & 0x7fffffff;
            }
        }

        bool HasWaterWithin(int wx, int wy, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if ((dx != 0 || dy != 0) && IsContinentWater(wx + dx, wy + dy))
                        return true;
            return false;
        }

        // Elevation-agnostic overload (flat/legacy world path). Uses a neutral mid
        // elevation so elevation-banded biomes still resolve sensibly.
        int SelectBiome(float t, float m) => SelectBiome(t, m, 0.5f);

        /// <summary>
        /// Picks a biome for a climate point. When biome_suite.json is applied, the
        /// climate RECTANGLE + priority table is authoritative: among biomes whose
        /// t/m/e rectangle contains the point, the highest climatePriority wins;
        /// biomes with priority &lt; 0 (beach/mountain/dropped) never win here; if nothing
        /// matches, the configured fallback biome is used. Without the suite it falls back
        /// to the original nearest-centroid behaviour.
        /// </summary>
        int SelectBiome(float t, float m, float e)
        {
            if (_biomes.Count == 0) return 0;

            if (_useBoxes)
            {
                int best = -1, bestPri = int.MinValue;
                for (int i = 0; i < _biomes.Count; i++)
                {
                    var b = _biomes[i];
                    if (b.climatePriority < 0) continue;            // structural / dropped
                    if (!b.MatchesClimate(t, m, e)) continue;
                    if (b.climatePriority > bestPri) { bestPri = b.climatePriority; best = i; }
                }
                if (best >= 0) return best;
                return _fallbackIndex >= 0 ? _fallbackIndex : 0;
            }

            int nb = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _biomes.Count; i++)
            {
                float d = _biomes[i].ClimateDistance(t, m);
                if (d < bestDist) { bestDist = d; nb = i; }
            }
            return nb;
        }

        /// <summary>Biome index at a cell using the same smooth climate fields as the land
        /// SelectBiome (temp salt 1/2, moist 3/4, climate-elevation 17/18). Used by the
        /// border blend to find the neighbouring biome across a seam.</summary>
        int BiomeIndexAt(int wx, int wy)
        {
            if (TrySelectStarterBiome(wx, wy, out int starterBiomeIndex))
                return starterBiomeIndex;
            float t = Perlin(wx, wy, _cfg.climateFrequency, 1, 2);
            float m = Perlin(wx, wy, _cfg.climateFrequency, 3, 4);
            float ce = Perlin(wx, wy, _cfg.climateFrequency, 17, 18);
            return SelectBiome(t, m, ce);
        }

        public BiomeDefinition BiomeAt(int index) =>
            (index >= 0 && index < _biomes.Count) ? _biomes[index] : null;
    }
}
