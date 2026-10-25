using System.Linq;
using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class InfluenceTests
    {
        GameData d;
        WorldState w;

        [SetUp]
        public void Setup()
        {
            d = TestWorlds.MinimalData();
            w = TestWorlds.Empty(d);
        }

        static int Sum(WorldState w, string region) => w.Regions[region].Influence.Values.Sum();

        [Test]
        public void Gain_UsesFreeShareFirst()
        {
            TestWorlds.AddFaction(w, "f1", Alignment.Righteous, "a", 70);
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 0);
            Assert.That(Influence.Gain(w, "a", "x", 10), Is.EqualTo(10));
            Assert.That(Influence.Get(w, "a", "f1"), Is.EqualTo(70));
            Assert.That(Influence.Get(w, "a", "x"), Is.EqualTo(10));
        }

        [Test]
        public void Expand_FullRegion_TakesFromStrongestAndNeverExceeds100()
        {
            TestWorlds.AddFaction(w, "f1", Alignment.Righteous, "a", 60);
            TestWorlds.AddFaction(w, "f2", Alignment.Righteous, "a", 40);
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 0);
            Assert.That(Influence.Gain(w, "a", "x", 8), Is.EqualTo(8));
            Assert.That(Influence.Get(w, "a", "f1"), Is.EqualTo(52));
            Assert.That(Influence.Get(w, "a", "f2"), Is.EqualTo(40));
            Assert.That(Sum(w, "a"), Is.EqualTo(100));
        }

        [Test]
        public void Gain_PartlyFreePartlyTaken()
        {
            TestWorlds.AddFaction(w, "f1", Alignment.Righteous, "a", 97);
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 0);
            Assert.That(Influence.Gain(w, "a", "x", 5), Is.EqualTo(5));
            Assert.That(Influence.Get(w, "a", "f1"), Is.EqualTo(95));
            Assert.That(Sum(w, "a"), Is.EqualTo(100));
        }

        [Test]
        public void Gain_AloneAt100_GainsNothing()
        {
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 100);
            Assert.That(Influence.Gain(w, "a", "x", 5), Is.EqualTo(0));
            Assert.That(Influence.Get(w, "a", "x"), Is.EqualTo(100));
        }

        [Test]
        public void Lose_NeverBelowZero_RemovesEntry()
        {
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 3);
            Influence.Lose(w, "a", "x", 10);
            Assert.That(Influence.Get(w, "a", "x"), Is.EqualTo(0));
            Assert.That(w.Regions["a"].Influence.ContainsKey("x"), Is.False);
        }

        [Test]
        public void Total_SumsAllRegions()
        {
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 3);
            w.Regions["b"].Influence["x"] = 4;
            Assert.That(Influence.Total(w, "x"), Is.EqualTo(7));
        }

        [Test]
        public void Dissolve_ReleasesInfluenceAndMembers()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            w.Regions["b"].Influence["x"] = 5;
            var other = TestWorlds.AddFaction(w, "y", Alignment.Righteous, "a", 10);
            w.Alliances.Add(WorldState.PairKey("x", "y"));
            var m = TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 20);
            FactionOps.Dissolve(w, f, "후계자 없음");
            Assert.That(f.IsDissolved, Is.True);
            Assert.That(Influence.Total(w, "x"), Is.EqualTo(0));
            Assert.That(m.FactionId, Is.Null);
            Assert.That(m.Role, Is.EqualTo(Role.None));
            Assert.That(f.MemberIds, Is.Empty);
            Assert.That(w.Alliances, Is.Empty);
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("Dissolved"));
        }

        [Test]
        public void MoveToFaction_UpdatesMembershipAndClearsHeir()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var g = TestWorlds.AddFaction(w, "y", Alignment.Righteous, "b", 10);
            var h = TestWorlds.AddCharacter(w, "x", Role.Heir, Sex.Female, 20);
            FactionOps.MoveToFaction(w, h, "y", Role.Family);
            Assert.That(f.MemberIds, Does.Not.Contain(h.Id));
            Assert.That(f.HeirId, Is.Null);
            Assert.That(g.MemberIds, Does.Contain(h.Id));
            Assert.That(h.FactionId, Is.EqualTo("y"));
            Assert.That(h.Role, Is.EqualTo(Role.Family));
            Assert.That(h.LocationRegionId, Is.EqualTo("b"));
        }
    }
}
