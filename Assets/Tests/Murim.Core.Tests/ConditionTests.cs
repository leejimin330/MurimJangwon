using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Rules;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class ConditionTests
    {
        GameData d;
        WorldState w;
        EventContext ctx;
        Character a;
        Character b;

        [SetUp]
        public void Setup()
        {
            d = EventFixtures.Data();
            w = TestWorlds.Empty(d);
            a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30, "bold");
            b = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 25);
            ctx = EventFixtures.Context(w, d, EventFixtures.Blank(d), ("a", a), ("b", b));
        }

        string Why(Conditions c, Character ch = null) => ConditionCheck.Why(c, ch ?? a, ctx);

        [Test]
        public void Age()
        {
            Assert.That(Why(new Conditions { AgeMin = 30, AgeMax = 30 }), Is.Null);
            Assert.That(Why(new Conditions { AgeMin = 31 }), Is.EqualTo("31세 이상이어야 합니다"));
            Assert.That(Why(new Conditions { AgeMax = 29 }), Is.EqualTo("29세 이하여야 합니다"));
        }

        [Test]
        public void Dead_FailsUnlessIncluded()
        {
            a.DeathMonth = w.Month;
            Assert.That(Why(new Conditions()), Is.EqualTo("세상을 떠난 사람입니다"));
            Assert.That(Why(new Conditions { IncludeDead = true }), Is.Null);
        }

        [Test]
        public void Traits()
        {
            Assert.That(Why(new Conditions { TraitsAll = new[] { "bold" } }), Is.Null);
            Assert.That(Why(new Conditions { TraitsNone = new[] { "bold" } }), Is.EqualTo("호탕은 고를 수 없습니다"));
            Assert.That(Why(new Conditions { TraitsAny = new[] { "sly", "bold" } }), Is.Null);
            Assert.That(Why(new Conditions { TraitsAny = new[] { "sly", "coward" } }), Is.EqualTo("음험·겁쟁이 중 하나가 있어야 합니다"));
        }

        [Test]
        public void Why_TraitReason() =>
            Assert.That(Why(new Conditions { TraitsAll = new[] { "hot_tempered" } }), Is.EqualTo("다혈질만 고를 수 있습니다"));

        [Test]
        public void Roles_NoneMeansUnaffiliated()
        {
            TestWorlds.AddFaction(w, "f", Alignment.Righteous, "a", 10);
            var m = TestWorlds.AddCharacter(w, "f", Role.Elder, Sex.Male, 40);
            Assert.That(Why(new Conditions { Roles = new[] { Role.None } }), Is.Null);
            Assert.That(Why(new Conditions { Roles = new[] { Role.None } }, m), Is.Not.Null);
            Assert.That(Why(new Conditions { Roles = new[] { Role.Elder, Role.Leader } }, m), Is.Null);
        }

        [Test]
        public void Alignment_UnaffiliatedByChivalry()
        {
            a.Chivalry = 10;
            Assert.That(Why(new Conditions { Alignment = new[] { Alignment.Righteous } }), Is.Null);
            a.Chivalry = -1;
            Assert.That(Why(new Conditions { Alignment = new[] { Alignment.Righteous } }), Is.Not.Null);
            Assert.That(Why(new Conditions { Alignment = new[] { Alignment.Unorthodox } }), Is.Null);
        }

        [Test]
        public void Silver_UsesWallet()
        {
            var f = TestWorlds.AddFaction(w, "f", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "f", Role.Leader, Sex.Male, 40);
            f.Silver = 300;
            leader.Silver = 0;
            Assert.That(Why(new Conditions { SilverMin = 200 }, leader), Is.Null);
            a.Silver = 100;
            Assert.That(Why(new Conditions { SilverMin = 200 }), Is.EqualTo("은자 200냥이 필요합니다"));
        }

        [Test]
        public void Married()
        {
            Assert.That(Why(new Conditions { Married = false }), Is.Null);
            a.SpouseId = b.Id;
            Assert.That(Why(new Conditions { Married = true }), Is.Null);
            b.DeathMonth = w.Month;
            Assert.That(Why(new Conditions { Married = true }), Is.Not.Null);
        }

        [Test]
        public void HasFlag()
        {
            Assert.That(Why(new Conditions { HasFlag = "injured" }), Is.Not.Null);
            a.SetFlag("injured", w.Month, 3);
            Assert.That(Why(new Conditions { HasFlag = "injured" }), Is.Null);
            Assert.That(Why(new Conditions { NotFlag = "injured" }), Is.Not.Null);
        }

        [Test]
        public void Memory_MinStrengthAndInherited()
        {
            Opinion.AddMemory(a, MemoryKind.Grudge, b.Id, "모욕", w.Month, 50, true);
            Assert.That(Why(new Conditions { Memory = new MemoryCond { Kind = MemoryKind.Grudge, MinStrength = 50 } }), Is.Null);
            Assert.That(Why(new Conditions { Memory = new MemoryCond { Kind = MemoryKind.Grudge, MinStrength = 51 } }), Is.Not.Null);
            Assert.That(Why(new Conditions { Memory = new MemoryCond { Kind = MemoryKind.Grudge, Inherited = false } }), Is.Not.Null);
            Assert.That(Why(new Conditions { Memory = new MemoryCond { Kind = MemoryKind.Grudge, Target = "b" } }), Is.Null);
            Assert.That(Why(new Conditions { Memory = new MemoryCond { Kind = MemoryKind.Debt } }), Is.Not.Null);
        }

        [Test]
        public void OpinionOf()
        {
            w.Opinions.Add(a.Id, b.Id, 40);
            Assert.That(Why(new Conditions { OpinionOf = new OpinionCond { Role = "b", Min = 40 } }), Is.Null);
            Assert.That(Why(new Conditions { OpinionOf = new OpinionCond { Role = "b", Min = 41 } }), Is.EqualTo("호감이 모자랍니다"));
            Assert.That(Why(new Conditions { OpinionOf = new OpinionCond { Role = "b", Max = 39 } }), Is.Not.Null);
        }

        [Test]
        public void RegionStability()
        {
            w.Regions["a"].Stability = 40;
            Assert.That(Why(new Conditions { RegionStabilityMax = 45 }), Is.Null);
            Assert.That(Why(new Conditions { RegionStabilityMin = 45 }), Is.Not.Null);
        }

        [Test]
        public void Chance_Zero_Fails()
        {
            Assert.That(Why(new Conditions { Chance = 0 }), Is.Not.Null);
            Assert.That(Why(new Conditions { Chance = 1 }), Is.Null);
        }

        [Test]
        public void ChivalryAndFame()
        {
            a.Chivalry = -40;
            a.Fame = 5;
            Assert.That(Why(new Conditions { ChivalryMax = -30 }), Is.Null);
            Assert.That(Why(new Conditions { ChivalryMin = 0 }), Is.EqualTo("협의가 0 이상이어야 합니다"));
            Assert.That(Why(new Conditions { FameMin = 20 }), Is.EqualTo("명성이 20 이상이어야 합니다"));
        }
    }
}
