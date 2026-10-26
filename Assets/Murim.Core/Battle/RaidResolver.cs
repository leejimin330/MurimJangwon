using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Events;
using Murim.Core.Manor;
using Murim.Core.Text;
using Murim.Core.World;

namespace Murim.Core.Battle
{
    /// <summary>사건 raid 효과: 플레이어 장원이면 전투를 미루고, 아니면 바로 자동 해결한다. 결과(사상자·문도·갈래)를 적용한다.</summary>
    public static class RaidResolver
    {
        public const double DownedDeathChance = 0.25;
        public const int InjuredMonths = 6;
        public const string DeathCause = "습격";
        public const int MaxDetailLines = 20;

        static Faction DefenderFaction(WorldState w, Character target)
        {
            var f = w.GetFaction(target?.FactionId);
            return f != null && !f.IsDissolved ? f : null;
        }

        public static string AttackerName(WorldState w, Character attacker, EventContext ctx, string role)
        {
            var f = w.GetFaction(attacker?.FactionId);
            if (f != null && !f.IsDissolved) return f.Name;
            var epithet = ctx?.EpithetOf(role);
            return epithet != null ? epithet + " " + attacker?.FullName : attacker?.FullName ?? "습격자";
        }

        /// <summary>raid 효과 실행 (Effects가 부른다).</summary>
        public static void Begin(EventContext ctx, RaidFx fx)
        {
            var w = ctx.W;
            var d = ctx.D;
            var attacker = Alive(ctx.Get(fx.Attacker));
            var target = Alive(ctx.Get(fx.Target));
            if (attacker == null || target == null) return;
            var defFaction = DefenderFaction(w, target);
            bool spawned = ctx.IsSpawned(attacker);
            var allies = spawned ? new List<Character>() : BattleSetup.PickAllies(w, attacker, fx.Allies);
            int grunts = fx.Grunts + (spawned ? fx.Allies : 0);
            string name = AttackerName(w, attacker, ctx, fx.Attacker);

            bool playerManor = target.Id == w.PlayerCharacterId && defFaction?.Manor != null && Wallet.LedFaction(w, target) == defFaction;
            if (playerManor)
            {
                // 전투 동안 새 인물(산적 두목 등)을 세계에 남긴다.
                if (spawned)
                {
                    w.Characters[attacker.Id] = attacker;
                    ctx.Keep.Add(attacker.Id);
                }
                var raid = new RaidState
                {
                    EventId = ctx.Def.Id, ChoiceIndex = ctx.ChoiceIndex, EffectIndex = ctx.EffectIndex, Seed = ctx.Rng.NextULong(),
                    AttackerLeaderId = attacker.Id, AttackerIds = allies.Select(c => c.Id).ToList(), AttackerGrunts = grunts,
                    DefenderGrunts = Math.Min(BattleSetup.MaxGrunts, defFaction.UnnamedDisciples), AttackerName = name, Month = w.Month,
                };
                foreach (var kv in ctx.Roles) raid.Roles[kv.Key] = kv.Value;
                w.PendingRaid = raid;
                ctx.Log.Add($"{name}의 무리가 장원으로 몰려온다!");
                return;
            }

            var manor = defFaction?.Manor ?? ManorOps.CreateDefault(d);
            var defenders = defFaction == null ? new List<Character> { target } : BattleSetup.PickDefenders(w, defFaction);
            if (!defenders.Contains(target) && defenders.Count < BattleSetup.MaxDefenders) defenders.Add(target);
            int defGrunts = Math.Min(BattleSetup.MaxGrunts, defFaction?.UnnamedDisciples ?? 0);
            var state = BattleSetup.Create(w, d, ctx.Rng.NextULong(), manor, defFaction, defenders, defGrunts, attacker, allies, grunts);
            BattleSim.RunToEnd(state);
            Finish(ctx, fx, state, defFaction, name);
        }

        /// <summary>전투 결과 적용: 사상자, 문도 손실, 결과 문장·경과, 갈래 효과.</summary>
        public static RaidOutcome Finish(EventContext ctx, RaidFx fx, BattleState s, Faction defFaction, string attackerName)
        {
            var w = ctx.W;
            var o = new RaidOutcome { DefendersWon = s.DefendersWon };
            int ourDead = 0;
            foreach (var u in s.Units.Where(u => u.Down))
            {
                if (!u.Named)
                {
                    if (u.Side == BattleSide.Defender) o.DefenderGruntsLost++;
                    else o.AttackersDowned++;
                    continue;
                }
                if (u.Side == BattleSide.Attacker) o.AttackersDowned++;
                var c = w.Get(u.CharacterId) ?? ctx.Spawned.FirstOrDefault(x => x.Id == u.CharacterId);
                if (c == null || !c.IsAlive) continue;
                if (s.Rng.Chance(DownedDeathChance))
                {
                    o.Dead.Add(c.FullName);
                    if (u.Side == BattleSide.Defender) ourDead++;
                    Effects.Kill(ctx, c, DeathCause);
                }
                else
                {
                    o.Injured.Add(c.FullName);
                    c.SetFlag("injured", w.Month, InjuredMonths);
                }
            }
            if (defFaction != null) defFaction.UnnamedDisciples = Math.Max(0, defFaction.UnnamedDisciples - o.DefenderGruntsLost);

            o.Summary = s.DefendersWon
                ? $"{attackerName}의 습격을 물리쳤다 (쓰러뜨린 적 {o.AttackersDowned}, 아군 사망 {ourDead}, 잃은 문도 {o.DefenderGruntsLost})."
                : $"{attackerName}의 습격에 장원이 짓밟혔다 (아군 사망 {ourDead}, 잃은 문도 {o.DefenderGruntsLost}).";
            o.Lines = s.Log.Take(MaxDetailLines).ToArray();
            int before = ctx.Log.Count;
            ctx.Log.Add(o.Summary);
            if (ctx.DuelLines.Count > 0) ctx.DuelLines.Add("");
            ctx.DuelLines.AddRange(o.Lines);
            Effects.Apply(s.DefendersWon ? fx.Win : fx.Lose, ctx);
            o.Result = string.Join(" ", ctx.Log.Skip(before));
            return o;
        }

        static Character Alive(Character c) => c != null && c.IsAlive ? c : null;
    }
}
