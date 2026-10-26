using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Session;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class DeterminismTests
    {
        [Test]
        public void SameSeed_50Years_IdenticalWorld()
        {
            var d = TestPaths.LoadShippedData();
            var o = new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 777 };
            var a = GameSession.NewGame(d, o);
            var b = GameSession.NewGame(d, o);
            a.IgnoreGameOver = b.IgnoreGameOver = true;
            a.AutoResolveEvents = b.AutoResolveEvents = true;
            for (int i = 0; i < 600; i++) { a.EndTurn(); b.EndTurn(); }
            Assert.That(a.Serialize(), Is.EqualTo(b.Serialize()));
        }
    }

    [Category("Long")]
    public class LongSimulationTests
    {
        [Test]
        public void TwoHundredYears_StaysHealthy()
        {
            var d = TestPaths.LoadShippedData();
            var s = GameSession.NewGame(d, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 12345 });
            s.IgnoreGameOver = true;
            s.AutoResolveEvents = true;
            var report = new List<string>();
            for (int year = 1; year <= 200; year++)
            {
                for (int m = 0; m < 12; m++) s.EndTurn();
                if (year % 10 != 0) continue;
                int alive = s.World.Characters.Values.Count(c => c.IsAlive);
                int factions = s.World.Factions.Values.Count(f => !f.IsDissolved);
                report.Add($"{year}년: 인물 {alive}, 세력 {factions}");
                TestContext.Progress.WriteLine(report.Last());
                Assert.That(alive, Is.InRange(500, 4000), string.Join("\n", report));
                Assert.That(factions, Is.AtLeast(20), string.Join("\n", report));
            }
            var kinds = new HashSet<string>(s.World.Chronicle.Select(e => e.Kind));
            Assert.That(kinds, Does.Contain("Married"));
            Assert.That(kinds, Does.Contain("Succeeded"));
            Assert.That(kinds, Does.Contain("AlignmentShifted"));
        }

        /// <summary>3단계: 경지는 돌파로만 오른다. 200년 동안 절정 이상 3~12%, 현경 0~3명, 삼류 30% 이상.</summary>
        [Test, Timeout(1200000)]
        public void TwoHundredYears_RealmDistribution()
        {
            var d = TestPaths.LoadShippedData();
            var s = GameSession.NewGame(d, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 2024 });
            s.IgnoreGameOver = true;
            s.AutoResolveEvents = true;
            var report = new List<string>();
            var failures = new List<string>();
            for (int year = 1; year <= 200; year++)
            {
                for (int m = 0; m < 12; m++) s.EndTurn();
                if (year % 10 != 0) continue;
                var adults = s.World.Characters.Values.Where(c => c.IsAlive && c.AgeYears(s.World.Month) >= 16).ToList();
                var byRealm = Enumerable.Range(0, 7).Select(r => adults.Count(c => c.Realm == r)).ToArray();
                double peak = adults.Count(c => c.Realm >= 3) / (double)adults.Count;
                double third = byRealm[0] / (double)adults.Count;
                int alive = s.World.Characters.Values.Count(c => c.IsAlive);
                string line = $"{year}년: 인물 {alive}, 성인 {adults.Count}, 경지 {string.Join("/", byRealm)}, 절정+ {peak:P1}, 삼류 {third:P1}";
                report.Add(line);
                TestContext.Progress.WriteLine(line);
                if (year >= 50)
                {
                    if (peak < 0.03 || peak > 0.12) failures.Add(line + " ← 절정 이상 비율");
                    if (byRealm[6] > 3) failures.Add(line + " ← 현경 수");
                    if (third < 0.30) failures.Add(line + " ← 삼류 비율");
                    if (alive < 500 || alive > 4000) failures.Add(line + " ← 인구");
                }
            }
            try
            {
                var dir = System.IO.Path.Combine(System.Environment.CurrentDirectory, "TestResults");
                if (System.IO.Directory.Exists(dir)) System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "realm_distribution.txt"), report);
            }
            catch (System.Exception) { }
            Assert.That(failures, Is.Empty, string.Join(System.Environment.NewLine, report));
        }

        static IEnumerable<string> AllScenarios() => TestPaths.LoadShippedData().Scenarios.Keys;

        [TestCaseSource(nameof(AllScenarios))]
        public void EachScenario_50Years_NoException(string scenarioId)
        {
            var d = TestPaths.LoadShippedData();
            var s = GameSession.NewGame(d, new NewGameOptions { ScenarioId = scenarioId, RegionId = "hubei", Seed = 3 });
            s.IgnoreGameOver = true;
            s.AutoResolveEvents = true;
            for (int i = 0; i < 600; i++) s.EndTurn();
            Assert.That(s.World.Month, Is.EqualTo(600));
        }
    }
}
