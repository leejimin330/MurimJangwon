using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Manor;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Battle
{
    /// <summary>습격 → 양쪽 단위 배치 (설계 3·4절).</summary>
    public static class BattleSetup
    {
        public const int MaxDefenders = 10;
        public const int MaxGrunts = 10;
        public const int GruntHp = 40;
        public const int GruntForce = 20;
        static readonly string[] Stems = { "갑", "을", "병", "정", "무", "기", "경", "신", "임", "계" };

        /// <summary>살아 있는 성인 소속원, 경지 높은 순 (같으면 id).</summary>
        public static List<Character> PickDefenders(WorldState w, Faction f, int max = MaxDefenders) =>
            f == null ? new List<Character>() : FactionOps.LivingMembers(w, f).Where(c => c.AgeYears(w.Month) >= SimConstants.AdultAge)
                .OrderByDescending(c => c.Id == f.LeaderId ? 1 : 0).ThenByDescending(c => c.Realm).ThenBy(c => c.Id, StringComparer.Ordinal)
                .Take(max).ToList();

        /// <summary>두목과 함께 오는 같은 세력 성인 (두목 제외), 경지 높은 순.</summary>
        public static List<Character> PickAllies(WorldState w, Character leader, int count)
        {
            var f = w.GetFaction(leader?.FactionId);
            if (f == null || count <= 0) return new List<Character>();
            return FactionOps.LivingMembers(w, f).Where(c => c.Id != leader.Id && c.AgeYears(w.Month) >= SimConstants.AdultAge)
                .OrderByDescending(c => c.Realm).ThenBy(c => c.Id, StringComparer.Ordinal).Take(count).ToList();
        }

        public static BattleState Create(WorldState w, GameData d, ulong seed, ManorState manor, Faction defender,
            IList<Character> defenders, int defenderGrunts, Character attackerLeader, IList<Character> attackers, int attackerGrunts)
        {
            var s = new BattleState { Map = BattleMap.FromManor(w, d, manor), Rng = new GameRandom(seed) };
            s.ExitX = ManorOps.GateX[0] + 1f;
            s.ExitY = ManorOps.GateY + 0.5f;

            var defenseSpots = Spots(s.Map, 11.5f, 9.5f);
            int i = 0;
            foreach (var c in defenders) Add(s, w, d, c, BattleSide.Defender, c.Id == defender?.LeaderId, defenseSpots[i++ % defenseSpots.Count]);
            for (int g = 0; g < Math.Min(MaxGrunts, defenderGrunts); g++)
                AddGrunt(s, "문도 " + Stems[g % Stems.Length], BattleSide.Defender, defenseSpots[i++ % defenseSpots.Count]);

            var attackSpots = Spots(s.Map, ManorOps.GateX[0] + 1f, ManorOps.GateY + 0.5f);
            int j = 0;
            if (attackerLeader != null) Add(s, w, d, attackerLeader, BattleSide.Attacker, true, attackSpots[j++ % attackSpots.Count]);
            foreach (var c in attackers.Where(c => c.Id != attackerLeader?.Id))
                Add(s, w, d, c, BattleSide.Attacker, false, attackSpots[j++ % attackSpots.Count]);
            for (int g = 0; g < Math.Min(MaxGrunts, attackerGrunts); g++)
                AddGrunt(s, "졸개 " + Stems[g % Stems.Length], BattleSide.Attacker, attackSpots[j++ % attackSpots.Count]);
            return s;
        }

        /// <summary>기준점에서 가까운 걸을 수 있는 칸들 (거리, y, x 순).</summary>
        static List<(int x, int y)> Spots(BattleMap map, float cx, float cy)
        {
            var list = new List<(int x, int y, float d)>();
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (map.Walkable(x, y)) list.Add((x, y, BattleSim.Dist(x + 0.5f, y + 0.5f, cx, cy)));
            return list.OrderBy(t => t.d).ThenBy(t => t.y).ThenBy(t => t.x).Take(40).Select(t => (t.x, t.y)).ToList();
        }

        static void Add(BattleState s, WorldState w, GameData d, Character c, BattleSide side, bool leader, (int x, int y) at)
        {
            int mod = 0;
            foreach (var kv in c.Flags)
                if (c.HasFlag(kv.Key, w.Month) && d.Flags.TryGetValue(kv.Key, out var fd)) mod += fd.DuelModifier;
            var deck = Duel.Deck(d, c);
            int movement = ArtGranting.Externals(d, c).Where(x => x.def.IsMovement).Select(x => x.known.Mastery).DefaultIfEmpty(0).Max();
            int force = Duel.MaxForce(d, c);
            s.Units.Add(new BattleUnit
            {
                Id = s.Units.Count, CharacterId = c.Id, Name = c.FullName, Side = side, IsLeader = leader,
                X = at.x + 0.5f, Y = at.y + 0.5f, Hp = Duel.MaxHp(d, c), MaxHp = Duel.MaxHp(d, c), Force = force, MaxForce = force,
                Realm = c.Realm, Deck = deck, MovementMastery = movement, Speed = 2f + movement * 0.1f, AttackInterval = 1.2f,
                Modifier = 1 + mod / 100.0,
            });
        }

        static void AddGrunt(BattleState s, string name, BattleSide side, (int x, int y) at)
        {
            var deck = Duel.BasicFists.Cards.Select(card => (card, Duel.BasicFists, 1)).ToList();
            s.Units.Add(new BattleUnit
            {
                Id = s.Units.Count, Name = name, Side = side, X = at.x + 0.5f, Y = at.y + 0.5f, Hp = GruntHp, MaxHp = GruntHp,
                Force = GruntForce, MaxForce = GruntForce, Realm = 0, Deck = deck, Speed = 1.8f, AttackInterval = 1.5f,
            });
        }
    }
}
