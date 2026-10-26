using System;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Manor;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Simulation;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Martial
{
    public enum BreakthroughOutcome { Success, Failed, Deviation, Died }

    /// <summary>내공·깨달음·성취의 성장과 경지 돌파 (설계 4절). 수치 조정은 여기 상수만 고친다.</summary>
    public static class Cultivation
    {
        /// <summary>경지 r → r+1 돌파에 필요한 내공(년).</summary>
        public static readonly int[] ThresholdYears = { 10, 20, 40, 60, 100, 180 };
        public static readonly double[] BaseChance = { 0.70, 0.55, 0.40, 0.25, 0.12, 0.05 };

        public const int EnlightenmentMax = 100;
        public const double InsightStep = 0.02;
        public const double ClosedBonus = 0.15;
        public const double ChanceMin = 0.02;
        public const double ChanceMax = 0.95;
        public const int FailEnlightenmentLoss = 50;
        public const int FailQiLossPercent = 10;
        public const double DeviationBase = 0.15;
        public const double DeviationDemonic = 0.15;
        public const double DeviationClosedReduction = 0.05;
        public const double DeviationDeathChance = 0.05;
        public const int DeviationMonths = 12;
        public const string DeviationFlag = "qi_deviation";
        public const string DeviationCause = "주화입마";
        public const int WinEnlightenment = 2;
        /// <summary>해마다 깨달음 += 오성 × InsightPercent / 100 (설계는 50%).</summary>
        public const int InsightPercent = 40;
        public const int MaxWinsCounted = 3;
        public const int QiStartAge = 6;
        public const int YouthAge = 10;
        public const int ElderAge = 70;
        public const int TrainingMultiplier = 3;

        public static int QiYears(Character c) => c.Qi / 12;

        public static int NextThresholdYears(Character c) =>
            c.Realm < ThresholdYears.Length ? ThresholdYears[c.Realm] : ThresholdYears[ThresholdYears.Length - 1];

        public static bool CanBreakthrough(Character c) =>
            c.IsAlive && c.Realm < SimConstants.MaxRealm && QiYears(c) >= ThresholdYears[c.Realm] && c.Enlightenment >= EnlightenmentMax;

        public static bool KnowsDemonic(GameData d, Character c) =>
            c.Arts.Any(k => d.Arts.TryGetValue(k.ArtId, out var a) && a.Alignment == Alignment.Demonic);

        /// <summary>한 달에 쌓이는 내공(개월): 1 + ⌊(근골 + 심법 등급×4)/10⌋. 마공 심법은 등급 +1.</summary>
        public static int MonthlyQi(WorldState w, GameData d, Character c)
        {
            int month = w.Month;
            int age = c.AgeYears(month);
            if (age < QiStartAge) return 0;
            var inner = ArtGranting.Inner(d, c);
            int grade = inner == null ? 0 : inner.Grade + (inner.Alignment == Alignment.Demonic ? 1 : 0);
            int gain = 1 + (c.Constitution + grade * 4) / 10;
            if (age < YouthAge || age >= ElderAge) gain = Math.Max(1, gain / 2);
            if (c.InTraining(month)) gain *= TrainingMultiplier + ManorOps.BonusesFor(w, d, c).TrainingQiMultiplier;
            return gain;
        }

        /// <summary>장원 보너스(석실)를 더한 돌파 확률.</summary>
        public static double BreakthroughChance(WorldState w, GameData d, Character c, double bonus, bool closed) =>
            BreakthroughChance(c, bonus + ManorOps.BonusesFor(w, d, c).BreakthroughBonus, closed);

        public static double BreakthroughChance(Character c, double bonus, bool closed)
        {
            if (c.Realm >= BaseChance.Length) return 0;
            double p = BaseChance[c.Realm] + (c.Insight - 10) * InsightStep + (closed ? ClosedBonus : 0) + bonus;
            return Math.Max(ChanceMin, Math.Min(ChanceMax, p));
        }

        public static double DeviationChance(WorldState w, GameData d, Character c, bool closed) =>
            Math.Max(0, DeviationBase + (KnowsDemonic(d, c) ? DeviationDemonic : 0) - (closed ? DeviationClosedReduction : 0)
                - ManorOps.BonusesFor(w, d, c).DeviationReduction);

        /// <summary>돌파를 시도한다. 조건을 채우지 못했으면 아무 일 없이 Failed.</summary>
        public static BreakthroughOutcome TryBreakthrough(WorldState w, GameData d, GameRandom rng, Character c, double bonus, bool closed)
        {
            if (!CanBreakthrough(c)) return BreakthroughOutcome.Failed;
            if (rng.Chance(BreakthroughChance(w, d, c, bonus, closed)))
            {
                c.Realm++;
                c.Enlightenment = 0;
                if (BirthPhase.IsNotable(w, c)) Chronicle.Breakthrough(w, c);
                return BreakthroughOutcome.Success;
            }
            c.Enlightenment = Math.Max(0, c.Enlightenment - FailEnlightenmentLoss);
            c.Qi -= c.Qi * FailQiLossPercent / 100;
            if (!rng.Chance(DeviationChance(w, d, c, closed))) return BreakthroughOutcome.Failed;

            c.SetFlag(DeviationFlag, w.Month, DeviationMonths);
            int loss = rng.Next(1, 3);
            if (rng.Chance(0.5)) c.Constitution = Math.Max(SimConstants.StatMin, c.Constitution - loss);
            else c.Insight = Math.Max(SimConstants.StatMin, c.Insight - loss);
            bool dies = !ManorOps.BonusesFor(w, d, c).NoDeviationDeath && rng.Chance(DeviationDeathChance);
            if (BirthPhase.IsNotable(w, c)) Chronicle.QiDeviation(w, c, dies);
            if (!dies) return BreakthroughOutcome.Deviation;
            DeathPhase.Kill(w, c, DeviationCause);
            return BreakthroughOutcome.Died;
        }

        /// <summary>1월: 깨달음 += 오성/2 + 2×이긴 결투(최대 3), 무공마다 성취 +1 판정.</summary>
        public static void YearlyGrowth(WorldState w, GameData d, GameRandom rng, Character c)
        {
            int wins = Math.Min(MaxWinsCounted, c.DuelWinsThisYear);
            c.DuelWinsThisYear = 0;
            if (c.AgeYears(w.Month) < YouthAge) return;
            var manor = ManorOps.BonusesFor(w, d, c);
            c.Enlightenment = Math.Min(EnlightenmentMax, c.Enlightenment + c.Insight * InsightPercent / 100 + WinEnlightenment * wins
                + manor.EnlightenmentPerYear);
            var main = ArtGranting.MainArt(d, c);
            foreach (var k in c.Arts)
            {
                if (k.Mastery >= ArtGranting.MaxMastery || !d.Arts.TryGetValue(k.ArtId, out var a)) continue;
                double p = (c.Insight + c.Constitution) / 40.0 * (6 - a.Grade) / 5.0;
                if (main != null && main.Id == a.Id) p *= 1.5;
                p *= 1 + manor.MasteryPercent / 100.0;
                if (rng.Chance(p)) k.Mastery++;
            }
        }

        /// <summary>입문 전수: 소속 세력(객경 제외)의 비전을 하나도 모르면 조건 맞는 것 하나를 1성으로.</summary>
        public static void TeachFactionSecret(WorldState w, GameData d, GameRandom rng, Character c)
        {
            var f = w.GetFaction(c.FactionId);
            if (f == null || f.IsDissolved || c.Role == Role.Guest || c.AgeYears(w.Month) < YouthAge) return;
            if (c.Arts.Any(k => d.Arts.TryGetValue(k.ArtId, out var a) && a.Faction == f.Id)) return;
            var pool = ArtGranting.Learnable(w, d, c, a => a.Faction == f.Id).OrderBy(a => a.IsInner ? 1 : 0).ToList();
            if (pool.Count == 0) return;
            var externals = pool.Where(a => !a.IsInner).ToList();
            var pick = externals.Count > 0 ? rng.Pick(externals) : pool[0];
            ArtGranting.Learn(d, c, pick.Id, 1);
            if (c.MainArtId == null) c.MainArtId = ArtGranting.MainArt(d, c)?.Id;
        }

        /// <summary>폐관을 시작한다: 이번 달부터 months개월 동안 내공 ×3, 행동 불가.</summary>
        public static void BeginTraining(WorldState w, Character c, int months)
        {
            c.TrainingUntil = w.Month + months;
            c.TrainingMonths = months;
            c.BusyUntilMonth = Math.Max(c.BusyUntilMonth, c.TrainingUntil);
        }

        /// <summary>폐관을 마쳤을 때 깨달음 보너스.</summary>
        public static int TrainingEnlightenment(int months) => months >= 12 ? 40 : months >= 6 ? 25 : 15;
    }
}
