using System;
using System.IO;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Session;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class GameSessionEventTests
    {
        GameData d;
        WorldState w;
        Character player;
        Character npc;
        Faction f;

        [SetUp]
        public void Setup()
        {
            d = EventFixtures.Data();
            d.Storytellers["balanced"].BaseChance = 0.0001;
            w = TestWorlds.Empty(d);
            f = TestWorlds.AddFaction(w, "f", Alignment.Righteous, "a", 10);
            player = TestWorlds.AddCharacter(w, "f", Role.Leader, Sex.Male, 40);
            npc = TestWorlds.AddCharacter(w, "f", Role.Heir, Sex.Male, 20);
            w.PlayerCharacterId = player.Id;
            EventFixtures.Add(d, "{\"id\":\"offer\",\"name\":\"제안\",\"category\":\"trait\",\"decider\":\"a\",\"text\":\"{b:이} 은자를 청했다.\"," +
                "\"roles\":{\"a\":{\"pick\":\"anyAdult\"},\"b\":{\"pick\":\"sameRegion\",\"of\":\"a\"}}," +
                "\"choices\":[" +
                "{\"text\":\"준다 (500냥)\",\"result\":\"{a:은} 은자를 내주었다.\",\"requires\":{\"a\":{\"silverMin\":500}},\"effects\":[{\"silver\":{\"who\":\"a\",\"amount\":-500}}]}," +
                "{\"text\":\"거절한다\",\"result\":\"{a:은} 거절했다.\",\"effects\":[{\"opinion\":{\"who\":\"b\",\"of\":\"a\",\"amount\":-10}}]}," +
                "{\"text\":\"목숨을 건다\",\"effects\":[{\"kill\":{\"who\":\"a\",\"cause\":\"사고\"}}]}]}");
        }

        GameSession Session() => new GameSession(d, w, new GameRandom(9));

        [Test]
        public void ForceEvent_QueuesForPlayer()
        {
            var s = Session();
            Assert.That(s.ForceEvent("offer").Ok, Is.True);
            Assert.That(s.PendingEvents.Count, Is.EqualTo(1));
            Assert.That(s.PendingEvents[0].Roles["a"], Is.EqualTo(player.Id));
            Assert.That(s.PendingEvents[0].Title, Is.EqualTo("제안"));
        }

        [Test]
        public void ForceEvent_NoBinding_Fails()
        {
            npc.DeathMonth = w.Month;
            var s = Session();
            var r = s.ForceEvent("offer");
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Reason, Is.EqualTo("지금은 이 사건이 일어날 수 없습니다"));
            Assert.That(s.ForceEvent("nothing").Ok, Is.False);
        }

        [Test]
        public void EndTurn_BlockedWhilePending()
        {
            var s = Session();
            s.ForceEvent("offer");
            int month = w.Month;
            Assert.That(s.CanEndTurn, Is.False);
            s.EndTurn();
            Assert.That(w.Month, Is.EqualTo(month));
            s.Choose(0, 1);
            Assert.That(s.CanEndTurn, Is.True);
            s.EndTurn();
            Assert.That(w.Month, Is.EqualTo(month + 1));
        }

        [Test]
        public void Choose_DisabledReason()
        {
            f.Silver = 100;
            var s = Session();
            s.ForceEvent("offer");
            Assert.That(s.CanChoose(0, 0).Reason, Is.EqualTo("은자 500냥이 필요합니다"));
            var r = s.Choose(0, 0);
            Assert.That(r.Ok, Is.False);
            Assert.That(s.PendingEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void Choose_RemovesAndReturnsText()
        {
            var s = Session();
            s.ForceEvent("offer");
            var r = s.Choose(0, 1);
            Assert.That(r.Ok, Is.True);
            Assert.That(r.Text, Does.EndWith("거절했다."));
            Assert.That(s.PendingEvents, Is.Empty);
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("Event:offer"));
        }

        [Test]
        public void Choose_PlayerDies_SuccessionRunsImmediately()
        {
            var s = Session();
            s.ForceEvent("offer");
            s.ForceEvent("offer");
            s.Choose(0, 2);
            Assert.That(player.IsAlive, Is.False);
            Assert.That(w.PlayerCharacterId, Is.EqualTo(npc.Id));
            Assert.That(f.LeaderId, Is.EqualTo(npc.Id));
            Assert.That(s.PendingEvents, Is.Empty, "옛 플레이어에게 온 사건은 버린다");
        }

        [Test]
        public void Notice_ChooseZeroOnly()
        {
            w.Events.Pending.Add(new PendingEvent { EventId = "offer", IsNotice = true, Text = "소식" });
            var s = Session();
            Assert.That(s.CanChoose(0, 1).Ok, Is.False);
            Assert.That(s.Choose(0, 0).Ok, Is.True);
            Assert.That(s.PendingEvents, Is.Empty);
        }

        [Test]
        public void Pending_SaveLoad_SameOutcome()
        {
            var data = TestPaths.LoadShippedData();
            var a = GameSession.NewGame(data, new NewGameOptions { ScenarioId = "wandering_hero", RegionId = "henan", Seed = 11 });
            Assert.That(a.ForceEvent("wanderer_tavern_quarrel").Ok, Is.True);
            var path = Path.Combine(Path.GetTempPath(), "murim-pending-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Assert.That(a.Save(path).Ok, Is.True);
                var b = GameSession.Load(data, path).Session;
                Assert.That(b.PendingEvents.Count, Is.EqualTo(1));
                Assert.That(b.PendingEvents[0].Text, Is.EqualTo(a.PendingEvents[0].Text));
                var ra = a.Choose(0, 0);
                var rb = b.Choose(0, 0);
                Assert.That(rb.Text, Is.EqualTo(ra.Text));
                Assert.That(b.Serialize(), Is.EqualTo(a.Serialize()));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void AutoResolve_AdvancesThroughEvents()
        {
            var s = Session();
            s.AutoResolveEvents = true;
            s.ForceEvent("offer");
            int month = w.Month;
            s.EndTurn();
            Assert.That(w.Month, Is.EqualTo(month + 1));
            Assert.That(s.PendingEvents, Is.Empty);
        }
    }
}
