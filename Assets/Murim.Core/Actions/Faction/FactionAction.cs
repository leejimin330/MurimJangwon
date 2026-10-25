using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.Rules;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    /// <summary>세력 장문인만 할 수 있는 행동의 공통 검사.</summary>
    public abstract class FactionAction : IGameAction
    {
        public string ActorId { get; }
        public abstract string Key { get; }

        protected FactionAction(string actorId) => ActorId = actorId;

        public ActionCheck CanExecute(WorldState w, GameData d)
        {
            var actor = w.Get(ActorId);
            if (actor == null || !actor.IsAlive) return ActionCheck.Fail("행동할 수 없는 인물입니다");
            var f = Wallet.LedFaction(w, actor);
            if (f == null) return ActionCheck.Fail("세력의 수장만 할 수 있습니다");
            return Check(w, d, actor, f);
        }

        public void Execute(WorldState w, GameData d, GameRandom rng)
        {
            var actor = w.Get(ActorId);
            Run(w, d, rng, actor, Wallet.LedFaction(w, actor));
        }

        public abstract string Describe(WorldState w);

        protected abstract ActionCheck Check(WorldState w, GameData d, Character actor, Faction own);
        protected abstract void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own);

        protected static ActionCheck NeedSilver(Faction f, int amount) =>
            f.Silver >= amount ? ActionCheck.Pass : ActionCheck.Fail("은자가 부족합니다");

        /// <summary>다른 살아 있는 세력과 그 장문인. 실패 사유를 돌려준다.</summary>
        protected static ActionCheck OtherFaction(WorldState w, Faction own, string targetId, out Faction target, out Character leader)
        {
            target = w.GetFaction(targetId);
            leader = null;
            if (target == null || target.IsDissolved) return ActionCheck.Fail("존재하지 않는 세력입니다");
            if (target.Id == own.Id) return ActionCheck.Fail("자기 세력입니다");
            leader = FactionOps.Leader(w, target);
            if (leader == null || !leader.IsAlive) return ActionCheck.Fail("상대 세력에 수장이 없습니다");
            return ActionCheck.Pass;
        }

        protected static Character Member(WorldState w, Faction f, string id)
        {
            var c = w.Get(id);
            return c != null && c.IsAlive && c.FactionId == f.Id ? c : null;
        }
    }
}
