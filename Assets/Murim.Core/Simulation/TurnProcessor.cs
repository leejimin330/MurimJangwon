using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Random;
using Murim.Core.World;

namespace Murim.Core.Simulation
{
    public class TurnContext
    {
        public WorldState World;
        public GameData Data;
        public GameRandom Rng;
        public CharacterGenerator Generator;
        /// <summary>테스트 전용: 게임 오버 뒤에도 세계를 계속 돌린다.</summary>
        public bool IgnoreGameOver;
    }

    public interface ITurnPhase
    {
        void Run(TurnContext ctx);
    }

    public class TurnProcessor
    {
        public IReadOnlyList<ITurnPhase> Phases { get; }

        public TurnProcessor(params ITurnPhase[] phases) => Phases = phases;

        /// <summary>[행동 단계들…, 사건, 출생, 사망, 후계, 연간(1월), 성향, 수입].</summary>
        public static TurnProcessor CreateDefault(IEnumerable<ITurnPhase> actionPhases) =>
            new TurnProcessor(actionPhases.Concat(new ITurnPhase[]
            {
                new Events.EventPhase(), new BirthPhase(), new DeathPhase(), new SuccessionPhase(), new YearlyPhase(), new AlignmentPhase(), new IncomePhase(),
            }).ToArray());

        public void Advance(TurnContext ctx)
        {
            if (ctx.World.GameOver && !ctx.IgnoreGameOver) return;
            foreach (var p in Phases) p.Run(ctx);
            ctx.World.Month++;
        }
    }
}
