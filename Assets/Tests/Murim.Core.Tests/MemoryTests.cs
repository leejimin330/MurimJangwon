using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class MemoryTests
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
        public void Decay_TakesTenPercent_AndRemovesBelow10()
        {
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(a, MemoryKind.Grudge, "x", "모욕", w.Month, 40);
            Opinion.AddMemory(a, MemoryKind.Grudge, "y", "모욕", w.Month, 10);
            Opinion.DecayMemories(a);
            Assert.That(a.Memories.Count, Is.EqualTo(1));
            Assert.That(a.Memories[0].Strength, Is.EqualTo(36));
        }

        [Test]
        public void Inherit_OnlyStrong_AtHalfStrength()
        {
            var parent = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 60);
            var child = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(parent, MemoryKind.Grudge, "x", "살부지수", w.Month, 60);
            Opinion.AddMemory(parent, MemoryKind.Grudge, "y", "모욕", w.Month, 40);
            Opinion.InheritMemories(parent, new[] { child }, w.Month);
            Assert.That(child.Memories.Count, Is.EqualTo(1));
            Assert.That(child.Memories[0].TargetId, Is.EqualTo("x"));
            Assert.That(child.Memories[0].Strength, Is.EqualTo(30));
        }

        [Test]
        public void StrongGrudge_AddsNemesisTag_StrongDebt_AddsBenefactorTag()
        {
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(a, MemoryKind.Grudge, "x", "모욕", w.Month, 30);
            Assert.That(a.Relations.ContainsKey("x"), Is.False);
            Opinion.AddMemory(a, MemoryKind.Grudge, "x", "모욕", w.Month, 30);
            Assert.That(a.Relations["x"], Does.Contain(RelationTag.Nemesis));
            Opinion.AddMemory(a, MemoryKind.Debt, "y", "선물", w.Month, 40);
            Assert.That(a.Relations["y"], Does.Contain(RelationTag.Benefactor));
        }
    }
}
