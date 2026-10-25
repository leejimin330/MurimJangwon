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

        static IEnumerable<string> AllScenarios() => TestPaths.LoadShippedData().Scenarios.Keys;

        [TestCaseSource(nameof(AllScenarios))]
        public void EachScenario_50Years_NoException(string scenarioId)
        {
            var d = TestPaths.LoadShippedData();
            var s = GameSession.NewGame(d, new NewGameOptions { ScenarioId = scenarioId, RegionId = "hubei", Seed = 3 });
            s.IgnoreGameOver = true;
            for (int i = 0; i < 600; i++) s.EndTurn();
            Assert.That(s.World.Month, Is.EqualTo(600));
        }
    }
}
