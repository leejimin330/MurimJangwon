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
    public class SuccessionTests
    {
        GameData d;
        WorldState w;
        TurnContext ctx;

        [SetUp]
        public void Setup()
        {
            d = TestWorlds.MinimalData();
            w = TestWorlds.Empty(d);
            var rng = new GameRandom(5);
            ctx = new TurnContext { World = w, Data = d, Rng = rng, Generator = new CharacterGenerator(d, rng) };
        }

        static void Parent(Character parent, Character child)
        {
            parent.ChildIds.Add(child.Id);
            if (parent.Sex == Sex.Male) child.FatherId = parent.Id; else child.MotherId = parent.Id;
        }

        [Test]
        public void Succession_DesignatedHeirFirst()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 70);
            var heir = TestWorlds.AddCharacter(w, "x", Role.Heir, Sex.Male, 20);
            var star = TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 30);
            star.Realm = 5;
            DeathPhase.Kill(w, leader, "노환");
            new SuccessionPhase().Run(ctx);
            Assert.That(f.LeaderId, Is.EqualTo(heir.Id));
            Assert.That(heir.Role, Is.EqualTo(Role.Leader));
            Assert.That(f.HeirId, Is.Null);
            Assert.That(w.Chronicle.Last(e => e.Kind != "Deserted").Kind, Is.EqualTo("Succeeded"));
        }

        [Test]
        public void Succession_SectPicksBestScore()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 70);
            var a = TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 30);
            var b = TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 30);
            var guest = TestWorlds.AddCharacter(w, "x", Role.Guest, Sex.Male, 30);
            // a: 2×3 + 10 + 10 = 26
            a.Realm = 2; b.Realm = 1; b.Cunning = 12;
            guest.Realm = 6;
            Assert.That(SuccessionPhase.PickSuccessor(w, f, leader).Id, Is.EqualTo(a.Id));
            b.Cunning = 13;
            Assert.That(SuccessionPhase.PickSuccessor(w, f, leader).Id, Is.EqualTo(a.Id), "동률이면 id 사전순");
            b.Cunning = 14;
            Assert.That(SuccessionPhase.PickSuccessor(w, f, leader).Id, Is.EqualTo(b.Id));
        }

        [Test]
        public void Succession_ClanPicksEldestChild()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10, FactionType.Clan);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 70);
            var young = TestWorlds.AddCharacter(w, "x", Role.Family, Sex.Male, 20);
            var eldest = TestWorlds.AddCharacter(w, "x", Role.Family, Sex.Female, 35);
            var star = TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 30);
            star.Realm = 6;
            Parent(leader, young); Parent(leader, eldest);
            Assert.That(SuccessionPhase.PickSuccessor(w, f, leader).Id, Is.EqualTo(eldest.Id));
        }

        [Test]
        public void Succession_AmbitiousHater_MayDesert()
        {
            int deserted = 0, n = 1000;
            for (int i = 0; i < n; i++)
            {
                var ww = TestWorlds.Empty(d);
                var rng = new GameRandom((ulong)i);
                var c = new TurnContext { World = ww, Data = d, Rng = rng, Generator = new CharacterGenerator(d, rng) };
                var f = TestWorlds.AddFaction(ww, "x", Alignment.Righteous, "a", 10);
                var leader = TestWorlds.AddCharacter(ww, "x", Role.Leader, Sex.Male, 70);
                var heir = TestWorlds.AddCharacter(ww, "x", Role.Heir, Sex.Male, 30);
                var rebel = TestWorlds.AddCharacter(ww, "x", Role.Disciple, Sex.Male, 30, "ambitious");
                ww.Opinions.Add(rebel.Id, heir.Id, -50);
                DeathPhase.Kill(ww, leader, "노환");
                new SuccessionPhase().Run(c);
                if (rebel.FactionId == null) deserted++;
            }
            Assert.That(deserted / (double)n, Is.InRange(0.20, 0.30));
        }

        [Test]
        public void Succession_HaterWithoutAmbition_Stays()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 70);
            var heir = TestWorlds.AddCharacter(w, "x", Role.Heir, Sex.Male, 30);
            var grump = TestWorlds.AddCharacter(w, "x", Role.Disciple, Sex.Male, 30);
            w.Opinions.Add(grump.Id, heir.Id, -100);
            DeathPhase.Kill(w, leader, "노환");
            new SuccessionPhase().Run(ctx);
            Assert.That(grump.FactionId, Is.EqualTo("x"));
        }

        [Test]
        public void Succession_NoCandidate_DissolvesFaction()
        {
            var f = TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 70);
            var guest = TestWorlds.AddCharacter(w, "x", Role.Guest, Sex.Male, 30);
            DeathPhase.Kill(w, leader, "노환");
            new SuccessionPhase().Run(ctx);
            Assert.That(f.IsDissolved, Is.True);
            Assert.That(guest.FactionId, Is.Null);
            Assert.That(Influence.Total(w, "x"), Is.EqualTo(0));
            Assert.That(w.Chronicle.Last().Kind, Is.EqualTo("Dissolved"));
        }

        [Test]
        public void PlayerDeath_AsLeader_HeirBecomesPlayer()
        {
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 70);
            var heir = TestWorlds.AddCharacter(w, "x", Role.Heir, Sex.Male, 30);
            w.PlayerCharacterId = leader.Id;
            DeathPhase.Kill(w, leader, "노환");
            new SuccessionPhase().Run(ctx);
            Assert.That(w.PlayerCharacterId, Is.EqualTo(heir.Id));
            Assert.That(w.GameOver, Is.False);
        }

        [Test]
        public void PlayerDeath_MinorChild_BecomesPlayer()
        {
            var hero = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40);
            var kid = TestWorlds.AddCharacter(w, null, Role.None, Sex.Female, 10);
            Parent(hero, kid);
            hero.Silver = 300;
            w.PlayerCharacterId = hero.Id;
            DeathPhase.Kill(w, hero, "사고");
            new SuccessionPhase().Run(ctx);
            Assert.That(w.PlayerCharacterId, Is.EqualTo(kid.Id));
            Assert.That(w.GameOver, Is.False);
            Assert.That(kid.Silver, Is.EqualTo(300));
        }

        [Test]
        public void PlayerDeath_FactionDissolved_ChildElsewhereContinues()
        {
            TestWorlds.AddFaction(w, "x", Alignment.Righteous, "a", 10);
            TestWorlds.AddFaction(w, "y", Alignment.Righteous, "b", 10);
            var leader = TestWorlds.AddCharacter(w, "x", Role.Leader, Sex.Male, 70);
            var daughter = TestWorlds.AddCharacter(w, "y", Role.Family, Sex.Female, 30);
            Parent(leader, daughter);
            w.PlayerCharacterId = leader.Id;
            DeathPhase.Kill(w, leader, "노환");
            new SuccessionPhase().Run(ctx);
            Assert.That(w.Factions["x"].IsDissolved, Is.True);
            Assert.That(w.PlayerCharacterId, Is.EqualTo(daughter.Id));
        }

        [Test]
        public void PlayerDeath_NoOneLeft_GameOver()
        {
            var hero = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 40);
            w.PlayerCharacterId = hero.Id;
            DeathPhase.Kill(w, hero, "사고");
            new SuccessionPhase().Run(ctx);
            Assert.That(w.GameOver, Is.True);
        }
    }
}
