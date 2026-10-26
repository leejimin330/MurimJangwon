using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.Session;
using Murim.Core.Text;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class DuelIntegrationTests
    {
        /// <summary>출하 데이터를 새로 읽은 사본 (사건을 더해도 다른 테스트에 번지지 않는다).</summary>
        static GameData Fresh()
        {
            var r = DataLoader.LoadFromDirectory(TestPaths.DataDir);
            Assert.That(r.Ok, string.Join("\n", r.Errors));
            return r.Data;
        }

        static EventDef DuelEvent(GameData d, double deathChance, int importance = 1) => EventFixtures.Add(d,
            "{\"id\":\"test_duel\",\"name\":\"시험 결투\",\"category\":\"trait\",\"decider\":\"a\",\"importance\":" + importance + "," +
            "\"text\":\"{a:이} {b:와} 겨룬다.\",\"roles\":{\"a\":{\"pick\":\"anyAdult\"},\"b\":{\"pick\":\"sameRegion\",\"of\":\"a\"}}," +
            "\"choices\":[{\"text\":\"싸운다\",\"effects\":[{\"duel\":{\"a\":\"a\",\"b\":\"b\",\"deathChance\":" + deathChance.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"win\":[],\"lose\":[]}}]}]}");

        [Test]
        public void EventDuel_WritesDetail()
        {
            var d = Fresh();
            var e = DuelEvent(d, 0);
            var w = TestWorlds.Empty(d);
            var me = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var foe = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            w.PlayerCharacterId = me.Id;
            var ctx = EventFixtures.Context(w, d, e, ("a", me), ("b", foe));
            var result = EventResolver.Resolve(ctx, 0);
            var line = w.Chronicle.Last();
            Assert.That(line.Detail, Is.Not.Null.And.Not.Empty);
            Assert.That(line.Detail, Is.EqualTo(ctx.DuelLines.ToArray()));
            Assert.That(result, Does.Contain("꺾었다").Or.Contain("눌렀다"));
        }

        [Test]
        public void WorldDuel_LowImportance_NoDetail()
        {
            var d = Fresh();
            var e = DuelEvent(d, 0);
            var w = TestWorlds.Empty(d);
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var b = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            EventResolver.Resolve(EventFixtures.Context(w, d, e, ("a", a), ("b", b)), 0);
            Assert.That(w.Chronicle.Last().Detail, Is.Null);
        }

        [Test]
        public void Spar_UsesEngine_AndNotifiesPlayer()
        {
            var d = Fresh();
            var w = TestWorlds.Empty(d);
            var me = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var other = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            w.PlayerCharacterId = me.Id;
            var spar = new Spar(me.Id, other.Id);
            Assert.That(spar.CanExecute(w, d).Ok);
            spar.Execute(w, d, new GameRandom(3));
            var notice = w.Events.Pending.Single();
            Assert.That(notice.IsNotice);
            Assert.That(notice.Detail, Is.Not.Null.And.Not.Empty);
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("Sparred"));
            Assert.That(w.Chronicle.Last().Detail, Is.EqualTo(notice.Detail));
        }

        [Test]
        public void EventDuel_PlayerDies_DetailEndsWithDeath()
        {
            var d = Fresh();
            DuelEvent(d, 1);
            var s = GameSession.NewGame(d, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 5 });
            var w = s.World;
            var me = s.Player;
            me.Realm = 0;
            me.Constitution = 1;
            var foe = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40);
            foe.LocationRegionId = me.LocationRegionId;
            foe.Realm = 6;
            foe.Arts.Add(new KnownArt { ArtId = "taiji_sword", Mastery = 10 });
            foe.Qi = 180 * 12;

            Assert.That(s.ForceEvent("test_duel", new Dictionary<string, string> { ["a"] = me.Id, ["b"] = foe.Id }).Ok);
            var outcome = s.Choose(0, 0);
            Assert.That(outcome.Ok, outcome.Reason);
            Assert.That(me.IsAlive, Is.False, "내가 졌고 죽었다");
            var deaths = d.Narration[NarrationKeys.Death].Select(t => DuelText.Format(t,
                new Dictionary<string, string> { ["loser"] = me.FullName, ["winner"] = foe.FullName }));
            Assert.That(deaths, Does.Contain(outcome.DuelLines.Last()));
            Assert.That(w.PlayerCharacterId, Is.Not.EqualTo(me.Id), "후계가 바로 이어진다");
            Assert.That(w.GameOver, Is.False);
            var line = w.Chronicle.Last(c => c.Kind == "Event:test_duel");
            Assert.That(line.Detail.Last(), Is.EqualTo(outcome.DuelLines.Last()));
        }
    }
}
