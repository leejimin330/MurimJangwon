using System.Linq;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.Simulation;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class StorytellerTests
    {
        GameData d;
        WorldState w;
        StorytellerDef def;
        Character player;

        [SetUp]
        public void Setup()
        {
            d = EventFixtures.Data();
            w = TestWorlds.Empty(d);
            def = d.Storytellers["balanced"];
            player = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            w.PlayerCharacterId = player.Id;
            TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
        }

        TurnContext Turn(ulong seed = 3) =>
            new TurnContext { World = w, Data = d, Rng = new GameRandom(seed), Generator = new CharacterGenerator(d, new GameRandom(seed)) };

        [Test]
        public void Tick_CapsAtMax()
        {
            w.Events.Tension = 95;
            Storyteller.Tick(w.Events, def);
            Assert.That(w.Events.Tension, Is.EqualTo(100));
        }

        [Test]
        public void Crisis_RespectsGap()
        {
            var e = EventFixtures.Blank(d);
            e.Tension = 25;
            w.Events.Tension = 100;
            w.Events.LastCrisisMonth = w.Month - 9;
            Assert.That(Storyteller.IsCandidate(e, w.Events, def, w.Month), Is.False);
            w.Events.LastCrisisMonth = w.Month - 10;
            Assert.That(Storyteller.IsCandidate(e, w.Events, def, w.Month), Is.True);
        }

        [Test]
        public void TensionAboveCurrent_NotCandidate()
        {
            var e = EventFixtures.Blank(d);
            e.Tension = 15;
            w.Events.Tension = 14;
            Assert.That(Storyteller.IsCandidate(e, w.Events, def, w.Month), Is.False);
            e.FollowupOnly = true;
            w.Events.Tension = 100;
            Assert.That(Storyteller.IsCandidate(e, w.Events, def, w.Month), Is.False);
        }

        [Test]
        public void Weight_CategoryMultiplier()
        {
            var e = EventFixtures.Blank(d);
            e.Weight = 10;
            def.CategoryMultipliers["trait"] = 1.5f;
            Assert.That(Storyteller.Weight(e, def), Is.EqualTo(15f));
        }

        [Test]
        public void MaxTwoStorytellerEventsPerMonth()
        {
            EventFixtures.Blank(d, "x");
            EventFixtures.Blank(d, "y");
            EventFixtures.Blank(d, "z");
            w.Events.Tension = 100;
            var picked = Storyteller.PickForPlayer(Turn(), CandidateIndex.Build(w), 0);
            Assert.That(picked.Count, Is.EqualTo(2));
            Assert.That(picked.Select(c => c.Def.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(picked.All(c => c.Roles["a"] == player.Id), Is.True);

            w.Events.Tension = 100;
            Assert.That(Storyteller.PickForPlayer(Turn(), CandidateIndex.Build(w), 2).Count, Is.EqualTo(1));
            Assert.That(Storyteller.PickForPlayer(Turn(), CandidateIndex.Build(w), 3).Count, Is.EqualTo(0));
        }

        [Test]
        public void SecondEvent_OnlyWhenTensionHigh()
        {
            EventFixtures.Blank(d, "x").Tension = 10;
            EventFixtures.Blank(d, "y").Tension = 10;
            w.Events.Tension = 50; // +10 → 60, 첫 사건 뒤 50 < 70
            Assert.That(Storyteller.PickForPlayer(Turn(), CandidateIndex.Build(w), 0).Count, Is.EqualTo(1));
            Assert.That(w.Events.Tension, Is.EqualTo(50));
        }

        [Test]
        public void Crisis_RecordsMonth()
        {
            EventFixtures.Blank(d, "x").Tension = 30;
            w.Events.Tension = 90;
            Storyteller.PickForPlayer(Turn(), CandidateIndex.Build(w), 0);
            Assert.That(w.Events.LastCrisisMonth, Is.EqualTo(w.Month));
        }
    }
}
