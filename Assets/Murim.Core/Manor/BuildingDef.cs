using System;
using System.Collections.Generic;
using Murim.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Murim.Core.Manor
{
    /// <summary>건물 하나가 주는 보너스. 여러 건물이면 더한다.</summary>
    public class BuildingBonuses
    {
        public int MasteryPercent;
        public int EnlightenmentPerYear;
        public int TrainingQiMultiplier;
        public double BreakthroughBonus;
        public double DeviationReduction;
        public bool NoDeviationDeath;
        public int IncomePerMonth;
        public int Capacity;
        public int Defense;
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;

        public void Add(BuildingBonuses o)
        {
            if (o == null) return;
            MasteryPercent += o.MasteryPercent;
            EnlightenmentPerYear += o.EnlightenmentPerYear;
            TrainingQiMultiplier += o.TrainingQiMultiplier;
            BreakthroughBonus += o.BreakthroughBonus;
            DeviationReduction += o.DeviationReduction;
            NoDeviationDeath |= o.NoDeviationDeath;
            IncomePerMonth += o.IncomePerMonth;
            Capacity += o.Capacity;
            Defense += o.Defense;
        }

        /// <summary>화면용 한 줄 설명.</summary>
        public string Describe()
        {
            var parts = new List<string>();
            if (MasteryPercent != 0) parts.Add($"성취 +{MasteryPercent}%");
            if (EnlightenmentPerYear != 0) parts.Add($"깨달음 +{EnlightenmentPerYear}/년");
            if (TrainingQiMultiplier != 0) parts.Add($"폐관 내공 ×{3 + TrainingQiMultiplier}");
            if (BreakthroughBonus != 0) parts.Add($"돌파 +{BreakthroughBonus * 100:0}%");
            if (DeviationReduction != 0) parts.Add($"주화입마 −{DeviationReduction * 100:0}%");
            if (NoDeviationDeath) parts.Add("주화입마로 죽지 않음");
            if (IncomePerMonth != 0) parts.Add($"수입 +{IncomePerMonth}냥/월");
            if (Capacity != 0) parts.Add($"정원 +{Capacity}");
            if (Defense != 0) parts.Add($"방어 +{Defense}");
            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }
    }

    /// <summary>buildings.json의 건물 하나.</summary>
    public class BuildingDef
    {
        public string Id;
        public string Name;
        public string Description;
        public int Width = 1;
        public int Height = 1;
        public int Cost;
        public int BuildMonths;
        public int Max = 1;
        /// <summary>처음부터 있고 허물 수 없다 (본당).</summary>
        public bool Fixed;
        /// <summary>화면 색 (#RRGGBB).</summary>
        public string Color;
        public BuildingBonuses Bonuses = new BuildingBonuses();
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;
    }

    public static class BuildingValidator
    {
        const string File = "buildings.json";
        public const string MainHall = "main_hall";

        public static List<string> Validate(GameData d)
        {
            var errors = new List<string>();
            if (d.Buildings.Count == 0) return errors;
            if (!d.Buildings.ContainsKey(MainHall)) errors.Add($"{File}: 본당 '{MainHall}'이(가) 없습니다");
            foreach (var b in d.Buildings.Values)
            {
                string at = $"{File}: 건물 '{b.Id}'";
                if (b.Unknown != null) foreach (var k in b.Unknown.Keys) errors.Add($"{at}: 알 수 없는 항목 '{k}'");
                if (b.Bonuses?.Unknown != null) foreach (var k in b.Bonuses.Unknown.Keys) errors.Add($"{at}: 알 수 없는 보너스 '{k}'");
                if (string.IsNullOrEmpty(b.Name)) errors.Add($"{at}: 이름이 없습니다");
                if (b.Width < 1 || b.Width > 5 || b.Height < 1 || b.Height > 5) errors.Add($"{at}: 크기가 1~5 밖입니다");
                if (b.Cost < 0 || b.BuildMonths < 0 || b.Max < 1) errors.Add($"{at}: 비용·기간·최대 개수가 올바르지 않습니다");
            }
            return errors;
        }
    }
}
