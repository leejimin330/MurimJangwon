using System.Linq;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Martial;
using Murim.Core.Session;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class ArtEventTests
    {
        GameData d;
        WorldState w;
        Character a;

        [SetUp]
        public void Setup()
        {
            var r = DataLoader.LoadFromDirectory(TestPaths.DataDir);
            Assert.That(r.Ok, string.Join("\n", r.Errors));
            d = r.Data;
            w = TestWorlds.Empty(d);
            a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
        }

        EventContext Ctx(EventDef e, params (string, Character)[] roles) => EventFixtures.Context(w, d, e, roles);

        EventDef One(string effectJson, string extraRoles = "") => EventFixtures.Add(d,
            "{\"id\":\"t\",\"name\":\"시험\",\"category\":\"martial\",\"decider\":\"a\",\"text\":\"{a}\"," +
            "\"roles\":{\"a\":{\"pick\":\"anyAdult\"}" + extraRoles + "},\"choices\":[{\"text\":\"한다\",\"effects\":[" + effectJson + "]}]}");

        static string Why(Conditions c, Character ch, EventContext ctx) => ConditionCheck.Why(c, ch, ctx);

        [Test]
        public void Conditions_ArtChecks()
        {
            var ctx = Ctx(EventFixtures.Blank(d), ("a", a));
            a.Arts.Add(new KnownArt { ArtId = "hunyuan_gong", Mastery = 3 });
            a.Arts.Add(new KnownArt { ArtId = "taiji_sword", Mastery = 6 });
            a.MainArtId = "taiji_sword";
            a.Qi = 35 * 12;

            Assert.That(Why(new Conditions { KnowsArt = "taiji_sword" }, a, ctx), Is.Null);
            Assert.That(Why(new Conditions { KnowsArt = "plum_sword" }, a, ctx), Is.EqualTo("매화검법을 알아야 합니다"));
            Assert.That(Why(new Conditions { NotKnowsArt = "taiji_sword" }, a, ctx), Is.Not.Null);
            Assert.That(Why(new Conditions { NotKnowsArt = "plum_sword" }, a, ctx), Is.Null);
            Assert.That(Why(new Conditions { ArtGradeMin = 4 }, a, ctx), Is.Null);
            Assert.That(Why(new Conditions { ArtGradeMin = 5 }, a, ctx), Is.EqualTo("신공 이상의 무공이 필요합니다"));
            Assert.That(Why(new Conditions { QiMin = 35 }, a, ctx), Is.Null);
            Assert.That(Why(new Conditions { QiMin = 36 }, a, ctx), Is.EqualTo("내공 36년이 필요합니다"));
            Assert.That(Why(new Conditions { MasteryMin = 6 }, a, ctx), Is.Null);
            Assert.That(Why(new Conditions { MasteryMin = 7 }, a, ctx), Is.EqualTo("주무공 성취 7성이 필요합니다"));
            Assert.That(Why(new Conditions { CanBreakthrough = true }, a, ctx), Is.Not.Null);
            a.Enlightenment = 100;
            Assert.That(Why(new Conditions { CanBreakthrough = true }, a, ctx), Is.Null);
            Assert.That(Why(new Conditions { CanBreakthrough = false }, a, ctx), Is.Not.Null);
        }

        [Test]
        public void LearnArt_RandomRespectsGradeAndAlignment()
        {
            var e = One("{\"learnArt\":{\"who\":\"a\",\"gradeMin\":2,\"gradeMax\":3}}");
            for (ulong seed = 1; seed <= 30; seed++)
            {
                a.Arts.Clear();
                var ctx = Ctx(e, ("a", a));
                ctx.Rng = new Random.GameRandom(seed);
                EventResolver.Resolve(ctx, 0);
                var learned = d.Arts[a.Arts.Single().ArtId];
                Assert.That(learned.Grade, Is.InRange(2, 3));
                Assert.That(learned.Alignment, Is.EqualTo(Alignment.Righteous));
            }
        }

        [Test]
        public void LearnArt_DemonicOverride()
        {
            var e = One("{\"learnArt\":{\"who\":\"a\",\"alignment\":\"Demonic\",\"gradeMin\":3,\"gradeMax\":4}}");
            EventResolver.Resolve(Ctx(e, ("a", a)), 0);
            Assert.That(d.Arts[a.Arts.Single().ArtId].Alignment, Is.EqualTo(Alignment.Demonic));
        }

        [Test]
        public void LearnArt_FromRole_PicksKnownArt()
        {
            var teacher = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 60);
            teacher.Arts.Add(new KnownArt { ArtId = "breeze_sword", Mastery = 8 });
            var e = One("{\"learnArt\":{\"who\":\"a\",\"from\":\"b\"}}", ",\"b\":{\"pick\":\"sameRegion\",\"of\":\"a\"}");
            var ctx = Ctx(e, ("a", a), ("b", teacher));
            var text = EventResolver.Resolve(ctx, 0);
            Assert.That(a.KnownArt("breeze_sword")?.Mastery, Is.EqualTo(1));
            Assert.That(text, Does.Contain("청풍검법을 익혔다"));
            Assert.That(a.MainArtId, Is.EqualTo("breeze_sword"));
        }

        [Test]
        public void Breakthrough_Effect_Branches()
        {
            var e = One("{\"breakthrough\":{\"who\":\"a\",\"bonus\":0.5,\"success\":[{\"chronicle\":{\"text\":\"성공\"}}],\"fail\":[{\"chronicle\":{\"text\":\"실패\"}}]}}");
            a.Insight = 20;
            a.Qi = 10 * 12;
            a.Enlightenment = 100;
            var text = EventResolver.Resolve(Ctx(e, ("a", a)), 0);
            Assert.That(a.Realm, Is.EqualTo(1));
            Assert.That(text, Does.Contain("이류의 경지에 올랐다").And.Contain("성공"));

            var text2 = EventResolver.Resolve(Ctx(e, ("a", a)), 0);
            Assert.That(text2, Does.Contain("때가 무르익지 않았다").And.Contain("실패"));
        }

        [Test]
        public void ClosedTraining_Effect_SetsTraining()
        {
            var e = One("{\"closedTraining\":{\"who\":\"a\",\"months\":6}}");
            EventResolver.Resolve(Ctx(e, ("a", a)), 0);
            Assert.That(a.TrainingUntil, Is.EqualTo(w.Month + 6));
            Assert.That(a.TrainingMonths, Is.EqualTo(6));
            Assert.That(a.BusyUntilMonth, Is.EqualTo(w.Month + 6));
            Assert.That(a.InTraining(w.Month + 5), Is.True);
            Assert.That(a.InTraining(w.Month + 6), Is.False);
        }

        [Test]
        public void Validator_UnknownArtAndBadMonths()
        {
            One("{\"learnArt\":{\"who\":\"a\",\"art\":\"nope\"}},{\"closedTraining\":{\"who\":\"a\",\"months\":5}}");
            var errors = EventValidator.Validate(d);
            Assert.That(errors, Has.Some.Contains("알 수 없는 무공 'nope'"));
            Assert.That(errors, Has.Some.Contains("3·6·12개월"));
        }

        [Test]
        public void Shipped_ArtEventsPresent()
        {
            foreach (var id in new[] { "art_breakthrough_chance", "art_closed_training_end", "art_enlightenment", "art_manual_found",
                         "art_master_teaching", "art_deviation_crisis", "art_elixir", "art_sect_tournament", "art_stolen_manual",
                         "art_rival_demonstration", "art_disciple_prodigy_growth", "art_demonic_temptation_deep" })
                Assert.That(d.Events.ContainsKey(id), id);
            Assert.That(d.Events["art_breakthrough_chance"].System);
            Assert.That(DataValidator.Validate(d), Is.Empty);
        }

        [Test]
        public void SystemEvents_NeverPickedByStorytellerOrWorld()
        {
            var s = GameSession.NewGame(d, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 31 }).Auto();
            s.IgnoreGameOver = true;
            for (int m = 0; m < 600; m++) s.EndTurn();
            foreach (var e in s.World.Chronicle.Where(e => e.Kind == "Event:art_breakthrough_chance" || e.Kind == "Event:art_closed_training_end"))
                Assert.That(e.InvolvedIds, Has.Some.Matches<string>(id => s.World.Get(id) != null && s.World.Characters.ContainsKey(id)),
                    "플레이어(또는 대를 이은 플레이어)에게만");
            Assert.That(Storyteller.IsCandidate(d.Events["art_breakthrough_chance"], new EventState { Tension = 1000 },
                d.Storytellers["balanced"], 0), Is.False);
        }
    }
}
