using System.Collections.Generic;
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
    public class EventPhaseTests
    {
        GameData d;
        WorldState w;
        Character player;
        Character npc;

        [SetUp]
        public void Setup()
        {
            d = EventFixtures.Data();
            d.Storytellers["balanced"].BaseChance = 0.0001; // 이야기꾼은 끈다 (0은 검증에서 막힘)
            w = TestWorlds.Empty(d);
            player = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            npc = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            w.PlayerCharacterId = player.Id;
        }

        void Run(ulong seed = 5) =>
            new EventPhase().Run(new TurnContext { World = w, Data = d, Rng = new GameRandom(seed), Generator = new CharacterGenerator(d, new GameRandom(seed)) });

        [Test]
        public void WorldEvents_ExcludePlayerAsDecider_AndNotice()
        {
            var e = EventFixtures.Blank(d);
            e.CooldownMonths = 0;
            Run();
            // 결정자는 언제나 npc, 상대는 플레이어 → 소식으로 들어온다.
            var lines = w.Chronicle.Where(c => c.Kind == "Event:blank").ToList();
            Assert.That(lines.Count, Is.EqualTo(EventPhase.WorldAttemptsPerMonth));
            Assert.That(w.Events.Pending.All(p => p.IsNotice), Is.True);
            Assert.That(w.Events.Pending.Count, Is.EqualTo(EventPhase.MaxNoticesPerMonth));
            Assert.That(w.Events.Pending[0].Text, Is.EqualTo(lines[0].Text));
        }

        [Test]
        public void NoNotice_WhenImportanceZero()
        {
            var e = EventFixtures.Blank(d);
            e.Importance = 0;
            Run();
            Assert.That(w.Events.Pending, Is.Empty);
            Assert.That(w.Chronicle.Count(c => c.Kind == "Event:blank"), Is.EqualTo(1), "결정자 쿨다운 때문에 한 번만");
        }

        [Test]
        public void FollowupCountsTowardThree()
        {
            EventFixtures.Blank(d);
            for (int i = 0; i < 5; i++)
                w.Events.Scheduled.Add(new ScheduledEvent
                {
                    EventId = "blank", Month = w.Month,
                    Roles = new SortedDictionary<string, string> { ["a"] = player.Id, ["b"] = npc.Id },
                });
            Run();
            Assert.That(w.Events.Pending.Count(p => !p.IsNotice), Is.EqualTo(EventPhase.MaxPlayerEventsPerMonth));
            Assert.That(w.Events.Scheduled, Is.Empty);
        }

        [Test]
        public void Scheduled_DeadRole_Dropped()
        {
            EventFixtures.Blank(d);
            npc.DeathMonth = w.Month - 1;
            w.Events.Scheduled.Add(new ScheduledEvent
            {
                EventId = "blank", Month = w.Month, Roles = new SortedDictionary<string, string> { ["a"] = player.Id, ["b"] = npc.Id },
            });
            Assert.DoesNotThrow(() => Run());
            Assert.That(w.Events.Pending, Is.Empty);
            Assert.That(w.Events.Scheduled, Is.Empty);
        }

        [Test]
        public void Scheduled_FutureMonth_Waits()
        {
            EventFixtures.Blank(d);
            w.Events.Scheduled.Add(new ScheduledEvent
            {
                EventId = "blank", Month = w.Month + 1, Roles = new SortedDictionary<string, string> { ["a"] = player.Id, ["b"] = npc.Id },
            });
            Run();
            Assert.That(w.Events.Scheduled.Count, Is.EqualTo(1));
        }
    }
}
