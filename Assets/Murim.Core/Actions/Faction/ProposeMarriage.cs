using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.Story;
using Murim.Core.Text;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    /// <summary>다른 세력(또는 무소속) 인물과의 혼담. 장문인은 자기 세력 인물을, 그 외 인물은 자기 자신을 내세운다.</summary>
    public class ProposeMarriage : IGameAction
    {
        public const int Cost = 50;

        public string ActorId { get; }
        public string Key => ActionKeys.Marriage;
        public readonly string OurMemberId;
        public readonly string TheirMemberId;
        /// <summary>true면 상대가 우리 쪽으로 온다.</summary>
        public readonly bool SpouseJoinsUs;

        public ProposeMarriage(string actorId, string ourMemberId, string theirMemberId, bool spouseJoinsUs)
        {
            ActorId = actorId;
            OurMemberId = ourMemberId;
            TheirMemberId = theirMemberId;
            SpouseJoinsUs = spouseJoinsUs;
        }

        public string Describe(WorldState w) =>
            $"{w.Get(OurMemberId)?.FullName}과(와) {w.Get(TheirMemberId)?.FullName}의 혼담";

        public ActionCheck CanExecute(WorldState w, GameData d)
        {
            var actor = w.Get(ActorId);
            if (actor == null || !actor.IsAlive) return ActionCheck.Fail("행동할 수 없는 인물입니다");
            var own = Wallet.LedFaction(w, actor);
            var ours = w.Get(OurMemberId);
            var theirs = w.Get(TheirMemberId);
            if (ours == null || theirs == null) return ActionCheck.Fail("존재하지 않는 인물입니다");
            if (own == null && ours.Id != actor.Id) return ActionCheck.Fail("자기 혼사만 정할 수 있습니다");
            if (own != null && ours.FactionId != own.Id) return ActionCheck.Fail("우리 세력 인물이 아닙니다");
            if (theirs.FactionId != null && theirs.FactionId == ours.FactionId)
                return ActionCheck.Fail("같은 세력끼리는 내부 혼인으로 정합니다");
            if (own != null && ours.Id == own.LeaderId && !SpouseJoinsUs)
                return ActionCheck.Fail("수장은 세력을 떠날 수 없습니다");
            var tf = w.GetFaction(theirs.FactionId);
            if (tf != null && (tf.IsDissolved || FactionOps.Leader(w, tf) == null)) return ActionCheck.Fail("상대 세력에 수장이 없습니다");
            if (tf != null && theirs.Id == tf.LeaderId && SpouseJoinsUs) return ActionCheck.Fail("상대 수장은 세력을 떠날 수 없습니다");
            if (Wallet.Silver(w, actor) < Cost) return ActionCheck.Fail("은자가 부족합니다");
            return MarriageRules.Eligible(w, ours, theirs);
        }

        public void Execute(WorldState w, GameData d, GameRandom rng)
        {
            var actor = w.Get(ActorId);
            var own = Wallet.LedFaction(w, actor);
            var ours = w.Get(OurMemberId);
            var theirs = w.Get(TheirMemberId);
            var tf = w.GetFaction(theirs.FactionId);
            Wallet.AddSilver(w, actor, -Cost);

            Character targetHead;
            bool accepted;
            if (tf != null)
            {
                targetHead = FactionOps.Leader(w, tf);
                int score = MarriageRules.AcceptanceScore(w, d, actor, targetHead, Wallet.Fame(w, actor), tf.Fame,
                    own?.Alignment, tf.Alignment, tf.Type == FactionType.Clan);
                accepted = score > 0;
            }
            else
            {
                targetHead = theirs;
                accepted = Opinion.Of(w, d, theirs.Id, ours.Id) >= SimConstants.UnaffiliatedMarriageOpinion;
            }

            if (!accepted)
            {
                Opinion.AddMemory(actor, MemoryKind.Grudge, targetHead.Id, "혼담 거절", w.Month, SimConstants.MarriageRejectGrudge);
                Chronicle.MarriageRejected(w, actor, targetHead);
                return;
            }

            new CharacterGenerator(d, rng).Marry(w, ours, theirs);
            if (SpouseJoinsUs)
            {
                FactionOps.MoveToFaction(w, theirs, ours.FactionId, ours.FactionId == null ? Role.None : Role.Family);
                theirs.LocationRegionId = ours.LocationRegionId;
            }
            else
            {
                FactionOps.MoveToFaction(w, ours, theirs.FactionId, theirs.FactionId == null ? Role.None : Role.Family);
                ours.LocationRegionId = theirs.LocationRegionId;
            }
            if (targetHead.Id != actor.Id)
            {
                w.Opinions.Add(targetHead.Id, actor.Id, SimConstants.MarriageOpinionGain);
                w.Opinions.Add(actor.Id, targetHead.Id, SimConstants.MarriageOpinionGain);
            }
            if (own != null && tf != null) w.AddAllianceScore(own.Id, tf.Id, SimConstants.MarriageAllianceGain);
            Chronicle.Married(w, ours, theirs);
        }
    }

    public class ArrangeInternalMarriage : FactionAction
    {
        public readonly string AId;
        public readonly string BId;

        public ArrangeInternalMarriage(string actorId, string aId, string bId) : base(actorId)
        {
            AId = aId;
            BId = bId;
        }

        public override string Key => ActionKeys.InternalMarriage;
        public override string Describe(WorldState w) => $"{w.Get(AId)?.FullName}과(와) {w.Get(BId)?.FullName}의 혼인";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            var a = Member(w, own, AId);
            var b = Member(w, own, BId);
            if (a == null || b == null) return ActionCheck.Fail("우리 세력 인물이 아닙니다");
            return MarriageRules.Eligible(w, a, b);
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            var a = w.Get(AId);
            var b = w.Get(BId);
            new CharacterGenerator(d, rng).Marry(w, a, b);
            Chronicle.Married(w, a, b);
        }
    }
}
