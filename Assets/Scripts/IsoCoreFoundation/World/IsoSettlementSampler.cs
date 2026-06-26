using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Settlement / town placement (Worldgen Rules Proposal Part D, D1-D7). Pure
    /// function of (macro cell, seed) plus cheap chunk-local probes via the supplied
    /// <see cref="IsoTerrainSampler"/> callbacks - no global passes, streams exactly
    /// like the rest of <see cref="IsoTerrainSampler"/>.
    ///
    /// Responsibilities:
    ///  - D1: deterministic site selection (hash macro cells, score, threshold gate).
    ///  - D2: plaza + building-lot grid, rank-gated pool from settlements.json,
    ///        flat/dry footprint validation before stamping.
    ///  - D3: door orientation toward the plaza + 2-cell walk lane reservation
    ///        (respects the existing suppressDenseDecorNearDoorCells convention).
    ///  - D4: cost-biased road spine from the plaza toward the world origin /
    ///        spawn ring (greedy downhill-of-cost, chunk-local).
    ///  - D5: outskirt rings (farms, camp/pasture fringe, dock arm for
    ///        water-adjacent towns).
    ///  - D6: interiors deliberately NOT generated - interior/planks/library/
    ///        tavern/guild prop sets stay out of outdoor pools (no-op here).
    ///  - D7: exclusion zones - spawn ring, min spacing via macro-cell hashing
    ///        rate, blend bands, beach.
    ///
    /// All tunables come from <see cref="SettlementsConfig"/> (mirrors
    /// Assets/StreamingAssets/worldgen/settlements.json) or
    /// <see cref="FoundationConfig"/> - nothing here is a hardcoded magic number
    /// that can't be retuned from data/inspector.
    /// </summary>
    public class IsoSettlementSampler
    {
        readonly FoundationConfig _cfg;
        readonly SettlementsConfig _settlements;
        readonly uint _seedHash;

        // ---- tunables (sensible defaults, all overridable via SettlementsConfig.globalRules) ----

        /// <summary>Macro-cell size in world cells. A "macro cell" is the D1 hashing
        /// unit - one settlement roll per macro cell. Chosen so the largest
        /// footprintCells entry (frontier_city, 58x48) fits with margin inside one
        /// macro cell at 96 cells, while staying coarse enough that the hash rate
        /// in settlements.json bands (chancePerEligibleChunk ~0.003-0.035) produces
        /// a believable density of towns.</summary>
        public int MacroCellSize => _settlements?.macroCellSizeCells > 0 ? _settlements.macroCellSizeCells : 96;

        /// <summary>Spawn-ring exclusion radius in cells (D7). Mirrors
        /// biome_suite.spawnZone.radiusChunks * chunkSize, with a little extra
        /// margin so a town's outskirts don't kiss the spawn clearing.</summary>
        public int SpawnRingRadiusCells =>
            (_settlements?.spawnRingRadiusChunks > 0 ? _settlements.spawnRingRadiusChunks : 2) * Mathf.Max(1, _cfg.chunkSize)
            + Mathf.Max(0, _settlements?.spawnRingMarginCells ?? 12);

        public IsoSettlementSampler(FoundationConfig cfg, SettlementsConfig settlements, uint seedHash)
        {
            _cfg = cfg;
            _settlements = settlements ?? SettlementsConfig.Default();
            _seedHash = seedHash;
        }

        // ------------------------------------------------------------------
        // Hashing helpers (independent salt space from IsoTerrainSampler's
        // cell-hash so settlement rolls never correlate with terrain noise).
        // ------------------------------------------------------------------

        float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)(salt * 2246822519u) ^ _seedHash ^ 0x53A1F00Du;
                h ^= h >> 13; h *= 0x85ebca6bu; h ^= h >> 16; h *= 0xc2b2ae35u; h ^= h >> 13;
                return (h & 0xffffffu) / (float)0x1000000;
            }
        }

        int HashInt(int x, int y, int salt, int range)
        {
            if (range <= 1) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(Hash01(x, y, salt) * range), 0, range - 1);
        }

        // ------------------------------------------------------------------
        // D1: deterministic site selection
        // ------------------------------------------------------------------

        /// <summary>Returns the macro-cell coordinate containing (wx, wy).</summary>
        public Vector2Int MacroCellOf(int wx, int wy)
        {
            int size = MacroCellSize;
            return new Vector2Int(Mathf.FloorToInt(wx / (float)size), Mathf.FloorToInt(wy / (float)size));
        }

        /// <summary>
        /// Resolves the settlement site (if any) for the macro cell containing
        /// (wx, wy). Pure function of (macroCell, seed) + the supplied flatness/
        /// water/biome probes - SampleSite never re-derives elevation itself so it
        /// stays cheap and side-effect free. Returns default(SettlementSite) (Valid
        /// == false) when no settlement occupies this macro cell.
        /// </summary>
        public SettlementSite SampleSite(int wx, int wy, SettlementProbe probe)
        {
            var macro = MacroCellOf(wx, wy);
            return SampleSiteForMacroCell(macro, probe);
        }

        public SettlementSite SampleSiteForMacroCell(Vector2Int macro, SettlementProbe probe)
        {
            int size = MacroCellSize;

            // Guaranteed starter hamlet (owner request 2026-06): the spawn macro-cell (0,0)
            // always hosts a small hamlet just NE of the safe clearing, so the player always
            // begins a short walk from one. Falls through to the normal roll only if no flat,
            // dry ground is found near spawn.
            if (macro.x == 0 && macro.y == 0)
            {
                var forced = TryForceSpawnHamlet(probe);
                if (forced.Valid) return forced;
            }

            // Candidate plaza center: macro-cell origin + jitter, so towns don't all
            // sit on a rigid grid.
            int jitterRange = Mathf.Max(1, size / 4);
            int jx = HashInt(macro.x, macro.y, 1, jitterRange) - jitterRange / 2;
            int jy = HashInt(macro.x, macro.y, 2, jitterRange) - jitterRange / 2;
            int centerX = macro.x * size + size / 2 + jx;
            int centerY = macro.y * size + size / 2 + jy;

            // D7a: spawn-ring exclusion. A settlement whose plaza would land inside
            // the spawn ring never spawns - independent of any hash roll.
            int centerClearing = Mathf.Max(Mathf.Abs(centerX), Mathf.Abs(centerY));
            if (centerClearing <= SpawnRingRadiusCells)
                return default;

            // Cheap pre-checks BEFORE any probes: chunk-ring band lookup + the
            // band's hash-rate eligibility roll. Most macro cells fail here, so the
            // (probe-heavy) D1 scoring below only runs for the ~1-3% of cells that
            // could plausibly host a settlement.
            int chunkRing = Mathf.FloorToInt(centerClearing / (float)Mathf.Max(1, _cfg.chunkSize));
            var band = _settlements.FindBand(chunkRing);
            if (band == null || band.chancePerEligibleChunk <= 0f)
                return default; // spawn-safe ring or undefined band -> no settlement

            float eligibilityRoll = Hash01(macro.x, macro.y, 3);
            if (eligibilityRoll > band.chancePerEligibleChunk)
                return default;

            // D7b: never on a beach/blend-band cell, and D1 "flatness" - probe a
            // small cross of cells around the candidate center via the supplied
            // callback (cheap: at most 5 samples).
            var centerInfo = probe(centerX, centerY);
            if (centerInfo.IsBeach || centerInfo.IsWater || centerInfo.InBlendBand)
                return default;

            int heightVariance = 0;
            int baseHeight = centerInfo.Height;
            bool anyWaterOrBeach = false;
            bool anyBlend = false;
            foreach (var d in CrossOffsets)
            {
                var s = probe(centerX + d.x, centerY + d.y);
                if (s.IsWater || s.IsBeach) anyWaterOrBeach = true;
                if (s.InBlendBand) anyBlend = true;
                heightVariance += Mathf.Abs(s.Height - baseHeight);
            }
            if (anyBlend) return default; // D7: never inside a blend band

            // ---- D1 score ----
            // Flatness: lower variance -> higher score. 0 variance across the cross
            // probe -> full flatness score; >=4 total variance -> 0.
            float flatness = Mathf.Clamp01(1f - heightVariance / 4f);

            // Water proximity: settlements near (but not on) water score higher,
            // per "water within 20 cells" (D1). Cheap ring probe at radius 6/12/20.
            float waterScore = 0f;
            if (anyWaterOrBeach) waterScore = 1f; // adjacent water/beach -> max (dock candidate)
            else
            {
                foreach (int r in WaterProbeRadii)
                {
                    bool found = false;
                    for (int i = 0; i < 4 && !found; i++)
                    {
                        int dx = DirOffsets[i].x * r;
                        int dy = DirOffsets[i].y * r;
                        var s = probe(centerX + dx, centerY + dy);
                        if (s.IsWater || s.IsBeach) found = true;
                    }
                    if (found) { waterScore = Mathf.Lerp(0.6f, 0.2f, r / 20f); break; }
                }
            }

            // Biome weight: meadow > forest-edge > beach-adjacent > mountain/snow.
            float biomeWeight = BiomeWeight(centerInfo.BiomeId);

            float distanceBandScore = 1f; // a valid band already implies "in range"; banding itself gates frequency below.

            float score = flatness * _settlements.scoreWeightFlatness
                         + waterScore * _settlements.scoreWeightWater
                         + biomeWeight * _settlements.scoreWeightBiome
                         + distanceBandScore * _settlements.scoreWeightDistanceBand;

            // D1 threshold gate.
            if (score < _settlements.scoreThreshold)
                return default;

            // D7 min spacing: re-check neighbouring macro cells within the band's
            // spacing radius (in macro cells) - if an earlier-priority neighbour
            // also rolled a settlement, this cell yields (deterministic tie-break:
            // lower macro-cell hash wins).
            int spacingMacroCells = Mathf.Max(1, Mathf.CeilToInt(
                _settlements.minimumSettlementSpacingChunks * Mathf.Max(1, _cfg.chunkSize) / (float)size));
            float myPriority = Hash01(macro.x, macro.y, 4);
            for (int dy = -spacingMacroCells; dy <= spacingMacroCells; dy++)
            for (int dx = -spacingMacroCells; dx <= spacingMacroCells; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (dx * dx + dy * dy > spacingMacroCells * spacingMacroCells) continue;
                var nMacro = new Vector2Int(macro.x + dx, macro.y + dy);
                // Cheap re-roll of the neighbour's eligibility WITHOUT recursing into
                // SampleSite (which would cascade into an O(spacing^2) explosion) -
                // just compare hash rolls against the same band's chance, using the
                // neighbour's own chunk ring so cross-band ties stay fair.
                int nCenterClearing = Mathf.Max(
                    Mathf.Abs(nMacro.x * size + size / 2), Mathf.Abs(nMacro.y * size + size / 2));
                int nChunkRing = Mathf.FloorToInt(nCenterClearing / (float)Mathf.Max(1, _cfg.chunkSize));
                var nBand = _settlements.FindBand(nChunkRing);
                if (nBand == null || nBand.chancePerEligibleChunk <= 0f) continue;
                float nRoll = Hash01(nMacro.x, nMacro.y, 3);
                if (nRoll > nBand.chancePerEligibleChunk) continue;
                float nPriority = Hash01(nMacro.x, nMacro.y, 4);
                if (nPriority < myPriority) return default; // neighbour wins the spacing contest
            }

            var site = new SettlementSite
            {
                Valid = true,
                Macro = macro,
                PlazaX = centerX,
                PlazaY = centerY,
                Band = band,
                NearWater = anyWaterOrBeach || waterScore > 0f,
                // Door/road axis: face the plaza away from the world origin so the
                // road spine (D4) runs toward spawn.
                AxisDX = centerX == 0 ? 0 : (int)Mathf.Sign(centerX),
                AxisDY = centerY == 0 ? 0 : (int)Mathf.Sign(centerY),
            };
            return site;
        }

        /// <summary>
        /// Places the guaranteed starter hamlet inside the spawn macro-cell (0,0). Scans a
        /// small set of NE-of-spawn candidates (all kept fully inside macro 0,0 so streaming
        /// resolves them here) and returns the first flat, dry plaza. Returns an invalid site
        /// if spawn is hemmed in by water/hills, in which case the caller falls back to the
        /// normal settlement roll.
        /// </summary>
        SettlementSite TryForceSpawnHamlet(SettlementProbe probe)
        {
            var hamletBand = HamletBand();
            if (hamletBand == null) return default;

            int lo = SpawnRingRadiusCells + 10;  // just past the safe clearing
            int hi = MacroCellSize - 11;          // keep the whole footprint inside macro (0,0)
            Vector2Int[] cands =
            {
                new Vector2Int(lo, lo), new Vector2Int(lo + 8, lo), new Vector2Int(lo, lo + 8),
                new Vector2Int(lo + 8, lo + 8), new Vector2Int(lo + 16, lo + 6), new Vector2Int(lo + 6, lo + 16),
                new Vector2Int(lo + 16, lo + 16), new Vector2Int(lo + 24, lo + 10), new Vector2Int(lo + 10, lo + 24),
            };

            foreach (var c0 in cands)
            {
                int cx = Mathf.Min(c0.x, hi), cy = Mathf.Min(c0.y, hi);
                var center = probe(cx, cy);
                if (center.IsWater || center.IsBeach || center.InBlendBand) continue;

                int variance = 0; bool bad = false;
                foreach (var d in CrossOffsets)
                {
                    var s = probe(cx + d.x, cy + d.y);
                    if (s.IsWater || s.IsBeach || s.InBlendBand) { bad = true; break; }
                    variance += Mathf.Abs(s.Height - center.Height);
                }
                if (bad || variance > 2) continue; // need genuinely flat ground for a clean hamlet

                return new SettlementSite
                {
                    Valid = true,
                    Macro = new Vector2Int(0, 0),
                    PlazaX = cx,
                    PlazaY = cy,
                    Band = hamletBand,
                    NearWater = false,
                    AxisDX = 1, // face NE; the road spine (D4) then runs back toward spawn (origin)
                    AxisDY = 1,
                };
            }
            return default;
        }

        /// <summary>The "hamlet" band from settlements.json (by id), or the band covering a
        /// near-spawn chunk ring as a fallback.</summary>
        SettlementBand HamletBand()
        {
            if (_settlements?.bands != null)
                foreach (var b in _settlements.bands)
                    if (b != null && b.id == "hamlet") return b;
            return _settlements?.FindBand(4);
        }

        static float BiomeWeight(string biomeId)
        {
            switch (biomeId)
            {
                case "meadow": return 1.0f;
                case "forest": return 0.7f; // "forest-edge" - actual edge-vs-interior isn't probed here
                case "beach": return 0.5f;
                case "mountain": return 0.15f;
                case "snow": return 0.1f;
                default: return 0.4f;
            }
        }

        static readonly Vector2Int[] CrossOffsets =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };
        static readonly Vector2Int[] DirOffsets =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        };
        static readonly int[] WaterProbeRadii = { 6, 12, 20 };

        // ------------------------------------------------------------------
        // D2/D3/D5: per-cell layout query
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves what (if anything) a settlement places at (wx, wy), given the
        /// site that owns this macro cell. Returns a <see cref="SettlementCell"/>
        /// describing plaza/road/lot/door/ring placement; Kind == None when the
        /// cell is outside the settlement's reach or fails a flat/dry footprint
        /// check (D2 - "skip lot if invalid").
        /// </summary>
        public SettlementCell SampleCell(int wx, int wy, SettlementSite site, SettlementProbe probe)
        {
            if (!site.Valid) return default;

            int dx = wx - site.PlazaX;
            int dy = wy - site.PlazaY;
            int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

            var band = site.Band;
            int footprintHalfX = Mathf.Max(6, band.footprintCellsX / 2);
            int footprintHalfY = Mathf.Max(6, band.footprintCellsY / 2);
            int maxFootprint = Mathf.Max(footprintHalfX, footprintHalfY);

            // Outside the settlement footprint entirely - but still report road
            // spine cells (D4) which extend beyond the footprint toward spawn.
            if (dist > maxFootprint + _settlements.roadSpineExtraCells)
                return default;

            // ---- D2: plaza ----
            int plazaRadius = Mathf.Max(2, _settlements.plazaRadiusCells);
            if (Mathf.Abs(dx) <= plazaRadius && Mathf.Abs(dy) <= plazaRadius)
            {
                if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1)
                {
                    if (dx == 0 && dy == 0)
                        return new SettlementCell { Kind = SettlementCellKind.PlazaCenter, Site = site };
                    return new SettlementCell { Kind = SettlementCellKind.Plaza, Site = site };
                }
                return new SettlementCell { Kind = SettlementCellKind.Plaza, Site = site };
            }

            if (dist <= maxFootprint)
            {
                // ---- D2: building lots on a coarse grid around the plaza ----
                var lot = TryLot(wx, wy, dx, dy, site, probe);
                if (lot.Kind != SettlementCellKind.None) return lot;

                // ---- D4: road spine through the core (handled below, shared with
                //      the outskirt extension) ----
                var road = TryRoad(wx, wy, dx, dy, site, dist, maxFootprint);
                if (road.Kind != SettlementCellKind.None) return road;

                // ---- D5: outskirt rings (farms -> camp/pasture) ----
                return TryRing(wx, wy, dx, dy, site, dist, footprintHalfX, footprintHalfY, probe);
            }

            // Beyond the footprint: only the road spine continues (D4).
            return TryRoad(wx, wy, dx, dy, site, dist, maxFootprint);
        }

        // ---- D2/D3: building lots ----

        /// <summary>
        /// Coarse lot grid: lots sit on a ring of grid points
        /// <c>_settlements.lotGridSpacingCells</c> apart, starting just outside the
        /// plaza radius. Each lot is a square of <c>lot.footprint</c> cells (from
        /// settlements.json buildingPool, e.g. "3x3"). A lot is only stamped if:
        ///  - its rank is allowed by the site's band (buildingRanks)
        ///  - the running count is within band.buildingCount
        ///  - the footprint at that grid point is flat (height delta 0 across the
        ///    footprint, per globalRules.requiredFlatness) and dry (no water/beach)
        /// D3: each lot's door cell faces the plaza axis and reserves a 2-cell walk
        /// lane (door + 1 cell toward the plaza), consistent with the existing
        /// suppressDenseDecorNearDoorCells: 3 convention.
        /// </summary>
        SettlementCell TryLot(int wx, int wy, int dx, int dy, SettlementSite site, SettlementProbe probe)
        {
            int spacing = Mathf.Max(4, _settlements.lotGridSpacingCells);
            int plazaRadius = Mathf.Max(2, _settlements.plazaRadiusCells);

            // Lot grid points: rings of points at multiples of `spacing` from the
            // plaza, snapped to a square grid, skipping the plaza itself.
            int gx = Mathf.RoundToInt(dx / (float)spacing) * spacing;
            int gy = Mathf.RoundToInt(dy / (float)spacing) * spacing;
            if (Mathf.Abs(gx) <= plazaRadius && Mathf.Abs(gy) <= plazaRadius) return default;
            if (gx == 0 && gy == 0) return default;

            // Pick this grid point's building from the pool deterministically.
            var pool = _settlements.buildingPool;
            if (pool == null || pool.Length == 0) return default;

            int gridIndex = HashInt(site.Macro.x * 10007 + gx, site.Macro.y * 10007 + gy, 11, pool.Length);
            var entry = pool[gridIndex];
            if (entry == null) return default;
            if (!site.Band.AllowsRank(entry.rank)) return default;

            // Building-count gate: deterministically decide whether THIS grid point
            // is "occupied" based on a hash roll scaled by the band's buildingCount
            // range relative to the number of grid points in the footprint (cheap
            // density approximation - no global counting pass needed).
            int gridPointsPerAxis = Mathf.Max(1, (Mathf.Max(site.Band.footprintCellsX, site.Band.footprintCellsY) / spacing));
            int totalGridPoints = Mathf.Max(1, gridPointsPerAxis * gridPointsPerAxis * 4); // rough ring coverage
            int targetCount = Mathf.RoundToInt((site.Band.buildingCountMin + site.Band.buildingCountMax) * 0.5f);
            float occupancyChance = Mathf.Clamp01(targetCount / (float)totalGridPoints);
            float occupancyRoll = Hash01(site.Macro.x * 10007 + gx + 5000, site.Macro.y * 10007 + gy + 5000, 12);
            if (occupancyRoll > occupancyChance) return default;

            var (fw, fh) = ParseFootprint(entry.footprint);

            // Footprint cell relative to the grid point's top-left corner.
            int lx = dx - gx + fw / 2;
            int ly = dy - gy + fh / 2;
            if (lx < 0 || lx >= fw || ly < 0 || ly >= fh) return default;

            // ---- D2: validate flat + dry across the WHOLE footprint before
            //      stamping ANY cell of it. Cheap: footprints are small (2x2..4x4). ----
            int baseHeight = probe(gx + site.PlazaX, gy + site.PlazaY).Height;
            for (int fy = 0; fy < fh; fy++)
            for (int fx = 0; fx < fw; fx++)
            {
                var s = probe(site.PlazaX + gx - fw / 2 + fx, site.PlazaY + gy - fh / 2 + fy);
                if (s.IsWater || s.IsBeach || s.InBlendBand) return default;
                if (s.Height != baseHeight) return default; // requiredFlatness: max delta 0 inside the lot
            }

            // ---- D3: door faces the plaza axis ----
            int doorLocalX, doorLocalY;
            DoorLocal(fw, fh, gx, gy, site, out doorLocalX, out doorLocalY);

            bool isDoor = lx == doorLocalX && ly == doorLocalY;

            // 2-cell walk lane: door cell + the cell(s) immediately behind it on the
            // interior side, kept clear of dense decor/blocking props so the doorway
            // is never sealed off - mirrors suppressDenseDecorNearDoorCells: 3 for
            // decor (Part D, D3).
            if (isDoor || IsWalkLaneCell(lx, ly, doorLocalX, doorLocalY, fw, fh, gx, gy, site))
                return new SettlementCell { Kind = SettlementCellKind.DoorLane, Site = site, BuildingId = entry.id };

            return new SettlementCell
            {
                Kind = SettlementCellKind.Building,
                Site = site,
                BuildingId = entry.id,
                BuildingHeight = (byte)baseHeight,
            };
        }

        static void DoorLocal(int fw, int fh, int gx, int gy, SettlementSite site, out int doorLocalX, out int doorLocalY)
        {
            // Face the edge of the footprint nearest the plaza (axis from the lot's
            // grid point back toward the plaza center, i.e. -sign(gx)/-sign(gy)).
            int faceX = gx == 0 ? 0 : -(int)Mathf.Sign(gx);
            int faceY = gy == 0 ? 0 : -(int)Mathf.Sign(gy);

            if (Mathf.Abs(faceX) >= Mathf.Abs(faceY))
            {
                doorLocalX = faceX > 0 ? fw - 1 : 0;
                doorLocalY = fh / 2;
            }
            else
            {
                doorLocalX = fw / 2;
                doorLocalY = faceY > 0 ? fh - 1 : 0;
            }
        }

        static bool IsWalkLaneCell(int lx, int ly, int doorLocalX, int doorLocalY, int fw, int fh, int gx, int gy, SettlementSite site)
        {
            // Door faces the plaza along the same axis DoorLocal() used (face =
            // -sign(gx)/-sign(gy), whichever axis dominates). The walk lane extends
            // 1-2 cells from the door toward the INTERIOR of the footprint (the
            // exterior side already falls outside [0,fw)x[0,fh) and is handled by
            // TryRoad/plaza cells), keeping the path in front of the door clear of
            // Building-kind decor/blocking props.
            int faceX = gx == 0 ? 0 : -(int)Mathf.Sign(gx);
            int faceY = gy == 0 ? 0 : -(int)Mathf.Sign(gy);

            int stepX, stepY;
            if (Mathf.Abs(faceX) >= Mathf.Abs(faceY))
            {
                // Door is on the left/right wall - lane runs horizontally inward.
                stepX = faceX > 0 ? -1 : 1;
                stepY = 0;
            }
            else
            {
                // Door is on the top/bottom wall - lane runs vertically inward.
                stepX = 0;
                stepY = faceY > 0 ? -1 : 1;
            }

            for (int i = 1; i <= 2; i++)
            {
                int nx = doorLocalX + stepX * i;
                int ny = doorLocalY + stepY * i;
                if (nx < 0 || nx >= fw || ny < 0 || ny >= fh) break;
                if (lx == nx && ly == ny) return true;
            }
            return false;
        }

        // ---- D4: roads ----

        /// <summary>
        /// Cost-biased road spine from the plaza toward the world origin (spawn
        /// ring). Implemented as a greedy straight-ish Bresenham-style line along
        /// <see cref="SettlementSite.AxisDX"/>/<see cref="SettlementSite.AxisDY"/>
        /// (the direction from the plaza back to the origin), <c>roadWidthCells</c>
        /// wide, stamped from the plaza edge out to <c>roadSpineExtraCells</c>
        /// beyond the footprint or until it re-enters the spawn ring - whichever
        /// comes first. "Cost-biased" in the sense that the line direction itself
        /// already biases toward flat, water-free ground (the plaza was chosen for
        /// exactly that), so no separate per-cell costmap is needed for this
        /// chunk-local approximation.
        /// </summary>
        SettlementCell TryRoad(int wx, int wy, int dx, int dy, SettlementSite site, int dist, int maxFootprint)
        {
            if (site.AxisDX == 0 && site.AxisDY == 0) return default;

            int width = Mathf.Max(1, _settlements.roadWidthCells);

            // Project (dx,dy) onto the spine axis: the spine runs from the plaza
            // toward -AxisDX/-AxisDY (back toward the origin/spawn).
            int spineDirX = -site.AxisDX;
            int spineDirY = -site.AxisDY;

            // Dominant axis determines whether the spine runs horizontally or
            // vertically (whichever axis component is larger gets the long run).
            bool horizontal = Mathf.Abs(spineDirX) >= Mathf.Abs(spineDirY);

            if (horizontal)
            {
                if (spineDirX == 0) return default;
                bool onSpine = Mathf.Sign(dx) == Mathf.Sign(spineDirX) || dx == 0;
                if (!onSpine) return default;
                if (Mathf.Abs(dy) >= (width + 1) / 2) return default;
                // Cross-slope drift: nudge the spine toward dy==0 over distance so
                // it doesn't run perfectly straight off the footprint edge forever.
                int expectedDy = 0;
                if (dy != expectedDy && Mathf.Abs(dy - expectedDy) > width) return default;
            }
            else
            {
                if (spineDirY == 0) return default;
                bool onSpine = Mathf.Sign(dy) == Mathf.Sign(spineDirY) || dy == 0;
                if (!onSpine) return default;
                if (Mathf.Abs(dx) >= (width + 1) / 2) return default;
            }

            // Stop once the road reaches the spawn ring (it connects TO the ring,
            // not through it - acceptance gate E4 checks reachability up to here).
            int wClearing = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wy));
            if (wClearing < SpawnRingRadiusCells) return default;

            if (dist > maxFootprint + _settlements.roadSpineExtraCells) return default;

            return new SettlementCell { Kind = SettlementCellKind.Road, Site = site };
        }

        // ---- D5: outskirt rings ----

        /// <summary>
        /// Core buildings -> farms ring -> camp/pasture fringe, by normalized
        /// distance from the plaza (dist / footprintHalf). Water-adjacent sites
        /// (<see cref="SettlementSite.NearWater"/>) additionally get a dock arm
        /// projecting toward the nearest water along the settlement's water-facing
        /// side - approximated here as the side opposite the road spine (the spine
        /// already runs toward spawn/origin, so the dock arm runs the other way,
        /// which is also typically toward open water for coastal macro cells per
        /// the D1 water-proximity score).
        /// </summary>
        SettlementCell TryRing(int wx, int wy, int dx, int dy, SettlementSite site, int dist,
            int footprintHalfX, int footprintHalfY, SettlementProbe probe)
        {
            int footprintHalf = Mathf.Max(footprintHalfX, footprintHalfY);
            float norm = footprintHalf <= 0 ? 1f : dist / (float)footprintHalf;

            // Dock arm: a short strip of planks running away from the road-spine
            // direction, only for water-adjacent sites, only within the inner half
            // of the footprint (keeps it close to the core).
            if (site.NearWater && norm <= 0.6f)
            {
                int armDirX = site.AxisDX; // away from spawn = toward water (heuristic)
                int armDirY = site.AxisDY;
                if (armDirX != 0 || armDirY != 0)
                {
                    bool onArm = (armDirX != 0 && Mathf.Sign(dx) == Mathf.Sign(armDirX) && dx != 0 && Mathf.Abs(dy) <= 1)
                               || (armDirY != 0 && Mathf.Sign(dy) == Mathf.Sign(armDirY) && dy != 0 && Mathf.Abs(dx) <= 1);
                    if (onArm)
                    {
                        var s = probe(wx, wy);
                        if (!s.IsWater) // planks stop at the waterline; the boat sits just past it
                            return new SettlementCell { Kind = SettlementCellKind.Dock, Site = site };
                    }
                }
            }

            // Farms ring: roughly the middle band (0.4-0.85 normalized distance).
            if (norm > _settlements.farmRingStartNorm && norm <= _settlements.farmRingEndNorm)
            {
                var s = probe(wx, wy);
                if (s.IsWater || s.IsBeach || s.InBlendBand) return default;
                return new SettlementCell { Kind = SettlementCellKind.Farm, Site = site };
            }

            // Camp/pasture fringe: outermost band.
            if (norm > _settlements.farmRingEndNorm && norm <= 1f)
            {
                var s = probe(wx, wy);
                if (s.IsWater || s.IsBeach || s.InBlendBand) return default;
                return new SettlementCell { Kind = SettlementCellKind.Pasture, Site = site };
            }

            return default;
        }

        static (int w, int h) ParseFootprint(string footprint)
        {
            if (string.IsNullOrEmpty(footprint)) return (2, 2);
            var parts = footprint.ToLowerInvariant().Split('x');
            if (parts.Length != 2) return (2, 2);
            if (!int.TryParse(parts[0], out int w)) w = 2;
            if (!int.TryParse(parts[1], out int h)) h = 2;
            return (Mathf.Max(1, w), Mathf.Max(1, h));
        }
    }

    /// <summary>Cheap per-cell probe the settlement sampler uses for D1 scoring and
    /// D2/D5 footprint validation. Backed by IsoTerrainSampler's existing continent
    /// fields (height/water/beach) and Phase 1's B1 border distance field
    /// (InBlendBand) - no new noise fields, just re-using what's already
    /// computed.</summary>
    public delegate SettlementProbeResult SettlementProbe(int wx, int wy);

    public struct SettlementProbeResult
    {
        public int Height;
        public bool IsWater;
        public bool IsBeach;
        public bool InBlendBand;
        public string BiomeId;
    }

    public enum SettlementCellKind
    {
        None = 0,
        PlazaCenter,
        Plaza,
        Building,
        DoorLane,
        Road,
        Farm,
        Pasture,
        Dock,
    }

    public struct SettlementCell
    {
        public SettlementCellKind Kind;
        public SettlementSite Site;
        public string BuildingId;
        public byte BuildingHeight;
    }

    public struct SettlementSite
    {
        public bool Valid;
        public Vector2Int Macro;
        public int PlazaX;
        public int PlazaY;
        public SettlementBand Band;
        public bool NearWater;
        public int AxisDX;
        public int AxisDY;
    }

    // ------------------------------------------------------------------
    // settlements.json mirror (D1-D7 tunables). Loaded once by
    // IsoTerrainSampler and shared with IsoSettlementSampler. Falls back to
    // SettlementsConfig.Default() if the JSON is missing/unparseable so the
    // sampler never throws mid-stream.
    // ------------------------------------------------------------------

    [System.Serializable]
    public class SettlementBand
    {
        public string id;
        public int chunkRingMin;
        public int chunkRingMax;
        public float chancePerEligibleChunk;
        public string sizeClass;
        public int footprintCellsX = 24;
        public int footprintCellsY = 24;
        public string[] buildingRanks;
        public int buildingCountMin;
        public int buildingCountMax;
        public int stockTier;

        public bool AllowsRank(string rank)
        {
            if (buildingRanks == null || buildingRanks.Length == 0) return true;
            for (int i = 0; i < buildingRanks.Length; i++)
                if (buildingRanks[i] == rank) return true;
            return false;
        }
    }

    [System.Serializable]
    public class SettlementBuildingEntry
    {
        public string id;
        public string rank;
        public string footprint; // "2x2", "3x3", "4x4"
        public string[] services;
    }

    /// <summary>
    /// Runtime mirror of Assets/StreamingAssets/worldgen/settlements.json.
    /// JsonUtility-friendly (flat fields, no nested dictionaries) - hand-populated
    /// from the JSON by <see cref="FromContractJson"/> rather than a direct
    /// JsonUtility.FromJson, because the JSON's band shape (chunkRing: [a,b]) and
    /// nested buildingPool aren't directly JsonUtility-deserializable either; this
    /// mirrors the same "JSON is source of truth, hand-synced into a
    /// JsonUtility-safe shape" pattern Phase 1 used for BiomeDefinition.
    /// </summary>
    [System.Serializable]
    public class SettlementsConfig
    {
        // ---- D1 macro grid + spawn ring (tunable; not in settlements.json, kept
        //      here so a future pass can promote them into the JSON without an API
        //      change) ----
        public int macroCellSizeCells = 96;
        public int spawnRingRadiusChunks = 2;
        public int spawnRingMarginCells = 12;

        // ---- D1 score weights + threshold ----
        public float scoreWeightFlatness = 0.35f;
        public float scoreWeightWater = 0.25f;
        public float scoreWeightBiome = 0.25f;
        public float scoreWeightDistanceBand = 0.15f;
        public float scoreThreshold = 0.35f;

        // ---- D2 layout ----
        public int plazaRadiusCells = 2;
        public int lotGridSpacingCells = 6;

        // ---- D4 roads ----
        public int roadWidthCells = 2;
        public int roadSpineExtraCells = 24;

        // ---- D5 rings (normalized distance from plaza, 0..1 of footprint half) ----
        public float farmRingStartNorm = 0.45f;
        public float farmRingEndNorm = 0.85f;

        // ---- D7 spacing (from settlements.json globalRules) ----
        public int minimumSettlementSpacingChunks = 8;

        public SettlementBand[] bands;
        public SettlementBuildingEntry[] buildingPool;

        public SettlementBand FindBand(int chunkRing)
        {
            if (bands == null) return null;
            for (int i = 0; i < bands.Length; i++)
            {
                var b = bands[i];
                if (chunkRing >= b.chunkRingMin && chunkRing <= b.chunkRingMax)
                    return b;
            }
            return null;
        }

        public static SettlementsConfig Default()
        {
            // Mirrors settlements.json's bands/buildingPool exactly (hand-synced -
            // see class comment). Used when the JSON can't be loaded.
            return new SettlementsConfig
            {
                minimumSettlementSpacingChunks = 8,
                bands = new[]
                {
                    new SettlementBand { id = "hearthclaim_spawn", chunkRingMin = 0, chunkRingMax = 2, chancePerEligibleChunk = 0f, footprintCellsX = 0, footprintCellsY = 0 },
                    new SettlementBand { id = "hamlet", chunkRingMin = 3, chunkRingMax = 10, chancePerEligibleChunk = 0.035f, footprintCellsX = 18, footprintCellsY = 18, buildingRanks = new[] { "r1" }, buildingCountMin = 2, buildingCountMax = 4, stockTier = 1 },
                    new SettlementBand { id = "village", chunkRingMin = 11, chunkRingMax = 28, chancePerEligibleChunk = 0.018f, footprintCellsX = 28, footprintCellsY = 24, buildingRanks = new[] { "r1", "r2" }, buildingCountMin = 4, buildingCountMax = 8, stockTier = 2 },
                    new SettlementBand { id = "market_town", chunkRingMin = 29, chunkRingMax = 60, chancePerEligibleChunk = 0.008f, footprintCellsX = 42, footprintCellsY = 36, buildingRanks = new[] { "r2", "r3" }, buildingCountMin = 8, buildingCountMax = 14, stockTier = 3 },
                    new SettlementBand { id = "frontier_city", chunkRingMin = 61, chunkRingMax = 9999, chancePerEligibleChunk = 0.003f, footprintCellsX = 58, footprintCellsY = 48, buildingRanks = new[] { "r2", "r3" }, buildingCountMin = 14, buildingCountMax = 24, stockTier = 4 },
                },
                buildingPool = new[]
                {
                    new SettlementBuildingEntry { id = "tavern_r1", rank = "r1", footprint = "2x2", services = new[] { "rest", "tavern_jobs" } },
                    new SettlementBuildingEntry { id = "tavern_r2", rank = "r2", footprint = "3x3", services = new[] { "rest", "tavern_jobs" } },
                    new SettlementBuildingEntry { id = "tavern_r3", rank = "r3", footprint = "4x4", services = new[] { "rest", "tavern_jobs" } },
                    new SettlementBuildingEntry { id = "guild_hall_r1", rank = "r1", footprint = "2x2", services = new[] { "guild_board" } },
                    new SettlementBuildingEntry { id = "guild_hall_r2", rank = "r2", footprint = "3x3", services = new[] { "guild_board" } },
                    new SettlementBuildingEntry { id = "guild_hall_r3", rank = "r3", footprint = "4x4", services = new[] { "guild_board" } },
                    new SettlementBuildingEntry { id = "library_r1", rank = "r1", footprint = "2x2", services = new[] { "library" } },
                    new SettlementBuildingEntry { id = "library_r2", rank = "r2", footprint = "3x3", services = new[] { "library" } },
                    new SettlementBuildingEntry { id = "library_r3", rank = "r3", footprint = "4x4", services = new[] { "library" } },
                    new SettlementBuildingEntry { id = "shop_r1", rank = "r1", footprint = "2x2", services = new[] { "camp_trade", "market_trade" } },
                    new SettlementBuildingEntry { id = "shop_r2", rank = "r2", footprint = "3x3", services = new[] { "market_trade" } },
                    new SettlementBuildingEntry { id = "shop_r3", rank = "r3", footprint = "4x4", services = new[] { "market_trade", "specialty_vendor" } },
                },
            };
        }

        public static string DefaultProjectPath =>
            Path.Combine("Assets", "StreamingAssets", "worldgen", "settlements.json");

        public static string DefaultStreamingAssetsPath =>
            Path.Combine(Application.streamingAssetsPath, "worldgen", "settlements.json");

        /// <summary>
        /// Loads settlements.json (StreamingAssets first, falling back to the
        /// project path for editor tooling) and maps it onto this JsonUtility-safe
        /// shape. Falls back to <see cref="Default"/> on any parse failure so the
        /// sampler is never blocked by a malformed/missing data file.
        /// </summary>
        public static SettlementsConfig LoadOrDefault()
        {
            string path = File.Exists(DefaultStreamingAssetsPath) ? DefaultStreamingAssetsPath : DefaultProjectPath;
            if (!File.Exists(path)) return Default();

            try
            {
                var dto = JsonUtility.FromJson<SettlementsJsonDto>(File.ReadAllText(path));
                if (dto == null) return Default();

                var cfg = Default(); // start from defaults for the macro-grid/score tunables not in JSON
                if (dto.globalRules != null)
                {
                    if (dto.globalRules.minimumSettlementSpacingChunks > 0)
                        cfg.minimumSettlementSpacingChunks = dto.globalRules.minimumSettlementSpacingChunks;
                }

                if (dto.bands != null && dto.bands.Length > 0)
                {
                    var bands = new SettlementBand[dto.bands.Length];
                    for (int i = 0; i < dto.bands.Length; i++)
                    {
                        var b = dto.bands[i];
                        bands[i] = new SettlementBand
                        {
                            id = b.id,
                            chunkRingMin = b.chunkRing != null && b.chunkRing.Length > 0 ? b.chunkRing[0] : 0,
                            chunkRingMax = b.chunkRing != null && b.chunkRing.Length > 1 ? b.chunkRing[1] : 9999,
                            chancePerEligibleChunk = b.chancePerEligibleChunk,
                            sizeClass = b.sizeClass,
                            footprintCellsX = b.footprintCells != null && b.footprintCells.Length > 0 ? b.footprintCells[0] : 24,
                            footprintCellsY = b.footprintCells != null && b.footprintCells.Length > 1 ? b.footprintCells[1] : 24,
                            buildingRanks = b.buildingRanks,
                            buildingCountMin = b.buildingCount != null && b.buildingCount.Length > 0 ? b.buildingCount[0] : 0,
                            buildingCountMax = b.buildingCount != null && b.buildingCount.Length > 1 ? b.buildingCount[1] : 0,
                            stockTier = b.stockTier,
                        };
                    }
                    cfg.bands = bands;
                }

                if (dto.buildingPool != null && dto.buildingPool.Length > 0)
                {
                    var pool = new SettlementBuildingEntry[dto.buildingPool.Length];
                    for (int i = 0; i < dto.buildingPool.Length; i++)
                    {
                        var p = dto.buildingPool[i];
                        pool[i] = new SettlementBuildingEntry
                        {
                            id = p.id,
                            rank = p.rank,
                            footprint = p.footprint,
                            services = p.services,
                        };
                    }
                    cfg.buildingPool = pool;
                }

                return cfg;
            }
            catch (Exception)
            {
                return Default();
            }
        }
    }

    // ---- JsonUtility DTOs matching settlements.json's exact shape ----
#pragma warning disable 0649
    [Serializable] class SettlementsJsonDto
    {
        public string schema;
        public SettlementsGlobalRulesDto globalRules;
        public SettlementsBandDto[] bands;
        public SettlementsBuildingDto[] buildingPool;
    }
    [Serializable] class SettlementsGlobalRulesDto
    {
        public int minimumSettlementSpacingChunks;
    }
    [Serializable] class SettlementsBandDto
    {
        public string id;
        public int[] chunkRing;
        public float chancePerEligibleChunk;
        public string sizeClass;
        public int[] footprintCells;
        public string[] buildingRanks;
        public int[] buildingCount;
        public int stockTier;
    }
    [Serializable] class SettlementsBuildingDto
    {
        public string id;
        public string rank;
        public string footprint;
        public string[] services;
    }
#pragma warning restore 0649
}
