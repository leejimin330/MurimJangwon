using System.Collections.Generic;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class RoleBinderTests
    {
        GameData d;
        WorldState w;

        [SetUp]
        public void Setup()
        {
            d = EventFixtures.Data();
            w = TestWorlds.Empty(d);
        }

        EventDef Duel() => EventFixtures.Add(d,
            "{\"id\":\"duel\",\"name\":\"생사결\",\"category\":\"grudge\",\"decider\":\"target\",\"text\":\"{avenger:이} {target}에게\"," +
            "\"roles\":{\"avenger\":{\"pick\":\"grudgeHolder\",\"memory\":{\"kind\":\"Grudge\",\"minStrength\":60}}," +
            "\"target\":{\"pick\":\"memoryTarget\",\"of\":\"avenger\"}}," +
            "\"choices\":[{\"text\":\"x\",\"effects\":[]}]}");

        [Test]
        public void MemoryTarget_PicksGrudgeTarget()
        {
            var e = Duel();
            var avenger = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var target = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var weak = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(avenger, MemoryKind.Grudge, target.Id, "모욕", w.Month, 70);
            Opinion.AddMemory(avenger, MemoryKind.Grudge, weak.Id, "모욕", w.Month, 20);
            var ctx = EventFixtures.Bind(w, d, e);
            Assert.That(ctx, Is.Not.Null);
            Assert.That(ctx.Roles["avenger"], Is.EqualTo(avenger.Id));
            Assert.That(ctx.Roles["target"], Is.EqualTo(target.Id));
        }

        [Test]
        public void NoCandidate_ReturnsNull()
        {
            var e = Duel();
            var avenger = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(avenger, MemoryKind.Grudge, "nobody", "모욕", w.Month, 70);
            Assert.That(EventFixtures.Bind(w, d, e), Is.Null);
        }

        [Test]
        public void FixedDecider_RelativePick_FindsHolder()
        {
            var e = Duel();
            var player = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            for (int i = 0; i < 20; i++)
            {
                var other = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
                var victim = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
                Opinion.AddMemory(other, MemoryKind.Grudge, victim.Id, "모욕", w.Month, 80);
            }
            var hater = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(hater, MemoryKind.Grudge, player.Id, "모욕", w.Month, 80);
            var ctx = EventFixtures.Bind(w, d, e, new Dictionary<string, string> { ["target"] = player.Id });
            Assert.That(ctx, Is.Not.Null);
            Assert.That(ctx.Roles["avenger"], Is.EqualTo(hater.Id));
            Assert.That(ctx.IsPlayerDecider, Is.False);
            w.PlayerCharacterId = player.Id;
            Assert.That(ctx.IsPlayerDecider, Is.True);
        }

        [Test]
        public void SameSeed_SameBinding()
        {
            var e = EventFixtures.Blank(d);
            for (int i = 0; i < 30; i++) TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var x = EventFixtures.Bind(w, d, e, seed: 5);
            var y = EventFixtures.Bind(w, d, e, seed: 5);
            Assert.That(x.Roles, Is.EqualTo(y.Roles));
        }

        [Test]
        public void RolesDistinct()
        {
            var e = EventFixtures.Blank(d);
            TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Assert.That(EventFixtures.Bind(w, d, e), Is.Null, "한 사람이 두 역할을 맡으면 안 된다");
            TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var ctx = EventFixtures.Bind(w, d, e);
            Assert.That(ctx.Roles["a"], Is.Not.EqualTo(ctx.Roles["b"]));
        }

        [Test]
        public void ExcludeDecider()
        {
            var e = EventFixtures.Blank(d);
            var p = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var q = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            for (ulong seed = 1; seed < 10; seed++)
            {
                var ctx = RoleBinder.TryBind(w, d, new GameRandom(seed), e, null, CandidateIndex.Build(w), true, p.Id);
                Assert.That(ctx.Roles["a"], Is.EqualTo(q.Id));
            }
        }

        [Test]
        public void New_SpawnedNotInWorld()
        {
            var e = EventFixtures.Add(d, "{\"id\":\"sp\",\"name\":\"x\",\"category\":\"wanderer\",\"decider\":\"a\",\"text\":\"t\"," +
                "\"roles\":{\"a\":{\"pick\":\"anyAdult\"},\"bandit\":{\"pick\":\"new\",\"of\":\"a\",\"spawn\":{\"epithet\":\"산적\",\"ageMin\":30,\"ageMax\":30,\"realmMin\":2,\"realmMax\":2}}}," +
                "\"choices\":[{\"text\":\"x\",\"effects\":[]}]}");
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            a.LocationRegionId = "b";
            int before = w.Characters.Count;
            var ctx = EventFixtures.Bind(w, d, e);
            Assert.That(w.Characters.Count, Is.EqualTo(before));
            var bandit = ctx.Get("bandit");
            Assert.That(bandit, Is.Not.Null);
            Assert.That(ctx.IsSpawned(bandit), Is.True);
            Assert.That(bandit.Realm, Is.EqualTo(2));
            Assert.That(bandit.AgeYears(w.Month), Is.EqualTo(30));
            Assert.That(bandit.LocationRegionId, Is.EqualTo("b"));
        }

        [Test]
        public void Month_Restricts()
        {
            var e = EventFixtures.Blank(d);
            e.Month = 4;
            TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            w.Month = 12 * 100 + 2;
            Assert.That(EventFixtures.Bind(w, d, e), Is.Null);
            w.Month = 12 * 100 + 3;
            Assert.That(EventFixtures.Bind(w, d, e), Is.Not.Null);
        }

        [Test]
        public void RivalAndDemonicNeighbor()
        {
            var e = EventFixtures.Add(d, "{\"id\":\"rv\",\"name\":\"x\",\"category\":\"faction\",\"decider\":\"a\",\"text\":\"t\"," +
                "\"roles\":{\"a\":{\"pick\":\"leader\"},\"r\":{\"pick\":\"rivalLeader\",\"of\":\"a\"},\"m\":{\"pick\":\"demonicNeighborLeader\",\"of\":\"a\"}}," +
                "\"choices\":[{\"text\":\"x\",\"effects\":[]}]}");
            TestWorlds.AddFaction(w, "good", Alignment.Righteous, "a", 10);
            TestWorlds.AddFaction(w, "bad", Alignment.Unorthodox, "a", 10);
            TestWorlds.AddFaction(w, "evil", Alignment.Demonic, "b", 10);
            var g = TestWorlds.AddCharacter(w, "good", Role.Leader, Sex.Male, 40);
            var bLeader = TestWorlds.AddCharacter(w, "bad", Role.Leader, Sex.Male, 40);
            var eLeader = TestWorlds.AddCharacter(w, "evil", Role.Leader, Sex.Male, 40);
            var ctx = EventFixtures.Bind(w, d, e, new Dictionary<string, string> { ["a"] = g.Id });
            Assert.That(ctx.Roles["r"], Is.EqualTo(bLeader.Id));
            Assert.That(ctx.Roles["m"], Is.EqualTo(eLeader.Id));
        }

        [Test]
        public void ChildAndSpouse()
        {
            var e = EventFixtures.Add(d, "{\"id\":\"fam\",\"name\":\"x\",\"category\":\"trait\",\"decider\":\"a\",\"text\":\"t\"," +
                "\"roles\":{\"a\":{\"pick\":\"anyAdult\",\"married\":true},\"s\":{\"pick\":\"spouse\",\"of\":\"a\"},\"k\":{\"pick\":\"child\",\"of\":\"a\",\"ageMin\":16}}," +
                "\"choices\":[{\"text\":\"x\",\"effects\":[]}]}");
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 50);
            var s = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 48);
            var kid = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 20);
            var baby = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 2);
            a.SpouseId = s.Id;
            s.SpouseId = a.Id;
            a.ChildIds.AddRange(new[] { kid.Id, baby.Id });
            var ctx = EventFixtures.Bind(w, d, e);
            Assert.That(ctx.Roles["a"], Is.EqualTo(a.Id));
            Assert.That(ctx.Roles["s"], Is.EqualTo(s.Id));
            Assert.That(ctx.Roles["k"], Is.EqualTo(kid.Id));
        }
    }
}
