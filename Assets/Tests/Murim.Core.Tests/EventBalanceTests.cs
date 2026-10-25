using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Session;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    /// <summary>이야기꾼·세계 사건의 빈도와 서사 고리를 긴 시뮬레이션으로 확인한다.</summary>
    [Category("Long")]
    public class EventBalanceTests
    {
        static GameData D => TestPaths.LoadShippedData();

        /// <summary>진단 출력: Unity에서는 TestResults/balance.txt에도 남긴다.</summary>
        static void Log(string line)
        {
            TestContext.Progress.WriteLine(line);
            try
            {
                var dir = System.IO.Path.Combine(System.Environment.CurrentDirectory, "TestResults");
                if (System.IO.Directory.Exists(dir)) System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "balance.txt"), line + System.Environment.NewLine);
            }
            catch (System.Exception) { }
        }
        static readonly string[] Scenarios = { "minor_sect", "fallen_clan", "wandering_hero", "ronin", "black_gang" };

        class Stats
        {
            public int Months;
            public int PlayerEvents;
            public int Notices;
            public int MaxInMonth;
            public int WorldEvents;
        }

        /// <summary>플레이어 사건 수를 세면서 AI 방식으로 답한다.</summary>
        static Stats Simulate(string scenario, string storyteller, int years, ulong seed)
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = scenario, RegionId = "henan", Seed = seed, StorytellerId = storyteller });
            s.IgnoreGameOver = true;
            var st = new Stats();
            for (int m = 0; m < years * 12; m++)
            {
                // 대가 끊긴 뒤에는 이야기꾼이 사건을 보내지 않으므로 세지 않는다.
                if (s.World.GameOver) break;
                int before = s.World.Chronicle.Count;
                s.EndTurn();
                st.Months++;
                int player = s.PendingEvents.Count(p => !p.IsNotice);
                st.PlayerEvents += player;
                st.Notices += s.PendingEvents.Count(p => p.IsNotice);
                st.MaxInMonth = System.Math.Max(st.MaxInMonth, player);
                st.WorldEvents += s.World.Chronicle.Skip(before).Count(e => e.Kind.StartsWith("Event:"));
                s.ResolveAllAuto();
            }
            return st;
        }

        static readonly Dictionary<string, (double min, double max)> Range = new Dictionary<string, (double, double)>
        {
            ["calm"] = (1, 4), ["balanced"] = (3, 8), ["turbulent"] = (6, 14),
        };

        [Test, Timeout(1200000)]
        public void PlayerEventsPerYear_ByStoryteller()
        {
            var avg = new Dictionary<string, double>();
            foreach (var teller in new[] { "calm", "balanced", "turbulent" })
            {
                var all = Scenarios.Select((sc, i) => Simulate(sc, teller, 50, (ulong)(100 + i))).ToList();
                double perYear = all.Sum(x => x.PlayerEvents) / (all.Sum(x => x.Months) / 12.0);
                avg[teller] = perYear;
                Log($"{teller}: 플레이어 사건 {perYear:F2}/년, 소식 {all.Sum(x => x.Notices) / (all.Sum(x => x.Months) / 12.0):F2}/년, " +
                    $"한 달 최대 {all.Max(x => x.MaxInMonth)}, 세계 기록 {all.Sum(x => x.WorldEvents) / (double)all.Sum(x => x.Months):F2}/월");
                foreach (var (x, sc) in all.Zip(Scenarios, (x, sc) => (x, sc)))
                    Log($"   {sc}: {x.PlayerEvents / (x.Months / 12.0):F2}/년");
                Assert.That(all.Max(x => x.MaxInMonth), Is.LessThanOrEqualTo(3));
            }
            foreach (var kv in avg)
                Assert.That(kv.Value, Is.InRange(Range[kv.Key].min, Range[kv.Key].max), kv.Key);
            Assert.That(avg["calm"], Is.LessThan(avg["balanced"]));
            Assert.That(avg["balanced"], Is.LessThan(avg["turbulent"]));
        }

        [Test, Timeout(600000)]
        public void TwoHundredYears_NarrativeChains()
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 12345 });
            s.IgnoreGameOver = true;
            s.AutoResolveEvents = true;
            for (int m = 0; m < 200 * 12; m++) s.EndTurn();
            var counts = s.World.Chronicle.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
            foreach (var kv in counts.Where(kv => kv.Key.StartsWith("Event:")).OrderBy(kv => kv.Key))
                Log($"{kv.Key}: {kv.Value}");
            int Count(string k) => counts.TryGetValue(k, out var v) ? v : 0;
            double perMonth = counts.Where(kv => kv.Key.StartsWith("Event:")).Sum(kv => kv.Value) / 2400.0;
            var live = s.World.Factions.Values.Where(f => !f.IsDissolved).ToList();
            double demonic = live.Count(f => f.Alignment == Alignment.Demonic) / (double)live.Count;
            int toDemonic = s.World.Chronicle.Count(e => e.Kind == "AlignmentShifted" && e.Text.Contains("마도에 몸을 담갔다"));
            Log($"세계 사건 {perMonth:F2}/월, 마도 비율 {demonic:P0}, 마도 전환 {toDemonic}, 하극상 {Count("Usurped")}");

            Assert.That(Count("Event:grudge_duel_challenge") + Count("Event:grudge_ambush"), Is.GreaterThan(0));
            Assert.That(Count("Event:grudge_enemy_child"), Is.GreaterThan(0));
            Assert.That(Count("Event:debt_repay"), Is.GreaterThan(0));
            Assert.That(toDemonic, Is.GreaterThan(0));
            Assert.That(demonic, Is.LessThanOrEqualTo(0.4));
            Assert.That(perMonth, Is.InRange(1.0, 4.0));
        }
    }
}
