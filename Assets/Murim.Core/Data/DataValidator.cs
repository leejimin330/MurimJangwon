using System.Collections.Generic;
using System.Linq;

namespace Murim.Core.Data
{
    public static class DataValidator
    {
        public static List<string> Validate(GameData d)
        {
            var errors = new List<string>();
            foreach (var r in d.Regions.Values)
            {
                foreach (var n in r.Neighbors ?? new string[0])
                {
                    if (!d.Regions.TryGetValue(n, out var other))
                        errors.Add($"regions.json: 지역 '{r.Id}'의 인접 지역 '{n}'이(가) 없습니다");
                    else if (other.Neighbors == null || !other.Neighbors.Contains(r.Id))
                        errors.Add($"regions.json: '{r.Id}' → '{n}' 인접 관계가 대칭이 아닙니다 ('{n}'에 '{r.Id}' 없음)");
                }
            }
            foreach (var f in d.Factions.Values)
            {
                if (f.HomeRegion == null || !d.Regions.ContainsKey(f.HomeRegion))
                    errors.Add($"factions.json: 세력 '{f.Id}'의 본거지 '{f.HomeRegion}'이(가) 없습니다");
                if (f.Influence < 0 || f.Influence > 100)
                    errors.Add($"factions.json: 세력 '{f.Id}'의 영향력 {f.Influence}이(가) 0~100 밖입니다");
            }
            foreach (var g in d.Factions.Values.Where(f => f.HomeRegion != null).GroupBy(f => f.HomeRegion))
            {
                int sum = g.Sum(f => f.Influence);
                if (sum > 100) errors.Add($"factions.json: 지역 '{g.Key}'의 영향력 합 {sum}이(가) 100을 넘습니다");
            }
            foreach (var t in d.Traits.Values)
            {
                foreach (var o in t.Opposites ?? new string[0])
                    if (!d.Traits.ContainsKey(o))
                        errors.Add($"traits.json: 특성 '{t.Id}'의 반대 특성 '{o}'이(가) 없습니다");
            }
            errors.AddRange(Events.EventValidator.Validate(d));
            return errors;
        }
    }
}
