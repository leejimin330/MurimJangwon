using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.Simulation;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class FactionAITests
    {
        GameData d;
        WorldState w;

        [SetUp]
        public void Setup()
        {
            d = TestWorlds.MinimalData();
            d.Traits["greedy"] = new TraitDef { Id = "greedy" };
            d.Traits["greedy"].ActionWeights["plunder"] = 15;
            d.Traits["chivalrous"] = new TraitDef { Id = "chivalrous" };
            d.Traits["chivalrous"].ActionWeights["insult_unorthodox"] = 15;
            w = TestWorlds.Empty(d);
        }

        (Faction f, Character leader) Staffed(string id, Alignment a, string region, params string[] traits)
        {
            var f = TestWorlds.AddFaction(w, id, a, region, 10);
            var leader = TestWorlds.AddCharacter(w, id, Role.Leader, Sex.Male, 45, traits);
            leader.Cunning = 5;
            TestWorlds.AddCharacter(w, id, Role.Heir, Sex.Male, 25);
            f.TargetNamedMembers = f.MemberIds.Count;
            return (f, leader);
        }

        int CountKey(Faction f, string key, int trials)
        {
            int n = 0;
            for (int i = 0; i < trials; i++)
                n += FactionAI.Choose(w, d, new GameRandom((ulong)i), f).Count(a => a.Key == key);
            return n;
        }

        [Test]
        public void Choose_NeverReturnsUnexecutableAction()
        {
            var (f, _) = Staffed("x", Alignment.Unorthodox, "a");
            Staffed("y", Alignment.Unorthodox, "a");
            f.Silver = 0;
            f.Fame = 0;
            for (int i = 0; i < 100; i++)
                foreach (var a in FactionAI.Choose(w, d, new GameRandom((ulong)i), f))
                    Assert.That(a.CanExecute(w, d).Ok, Is.True, a.Key);
        }

        [Test]
        public void Choose_GreedyUnorthodoxLeader_PrefersPlunder()
        {
            var (greedy, _) = Staffed("g", Alignment.Unorthodox, "a", "greedy");
            var (plain, _) = Staffed("p", Alignment.Unorthodox, "b");
            Assert.That(CountKey(greedy, ActionKeys.Plunder, 200), Is.GreaterThan(CountKey(plain, ActionKeys.Plunder, 200)));
        }

        [Test]
        public void Choose_RighteousNeverPlunders()
        {
            var (f, _) = Staffed("r", Alignment.Righteous, "a", "greedy");
            Assert.That(CountKey(f, ActionKeys.Plunder, 200), Is.EqualTo(0));
        }

        [Test]
        public void Choose_ChivalrousRighteous_InsultsLocalUnorthodoxMoreThanNeutral()
        {
            var (hero, _) = Staffed("h", Alignment.Righteous, "a", "chivalrous");
            Staffed("bad", Alignment.Unorthodox, "a");
            var (plain, _) = Staffed("p", Alignment.Righteous, "a");
            Assert.That(CountKey(hero, ActionKeys.Insult, 200), Is.GreaterThan(CountKey(plain, ActionKeys.Insult, 200)));
        }

        [Test]
        public void Choose_NeverProposesToPlayerFaction()
        {
            var (ai, aiLeader) = Staffed("ai", Alignment.Righteous, "a");
            var (player, playerLeader) = Staffed("pl", Alignment.Righteous, "b");
            w.PlayerCharacterId = playerLeader.Id;
            TestWorlds.AddCharacter(w, "ai", Role.Disciple, Sex.Male, 20);
            TestWorlds.AddCharacter(w, "pl", Role.Disciple, Sex.Female, 20);
            w.Opinions.Add(playerLeader.Id, aiLeader.Id, 100);
            for (int i = 0; i < 200; i++)
                foreach (var a in FactionAI.Choose(w, d, new GameRandom((ulong)i), ai).OfType<ProposeMarriage>())
                    Assert.That(w.Get(a.TheirMemberId).FactionId, Is.Not.EqualTo("pl"));
        }

        [Test]
        public void Choose_ProposesMarriageToFriendlyFaction()
        {
            var (ai, aiLeader) = Staffed("ai", Alignment.Righteous, "a");
            var (other, otherLeader) = Staffed("ot", Alignment.Righteous, "b");
            TestWorlds.AddCharacter(w, "ai", Role.Disciple, Sex.Male, 20);
            TestWorlds.AddCharacter(w, "ot", Role.Disciple, Sex.Female, 20);
            ai.TargetNamedMembers = ai.MemberIds.Count;
            w.Opinions.Add(otherLeader.Id, aiLeader.Id, 50);
            Assert.That(CountKey(ai, ActionKeys.Marriage, 100), Is.GreaterThan(0));
        }

        [Test]
        public void Choose_UnderstaffedFaction_Recruits()
        {
            var (f, _) = Staffed("x", Alignment.Righteous, "a");
            f.TargetNamedMembers = 10;
            Assert.That(CountKey(f, ActionKeys.RecruitNamed, 50), Is.EqualTo(50));
        }

        [Test]
        public void Choose_OldLeaderWithoutHeir_DesignatesHeir()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 60);
            leader.Cunning = 5;
            TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 25);
            f.TargetNamedMembers = f.MemberIds.Count;
            f.Silver = 0;
            Assert.That(CountKey(f, ActionKeys.DesignateHeir, 20), Is.EqualTo(20));
        }

        [Test]
        public void Choose_CunningLeader_UpToTwoActions()
        {
            var (f, leader) = Staffed("x", Alignment.Righteous, "a");
            f.Silver = 5000;
            leader.Cunning = 11;
            for (int i = 0; i < 20; i++) Assert.That(FactionAI.Choose(w, d, new GameRandom((ulong)i), f).Count, Is.EqualTo(1));
            leader.Cunning = 12;
            for (int i = 0; i < 20; i++) Assert.That(FactionAI.Choose(w, d, new GameRandom((ulong)i), f).Count, Is.EqualTo(2));
        }

        [Test]
        public void AiPhase_SkipsPlayerLedFaction()
        {
            var (f, leader) = Staffed("x", Alignment.Righteous, "a");
            f.TargetNamedMembers = 10;
            w.PlayerCharacterId = leader.Id;
            int before = f.MemberIds.Count;
            var rng = new GameRandom(1);
            new AiPhase().Run(new TurnContext { World = w, Data = d, Rng = rng, Generator = new CharacterGenerator(d, rng) });
            Assert.That(f.MemberIds.Count, Is.EqualTo(before));
        }

        [Test]
        public void Unaffiliated_JoinMatchingAlignmentFaction()
        {
            Staffed("good", Alignment.Righteous, "a");
            Staffed("bad", Alignment.Unorthodox, "a");
            var saint = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            var thug = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            saint.LocationRegionId = "a"; saint.Chivalry = 10;
            thug.LocationRegionId = "a"; thug.Chivalry = -10;
            var rng = new GameRandom(2);
            var ctx = new TurnContext { World = w, Data = d, Rng = rng, Generator = new CharacterGenerator(d, rng) };
            for (int i = 0; i < 40 && (saint.FactionId == null || thug.FactionId == null); i++)
            {
                w.Month = 12 * (200 + i);
                new UnaffiliatedPhase().Run(ctx);
            }
            Assert.That(saint.FactionId, Is.EqualTo("good"));
            Assert.That(thug.FactionId, Is.EqualTo("bad"));
            Assert.That(saint.Role, Is.EqualTo(Role.Disciple));
        }

        [Test]
        public void Unaffiliated_NeverJoinsPlayerFaction()
        {
            var (f, leader) = Staffed("pl", Alignment.Righteous, "a");
            w.PlayerCharacterId = leader.Id;
            var drifter = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            drifter.LocationRegionId = "a";
            var rng = new GameRandom(3);
            var ctx = new TurnContext { World = w, Data = d, Rng = rng, Generator = new CharacterGenerator(d, rng) };
            for (int i = 0; i < 40; i++)
            {
                w.Month = 12 * (200 + i);
                new UnaffiliatedPhase().Run(ctx);
            }
            Assert.That(drifter.FactionId, Is.Not.EqualTo("pl"));
        }
    }
}
