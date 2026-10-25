using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.Text;
using Murim.Core.World;

namespace Murim.Core.Events
{
    public static class ConditionCheck
    {
        /// <summary>인물이 있는 지역. 위치가 없으면 세력 본거지.</summary>
        public static string RegionOf(WorldState w, Character c) =>
            c == null ? null : c.LocationRegionId ?? w.GetFaction(c.FactionId)?.HomeRegionId;

        /// <summary>무소속은 협의로 정·사를 가른다.</summary>
        public static Alignment AlignmentOf(WorldState w, Character c)
        {
            var f = w.GetFaction(c.FactionId);
            if (f != null && !f.IsDissolved) return f.Alignment;
            return c.Chivalry >= 0 ? Alignment.Righteous : Alignment.Unorthodox;
        }

        public static int ChivalryOf(WorldState w, Character c) => Wallet.LedFaction(w, c)?.Chivalry ?? c.Chivalry;

        static string TraitName(GameData d, string id) => d.Traits.TryGetValue(id, out var t) ? t.Name : id;

        /// <summary>null이면 통과. 아니면 화면에 보일 사유.</summary>
        public static string Why(Conditions c, Character ch, EventContext ctx) => Why(c, ch, ctx, false, false);

        /// <param name="deferUnbound">아직 채우지 않은 역할을 가리키는 조건은 통과로 본다 (역할 묶는 도중).</param>
        /// <param name="skipChance">chance는 굴리지 않는다.</param>
        public static string Why(Conditions c, Character ch, EventContext ctx, bool deferUnbound, bool skipChance)
        {
            if (c == null) return null;
            if (ch == null) return "조건이 맞지 않습니다";
            var w = ctx.W;
            var d = ctx.D;
            if (!c.IncludeDead && !ch.IsAlive) return "세상을 떠난 사람입니다";
            int age = ch.AgeYears(w.Month);
            if (c.AgeMin != null && age < c.AgeMin) return $"{c.AgeMin}세 이상이어야 합니다";
            if (c.AgeMax != null && age > c.AgeMax) return $"{c.AgeMax}세 이하여야 합니다";
            if (c.Sex != null && ch.Sex != c.Sex) return c.Sex == Sex.Male ? "남자만 고를 수 있습니다" : "여자만 고를 수 있습니다";
            if (c.TraitsAll != null)
                foreach (var t in c.TraitsAll)
                    if (!ch.HasTrait(t)) return $"{TraitName(d, t)}만 고를 수 있습니다";
            if (c.TraitsAny != null && c.TraitsAny.Length > 0 && !c.TraitsAny.Any(ch.HasTrait))
                return c.TraitsAny.Length == 1
                    ? $"{TraitName(d, c.TraitsAny[0])}만 고를 수 있습니다"
                    : string.Join("·", c.TraitsAny.Select(t => TraitName(d, t))) + " 중 하나가 있어야 합니다";
            if (c.TraitsNone != null)
                foreach (var t in c.TraitsNone)
                    if (ch.HasTrait(t)) return $"{Josa.EunNeun(TraitName(d, t))} 고를 수 없습니다";
            if (c.Roles != null && c.Roles.Length > 0)
            {
                var role = ch.FactionId == null ? Role.None : ch.Role;
                if (!c.Roles.Contains(role)) return "조건이 맞지 않습니다";
            }
            if (c.Alignment != null && c.Alignment.Length > 0 && !c.Alignment.Contains(AlignmentOf(w, ch))) return "조건이 맞지 않습니다";
            if (c.Married != null)
            {
                var spouse = w.Get(ch.SpouseId);
                bool married = spouse != null && spouse.IsAlive;
                if (married != c.Married) return c.Married.Value ? "혼인한 사람만 고를 수 있습니다" : "혼인하지 않은 사람만 고를 수 있습니다";
            }
            if (c.RealmMin != null && ch.Realm < c.RealmMin) return $"{Titles.Realm(c.RealmMin.Value)} 이상의 경지가 필요합니다";
            if (c.RealmMax != null && ch.Realm > c.RealmMax) return "조건이 맞지 않습니다";
            if (c.SilverMin != null && Wallet.Silver(w, ch) < c.SilverMin) return $"은자 {c.SilverMin}냥이 필요합니다";
            if (c.FameMin != null && Wallet.Fame(w, ch) < c.FameMin) return $"명성이 {c.FameMin} 이상이어야 합니다";
            int chivalry = ChivalryOf(w, ch);
            if (c.ChivalryMin != null && chivalry < c.ChivalryMin) return $"협의가 {c.ChivalryMin} 이상이어야 합니다";
            if (c.ChivalryMax != null && chivalry > c.ChivalryMax) return $"협의가 {c.ChivalryMax} 이하여야 합니다";
            if (c.FactionSizeMin != null)
            {
                var f = w.GetFaction(ch.FactionId);
                if (f == null || FactionOps.LivingMembers(w, f).Count() < c.FactionSizeMin) return "조건이 맞지 않습니다";
            }
            if (c.RegionStabilityMin != null || c.RegionStabilityMax != null)
            {
                var r = RegionOf(w, ch);
                if (r == null || !w.Regions.TryGetValue(r, out var rs)) return "조건이 맞지 않습니다";
                if (c.RegionStabilityMin != null && rs.Stability < c.RegionStabilityMin) return "조건이 맞지 않습니다";
                if (c.RegionStabilityMax != null && rs.Stability > c.RegionStabilityMax) return "조건이 맞지 않습니다";
            }
            if (c.HasFlag != null && !ch.HasFlag(c.HasFlag, w.Month)) return "조건이 맞지 않습니다";
            if (c.NotFlag != null && ch.HasFlag(c.NotFlag, w.Month)) return "조건이 맞지 않습니다";
            if (c.Memory != null && !HasMemory(c.Memory, ch, ctx)) return "조건이 맞지 않습니다";
            if (c.OpinionOf != null)
            {
                var other = ctx.Get(c.OpinionOf.Role);
                if (other == null)
                {
                    if (!deferUnbound) return "조건이 맞지 않습니다";
                }
                else
                {
                    int op = Opinion.Of(w, d, ch.Id, other.Id);
                    if (c.OpinionOf.Min != null && op < c.OpinionOf.Min) return "호감이 모자랍니다";
                    if (c.OpinionOf.Max != null && op > c.OpinionOf.Max) return "조건이 맞지 않습니다";
                }
            }
            if (c.OppositeSexOf != null)
            {
                var other = ctx.Get(c.OppositeSexOf);
                if (other == null ? !deferUnbound : other.Sex == ch.Sex) return "조건이 맞지 않습니다";
            }
            if (c.Chance != null && !skipChance && !ctx.Rng.Chance(c.Chance.Value)) return "때가 아닙니다";
            return null;
        }

        public static bool Passes(Conditions c, Character ch, EventContext ctx) => Why(c, ch, ctx) == null;

        /// <summary>조건에 맞는 기억의 대상들. 같은 대상에 대한 기억은 강도를 합쳐 본다.
        /// Target 역할이 아직 채워지지 않았으면 대상은 따지지 않는다.</summary>
        public static IEnumerable<string> MatchingTargets(MemoryCond m, Character ch, EventContext ctx)
        {
            string only = m.Target != null && ctx.Roles.TryGetValue(m.Target, out var id) ? id : null;
            return ch.Memories
                .Where(mem => mem.Kind == m.Kind && (m.Inherited == null || mem.Inherited == m.Inherited) && (only == null || mem.TargetId == only))
                .GroupBy(mem => mem.TargetId)
                .Where(g => g.Sum(mem => mem.Strength) >= m.MinStrength)
                .Select(g => g.Key)
                .OrderBy(t => t, System.StringComparer.Ordinal);
        }

        static bool HasMemory(MemoryCond m, Character ch, EventContext ctx) => MatchingTargets(m, ch, ctx).Any();
    }
}
