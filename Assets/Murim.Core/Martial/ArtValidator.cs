using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Newtonsoft.Json.Linq;

namespace Murim.Core.Martial
{
    /// <summary>martial_arts.json 검증.</summary>
    public static class ArtValidator
    {
        const string File = "martial_arts.json";

        public static List<string> Validate(GameData d)
        {
            var errors = new List<string>();
            if (d.Arts.Count == 0) return errors;
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in d.Arts.Values)
            {
                string at = $"{File}: 무공 '{a.Id}'";
                Unknown(a.Unknown, at, errors);
                Unknown(a.Requires?.Unknown, at + " requires", errors);
                if (string.IsNullOrEmpty(a.Name)) errors.Add($"{at}: 이름이 없습니다");
                if (a.Grade < 1 || a.Grade > 5) errors.Add($"{at}: 등급 {a.Grade}이(가) 1~5 밖입니다");
                if (a.Faction != null && !d.Factions.ContainsKey(a.Faction)) errors.Add($"{at}: 세력 '{a.Faction}'이(가) 없습니다");
                var cards = a.Cards ?? new List<CardDef>();
                if (a.IsInner && cards.Count > 0) errors.Add($"{at}: 내공심법에는 카드가 없어야 합니다");
                if (!a.IsInner && cards.Count < 2) errors.Add($"{at}: 외공에는 카드가 2장 이상 있어야 합니다");
                if (!a.IsInner && cards.Count(c => c.MasteryMin <= 1) < 1) errors.Add($"{at}: 1성에서 쓸 수 있는 카드가 없습니다");
                foreach (var c in cards)
                {
                    string ca = $"{at} 카드 '{c.Id}'";
                    Unknown(c.Unknown, ca, errors);
                    if (string.IsNullOrEmpty(c.Id) || !cardIds.Add(c.Id)) errors.Add($"{ca}: id가 없거나 중복됩니다");
                    if (string.IsNullOrEmpty(c.Name)) errors.Add($"{ca}: 이름이 없습니다");
                    if (c.Power <= 0 || c.Cost < 0) errors.Add($"{ca}: 위력·내력 값이 올바르지 않습니다");
                    if (c.MasteryMin < 0 || c.MasteryMin > 10) errors.Add($"{ca}: 필요 성취가 0~10 밖입니다");
                }
            }
            foreach (var key in NarrationKeys.All)
                if (!d.Narration.TryGetValue(key, out var lines) || lines == null || lines.Length == 0)
                    errors.Add($"{File}: 결투 문구 '{key}'이(가) 없습니다");
            foreach (var kv in d.Narration)
            {
                if (!NarrationKeys.All.Contains(kv.Key)) errors.Add($"{File}: 알 수 없는 결투 문구 '{kv.Key}'");
                foreach (var line in kv.Value ?? new string[0])
                    foreach (var bad in DuelText.BadTokens(line)) errors.Add($"{File}: 결투 문구 '{kv.Key}'의 알 수 없는 표기 '{bad}'");
            }
            return errors;
        }

        static void Unknown(IDictionary<string, JToken> unknown, string at, List<string> errors)
        {
            if (unknown == null) return;
            foreach (var k in unknown.Keys) errors.Add($"{at}: 알 수 없는 항목 '{k}'");
        }
    }
}
