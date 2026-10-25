using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class ShippedDataTests
    {
        static GameData D => TestPaths.LoadShippedData();

        [Test]
        public void ShippedData_LoadsAndValidates()
        {
            Assert.That(DataValidator.Validate(D), Is.Empty);
        }

        [Test]
        public void Regions_Are13AndConnected()
        {
            Assert.That(D.Regions.Count, Is.EqualTo(13));
            var seen = new HashSet<string> { "hebei" };
            var queue = new Queue<string>(seen);
            while (queue.Count > 0)
                foreach (var n in D.Regions[queue.Dequeue()].Neighbors)
                    if (seen.Add(n)) queue.Enqueue(n);
            Assert.That(seen.Count, Is.EqualTo(13));
        }

        [Test]
        public void MajorFactions_About100_WithAlignmentMix()
        {
            var f = D.Factions.Values.ToList();
            Assert.That(f.Count, Is.InRange(95, 105));
            Assert.That(f.Count(x => x.Alignment == Alignment.Righteous), Is.InRange(45, 55));
            Assert.That(f.Count(x => x.Alignment == Alignment.Unorthodox), Is.InRange(30, 40));
            Assert.That(f.Count(x => x.Alignment == Alignment.Demonic), Is.InRange(12, 18));
        }

        [Test]
        public void EveryRegion_Has5To10MajorFactions_AndInfluenceAtMost80()
        {
            foreach (var r in D.Regions.Keys)
            {
                var inRegion = D.Factions.Values.Where(f => f.HomeRegion == r).ToList();
                Assert.That(inRegion.Count, Is.InRange(5, 10), r);
                Assert.That(inRegion.Sum(f => f.Influence), Is.AtMost(80), r);
            }
        }

        [Test]
        public void Traits_About20_AndOppositesAreMutual()
        {
            Assert.That(D.Traits.Count, Is.InRange(18, 24));
            foreach (var t in D.Traits.Values)
                foreach (var o in t.Opposites)
                    Assert.That(D.Traits[o].Opposites, Does.Contain(t.Id), $"{t.Id} ↔ {o}");
        }

        [Test]
        public void Scenarios_AllFivePresent()
        {
            Assert.That(D.Scenarios.Keys,
                Is.EquivalentTo(new[] { "minor_sect", "fallen_clan", "wandering_hero", "ronin", "black_gang" }));
        }

        [Test]
        public void Names_EnoughForVariety()
        {
            Assert.That(D.Names.FamilyNames.Distinct().Count(), Is.AtLeast(80));
            Assert.That(D.Names.MaleGivenNames.Distinct().Count(), Is.AtLeast(150));
            Assert.That(D.Names.FemaleGivenNames.Distinct().Count(), Is.AtLeast(150));
        }

        [Test]
        public void NameParts_EnoughForRandomFactions()
        {
            Assert.That(D.FactionNameParts.Prefixes.Distinct().Count(), Is.AtLeast(40));
            Assert.That(D.FactionNameParts.RighteousSuffixes.Length, Is.AtLeast(10));
            Assert.That(D.FactionNameParts.UnorthodoxSuffixes.Length, Is.AtLeast(10));
        }
    }
}
