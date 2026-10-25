using System;
using System.Linq;
using Murim.Core.World;

namespace Murim.Core.Rules
{
    /// <summary>지역 영향력 증감. 한 지역의 합은 항상 100 이하.</summary>
    public static class Influence
    {
        public const int RegionCap = 100;

        public static int Get(WorldState w, string regionId, string factionId) =>
            w.Regions.TryGetValue(regionId, out var r) && r.Influence.TryGetValue(factionId, out var v) ? v : 0;

        public static int Free(WorldState w, string regionId) => RegionCap - w.Regions[regionId].Influence.Values.Sum();

        /// <summary>남은 몫에서 먼저, 모자라면 그 지역 1위 경쟁 세력에게서 빼앗는다. 실제 증가량을 돌려준다.</summary>
        public static int Gain(WorldState w, string regionId, string factionId, int amount)
        {
            if (amount <= 0) return 0;
            var region = w.Regions[regionId];
            int gained = Math.Min(amount, Math.Max(0, Free(w, regionId)));
            int remaining = amount - gained;
            while (remaining > 0)
            {
                var rival = region.Influence
                    .Where(kv => kv.Key != factionId && kv.Value > 0)
                    .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => kv.Key).FirstOrDefault();
                if (rival == null) break;
                int take = Math.Min(remaining, region.Influence[rival]);
                Lose(w, regionId, rival, take);
                gained += take;
                remaining -= take;
            }
            if (gained > 0) region.Influence[factionId] = Get(w, regionId, factionId) + gained;
            return gained;
        }

        public static void Lose(WorldState w, string regionId, string factionId, int amount)
        {
            var region = w.Regions[regionId];
            if (!region.Influence.TryGetValue(factionId, out var v)) return;
            v -= amount;
            if (v <= 0) region.Influence.Remove(factionId);
            else region.Influence[factionId] = v;
        }

        public static int Total(WorldState w, string factionId) =>
            w.Regions.Values.Sum(r => r.Influence.TryGetValue(factionId, out var v) ? v : 0);

        /// <summary>그 지역에서 영향력 1위 세력 id. 없으면 null.</summary>
        public static string Leading(WorldState w, string regionId) =>
            w.Regions[regionId].Influence.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key).FirstOrDefault();
    }
}
