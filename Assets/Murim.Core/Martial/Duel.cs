using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.World;

namespace Murim.Core.Martial
{
    public class DuelOptions
    {
        public int MaxRounds = 6;
        /// <summary>진 쪽이 죽을 확률. 엔진은 판정만 하고 죽이지는 않는다.</summary>
        public double DeathChance;
        /// <summary>테스트용: 시작 내력을 덮어쓴다 (null이면 최대치).</summary>
        public int? StartForceA;
        public int? StartForceB;
    }

    public enum RoundEvent { Hit, Graze, Block, Evade, Clash, Probe, Counter, Exhausted }

    /// <summary>한 합 안의 한 갈래 (누가 누구에게 무엇을 했나).</summary>
    public class DuelRound
    {
        public int Number;
        public string ActorId;
        public string TargetId;
        public CardDef Card;
        public ArtDef Art;
        public CardDef Guard;
        public RoundEvent Event;
        public int Damage;
        public bool IsFinisher => Card != null && Card.Kind == CardKind.Finisher;
    }

    public class DuelResult
    {
        public Character Winner;
        public Character Loser;
        public int Rounds;
        public bool TimedOut;
        public bool LoserDied;
        public List<DuelRound> Log = new List<DuelRound>();
        public List<string> Lines = new List<string>();
        public string Summary;
    }

    /// <summary>초식 카드 자동 결투 (설계 5절). 수치 조정은 여기 상수만 고친다.</summary>
    public static class Duel
    {
        // 설계의 1.35·35·±15%는 경지 하나 차이로 98%를 이겨 목표(80%)보다 강했다 (설계 12절).
        public const double RealmBase = 1.1;
        public const int HpPerRealm = 20;
        public const int ForcePerRealm = 15;
        public const double StyleWin = 1.3;
        public const double StyleLose = 0.8;
        public const double ClashScale = 0.7;
        public const double RollMin = 0.65;
        public const double RollMax = 1.35;
        public const double BlockFloor = 0.3;
        public const int ProbeForce = 5;
        public const int RestForce = 10;

        public static readonly ArtDef BasicFists = new ArtDef
        {
            Id = "basic_fists", Name = "주먹질", Type = ArtType.Fist, Grade = 1, Alignment = Alignment.Righteous,
            Cards = new List<CardDef>
            {
                new CardDef { Id = "basic_fists_1", Name = "주먹질", Kind = CardKind.Attack, Style = CardStyle.Fast, Power = 8, Cost = 0 },
                new CardDef { Id = "basic_fists_2", Name = "팔로 막기", Kind = CardKind.Defense, Style = CardStyle.Hard, Power = 8, Cost = 0 },
                new CardDef { Id = "basic_fists_3", Name = "어깨치기", Kind = CardKind.Attack, Style = CardStyle.Hard, Power = 10, Cost = 2 },
            },
        };

        public static int InnerGrade(GameData d, Character c) => ArtGranting.Inner(d, c)?.Grade ?? 0;

        public static int MaxHp(GameData d, Character c) => 60 + c.Realm * HpPerRealm + c.Constitution * 3 + InnerGrade(d, c) * 10;

        public static int MaxForce(GameData d, Character c) =>
            20 + c.Realm * ForcePerRealm + Math.Min(Cultivation.QiYears(c), c.Realm * 40 + 40) / 2;

        /// <summary>주무공을 앞에, 다른 외공·신법 카드를 뒤에. 성취가 모자란 카드는 뺀다. 외공이 없으면 주먹질.</summary>
        public static List<(CardDef card, ArtDef art, int mastery)> Deck(GameData d, Character c)
        {
            var main = ArtGranting.MainArt(d, c);
            var deck = new List<(CardDef, ArtDef, int)>();
            foreach (var (known, def) in ArtGranting.Externals(d, c).OrderBy(x => main != null && x.def.Id == main.Id ? 0 : 1)
                         .ThenBy(x => x.def.Id, StringComparer.Ordinal))
                foreach (var card in def.Cards)
                    if (known.Mastery >= card.MasteryMin) deck.Add((card, def, known.Mastery));
            if (deck.Count == 0)
                foreach (var card in BasicFists.Cards) deck.Add((card, BasicFists, 1));
            return deck;
        }

        public static double StyleMult(CardStyle attack, CardStyle? guard)
        {
            if (guard == null || attack == guard) return 1.0;
            if (Beats(attack, guard.Value)) return StyleWin;
            return StyleLose;
        }

