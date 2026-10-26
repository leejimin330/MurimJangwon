using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Manor
{
    /// <summary>장원 안 한 사람의 지금 일과와 자리 (칸 좌표, 소수).</summary>
    public struct LifeSpot
    {
        public string Activity;
        public BuildingInstance Building;
        public float X;
        public float Y;
    }

    /// <summary>제자 하루 일과 (설계 6절). 보여 주기용이고 월 처리에는 영향이 없다. 같은 입력이면 같은 결과.</summary>
    public static class ManorLife
    {
        public const float SecondsPerHour = 2f;

        public static IEnumerable<Character> Residents(WorldState w, Faction f) =>
            FactionOps.LivingMembers(w, f).OrderBy(c => c.FullName, StringComparer.Ordinal).ThenBy(c => c.Id, StringComparer.Ordinal);

        public static LifeSpot Spot(WorldState w, GameData d, ManorState m, Character c, int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            string activity;
            string building;
            if (c.InTraining(w.Month))
            {
                activity = "폐관";
                building = "meditation_cave";
            }
            else if (hour < 6) { activity = "잠"; building = "dormitory"; }
            else if (hour < 9) { activity = "수련"; building = "training_ground"; }
            else if (hour < 12) { activity = "공부"; building = "library"; }
            else if (hour < 14) { activity = "식사"; building = BuildingValidator.MainHall; }
            else if (hour < 18) { activity = "수련"; building = "training_ground"; }
            else if (hour < 21)
            {
                activity = "휴식";
                building = c.HasTrait("bold") ? "market_stall" : c.HasTrait("diligent") ? "training_ground" : BuildingValidator.MainHall;
            }
            else { activity = "휴식"; building = "dormitory"; }

            var f = w.GetFaction(c.FactionId);
            if (f != null && f.LeaderId == c.Id && !c.InTraining(w.Month) && (hour >= 9 && hour < 12 || hour >= 14 && hour < 18))
            {
                activity = "집무";
                building = BuildingValidator.MainHall;
            }

            var target = Pick(w, d, m, c, building) ?? Pick(w, d, m, c, BuildingValidator.MainHall);
            var spot = new LifeSpot { Activity = activity, Building = target };
            if (target == null)
            {
                spot.X = m.Width / 2f;
                spot.Y = m.Height / 2f;
                return spot;
            }
            var def = ManorOps.Def(d, target.DefId);
            // 사람마다 건물 칸 안 자리를 흩는다 (id 해시, 결정론).
            uint h = Hash(c.Id);
            spot.X = target.X + 0.2f + (h % 1000) / 1000f * Math.Max(0.1f, def.Width - 0.4f);
            spot.Y = target.Y + 0.2f + (h / 1000 % 1000) / 1000f * Math.Max(0.1f, def.Height - 0.4f);
            return spot;
        }

        /// <summary>그 종류의 완공 건물 중 하나 (여러 개면 사람마다 고정으로 나눈다).</summary>
        static BuildingInstance Pick(WorldState w, GameData d, ManorState m, Character c, string defId)
        {
            var list = m.Buildings.Where(b => b.DefId == defId && ManorOps.IsComplete(w, b)).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
            return list.Count == 0 ? null : list[(int)(Hash(c.Id) % (uint)list.Count)];
        }

        static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s ?? "")
            {
                h ^= ch;
                h *= 16777619;
            }
            return h;
        }
    }
}
