using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    internal enum FoundationInteriorAnchorKind
    {
        Floor,
        Wall,
        Rug
    }

    internal sealed class FoundationInteriorPropPlacement
    {
        public string spriteKey;
        public string displayName;
        public Vector2Int cell;
        public float yOffset;
        public bool blocksMovement;
        public int footprintW = 1;
        public int footprintH = 1;
        public int sortingOffset;
        public FoundationInteriorAnchorKind anchor;

        public FoundationInteriorPropPlacement(string spriteKey, string displayName, int x, int y,
            bool blocksMovement, int footprintW = 1, int footprintH = 1, float yOffset = -0.04f,
            FoundationInteriorAnchorKind anchor = FoundationInteriorAnchorKind.Floor, int sortingOffset = 0)
        {
            this.spriteKey = spriteKey;
            this.displayName = displayName;
            cell = new Vector2Int(x, y);
            this.blocksMovement = blocksMovement;
            this.footprintW = Mathf.Max(1, footprintW);
            this.footprintH = Mathf.Max(1, footprintH);
            this.yOffset = yOffset;
            this.anchor = anchor;
            this.sortingOffset = sortingOffset;
        }
    }

    internal sealed class FoundationInteriorLayout
    {
        public string id;
        public string displayName;
        public string floorBlockId;
        public Vector2Int spawnCell;
        public Vector2Int exitCell;
        public int wallStackLevels = 2;
        public readonly HashSet<Vector2Int> floorTiles = new();
        public readonly HashSet<Vector2Int> reservedWalkTiles = new();
        public readonly List<FoundationInteriorPropPlacement> props = new();

        public Vector2Int ToWorld(Vector2Int origin, Vector2Int rel) =>
            new(origin.x + rel.x - spawnCell.x, origin.y + rel.y - spawnCell.y);

        public static FoundationInteriorLayout TavernHearthSnug()
        {
            var layout = new FoundationInteriorLayout
            {
                id = "tavern_hearth_snug_v1",
                displayName = "Tavern",
                floorBlockId = "wood_floor",
                spawnCell = new Vector2Int(10, 13),
                exitCell = new Vector2Int(10, 13),
                wallStackLevels = 2
            };

            AddRect(layout.floorTiles, 2, 14, 4, 11);
            AddRect(layout.floorTiles, 5, 15, 0, 5);
            AddRect(layout.floorTiles, 0, 5, 1, 6);
            AddRect(layout.floorTiles, 8, 12, 11, 13);
            AddRect(layout.floorTiles, 14, 17, 5, 9);
            RemoveRect(layout.floorTiles, 0, 1, 5, 6);
            RemoveRect(layout.floorTiles, 15, 17, 9, 11);
            RemoveRect(layout.floorTiles, 2, 3, 10, 11);

            AddLine(layout.reservedWalkTiles, 10, 3, 10, 13);
            AddLine(layout.reservedWalkTiles, 3, 3, 11, 3);
            AddLine(layout.reservedWalkTiles, 3, 6, 17, 6);
            AddLine(layout.reservedWalkTiles, 3, 4, 3, 10);
            AddLine(layout.reservedWalkTiles, 8, 11, 12, 11);
            layout.reservedWalkTiles.RemoveWhere(c => !layout.floorTiles.Contains(c));

            layout.props.Add(new FoundationInteriorPropPlacement("tavern_fireplace_v2", "Hearth", 2, 1,
                true, 2, 1, -0.03f, FoundationInteriorAnchorKind.Wall));
            layout.props.Add(new FoundationInteriorPropPlacement("tavern_back_bar_v2", "Bottle shelf", 8, 1,
                true, 3, 1, -0.04f, FoundationInteriorAnchorKind.Wall));
            layout.props.Add(new FoundationInteriorPropPlacement("tavern_bar_counter_v2", "Bar counter", 8, 2,
                true, 3, 1, -0.05f));
            layout.props.Add(new FoundationInteriorPropPlacement("tavern_barrel_stack_v2", "Barrel stack", 13, 2,
                true, 2, 2, -0.04f));
            layout.props.Add(new FoundationInteriorPropPlacement("tavern_large_rug_v2", "Common room rug", 8, 7,
                false, 4, 2, -0.12f, FoundationInteriorAnchorKind.Rug, -8));
            layout.props.Add(new FoundationInteriorPropPlacement("tavern_feast_table_v2", "Feast table", 8, 7,
                true, 3, 1, -0.05f));
            layout.props.Add(new FoundationInteriorPropPlacement("tavern_round_table_food_v2", "Round table", 5, 9,
                true, 2, 2, -0.05f));
            layout.props.Add(new FoundationInteriorPropPlacement("tavern_round_table_food_v2", "Snug table", 15, 8,
                true, 2, 2, -0.05f));
            layout.props.Add(new FoundationInteriorPropPlacement("wood_bench_row_v2", "Bench", 5, 5,
                true, 3, 1, -0.05f));
            layout.props.Add(new FoundationInteriorPropPlacement("wood_bench_row_v2", "Bench", 12, 10,
                true, 3, 1, -0.05f));

            return layout;
        }

        static void AddRect(HashSet<Vector2Int> cells, int minX, int maxX, int minY, int maxY)
        {
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                cells.Add(new Vector2Int(x, y));
        }

        static void RemoveRect(HashSet<Vector2Int> cells, int minX, int maxX, int minY, int maxY)
        {
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                cells.Remove(new Vector2Int(x, y));
        }

        static void AddLine(HashSet<Vector2Int> cells, int x0, int y0, int x1, int y1)
        {
            int dx = x1.CompareTo(x0);
            int dy = y1.CompareTo(y0);
            int x = x0;
            int y = y0;
            while (true)
            {
                cells.Add(new Vector2Int(x, y));
                if (x == x1 && y == y1) break;
                if (x != x1) x += dx;
                if (y != y1) y += dy;
            }
        }
    }

    internal static class FoundationInteriorLayoutValidator
    {
        static readonly Vector2Int[] Cardinal =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
        };

        public static bool TryValidate(FoundationInteriorLayout layout, out string message)
        {
            if (layout == null)
            {
                message = "Layout is null.";
                return false;
            }

            var errors = new List<string>();
            if (!layout.floorTiles.Contains(layout.spawnCell))
                errors.Add($"Spawn cell {layout.spawnCell} is not floor.");
            if (!layout.floorTiles.Contains(layout.exitCell))
                errors.Add($"Exit cell {layout.exitCell} is not floor.");

            var blocked = new HashSet<Vector2Int>();
            foreach (var prop in layout.props)
            {
                if (prop == null || !prop.blocksMovement)
                    continue;

                foreach (var cell in Footprint(prop.cell, prop.footprintW, prop.footprintH))
                {
                    if (!layout.floorTiles.Contains(cell))
                        errors.Add($"{prop.displayName} footprint cell {cell} is outside the floor mask.");
                    if (layout.reservedWalkTiles.Contains(cell))
                        errors.Add($"{prop.displayName} blocks reserved walk tile {cell}.");
                    blocked.Add(cell);
                }
            }

            var reachable = Flood(layout.spawnCell, layout.floorTiles, blocked);
            if (!reachable.Contains(layout.exitCell))
                errors.Add("Exit is not reachable from spawn.");

            foreach (var lane in layout.reservedWalkTiles)
            {
                if (!layout.floorTiles.Contains(lane))
                    errors.Add($"Reserved walk tile {lane} is not floor.");
                else if (!reachable.Contains(lane))
                    errors.Add($"Reserved walk tile {lane} is not reachable.");
            }

            foreach (var prop in layout.props)
            {
                if (prop == null || !prop.blocksMovement)
                    continue;

                bool hasAccess = false;
                foreach (var cell in Footprint(prop.cell, prop.footprintW, prop.footprintH))
                foreach (var dir in Cardinal)
                {
                    var access = cell + dir;
                    if (reachable.Contains(access))
                    {
                        hasAccess = true;
                        break;
                    }
                }

                if (!hasAccess)
                    errors.Add($"{prop.displayName} has no reachable adjacent access tile.");
            }

            message = errors.Count == 0 ? "OK" : string.Join("; ", errors);
            return errors.Count == 0;
        }

        public static IEnumerable<Vector2Int> Footprint(Vector2Int center, int width, int height)
        {
            int minX = center.x - Mathf.Max(1, width) / 2;
            int minY = center.y - Mathf.Max(1, height) / 2;
            for (int y = minY; y < minY + Mathf.Max(1, height); y++)
            for (int x = minX; x < minX + Mathf.Max(1, width); x++)
                yield return new Vector2Int(x, y);
        }

        static HashSet<Vector2Int> Flood(Vector2Int start, HashSet<Vector2Int> floor, HashSet<Vector2Int> blocked)
        {
            var reached = new HashSet<Vector2Int>();
            if (!floor.Contains(start) || blocked.Contains(start))
                return reached;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            reached.Add(start);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var dir in Cardinal)
                {
                    var n = c + dir;
                    if (!floor.Contains(n) || blocked.Contains(n) || reached.Contains(n))
                        continue;
                    reached.Add(n);
                    queue.Enqueue(n);
                }
            }

            return reached;
        }
    }
}
