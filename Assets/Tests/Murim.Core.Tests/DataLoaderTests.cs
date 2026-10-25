using System.Linq;
using Murim.Core.Data;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class DataLoaderTests
    {
        [Test]
        public void LoadFromStrings_ValidMinimalData_Ok()
        {
            var r = DataLoader.LoadFromStrings(TestJson.MinimalFiles());
            Assert.That(r.Errors, Is.Empty);
            Assert.That(r.Ok, Is.True);
            Assert.That(r.Data.Regions["hebei"].Neighbors, Does.Contain("shandong"));
            Assert.That(r.Data.Factions["shaolin"].Alignment, Is.EqualTo(Alignment.Righteous));
            Assert.That(r.Data.Traits["bold"].StatMods.Charisma, Is.EqualTo(2));
            Assert.That(r.Data.Traits["bold"].ActionWeights["accept_marriage"], Is.EqualTo(5f));
            Assert.That(r.Data.Scenarios["minor_sect"].AllowedAlignments, Has.Length.EqualTo(2));
            Assert.That(r.Data.Names.FamilyNames, Does.Contain("남궁"));
            Assert.That(r.Data.FactionNameParts.Prefixes, Does.Contain("청운"));
        }

        [Test]
        public void LoadFromStrings_MissingFile_ErrorNamesFile()
        {
            var files = TestJson.MinimalFiles();
            files.Remove("traits.json");
            var r = DataLoader.LoadFromStrings(files);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Errors.Any(e => e.Contains("traits.json")), Is.True);
        }

        [Test]
        public void LoadFromStrings_BrokenJson_ErrorNamesFileAndLine()
        {
            var files = TestJson.MinimalFiles();
            files["factions.json"] = "[{\"id\": \"a\",,}]";
            var r = DataLoader.LoadFromStrings(files);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Errors.Any(e => e.Contains("factions.json") && e.Contains("line")), Is.True, string.Join("\n", r.Errors));
        }

        [Test]
        public void LoadFromStrings_DuplicateId_Error()
        {
            var files = TestJson.MinimalFiles();
            files["factions.json"] = @"[{""id"":""dup"",""homeRegion"":""hebei""},{""id"":""dup"",""homeRegion"":""hebei""}]";
            var r = DataLoader.LoadFromStrings(files);
            Assert.That(r.Errors.Any(e => e.Contains("dup")), Is.True);
        }
    }
}
