using System.Linq;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class EffectTests
    {
        GameData d;
        WorldState w;
        Character a;
        Character b;
        int n;

        [SetUp]
        public void Setup()
        {
            d = EventFixtures.Data();
            w = TestWorlds.Empty(d);
            a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            b = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
        }

        /// <summary>선택지 하나에 효과 목록을 넣은 사건을 a·b로 묶어 실행한다.</summary>
        EventContext Run(string effects, ulong seed = 7)
        {
            var e = EventFixtures.Add(d, "{\"id\":\"fx" + (++n) + "\",\"name\":\"효과\",\"category\":\"trait\",\"decider\":\"a\",\"text\":\"t\"," +
                "\"roles\":{\"a\":{\"pick\":\"anyAdult\"},\"b\":{\"pick\":\"sameRegion\",\"of\":\"a\"}}," +
                "\"choices\":[{\"text\":\"x\",\"effects\":" + effects + "}]}");
            var ctx = EventFixtures.Context(w, d, e, ("a", a), ("b", b));
            ctx.Rng = new GameRandom(seed);
            Effects.Apply(e.Choices[0].Effects, ctx);
            return ctx;
        }

        [Test]
        public void Opinion_Adds()
        {
            Run("[{\"opinion\":{\"who\":\"b\",\"of\":\"a\",\"amount\":15}}]");
            Assert.That(w.Opinions.GetStored(b.Id, a.Id), Is.EqualTo(15));
        }

        [Test]
        public void Memory_AddsAndNemesis()
        {
            Run("[{\"memory\":{\"holder\":\"b\",\"kind\":\"Grudge\",\"target\":\"a\",\"reason\":\"모욕\",\"strength\":70}}]");
            Assert.That(b.Memories.Single().Reason, Is.EqualTo("모욕"));
            Assert.That(b.HasRelation(a.Id, RelationTag.Nemesis), Is.True);
        }

        [Test]
        public void WeakenMemory()
        {
            Opinion.AddMemory(b, MemoryKind.Grudge, a.Id, "모욕", w.Month, 70);
            Run("[{\"weakenMemory\":{\"holder\":\"b\",\"target\":\"a\",\"amount\":40}}]");
            Assert.That(b.Memories.Single().Strength, Is.EqualTo(30));
            Assert.That(b.HasRelation(a.Id, RelationTag.Nemesis), Is.False);
        }

        [Test]
        public void RemoveMemory()
        {
            Opinion.AddMemory(b, MemoryKind.Grudge, a.Id, "모욕", w.Month, 70);
            Opinion.AddMemory(b, MemoryKind.Debt, a.Id, "선물", w.Month, 30);
            Run("[{\"removeMemory\":{\"holder\":\"b\",\"target\":\"a\"}}]");
            Assert.That(b.Memories.Single().Kind, Is.EqualTo(MemoryKind.Debt));
        }

        [Test]
        public void Silver_ClampsAtZero()
        {
            a.Silver = 50;
            Run("[{\"silver\":{\"who\":\"a\",\"amount\":-200}},{\"fame\":{\"who\":\"a\",\"amount\":4}},{\"chivalry\":{\"who\":\"a\",\"amount\":-7}}]");
            Assert.That(a.Silver, Is.EqualTo(0));
            Assert.That(a.Fame, Is.EqualTo(4));
            Assert.That(a.Chivalry, Is.EqualTo(-7));
        }

        [Test]
        public void Influence_Gain_And_Unnamed()
        {
            var f = TestWorlds.AddFaction(w, "f", Alignment.Righteous, "a", 10);
            FactionOps.MoveToFaction(w, a, "f", Role.Elder);
            f.UnnamedDisciples = 3;
            Run("[{\"influence\":{\"faction\":\"a\",\"amount\":5}},{\"unnamed\":{\"faction\":\"a\",\"amount\":-10}}]");
            Assert.That(Influence.Get(w, "a", "f"), Is.EqualTo(15));
            Assert.That(f.UnnamedDisciples, Is.EqualTo(0));
            Run("[{\"influence\":{\"faction\":\"a\",\"amount\":-20}}]");
            Assert.That(Influence.Get(w, "a", "f"), Is.EqualTo(0));
        }

        [Test]
        public void Stability_Clamped0to100()
        {
            Run("[{\"stability\":{\"region\":\"a\",\"amount\":80}}]");
            Assert.That(w.Regions["a"].Stability, Is.EqualTo(100));
            Run("[{\"stability\":{\"region\":\"a\",\"amount\":-300}}]");
            Assert.That(w.Regions["a"].Stability, Is.EqualTo(0));
        }

        [Test]
        public void Stat_Clamped1to20()
        {
            Run("[{\"stat\":{\"who\":\"a\",\"stat\":\"insight\",\"amount\":50}},{\"stat\":{\"who\":\"a\",\"stat\":\"cunning\",\"amount\":-50}}]");
            Assert.That(a.Insight, Is.EqualTo(20));
            Assert.That(a.Cunning, Is.EqualTo(1));
        }

        [Test]
        public void Kill_InheritsMemories()
        {
            var child = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 10);
            b.ChildIds.Add(child.Id);
            Opinion.AddMemory(b, MemoryKind.Grudge, a.Id, "모욕", w.Month, 80);
            Run("[{\"kill\":{\"who\":\"b\",\"cause\":\"역병\"}}]");
            Assert.That(b.IsAlive, Is.False);
            Assert.That(b.DeathCause, Is.EqualTo("역병"));
            Assert.That(child.Memories.Single().Inherited, Is.True);
        }

        [Test]
        public void Flag_SetAndClear()
        {
            Run("[{\"flag\":{\"who\":\"a\",\"id\":\"injured\",\"months\":6}}]");
            Assert.That(a.HasFlag("injured", w.Month + 5), Is.True);
            Run("[{\"clearFlag\":{\"who\":\"a\",\"id\":\"injured\"}}]");
            Assert.That(a.HasFlag("injured", w.Month), Is.False);
        }

        [Test]
        public void Relation_BothSides()
        {
            Run("[{\"relation\":{\"a\":\"a\",\"b\":\"b\",\"tag\":\"SwornSibling\"}}]");
            Assert.That(a.HasRelation(b.Id, RelationTag.SwornSibling) && b.HasRelation(a.Id, RelationTag.SwornSibling), Is.True);
        }

        [Test]
        public void SetAlignment_DemonicChronicle()
        {
            var f = TestWorlds.AddFaction(w, "f", Alignment.Unorthodox, "a", 10);
            FactionOps.MoveToFaction(w, a, "f", Role.Leader);
            Run("[{\"setAlignment\":{\"faction\":\"a\",\"alignment\":\"Demonic\"}}]");
            Assert.That(f.Alignment, Is.EqualTo(Alignment.Demonic));
            Assert.That(f.Chivalry, Is.EqualTo(SimConstants.DemonicStartChivalry));
            Assert.That(w.Chronicle.Last().Text, Is.EqualTo("f파가 마도에 몸을 담갔다."));
        }

        [Test]
        public void Leave_And_Move()
        {
            TestWorlds.AddFaction(w, "f", Alignment.Righteous, "b", 10);
            FactionOps.MoveToFaction(w, a, "f", Role.Disciple);
            Run("[{\"leave\":{\"who\":\"a\"}}]");
            Assert.That(a.FactionId, Is.Null);
            Assert.That(a.LocationRegionId, Is.EqualTo("b"));
            Run("[{\"move\":{\"who\":\"a\"}}]");
            Assert.That(new[] { "a", "c" }, Does.Contain(a.LocationRegionId));
        }

        [Test]
        public void Usurp_SwapsLeader()
        {
            var f = TestWorlds.AddFaction(w, "f", Alignment.Righteous, "a", 10);
            FactionOps.MoveToFaction(w, b, "f", Role.Leader);
            FactionOps.MoveToFaction(w, a, "f", Role.Elder);
            Run("[{\"usurp\":{\"who\":\"a\"}}]");
            Assert.That(f.LeaderId, Is.EqualTo(a.Id));
            Assert.That(a.Role, Is.EqualTo(Role.Leader));
            Assert.That(b.FactionId, Is.Null);
            Assert.That(b.Memories.Single().Reason, Is.EqualTo("하극상"));
        }

        [Test]
        public void AllianceScore_BetweenFactions()
        {
            TestWorlds.AddFaction(w, "f", Alignment.Righteous, "a", 10);
            TestWorlds.AddFaction(w, "g", Alignment.Righteous, "a", 10);
            FactionOps.MoveToFaction(w, a, "f", Role.Leader);
            FactionOps.MoveToFaction(w, b, "g", Role.Leader);
            Run("[{\"allianceScore\":{\"a\":\"a\",\"b\":\"b\",\"amount\":-20}}]");
            Assert.That(w.AllianceScore("f", "g"), Is.EqualTo(-20));
        }

        [Test]
        public void Duel_DeathChance1_LoserDies()
        {
            a.Realm = 5;
            b.Realm = 0;
            var ctx = Run("[{\"duel\":{\"a\":\"a\",\"b\":\"b\",\"deathChance\":1,\"win\":[{\"fame\":{\"who\":\"a\",\"amount\":5}}],\"lose\":[]}}]");
            Assert.That(b.IsAlive, Is.False);
            Assert.That(b.DeathCause, Is.EqualTo("결투"));
            Assert.That(a.Fame, Is.EqualTo(5));
            Assert.That(ctx.Log.Last(), Does.Contain("목숨을 잃었다"));
        }

        [Test]
        public void Duel_DeathChance0_NoDeath()
        {
            a.Realm = 0;
            b.Realm = 5;
            Run("[{\"duel\":{\"a\":\"a\",\"b\":\"b\",\"deathChance\":0,\"win\":[],\"lose\":[{\"fame\":{\"who\":\"b\",\"amount\":3}}]}}]");
            Assert.That(a.IsAlive && b.IsAlive, Is.True);
            Assert.That(b.Fame, Is.EqualTo(3));
        }

        [Test]
        public void Duel_InjuredPenalty()
        {
            // 같은 실력이면 반반이지만, 부상(−15)이 있으면 대부분 진다.
            int wins = 0;
            a.SetFlag("injured", w.Month, 12);
            for (ulong seed = 1; seed <= 200; seed++)
            {
                a.Fame = 0;
                Run("[{\"duel\":{\"a\":\"a\",\"b\":\"b\",\"deathChance\":0,\"win\":[{\"fame\":{\"who\":\"a\",\"amount\":1}}],\"lose\":[]}}]", seed);
                wins += a.Fame;
            }
            Assert.That(wins, Is.LessThan(60));
        }

        [Test]
        public void Contest_Branches()
        {
            a.Cunning = 20;
            Run("[{\"contest\":{\"who\":\"a\",\"stat\":\"cunning\",\"against\":0,\"win\":[{\"fame\":{\"who\":\"a\",\"amount\":1}}],\"lose\":[{\"fame\":{\"who\":\"a\",\"amount\":9}}]}}]");
            Assert.That(a.Fame, Is.EqualTo(1));
            Run("[{\"contest\":{\"who\":\"a\",\"stat\":\"cunning\",\"against\":99,\"win\":[],\"lose\":[{\"fame\":{\"who\":\"a\",\"amount\":9}}]}}]");
            Assert.That(a.Fame, Is.EqualTo(10));
        }

        [Test]
        public void Chance_Branches()
        {
            Run("[{\"chance\":{\"p\":1,\"then\":[{\"fame\":{\"who\":\"a\",\"amount\":2}}],\"else\":[{\"fame\":{\"who\":\"a\",\"amount\":100}}]}}]");
            Run("[{\"chance\":{\"p\":0,\"then\":[{\"fame\":{\"who\":\"a\",\"amount\":100}}],\"else\":[{\"fame\":{\"who\":\"a\",\"amount\":3}}]}}]");
            Assert.That(a.Fame, Is.EqualTo(5));
        }

        [Test]
        public void Schedule_AddsWithRoles()
        {
            EventFixtures.Blank(d, "later");
            Run("[{\"schedule\":{\"event\":\"later\",\"afterMonths\":[6,6],\"roles\":{\"a\":\"b\",\"b\":\"a\"}}}]");
            var s = w.Events.Scheduled.Single();
            Assert.That(s.EventId, Is.EqualTo("later"));
            Assert.That(s.Month, Is.EqualTo(w.Month + 6));
            Assert.That(s.Roles["a"], Is.EqualTo(b.Id));
            Assert.That(s.Roles["b"], Is.EqualTo(a.Id));
        }

        [Test]
        public void DeadRole_EffectSkipped()
        {
            b.DeathMonth = w.Month;
            b.Silver = 10;
            Run("[{\"silver\":{\"who\":\"b\",\"amount\":100}},{\"kill\":{\"who\":\"b\"}}]");
            Assert.That(b.Silver, Is.EqualTo(10));
            Assert.That(b.DeathCause, Is.Null);
        }

        [Test]
        public void Chronicle_AddsFormattedLog()
        {
            a.FamilyName = "장";
            a.GivenName = "삼";
            var ctx = Run("[{\"chronicle\":{\"text\":\"{a:이} 웃었다.\"}}]");
            Assert.That(ctx.Log.Single(), Is.EqualTo("장삼이 웃었다."));
        }
    }
}
