using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class OpinionTests
    {
        GameData d;
        WorldState w;

        [SetUp]
        public void Setup()
        {
            d = TestWorlds.MinimalData();
            w = TestWorlds.Empty(d);
        }

        [Test]
        public void SharedTrait_Plus10_OppositeTrait_Minus10()
        {
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30, "bold");
            var b = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30, "bold");
            var c = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30, "sly");
            Assert.That(Opinion.Of(w, d, a.Id, b.Id), Is.EqualTo(10));
            Assert.That(Opinion.Of(w, d, a.Id, c.Id), Is.EqualTo(-10));
        }

        [Test]
        public void GrudgeMemory_Subtracts_DebtMemory_AddsHalf()
        {
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var b = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(a, MemoryKind.Grudge, b.Id, "모욕", w.Month, 40);
            Assert.That(Opinion.Of(w, d, a.Id, b.Id), Is.EqualTo(-40));
            Opinion.AddMemory(a, MemoryKind.Debt, b.Id, "선물", w.Month, 30);
            Assert.That(Opinion.Of(w, d, a.Id, b.Id), Is.EqualTo(-25));
            Assert.That(Opinion.Of(w, d, b.Id, a.Id), Is.EqualTo(0));
        }

        [Test]
        public void Opinion_ClampedTo100()
        {
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var b = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            w.Opinions.Add(a.Id, b.Id, 500);
            Assert.That(Opinion.Of(w, d, a.Id, b.Id), Is.EqualTo(100));
            w.Opinions.Add(a.Id, b.Id, -2000);
            Assert.That(Opinion.Of(w, d, a.Id, b.Id), Is.EqualTo(-100));
        }

        [Test]
        public void OpinionStore_RemovesEntryWhenBackToZero()
        {
            w.Opinions.Add("x", "y", 10);
            w.Opinions.Add("x", "y", -10);
            Assert.That(w.Opinions.Entries, Is.Empty);
        }
    }
}
