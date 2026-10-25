using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class MarriageRulesTests
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
        public void Score_FollowsSpecFormula()
        {
            var p = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40);
            var t = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40);
            w.Opinions.Add(t.Id, p.Id, 10);
            int score = MarriageRules.AcceptanceScore(w, d, p, t, 50, 20, Alignment.Righteous, Alignment.Righteous, true);
            Assert.That(score, Is.EqualTo(10 + 3 + 20 + 15));
        }

        [Test]
        public void Score_RighteousVsUnorthodox_Minus40()
        {
            var p = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40);
            var t = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40);
            Assert.That(MarriageRules.AcceptanceScore(w, d, p, t, 0, 0, Alignment.Unorthodox, Alignment.Righteous, false), Is.EqualTo(-40));
            Assert.That(MarriageRules.AcceptanceScore(w, d, p, t, 0, 0, Alignment.Demonic, Alignment.Righteous, false), Is.EqualTo(-40));
            Assert.That(MarriageRules.AcceptanceScore(w, d, p, t, 0, 0, Alignment.Unorthodox, Alignment.Demonic, false), Is.EqualTo(0));
            Assert.That(MarriageRules.AcceptanceScore(w, d, p, t, 0, 0, null, Alignment.Righteous, false), Is.EqualTo(0));
        }

        [Test]
        public void Score_IncludesTargetHeadTraitWeight()
        {
            d.Traits["bold"].ActionWeights["accept_marriage"] = 5;
            var p = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40);
            var t = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40, "bold");
            Assert.That(MarriageRules.AcceptanceScore(w, d, p, t, 0, 0, null, null, false), Is.EqualTo(5));
        }

        [Test]
        public void Eligible_RejectsMinorMarriedSameSexAndSiblings()
        {
            var m = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 20);
            var f = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 20);
            Assert.That(MarriageRules.Eligible(w, m, f).Ok, Is.True);

            var kid = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 15);
            Assert.That(MarriageRules.Eligible(w, m, kid).Ok, Is.False);

            var m2 = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 20);
            Assert.That(MarriageRules.Eligible(w, m, m2).Ok, Is.False);

            var sib = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 22);
            var parent = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 50);
            m.FatherId = parent.Id; sib.FatherId = parent.Id;
            Assert.That(MarriageRules.Eligible(w, m, sib).Ok, Is.False);

            var daughter = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 20);
            daughter.FatherId = parent.Id;
            Assert.That(MarriageRules.Eligible(w, parent, daughter).Ok, Is.False);

            var wife = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 20);
            m.SpouseId = wife.Id; wife.SpouseId = m.Id;
            Assert.That(MarriageRules.Eligible(w, m, f).Ok, Is.False);
        }

        [Test]
        public void DeadSpouse_AllowsRemarriage()
        {
            var m = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var wife = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 30);
            var f = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 25);
            m.SpouseId = wife.Id; wife.SpouseId = m.Id;
            wife.DeathMonth = w.Month;
            Assert.That(MarriageRules.Eligible(w, m, f).Ok, Is.True);
        }
    }
}
