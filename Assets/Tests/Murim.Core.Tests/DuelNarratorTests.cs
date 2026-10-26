using System.Linq;
using Murim.Core.Data;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class DuelNarratorTests
    {
        static GameData D => TestPaths.LoadShippedData();
        WorldState w;
        static readonly string[] Names = { "무기", "설", "청", "운학", "태산", "혜" };
        int next;

        [SetUp]
        public void Setup() => w = TestWorlds.Empty(D);

        Character Fighter(int realm, string art, int mastery)
        {
            var c = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            c.GivenName = Names[next++ % Names.Length];
            c.Realm = realm;
            c.Arts.Add(new KnownArt { ArtId = "hunyuan_gong", Mastery = mastery });
            c.Arts.Add(new KnownArt { ArtId = art, Mastery = mastery });
            c.Arts.Add(new KnownArt { ArtId = "grass_flight", Mastery = mastery });
            c.Qi = 30 * 12;
            return c;
        }

        [Test]
        public void Lines_AtMost14_NoDuplicates()
        {
            var a = Fighter(2, "taiji_sword", 8);
            var b = Fighter(2, "plum_sword", 8);
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var r = Duel.Fight(w, D, new GameRandom(seed), a, b);
                Assert.That(r.Lines.Count, Is.InRange(2, DuelNarrator.MaxLines), seed.ToString());
                // 문구 수보다 합이 많으면 다시 쓸 수 있으므로, 처음 7줄만 중복 검사
                Assert.That(r.Lines.Take(7).Distinct().Count(), Is.EqualTo(r.Lines.Take(7).Count()), string.Join("\n", r.Lines));
            }
        }

        [Test]
        public void DecisiveRoundsKept()
        {
            var a = Fighter(3, "taiji_sword", 8);
            var b = Fighter(2, "plum_sword", 8);
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var r = Duel.Fight(w, D, new GameRandom(seed), a, b);
                var biggest = r.Log.Where(l => l.Damage > 0 && l.Event != RoundEvent.Clash).OrderByDescending(l => l.Damage).FirstOrDefault();
                if (biggest == null || biggest.Card == null) continue;
                Assert.That(r.Lines.Any(l => l.Contains("'" + biggest.Card.Name + "'")), string.Join("\n", r.Lines));
            }
        }

        [Test]
        public void Death_LastLineIsDeath()
        {
            var a = Fighter(4, "taiji_sword", 6);
            var b = Fighter(0, "plum_sword", 2);
            var r = Duel.Fight(w, D, new GameRandom(4), a, b, new DuelOptions { DeathChance = 1 });
            Assert.That(r.LoserDied);
            var deaths = D.Narration[NarrationKeys.Death].Select(t => DuelText.Format(t,
                new System.Collections.Generic.Dictionary<string, string> { ["loser"] = r.Loser.FullName, ["winner"] = r.Winner.FullName }));
            Assert.That(deaths, Does.Contain(r.Lines.Last()));
        }

        [Test]
        public void NoRawTokensOrJosaMarks()
        {
            var a = Fighter(2, "dragon_palms", 9);
            var b = Fighter(2, "yiyang_finger", 9);
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var r = Duel.Fight(w, D, new GameRandom(seed), a, b);
                foreach (var line in r.Lines.Append(r.Summary))
                {
                    Assert.That(line, Does.Not.Contain("{"));
                    foreach (var mark in new[] { "(를)", "(가)", "(는)", "(와)", "(으)로", "'였다" }) Assert.That(line, Does.Not.Contain(mark));
                    Assert.That(line, Does.Not.Contain("상대의 상대"));
                }
            }
        }

        [Test]
        public void Summary_UsesFinisherName()
        {
            var a = Fighter(5, "taiji_sword", 10);
            var b = Fighter(1, "plum_sword", 3);
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var r = Duel.Fight(w, D, new GameRandom(seed), a, b);
                var last = r.Log.LastOrDefault(l => l.Damage > 0);
                if (r.TimedOut || last == null || !last.IsFinisher || last.ActorId != r.Winner.Id) continue;
                Assert.That(r.Summary, Does.Contain("절초 '" + last.Card.Name + "'"));
                return;
            }
            Assert.Fail("절초로 끝난 결투가 없다");
        }
    }
}