        public static bool Beats(CardStyle a, CardStyle b) =>
            (a == CardStyle.Fast && b == CardStyle.Hard) || (a == CardStyle.Hard && b == CardStyle.Soft) || (a == CardStyle.Soft && b == CardStyle.Fast);

        class Fighter
        {
            public Character C;
            public int Hp, MaxHp, Force, MovementMastery;
            public double Modifier;
            public List<(CardDef card, ArtDef art, int mastery)> Deck;
            public double HpRatio => MaxHp <= 0 ? 0 : Hp / (double)MaxHp;
        }

        static Fighter Make(WorldState w, GameData d, Character c, int? startForce)
        {
            int mod = 0;
            foreach (var kv in c.Flags)
                if (c.HasFlag(kv.Key, w.Month) && d.Flags.TryGetValue(kv.Key, out var fd)) mod += fd.DuelModifier;
            int hp = MaxHp(d, c);
            var movement = ArtGranting.Externals(d, c).Where(x => x.def.IsMovement).Select(x => x.known.Mastery).DefaultIfEmpty(0).Max();
            return new Fighter
            {
                C = c, Hp = hp, MaxHp = hp, Force = startForce ?? MaxForce(d, c), MovementMastery = movement,
                Modifier = 1 + mod / 100.0, Deck = Deck(d, c),
            };
        }

        public static DuelResult Fight(WorldState w, GameData d, GameRandom rng, Character a, Character b, DuelOptions o = null)
        {
            o ??= new DuelOptions();
            var fa = Make(w, d, a, o.StartForceA);
            var fb = Make(w, d, b, o.StartForceB);
            var r = new DuelResult();
            int round = 0;
            while (round < o.MaxRounds && fa.Hp > 0 && fb.Hp > 0)
            {
                round++;
                var ca = Choose(fa, fb, rng);
                var cb = Choose(fb, fa, rng);
                PlayRound(r, round, fa, ca, fb, cb, rng);
            }
            r.Rounds = round;
            bool aWins;
            if (fa.Hp > 0 && fb.Hp > 0)
            {
                r.TimedOut = true;
                aWins = fa.HpRatio != fb.HpRatio ? fa.HpRatio > fb.HpRatio : a.Realm >= b.Realm;
            }
            else if (fa.Hp <= 0 && fb.Hp <= 0) aWins = fa.Hp >= fb.Hp;
            else aWins = fb.Hp <= 0;
            r.Winner = aWins ? a : b;
            r.Loser = aWins ? b : a;
            r.Winner.DuelWinsThisYear++;
            r.LoserDied = o.DeathChance > 0 && rng.Chance(o.DeathChance);
            DuelNarrator.Narrate(d, rng, r, a, b);
            return r;
        }

        /// <summary>AI 카드 선택: 위력 × 상성 기대값 × 상황 × 성격, 가중 무작위. 낼 카드가 없으면 null.</summary>
        static (CardDef card, ArtDef art, int mastery)? Choose(Fighter me, Fighter foe, GameRandom rng)
        {
            var affordable = me.Deck.Where(x => x.card.Cost <= me.Force).ToList();
            if (affordable.Count == 0) return null;
            bool sly = me.C.HasTrait("sly"), hot = me.C.HasTrait("hot_tempered"), coward = me.C.HasTrait("coward");
            var weights = new List<float>();
            foreach (var (card, _, _) in affordable)
            {
                double expect = foe.Deck.Average(x => StyleMult(card.Style, x.card.Style));
                if (sly) expect *= expect;
                double wgt = card.Power * expect;
                if (card.Kind == CardKind.Finisher) wgt *= foe.HpRatio <= 0.3 ? 3 : 0.6;
                if (card.Kind == CardKind.Defense || card.Kind == CardKind.Evade) wgt *= me.HpRatio <= 0.35 ? 2 : 0.8;
                if (hot && card.IsOffense) wgt *= 1.4;
                if (coward && card.Kind == CardKind.Evade) wgt *= 1.6;
                weights.Add((float)Math.Max(0.01, wgt));
            }
            int i = PickIndex(weights, rng);
            return affordable[i];
        }

        static int PickIndex(List<float> weights, GameRandom rng)
        {
            double total = weights.Sum(x => (double)x);
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return weights.Count - 1;
        }

        static double AttackValue(Fighter atk, CardDef card, int mastery, Fighter def, CardStyle? guardStyle, GameRandom rng) =>
            card.Power * (1 + mastery / 10.0) * Math.Pow(RealmBase, atk.C.Realm - def.C.Realm) * StyleMult(card.Style, guardStyle)
            * (RollMin + rng.NextDouble() * (RollMax - RollMin)) * atk.Modifier;

