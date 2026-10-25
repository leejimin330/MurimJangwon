using System.Linq;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class CharacterGeneratorTests
    {
        GameData d;
        WorldState w;

        [SetUp]
        public void Setup()
        {
            d = TestPaths.LoadShippedData();
            w = new WorldState { Month = 1200 };
            foreach (var r in d.Regions.Keys) w.Regions[r] = new RegionState { Id = r };
        }

        [Test]
        public void Create_StatsInRange_TraitsNotOpposed()
        {
            var gen = new CharacterGenerator(d, new GameRandom(1));
            for (int i = 0; i < 500; i++)
            {
                var c = gen.Create(w, i % 2 == 0 ? Sex.Male : Sex.Female, 20, null, Role.None, 0);
                foreach (var s in new[] { c.Constitution, c.Insight, c.Cunning, c.Charisma }) Assert.That(s, Is.InRange(1, 20));
                Assert.That(c.TraitIds.Count, Is.InRange(2, 4));
                Assert.That(c.TraitIds.Distinct().Count(), Is.EqualTo(c.TraitIds.Count));
                foreach (var t in c.TraitIds)
                    Assert.That(d.Traits[t].Opposites.Intersect(c.TraitIds), Is.Empty, c.FullName);
                Assert.That(c.AgeYears(w.Month), Is.EqualTo(20));
                Assert.That(w.Characters.ContainsKey(c.Id), Is.True);
                Assert.That(c.FamilyName, Is.Not.Empty);
            }
        }

        [Test]
        public void CreateChild_InheritsFamilyNameAndFaction_LinksParents()
        {
            var gen = new CharacterGenerator(d, new GameRandom(2));
            var f = new Faction { Id = "fx", Name = "가", HomeRegionId = "henan" };
            w.Factions[f.Id] = f;
            var father = gen.Create(w, Sex.Male, 30, null, Role.None, 1, "남궁");
            var mother = gen.Create(w, Sex.Female, 28, "fx", Role.Family, 0);
            gen.Marry(w, father, mother);
            var child = gen.CreateChild(w, mother, father);
            Assert.That(child.FamilyName, Is.EqualTo("남궁"));
            Assert.That(child.FactionId, Is.EqualTo("fx"));
            Assert.That(child.Role, Is.EqualTo(Role.Family));
            Assert.That(f.MemberIds, Does.Contain(child.Id));
            Assert.That(child.FatherId, Is.EqualTo(father.Id));
            Assert.That(child.MotherId, Is.EqualTo(mother.Id));
            Assert.That(father.ChildIds, Does.Contain(child.Id));
            Assert.That(mother.ChildIds, Does.Contain(child.Id));
            Assert.That(child.AgeYears(w.Month), Is.EqualTo(0));
            Assert.That(father.SpouseId, Is.EqualTo(mother.Id));
            Assert.That(father.HasRelation(mother.Id, RelationTag.Spouse), Is.True);
        }

        [Test]
        public void CreateChild_TraitInheritanceRate_Near40Percent()
        {
            var gen = new CharacterGenerator(d, new GameRandom(3));
            var father = gen.Create(w, Sex.Male, 30, null, Role.None, 0);
            var mother = gen.Create(w, Sex.Female, 28, null, Role.None, 0);
            father.TraitIds.Clear(); father.TraitIds.Add("heaven_bone");
            mother.TraitIds.Clear(); mother.TraitIds.Add("genius");
            int inherited = 0, n = 2000;
            for (int i = 0; i < n; i++)
            {
                var c = gen.CreateChild(w, mother, father);
                if (c.TraitIds.Contains("heaven_bone") || c.TraitIds.Contains("genius")) inherited++;
            }
            Assert.That(inherited / (double)n, Is.InRange(0.30, 0.55));
        }

        [Test]
        public void CreateChild_StatsNearParentAverage()
        {
            var gen = new CharacterGenerator(d, new GameRandom(4));
            var father = gen.Create(w, Sex.Male, 30, null, Role.None, 0);
            var mother = gen.Create(w, Sex.Female, 28, null, Role.None, 0);
            father.TraitIds.Clear(); mother.TraitIds.Clear();
            father.Constitution = 20; mother.Constitution = 10;
            for (int i = 0; i < 200; i++)
            {
                var c = gen.CreateChild(w, mother, father);
                int traitMod = c.TraitIds.Sum(t => d.Traits[t].StatMods.Constitution);
                Assert.That(c.Constitution - traitMod, Is.InRange(11, 19).Or.EqualTo(20).Or.EqualTo(1));
            }
        }
    }
}
