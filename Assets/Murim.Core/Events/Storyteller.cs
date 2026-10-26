using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Random;
using Murim.Core.Simulation;
using Murim.Core.World;

namespace Murim.Core.Events
{
    /// <summary>플레이어에게 보낼 사건을 긴장도에 맞춰 고른다. 세계 사건에는 관여하지 않는다.</summary>
    public static class Storyteller
    {
        public const int MaxPerMonth = 2;
        /// <summary>두 번째 사건은 긴장도가 상한의 이 비율 이상일 때만 판정한다.</summary>
        public const double SecondEventRatio = 0.7;

        public static StorytellerDef Def(Data.GameData d, EventState s) =>
            d.Storytellers.TryGetValue(s.StorytellerId ?? "", out var def) ? def
            : d.Storytellers.TryGetValue("balanced", out var b) ? b : null;

        public static void Tick(EventState s, StorytellerDef def) => s.Tension = Math.Min(def.TensionMax, s.Tension + def.TensionGain);

        public static bool IsCandidate(EventDef e, EventState s, StorytellerDef def, int month)
        {
            if (e.FollowupOnly || e.WorldOnly || e.System) return false;
            if (e.Tension > s.Tension) return false;
            if (e.IsCrisis && month - s.LastCrisisMonth < def.CrisisGapMonths) return false;
            return true;
        }

        public static float Weight(EventDef e, StorytellerDef def) => e.Weight * def.Multiplier(e.Category);

        /// <summary>이번 달 플레이어 사건 0~2개. 고른 사건의 긴장도를 뺀다.</summary>
        public static List<EventContext> PickForPlayer(TurnContext t, CandidateIndex index, int alreadyThisMonth)
        {
            var w = t.World;
            var s = w.Events;
            var def = Def(t.Data, s);
            var picked = new List<EventContext>();
            var player = w.Player;
            if (def == null || player == null || !player.IsAlive || w.GameOver) return picked;
            // 폐관 중에는 이야기꾼 사건이 오지 않는다 (소식은 온다).
            if (player.InTraining(w.Month)) return picked;
            Tick(s, def);

            for (int attempt = 0; attempt < MaxPerMonth; attempt++)
            {
                if (alreadyThisMonth + picked.Count >= EventPhase.MaxPlayerEventsPerMonth) break;
                if (attempt > 0 && s.Tension < def.TensionMax * SecondEventRatio) break;
                if (!t.Rng.Chance(def.BaseChance)) break;
                var pool = t.Data.Events.Values.Where(e => IsCandidate(e, s, def, w.Month)
                    && !picked.Any(p => p.Def.Id == e.Id)).ToList();
                var fixedRoles = new Dictionary<string, string>();
                while (pool.Count > 0)
                {
                    var e = WeightedPick(pool, x => Weight(x, def), t.Rng);
                    pool.Remove(e);
                    fixedRoles.Clear();
                    fixedRoles[e.Decider] = player.Id;
                    var ctx = RoleBinder.TryBind(w, t.Data, t.Rng, e, fixedRoles, index, false);
                    if (ctx == null) continue;
                    s.Tension -= e.Tension;
                    if (e.IsCrisis) s.LastCrisisMonth = w.Month;
                    picked.Add(ctx);
                    break;
                }
            }
            return picked;
        }

        public static T WeightedPick<T>(IReadOnlyList<T> items, Func<T, float> weight, GameRandom rng)
        {
            double total = 0;
            foreach (var x in items) total += Math.Max(0, weight(x));
            if (total <= 0) return items[rng.Next(0, items.Count)];
            double roll = rng.NextDouble() * total;
            foreach (var x in items)
            {
                roll -= Math.Max(0, weight(x));
                if (roll < 0) return x;
            }
            return items[items.Count - 1];
        }
    }
}
