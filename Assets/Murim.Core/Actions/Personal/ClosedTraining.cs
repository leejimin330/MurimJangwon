using Murim.Core.Data;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.Simulation;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    /// <summary>폐관수련: 3·6·12개월 동안 내공 ×3, 그동안 행동과 이야기꾼 사건이 멈춘다. 수장·개인 모두 할 수 있다.</summary>
    public class ClosedTraining : IGameAction, IWholeMonthAction
    {
        public string ActorId { get; }
        public readonly int Months;

        public ClosedTraining(string actorId, int months)
        {
            ActorId = actorId;
            Months = months;
        }

        public string Key => ActionKeys.ClosedTraining;
        public string Describe(WorldState w) => $"{Months}개월 폐관수련";

        public static bool ValidMonths(int months) => months == 3 || months == 6 || months == 12;

        public ActionCheck CanExecute(WorldState w, GameData d)
        {
            var actor = w.Get(ActorId);
            if (actor == null || !actor.IsAlive) return ActionCheck.Fail("행동할 수 없는 인물입니다");
            if (!ValidMonths(Months)) return ActionCheck.Fail("폐관은 3·6·12개월만 할 수 있습니다");
            if (actor.InTraining(w.Month)) return ActionCheck.Fail("이미 폐관 중입니다");
            return ActionCheck.Pass;
        }

        public void Execute(WorldState w, GameData d, GameRandom rng)
        {
            var actor = w.Get(ActorId);
            Cultivation.BeginTraining(w, actor, Months);
            if (BirthPhase.IsNotable(w, actor)) Chronicle.TrainingStarted(w, actor, Months);
        }
    }
}
