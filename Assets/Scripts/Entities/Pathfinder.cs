using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>A* over maze cells, bounded so a search in an endless maze always ends.</summary>
    public static class Pathfinder
    {
        private static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public static List<Vector2Int> Find(MazeField f, Vector2Int start, Vector2Int goal, int maxNodes = 2500)
        {
            if (start == goal) return new List<Vector2Int> { goal };
            var open = new SortedSet<(int f, int id)>();
            var came = new Dictionary<Vector2Int, Vector2Int>();
            var g = new Dictionary<Vector2Int, int>();
            var ids = new Dictionary<int, Vector2Int>();
            int next = 0;
            g[start] = 0;
            open.Add((H(start, goal), next)); ids[next++] = start;
            Vector2Int bestNode = start; int bestH = H(start, goal);
            int expanded = 0;
            while (open.Count > 0 && expanded < maxNodes)
            {
                var top = open.Min; open.Remove(top);
                var cur = ids[top.id];
                expanded++;
                if (cur == goal) return Build(came, cur);
                int h = H(cur, goal);
                if (h < bestH) { bestH = h; bestNode = cur; }
                foreach (var d in Dirs)
                {
                    var n = cur + d;
                    if (!f.Passable(cur, n)) continue;
                    int ng = g[cur] + 1;
                    if (g.TryGetValue(n, out int old) && old <= ng) continue;
                    g[n] = ng; came[n] = cur;
                    open.Add((ng + H(n, goal), next)); ids[next++] = n;
                }
            }
            // goal not reached within budget: head toward the closest cell we found
            return bestNode == start ? null : Build(came, bestNode);
        }

        private static int H(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static List<Vector2Int> Build(Dictionary<Vector2Int, Vector2Int> came, Vector2Int end)
        {
            var path = new List<Vector2Int> { end };
            while (came.TryGetValue(end, out var p)) { end = p; path.Add(end); }
            path.Reverse();
            return path;
        }

        /// <summary>Cells reachable from `start` within `steps` moves (breadth first).</summary>
        public static List<(Vector2Int cell, int dist)> Reachable(MazeField f, Vector2Int start, int steps, int cap = 3000)
        {
            var seen = new Dictionary<Vector2Int, int> { [start] = 0 };
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            var list = new List<(Vector2Int, int)>();
            while (q.Count > 0 && seen.Count < cap)
            {
                var c = q.Dequeue();
                int d = seen[c];
                list.Add((c, d));
                if (d >= steps) continue;
                foreach (var dir in Dirs)
                {
                    var n = c + dir;
                    if (seen.ContainsKey(n) || !f.Passable(c, n)) continue;
                    seen[n] = d + 1;
                    q.Enqueue(n);
                }
            }
            return list;
        }
    }
}
