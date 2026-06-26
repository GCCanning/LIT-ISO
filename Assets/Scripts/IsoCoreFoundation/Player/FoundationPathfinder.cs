using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Stateless A* over IsoWorld cells, used by click-to-move. Mirrors the player's
    /// walking constraints exactly: a cell is traversable only if IsoWorld.IsWalkable
    /// AND the height delta from the cell we are leaving is &lt;= maxWalkStepHeight
    /// (downhill is always free — same rule as IsoFoundationPlayer.Walkable). It never
    /// touches physics and never mutates the world, keeping the "ask the world" contract.
    ///
    /// 8-neighbour with no diagonal corner-cutting: a diagonal is rejected if BOTH of
    /// its shared orthogonal neighbours are blocked (so we never slip through a hard
    /// 1-cell gap between two walls/props). Node expansion is capped so a click into the
    /// void / an unreachable goal returns null quickly instead of hanging.
    /// </summary>
    public static class FoundationPathfinder
    {
        // 8 neighbours: 4 orthogonal first, then 4 diagonals.
        static readonly Vector2Int[] Dirs =
        {
            new Vector2Int( 1,  0), new Vector2Int(-1,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int( 1,  1), new Vector2Int( 1, -1),
            new Vector2Int(-1,  1), new Vector2Int(-1, -1),
        };

        /// <summary>
        /// A* from start to goal over walkable cells. Returns the cell list start..goal
        /// (inclusive) or null if no path is found within maxNodes expansions, if the
        /// goal is unwalkable, or if start == goal (nothing to do).
        /// </summary>
        public static List<Vector2Int> FindPath(
            IsoWorld world, Vector2Int start, Vector2Int goal,
            int maxWalkStepHeight, int maxNodes = 4000)
        {
            if (world == null) return null;
            if (start == goal) return null;
            if (!world.IsWalkable(goal.x, goal.y)) return null;
            // A prop (tree/rock/placeable) is a hard blocker from every direction, so a
            // click on one is never a valid destination even if the cell reads walkable.
            if (world.IsPropBlocked(goal.x, goal.y)) return null;

            int step = Mathf.Max(0, maxWalkStepHeight);

            var open = new List<Vector2Int>();           // simple open set (small for short hops)
            var gScore = new Dictionary<Vector2Int, float>();
            var fScore = new Dictionary<Vector2Int, float>();
            var came = new Dictionary<Vector2Int, Vector2Int>();
            var closed = new HashSet<Vector2Int>();

            open.Add(start);
            gScore[start] = 0f;
            fScore[start] = Heuristic(start, goal);

            int expanded = 0;
            while (open.Count > 0)
            {
                // Pull the lowest-f node from the open set.
                int bestIdx = 0;
                float bestF = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    float f = fScore.TryGetValue(open[i], out var v) ? v : float.MaxValue;
                    if (f < bestF) { bestF = f; bestIdx = i; }
                }
                Vector2Int current = open[bestIdx];
                if (current == goal) return Reconstruct(came, current);

                open.RemoveAt(bestIdx);
                closed.Add(current);

                if (++expanded > maxNodes) return null; // unreachable / void click — bail fast

                int curH = world.GetHeight(current.x, current.y);
                float curG = gScore.TryGetValue(current, out var cg) ? cg : float.MaxValue;

                for (int d = 0; d < Dirs.Length; d++)
                {
                    Vector2Int n = current + Dirs[d];
                    if (closed.Contains(n)) continue;
                    if (!Traversable(world, current, n, curH, step)) continue;

                    bool diagonal = d >= 4;
                    if (diagonal)
                    {
                        // No corner-cutting: forbid the diagonal if both shared
                        // orthogonal cells are blocked (can't squeeze through a 1-cell
                        // gap between two walls/props).
                        var a = new Vector2Int(n.x, current.y);
                        var b = new Vector2Int(current.x, n.y);
                        bool aBlocked = !world.IsWalkable(a.x, a.y) || world.IsPropBlocked(a.x, a.y);
                        bool bBlocked = !world.IsWalkable(b.x, b.y) || world.IsPropBlocked(b.x, b.y);
                        if (aBlocked && bBlocked) continue;
                    }

                    float stepCost = diagonal ? 1.41421356f : 1f;
                    float tentative = curG + stepCost;
                    float known = gScore.TryGetValue(n, out var ng) ? ng : float.MaxValue;
                    if (tentative >= known) continue;

                    came[n] = current;
                    gScore[n] = tentative;
                    fScore[n] = tentative + Heuristic(n, goal);
                    if (!open.Contains(n)) open.Add(n);
                }
            }
            return null;
        }

        /// <summary>
        /// Can we walk from `from` into `to`? Matches IsoFoundationPlayer.Walkable's land
        /// rules: target must be walkable and not a hard prop; height may rise at most
        /// `maxWalkStepHeight` from the cell we are leaving (descending is always free).
        /// </summary>
        static bool Traversable(IsoWorld world, Vector2Int from, Vector2Int to, int fromH, int maxStep)
        {
            if (!world.IsWalkable(to.x, to.y)) return false;
            if (world.IsPropBlocked(to.x, to.y)) return false;
            int toH = world.GetHeight(to.x, to.y);
            if (toH <= fromH) return true;          // level or downhill
            return toH - fromH <= maxStep;          // small step-up only (taller needs a jump)
        }

        // Octile distance: admissible for an 8-neighbour grid with diagonal cost ~1.414.
        static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            int min = Mathf.Min(dx, dy);
            int max = Mathf.Max(dx, dy);
            return (max - min) + 1.41421356f * min;
        }

        static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> came, Vector2Int current)
        {
            var path = new List<Vector2Int> { current };
            while (came.TryGetValue(current, out var prev))
            {
                current = prev;
                path.Add(current);
            }
            path.Reverse(); // start .. goal
            return path;
        }
    }
}
