using System;
using System.IO;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.Session;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class ArtGrantingTests
    {
        static GameData D => TestPaths.LoadShippedData();

        [Test]
        public void Grant_GivesInnerAndExternal()
        {
            var w = TestWorlds.Empty(D);
            TestWorlds.AddFaction(w, "wudang", Alignment.Righteous, "hubei", 10);
            var c = TestWorlds.AddCharacter(w, "wudang", Role.Disciple, Sex.Male, 30);
            c.Realm = 2;
            c.Insight = 12;
            ArtGranting.Grant(w, D, new GameRandom(3), c);

            Assert.That(c.Arts.Count(k => D.Arts[k.ArtId].IsInner), Is.EqualTo(1));
            Assert.That(ArtGranting.Externals(D, c).Count(), Is.InRange(1, 2));
            Assert.That(c.Arts.Any(k => D.Arts[k.ArtId].Faction == "wudang"));
            Assert.That(c.Qi, Is.InRange(20 * 12, 40 * 12));
            Assert.That(c.MainArtId, Is.Not.Null);
            Assert.That(c.Arts.All(k => k.Mastery >= 1 && k.Mastery <= 10));
        }

        [Test]
        public void Grant_Child_OneExternalZeroQi()
        {
            var w = TestWorlds.Empty(D);
            var c = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 3);
            ArtGranting.Grant(w, D, new GameRandom(4), c);
            Assert.That(ArtGranting.Externals(D, c).Count(), Is.EqualTo(1));
            Assert.That(c.Qi, Is.Zero);
            Assert.That(ArtGranting.Inner(D, c), Is.Not.Null);
        }

        [Test]
        public void Grant_Unorthodox_NoDemonicGeneralArts()
        {
            var w = TestWorlds.Empty(D);
            TestWorlds.AddFaction(w, "x", Alignment.Unorthodox, "hubei", 10);
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var c = TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 30);
                c.Realm = 3;
                ArtGranting.Grant(w, D, new GameRandom(seed), c);
                Assert.That(c.Arts.All(k => D.Arts[k.ArtId].Alignment != Alignment.Demonic), seed.ToString());
            }
        }

        [Test]
        public void Grant_MinimalDataWithoutArts_NoOp()
        {
            var d = TestWorlds.MinimalData();
            var w = TestWorlds.Empty(d);
            var c = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            ArtGranting.Grant(w, d, new GameRandom(1), c);
            Assert.That(c.Arts, Is.Empty);
        }

        [Test]
        public void Learn_RespectsMaxAndInnerReplace()
        {
            var w = TestWorlds.Empty(D);
            var c = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Assert.That(ArtGranting.Learn(D, c, "hunyuan_gong", 3), Is.True);
            Assert.That(ArtGranting.Learn(D, c, "basic_breathing", 3), Is.False, "더 낮은 내공심법");
            Assert.That(ArtGranting.Learn(D, c, "free_mind", 1), Is.True, "더 높은 내공심법은 교체");
            Assert.That(ArtGranting.Inner(D, c).Id, Is.EqualTo("free_mind"));
            Assert.That(c.Arts.Count, Is.EqualTo(1));
            foreach (var id in new[] { "three_talent_sword", "taizu_fist", "tiger_saber", "escort_spear", "flying_dagger" })
                Assert.That(ArtGranting.Learn(D, c, id, 1), Is.True, id);
            Assert.That(ArtGranting.Learn(D, c, "breeze_sword", 1), Is.False, "6개가 차면 못 배운다");
            Assert.That(ArtGranting.Learn(D, c, "taizu_fist", 1), Is.False, "이미 안다");
        }

        [Test]
        public void NewGame_EveryoneHasArts()
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 9 });
            var living = s.World.Characters.Values.Where(c => c.IsAlive).ToList();
            Assert.That(living.All(c => c.Arts.Count >= 1));
            var shaolin = living.Where(c => c.FactionId == "shaolin" && c.Realm >= 2).ToList();
            Assert.That(shaolin.Count(c => c.Arts.Any(k => D.Arts[k.ArtId].Faction == "shaolin")), Is.GreaterThan(shaolin.Count / 2));
        }

        [Test]
        public void Load_Version2_MigratesDeterministically()
        {
            var dir = Path.Combine(Path.GetTempPath(), "murim-mig-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 11 });
                foreach (var c in s.World.Characters.Values)
                {
                    c.Arts.Clear();
                    c.Qi = 0;
                    c.Enlightenment = 0;
                    c.MainArtId = null;
                }
                s.World.SaveVersion = 2;
                var path = Path.Combine(dir, "v2.json");
                Assert.That(s.Save(path).Ok);
                var a = GameSession.Load(D, path);
                var b = GameSession.Load(D, path);
                Assert.That(a.Ok, a.Error);
                Assert.That(a.Session.Serialize(), Is.EqualTo(b.Session.Serialize()));
                Assert.That(a.Session.World.Characters.Values.Where(c => c.IsAlive).All(c => c.Arts.Count >= 1));
                Assert.That(a.Session.World.SaveVersion, Is.EqualTo(3));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Load_UnknownArt_Rejected()
        {
            var dir = Path.Combine(Path.GetTempPath(), "murim-bad-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 11 });
                s.Player.Arts.Add(new KnownArt { ArtId = "no_such_art", Mastery = 1 });
                var path = Path.Combine(dir, "bad.json");
                s.Save(path);
                var r = GameSession.Load(D, path);
                Assert.That(r.Ok, Is.False);
                Assert.That(r.Error, Does.Contain("알 수 없는 무공"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
