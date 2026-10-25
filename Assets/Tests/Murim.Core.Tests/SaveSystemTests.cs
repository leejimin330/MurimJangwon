using System;
using System.IO;
using Murim.Core.Data;
using Murim.Core.Persistence;
using Murim.Core.Session;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class SaveSystemTests
    {
        static GameData D => TestPaths.LoadShippedData();
        string dir;

        [SetUp]
        public void Setup()
        {
            dir = Path.Combine(Path.GetTempPath(), "murim-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        static GameSession NewSession(ulong seed = 21) =>
            GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "sichuan", Seed = seed }).Auto();

        [Test]
        public void SaveLoadSave_RoundTripIdentical()
        {
            var s = NewSession();
            for (int i = 0; i < 60; i++) s.EndTurn();
            var path = Path.Combine(dir, "a.json");
            Assert.That(s.Save(path).Ok, Is.True);
            var loaded = GameSession.Load(D, path);
            Assert.That(loaded.Ok, Is.True, loaded.Error);
            Assert.That(loaded.Session.Serialize(), Is.EqualTo(s.Serialize()));
        }

        [Test]
        public void Load_ThenContinue_MatchesUninterruptedRun()
        {
            var a = NewSession(5);
            for (int i = 0; i < 120; i++) a.EndTurn();
            var path = Path.Combine(dir, "mid.json");
            a.Save(path);
            var b = GameSession.Load(D, path).Session.Auto();
            for (int i = 0; i < 120; i++) b.EndTurn();

            var c = NewSession(5);
            for (int i = 0; i < 240; i++) c.EndTurn();
            Assert.That(b.Serialize(), Is.EqualTo(c.Serialize()));
        }

        [Test]
        public void Save_ExistingFile_KeepsBak()
        {
            var s = NewSession();
            var path = Path.Combine(dir, "slot1.json");
            s.Save(path);
            var first = File.ReadAllText(path);
            s.EndTurn();
            s.Save(path);
            Assert.That(File.Exists(path + ".bak"), Is.True);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(first));
        }

        [Test]
        public void Load_CorruptFile_ReturnsErrorAndKeepsCurrentSession()
        {
            var current = NewSession();
            current.EndTurn();
            int month = current.World.Month;

            var broken = Path.Combine(dir, "broken.json");
            File.WriteAllText(broken, "{ \"month\": 3, ");
            var r = GameSession.Load(D, broken);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error, Is.Not.Empty);
            Assert.That(r.Session, Is.Null);

            var old = Path.Combine(dir, "old.json");
            current.Save(old);
            File.WriteAllText(old, File.ReadAllText(old).Replace("\"saveVersion\": 2", "\"saveVersion\": 99"));
            var r2 = GameSession.Load(D, old);
            Assert.That(r2.Ok, Is.False);
            Assert.That(r2.Error, Does.Contain("버전"));

            Assert.That(GameSession.Load(D, Path.Combine(dir, "nothing.json")).Ok, Is.False);
            Assert.That(current.World.Month, Is.EqualTo(month));
        }

        [Test]
        public void Load_UnknownTraitReference_Rejected()
        {
            var s = NewSession();
            var path = Path.Combine(dir, "t.json");
            s.Save(path);
            var trait = s.Player.TraitIds[0];
            File.WriteAllText(path, File.ReadAllText(path).Replace($"\"{trait}\"", "\"no_such_trait\""));
            var r = GameSession.Load(D, path);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error, Does.Contain("no_such_trait"));
        }

        [Test]
        public void Serialize_DoesNotDuplicatePlayer()
        {
            var json = SaveSystem.Serialize(NewSession().World);
            Assert.That(json, Does.Not.Contain("\"player\":"));
            Assert.That(json, Does.Not.Contain("\"fullName\":"));
        }
    }
}
