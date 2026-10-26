using System.Linq;
using Murim.Core.Data;
using Murim.Core.Simulation;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Martial
{
    /// <summary>수련 단계: 매달 내공과 폐관 종료, 1월에 깨달음·성취·입문 전수·AI 돌파. 플레이어에게는 돌파의 기회 사건을 예약한다.</summary>
    public class CultivationPhase : ITurnPhase
    {
        public const string BreakthroughEvent = "art_breakthrough_chance";
        public const string TrainingEndEvent = "art_closed_training_end";

        public void Run(TurnContext t)
        {
            var w = t.World;
            var d = t.Data;
            var living = w.Characters.Values.Where(c => c.IsAlive).ToList();
            foreach (var c in living)
            {
                c.Qi += Cultivation.MonthlyQi(d, c, w.Month);
                if (c.TrainingUntil > 0 && w.Month >= c.TrainingUntil - 1) FinishTraining(t, c);
            }

            if (w.Month % 12 == 0)
            {
                foreach (var c in living)
                {
                    Cultivation.YearlyGrowth(w, d, t.Rng, c);
                    Cultivation.TeachFactionSecret(w, d, t.Rng, c);
                }
                foreach (var c in living)
                {
                    if (c.Id == w.PlayerCharacterId || !c.IsAlive || !Cultivation.CanBreakthrough(c)) continue;
                    bool closed = c.Role == Role.Leader || c.Role == Role.Heir;
                    Cultivation.TryBreakthrough(w, d, t.Rng, c, 0, closed);
                }
            }
            OfferBreakthrough(w, d);
        }

        static void FinishTraining(TurnContext t, Character c)
        {
            var w = t.World;
            var d = t.Data;
            int months = c.TrainingMonths;
            c.Enlightenment = System.Math.Min(Cultivation.EnlightenmentMax, c.Enlightenment + Cultivation.TrainingEnlightenment(months));
            var main = ArtGranting.MainArt(d, c);
            var known = main == null ? null : c.KnownArt(main.Id);
            if (known != null) known.Mastery = System.Math.Min(ArtGranting.MaxMastery, known.Mastery + t.Rng.Next(1, 4));
            c.TrainingUntil = 0;
            c.TrainingMonths = 0;
            if (BirthPhase.IsNotable(w, c)) Chronicle.TrainingEnded(w, c, months);
            if (c.Id == w.PlayerCharacterId && d.Events.ContainsKey(TrainingEndEvent))
                Schedule(w, TrainingEndEvent, c.Id);
        }

        /// <summary>돌파 조건을 채운 플레이어에게 다음 달 "돌파의 기회"를 보낸다 (이야기꾼과 상관없이).</summary>
        static void OfferBreakthrough(WorldState w, GameData d)
        {
            var p = w.Player;
            if (p == null || !p.IsAlive || w.GameOver || !d.Events.ContainsKey(BreakthroughEvent)) return;
            if (!Cultivation.CanBreakthrough(p) || p.InTraining(w.Month) || p.TrainingUntil > 0) return;
            if (w.Events.IsCooling(Events.EventResolver.CooldownKey(BreakthroughEvent, p.Id), w.Month + 1)) return;
            if (w.Events.Pending.Any(e => e.EventId == BreakthroughEvent) || w.Events.Scheduled.Any(e => e.EventId == BreakthroughEvent)) return;
            Schedule(w, BreakthroughEvent, p.Id);
        }

        static void Schedule(WorldState w, string eventId, string heroId)
        {
            var s = new ScheduledEvent { EventId = eventId, Month = w.Month + 1 };
            s.Roles["hero"] = heroId;
            w.Events.Scheduled.Add(s);
        }
    }
}
