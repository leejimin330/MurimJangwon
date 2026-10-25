using System.Collections.Generic;
using Murim.Core.Random;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class GameRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new GameRandom(42);
            var b = new GameRandom(42);
            for (int i = 0; i < 1000; i++) Assert.That(a.Next(0, 1000), Is.EqualTo(b.Next(0, 1000)));
        }

        [Test]
        public void RestoredState_ContinuesSameSequence()
        {
            var a = new GameRandom(7);
            a.Next(0, 10);
            var saved = a.State;
            var expected = a.Next(0, 1_000_000);
            var b = new GameRandom(1) { State = saved };
            Assert.That(b.Next(0, 1_000_000), Is.EqualTo(expected));
        }

        [Test]
        public void Next_StaysInRange()
        {
            var r = new GameRandom(3);
            for (int i = 0; i < 10000; i++) Assert.That(r.Next(-5, 5), Is.InRange(-5, 4));
        }

        [Test]
        public void Chance_ZeroNeverOneAlways()
        {
            var r = new GameRandom(9);
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(r.Chance(0), Is.False);
                Assert.That(r.Chance(1), Is.True);
            }
        }

        [Test]
        public void SeedZero_StillProducesVariedValues()
        {
            var r = new GameRandom(0);
            var set = new HashSet<int>();
            for (int i = 0; i < 100; i++) set.Add(r.Next(0, 1000));
            Assert.That(set.Count, Is.GreaterThan(50));
        }
    }
}
