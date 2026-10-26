using System.Collections.Generic;
using Murim.Core.Simulation;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class TurnProcessorTests
    {
        class Recorder : ITurnPhase
        {
            readonly List<string> log;
            readonly string name;
            public Recorder(List<string> log, string name) { this.log = log; this.name = name; }
            public void Run(TurnContext ctx) => log.Add(name + ctx.World.Month);
        }

        [Test]
        public void Advance_IncrementsMonth_RunsPhasesInOrder()
        {
            var log = new List<string>();
            var p = new TurnProcessor(new Recorder(log, "a"), new Recorder(log, "b"), new Recorder(log, "c"));
            var ctx = new TurnContext { World = new WorldState { Month = 5 } };
            p.Advance(ctx);
            Assert.That(log, Is.EqualTo(new[] { "a5", "b5", "c5" }));
            Assert.That(ctx.World.Month, Is.EqualTo(6));
        }

        [Test]
        public void Advance_GameOver_DoesNothing()
        {
            var log = new List<string>();
            var p = new TurnProcessor(new Recorder(log, "a"));
            var ctx = new TurnContext { World = new WorldState { Month = 5, GameOver = true } };
            p.Advance(ctx);
            Assert.That(log, Is.Empty);
            Assert.That(ctx.World.Month, Is.EqualTo(5));
            ctx.IgnoreGameOver = true;
            p.Advance(ctx);
            Assert.That(ctx.World.Month, Is.EqualTo(6));
        }

        [Test]
        public void CreateDefault_PutsActionPhasesFirst()
        {
            var log = new List<string>();
            var p = TurnProcessor.CreateDefault(new ITurnPhase[] { new Recorder(log, "act") });
            Assert.That(p.Phases[0], Is.InstanceOf<Recorder>());
            Assert.That(p.Phases[1], Is.InstanceOf<Murim.Core.Events.EventPhase>());
            Assert.That(p.Phases[2], Is.InstanceOf<Murim.Core.Martial.CultivationPhase>());
            Assert.That(p.Phases[3], Is.InstanceOf<Murim.Core.Manor.ManorPhase>());
            Assert.That(p.Phases[4], Is.InstanceOf<BirthPhase>());
            Assert.That(p.Phases[p.Phases.Count - 1], Is.InstanceOf<IncomePhase>());
        }
    }
}
