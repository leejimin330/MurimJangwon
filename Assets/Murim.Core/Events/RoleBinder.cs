using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Events
{
    /// <summary>사건의 역할에 인물을 채운다. 성립하지 않으면 null.</summary>
    public static class RoleBinder
    {
        public const int RootSample = 24;

        /// <param name="fixedRoles">역할 → 인물 id. 그 역할은 이 인물로만 채운다.</param>
        /// <param name="sampleRoot">첫 뿌리 역할 후보를 최대 24명만 시도한다 (세계 사건).</param>
        /// <param name="excludeDeciderId">결정자가 되어서는 안 되는 인물 (세계 사건에서 플레이어).</param>
        public static EventContext TryBind(WorldState w, GameData d, GameRandom rng, EventDef e, IDictionary<string, string> fixedRoles,
            CandidateIndex index, bool sampleRoot, string excludeDeciderId = null)
        {
            if (e.Month != 0 && w.Month % 12 + 1 != e.Month) return null;
            if (e.BindOrder == null || e.BindOrder.Count == 0) return null;
            fixedRoles ??= new Dictionary<string, string>();
            var ctx = new EventContext { W = w, D = d, Rng = rng, Def = e };
            var root = e.BindOrder[0];

            // 뿌리 후보는 먼저 섞은 뒤 앞에서부터 조건을 본다 (전체를 다 걸러 보지 않는다).
            var pool = index.Root(e.Roles[root].Pick).ToList();
            rng.Shuffle(pool);
            int tried = 0;
            using (var it = Filter(ctx, root, pool, fixedRoles, index, excludeDeciderId).GetEnumerator())
            {
                while (true)
                {
                    ctx.Roles.Clear();
                    if (!it.MoveNext()) break;
                    if (sampleRoot && ++tried > RootSample) break;
                    ctx.Roles[root] = it.Current.Id;
                    if (!BindRest(ctx, fixedRoles, index, excludeDeciderId)) continue;
                    if (!FinalCheck(ctx)) continue;
                    // 확률 조건은 한 번만 굴린다: 실패하면 이번에는 일어나지 않는다.
                    foreach (var name in e.BindOrder)
                    {
                        var r = e.Roles[name];
                        if (r.Chance != null && r.Pick != EventPicks.New && !rng.Chance(r.Chance.Value)) return null;
                    }
                    Spawn(ctx);
                    return ctx;
                }
            }
            return null;
        }

        static bool BindRest(EventContext ctx, IDictionary<string, string> fixedRoles, CandidateIndex index, string excludeDeciderId)
        {
            var e = ctx.Def;
            for (int i = 1; i < e.BindOrder.Count; i++)
            {
                var name = e.BindOrder[i];
                if (e.Roles[name].Pick == EventPicks.New) continue;
                var role = e.Roles[name];
                var list = Filter(ctx, name, Relative(ctx, role, ctx.Get(role.Of), index), fixedRoles, index, excludeDeciderId).ToList();
                if (list.Count == 0) return false;
                ctx.Roles[name] = list[ctx.Rng.Next(0, list.Count)].Id;
            }
            return true;
        }

        /// <summary>모든 역할을 채운 뒤 조건을 다시 본다 (나중에 채운 역할을 가리키는 조건 때문).</summary>
        static bool FinalCheck(EventContext ctx)
        {
            foreach (var kv in ctx.Def.Roles)
            {
                if (kv.Value.Pick == EventPicks.New) continue;
                if (ConditionCheck.Why(kv.Value, ctx.Get(kv.Key), ctx, false, true) != null) return false;
            }
            return true;
        }

        static IEnumerable<Character> Filter(EventContext ctx, string name, IEnumerable<Character> pool, IDictionary<string, string> fixedRoles,
            CandidateIndex index, string excludeDeciderId)
        {
            var role = ctx.Def.Roles[name];
            var used = new HashSet<string>(ctx.Roles.Values, StringComparer.Ordinal);
            fixedRoles.TryGetValue(name, out var fixedId);
            // 아직 채우지 않은 고정 역할이 이 역할에 기대면, 그 고정 인물로 이어지는 후보만 남긴다.
            var dependents = ctx.Def.Roles.Where(kv => kv.Value.Of == name && fixedRoles.ContainsKey(kv.Key)).ToList();
            foreach (var c in pool.ToList())
            {
                if (c == null || used.Contains(c.Id)) continue;
                if (fixedId != null && c.Id != fixedId) continue;
                if (fixedId == null && fixedRoles.Values.Contains(c.Id)) continue;
                if (name == ctx.Def.Decider && excludeDeciderId != null && c.Id == excludeDeciderId) continue;
                ctx.Roles[name] = c.Id;
                bool ok = ConditionCheck.Why(role, c, ctx, true, true) == null
                    && dependents.All(dep => Relative(ctx, dep.Value, c, index).Any(x => x != null && x.Id == fixedRoles[dep.Key]));
                ctx.Roles.Remove(name);
                if (ok) yield return c;
            }
        }

        /// <summary>of 인물을 기준으로 한 후보.</summary>
        public static IEnumerable<Character> Relative(EventContext ctx, RoleDef role, Character of, CandidateIndex index)
        {
            var w = ctx.W;
            if (of == null) return Enumerable.Empty<Character>();
            switch (role.Pick)
            {
                case EventPicks.MemoryTarget:
                    var ofRole = ctx.Def.Roles.TryGetValue(role.Of, out var r) ? r : null;
                    var cond = ofRole?.Memory != null && ofRole.Memory.Kind == role.MemoryKind ? ofRole.Memory : null;
                    return of.Memories
                        .Where(m => m.Kind == role.MemoryKind && (cond == null || ConditionCheck.Matches(cond, m, ctx)))
                        .Select(m => m.TargetId).Distinct().OrderBy(id => id, StringComparer.Ordinal).Select(w.Get);
                case EventPicks.Spouse:
                    return new[] { w.Get(of.SpouseId) };
                case EventPicks.Child:
                    return of.ChildIds.Select(w.Get);
                case EventPicks.LeaderOf:
                    var lf = w.GetFaction(of.FactionId);
                    return lf == null || lf.IsDissolved || lf.LeaderId == of.Id ? Enumerable.Empty<Character>() : new[] { w.Get(lf.LeaderId) };
                case EventPicks.SameFaction:
                    var sf = w.GetFaction(of.FactionId);
                    return sf == null ? Enumerable.Empty<Character>() : FactionOps.LivingMembers(w, sf).Where(c => c.Id != of.Id);
                case EventPicks.SameRegion:
                    return index.InRegion(ConditionCheck.RegionOf(w, of)).Where(c => c.Id != of.Id);
                case EventPicks.RivalLeader:
                    var here = ConditionCheck.RegionOf(w, of);
                    bool good = ConditionCheck.AlignmentOf(w, of) == Alignment.Righteous;
                    return w.Factions.Values
                        .Where(f => !f.IsDissolved && f.HomeRegionId == here && f.Id != of.FactionId && (f.Alignment == Alignment.Righteous) != good)
                        .Select(f => w.Get(f.LeaderId));
                case EventPicks.DemonicNeighborLeader:
                    var at = ConditionCheck.RegionOf(w, of);
                    if (at == null || !ctx.D.Regions.TryGetValue(at, out var def)) return Enumerable.Empty<Character>();
                    var near = new HashSet<string>(def.Neighbors, StringComparer.Ordinal) { at };
                    return w.Factions.Values
                        .Where(f => !f.IsDissolved && f.Alignment == Alignment.Demonic && f.Id != of.FactionId && near.Contains(f.HomeRegionId))
                        .Select(f => w.Get(f.LeaderId));
                default:
                    return Enumerable.Empty<Character>();
            }
        }

        /// <summary>새 인물을 만든다. 세계에는 넣지 않고 ctx.Spawned에 둔다.</summary>
        static void Spawn(EventContext ctx)
        {
            var w = ctx.W;
            foreach (var name in ctx.Def.BindOrder)
            {
                var role = ctx.Def.Roles[name];
                if (role.Pick != EventPicks.New) continue;
                var s = role.Spawn ?? new SpawnDef();
                var gen = new CharacterGenerator(ctx.D, ctx.Rng);
                var sex = s.Sex ?? (ctx.Rng.Chance(0.5) ? Sex.Male : Sex.Female);
                int age = ctx.Rng.Next(s.AgeMin, Math.Max(s.AgeMin, s.AgeMax) + 1);
                int realm = ctx.Rng.Next(s.RealmMin, Math.Max(s.RealmMin, s.RealmMax) + 1);
                var c = gen.Create(w, sex, age, null, Role.None, realm, statBonus: s.StatBonus);
                w.Characters.Remove(c.Id);
                c.LocationRegionId = ConditionCheck.RegionOf(w, ctx.Get(role.Of) ?? ctx.Decider);
                ctx.Spawned.Add(c);
                ctx.Roles[name] = c.Id;
            }
        }
    }
}
