using System.Linq;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.Simulation;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class CultivationTests
    {
        static GameData D => TestPaths.LoadShippedData();
        WorldState w;

        [SetUp]
        public void Setup() => w = TestWorlds.Empty(D);

        Character Adult(int realm = 0, string faction = null)
        {
            var c = TestWorlds.AddCharacter(w, faction, faction == null ? Role.None : Role.Disciple, Sex.Male, 30);
            c.Realm = realm;
            return c;
        }

        static void Ready(Character c)
        {
            c.Qi = Cultivation.ThresholdYears[c.Realm] * 12;
            c.Enlightenment = 100;
        }

        TurnContext Ctx(ulong seed) =>
            new TurnContext { World = w, Data = D, Rng = new GameRandom(seed), Generator = new CharacterGenerator(D, new GameRandom(seed)) };

        [Test]
        public void Threshold_BlocksAndOpens()
        {
            var c = Adult(1);
            c.Qi = 20 * 12 - 1;
            c.Enlightenment = 100;
            Assert.That(Cultivation.CanBreakthrough(c), Is.False, "내공 모자람");
            c.Qi = 20 * 12;
            c.Enlightenment = 99;
            Assert.That(Cultivation.CanBreakthrough(c), Is.False, "깨달음 모자람");
            c.Enlightenment = 100;
            Assert.That(Cultivation.CanBreakthrough(c), Is.True);
            c.Realm = 6;
            Assert.That(Cultivation.CanBreakthrough(c), Is.False, "현경 위는 없다");
        }

        [Test]
        public void Chance_FormulaAndClamp()
        {
            var c = Adult(0);
            c.Insight = 15;
            Assert.That(Cultivation.BreakthroughChance(c, 0, false), Is.EqualTo(0.80).Within(1e-9));
            Assert.That(Cultivation.BreakthroughChance(c, 0, true), Is.EqualTo(0.95).Within(1e-9));
            c.Insight = 20;
            Assert.That(Cultivation.BreakthroughChance(c, 0.5, true), Is.EqualTo(0.95).Within(1e-9));
            c.Realm = 5;
            c.Insight = 1;
            Assert.That(Cultivation.BreakthroughChance(c, 0, false), Is.EqualTo(0.02).Within(1e-9));
        }

        [Test]
        public void Success_RaisesRealmAndResetsEnlightenment()
        {
            var c = Adult(0);
            c.Insight = 20;
            Ready(c);
            var r = Cultivation.TryBreakthrough(w, D, new GameRandom(1), c, 0.5, true);
            Assert.That(r, Is.EqualTo(BreakthroughOutcome.Success));
            Assert.That(c.Realm, Is.EqualTo(1));
            Assert.That(c.Enlightenment, Is.Zero);
        }

        [Test]
        public void Fail_LosesEnlightenmentAndQi()
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                var c = Adult(5);
                c.Insight = 1;
                Ready(c);
                int qi = c.Qi;
                var r = Cultivation.TryBreakthrough(w, D, new GameRandom(seed), c, 0, false);
                if (r == BreakthroughOutcome.Success) continue;
                Assert.That(c.Enlightenment, Is.EqualTo(50));
                Assert.That(c.Qi, Is.EqualTo(qi - qi / 10));
                return;
            }
            Assert.Fail("실패 사례가 없다");
        }

        [Test]
        public void Deviation_DemonicDoubles()
        {
            double Rate(bool demonic)
            {
                int fails = 0, deviations = 0;
                var rng = new GameRandom(demonic ? 99UL : 98UL);
                for (int i = 0; i < 10000; i++)
                {
                    var c = new Character { Id = "t", FamilyName = "장", GivenName = "시험", BirthMonth = w.Month - 360, Realm = 5, Insight = 1, Constitution = 10 };
                    if (demonic) c.Arts.Add(new KnownArt { ArtId = "star_absorb", Mastery = 1 });
                    Ready(c);
                    var r = Cultivation.TryBreakthrough(w, D, rng, c, 0, false);
                    if (r == BreakthroughOutcome.Success) continue;
                    fails++;
                    if (r != BreakthroughOutcome.Failed) deviations++;
                }
                return deviations / (double)fails;
            }
            Assert.That(Rate(false), Is.InRange(0.13, 0.17));
            Assert.That(Rate(true), Is.InRange(0.28, 0.32));
        }

        [Test]
        public void MonthlyQi_TrainingTriples()
        {
            var c = Adult();
            c.Arts.Add(new KnownArt { ArtId = "hunyuan_gong", Mastery = 1 }); // 이류 심법
            int normal = Cultivation.MonthlyQi(D, c, w.Month);
            Assert.That(normal, Is.EqualTo(1 + (10 + 2 * 4) / 10));
            c.TrainingUntil = w.Month + 3;
            Assert.That(Cultivation.MonthlyQi(D, c, w.Month), Is.EqualTo(normal * 3));
        }

        [Test]
        public void MonthlyQi_DemonicInnerPlusOneGrade_ChildrenNone()
        {
            var c = Adult();
            c.Constitution = 4;
            c.Arts.Add(new KnownArt { ArtId = "corpse_blood_art", Mastery = 1 }); // 마공 3등급 → 4로 계산
            Assert.That(Cultivation.MonthlyQi(D, c, w.Month), Is.EqualTo(1 + (4 + 16) / 10));
            var kid = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 4);
            Assert.That(Cultivation.MonthlyQi(D, kid, w.Month), Is.Zero);
        }

        [Test]
        public void YearlyGrowth_EnlightenmentAndMasteryProbability()
        {
            int raised = 0;
            var rng = new GameRandom(5);
            for (int i = 0; i < 2000; i++)
            {
                var c = new Character { Id = "t", BirthMonth = w.Month - 360, Insight = 10, Constitution = 10, DuelWinsThisYear = 5 };
                c.Arts.Add(new KnownArt { ArtId = "basic_breathing", Mastery = 1 }); // 1등급, 외공이 없으니 주무공 아님
                Cultivation.YearlyGrowth(w, D, rng, c);
                if (c.Arts[0].Mastery == 2) raised++;
                if (i == 0)
                {
                    Assert.That(c.Enlightenment, Is.EqualTo(5 + 2 * 3));
                    Assert.That(c.DuelWinsThisYear, Is.Zero);
                }
            }
            Assert.That(raised / 2000.0, Is.InRange(0.45, 0.55));
        }

        [Test]
        public void Join_LearnsFactionSecretNextJanuary()
        {
            TestWorlds.AddFaction(w, "wudang", Alignment.Righteous, "hubei", 10);
            var c = Adult(0, "wudang");
            c.Arts.Add(new KnownArt { ArtId = "basic_breathing", Mastery = 1 });
            w.Month = 12 * 100 + 5;
            new CultivationPhase().Run(Ctx(3));
            Assert.That(c.Arts.Any(k => D.Arts[k.ArtId].Faction == "wudang"), Is.False, "1월이 아니면 아직");
            w.Month = 12 * 101;
            new CultivationPhase().Run(Ctx(3));
            Assert.That(c.Arts.Any(k => D.Arts[k.ArtId].Faction == "wudang" && k.Mastery == 1), Is.True);
        }

        [Test]
        public void Phase_AiBreaksThroughInJanuary_PlayerDoesNot()
        {
            var npc = Adult(0);
            npc.Insight = 20;
            Ready(npc);
            var me = Adult(0);
            me.Insight = 20;
            Ready(me);
            w.PlayerCharacterId = me.Id;
            w.Month = 12 * 101;
            new CultivationPhase().Run(Ctx(8));
            Assert.That(npc.Realm == 1 || npc.Enlightenment < 100, "AI는 시도했다");
            Assert.That(me.Realm, Is.Zero, "플레이어는 스스로 고른다");
        }
    }
}
