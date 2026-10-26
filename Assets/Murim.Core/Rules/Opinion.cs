using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.World;

namespace Murim.Core.Rules
{
    public static class Opinion
    {
        /// <summary>from이 to를 어떻게 보는가. 특성 상성 + 저장 변화량 + 기억, -100~100.</summary>
        public static int Of(WorldState w, GameData d, string fromId, string toId)
        {
            var from = w.Get(fromId);
            var to = w.Get(toId);
            if (from == null || to == null) return 0;
            int v = TraitAffinity(d, from, to) + w.Opinions.GetStored(fromId, toId) + MemoryTotal(from, toId);
            return Math.Max(SimConstants.OpinionMin, Math.Min(SimConstants.OpinionMax, v));
        }

        public static int TraitAffinity(GameData d, Character from, Character to)
        {
            int v = 0;
            foreach (var t in from.TraitIds)
            {
                if (to.TraitIds.Contains(t)) v += SimConstants.SameTraitOpinion;
                if (d.Traits.TryGetValue(t, out var def) && def.Opposites != null)
                    v += def.Opposites.Count(o => to.TraitIds.Contains(o)) * SimConstants.OppositeTraitOpinion;
            }
            return v;
        }

        public static int MemoryTotal(Character from, string toId)
        {
            int v = 0;
            foreach (var m in from.Memories)
            {
                if (m.TargetId != toId) continue;
                v += m.Kind == MemoryKind.Grudge ? -m.Strength : m.Strength / 2;
            }
            return v;
        }

        public static void AddMemory(Character c, MemoryKind kind, string targetId, string reason, int month, int strength,
            bool inherited = false)
        {
            c.Memories.Add(new Memory
            {
                Kind = kind, TargetId = targetId, Reason = reason, Month = month, Strength = strength, Inherited = inherited,
            });
            int total = c.Memories.Where(m => m.Kind == kind && m.TargetId == targetId).Sum(m => m.Strength);
            if (kind == MemoryKind.Grudge && total >= SimConstants.NemesisThreshold) c.AddRelation(targetId, RelationTag.Nemesis);
            if (kind == MemoryKind.Debt && total >= SimConstants.BenefactorThreshold) c.AddRelation(targetId, RelationTag.Benefactor);
        }

        public static void DecayMemories(Character c)
        {
            foreach (var m in c.Memories) m.Strength = m.Strength * (100 - SimConstants.MemoryDecayPercent) / 100;
            c.Memories.RemoveAll(m => m.Strength < SimConstants.MemoryMinStrength);
        }

        public static void InheritMemories(Character deceased, IEnumerable<Character> children, int month)
        {
            // 같은 대상에 대한 기억을 합쳐 문턱을 넘으면 하나로 물려준다 (사유는 가장 강한 기억).
            var strong = deceased.Memories.GroupBy(m => (m.Kind, m.TargetId))
                .Select(g => (first: g.OrderByDescending(m => m.Strength).First(), total: g.Sum(m => m.Strength)))
                .Where(x => x.total >= SimConstants.MemoryInheritThreshold)
                .OrderBy(x => x.first.TargetId, System.StringComparer.Ordinal).ThenBy(x => x.first.Kind).ToList();
            foreach (var child in children)
                foreach (var (m, total) in strong)
                    AddMemory(child, m.Kind, m.TargetId, m.Reason, month, total / 2, true);
        }
    }
}
