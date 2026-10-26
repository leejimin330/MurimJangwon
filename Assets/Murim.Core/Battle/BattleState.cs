using System.Collections.Generic;
using System.Linq;
using Murim.Core.Random;

namespace Murim.Core.Battle
{
    /// <summary>한 번의 습격전. 0.1초 틱으로만 바뀐다.</summary>
    public class BattleState
    {
        public const float Dt = 0.1f;
        public const int MaxTicks = 1800;
        public const int MaxLogLines = 40;

        public BattleMap Map;
        public List<BattleUnit> Units = new List<BattleUnit>();
        public GameRandom Rng;
        public int Tick;
        public bool Over;
        public bool DefendersWon;
        public bool Routed;
        public bool TimedOut;
        public List<string> Log = new List<string>();
        /// <summary>습격자가 몰려가는 곳 (본당 앞).</summary>
        public float RallyX = 11.5f, RallyY = 9.5f;
        /// <summary>달아나는 곳 (정문).</summary>
        public float ExitX = 12f, ExitY = 15.5f;

        public BattleUnit Unit(int id) => id >= 0 && id < Units.Count ? Units[id] : null;

        public IEnumerable<BattleUnit> Side(BattleSide side) => Units.Where(u => u.Side == side);

        public float SecondsLeft => (MaxTicks - Tick) * Dt;

        public void AddLog(string line)
        {
            if (Log.Count < MaxLogLines) Log.Add(line);
        }
    }
}
