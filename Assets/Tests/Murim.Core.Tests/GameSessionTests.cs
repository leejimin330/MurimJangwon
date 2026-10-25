using System.IO;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Session;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class GameSessionTests
    {
        static GameData D => TestPaths.LoadShippedData();

        static GameSession Wanderer() =>
            GameSession.NewGame(D, new NewGameOptions { ScenarioId = "wandering_hero", RegionId = "henan", Seed = 11 }).Auto();

        [Test]
        public void NewGame_HasPlayerAndFullBudget()
        {
            var s = Wanderer();
            Assert.That(s.Player, Is.Not.Null);
            Assert.That(s.RemainingActions, Is.EqualTo(ActionBudget.Max(s.Player)));
        }

        [Test]
        public void Queue_RespectsBudget()
        {
            var s = Wanderer();
            int max = s.RemainingActions;
            for (int i = 0; i < max; i++) Assert.That(s.Queue(new PersonalQuest(s.Player.Id, false)).Ok, Is.True);
            Assert.That(s.RemainingActions, Is.EqualTo(0));
            var fail = s.Queue(new PersonalQuest(s.Player.Id, false));
            Assert.That(fail.Ok, Is.False);
            Assert.That(fail.Reason, Is.EqualTo("이번 달에는 더 할 수 없습니다"));
            s.Unqueue(0);
            Assert.That(s.RemainingActions, Is.EqualTo(1));
        }

        [Test]
        public void Queue_InvalidAction_ReturnsReason()
        {
            var s = Wanderer();
            var r = s.Queue(new Travel(s.Player.Id, "yunnan"));
            Assert.That(r.Ok, Is.False);
            Assert.That(s.Pending, Is.Empty);
        }

        [Test]
        public void Travel_WithOtherPending_Rejected_AndConsumesWholeBudget()
        {
            var s = Wanderer();
            s.Queue(new PersonalQuest(s.Player.Id, false));
            var r = s.Queue(new Travel(s.Player.Id, "hubei"));
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Reason, Is.EqualTo("이동은 다른 행동과 함께 할 수 없습니다"));
            s.Unqueue(0);
            Assert.That(s.Queue(new Travel(s.Player.Id, "hubei")).Ok, Is.True);
            Assert.That(s.RemainingActions, Is.EqualTo(0));
            s.EndTurn();
            Assert.That(s.Player.LocationRegionId, Is.EqualTo("hubei"));
            Assert.That(s.Pending, Is.Empty);
            Assert.That(s.RemainingActions, Is.GreaterThan(0));
        }

        [Test]
        public void Escort_BlocksNextMonthBudget()
        {
            var s = Wanderer();
            Assert.That(s.Queue(new Escort(s.Player.Id, "hubei")).Ok, Is.True);
            s.EndTurn();
            Assert.That(s.RemainingActions, Is.EqualTo(0));
            s.EndTurn();
            Assert.That(s.RemainingActions, Is.GreaterThan(0));
        }

        [Test]
        public void QueuedAction_TargetMarriedMeanwhile_SkippedWithChronicle()
        {
            var d = TestWorlds.MinimalData();
            var w = TestWorlds.Empty(d);
            TestWorlds.AddFaction(w, "us", Alignment.Righteous, "a", 10);
            TestWorlds.AddFaction(w, "them", Alignment.Righteous, "c", 10);
            var leader = TestWorlds.AddCharacter(w, "us", Role.Leader, Sex.Male, 50);
            leader.Cunning = 20;
            var theirLeader = TestWorlds.AddCharacter(w, "them", Role.Leader, Sex.Male, 50);
            var son1 = TestWorlds.AddCharacter(w, "us", Role.Family, Sex.Male, 20);
            var son2 = TestWorlds.AddCharacter(w, "us", Role.Family, Sex.Male, 21);
            var bride = TestWorlds.AddCharacter(w, "them", Role.Family, Sex.Female, 19);
            w.Opinions.Add(theirLeader.Id, leader.Id, 50);
            w.PlayerCharacterId = leader.Id;
            var s = new GameSession(d, w, new GameRandom(1));
            Assert.That(s.Queue(new ProposeMarriage(leader.Id, son1.Id, bride.Id, true)).Ok, Is.True);
            Assert.That(s.Queue(new ProposeMarriage(leader.Id, son2.Id, bride.Id, true)).Ok, Is.True);
            s.EndTurn();
            Assert.That(bride.SpouseId, Is.EqualTo(son1.Id));
            Assert.That(son2.SpouseId, Is.Null);
            Assert.That(w.Chronicle.Any(e => e.Kind == "ActionSkipped" && e.InvolvedIds.Contains(leader.Id)), Is.True);
        }

        [Test]
        public void EndTurn_January_WritesAutosave()
        {
            var s = Wanderer();
            var dir = Path.Combine(Path.GetTempPath(), "murim-test-" + System.Guid.NewGuid().ToString("N"));
            s.AutosaveDirectory = dir;
            try
            {
                for (int i = 0; i < 11; i++) s.EndTurn();
                Assert.That(File.Exists(Path.Combine(dir, "autosave.json")), Is.False);
                s.EndTurn();
                Assert.That(s.World.Month % 12, Is.EqualTo(0));
                Assert.That(File.Exists(Path.Combine(dir, "autosave.json")), Is.True);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
