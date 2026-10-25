using System;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    public class RecruitNamed : FactionAction
    {
        public const int Cost = 20;

        public RecruitNamed(string actorId) : base(actorId) { }

        public override string Key => ActionKeys.RecruitNamed;
        public override string Describe(WorldState w) => "제자 받아들이기";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own) => NeedSilver(own, Cost);

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            own.Silver -= Cost;
            var local = w.Characters.Values
                .Where(c => c.IsAlive && c.FactionId == null && c.Id != w.PlayerCharacterId
                    && c.LocationRegionId == own.HomeRegionId && c.AgeYears(w.Month) >= SimConstants.AdultAge)
                .OrderBy(c => Math.Abs(c.Charisma - actor.Charisma)).ThenBy(c => c.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            Character recruit;
            if (local != null)
            {
                recruit = local;
                FactionOps.MoveToFaction(w, recruit, own.Id, Role.Disciple);
            }
            else
            {
                recruit = new CharacterGenerator(d, rng).Create(w, rng.Chance(0.25) ? Sex.Female : Sex.Male, rng.Next(14, 26),
                    own.Id, Role.Disciple, 0, null, 12, actor.Charisma / 4);
            }
            recruit.AddRelation(actor.Id, RelationTag.Master);
            actor.AddRelation(recruit.Id, RelationTag.Disciple);
        }
    }

    public class RecruitUnnamed : FactionAction
    {
        public const int CostEach = 5;
        public readonly int Count;

        public RecruitUnnamed(string actorId, int count) : base(actorId) => Count = count;

        public override string Key => ActionKeys.RecruitUnnamed;
        public override string Describe(WorldState w) => $"제자 {Count}명 모집";

        public static int Cap(WorldState w, Faction f) => Influence.Total(w, f.Id) * 5 + 10;

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            if (Count < 1) return ActionCheck.Fail("한 명 이상 모집해야 합니다");
            if (own.UnnamedDisciples + Count > Cap(w, own)) return ActionCheck.Fail($"영향력이 부족해 {Cap(w, own)}명 이상 거느릴 수 없습니다");
            return NeedSilver(own, CostEach * Count);
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            own.Silver -= CostEach * Count;
            own.UnnamedDisciples += Count;
        }
    }

    public class Dismiss : FactionAction
    {
        public readonly string MemberId;

        public Dismiss(string actorId, string memberId) : base(actorId) => MemberId = memberId;

        public override string Key => ActionKeys.Dismiss;
        public override string Describe(WorldState w) => $"{w.Get(MemberId)?.FullName} 파문";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            var m = Member(w, own, MemberId);
            if (m == null) return ActionCheck.Fail("우리 세력 인물이 아닙니다");
            return m.Id == actor.Id ? ActionCheck.Fail("자기 자신은 파문할 수 없습니다") : ActionCheck.Pass;
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            var m = w.Get(MemberId);
            FactionOps.MoveToFaction(w, m, null, Role.None);
            m.LocationRegionId = own.HomeRegionId;
            Opinion.AddMemory(m, MemoryKind.Grudge, actor.Id, "파문", w.Month, 40);
            own.Fame = Math.Max(0, own.Fame - 3);
            Chronicle.Dismissed(w, own, m);
        }
    }

    public class DesignateHeir : FactionAction
    {
        public readonly string MemberId;

        public DesignateHeir(string actorId, string memberId) : base(actorId) => MemberId = memberId;

        public override string Key => ActionKeys.DesignateHeir;
        public override string Describe(WorldState w) => $"{w.Get(MemberId)?.FullName}을(를) 후계자로 지정";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            var m = Member(w, own, MemberId);
            if (m == null) return ActionCheck.Fail("우리 세력 인물이 아닙니다");
            if (m.Id == actor.Id) return ActionCheck.Fail("자기 자신입니다");
            if (m.Role == Role.Guest) return ActionCheck.Fail("객경은 후계자가 될 수 없습니다");
            return own.HeirId == m.Id ? ActionCheck.Fail("이미 후계자입니다") : ActionCheck.Pass;
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            var prev = w.Get(own.HeirId);
            if (prev != null && prev.IsAlive && prev.FactionId == own.Id)
                prev.Role = prev.FatherId == actor.Id || prev.MotherId == actor.Id ? Role.Family : Role.Disciple;
            var heir = w.Get(MemberId);
            heir.Role = Role.Heir;
            own.HeirId = heir.Id;
            foreach (var m in FactionOps.LivingMembers(w, own).ToList())
            {
                if (m.Id == actor.Id || m.Id == heir.Id || m.Id == actor.SpouseId || m.Role == Role.Guest) continue;
                if (m.AgeYears(w.Month) < SimConstants.AdultAge) continue;
                w.Opinions.Add(m.Id, actor.Id, -10);
                Opinion.AddMemory(m, MemoryKind.Grudge, actor.Id, "후계 탈락", w.Month, 20);
            }
            Chronicle.HeirDesignated(w, own, heir);
        }
    }
}
