using System.Linq;
using Murim.Core.Data;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.World;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class DuelTests
    {
        static GameData D => TestPaths.LoadShippedData();
        WorldState w;

        [SetUp]
        public void Setup() => w = TestWorlds.Empty(D);

        /// <summary>근골·오성 10, 삼재검법 5성 + 혼원공, 내공은 경지 문턱.</summary>
        Character Fighter(int realm, string sword = "three_talent_sword", int mastery = 5)
        {
            var c = TestWorlds.AddCharacter(w, null, Role.None, Sex.Male, 30);
            c.Realm = realm;
            c.Arts.Add(new KnownArt { ArtId = "hunyuan_gong", Mastery = mastery });
            if (sword != null) c.Arts.Add(new KnownArt { ArtId = sword, Mastery = mastery });
            c.Qi = (realm == 0 ? 0 : Cultivation.ThresholdYears[realm - 1]) * 12;
            return c;
        }

        double WinRate(int realmA, int realmB, int n = 1000)
        {
            var a = Fighter(realmA);
            var b = Fighter(realmB);
            var rng = new GameRandom(1234);
            int wins = 0;
            for (int i = 0; i < n; i++)
                if (Duel.Fight(w, D, rng, a, b).Winner == a) wins++;
            return wins / (double)n;
        }

        [Test]
        public void SameSeed_SameResult()
        {
            var a = Fighter(2);
            var b = Fighter(2, "breeze_sword");
            var x = Duel.Fight(w, D, new GameRandom(5), a, b);
            var y = Duel.Fight(w, D, new GameRandom(5), a, b);
            Assert.That(x.Winner, Is.SameAs(y.Winner));
            Assert.That(x.Lines, Is.EqualTo(y.Lines));
            Assert.That(x.Log.Select(l => l.Damage), Is.EqualTo(y.Log.Select(l => l.Damage)));
        }

        [Test]
        public void WinRate_RealmDiff()
        {
            double same = WinRate(2, 2), one = WinRate(3, 2), two = WinRate(4, 2);
            TestContext.Progress.WriteLine($"승률: 같음 {same:P1}, +1 {one:P1}, +2 {two:P1}");
            Assert.That(same, Is.InRange(0.40, 0.60), "같은 경지");
            Assert.That(one, Is.InRange(0.75, 0.85), "경지 +1");
            Assert.That(two, Is.AtLeast(0.90), "경지 +2");
        }

        [Test]
        public void Exhausted_NoUnaffordableCards()
        {
            var a = Fighter(2);
            var b = Fighter(2);
            var r = Duel.Fight(w, D, new GameRandom(3), a, b, new DuelOptions { StartForceA = 0 });
            Assert.That(r.Log.Any(l => l.ActorId == a.Id && l.Event == RoundEvent.Exhausted));
            var first = r.Log.First(l => l.ActorId == a.Id);
            Assert.That(first.Event == RoundEvent.Exhausted || first.Card.Cost == 0, "첫 합에는 내력 0으로 낼 수 있는 카드만");
            Assert.That(r.Lines.Count, Is.GreaterThan(0));
        }

        [Test]
        public void NoExternalArts_UsesBasicFists()
        {
            var a = Fighter(0, null);
            var deck = Duel.Deck(D, a);
            Assert.That(deck.All(x => x.art.Id == "basic_fists"));
            var r = Duel.Fight(w, D, new GameRandom(1), a, Fighter(0));
            Assert.That(r.Log.Where(l => l.ActorId == a.Id && l.Card != null).All(l => l.Art.Id == "basic_fists"));
        }

        [Test]
        public void Finisher_RequiresMastery()
        {
            Assert.That(Duel.Deck(D, Fighter(2, mastery: 6)).Any(x => x.card.Kind == CardKind.Finisher), Is.False);
            Assert.That(Duel.Deck(D, Fighter(2, mastery: 7)).Any(x => x.card.Kind == CardKind.Finisher), Is.True);
        }

        [Test]
        public void TimedOut_HigherHpRatioWins()
        {
            var rng = new GameRandom(9);
            for (int i = 0; i < 200; i++)
            {
                var a = Fighter(6);
                var b = Fighter(6);
                var r = Duel.Fight(w, D, rng, a, b, new DuelOptions { MaxRounds = 1 });
                if (!r.TimedOut) continue;
                int dmgToWinner = r.Log.Where(l => l.TargetId == r.Winner.Id || (l.Event == RoundEvent.Clash)).Sum(l => l.Event == RoundEvent.Clash ? 0 : l.Damage);
                int dmgToLoser = r.Log.Where(l => l.TargetId == r.Loser.Id && l.Event != RoundEvent.Clash).Sum(l => l.Damage);
                Assert.That(dmgToWinner, Is.LessThanOrEqualTo(dmgToLoser));
                return;
            }
            Assert.Fail("판정으로 끝난 결투가 없다");
        }

        [Test]
        public void Winner_CountsDuelWin()
        {
            var a = Fighter(3);
            var b = Fighter(0);
            var r = Duel.Fight(w, D, new GameRandom(2), a, b);
            Assert.That(r.Winner.DuelWinsThisYear, Is.EqualTo(1));
            Assert.That(r.Loser.DuelWinsThisYear, Is.Zero);
        }
    }
}
