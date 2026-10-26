using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.World;

namespace Murim.Core.Martial
{
    /// <summary>결투 기록(DuelRound 목록)을 읽을 수 있는 문장으로 바꾼다 (설계 5.2).</summary>
    public static class DuelNarrator
    {
        public const int MaxLines = 14;
        const int Always = 9;

        class Line
        {
            public string Text;
            public int Priority;
            public int Order;
        }

        public static void Narrate(GameData d, GameRandom rng, DuelResult r, Character a, Character b)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            var lines = new List<Line>();
            string Name(string id) => id == a.Id ? a.FullName : id == b.Id ? b.FullName : "누군가";
            void Add(string key, IDictionary<string, string> words, int priority)
            {
                var text = Pick(d, rng, key, used);
                if (text == null) return;
                lines.Add(new Line { Text = DuelText.Format(text, words), Priority = priority, Order = lines.Count });
            }

            Add(NarrationKeys.Open, new Dictionary<string, string>
            {
                ["a"] = a.FullName, ["b"] = b.FullName, ["art"] = ArtGranting.MainArt(d, b)?.Name ?? Duel.BasicFists.Name,
            }, Always);

            var damaging = r.Log.Where(x => x.Damage > 0).ToList();
            var biggest = damaging.OrderByDescending(x => x.Damage).FirstOrDefault();
            var last = damaging.LastOrDefault();
            foreach (var e in r.Log)
            {
                var words = new Dictionary<string, string>
                {
                    ["a"] = Name(e.ActorId), ["b"] = Name(e.TargetId), ["art"] = e.Art?.Name, ["card"] = e.Card?.Name, ["guard"] = e.Guard?.Name,
                };
                bool decisive = e == biggest || e == last;
                if (e.IsFinisher && e.Event != RoundEvent.Exhausted) Add(NarrationKeys.Finisher, words, 3);
                int priority = decisive ? 3 : e.Event == RoundEvent.Counter ? 2 : e.Event == RoundEvent.Hit ? 1 : 0;
                Add(Key(e.Event), words, priority);
            }

            var end = new Dictionary<string, string> { ["winner"] = r.Winner.FullName, ["loser"] = r.Loser.FullName };
            Add(r.TimedOut ? NarrationKeys.Timeout : NarrationKeys.Win, end, Always);
            if (r.LoserDied) Add(NarrationKeys.Death, end, Always);

            // 넘치면 덜 중요한 줄부터, 같은 중요도면 뒤 합부터 뺀다.
            while (lines.Count > MaxLines)
            {
                var drop = lines.Where(l => l.Priority < Always).OrderBy(l => l.Priority).ThenByDescending(l => l.Order).FirstOrDefault();
                if (drop == null) break;
                lines.Remove(drop);
            }
            r.Lines = lines.OrderBy(l => l.Order).Select(l => l.Text).ToList();
            r.Summary = Summary(d, r, last);
        }

        static string Summary(GameData d, DuelResult r, DuelRound last)
        {
            var words = new Dictionary<string, string>
            {
                ["winner"] = r.Winner.FullName, ["loser"] = r.Loser.FullName,
                ["art"] = ArtGranting.MainArt(d, r.Winner)?.Name ?? Duel.BasicFists.Name,
            };
            if (r.TimedOut) return DuelText.Format($"{{winner:이}} {r.Rounds}합 끝에 판정으로 {{loser:을}} 눌렀다", words);
            if (last != null && last.IsFinisher && last.ActorId == r.Winner.Id)
            {
                words["card"] = last.Card.Name;
                return DuelText.Format($"{{winner:이}} 절초 {{card:으로}} {{loser:을}} 꺾었다 ({r.Rounds}합)", words);
            }
            return DuelText.Format($"{{winner:이}} {{art:으로}} {{loser:을}} 꺾었다 ({r.Rounds}합)", words);
        }

        static string Key(RoundEvent e) => e switch
        {
            RoundEvent.Hit => NarrationKeys.Hit,
            RoundEvent.Graze => NarrationKeys.Graze,
            RoundEvent.Block => NarrationKeys.Block,
            RoundEvent.Evade => NarrationKeys.Evade,
            RoundEvent.Clash => NarrationKeys.Clash,
            RoundEvent.Probe => NarrationKeys.Probe,
            RoundEvent.Counter => NarrationKeys.Counter,
            _ => NarrationKeys.Exhausted,
        };

        /// <summary>쓰지 않은 문장 중 무작위. 다 썼으면 다시 쓴다. 문구가 없으면 null.</summary>
        static string Pick(GameData d, GameRandom rng, string key, HashSet<string> used)
        {
            if (!d.Narration.TryGetValue(key, out var pool) || pool == null || pool.Length == 0) return null;
            var fresh = pool.Where(x => !used.Contains(x)).ToList();
            var text = fresh.Count > 0 ? rng.Pick(fresh) : rng.Pick(pool);
            used.Add(text);
            return text;
        }
    }
}
