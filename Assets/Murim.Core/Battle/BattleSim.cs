using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Martial;
using Murim.Core.Text;

namespace Murim.Core.Battle
{
    /// <summary>습격전 0.1초 틱 시뮬레이션 (설계 5절). 같은 시드·같은 명령이면 같은 결과.</summary>
    public static class BattleSim
    {
        public const float MeleeRange = 1.3f;
        public const float HiddenRange = 4f;
        public const float TowerRangeBonus = 2f;
        public const double TowerDamage = 1.2;
        public const float AggroRange = 6f;
        public const float ForceRegen = 2f;
        public const double RoutRatio = 0.6;
        public const float RepathSeconds = 0.5f;
        /// <summary>전투는 여럿이 한 사람을 둘러싸므로 1:1 결투(1.1)보다 경지 차이를 크게 본다.</summary>
        public const double RealmBase = 1.2;
        public const double EvadePerRealm = 0.05;

        public static void Order(BattleState s, int unitId, BattleOrder o)
        {
            var u = s.Unit(unitId);
            if (u == null || !u.Active || u.Fleeing) return;
            u.Order = o;
            u.Holding = false;
            u.Path.Clear();
            u.PathGoalX = u.PathGoalY = -1;
            u.TargetId = -1;
        }

        public static void RunToEnd(BattleState s)
        {
            while (!s.Over) Step(s);
        }

        public static void Step(BattleState s)
        {
            if (s.Over) return;
            s.Tick++;
            foreach (var u in s.Units)
            {
                if (!u.Active) continue;
                u.Force = Math.Min(u.MaxForce, u.Force + ForceRegen * BattleState.Dt);
                u.Cooldown -= BattleState.Dt;
                u.RepathTimer -= BattleState.Dt;
                Act(s, u);
            }
            CheckRout(s);
            CheckEnd(s);
        }

        static void Act(BattleState s, BattleUnit u)
        {
            if (u.Fleeing)
            {
                MoveTo(s, u, (int)s.ExitX, (int)s.ExitY);
                if (u.Y >= s.Map.Height - 0.7f && Math.Abs(u.X - s.ExitX) < 2.5f)
                {
                    u.Fled = true;
                    if (u.Named) s.AddLog($"{Josa.EunNeun(u.Name)} 정문 밖으로 달아났다.");
                }
                return;
            }
            if (u.Order != null && !u.Order.Attack)
            {
                MoveTo(s, u, u.Order.X, u.Order.Y);
                if (Dist(u.X, u.Y, u.Order.X + 0.5f, u.Order.Y + 0.5f) < 0.2f)
                {
                    u.Order = null;
                    u.Holding = true;
                }
                return;
            }
            BattleUnit target = null;
            if (u.Order != null && u.Order.Attack)
            {
                target = s.Unit(u.Order.TargetId);
                if (target == null || !target.Active) { u.Order = null; target = null; }
            }
            if (target == null) target = AutoTarget(s, u);
            if (target == null)
            {
                if (u.Side == BattleSide.Attacker) MoveTo(s, u, (int)s.RallyX, (int)s.RallyY);
                return;
            }
            float d = Dist(u.X, u.Y, target.X, target.Y);
            if (d <= Reach(s, u))
            {
                if (u.Cooldown <= 0) Attack(s, u, target, d);
            }
            else if (!u.Holding || (u.Order != null && u.Order.Attack)) MoveTo(s, u, target.CellX, target.CellY);
        }

        /// <summary>자동 교전: 방어자는 6칸 안 가장 가까운 적, 습격자는 거리와 상관없이 가장 가까운 방어자.</summary>
        static BattleUnit AutoTarget(BattleState s, BattleUnit u)
        {
            var current = s.Unit(u.TargetId);
            if (current != null && current.Active && !current.Fleeing && !u.Holding
                && (u.Side == BattleSide.Attacker || Dist(u.X, u.Y, current.X, current.Y) <= AggroRange * 1.5f))
                return current;
            BattleUnit best = null;
            float bestD = float.MaxValue;
            float reach = Reach(s, u);
            foreach (var e in s.Units)
            {
                if (!e.Active || e.Side == u.Side || e.Fleeing) continue;
                float d = Dist(u.X, u.Y, e.X, e.Y);
                if (u.Side == BattleSide.Defender && d > AggroRange) continue;
                if (u.Holding && d > reach) continue;
                if (d < bestD) { bestD = d; best = e; }
            }
            u.TargetId = best?.Id ?? -1;
            return best;
        }

        static bool OnTower(BattleState s, BattleUnit u) => s.Map.Inside(u.CellX, u.CellY) && s.Map.Tower[u.CellX, u.CellY];

        static float CardRange(BattleState s, BattleUnit u, ArtDef art) =>
            art != null && art.Type == ArtType.Hidden ? HiddenRange + (OnTower(s, u) ? TowerRangeBonus : 0) : MeleeRange;

        /// <summary>지금 내력으로 칠 수 있는 가장 먼 거리.</summary>
        public static float Reach(BattleState s, BattleUnit u)
        {
            float r = MeleeRange;
            foreach (var (card, art, _) in u.Deck)
                if (card.IsOffense && card.Cost <= u.Force) r = Math.Max(r, CardRange(s, u, art));
            return r;
        }

