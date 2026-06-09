using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    public static class FoundationDungeonGenerator
    {
        const string FloorBlock = "dungeon_floor_1";
        const string WallBlock = "stone_block";
        const int BoundaryPadding = 1;
        const int MinDungeonSize = 48;
        const int MaxDungeonSize = 96;
        const int RoomOuterMargin = 3;
        const int RoomSeparation = 2;
        const double LoopConnectionChance = 0.35;
        static readonly string[] FloorBlocks =
        {
            "dungeon_floor_1",
            "dungeon_floor_2",
            "dungeon_floor_3",
            "dungeon_floor_4",
            "dungeon_floor_5",
        };

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
                    CorridorRadiusForTier(tier, rng));

                if (i > 2 && rng.NextDouble() < LoopConnectionChance)
                {
                    int linkIndex = rng.Next(0, i - 1);
                    Connect(floor, ToCell(rooms[i].center), ToCell(rooms[linkIndex].center), rng,
                        CorridorRadiusForTier(tier, rng));
                }
            }

            PickFarthestRoomPair(rooms, out var spawnLocal, out var exitLocal);
            var cells = BuildCells(floor, renderMin, renderMax);
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

        static void Connect(bool[,] floor, Vector2Int a, Vector2Int b, System.Random rng, int corridorRadius)
        {
            bool horizontalFirst = rng.NextDouble() < 0.5;
            if (horizontalFirst)
            {
                DigLine(floor, a.x, b.x, a.y, true, corridorRadius);
                DigLine(floor, a.y, b.y, b.x, false, corridorRadius);
            }
            else
            {
                DigLine(floor, a.y, b.y, a.x, false, corridorRadius);
                DigLine(floor, a.x, b.x, b.y, true, corridorRadius);
            }
        }

        static void DigLine(bool[,] floor, int from, int to, int fixedCoord, bool horizontal, int corridorRadius)
        {
            int min = Math.Min(from, to);
            int max = Math.Max(from, to);
            for (int v = min; v <= max; v++)
            {
                if (horizontal) SetFloor(floor, v, fixedCoord, corridorRadius);
                else SetFloor(floor, fixedCoord, v, corridorRadius);
            }
        }

        static int CorridorRadiusForTier(int tier, System.Random rng)
        {
            int radius = tier <= 1 ? 1 : 2;
            if (tier == 1 && rng.NextDouble() < 0.4)
                radius = 2;
            if (tier >= 4 && rng.NextDouble() < 0.35)
                radius++;
            return Mathf.Clamp(radius, 1, 3);
        }

        static void SetFloor(bool[,] floor, int x, int y, int radius)
        {
            int w = floor.GetLength(0);
            int h = floor.GetLength(1);
            for (int yy = y - radius; yy <= y + radius; yy++)
            for (int xx = x - radius; xx <= x + radius; xx++)
                if (xx > 0 && xx < w - 1 && yy > 0 && yy < h - 1)
                    floor[xx, yy] = true;
        }

        static FoundationSavedCell[] BuildCells(bool[,] floor, Vector2Int renderMin, Vector2Int renderMax)
        {
            var cells = new List<FoundationSavedCell>();
            int w = floor.GetLength(0);
            int h = floor.GetLength(1);

            for (int y = renderMin.y - BoundaryPadding; y <= renderMax.y + BoundaryPadding; y++)
            for (int x = renderMin.x - BoundaryPadding; x <= renderMax.x + BoundaryPadding; x++)
            {
                int lx = x - renderMin.x;
                int ly = y - renderMin.y;
                bool inRender = lx >= 0 && lx < w && ly >= 0 && ly < h;
                bool walkable = inRender && floor[lx, ly];
                bool solidBoundary = !walkable;
                cells.Add(new FoundationSavedCell
                {
                    x = x,
                    y = y,
                    height = 0,
                    biomeIndex = 0,
                    surfaceBlockId = walkable ? DungeonFloorBlock(x, y, lx, ly) : WallBlock,
                    occupantId = null,
                    nodeId = null,
                    solidBlock = solidBoundary,
                    water = false,
                    occupantBlocks = false,
                    nodeBlocks = false,
                    underBlockId = walkable ? null : FloorBlock,
                    underHeight = 0,
                });
            }

            return cells.ToArray();
        }

        static string DungeonFloorBlock(int worldX, int worldY, int localX, int localY)
        {
            unchecked
            {
                int h = worldX * 73856093 ^ worldY * 19349663 ^ localX * 83492791 ^ localY * 265443576;
                int index = (h & 0x7fffffff) % FloorBlocks.Length;
                return FloorBlocks[index];
            }
        }

        static Vector2Int[] BuildRenderCells(bool[,] floor, Vector2Int renderMin)
        {
            var cells = new List<Vector2Int>();
            int w = floor.GetLength(0);
            int h = floor.GetLength(1);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!floor[x, y])
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
                    y = renderMin.y + center.y,
                    width = Mathf.Max(3, rooms[i].width / 2),
                    height = Mathf.Max(3, rooms[i].height / 2),
                });
            }
        }

        static int Hash(int seed, string id, int x, int y, int tier)
        {
            unchecked
            {
                int h = seed;
                h = h * 397 ^ x;
                h = h * 397 ^ y;
                h = h * 397 ^ tier;
                if (!string.IsNullOrEmpty(id))
                    for (int i = 0; i < id.Length; i++)
                        h = h * 31 + id[i];
                return h;
            }
        }
    }
}
