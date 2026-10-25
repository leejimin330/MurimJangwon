using System;
using System.IO;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Session;
using Murim.Core.Simulation;
using Murim.Core.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class EventStateTests
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
        public void Flag_ExpiresAtMonth()
        {
            var c = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            c.SetFlag("injured", 100, 6);
            Assert.That(c.HasFlag("injured", 105), Is.True);
            Assert.That(c.HasFlag("injured", 106), Is.False);
            c.SetFlag("scandal", 100, 0);
            Assert.That(c.HasFlag("scandal", 99999), Is.True);
            c.ClearFlag("scandal");
            Assert.That(c.HasFlag("scandal", 100), Is.False);
        }

        [Test]
        public void YearlyPhase_RemovesExpiredFlagsAndCooldowns()
        {
            w.Month = 1200;
            var c = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            c.Flags["old"] = 1190;
            c.Flags["new"] = 1210;
            c.Flags["forever"] = 0;
            w.Events.SetCooldown("x|*", 1100);
            w.Events.SetCooldown("y|*", 1300);
            new YearlyPhase().Run(new TurnContext { World = w, Data = d, Rng = new GameRandom(1), Generator = new CharacterGenerator(d, new GameRandom(1)) });
            Assert.That(c.Flags.Keys, Is.EquivalentTo(new[] { "new", "forever" }));
            Assert.That(w.Events.Cooldowns.Keys, Is.EquivalentTo(new[] { "y|*" }));
        }

        [Test]
        public void SetCooldown_KeepsLater()
        {
            w.Events.SetCooldown("k", 50);
            w.Events.SetCooldown("k", 40);
            Assert.That(w.Events.IsCooling("k", 49), Is.True);
            Assert.That(w.Events.IsCooling("k", 50), Is.False);
        }

        [Test]
        public void InheritMemories_MarksInherited()
        {
            var parent = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 60);
            var child = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(parent, MemoryKind.Grudge, "x", "모욕", w.Month, 60);
            Opinion.InheritMemories(parent, new[] { child }, w.Month);
            Assert.That(parent.Memories[0].Inherited, Is.False);
            Assert.That(child.Memories[0].Inherited, Is.True);
        }

        static string SaveVersionedCopy(int version, bool dropEvents)
        {
            var d = TestPaths.LoadShippedData();
            var s = GameSession.NewGame(d, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 4 });
            var json = JObject.Parse(s.Serialize());
            json["saveVersion"] = version;
            if (dropEvents) json.Remove("events");
            var path = Path.Combine(Path.GetTempPath(), "murim-v" + version + "-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, json.ToString());
            return path;
        }

        [Test]
        public void Load_Version1_Accepted()
        {
            var path = SaveVersionedCopy(1, true);
            try
            {
                var r = GameSession.Load(TestPaths.LoadShippedData(), path);
                Assert.That(r.Ok, Is.True, r.Error);
                Assert.That(r.Session.World.Events.StorytellerId, Is.EqualTo("balanced"));
                Assert.That(r.Session.World.Events.Pending, Is.Empty);
                Assert.That(r.Session.World.SaveVersion, Is.EqualTo(2));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Load_Version3_Rejected()
        {
            var path = SaveVersionedCopy(3, false);
            try
            {
                var r = GameSession.Load(TestPaths.LoadShippedData(), path);
                Assert.That(r.Ok, Is.False);
                Assert.That(r.Error, Does.Contain("버전"));
            }
            finally { File.Delete(path); }
        }
    }
}
