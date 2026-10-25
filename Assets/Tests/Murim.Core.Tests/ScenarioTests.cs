using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Session;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class ScenarioTests
    {
        static GameData D => TestPaths.LoadShippedData();

        static IEnumerable<TestCaseData> AllScenarioRegionPairs()
        {
            var d = TestPaths.LoadShippedData();
            foreach (var s in d.Scenarios.Keys)
                foreach (var r in d.Regions.Keys)
                    yield return new TestCaseData(s, r);
        }

        static WorldState Start(string scenario, string region, out Character player, ulong seed = 5,
            List<string> traits = null)
        {
            var o = new NewGameOptions { ScenarioId = scenario, RegionId = region, Seed = seed, Sex = Sex.Male };
            if (traits != null) o.ChosenTraitIds = traits;
            var w = SimSetup.NewWorld(D, o, out _);
            player = w.Player;
            return w;
        }

        [TestCaseSource(nameof(AllScenarioRegionPairs))]
        public void Scenario_StartsInEveryRegion(string scenarioId, string regionId)
        {
            var w = Start(scenarioId, regionId, out var p);
            Assert.That(p, Is.Not.Null);
            Assert.That(p.LocationRegionId, Is.EqualTo(regionId));
            foreach (var r in w.Regions.Values) Assert.That(r.Influence.Values.Sum(), Is.AtMost(100), r.Id);
        }

        [Test]
        public void MinorSect_MatchesSpecNumbers()
        {
            var w = Start("minor_sect", "henan", out var p);
            var f = w.GetFaction(p.FactionId);
            Assert.That(f.LeaderId, Is.EqualTo(p.Id));
            Assert.That(f.Silver, Is.EqualTo(500));
            Assert.That(f.Fame, Is.EqualTo(10));
            Assert.That(f.UnnamedDisciples, Is.EqualTo(10));
            Assert.That(Influence.Get(w, "henan", f.Id), Is.EqualTo(5));
            var members = f.MemberIds.Select(w.Get).ToList();
            Assert.That(members.Count(m => m.Role == Role.Disciple || m.Role == Role.Heir), Is.EqualTo(4));
            Assert.That(members.Count(m => m.Role == Role.Family), Is.InRange(1, 3));
            Assert.That(p.Realm, Is.EqualTo(1));
            Assert.That(p.AgeYears(w.Month), Is.InRange(30, 45));
            Assert.That(f.Alignment, Is.EqualTo(Alignment.Righteous));
            Assert.That(f.Chivalry, Is.EqualTo(50));
        }

        [Test]
        public void MinorSect_UnorthodoxChoice_Respected()
        {
            var o = new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 1, Alignment = Alignment.Unorthodox };
            var w = SimSetup.NewWorld(D, o, out _);
            var f = w.GetFaction(w.Player.FactionId);
            Assert.That(f.Alignment, Is.EqualTo(Alignment.Unorthodox));
            Assert.That(f.Chivalry, Is.EqualTo(-50));
        }

        [Test]
        public void FallenClan_HasDebtAndNeighborGrudge()
        {
            var w = Start("fallen_clan", "shandong", out var p);
            var f = w.GetFaction(p.FactionId);
            Assert.That(f.Type, Is.EqualTo(FactionType.Clan));
            Assert.That(f.Debt, Is.EqualTo(800));
            Assert.That(f.Fame, Is.EqualTo(30));
            Assert.That(p.Memories.Count(m => m.Kind == MemoryKind.Grudge && m.Strength == 60), Is.EqualTo(1));
            var enemy = w.Get(p.Memories[0].TargetId);
            Assert.That(enemy.Role, Is.EqualTo(Role.Leader));
            Assert.That(enemy.Memories.Any(m => m.TargetId == p.Id && m.Strength == 30), Is.True);
        }

        [Test]
        public void WanderingHero_NoFaction_PersonalResources()
        {
            var w = Start("wandering_hero", "sichuan", out var p);
            Assert.That(p.FactionId, Is.Null);
            Assert.That(p.Role, Is.EqualTo(Role.None));
            Assert.That(p.Silver, Is.EqualTo(80));
            Assert.That(p.Fame, Is.EqualTo(5));
            Assert.That(p.Chivalry, Is.EqualTo(60));
            Assert.That(p.Realm, Is.EqualTo(1));
            var friend = w.Characters.Values.FirstOrDefault(c => c.Id != p.Id && w.Opinions.GetStored(c.Id, p.Id) >= 40);
            Assert.That(friend, Is.Not.Null);
            Assert.That(w.GetFaction(friend.FactionId).Alignment, Is.EqualTo(Alignment.Righteous));
            Assert.That(w.Opinions.GetStored(p.Id, friend.Id), Is.AtLeast(40));
        }

        [Test]
        public void Ronin_HasPastGrudge()
        {
            var w = Start("ronin", "hubei", out var p);
            Assert.That(p.FactionId, Is.Null);
            Assert.That(p.Realm, Is.EqualTo(2));
            Assert.That(p.Silver, Is.EqualTo(50));
            Assert.That(p.Memories.Single().Strength, Is.EqualTo(60));
            Assert.That(p.Memories.Single().Reason, Is.EqualTo("과거의 원한"));
        }

        [Test]
        public void BlackGang_IsUnorthodox_With20Thugs()
        {
            var w = Start("black_gang", "jiangnan", out var p);
            var f = w.GetFaction(p.FactionId);
            Assert.That(f.Alignment, Is.EqualTo(Alignment.Unorthodox));
            Assert.That(f.UnnamedDisciples, Is.EqualTo(20));
            Assert.That(f.Chivalry, Is.EqualTo(-50));
            Assert.That(f.MemberIds.Select(w.Get).Count(m => m.Role == Role.Disciple || m.Role == Role.Heir), Is.EqualTo(5));
        }

        [Test]
        public void ChosenTraits_AppliedMax2_OppositeIgnored()
        {
            Start("ronin", "hubei", out var p, traits: new List<string> { "bold", "sly", "genius" });
            Assert.That(p.TraitIds, Does.Contain("bold"));
            Assert.That(p.TraitIds, Does.Not.Contain("sly"));
            Assert.That(p.TraitIds, Does.Not.Contain("genius"));

            Start("ronin", "hubei", out var q, traits: new List<string> { "genius", "chivalrous" });
            Assert.That(q.TraitIds, Does.Contain("genius"));
            Assert.That(q.TraitIds, Does.Contain("chivalrous"));
        }

        [Test]
        public void CustomNames_Applied()
        {
            var o = new NewGameOptions
            {
                ScenarioId = "minor_sect", RegionId = "hebei", Seed = 3,
                FamilyName = "독고", GivenName = "구패", FactionName = "독고검문",
            };
            var w = SimSetup.NewWorld(D, o, out _);
            Assert.That(w.Player.FullName, Is.EqualTo("독고구패"));
            Assert.That(w.GetFaction(w.Player.FactionId).Name, Is.EqualTo("독고검문"));
        }
    }
}