        static void Attack(BattleState s, BattleUnit a, BattleUnit t, float dist)
        {
            var options = a.Deck.Where(x => x.card.IsOffense && x.card.Cost <= a.Force && CardRange(s, a, x.art) >= dist).ToList();
            CardDef card;
            ArtDef art;
            int mastery;
            if (options.Count == 0)
            {
                card = Duel.BasicFists.Cards[0];
                art = Duel.BasicFists;
                mastery = 1;
            }
            else
            {
                double tRatio = t.MaxHp <= 0 ? 0 : t.Hp / (double)t.MaxHp;
                var weights = options.Select(x =>
                {
                    double expect = t.Deck.Count == 0 ? 1 : t.Deck.Average(y => Duel.StyleMult(x.card.Style, y.card.Style));
                    double wgt = x.card.Power * expect;
                    if (x.card.Kind == CardKind.Finisher) wgt *= tRatio <= 0.3 ? 3 : 0.6;
                    return Math.Max(0.01, wgt);
                }).ToList();
                double roll = s.Rng.NextDouble() * weights.Sum();
                int i = 0;
                for (; i < weights.Count - 1; i++)
                {
                    roll -= weights[i];
                    if (roll < 0) break;
                }
                (card, art, mastery) = options[i];
            }
            a.Force -= card.Cost;
            a.Cooldown = a.AttackInterval;

            double value = card.Power * (1 + mastery / 10.0) * Math.Pow(RealmBase, a.Realm - t.Realm)
                * (Duel.RollMin + s.Rng.NextDouble() * (Duel.RollMax - Duel.RollMin)) * a.Modifier * (OnTower(s, a) ? TowerDamage : 1);
            // 경지가 낮은 상대의 손속은 흘려보내기 쉽고, 높은 상대의 손속은 피하기 어렵다.
            double evade = Math.Max(0.05, Math.Min(0.6, 0.15 + 0.02 * t.MovementMastery + EvadePerRealm * (t.Realm - a.Realm)));
            if (s.Rng.Chance(evade)) value = 0;
            else
            {
                var guard = t.Deck.Where(x => x.card.Kind == CardKind.Defense && x.card.Cost <= t.Force).OrderByDescending(x => x.card.Power).FirstOrDefault();
                if (guard.card != null)
                {
                    t.Force -= guard.card.Cost;
                    value *= 0.6;
                }
            }
            if (card.Kind == CardKind.Finisher && a.Named) s.AddLog($"{Josa.IGa(a.Name)} 절초 '{card.Name}'{Josa.EulReul(card.Name).Substring(card.Name.Length)} 펼쳤다!");
            int dmg = (int)Math.Round(value);
            if (dmg <= 0) return;
            t.Hp -= dmg;
            if (t.Hp > 0) return;
            t.Hp = 0;
            t.Down = true;
            a.Downed++;
            if (a.Named || t.Named || t.IsLeader)
                s.AddLog($"{Josa.IGa(a.Name)} '{card.Name}'{Josa.EuroRo(card.Name).Substring(card.Name.Length)} {Josa.EulReul(t.Name)} 쓰러뜨렸다.");
        }

        static void MoveTo(BattleState s, BattleUnit u, int cx, int cy)
        {
            if (u.Path.Count == 0 || u.PathGoalX != cx || u.PathGoalY != cy)
            {
                if (u.RepathTimer > 0 && u.Path.Count > 0) { /* 잠시 지난 길로 간다 */ }
                else
                {
                    u.Path = s.Map.FindPath(u.CellX, u.CellY, cx, cy);
                    u.PathGoalX = cx;
                    u.PathGoalY = cy;
                    u.RepathTimer = RepathSeconds;
                }
            }
            float step = u.Speed * BattleState.Dt;
            while (step > 0)
            {
                float tx, ty;
                if (u.Path.Count > 0)
                {
                    tx = u.Path[0].x + 0.5f;
                    ty = u.Path[0].y + 0.5f;
                }
                else if (u.CellX == cx && u.CellY == cy)
                {
                    tx = cx + 0.5f;
                    ty = cy + 0.5f;
                }
                else return;
                float d = Dist(u.X, u.Y, tx, ty);
                if (d <= step)
                {
                    u.X = tx;
                    u.Y = ty;
                    step -= d;
                    if (u.Path.Count > 0) u.Path.RemoveAt(0);
                    else return;
                }
                else
                {
                    u.X += (tx - u.X) / d * step;
                    u.Y += (ty - u.Y) / d * step;
                    return;
                }
            }
        }

        static void CheckRout(BattleState s)
        {
            if (s.Routed) return;
            var attackers = s.Side(BattleSide.Attacker).ToList();
            if (attackers.Count == 0) return;
            bool leaderDown = attackers.Any(u => u.IsLeader && u.Down);
            int down = attackers.Count(u => u.Down);
            if (!leaderDown && down < attackers.Count * RoutRatio) return;
            s.Routed = true;
            foreach (var u in attackers.Where(u => u.Active))
            {
                u.Fleeing = true;
                u.Order = null;
                u.Path.Clear();
                u.PathGoalX = u.PathGoalY = -1;
            }
            s.AddLog(leaderDown ? "두목이 쓰러지자 습격자들이 무너져 달아나기 시작했다." : "습격자들이 겁을 먹고 달아나기 시작했다.");
        }

        static void CheckEnd(BattleState s)
        {
            bool attackersLeft = s.Side(BattleSide.Attacker).Any(u => u.Active);
            bool defendersLeft = s.Side(BattleSide.Defender).Any(u => u.Active);
            if (!attackersLeft)
            {
                s.Over = true;
                s.DefendersWon = true;
                s.AddLog("습격을 막아냈다.");
            }
            else if (!defendersLeft)
            {
                s.Over = true;
                s.DefendersWon = false;
                s.AddLog("장원을 지키던 이들이 모두 쓰러졌다.");
            }
            else if (s.Tick >= BattleState.MaxTicks)
            {
                s.Over = true;
                s.DefendersWon = true;
                s.TimedOut = true;
                s.AddLog("날이 저물자 습격자들이 물러갔다.");
            }
        }

        public static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
