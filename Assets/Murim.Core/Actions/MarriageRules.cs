using Murim.Core.Data;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    public static class MarriageRules
    {
        /// <summary>혼담 수락 점수. 0보다 크면 수락. 무소속 쪽 성향은 null.</summary>
        public static int AcceptanceScore(WorldState w, GameData d, Character proposerHead, Character targetHead,
            int proposerFame, int targetFame, Alignment? proposerAlign, Alignment? targetAlign, bool targetIsClan)
        {
            int score = Opinion.Of(w, d, targetHead.Id, proposerHead.Id);
            score += (proposerFame - targetFame) / 10;
            if (proposerAlign != null && targetAlign != null)
            {
                if (proposerAlign == targetAlign) score += SimConstants.SameAlignmentMarriageBonus;
                else if (proposerAlign == Alignment.Righteous || targetAlign == Alignment.Righteous)
                    score += SimConstants.OpposedAlignmentMarriagePenalty;
            }
            if (targetIsClan) score += SimConstants.ClanMarriageBonus;
            foreach (var t in targetHead.TraitIds)
                if (d.Traits.TryGetValue(t, out var def)) score += (int)def.Weight(ActionKeys.AcceptMarriage);
            return score;
        }

        public static bool IsMarried(WorldState w, Character c) => w.Get(c.SpouseId)?.IsAlive == true;

        public static ActionCheck Eligible(WorldState w, Character a, Character b)
        {
            if (a == null || b == null || !a.IsAlive || !b.IsAlive) return ActionCheck.Fail("살아 있는 사람이 아닙니다");
            if (a.Id == b.Id) return ActionCheck.Fail("같은 사람입니다");
            if (a.AgeYears(w.Month) < SimConstants.MarriageMinAge || b.AgeYears(w.Month) < SimConstants.MarriageMinAge)
                return ActionCheck.Fail("아직 혼인할 나이가 아닙니다");
            if (IsMarried(w, a) || IsMarried(w, b)) return ActionCheck.Fail("이미 혼인한 사람입니다");
            if (a.Sex == b.Sex) return ActionCheck.Fail("혼인할 수 없는 상대입니다");
            if (a.FatherId == b.Id || a.MotherId == b.Id || b.FatherId == a.Id || b.MotherId == a.Id)
                return ActionCheck.Fail("직계 가족입니다");
            bool sibling = (a.FatherId != null && a.FatherId == b.FatherId) || (a.MotherId != null && a.MotherId == b.MotherId);
            if (sibling) return ActionCheck.Fail("형제자매입니다");
            return ActionCheck.Pass;
        }
    }
}
