using System.Collections.Generic;
using UnityEngine;
using Purrville.World;

namespace Purrville.Colony
{
    /// <summary>Grid A* for prototype cats. Water is impassable; diagonal movement is disabled.</summary>
    public static class GridPathfinder
    {
        private static readonly Vector2Int[] Directions = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        public static List<Vector2Int> Find(ProceduralWorld world, Vector2Int start, Vector2Int goal)
        {
            if (!Walkable(world, start) || !Walkable(world, goal)) return null;
            var open = new List<Vector2Int> { start };
            var closed = new HashSet<Vector2Int>();
            var came = new Dictionary<Vector2Int, Vector2Int>();
            var g = new Dictionary<Vector2Int, int> { [start] = 0 };
            while (open.Count > 0)
            {
                int best = 0;
                int bestScore = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    var p = open[i];
                    int score = g[p] + Mathf.Abs(p.x - goal.x) + Mathf.Abs(p.y - goal.y);
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                var current = open[best];
                open.RemoveAt(best);
                if (current == goal)
                {
                    var result = new List<Vector2Int>();
                    while (current != start) { result.Add(current); current = came[current]; }
                    result.Reverse();
                    return result;
                }
                closed.Add(current);
                foreach (var d in Directions)
                {
                    var next = current + d;
                    if (!Walkable(world, next) || closed.Contains(next)) continue;
                    int tentative = g[current] + 1;
                    if (!g.TryGetValue(next, out int old) || tentative < old)
                    {
                        came[next] = current;
                        g[next] = tentative;
                        if (!open.Contains(next)) open.Add(next);
                    }
                }
            }
            return null;
        }

        private static bool Walkable(ProceduralWorld world, Vector2Int cell)
        {
            return world.TryGetTerrain(cell.x, cell.y, out var type) && type != TerrainType.Water;
        }
    }
}
