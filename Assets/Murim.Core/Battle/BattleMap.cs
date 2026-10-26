using System;
using System.Collections.Generic;
using Murim.Core.Data;
using Murim.Core.Manor;
using Murim.Core.World;

namespace Murim.Core.Battle
{
    /// <summary>장원 격자를 전투 지도로: 완공 건물은 막힘, 망루는 올라설 수 있는 칸. A* 8방향 길찾기.</summary>
    public class BattleMap
    {
        public int Width;
        public int Height;
        public bool[,] Blocked;
        public bool[,] Tower;

        public BattleMap(int width, int height)
        {
            Width = width;
            Height = height;
            Blocked = new bool[width, height];
            Tower = new bool[width, height];
        }

        public static BattleMap FromManor(WorldState w, GameData d, ManorState m)
        {
            var map = new BattleMap(m.Width, m.Height);
            foreach (var b in m.Buildings)
            {
                if (!ManorOps.IsComplete(w, b)) continue;
                var def = ManorOps.Def(d, b.DefId);
                if (def == null) continue;
                bool tower = b.DefId == "watchtower";
                for (int x = b.X; x < b.X + def.Width; x++)
                    for (int y = b.Y; y < b.Y + def.Height; y++)
                    {
                        if (!map.Inside(x, y)) continue;
                        if (tower) map.Tower[x, y] = true;
                        else map.Blocked[x, y] = true;
                    }
            }
            return map;
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool Walkable(int x, int y) => Inside(x, y) && !Blocked[x, y];

        static readonly (int dx, int dy)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

        /// <summary>시작 칸에서 목표 칸까지 (시작 칸 제외). 목표에 닿을 수 없으면 목표에 가장 가까운 닿는 칸까지. 시작 칸이 막혔어도 거기서 출발한다.</summary>
        public List<(int x, int y)> FindPath(int sx, int sy, int tx, int ty)
        {
            var result = new List<(int, int)>();
            if (!Inside(sx, sy)) return result;
            tx = Math.Max(0, Math.Min(Width - 1, tx));
            ty = Math.Max(0, Math.Min(Height - 1, ty));
            int n = Width * Height;
            var g = new float[n];
            var came = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++)
            {
                g[i] = float.MaxValue;
                came[i] = -1;
            }
            int start = sy * Width + sx;
            g[start] = 0;
            var open = new List<int> { start };
            int best = start;
            float bestH = Heuristic(sx, sy, tx, ty);
            while (open.Count > 0)
            {
                // 작은 격자라 선형 탐색으로 충분하다. 같은 값이면 먼저 들어온 칸 (결정론).
                int oi = 0;
                float of = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int c = open[i];
                    float f = g[c] + Heuristic(c % Width, c / Width, tx, ty);
                    if (f < of) { of = f; oi = i; }
                }
                int cur = open[oi];
                open.RemoveAt(oi);
                if (closed[cur]) continue;
                closed[cur] = true;
                int cx = cur % Width, cy = cur / Width;
                float h = Heuristic(cx, cy, tx, ty);
                if (h < bestH) { bestH = h; best = cur; }
                if (cx == tx && cy == ty) { best = cur; break; }
                foreach (var (dx, dy) in Dirs)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!Walkable(nx, ny)) continue;
                    if (dx != 0 && dy != 0 && (!Walkable(cx + dx, cy) || !Walkable(cx, cy + dy))) continue; // 모서리 통과 금지
                    int ni = ny * Width + nx;
                    if (closed[ni]) continue;
                    float ng = g[cur] + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                    if (ng >= g[ni]) continue;
                    g[ni] = ng;
                    came[ni] = cur;
                    open.Add(ni);
                }
            }
            for (int c = best; c != start && c >= 0; c = came[c]) result.Add((c % Width, c / Width));
            result.Reverse();
            return result;
        }

        static float Heuristic(int x, int y, int tx, int ty)
        {
            int dx = Math.Abs(x - tx), dy = Math.Abs(y - ty);
            return Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy);
        }
    }
}