        static void PlayRound(DuelResult r, int n, Fighter fa, (CardDef card, ArtDef art, int mastery)? ca,
            Fighter fb, (CardDef card, ArtDef art, int mastery)? cb, GameRandom rng)
        {
            if (ca != null) fa.Force -= ca.Value.card.Cost;
            if (cb != null) fb.Force -= cb.Value.card.Cost;
            bool aOff = ca != null && ca.Value.card.IsOffense;
            bool bOff = cb != null && cb.Value.card.IsOffense;

            if (ca == null) Rest(r, n, fa, fb);
            if (cb == null) Rest(r, n, fb, fa);

            if (aOff && bOff)
            {
                var x = ca.Value;
                var y = cb.Value;
                bool clash = x.card.Style == y.card.Style;
                double scale = clash ? ClashScale : 1;
                int toB = (int)Math.Round(AttackValue(fa, x.card, x.mastery, fb, y.card.Style, rng) * scale);
                int toA = (int)Math.Round(AttackValue(fb, y.card, y.mastery, fa, x.card.Style, rng) * scale);
                fb.Hp -= toB;
                fa.Hp -= toA;
                if (clash)
                {
                    r.Log.Add(new DuelRound { Number = n, ActorId = fa.C.Id, TargetId = fb.C.Id, Card = x.card, Art = x.art, Guard = y.card, Event = RoundEvent.Clash, Damage = toB + toA });
                    return;
                }
                r.Log.Add(new DuelRound { Number = n, ActorId = fa.C.Id, TargetId = fb.C.Id, Card = x.card, Art = x.art, Event = RoundEvent.Hit, Damage = toB });
                r.Log.Add(new DuelRound { Number = n, ActorId = fb.C.Id, TargetId = fa.C.Id, Card = y.card, Art = y.art, Event = RoundEvent.Hit, Damage = toA });
                return;
            }
            if (aOff) Strike(r, n, fa, ca.Value, fb, cb, rng);
            if (bOff) Strike(r, n, fb, cb.Value, fa, ca, rng);
            if (!aOff && !bOff && ca != null && cb != null)
            {
                fa.Force += ProbeForce;
                fb.Force += ProbeForce;
                r.Log.Add(new DuelRound { Number = n, ActorId = fa.C.Id, TargetId = fb.C.Id, Card = ca.Value.card, Art = ca.Value.art, Guard = cb.Value.card, Event = RoundEvent.Probe });
            }
        }

        static void Rest(DuelResult r, int n, Fighter tired, Fighter foe)
        {
            tired.Force += RestForce;
            r.Log.Add(new DuelRound { Number = n, ActorId = tired.C.Id, TargetId = foe.C.Id, Event = RoundEvent.Exhausted });
        }

        /// <summary>공격하는 쪽만 공격 카드를 냈을 때. 상대 카드는 방어·회피이거나 없음(내력 고갈).</summary>
        static void Strike(DuelResult r, int n, Fighter atk, (CardDef card, ArtDef art, int mastery) x, Fighter def,
            (CardDef card, ArtDef art, int mastery)? guard, GameRandom rng)
        {
            var g = guard?.card;
            double raw = AttackValue(atk, x.card, x.mastery, def, g?.Style, rng);
            var entry = new DuelRound { Number = n, ActorId = atk.C.Id, TargetId = def.C.Id, Card = x.card, Art = x.art, Guard = g };
            if (g == null)
            {
                entry.Damage = (int)Math.Round(raw);
                entry.Event = RoundEvent.Hit;
            }
            else if (g.Kind == CardKind.Defense)
            {
                double block = g.Power * (1 + guard.Value.mastery / 10.0);
                bool countered = Beats(x.card.Style, g.Style);
                if (countered) block *= 0.5;
                int dmg = (int)Math.Round(Math.Max(0, raw - block));
                entry.Damage = dmg;
                entry.Event = countered && dmg > 0 ? RoundEvent.Counter : dmg <= raw * BlockFloor ? RoundEvent.Block : RoundEvent.Hit;
            }
            else
            {
                double p = 0.25 + 0.03 * def.MovementMastery - 0.1 * Math.Max(0, atk.C.Realm - def.C.Realm);
                p = Math.Max(0.05, Math.Min(0.70, p));
                if (rng.Chance(p))
                {
                    entry.Damage = 0;
                    entry.Event = RoundEvent.Evade;
                }
                else
                {
                    entry.Damage = (int)Math.Round(raw / 2);
                    entry.Event = RoundEvent.Graze;
                }
            }
            def.Hp -= entry.Damage;
            r.Log.Add(entry);
        }
    }
}
