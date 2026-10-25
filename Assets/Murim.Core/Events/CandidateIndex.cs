using System;
using System.Collections.Generic;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Events
{
    /// <summary>사건 단계가 시작될 때 한 번 만드는 후보 목록 (id 순).</summary>
    public class CandidateIndex
    {
        public readonly List<Character> Adults = new List<Character>();
        public readonly List<Character> Leaders = new List<Character>();
        public readonly List<Character> GrudgeHolders = new List<Character>();
        public readonly List<Character> DebtHolders = new List<Character>();
        readonly Dictionary<string, List<Character>> byRegion = new Dictionary<string, List<Character>>(StringComparer.Ordinal);
        static readonly List<Character> None = new List<Character>();

        public static CandidateIndex Build(WorldState w)
        {
            var x = new CandidateIndex();
            foreach (var c in w.Characters.Values)
            {
                if (!c.IsAlive || c.AgeYears(w.Month) < SimConstants.AdultAge) continue;
                x.Adults.Add(c);
                if (Wallet.LedFaction(w, c) != null) x.Leaders.Add(c);
                bool grudge = false, debt = false;
                foreach (var m in c.Memories)
                {
                    if (m.Kind == MemoryKind.Grudge) grudge = true;
                    else debt = true;
                }
                if (grudge) x.GrudgeHolders.Add(c);
                if (debt) x.DebtHolders.Add(c);
                var r = ConditionCheck.RegionOf(w, c);
                if (r == null) continue;
                if (!x.byRegion.TryGetValue(r, out var list)) x.byRegion[r] = list = new List<Character>();
                list.Add(c);
            }
            return x;
        }

        public IReadOnlyList<Character> InRegion(string regionId) =>
            regionId != null && byRegion.TryGetValue(regionId, out var list) ? list : None;

        public IReadOnlyList<Character> Root(string pick)
        {
            switch (pick)
            {
                case EventPicks.AnyAdult: return Adults;
                case EventPicks.Leader: return Leaders;
                case EventPicks.GrudgeHolder: return GrudgeHolders;
                case EventPicks.DebtHolder: return DebtHolders;
                default: return None;
            }
        }
    }
}
