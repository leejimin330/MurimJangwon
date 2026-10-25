using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.Simulation;
using Murim.Core.Story;
using Murim.Core.Text;
using Murim.Core.World;

namespace Murim.Core.Events
{
    /// <summary>사건 효과 실행. 역할이 비었거나 이미 죽은 인물을 가리키는 효과는 건너뛴다.</summary>
    public static class Effects
    {
        public const string DuelDeathCause = "결투";

        public static void Apply(IList<EffectDef> list, EventContext ctx)
        {
            if (list == null) return;
            foreach (var fx in list)
            {
                if (fx == null || fx.Kind == null) continue;
                Apply(fx.Kind, fx.Args, ctx);
            }
        }

        static Character Alive(EventContext ctx, string role)
        {
            var c = ctx.Get(role);
            return c != null && c.IsAlive ? c : null;
        }

        static Faction FactionOf(EventContext ctx, string role)
        {
            var c = ctx.Get(role);
            var f = c == null ? null : ctx.W.GetFaction(c.FactionId);
            return f != null && !f.IsDissolved ? f : null;
        }

        static void Apply(string kind, FxArgs args, EventContext ctx)
        {
            var w = ctx.W;
            switch (kind)
            {
                case "opinion":
                {
                    var o = (OpinionFx)args;
                    var who = Alive(ctx, o.Who);
                    var of = ctx.Get(o.Of);
                    if (who != null && of != null) w.Opinions.Add(who.Id, of.Id, o.Amount);
                    break;
                }
                case "memory":
                {
                    var m = (MemoryFx)args;
                    var holder = Alive(ctx, m.Holder);
                    var target = ctx.Get(m.Target);
                    if (holder != null && target != null)
                        Opinion.AddMemory(holder, m.Kind, target.Id, m.Reason ?? ctx.Def.Name, w.Month, m.Strength);
                    break;
                }
                case "weakenMemory":
                case "removeMemory":
                {
                    var r = (MemoryRefFx)args;
                    var holder = ctx.Get(r.Holder);
                    var target = ctx.Get(r.Target);
                    if (holder == null || target == null) break;
                    foreach (var mem in holder.Memories.Where(x => x.Kind == r.Kind && x.TargetId == target.Id))
                        mem.Strength = kind == "removeMemory" ? 0 : mem.Strength - r.Amount;
                    holder.Memories.RemoveAll(x => x.Strength < SimConstants.MemoryMinStrength);
                    if (r.Kind == MemoryKind.Grudge && Opinion.MemoryTotal(holder, target.Id) > -SimConstants.NemesisThreshold
                        && holder.Relations.TryGetValue(target.Id, out var tags)) tags.Remove(RelationTag.Nemesis);
                    break;
                }
                case "silver":
                case "fame":
                case "chivalry":
                {
                    var s = (WhoAmountFx)args;
                    var who = Alive(ctx, s.Who);
                    if (who == null) break;
                    if (kind == "silver") Wallet.AddSilver(w, who, s.Amount);
                    else if (kind == "fame") Wallet.AddFame(w, who, s.Amount);
                    else Wallet.AddChivalry(w, who, s.Amount);
                    break;
                }
                case "influence":
                {
                    var x = (FactionAmountFx)args;
                    var f = FactionOf(ctx, x.Faction);
                    if (f == null || !w.Regions.ContainsKey(f.HomeRegionId)) break;
                    if (x.Amount > 0) Influence.Gain(w, f.HomeRegionId, f.Id, x.Amount);
                    else Influence.Lose(w, f.HomeRegionId, f.Id, -x.Amount);
                    break;
                }
                case "unnamed":
                {
                    var x = (FactionAmountFx)args;
                    var f = FactionOf(ctx, x.Faction);
                    if (f != null) f.UnnamedDisciples = Math.Max(0, f.UnnamedDisciples + x.Amount);
                    break;
                }
                case "stability":
                {
                    var x = (RegionAmountFx)args;
                    var region = ConditionCheck.RegionOf(w, ctx.Get(x.Region));
                    if (region != null && w.Regions.TryGetValue(region, out var rs))
                        rs.Stability = Math.Max(0, Math.Min(100, rs.Stability + x.Amount));
                    break;
                }
                case "stat":
                {
                    var x = (StatFx)args;
                    var who = Alive(ctx, x.Who);
                    if (who != null)
                        Stats.Set(who, x.Stat, Math.Max(SimConstants.StatMin, Math.Min(SimConstants.StatMax, Stats.Get(who, x.Stat) + x.Amount)));
                    break;
                }
                case "kill":
                {
                    var x = (KillFx)args;
                    var who = Alive(ctx, x.Who);
                    if (who != null) Kill(ctx, who, x.Cause ?? "사고");
                    break;
                }
                case "flag":
                {
                    var x = (FlagFx)args;
                    Alive(ctx, x.Who)?.SetFlag(x.Id, w.Month, x.Months);
                    break;
                }
                case "clearFlag":
                {
                    var x = (FlagFx)args;
                    ctx.Get(x.Who)?.ClearFlag(x.Id);
                    break;
                }
                case "relation":
                {
                    var x = (RelationFx)args;
                    var a = ctx.Get(x.A);
                    var b = ctx.Get(x.B);
                    if (a == null || b == null) break;
                    a.AddRelation(b.Id, x.Tag);
                    b.AddRelation(a.Id, x.Tag);
                    break;
                }
                case "setAlignment":
                {
                    var x = (SetAlignmentFx)args;
                    var f = FactionOf(ctx, x.Faction);
                    if (f == null || f.Alignment == x.Alignment) break;
                    var from = f.Alignment;
                    f.Alignment = x.Alignment;
                    f.Chivalry = x.Alignment == Alignment.Righteous ? SimConstants.RighteousStartChivalry
                        : x.Alignment == Alignment.Unorthodox ? SimConstants.UnorthodoxStartChivalry
                        : SimConstants.DemonicStartChivalry;
                    Chronicle.AlignmentShifted(w, f, from);
                    break;
                }
                case "recruit":
                {
                    var x = (RecruitFx)args;
                    var who = Alive(ctx, x.Who);
                    var f = FactionOf(ctx, x.Faction);
                    if (who == null || f == null || who.FactionId == f.Id) break;
                    if (ctx.IsSpawned(who))
                    {
                        w.Characters[who.Id] = who;
                        ctx.Keep.Add(who.Id);
                    }
                    if (Wallet.LedFaction(w, who) != null) break;
                    FactionOps.MoveToFaction(w, who, f.Id, x.Role);
                    break;
                }
                case "leave":
                {
                    var who = Alive(ctx, ((WhoFx)args).Who);
                    if (who == null || who.FactionId == null || Wallet.LedFaction(w, who) != null) break;
                    var home = w.GetFaction(who.FactionId)?.HomeRegionId;
                    FactionOps.MoveToFaction(w, who, null, Role.None);
                    who.LocationRegionId ??= home;
                    break;
                }
                case "move":
                {
                    var who = Alive(ctx, ((WhoFx)args).Who);
                    if (who == null || who.FactionId != null) break;
                    if (who.LocationRegionId != null && ctx.D.Regions.TryGetValue(who.LocationRegionId, out var r) && r.Neighbors.Length > 0)
                        who.LocationRegionId = ctx.Rng.Pick(r.Neighbors);
                    break;
                }
                case "usurp":
                {
                    var who = Alive(ctx, ((WhoFx)args).Who);
                    var f = who == null ? null : w.GetFaction(who.FactionId);
                    var old = f == null ? null : w.Get(f.LeaderId);
                    if (f == null || f.IsDissolved || old == null || old.Id == who.Id) break;
                    FactionOps.MoveToFaction(w, old, null, Role.None);
                    old.LocationRegionId = f.HomeRegionId;
                    if (f.HeirId == who.Id) f.HeirId = null;
                    who.Role = Role.Leader;
                    f.LeaderId = who.Id;
                    Opinion.AddMemory(old, MemoryKind.Grudge, who.Id, "하극상", w.Month, 60);
                    Chronicle.Usurped(w, f, who, old);
                    break;
                }
                case "allianceScore":
                {
                    var x = (PairAmountFx)args;
                    var a = FactionOf(ctx, x.A);
                    var b = FactionOf(ctx, x.B);
                    if (a != null && b != null && a.Id != b.Id) w.AddAllianceScore(a.Id, b.Id, x.Amount);
                    break;
                }
                case "duel":
                {
                    var x = (DuelFx)args;
                    var a = Alive(ctx, x.A);
                    var b = Alive(ctx, x.B);
                    if (a == null || b == null) break;
                    int score = PersonalRules.SparScore(a, b, ctx.Rng.Next(-20, 21)) + DuelModifier(ctx, a) - DuelModifier(ctx, b);
                    bool aWins = score > 0;
                    var loser = aWins ? b : a;
                    Apply(aWins ? x.Win : x.Lose, ctx);
                    if (loser.IsAlive && ctx.Rng.Chance(x.DeathChance))
                    {
                        ctx.Log.Add($"{Josa.EunNeun(loser.FullName)} 그 자리에서 목숨을 잃었다.");
                        Kill(ctx, loser, DuelDeathCause);
                    }
                    break;
                }
                case "contest":
                {
                    var x = (ContestFx)args;
                    var who = Alive(ctx, x.Who);
                    if (who == null) break;
                    int mine = Stats.Get(who, x.Stat) + ctx.Rng.Next(1, 21);
                    var other = x.AgainstRole == null ? null : ctx.Get(x.AgainstRole);
                    int theirs = other != null ? Stats.Get(other, x.AgainstStat) + ctx.Rng.Next(1, 21) : x.Against;
                    Apply(mine > theirs ? x.Win : x.Lose, ctx);
                    break;
                }
                case "chance":
                {
                    var x = (ChanceFx)args;
                    Apply(ctx.Rng.Chance(x.P) ? x.Then : x.Else, ctx);
                    break;
                }
                case "schedule":
                {
                    var x = (ScheduleFx)args;
                    var s = new ScheduledEvent { EventId = x.Event, Month = w.Month + ctx.Rng.Next(x.AfterMonths[0], x.AfterMonths[1] + 1) };
                    foreach (var kv in x.Roles)
                        if (ctx.Roles.TryGetValue(kv.Value, out var id)) s.Roles[kv.Key] = id;
                    w.Events.Scheduled.Add(s);
                    break;
                }
                case "chronicle":
                    ctx.Log.Add(EventText.Format(((ChronicleFx)args).Text, ctx));
                    break;
            }
        }

        static int DuelModifier(EventContext ctx, Character c)
        {
            int m = 0;
            foreach (var kv in c.Flags)
                if (c.HasFlag(kv.Key, ctx.W.Month) && ctx.D.Flags.TryGetValue(kv.Key, out var def)) m += def.DuelModifier;
            return m;
        }

        /// <summary>세계의 인물은 1단계 사망 처리(기억 대물림, 연대기)를 그대로 탄다. 새 인물은 표시만 한다.</summary>
        static void Kill(EventContext ctx, Character c, string cause)
        {
            if (ctx.IsSpawned(c) && !ctx.Keep.Contains(c.Id))
            {
                c.DeathMonth = ctx.W.Month;
                c.DeathCause = cause;
                return;
            }
            DeathPhase.Kill(ctx.W, c, cause);
        }
    }
}
