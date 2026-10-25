using System.Linq;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Simulation;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class LifePhaseTests
    {
        GameData d;
        WorldState w;
        TurnContext ctx;

        [SetUp]
        public void Setup()
        {
            d = TestWorlds.MinimalData();
            w = TestWorlds.Empty(d);
            var rng = new GameRandom(3);
            ctx = new TurnContext { World = w, Data = d, Rng = rng, Generator = new CharacterGenerator(d, rng) };
        }

        [Test]
        public void DeathChance_RisesWithAge_RealmShiftsBy8Years()
        {
            Assert.That(DeathPhase.MonthlyDeathChance(70, 0, 1), Is.GreaterThan(DeathPhase.MonthlyDeathChance(40, 0, 1)));
            Assert.That(DeathPhase.MonthlyDeathChance(68, 1, 1), Is.EqualTo(DeathPhase.MonthlyDeathChance(60, 0, 1)).Within(1e-12));
            Assert.That(DeathPhase.MonthlyDeathChance(200, 0, 1), Is.AtMost(1.0));
        }

        [Test]
        public void SicklyTrait_DoublesRisk()
        {
            double baseRisk = DeathPhase.MonthlyDeathChance(50, 0, 1) - SimConstants.MonthlyAccidentChance;
            double sick = DeathPhase.MonthlyDeathChance(50, 0, 2) - SimConstants.MonthlyAccidentChance;
            Assert.That(sick, Is.EqualTo(baseRisk * 2).Within(1e-12));
            Assert.That(DeathPhase.TraitMultiplier(d, new[] { "sickly", "bold" }), Is.EqualTo(2f));
        }

        [Test]
        public void MeanLifespan_Realm0_About60()
        {
            var rng = new GameRandom(42);
            double total = 0;
            int n = 2000;
            for (int i = 0; i < n; i++)
            {
                int months = 0;
                while (!rng.Chance(DeathPhase.MonthlyDeathChance(months / 12, 0, 1))) months++;
                total += months / 12.0;
            }
            Assert.That(total / n, Is.InRange(55, 66));
        }

        [Test]
        public void Kill_InheritsMemoriesAndRecords()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var dad = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 60);
            var son = TestWorlds.AddCharacter(w, "x", Role.Family, Sex.Male, 30);
            dad.ChildIds.Add(son.Id); son.FatherId = dad.Id;
            Opinion.AddMemory(dad, MemoryKind.Grudge, "enemy", "살부지수", w.Month, 80);
            DeathPhase.Kill(w, dad, "노환");
            Assert.That(dad.IsAlive, Is.False);
            Assert.That(dad.DeathCause, Is.EqualTo("노환"));
            Assert.That(son.Memories.Single().Strength, Is.EqualTo(40));
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("Died"));
        }

        [Test]
        public void Birth_OnlyMarriedFertileWomen()
        {
            var old = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 50);
            var oldHusband = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 50);
            ctx.Generator.Marry(w, old, oldHusband);
            TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 30);
            var phase = new BirthPhase();
            for (int i = 0; i < 1000; i++) phase.Run(ctx);
            Assert.That(w.Characters.Count, Is.EqualTo(3));
        }

        [Test]
        public void Birth_MarriedCouple_HasChildrenAtAboutSpecRate()
        {
            var wife = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 20);
            var husband = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 22);
            ctx.Generator.Marry(w, wife, husband);
            var phase = new BirthPhase();
            for (int i = 0; i < 1000; i++) phase.Run(ctx);
            Assert.That(wife.ChildIds.Count, Is.InRange(7, 25));
        }

        [Test]
        public void BirthChance_FallsWhenFactionOvercrowded()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            f.TargetNamedMembers = 4;
            var wife = TestWorlds.AddCharacter(w, "x", Role.Family, Sex.Female, 20);
            var husband = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 22);
            double small = BirthPhase.BirthChance(w, wife);
            for (int i = 0; i < 20; i++) TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 20);
            double crowded = BirthPhase.BirthChance(w, wife);
            Assert.That(crowded, Is.LessThan(small / 2));
            Assert.That(crowded, Is.GreaterThan(0));
            var loner = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 20);
            Assert.That(BirthPhase.BirthChance(w, loner), Is.EqualTo(SimConstants.MonthlyBirthChance));
        }

        [Test]
        public void Yearly_DecaysMemories_OnlyInJanuary()
        {
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            Opinion.AddMemory(a, MemoryKind.Grudge, "x", "모욕", w.Month, 40);
            w.Month = 12 * 100 + 3;
            new YearlyPhase().Run(ctx);
            Assert.That(a.Memories[0].Strength, Is.EqualTo(40));
            w.Month = 12 * 101;
            new YearlyPhase().Run(ctx);
            Assert.That(a.Memories[0].Strength, Is.EqualTo(36));
        }

        [Test]
        public void Yearly_ChivalrousLeader_RaisesFactionChivalry3()
        {
            d.Traits["chivalrous"] = new TraitDef { Id = "chivalrous", YearlyChivalry = 3 };
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 50, "chivalrous");
            new YearlyPhase().Run(ctx);
            Assert.That(f.Chivalry, Is.EqualTo(53));
        }

        [Test]
        public void Yearly_UnaffiliatedTraitChivalry_GoesToPersonal()
        {
            d.Traits["greedy"] = new TraitDef { Id = "greedy", YearlyChivalry = -3 };
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30, "greedy");
            new YearlyPhase().Run(ctx);
            Assert.That(a.Chivalry, Is.EqualTo(-3));
        }

        [Test]
        public void Yearly_DebtInterest10Percent()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 50);
            f.Debt = 800;
            new YearlyPhase().Run(ctx);
            Assert.That(f.Debt, Is.EqualTo(880));
        }

        [Test]
        public void Yearly_HugeDebt_FamilyResentsLeader()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 50);
            var kin = TestWorlds.AddCharacter(w, "x", Role.Family, Sex.Male, 20);
            f.Debt = 3001;
            new YearlyPhase().Run(ctx);
            Assert.That(w.Opinions.GetStored(kin.Id, leader.Id), Is.EqualTo(-10));
        }

        [Test]
        public void Yearly_GuestWithHighOpinion_BecomesElder()
        {
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 50);
            var guest = TestWorlds.AddCharacter(w, "x", Role.Guest, Sex.Male, 30);
            var guest2 = TestWorlds.AddCharacter(w, "x", Role.Guest, Sex.Male, 30);
            w.Opinions.Add(leader.Id, guest.Id, 60);
            new YearlyPhase().Run(ctx);
            Assert.That(guest.Role, Is.EqualTo(Role.Elder));
            Assert.That(guest2.Role, Is.EqualTo(Role.Guest));
        }

        [Test]
        public void Yearly_FameDriftsTowardInfluence()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 30);
            TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 50);
            f.Fame = 20;
            new YearlyPhase().Run(ctx);
            Assert.That(f.Fame, Is.EqualTo(21));
        }

        [Test]
        public void Yearly_RealmGrowth_OnlyForYoungAdults()
        {
            var young = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 20);
            var old = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 70);
            young.Constitution = 20; young.Insight = 20;
            old.Constitution = 20; old.Insight = 20;
            for (int i = 0; i < 100; i++) new YearlyPhase().Run(ctx);
            Assert.That(young.Realm, Is.GreaterThan(0));
            Assert.That(old.Realm, Is.EqualTo(0));
        }

        [Test]
        public void Alignment_RighteousBelowMinus30_BecomesUnorthodox_DemonicNeverShifts()
        {
            var r = TestWorlds.AddFaction(w, "r", Alignment.Righteous, "a", 10);
            var u = TestWorlds.AddFaction(w, "u", Alignment.Unorthodox, "a", 10);
            var m = TestWorlds.AddFaction(w, "m", Alignment.Demonic, "a", 10);
            r.Chivalry = -31; u.Chivalry = 31; m.Chivalry = 100;
            new AlignmentPhase().Run(ctx);
            Assert.That(r.Alignment, Is.EqualTo(Alignment.Unorthodox));
            Assert.That(u.Alignment, Is.EqualTo(Alignment.Righteous));
            Assert.That(m.Alignment, Is.EqualTo(Alignment.Demonic));
            Assert.That(w.Chronicle.Count(e => e.Kind == "AlignmentShifted"), Is.EqualTo(2));
        }

        [Test]
        public void Alignment_BetweenThresholds_Unchanged()
        {
            var r = TestWorlds.AddFaction(w, "r", Alignment.Righteous, "a", 10);
            r.Chivalry = -20;
            new AlignmentPhase().Run(ctx);
            Assert.That(r.Alignment, Is.EqualTo(Alignment.Righteous));
        }

        [Test]
        public void Income_InfluenceTimes2PlusUnnamed()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 6);
            w.Regions["b"].Influence["x"] = 4;
            TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 50);
            f.UnnamedDisciples = 15;
            new IncomePhase().Run(ctx);
            Assert.That(f.Silver, Is.EqualTo(1035));
        }

        [Test]
        public void Income_GuestStipendPaidFromFaction()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 0);
            TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 50);
            var guest = TestWorlds.AddCharacter(w, "x", Role.Guest, Sex.Male, 30);
            new IncomePhase().Run(ctx);
            Assert.That(guest.Silver, Is.EqualTo(20));
            Assert.That(f.Silver, Is.EqualTo(980));
            f.Silver = 10;
            new IncomePhase().Run(ctx);
            Assert.That(guest.Silver, Is.EqualTo(20));
        }
    }
}
