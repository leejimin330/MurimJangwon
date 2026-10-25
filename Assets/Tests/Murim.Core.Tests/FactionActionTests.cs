using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class FactionActionTests
    {
        GameData d;
        WorldState w;
        GameRandom rng;
        Faction us, them;
        Character ourLeader, theirLeader, ourSon, theirDaughter;

        [SetUp]
        public void Setup()
        {
            d = TestWorlds.MinimalData();
            w = TestWorlds.Empty(d);
            rng = new GameRandom(1);
            us = TestWorlds.AddFaction(w, "us", Alignment.Righteous, "a", 10);
            them = TestWorlds.AddFaction(w, "them", Alignment.Righteous, "c", 10);
            ourLeader = TestWorlds.AddCharacter(w, "us", Role.Leader, Sex.Male, 50);
            theirLeader = TestWorlds.AddCharacter(w, "them", Role.Leader, Sex.Male, 50);
            ourSon = TestWorlds.AddCharacter(w, "us", Role.Family, Sex.Male, 20);
            theirDaughter = TestWorlds.AddCharacter(w, "them", Role.Family, Sex.Female, 19);
        }

        void Run(IGameAction a)
        {
            var check = a.CanExecute(w, d);
            Assert.That(check.Ok, Is.True, check.Reason);
            a.Execute(w, d, rng);
        }

        [Test]
        public void ProposeMarriage_Accepted_MarriesMovesSpouseRaisesOpinionAndAlliance()
        {
            w.Opinions.Add(theirLeader.Id, ourLeader.Id, 30);
            Run(new ProposeMarriage(ourLeader.Id, ourSon.Id, theirDaughter.Id, true));
            Assert.That(ourSon.SpouseId, Is.EqualTo(theirDaughter.Id));
            Assert.That(theirDaughter.FactionId, Is.EqualTo("us"));
            Assert.That(theirDaughter.Role, Is.EqualTo(Role.Family));
            Assert.That(them.MemberIds, Does.Not.Contain(theirDaughter.Id));
            Assert.That(w.Opinions.GetStored(theirLeader.Id, ourLeader.Id), Is.EqualTo(50));
            Assert.That(w.Opinions.GetStored(ourLeader.Id, theirLeader.Id), Is.EqualTo(20));
            Assert.That(w.AllianceScore("us", "them"), Is.EqualTo(15));
            Assert.That(us.Silver, Is.EqualTo(950));
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("Married"));
        }

        [Test]
        public void ProposeMarriage_SpouseJoinsThem_MovesOurMember()
        {
            w.Opinions.Add(theirLeader.Id, ourLeader.Id, 30);
            Run(new ProposeMarriage(ourLeader.Id, ourSon.Id, theirDaughter.Id, false));
            Assert.That(ourSon.FactionId, Is.EqualTo("them"));
        }

        [Test]
        public void ProposeMarriage_Rejected_AddsGrudge30ToProposer()
        {
            w.Opinions.Add(theirLeader.Id, ourLeader.Id, -50);
            Run(new ProposeMarriage(ourLeader.Id, ourSon.Id, theirDaughter.Id, true));
            Assert.That(ourSon.SpouseId, Is.Null);
            var m = ourLeader.Memories.Single();
            Assert.That(m.Kind, Is.EqualTo(MemoryKind.Grudge));
            Assert.That(m.TargetId, Is.EqualTo(theirLeader.Id));
            Assert.That(m.Strength, Is.EqualTo(30));
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("MarriageRejected"));
            Assert.That(us.Silver, Is.EqualTo(950));
        }

        [Test]
        public void ProposeMarriage_CostsSilver50_FailsWhenPoor()
        {
            us.Silver = 49;
            Assert.That(new ProposeMarriage(ourLeader.Id, ourSon.Id, theirDaughter.Id, true).CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void ProposeMarriage_LeaderCannotLeaveOwnFaction()
        {
            var widow = TestWorlds.AddCharacter(w, "them", Role.Family, Sex.Female, 40);
            Assert.That(new ProposeMarriage(ourLeader.Id, ourLeader.Id, widow.Id, false).CanExecute(w, d).Ok, Is.False);
            Assert.That(new ProposeMarriage(ourLeader.Id, ourLeader.Id, widow.Id, true).CanExecute(w, d).Ok, Is.True);
        }

        [Test]
        public void ProposeMarriage_SameFaction_NotAllowed()
        {
            var girl = TestWorlds.AddCharacter(w, "us", Role.Disciple, Sex.Female, 20);
            Assert.That(new ProposeMarriage(ourLeader.Id, ourSon.Id, girl.Id, true).CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void InternalMarriage_MarriesTwoMembers()
        {
            var girl = TestWorlds.AddCharacter(w, "us", Role.Disciple, Sex.Female, 20);
            Run(new ArrangeInternalMarriage(ourLeader.Id, ourSon.Id, girl.Id));
            Assert.That(girl.SpouseId, Is.EqualTo(ourSon.Id));
            Assert.That(new ArrangeInternalMarriage(ourLeader.Id, ourSon.Id, theirDaughter.Id).CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void Alliance_RequiresScore30()
        {
            w.AddAllianceScore("us", "them", 29);
            Run(new ProposeAlliance(ourLeader.Id, "them"));
            Assert.That(w.AreAllied("us", "them"), Is.False);
            Assert.That(us.Fame, Is.EqualTo(15));
            w.AddAllianceScore("us", "them", 1);
            Run(new ProposeAlliance(ourLeader.Id, "them"));
            Assert.That(w.AreAllied("us", "them"), Is.True);
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("Allied"));
            Assert.That(new ProposeAlliance(ourLeader.Id, "them").CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void Gift_RaisesOpinionBySilverOver10_AndAddsDebtMemory()
        {
            Run(new SendGift(ourLeader.Id, "them", 100));
            Assert.That(us.Silver, Is.EqualTo(900));
            Assert.That(w.Opinions.GetStored(theirLeader.Id, ourLeader.Id), Is.EqualTo(10));
            var m = theirLeader.Memories.Single();
            Assert.That(m.Kind, Is.EqualTo(MemoryKind.Debt));
            Assert.That(m.Strength, Is.EqualTo(20));
            Assert.That(w.AllianceScore("us", "them"), Is.EqualTo(5));
            Assert.That(new SendGift(ourLeader.Id, "them", 20).CanExecute(w, d).Ok, Is.False);
            Assert.That(new SendGift(ourLeader.Id, "them", 201).CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void Insult_Minus30AndGrudge40_UnorthodoxGainsFame2()
        {
            Run(new Insult(ourLeader.Id, "them"));
            Assert.That(w.Opinions.GetStored(theirLeader.Id, ourLeader.Id), Is.EqualTo(-30));
            Assert.That(theirLeader.Memories.Single().Strength, Is.EqualTo(40));
            Assert.That(w.AllianceScore("us", "them"), Is.EqualTo(-20));
            Assert.That(us.Fame, Is.EqualTo(20));
            us.Alignment = Alignment.Unorthodox;
            w.Month += Insult.CooldownMonths;
            Run(new Insult(ourLeader.Id, "them"));
            Assert.That(us.Fame, Is.EqualTo(22));
        }

        [Test]
        public void Insult_Twice_Within24Months_Fails()
        {
            Run(new Insult(ourLeader.Id, "them"));
            w.Month += Insult.CooldownMonths - 1;
            var check = new Insult(ourLeader.Id, "them").CanExecute(w, d);
            Assert.That(check.Ok, Is.False);
            Assert.That(check.Reason, Is.EqualTo("2년 안에 다시 모욕할 수 없습니다"));
            Assert.That(new Insult(theirLeader.Id, "us").CanExecute(w, d).Ok, Is.True, "반대 방향은 따로 센다");
        }

        [Test]
        public void Insult_After24Months_Ok()
        {
            Run(new Insult(ourLeader.Id, "them"));
            w.Month += Insult.CooldownMonths;
            Assert.That(new Insult(ourLeader.Id, "them").CanExecute(w, d).Ok, Is.True);
        }

        [Test]
        public void RecruitNamed_CreatesDiscipleOrHiresLocalUnaffiliated()
        {
            int before = us.MemberIds.Count;
            Run(new RecruitNamed(ourLeader.Id));
            Assert.That(us.MemberIds.Count, Is.EqualTo(before + 1));
            var recruit = w.Get(us.MemberIds.Last());
            Assert.That(recruit.Role, Is.EqualTo(Role.Disciple));
            Assert.That(recruit.AgeYears(w.Month), Is.InRange(14, 25));
            Assert.That(recruit.HasRelation(ourLeader.Id, RelationTag.Master), Is.True);
            Assert.That(us.Silver, Is.EqualTo(980));

            var drifter = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            drifter.LocationRegionId = "a";
            Run(new RecruitNamed(ourLeader.Id));
            Assert.That(drifter.FactionId, Is.EqualTo("us"));
        }

        [Test]
        public void RecruitUnnamed_RespectsInfluenceCap()
        {
            us.UnnamedDisciples = 55;
            Assert.That(new RecruitUnnamed(ourLeader.Id, 5).CanExecute(w, d).Ok, Is.True);
            Assert.That(new RecruitUnnamed(ourLeader.Id, 6).CanExecute(w, d).Ok, Is.False);
            Run(new RecruitUnnamed(ourLeader.Id, 5));
            Assert.That(us.UnnamedDisciples, Is.EqualTo(60));
            Assert.That(us.Silver, Is.EqualTo(975));
        }

        [Test]
        public void Dismiss_MakesUnaffiliatedWithGrudge()
        {
            var d1 = TestWorlds.AddCharacter(w, "us", Role.Disciple, Sex.Male, 20);
            Run(new Dismiss(ourLeader.Id, d1.Id));
            Assert.That(d1.FactionId, Is.Null);
            Assert.That(d1.LocationRegionId, Is.EqualTo("a"));
            Assert.That(d1.Memories.Single().Strength, Is.EqualTo(40));
            Assert.That(us.Fame, Is.EqualTo(17));
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("Dismissed"));
            Assert.That(new Dismiss(ourLeader.Id, ourLeader.Id).CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void DesignateHeir_PassedOverMembersResent()
        {
            var a = TestWorlds.AddCharacter(w, "us", Role.Disciple, Sex.Male, 25);
            var b = TestWorlds.AddCharacter(w, "us", Role.Disciple, Sex.Male, 25);
            Run(new DesignateHeir(ourLeader.Id, a.Id));
            Assert.That(us.HeirId, Is.EqualTo(a.Id));
            Assert.That(a.Role, Is.EqualTo(Role.Heir));
            Assert.That(w.Opinions.GetStored(b.Id, ourLeader.Id), Is.EqualTo(-10));
            Assert.That(b.Memories.Single().Reason, Is.EqualTo("후계 탈락"));
            Assert.That(w.Opinions.GetStored(a.Id, ourLeader.Id), Is.EqualTo(0));

            Run(new DesignateHeir(ourLeader.Id, b.Id));
            Assert.That(a.Role, Is.EqualTo(Role.Disciple));
            Assert.That(us.HeirId, Is.EqualTo(b.Id));
        }

        [Test]
        public void ExpandInfluence_GainScalesWithCunning()
        {
            ourLeader.Cunning = 0;
            Run(new ExpandInfluence(ourLeader.Id, "a"));
            Assert.That(Influence.Get(w, "a", "us"), Is.EqualTo(13));
            ourLeader.Cunning = 20;
            Run(new ExpandInfluence(ourLeader.Id, "a"));
            Assert.That(Influence.Get(w, "a", "us"), Is.EqualTo(21));
            Assert.That(us.Silver, Is.EqualTo(800));
        }

        [Test]
        public void ExpandInfluence_AngersOtherLeadersInRegion()
        {
            w.Regions["b"].Influence["us"] = 1;
            Run(new ExpandInfluence(ourLeader.Id, "c"));
            Assert.That(w.Opinions.GetStored(theirLeader.Id, ourLeader.Id), Is.EqualTo(-5));
        }

        [Test]
        public void Expand_NotAdjacentRegion_CannotExecute()
        {
            var lonely = TestWorlds.AddFaction(w, "lonely", Alignment.Righteous, "a", 0);
            w.Regions["a"].Influence.Remove("lonely");
            var l = TestWorlds.AddCharacter(w, "lonely", Role.Leader, Sex.Male, 40);
            Assert.That(new ExpandInfluence(l.Id, "b").CanExecute(w, d).Ok, Is.True);
            Assert.That(new ExpandInfluence(l.Id, "c").CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void Quest_GivesSilver_RighteousGainsChivalry2()
        {
            Run(new DoQuest(ourLeader.Id, false));
            Assert.That(us.Silver, Is.InRange(1030, 1080));
            Assert.That(us.Chivalry, Is.EqualTo(52));
        }

        [Test]
        public void Plunder_DoublesSilverLowersChivalryAndStability()
        {
            Run(new DoQuest(ourLeader.Id, true));
            Assert.That(us.Silver - 1000, Is.InRange(60, 160));
            Assert.That((us.Silver - 1000) % 2, Is.EqualTo(0));
            Assert.That(us.Chivalry, Is.EqualTo(45));
            Assert.That(w.Regions["a"].Stability, Is.EqualTo(55));
        }

        [Test]
        public void RepayDebt_LimitedByDebtAndSilver()
        {
            us.Debt = 300; us.Silver = 200;
            Assert.That(new RepayDebt(ourLeader.Id, 250).CanExecute(w, d).Ok, Is.False);
            Run(new RepayDebt(ourLeader.Id, 200));
            Assert.That(us.Debt, Is.EqualTo(100));
            Assert.That(us.Silver, Is.EqualTo(0));
        }

        [Test]
        public void ActionBudget_TwoPlusCunningOver8()
        {
            ourLeader.Cunning = 7; Assert.That(ActionBudget.Max(ourLeader), Is.EqualTo(2));
            ourLeader.Cunning = 8; Assert.That(ActionBudget.Max(ourLeader), Is.EqualTo(3));
            ourLeader.Cunning = 20; Assert.That(ActionBudget.Max(ourLeader), Is.EqualTo(4));
        }

        [Test]
        public void NonLeader_CannotUseFactionActions()
        {
            Assert.That(new SendGift(ourSon.Id, "them", 50).CanExecute(w, d).Ok, Is.False);
            Assert.That(new Insult(ourSon.Id, "them").CanExecute(w, d).Ok, Is.False);
            Assert.That(new RecruitNamed(ourSon.Id).CanExecute(w, d).Ok, Is.False);
            Assert.That(new DoQuest(ourSon.Id, false).CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void DeadActor_CannotAct()
        {
            ourLeader.DeathMonth = w.Month;
            Assert.That(new SendGift(ourLeader.Id, "them", 50).CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void TargetDissolved_CannotInsultOrGift()
        {
            them.IsDissolved = true;
            Assert.That(new Insult(ourLeader.Id, "them").CanExecute(w, d).Ok, Is.False);
            Assert.That(new SendGift(ourLeader.Id, "them", 50).CanExecute(w, d).Ok, Is.False);
            Assert.That(new Insult(ourLeader.Id, "us").CanExecute(w, d).Ok, Is.False);
        }
    }
}
