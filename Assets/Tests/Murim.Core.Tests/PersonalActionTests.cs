using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class PersonalActionTests
    {
        GameData d;
        WorldState w;
        GameRandom rng;
        Character hero;
        Faction sect;
        Character sectLeader;

        [SetUp]
        public void Setup()
        {
            d = TestWorlds.MinimalData();
            w = TestWorlds.Empty(d);
            rng = new GameRandom(7);
            hero = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 25);
            hero.LocationRegionId = "a";
            hero.Silver = 100;
            sect = TestWorlds.AddFaction(w, "sect", Alignment.Righteous, "a", 20);
            sectLeader = TestWorlds.AddCharacter(w, "sect", Role.Leader, Sex.Male, 50);
        }

        void Run(IGameAction a)
        {
            var check = a.CanExecute(w, d);
            Assert.That(check.Ok, Is.True, check.Reason);
            a.Execute(w, d, rng);
        }

        Character Follower(int opinion)
        {
            var f = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 20);
            f.LocationRegionId = hero.LocationRegionId;
            w.Opinions.Add(f.Id, hero.Id, opinion);
            return f;
        }

        [Test]
        public void Travel_OnlyAdjacent_MovesLocation()
        {
            Assert.That(new Travel(hero.Id, "c").CanExecute(w, d).Ok, Is.False);
            Run(new Travel(hero.Id, "b"));
            Assert.That(hero.LocationRegionId, Is.EqualTo("b"));
            Assert.That(new Travel(hero.Id, "c").CanExecute(w, d).Ok, Is.True);
        }

        [Test]
        public void FactionLeader_CannotUsePersonalActions()
        {
            Assert.That(PersonalRules.UsesPersonalActions(w, sectLeader), Is.False);
            Assert.That(new Travel(sectLeader.Id, "b").CanExecute(w, d).Ok, Is.False);
            Assert.That(PersonalRules.UsesPersonalActions(w, hero), Is.True);
        }

        [Test]
        public void SparScore_FollowsSpecFormula()
        {
            var a = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 25);
            var b = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 25);
            a.Realm = 2; b.Realm = 1; b.Constitution = 12; b.Insight = 12;
            Assert.That(PersonalRules.SparScore(a, b, -20), Is.EqualTo(1));
        }

        [Test]
        public void Spar_WinRaisesFame_LossLowersFameNotBelowZero()
        {
            var weak = TestWorlds.AddCharacter(w, "sect", Role.Disciple, Sex.Male, 20);
            hero.Realm = 3;
            Run(new Spar(hero.Id, weak.Id));
            Assert.That(hero.Fame, Is.InRange(2, 5));
            Assert.That(w.Opinions.GetStored(weak.Id, hero.Id), Is.EqualTo(10));

            hero.Fame = 0; hero.Realm = 0;
            var strong = TestWorlds.AddCharacter(w, "sect", Role.Elder, Sex.Male, 50);
            strong.Realm = 4;
            Run(new Spar(hero.Id, strong.Id));
            Assert.That(hero.Fame, Is.EqualTo(0));
            Assert.That(w.Opinions.GetStored(strong.Id, hero.Id), Is.EqualTo(-10));
        }

        [Test]
        public void Spar_TargetElsewhere_CannotExecute()
        {
            var far = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 20);
            far.LocationRegionId = "c";
            Assert.That(new Spar(hero.Id, far.Id).CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void ContractQuest_PaysOneAndHalf_LowersChivalry4()
        {
            Run(new PersonalQuest(hero.Id, true));
            Assert.That(hero.Silver - 100, Is.InRange(30, 90));
            Assert.That(hero.Chivalry, Is.EqualTo(-4));
            Assert.That(hero.Fame, Is.EqualTo(0));
        }

        [Test]
        public void ChivalrousQuest_RaisesChivalry3AndFame1()
        {
            Run(new PersonalQuest(hero.Id, false));
            Assert.That(hero.Silver - 100, Is.InRange(20, 60));
            Assert.That(hero.Chivalry, Is.EqualTo(3));
            Assert.That(hero.Fame, Is.EqualTo(1));
        }

        [Test]
        public void Escort_PaysAndBlocksNextMonth()
        {
            Run(new Escort(hero.Id, "b"));
            Assert.That(hero.Silver, Is.EqualTo(180));
            Assert.That(hero.LocationRegionId, Is.EqualTo("b"));
            Assert.That(hero.BusyUntilMonth, Is.EqualTo(w.Month + 2));
            Assert.That(new Escort(hero.Id, "a"), Is.InstanceOf<IWholeMonthAction>());
            Assert.That(new Travel(hero.Id, "a"), Is.InstanceOf<IWholeMonthAction>());
        }

        [Test]
        public void Befriend_ScalesWithCharismaCappedAt20()
        {
            var t = TestWorlds.AddCharacter(w, "sect", Role.Disciple, Sex.Male, 20);
            hero.Charisma = 4;
            Run(new Befriend(hero.Id, t.Id));
            Assert.That(w.Opinions.GetStored(t.Id, hero.Id), Is.EqualTo(12));
            Assert.That(hero.Silver, Is.EqualTo(90));
            hero.Charisma = 20;
            Run(new Befriend(hero.Id, t.Id));
            Assert.That(w.Opinions.GetStored(t.Id, hero.Id), Is.EqualTo(32));
        }

        [Test]
        public void JoinAsGuest_AcceptsByOpinionOrFame()
        {
            Run(new JoinAsGuest(hero.Id, "sect"));
            Assert.That(hero.FactionId, Is.Null);
            hero.Fame = 20;
            Run(new JoinAsGuest(hero.Id, "sect"));
            Assert.That(hero.FactionId, Is.EqualTo("sect"));
            Assert.That(hero.Role, Is.EqualTo(Role.Guest));
            Assert.That(PersonalRules.UsesPersonalActions(w, hero), Is.True);
        }

        [Test]
        public void JoinAsGuest_ByOpinion()
        {
            w.Opinions.Add(sectLeader.Id, hero.Id, 20);
            Run(new JoinAsGuest(hero.Id, "sect"));
            Assert.That(hero.Role, Is.EqualTo(Role.Guest));
        }

        [Test]
        public void JoinAsGuest_FactionElsewhere_CannotExecute()
        {
            hero.LocationRegionId = "b";
            Assert.That(new JoinAsGuest(hero.Id, "sect").CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void FoundSect_ConditionsMet_CreatesFactionWithFollowers()
        {
            hero.Silver = 1200; hero.Fame = 30; hero.Chivalry = 40;
            var f1 = Follower(40);
            var f2 = Follower(45);
            Follower(10);
            Run(new FoundSect(hero.Id, "청운검문"));
            var f = w.GetFaction(hero.FactionId);
            Assert.That(f.Name, Is.EqualTo("청운검문"));
            Assert.That(f.LeaderId, Is.EqualTo(hero.Id));
            Assert.That(hero.Role, Is.EqualTo(Role.Leader));
            Assert.That(f.Alignment, Is.EqualTo(Alignment.Righteous));
            Assert.That(f.Chivalry, Is.EqualTo(40));
            Assert.That(f.Silver, Is.EqualTo(200));
            Assert.That(f.Fame, Is.EqualTo(30));
            Assert.That(f.HomeRegionId, Is.EqualTo("a"));
            Assert.That(f.MemberIds, Is.EquivalentTo(new[] { hero.Id, f1.Id, f2.Id }));
            Assert.That(f1.Role, Is.EqualTo(Role.Disciple));
            Assert.That(Influence.Get(w, "a", f.Id), Is.EqualTo(3));
            Assert.That(hero.Silver, Is.EqualTo(0));
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("SectFounded"));
        }

        [Test]
        public void FoundSect_MissingFollowers_CannotExecute()
        {
            hero.Silver = 1200; hero.Fame = 30;
            Follower(40);
            Assert.That(new FoundSect(hero.Id, "청운검문").CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void FoundSect_NotEnoughFameOrSilver_CannotExecute()
        {
            Follower(50); Follower(50);
            hero.Silver = 1200; hero.Fame = 29;
            Assert.That(new FoundSect(hero.Id, "가").CanExecute(w, d).Ok, Is.False);
            hero.Silver = 999; hero.Fame = 30;
            Assert.That(new FoundSect(hero.Id, "가").CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void FoundSect_FullRegion_KeepsSumAtMost100()
        {
            w.Regions["a"].Influence["sect"] = 100;
            hero.Silver = 1000; hero.Fame = 30;
            Follower(50); Follower(50);
            Run(new FoundSect(hero.Id, "흑풍채"));
            Assert.That(w.Regions["a"].Influence.Values.Sum(), Is.EqualTo(100));
            Assert.That(Influence.Get(w, "a", hero.FactionId), Is.EqualTo(3));
        }

        [Test]
        public void FoundSect_AlignmentFromChivalry()
        {
            hero.Silver = 1000; hero.Fame = 30; hero.Chivalry = -1;
            Follower(50); Follower(50);
            Run(new FoundSect(hero.Id, "흑풍채"));
            Assert.That(w.GetFaction(hero.FactionId).Alignment, Is.EqualTo(Alignment.Unorthodox));
        }

        [Test]
        public void FoundSect_EmptyName_CannotExecute()
        {
            hero.Silver = 1000; hero.Fame = 30;
            Follower(50); Follower(50);
            Assert.That(new FoundSect(hero.Id, "  ").CanExecute(w, d).Ok, Is.False);
        }

        [Test]
        public void LeaveFaction_MakesUnaffiliatedAndAngersLeader()
        {
            hero.Fame = 20;
            Run(new JoinAsGuest(hero.Id, "sect"));
            Run(new LeaveFaction(hero.Id));
            Assert.That(hero.FactionId, Is.Null);
            Assert.That(hero.Role, Is.EqualTo(Role.None));
            Assert.That(w.Opinions.GetStored(sectLeader.Id, hero.Id), Is.EqualTo(-20));
            Assert.That(new LeaveFaction(hero.Id).CanExecute(w, d).Ok, Is.False);
        }
    }
}
