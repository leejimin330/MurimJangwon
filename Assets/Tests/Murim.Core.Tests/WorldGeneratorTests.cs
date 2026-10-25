using System.Linq;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Persistence;
using Murim.Core.Random;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class WorldGeneratorTests
    {
        static GameData D => TestPaths.LoadShippedData();

        [Test]
        public void Generate_AllMajorFactionsPresent_WithLeaderAndHeir()
        {
            var w = WorldGenerator.Generate(D, new GameRandom(10), null, 0);
            foreach (var def in D.Factions.Values)
            {
                Assert.That(w.Factions.ContainsKey(def.Id), Is.True, def.Id);
                var f = w.Factions[def.Id];
                var leader = w.Get(f.LeaderId);
                Assert.That(leader, Is.Not.Null, def.Id);
                Assert.That(leader.Role, Is.EqualTo(Role.Leader));
                Assert.That(leader.Realm, Is.EqualTo(def.LeaderRealm));
                Assert.That(leader.AgeYears(w.Month), Is.InRange(40, 70));
                Assert.That(w.Get(f.HeirId), Is.Not.Null, def.Id);
                Assert.That(f.MemberIds.Count, Is.AtLeast(def.NamedMembers));
                Assert.That(f.MemberIds.Select(w.Get).All(c => c.LocationRegionId == def.HomeRegion), Is.True);
            }
        }

        [Test]
        public void Generate_MothersWereFertileAgeAtEveryBirth()
        {
            foreach (var seed in new ulong[] { 1, 2, 3, 42 })
                AssertFertileBirths(WorldGenerator.Generate(D, new GameRandom(seed), null, 0), "seed " + seed);
        }

        [Test]
        public void Scenarios_MothersWereFertileAgeAtEveryBirth()
        {
            foreach (var scenario in D.Scenarios.Keys)
                foreach (var seed in new ulong[] { 1, 7, 42 })
                {
                    var s = Session.GameSession.NewGame(D, new Session.NewGameOptions { ScenarioId = scenario, RegionId = "henan", Seed = seed });
                    AssertFertileBirths(s.World, scenario + " seed " + seed);
                }
        }

        static void AssertFertileBirths(WorldState w, string label)
        {
            foreach (var c in w.Characters.Values)
            {
                var mother = w.Get(c.MotherId);
                if (mother == null) continue;
                int ageAtBirth = (c.BirthMonth - mother.BirthMonth) / 12;
                Assert.That(ageAtBirth, Is.InRange(Rules.SimConstants.FertileMinAge, Rules.SimConstants.FertileMaxAge),
                    $"{label}: {mother.FullName} ({mother.AgeYears(w.Month)}세)이 {c.FullName} ({c.AgeYears(w.Month)}세)을 {ageAtBirth}세에 낳음");
            }
        }

        [Test]
        public void Generate_ClanMembersMostlyFamilyWithLeaderSurname()
        {
            var w = WorldGenerator.Generate(D, new GameRandom(11), null, 0);
            var clan = w.Factions["namgung_clan"];
            var leader = w.Get(clan.LeaderId);
            var members = clan.MemberIds.Select(w.Get).ToList();
            Assert.That(members.Count(m => m.FamilyName == leader.FamilyName) * 2, Is.AtLeast(members.Count));
        }

        [Test]
        public void Generate_EveryRegionHasRandomMinorFaction_AndInfluenceAtMost100()
        {
            var w = WorldGenerator.Generate(D, new GameRandom(12), null, 0);
            foreach (var r in w.Regions.Values)
            {
                Assert.That(w.Factions.Values.Count(f => f.IsRandomMinor && f.HomeRegionId == r.Id), Is.InRange(1, 3), r.Id);
                Assert.That(r.Influence.Values.Sum(), Is.AtMost(100), r.Id);
            }
            var names = w.Factions.Values.Select(f => f.Name).ToList();
            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count));
        }

        [Test]
        public void Generate_PlayerRegion_HasMatchingRighteousAndUnorthodoxRival()
        {
            var w = WorldGenerator.Generate(D, new GameRandom(13), "henan", 7);
            var rivals = w.Factions.Values.Where(f => f.IsRandomMinor && f.HomeRegionId == "henan"
                && f.MemberIds.Count >= 6 && f.MemberIds.Count <= 8).ToList();
            Assert.That(rivals.Any(f => f.Alignment == Alignment.Righteous), Is.True);
            Assert.That(rivals.Any(f => f.Alignment == Alignment.Unorthodox), Is.True);
            Assert.That(w.Regions["henan"].Influence.Values.Sum(), Is.AtMost(100));
        }

        [Test]
        public void Generate_SameSeed_SameWorld()
        {
            var a = SaveSystem.Serialize(WorldGenerator.Generate(D, new GameRandom(99), "sichuan", 5));
            var b = SaveSystem.Serialize(WorldGenerator.Generate(D, new GameRandom(99), "sichuan", 5));
            Assert.That(a, Is.EqualTo(b));
        }
    }
}
