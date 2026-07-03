using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    public static class FoundationDungeonGenerator
    {
        // 2026-06-13 dungeon overhaul: switched the active palette from the flat
        // dungeon_floor_1..5 / stone_block placeholders to the promoted "dungeon2"
        // PixelLab tileset (Assets/Resources/Tiles/dungeon2_00..15). The old IDs
        // stay registered in FoundationContent for back-compat.
        const string FloorBlock = "dungeon2_00";
        const int BoundaryPadding = 1;
        const int MinDungeonSize = 48;
        const int MaxDungeonSize = 96;
        const int RoomOuterMargin = 3;
        const int RoomSeparation = 2;
        const double LoopConnectionChance = 0.35;

        // Plain stone floor variants (used everywhere).
        static readonly string[] FloorBlocks =
        {
            "dungeon2_00",
            "dungeon2_01",
            "dungeon2_03",
            "dungeon2_06",
            "dungeon2_09",
            "dungeon2_12",
        };

        // Mossy "ruins" floor variants — mixed in at higher tiers for room flavor.
        static readonly string[] RuinsFloorBlocks =
        {
            "dungeon2_08",
            "dungeon2_15",
        };

        // Plain stone wall variants.
        static readonly string[] WallBlocks =
        {
            "dungeon2_07",
            "dungeon2_11",
        };

        // Rune-sigil landmark wall, reserved for the spawn/exit rooms.
        const string LandmarkWallBlock = "dungeon2_02";

        // Lava / fire-trap hazard tiles, stamped into a fraction of non-spawn/exit
        // room floors as tier scales up.
        static readonly string[] HazardBlocks = { "lava", "fire_trap" };

        // World-cell Chebyshev radius around the origin that must stay hazard-free
        // (spawn clearing + apron; see IsoTerrainSampler spawn safety guard).
        const int SpawnSafeRadius = 24;

        // Sentinel surface block for cells outside the room/corridor + wall-ring
        // layout. Solid for collision but excluded from renderCells, so it never
        // draws — true empty space around the dungeon footprint.
        const string VoidBlock = "void";

        public static FoundationDungeonBuild Generate(FoundationContent content, string dungeonId,
            string displayName, int worldSeed, Vector2Int entranceCell, Vector2Int origin, int tier)
        {
            tier = Mathf.Clamp(tier, 1, 6);
            int layoutSeed = Hash(worldSeed, dungeonId, entranceCell.x, entranceCell.y, tier);
            var rng = new System.Random(layoutSeed);

            int size = Mathf.Clamp(38 + tier * 10, MinDungeonSize, MaxDungeonSize);
            var renderMin = new Vector2Int(origin.x - size / 2, origin.y - size / 2);
            var renderMax = new Vector2Int(renderMin.x + size - 1, renderMin.y + size - 1);
            bool[,] floor = new bool[size, size];
            var rooms = new List<RectInt>();

            int roomCount = Mathf.Clamp(6 + tier * 2, 8, 18);
            int roomMin = Mathf.Clamp(7 + tier / 2, 7, 10);
            int roomMax = Mathf.Clamp(12 + tier * 2, 14, 24);
            for (int attempt = 0; attempt < roomCount * 18 && rooms.Count < roomCount; attempt++)
            {
                int w = NextRoomDimension(rng, roomMin, roomMax, size);
                int h = NextRoomDimension(rng, roomMin, roomMax, size);
                int x = NextRoomPosition(rng, size, w);
                int y = NextRoomPosition(rng, size, h);
                var room = new RectInt(x, y, w, h);
                if (OverlapsAny(room, rooms)) continue;
                rooms.Add(room);
                StampRoom(floor, room);
            }

            if (rooms.Count == 0)
            {
                int fallbackSize = Mathf.Clamp(size / 4, 10, 16);
                var fallback = new RectInt(size / 2 - fallbackSize / 2, size / 2 - fallbackSize / 2,
                    fallbackSize, fallbackSize);
                rooms.Add(fallback);
                StampRoom(floor, fallback);
            }

            rooms.Sort((a, b) => a.center.x.CompareTo(b.center.x));
            for (int i = 1; i < rooms.Count; i++)
            {
                Connect(floor, ToCell(rooms[i - 1].center), ToCell(rooms[i].center), rng,
                    CorridorWidthForTier(tier, rng));

                if (i > 2 && rng.NextDouble() < LoopConnectionChance)
                {
                    int linkIndex = rng.Next(0, i - 1);
                    Connect(floor, ToCell(rooms[i].center), ToCell(rooms[linkIndex].center), rng,
                        CorridorWidthForTier(tier, rng));
                }
            }

            PickFarthestRoomPair(rooms, out var spawnLocal, out var exitLocal);
            var spawnRoom = RoomAt(rooms, spawnLocal);
            var exitRoom = RoomAt(rooms, exitLocal);
            var hazardCells = BuildHazardCells(rng, floor, rooms, spawnLocal, exitLocal, tier);
            var cells = BuildCells(floor, renderMin, renderMax, tier, spawnRoom, exitRoom, hazardCells);
            var renderCells = BuildRenderCells(floor, renderMin);
            var decorations = BuildDecorations(renderMin, exitLocal);
            var mobs = BuildMobs(rng, content, floor, rooms, renderMin, tier);
            var roomMarkers = BuildRoomMarkers(rooms, renderMin, spawnLocal, exitLocal);

            return new FoundationDungeonBuild
            {
                instanceId = $"dungeon_{dungeonId}_{layoutSeed:x8}",
                dungeonId = dungeonId,
                displayName = string.IsNullOrWhiteSpace(displayName) ? "Dungeon" : displayName,
                tier = tier,
                layoutSeed = layoutSeed,
                renderMin = renderMin,
                renderMax = renderMax,
                spawnCell = new Vector2Int(renderMin.x + spawnLocal.x, renderMin.y + spawnLocal.y),
                exitCell = new Vector2Int(renderMin.x + exitLocal.x, renderMin.y + exitLocal.y),
                renderCells = renderCells,
                cells = cells,
                decorations = decorations,
                mobs = mobs,
                roomMarkers = roomMarkers,
            };
        }

        static bool OverlapsAny(RectInt room, List<RectInt> rooms)
        {
            var padded = new RectInt(room.x - RoomSeparation, room.y - RoomSeparation,
                room.width + RoomSeparation * 2, room.height + RoomSeparation * 2);
            foreach (var existing in rooms)
                if (padded.Overlaps(existing))
                    return true;
            return false;
        }

        static int NextRoomDimension(System.Random rng, int min, int max, int dungeonSize)
        {
            int upper = Mathf.Min(max, dungeonSize - RoomOuterMargin * 2 - 2);
            int lower = Mathf.Clamp(min, 4, upper);
            return rng.Next(lower, upper + 1);
        }

        static int NextRoomPosition(System.Random rng, int dungeonSize, int roomSize)
        {
            int min = RoomOuterMargin;
            int maxInclusive = dungeonSize - roomSize - RoomOuterMargin;
            if (maxInclusive <= min)
                return Mathf.Clamp((dungeonSize - roomSize) / 2, 1, Mathf.Max(1, dungeonSize - roomSize - 1));
            return rng.Next(min, maxInclusive + 1);
        }

        static void StampRoom(bool[,] floor, RectInt room)
        {
            for (int y = room.yMin; y < room.yMax; y++)
            for (int x = room.xMin; x < room.xMax; x++)
                floor[x, y] = true;
        }

        static Vector2Int ToCell(Vector2 value) => new(Mathf.RoundToInt(value.x), Mathf.RoundToInt(value.y));

        static RectInt RoomAt(List<RectInt> rooms, Vector2Int centerCell)
        {
            foreach (var room in rooms)
                if (ToCell(room.center) == centerCell)
                    return room;
            return rooms[0];
        }

        static RectInt Expand(RectInt room, int by) =>
            new(room.x - by, room.y - by, room.width + by * 2, room.height + by * 2);

        /// <summary>
        /// Picks a tier-scaled set of interior floor cells inside non-spawn/non-exit
        /// rooms to become lava/fire-trap hazards (item 3 of the 2026-06-13 overhaul).
        /// Interior-only (1-cell margin from room walls) so traps never block doorways.
        /// </summary>
        static HashSet<Vector2Int> BuildHazardCells(System.Random rng, bool[,] floor, List<RectInt> rooms,
            Vector2Int spawnLocal, Vector2Int exitLocal, int tier)
        {
            var hazards = new HashSet<Vector2Int>();
            float density = Mathf.Clamp(0.03f + (tier - 1) * 0.006f, 0.03f, 0.06f);

            foreach (var room in rooms)
            {
                var center = ToCell(room.center);
                if (center == spawnLocal || center == exitLocal)
                    continue; // never trap the entrance or exit room

                for (int y = room.yMin + 1; y < room.yMax - 1; y++)
                for (int x = room.xMin + 1; x < room.xMax - 1; x++)
                {
                    if (!floor[x, y]) continue;
                    if (rng.NextDouble() < density)
                        hazards.Add(new Vector2Int(x, y));
                }
            }

            return hazards;
        }

        static void PickFarthestRoomPair(List<RectInt> rooms, out Vector2Int spawn, out Vector2Int exit)
        {
            spawn = ToCell(rooms[0].center);
            exit = spawn;
            int bestDistance = -1;

            for (int i = 0; i < rooms.Count; i++)
            for (int j = i + 1; j < rooms.Count; j++)
            {
                var a = ToCell(rooms[i].center);
                var b = ToCell(rooms[j].center);
                int distance = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
                if (distance <= bestDistance)
                    continue;

                bestDistance = distance;
                spawn = a;
                exit = b;
            }
        }

        // 2026-06-13: corridors used to be a direct 2-segment "L" between room
        // centers, which often produced very short connectors for nearby rooms.
        // Route through an offset midpoint instead, producing a longer 3-segment
        // "Z" / dogleg corridor with a deliberate jog away from the straight line.
        static void Connect(bool[,] floor, Vector2Int a, Vector2Int b, System.Random rng, int corridorWidth)
        {
            int w = floor.GetLength(0);
            int h = floor.GetLength(1);
            const int MinJog = 4;
            const int MaxJog = 12;
            int jog = MinJog + rng.Next(0, MaxJog - MinJog + 1);
            if (rng.NextDouble() < 0.5) jog = -jog;

            bool horizontalFirst = rng.NextDouble() < 0.5;
            if (horizontalFirst)
            {
                int midX = a.x + (b.x - a.x) / 2;
                int jogY = Mathf.Clamp(a.y + jog, 1, h - 2);
                DigLine(floor, a.x, midX, a.y, true, corridorWidth);
                DigLine(floor, a.y, jogY, midX, false, corridorWidth);
                DigLine(floor, midX, b.x, jogY, true, corridorWidth);
                DigLine(floor, jogY, b.y, b.x, false, corridorWidth);
            }
            else
            {
                int midY = a.y + (b.y - a.y) / 2;
                int jogX = Mathf.Clamp(a.x + jog, 1, w - 2);
                DigLine(floor, a.y, midY, a.x, false, corridorWidth);
                DigLine(floor, a.x, jogX, midY, true, corridorWidth);
                DigLine(floor, midY, b.y, jogX, false, corridorWidth);
                DigLine(floor, jogX, b.x, b.y, true, corridorWidth);
            }
        }

        static void DigLine(bool[,] floor, int from, int to, int fixedCoord, bool horizontal, int corridorWidth)
        {
            int min = Math.Min(from, to);
            int max = Math.Max(from, to);
            for (int v = min; v <= max; v++)
            {
                if (horizontal) SetFloor(floor, v, fixedCoord, corridorWidth);
                else SetFloor(floor, fixedCoord, v, corridorWidth);
            }
        }

        /// <summary>
        /// Corridor width in tiles, clamped to [2,4] per the 2026-06-13 spec
        /// (was an odd radius-based width of 3-7 before).
        /// </summary>
        static int CorridorWidthForTier(int tier, System.Random rng)
        {
            int width = tier <= 2 ? 2 : 3;
            if (tier >= 4 && rng.NextDouble() < 0.5)
                width = 4;
            return Mathf.Clamp(width, 2, 4);
        }

        // Stamps a (width x width) floor patch with its near corner at (x,y), so a
        // corridor traced along a line is exactly `width` tiles wide.
        static void SetFloor(bool[,] floor, int x, int y, int width)
        {
            int w = floor.GetLength(0);
            int h = floor.GetLength(1);
            for (int dy = 0; dy < width; dy++)
            for (int dx = 0; dx < width; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx > 0 && xx < w - 1 && yy > 0 && yy < h - 1)
                    floor[xx, yy] = true;
            }
        }

        /// <summary>True if any of the 8 neighbours (or the cell itself) of a local
        /// floor-array coordinate is a floor cell. Used to find the 1-cell wall ring
        /// around rooms/corridors — everything beyond that ring becomes void.</summary>
        static bool HasFloorNeighbor(bool[,] floor, int lx, int ly)
        {
            int w = floor.GetLength(0);
            int h = floor.GetLength(1);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = lx + dx, ny = ly + dy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (floor[nx, ny]) return true;
            }
            return false;
        }

        static FoundationSavedCell[] BuildCells(bool[,] floor, Vector2Int renderMin, Vector2Int renderMax,
            int tier, RectInt spawnRoom, RectInt exitRoom, HashSet<Vector2Int> hazardCells)
        {
            var cells = new List<FoundationSavedCell>();
            int w = floor.GetLength(0);
            int h = floor.GetLength(1);
            var landmarkSpawn = Expand(spawnRoom, 1);
            var landmarkExit = Expand(exitRoom, 1);

            for (int y = renderMin.y - BoundaryPadding; y <= renderMax.y + BoundaryPadding; y++)
            for (int x = renderMin.x - BoundaryPadding; x <= renderMax.x + BoundaryPadding; x++)
            {
                int lx = x - renderMin.x;
                int ly = y - renderMin.y;
                bool inRender = lx >= 0 && lx < w && ly >= 0 && ly < h;
                bool walkable = inRender && floor[lx, ly];

                string surfaceBlockId;
                string underBlockId;
                bool solidBlock;

                if (walkable)
                {
                    var local = new Vector2Int(lx, ly);
                    // Spawn safety (playtest 2026-07-02, task #2): never stamp a hazard
                    // surface within the world-spawn safe zone, even if a dungeon build
                    // ever overlaps the origin (origins live at 60000+ today; guard is
                    // defense-in-depth against future relocation).
                    bool inSpawnSafeZone = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) <= SpawnSafeRadius;
                    if (hazardCells.Contains(local) && !inSpawnSafeZone)
                    {
                        surfaceBlockId = HazardBlock(x, y);
                        underBlockId = DungeonFloorBlock(tier, x, y, lx, ly);
                    }
                    else
                    {
                        surfaceBlockId = DungeonFloorBlock(tier, x, y, lx, ly);
                        underBlockId = null;
                    }
                    solidBlock = false;
                }
                else if (HasFloorNeighbor(floor, lx, ly))
                {
                    // Wall ring immediately around a room/corridor — visible.
                    var local = new Vector2Int(lx, ly);
                    surfaceBlockId = (landmarkSpawn.Contains(local) || landmarkExit.Contains(local))
                        ? LandmarkWallBlock
                        : WallBlockFor(x, y);
                    underBlockId = FloorBlock;
                    solidBlock = true;
                }
                else
                {
                    // True empty space beyond the wall ring — never rendered (see
                    // BuildRenderCells), but still blocks movement for collision.
                    surfaceBlockId = VoidBlock;
                    underBlockId = null;
                    solidBlock = true;
                }

                cells.Add(new FoundationSavedCell
                {
                    x = x,
                    y = y,
                    height = 0,
                    biomeIndex = 0,
                    surfaceBlockId = surfaceBlockId,
                    occupantId = null,
                    nodeId = null,
                    solidBlock = solidBlock,
                    water = false,
                    occupantBlocks = false,
                    nodeBlocks = false,
                    underBlockId = underBlockId,
                    underHeight = 0,
                });
            }

            return cells.ToArray();
        }

        static string DungeonFloorBlock(int tier, int worldX, int worldY, int localX, int localY)
        {
            unchecked
            {
                int h = worldX * 73856093 ^ worldY * 19349663 ^ localX * 83492791 ^ localY * 265443576;
                int magnitude = h & 0x7fffffff;
                // Higher tiers mix in mossy "ruins" floor variants for ~15% of cells.
                if (tier >= 4 && magnitude % 100 < 15)
                    return RuinsFloorBlocks[magnitude % RuinsFloorBlocks.Length];
                return FloorBlocks[magnitude % FloorBlocks.Length];
            }
        }

        static string HazardBlock(int worldX, int worldY)
        {
            unchecked
            {
                int h = worldX * 374761393 ^ worldY * 668265263;
                int index = (h & 0x7fffffff) % HazardBlocks.Length;
                return HazardBlocks[index];
            }
        }

        static string WallBlockFor(int worldX, int worldY)
        {
            unchecked
            {
                int h = worldX * 73856093 ^ worldY * 19349663;
                int index = (h & 0x7fffffff) % WallBlocks.Length;
                return WallBlocks[index];
            }
        }

        // renderCells = everything that should actually be drawn: room/corridor floor
        // plus the 1-cell wall ring around it. Cells beyond that ring are "void" and
        // are deliberately excluded here so the ground renderer never shows them.
        static Vector2Int[] BuildRenderCells(bool[,] floor, Vector2Int renderMin)
        {
            var cells = new List<Vector2Int>();
            int w = floor.GetLength(0);
            int h = floor.GetLength(1);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!floor[x, y] && !HasFloorNeighbor(floor, x, y))
                    continue;

                cells.Add(new Vector2Int(renderMin.x + x, renderMin.y + y));
            }

            return cells.ToArray();
        }

        static FoundationDungeonDecoration[] BuildDecorations(Vector2Int renderMin, Vector2Int exitLocal)
        {
            var result = new List<FoundationDungeonDecoration>();
            AddDecoration(result, renderMin, exitLocal, FoundationDungeonDecoration.ExitPortalSpriteKey, 1.5f);
            return result.ToArray();
        }

        static void AddDecoration(List<FoundationDungeonDecoration> result, Vector2Int renderMin,
            Vector2Int local, string key, float height)
        {
            result.Add(new FoundationDungeonDecoration
            {
                spriteKey = key,
                x = renderMin.x + local.x,
                y = renderMin.y + local.y,
                heightUnits = height,
                yOffset = -0.08f,
            });
        }

        static FoundationDungeonMobSpawn[] BuildMobs(System.Random rng, FoundationContent content,
            bool[,] floor, List<RectInt> rooms, Vector2Int renderMin, int tier)
        {
            var result = new List<FoundationDungeonMobSpawn>();
            string[] mobIds = tier >= 4 ? new[] { "slime", "fox" } : new[] { "slime" };
            int count = Mathf.Clamp(4 + tier * 2, 6, 16);

            for (int i = 0; i < count; i++)
            {
                var room = rooms[rng.Next(rooms.Count)];
                var local = new Vector2Int(rng.Next(room.xMin, room.xMax), rng.Next(room.yMin, room.yMax));
                if (!floor[local.x, local.y]) continue;

                string mobId = mobIds[rng.Next(mobIds.Length)];
                if (content?.Mobs.Get(mobId) == null) continue;
                result.Add(new FoundationDungeonMobSpawn
                {
                    mobId = mobId,
                    x = renderMin.x + local.x,
                    y = renderMin.y + local.y,
                    level = Mathf.Max(1, tier),
                });
            }

            return result.ToArray();
        }

        static FoundationDungeonRoomMarker[] BuildRoomMarkers(List<RectInt> rooms, Vector2Int renderMin,
            Vector2Int spawnLocal, Vector2Int exitLocal)
        {
            var result = new List<FoundationDungeonRoomMarker>();
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                var center = ToCell(room.center);
                var kind = FoundationDungeonRoomKind.Combat;
                string label = "Combat";

                if (center == spawnLocal)
                {
                    kind = FoundationDungeonRoomKind.Spawn;
                    label = "Entrance";
                }
                else if (center == exitLocal)
                {
                    kind = FoundationDungeonRoomKind.Exit;
                    label = "Exit";
                }
                else if (room.width * room.height >= 150)
                {
                    kind = FoundationDungeonRoomKind.Arena;
                    label = "Arena";
                }

                result.Add(new FoundationDungeonRoomMarker
                {
                    kind = kind,
                    label = label,
                    x = renderMin.x + center.x,
                    y = renderMin.y + center.y,
                    width = room.width,
                    height = room.height,
                });
            }

            AddJunctionMarkers(result, rooms, renderMin, spawnLocal, exitLocal);
            return result.ToArray();
        }

        static void AddJunctionMarkers(List<FoundationDungeonRoomMarker> markers, List<RectInt> rooms,
            Vector2Int renderMin, Vector2Int spawnLocal, Vector2Int exitLocal)
        {
            if (rooms.Count < 4)
                return;

            int stride = Mathf.Max(2, rooms.Count / 4);
            for (int i = stride; i < rooms.Count - 1; i += stride)
            {
                var center = ToCell(rooms[i].center);
                if (center == spawnLocal || center == exitLocal)
                    continue;

                markers.Add(new FoundationDungeonRoomMarker
                {
                    kind = FoundationDungeonRoomKind.Junction,
                    label = "Junction",
                    x = renderMin.x + center.x,
            