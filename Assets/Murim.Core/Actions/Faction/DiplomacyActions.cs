using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    public class ProposeAlliance : FactionAction
    {
        public const int FameCost = 5;
        public readonly string TargetFactionId;

        public ProposeAlliance(string actorId, string targetFactionId) : base(actorId) => TargetFactionId = targetFactionId;

        public override string Key => ActionKeys.Alliance;
        public override string Describe(WorldState w) => $"{w.GetFaction(TargetFactionId)?.Name}에 동맹 제안";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            var c = OtherFaction(w, own, TargetFactionId, out _, out _);
            if (!c.Ok) return c;
            if (w.AreAllied(own.Id, TargetFactionId)) return ActionCheck.Fail("이미 동맹입니다");
            return own.Fame >= FameCost ? ActionCheck.Pass : ActionCheck.Fail("명성이 부족합니다");
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            var target = w.GetFaction(TargetFactionId);
            own.Fame -= FameCost;
            if (w.AllianceScore(own.Id, target.Id) >= SimConstants.AllianceThreshold)
            {
                w.Alliances.Add(WorldState.PairKey(own.Id, target.Id));
                Chronicle.Allied(w, own, target);
            }
            else Chronicle.AllianceRejected(w, own, target);
        }
    }

    public class SendGift : FactionAction
    {
        public const int Min = 30;
        public const int Max = 200;
        public readonly string TargetFactionId;
        public readonly int Amount;

        public SendGift(string actorId, string targetFactionId, int amount) : base(actorId)
        {
            TargetFactionId = targetFactionId;
            Amount = amount;
        }

        public override string Key => ActionKeys.Gift;
        public override string Describe(WorldState w) => $"{w.GetFaction(TargetFactionId)?.Name}에 은자 {Amount}냥 선물";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            if (Amount < Min || Amount > Max) return ActionCheck.Fail($"선물은 {Min}~{Max}냥입니다");
            var c = OtherFaction(w, own, TargetFactionId, out _, out _);
            return c.Ok ? NeedSilver(own, Amount) : c;
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            var target = w.GetFaction(TargetFactionId);
            var leader = FactionOps.Leader(w, target);
            own.Silver -= Amount;
            w.Opinions.Add(leader.Id, actor.Id, Amount / 10);
            Opinion.AddMemory(leader, MemoryKind.Debt, actor.Id, "선물", w.Month, 10 + Amount / 10);
            w.AddAllianceScore(own.Id, target.Id, 5);
        }
    }

    public class Insult : FactionAction
    {
        public readonly string TargetFactionId;

        public Insult(string actorId, string targetFactionId) : base(actorId) => TargetFactionId = targetFactionId;

        public override string Key => ActionKeys.Insult;
        public override string Describe(WorldState w) => $"{w.GetFaction(TargetFactionId)?.Name} 모욕";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own) =>
            OtherFaction(w, own, TargetFactionId, out _, out _);

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            var target = w.GetFaction(TargetFactionId);
            var leader = FactionOps.Leader(w, target);
            w.Opinions.Add(leader.Id, actor.Id, -30);
            Opinion.AddMemory(leader, MemoryKind.Grudge, actor.Id, "모욕", w.Month, 40);
            w.AddAllianceScore(own.Id, target.Id, -20);
            if (own.Alignment == Alignment.Unorthodox) own.Fame += 2;
            Chronicle.Insulted(w, actor, leader);
        }
    }
}
