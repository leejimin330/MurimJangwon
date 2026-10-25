using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class DataValidatorTests
    {
        static GameData Load(Dictionary<string, string> files)
        {
            var r = DataLoader.LoadFromStrings(files);
            Assert.That(r.Ok, Is.True, string.Join("\n", r.Errors));
            return r.Data;
        }

        [Test]
        public void ValidData_NoErrors()
        {
            Assert.That(DataValidator.Validate(Load(TestJson.MinimalFiles())), Is.Empty);
        }

        [Test]
        public void AsymmetricNeighbor_Reported()
        {
            var f = TestJson.MinimalFiles();
            f["regions.json"] = @"[{""id"":""A"",""name"":""a"",""neighbors"":[""B""]},{""id"":""B"",""name"":""b"",""neighbors"":[]}]";
            f["factions.json"] = "[]";
            var errors = DataValidator.Validate(Load(f));
            Assert.That(errors.Any(e => e.Contains("A") && e.Contains("B")), Is.True);
        }

        [Test]
        public void UnknownHomeRegion_Reported()
        {
            var f = TestJson.MinimalFiles();
            f["factions.json"] = @"[{""id"":""lost"",""name"":""l"",""homeRegion"":""nowhere"",""influence"":10}]";
            var errors = DataValidator.Validate(Load(f));
            Assert.That(errors.Any(e => e.Contains("lost") && e.Contains("nowhere")), Is.True);
        }

        [Test]
        public void UnknownOppositeTrait_Reported()
        {
            var f = TestJson.MinimalFiles();
            f["traits.json"] = @"[{""id"":""bold"",""name"":""호탕"",""opposites"":[""ghost""]}]";
            var errors = DataValidator.Validate(Load(f));
            Assert.That(errors.Any(e => e.Contains("ghost")), Is.True);
        }

        [Test]
        public void RegionInfluenceOver100_Reported()
        {
            var f = TestJson.MinimalFiles();
            f["factions.json"] = @"[{""id"":""a"",""name"":""a"",""homeRegion"":""hebei"",""influence"":60},
                                    {""id"":""b"",""name"":""b"",""homeRegion"":""hebei"",""influence"":50}]";
            var errors = DataValidator.Validate(Load(f));
            Assert.That(errors.Any(e => e.Contains("hebei")), Is.True);
        }
    }
}
