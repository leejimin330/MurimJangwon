using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Generation;
using Murim.Core.Manor;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.Session;
using Murim.Core.Simulation;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class BuildingDataTests
    {
        static List<string> ErrorsFor(string json)
        {
            var files = TestJson.MinimalFiles();
            files["buildings.json"] = json;
            var r = DataLoader.LoadFromStrings(files);
            var errors = new List<string>(r.Errors);
            errors.AddRange(BuildingValidator.Validate(r.Data));
            return errors;
        }

        const string Hall = "{\"id\":\"main_hall\",\"name\":\"본당\",\"width\":4,\"height\":3,\"fixed\":true}";

        [Test]
        public void Validate_UnknownBonusKey_Error() =>
            Assert.That(ErrorsFor("[" + Hall + ",{\"id\":\"x\",\"name\":\"엑스\",\"width\":1,\"height\":1,\"bonuses\":{\"luck\":3}}]"),
                Has.Some.Contains("알 수 없는 보너스 'luck'"));

        [Test]
        public void Validate_SizeOutOfRange_Error() =>
            Assert.That(ErrorsFor("[" + Hall + ",{\"id\":\"x\",\"name\":\"엑스\",\"width\":6,\"height\":1}]"), Has.Some.Contains("1~5 밖"));

        [Test]
        public void Validate_NoMainHall_Error() =>
            Assert.That(ErrorsFor("[{\"id\":\"x\",\"name\":\"엑스\",\"width\":1,\"height\":1}]"), Has.Some.Contains("본당"));

        [Test]
        public void Shipped_HasNineBuildingsAndMainHall()
        {
            var d = TestPaths.LoadShippedData();
            Assert.That(d.Buildings.Count, Is.EqualTo(9));
            Assert.That(d.Buildings["main_hall"].Fixed);
            Assert.That(d.Buildings["training_ground"].Bonuses.MasteryPercent, Is.EqualTo(25));
        }
    }

    public class ManorOpsTests
    {
        static GameData D => TestPaths.LoadShippedData();
        WorldState w;
        Faction f;
        Character leader;

        [SetUp]
        public void Setup()
        {
            w = TestWorlds.Empty(D);
            f = TestWorlds.AddFaction(w, "mine", Alignment.Righteous, "hubei", 10);
            leader = TestWorlds.AddCharacter(w, "mine", Role.Leader, Sex.Male, 40);
            w.PlayerCharacterId = leader.Id;
            ManorOps.EnsurePlayerManor(w, D);
        }

        string Why(string def, int x, int y) => ManorOps.CanPlace(w, D, f, def, x, y).Reason;

        [Test]
        public void CreateDefault_HasMainHall()
        {
            Assert.That(f.Manor, Is.Not.Null);
            var hall = f.Manor.Buildings.Single();
            Assert.That(hall.DefId, Is.EqualTo("main_hall"));
            Assert.That((hall.X, hall.Y), Is.EqualTo((10, 6)));
            Assert.That(ManorOps.IsComplete(w, hall));
        }

        [Test]
        public void Place_Rejections()
        {
            Assert.That(Why("training_ground", 22, 0), Is.EqualTo("장원 밖입니다"));
            Assert.That(Why("training_ground", 9, 5), Is.EqualTo("다른 건물과 겹칩니다"));
            Assert.That(Why("wall", 11, 15), Is.EqualTo("정문 자리는 비워 두어야 합니다"));
            Assert.That(Why("main_hall", 0, 0), Is.EqualTo("지을 수 없는 건물입니다"));
            ManorOps.Place(w, D, f, "library", 0, 0);
            Assert.That(Why("library", 5, 0), Is.EqualTo("장경각은 1개까지 지을 수 있습니다"));
        }

        [Test]
        public void Place_NotEnoughSilver()
        {
            f.Silver = 299;
            Assert.That(Why("training_ground", 0, 0), Is.EqualTo("은자가 부족합니다"));
            Assert.That(f.Silver, Is.EqualTo(299));
        }

        [Test]
        public void Place_DeductsSilverAndSetsCompleteMonth()
        {
            f.Silver = 1000;
            var b = ManorOps.Place(w, D, f, "training_ground", 0, 0);
            Assert.That(f.Silver, Is.EqualTo(700));
            Assert.That(b.CompleteMonth, Is.EqualTo(w.Month + 3));
            Assert.That(ManorOps.IsComplete(w, b), Is.False);
            Assert.That(ManorOps.At(f.Manor, D, 2, 2), Is.SameAs(b));
        }

        [Test]
        public void Demolish_RefundRules()
        {
            f.Silver = 1000;
            var hall = f.Manor.Buildings[0];
            Assert.That(ManorOps.CanDemolish(D, f, hall.Id).Reason, Is.EqualTo("본당은 허물 수 없습니다"));
            var a = ManorOps.Place(w, D, f, "training_ground", 0, 0);
            Assert.That(ManorOps.Demolish(w, D, f, a.Id), Is.EqualTo(300));
            var b = ManorOps.Place(w, D, f, "training_ground", 0, 0);
            w.Month += 3;
            Assert.That(ManorOps.Demolish(w, D, f, b.Id), Is.EqualTo(150));
            Assert.That(f.Silver, Is.EqualTo(1000 - 300 + 300 - 300 + 150));
        }

        [Test]
        public void EnsureManor_OnSuccessionAndFounding()
        {
            var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "wandering_hero", RegionId = "henan", Seed = 3 });
            Assert.That(s.World.Factions.Values.All(x => x.Manor == null), "떠돌이는 장원이 없다");
            var other = s.World.Factions.Values.First(x => !x.IsDissolved && x.LeaderId != null);
            var head = s.World.Get(other.LeaderId);
            s.World.PlayerCharacterId = head.Id;
            ManorOps.EnsurePlayerManor(s.World, D);
            Assert.That(other.Manor, Is.Not.Null);

            var sect = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 3 });
            Assert.That(Wallet.LedFaction(sect.World, sect.Player).Manor, Is.Not.Null, "시나리오 시작");
        }

        [Test]
        public void Load_Version3_CreatesManor()
        {
            var dir = Path.Combine(Path.GetTempPath(), "murim-manor-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 6 });
                Wallet.LedFaction(s.World, s.Player).Manor = null;
                s.World.SaveVersion = 3;
                var path = Path.Combine(dir, "v3.json");
                s.Save(path);
                var a = GameSession.Load(D, path);
                var b = GameSession.Load(D, path);
                Assert.That(a.Ok, a.Error);
                Assert.That(Wallet.LedFaction(a.Session.World, a.Session.Player).Manor.Buildings.Single().DefId, Is.EqualTo("main_hall"));
                Assert.That(a.Session.Serialize(), Is.EqualTo(b.Session.Serialize()));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Load_UnknownBuilding_Rejected()
        {
            var dir = Path.Combine(Path.GetTempPath(), "murim-manor-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var s = GameSession.NewGame(D, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 6 });
                Wallet.LedFaction(s.World, s.Player).Manor.Buildings.Add(new BuildingInstance { Id = "bx", DefId = "castle" });
                var path = Path.Combine(dir, "bad.json");
                s.Save(path);
                var r = GameSession.Load(D, path);
                Assert.That(r.Ok, Is.False);
                Assert.That(r.Error, Does.Contain("알 수 없는 건물 'castle'"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }

    public class ManorBonusTests
    {
        static GameData D => TestPaths.LoadShippedData();
        WorldState w;
        Faction f;
        Character leader;

        [SetUp]
        public void Setup()
        {
            w = TestWorlds.Empty(D);
            f = TestWorlds.AddFaction(w, "mine", Alignment.Righteous, "hubei", 10);
            f.Silver = 10000;
            leader = TestWorlds.AddCharacter(w, "mine", Role.Leader, Sex.Male, 40);
            w.PlayerCharacterId = leader.Id;
            ManorOps.EnsurePlayerManor(w, D);
        }

        BuildingInstance Build(string def, int x, int y, bool complete = true)
        {
            var b = ManorOps.Place(w, D, f, def, x, y);
            if (complete) b.CompleteMonth = w.Month;
            return b;
        }

        TurnContext Ctx(ulong seed = 1) =>
            new TurnContext { World = w, Data = D, Rng = new GameRandom(seed), Generator = new CharacterGenerator(D, new GameRandom(seed)) };

        [Test]
        public void Bonuses_OnlyCompleted()
        {
            Build("training_ground", 0, 0, complete: false);
            Assert.That(ManorOps.Bonuses(w, D, f).MasteryPercent, Is.Zero);
            w.Month += 3;
            Assert.That(ManorOps.Bonuses(w, D, f).MasteryPercent, Is.EqualTo(25));
        }

        [Test]
        public void TwoTrainingGrounds_Plus50Percent()
        {
            Build("training_ground", 0, 0);
            Build("training_ground", 4, 0);
            Assert.That(ManorOps.BonusesFor(w, D, leader).MasteryPercent, Is.EqualTo(50));
            int raised = 0;
            var rng = new GameRandom(5);
            for (int i = 0; i < 2000; i++)
            {
                var c = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 30);
                c.Arts.Add(new KnownArt { ArtId = "basic_breathing", Mastery = 1 });
                Cultivation.YearlyGrowth(w, D, rng, c);
                if (c.Arts[0].Mastery == 2) raised++;
            }
            Assert.That(raised / 2000.0, Is.InRange(0.70, 0.80), "0.5 × 1.5");
        }

        [Test]
        public void Library_AddsEnlightenment()
        {
            Build("library", 0, 0);
            var c = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 30);
            Cultivation.YearlyGrowth(w, D, new GameRandom(1), c);
            Assert.That(c.Enlightenment, Is.EqualTo(10 * Cultivation.InsightPercent / 100 + 3));
        }

        [Test]
        public void Cave_TrainingQiTimesFour_AndBreakthroughBonus()
        {
            var c = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 30);
            c.Arts.Add(new KnownArt { ArtId = "hunyuan_gong", Mastery = 1 });
            int normal = Cultivation.MonthlyQi(w, D, c);
            c.TrainingUntil = w.Month + 3;
            Build("meditation_cave", 0, 0);
            Assert.That(Cultivation.MonthlyQi(w, D, c), Is.EqualTo(normal * 4));
            Assert.That(Cultivation.BreakthroughChance(w, D, c, 0, false), Is.EqualTo(Cultivation.BreakthroughChance(c, 0.05, false)).Within(1e-9));
        }

        [Test]
        public void Apothecary_NoDeviationDeath()
        {
            Build("apothecary", 0, 0);
            var rng = new GameRandom(3);
            for (int i = 0; i < 3000; i++)
            {
                var c = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 30);
                c.Realm = 5;
                c.Insight = 1;
                c.Qi = 180 * 12;
                c.Enlightenment = 100;
                Assert.That(Cultivation.TryBreakthrough(w, D, rng, c, -1, false), Is.Not.EqualTo(BreakthroughOutcome.Died));
            }
        }

        [Test]
        public void MarketStalls_Income()
        {
            Build("market_stall", 0, 0);
            Build("market_stall", 3, 0);
            int before = f.Silver;
            new ManorPhase().Run(Ctx());
            Assert.That(f.Silver, Is.EqualTo(before + 30));
        }

        [Test]
        public void Capacity_BlocksRecruitAndEvent()
        {
            while (Rules.FactionOps.LivingMembers(w, f).Count() < 12) TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 20);
            var recruit = new RecruitNamed(leader.Id);
            Assert.That(recruit.CanExecute(w, D).Reason, Is.EqualTo("숙소가 모자랍니다"));
            Build("dormitory", 0, 0);
            Assert.That(recruit.CanExecute(w, D).Ok);

            while (Rules.FactionOps.LivingMembers(w, f).Count() < 18) TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 20);
            var d = DataLoader.LoadFromDirectory(TestPaths.DataDir).Data;
            var e = EventFixtures.Add(d, "{\"id\":\"join\",\"name\":\"입문\",\"category\":\"faction\",\"decider\":\"a\",\"text\":\"{a}\"," +
                "\"roles\":{\"a\":{\"pick\":\"leader\"},\"b\":{\"pick\":\"anyAdult\"}},\"choices\":[{\"text\":\"받는다\",\"effects\":[{\"recruit\":{\"who\":\"b\",\"faction\":\"a\"}}]}]}");
            var outsider = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 20);
            var text = EventResolver.Resolve(EventFixtures.Context(w, d, e, ("a", leader), ("b", outsider)), 0);
            Assert.That(outsider.FactionId, Is.Null);
            Assert.That(text, Does.Contain("숙소가 모자라"));
        }

        [Test]
        public void Phase_CompletionChronicle()
        {
            var b = Build("library", 0, 0, complete: false);
            w.Month = b.CompleteMonth - 1;
            new ManorPhase().Run(Ctx());
            Assert.That(w.Chronicle.Last().Text, Is.EqualTo("mine파의 장원에 장경각이 들어섰다."));
        }
    }

    public class ManorLifeTests
    {
        static GameData D => TestPaths.LoadShippedData();
        WorldState w;
        Faction f;

        [SetUp]
        public void Setup()
        {
            w = TestWorlds.Empty(D);
            f = TestWorlds.AddFaction(w, "mine", Alignment.Righteous, "hubei", 10);
            f.Silver = 10000;
            var leader = TestWorlds.AddCharacter(w, "mine", Role.Leader, Sex.Male, 40);
            w.PlayerCharacterId = leader.Id;
            ManorOps.EnsurePlayerManor(w, D);
        }

        void Build(string def, int x, int y) => ManorOps.Place(w, D, f, def, x, y).CompleteMonth = w.Month;

        [Test]
        public void Schedule_ByHour()
        {
            Build("training_ground", 0, 0);
            Build("dormitory", 0, 4);
            var c = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 20);
            Assert.That(ManorLife.Spot(w, D, f.Manor, c, 6).Building.DefId, Is.EqualTo("training_ground"));
            Assert.That(ManorLife.Spot(w, D, f.Manor, c, 6).Activity, Is.EqualTo("수련"));
            Assert.That(ManorLife.Spot(w, D, f.Manor, c, 2).Building.DefId, Is.EqualTo("dormitory"));
            Assert.That(ManorLife.Spot(w, D, f.Manor, c, 12).Building.DefId, Is.EqualTo("main_hall"));
            var spot = ManorLife.Spot(w, D, f.Manor, c, 6);
            Assert.That(spot.X, Is.InRange(0f, 3f));
            Assert.That(spot.Y, Is.InRange(0f, 3f));
        }

        [Test]
        public void NoLibrary_FallsBackToMainHall()
        {
            var c = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 20);
            var spot = ManorLife.Spot(w, D, f.Manor, c, 10);
            Assert.That(spot.Activity, Is.EqualTo("공부"));
            Assert.That(spot.Building.DefId, Is.EqualTo("main_hall"));
        }

        [Test]
        public void Training_StaysInCave()
        {
            Build("meditation_cave", 0, 0);
            var c = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 20);
            c.TrainingUntil = w.Month + 3;
            foreach (var h in new[] { 0, 7, 13, 22 })
                Assert.That(ManorLife.Spot(w, D, f.Manor, c, h).Building.DefId, Is.EqualTo("meditation_cave"));
        }

        [Test]
        public void SameInput_SameSpot_DifferentPeople_DifferentSpots()
        {
            Build("training_ground", 0, 0);
            var a = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 20);
            var b = TestWorlds.AddCharacter(w, "mine", Role.Disciple, Sex.Male, 20);
            var x = ManorLife.Spot(w, D, f.Manor, a, 7);
            var y = ManorLife.Spot(w, D, f.Manor, a, 7);
            var z = ManorLife.Spot(w, D, f.Manor, b, 7);
            Assert.That((x.X, x.Y), Is.EqualTo((y.X, y.Y)));
            Assert.That((x.X, x.Y), Is.Not.EqualTo((z.X, z.Y)));
        }
    }
}
