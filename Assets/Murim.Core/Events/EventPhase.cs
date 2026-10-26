using System.Collections.Generic;
using System.Linq;
using Murim.Core.Simulation;
using Murim.Core.World;

namespace Murim.Core.Events
{
    /// <summary>월 처리의 사건 단계: 예약된 후속 사건 → 세계 사건 → 이야기꾼이 고른 플레이어 사건.</summary>
    public class EventPhase : ITurnPhase
    {
        public const int WorldAttemptsPerMonth = 4;
        public const int MaxPlayerEventsPerMonth = 3;
        public const int MaxNoticesPerMonth = 3;

        public void Run(TurnContext t)
        {
            var w = t.World;
            var d = t.Data;
            if (d.Events.Count == 0) return;
            var index = CandidateIndex.Build(w);
            var month = new MonthCount();

            foreach (var s in w.Events.Scheduled.Where(s => s.Month <= w.Month).ToList())
            {
                w.Events.Scheduled.Remove(s);
                if (!d.Events.TryGetValue(s.EventId, out var e)) continue;
                var ctx = RoleBinder.TryBind(w, d, t.Rng, e, s.Roles, index, false, ignoreCooldown: true);
                if (ctx == null) continue;
                if (ctx.IsPlayerDecider)
                {
                    if (month.Player >= MaxPlayerEventsPerMonth) continue;
                    Enqueue(ctx);
                    month.Player++;
                }
                else ResolveWorld(ctx, month);
            }

            var pool = d.Events.Values.Where(e => !e.FollowupOnly && !e.PlayerOnly && !e.System).ToList();
            for (int i = 0; i < WorldAttemptsPerMonth && pool.Count > 0; i++)
            {
                var e = Storyteller.WeightedPick(pool, x => x.Weight, t.Rng);
                var ctx = RoleBinder.TryBind(w, d, t.Rng, e, null, index, true, w.PlayerCharacterId);
                if (ctx != null) ResolveWorld(ctx, month);
            }

            foreach (var ctx in Storyteller.PickForPlayer(t, index, month.Player))
            {
                Enqueue(ctx);
                month.Player++;
            }
        }

        class MonthCount
        {
            public int Player;
            public int Notices;
        }

        /// <summary>플레이어의 답을 기다리는 사건으로 넣는다.</summary>
        public static PendingEvent Enqueue(EventContext ctx)
        {
            EventResolver.MarkFired(ctx);
            var p = new PendingEvent
            {
                EventId = ctx.Def.Id, Title = ctx.Def.Name, Text = EventText.Format(ctx.Def.Text, ctx), Month = ctx.W.Month,
                Spawned = ctx.Spawned.ToList(),
            };
            foreach (var kv in ctx.Roles) p.Roles[kv.Key] = kv.Value;
            ctx.W.Events.Pending.Add(p);
            return p;
        }

        /// <summary>AI가 바로 고른다. 플레이어가 얽혀 있고 중요하면 소식을 남긴다.</summary>
        public static void ResolveWorld(EventContext ctx, int noticesSoFar, out bool noticed)
        {
            var w = ctx.W;
            EventResolver.MarkFired(ctx);
            EventResolver.Resolve(ctx, EventAI.Choose(ctx));
            noticed = false;
            if (ctx.Def.Importance < 1 || noticesSoFar >= MaxNoticesPerMonth || !InvolvesPlayer(ctx)) return;
            w.Events.Pending.Add(new PendingEvent
            {
                EventId = ctx.Def.Id, IsNotice = true, Title = ctx.Def.Name, Text = w.Chronicle[w.Chronicle.Count - 1].Text, Month = w.Month,
                Detail = w.Chronicle[w.Chronicle.Count - 1].Detail,
            });
            noticed = true;
        }

        static void ResolveWorld(EventContext ctx, MonthCount month)
        {
            ResolveWorld(ctx, month.Notices, out bool noticed);
            if (noticed) month.Notices++;
        }

        public static bool InvolvesPlayer(EventContext ctx)
        {
            var player = ctx.W.Player;
            if (player == null) return false;
            if (ctx.Roles.Values.Contains(player.Id)) return true;
            var decider = ctx.Decider;
            return player.FactionId != null && decider != null && decider.FactionId == player.FactionId;
        }
    }
}
