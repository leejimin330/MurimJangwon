using System.Collections.Generic;
using System.IO;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Generation;
using Murim.Core.Persistence;
using Murim.Core.Random;
using Murim.Core.Simulation;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Session
{
    public class LoadResult
    {
        public bool Ok;
        public string Error;
        public GameSession Session;
    }

    /// <summary>화면이 쓰는 단일 진입점: 행동 예약, 다음 달, 저장·불러오기.</summary>
    public class GameSession
    {
        public GameData Data { get; }
        public WorldState World { get; }
        public Character Player => World.Player;
        public string AutosaveDirectory { get; set; }
        /// <summary>테스트 전용.</summary>
        public bool IgnoreGameOver { get; set; }
        public IReadOnlyList<IGameAction> Pending => pending;

        readonly GameRandom rng;
        readonly List<IGameAction> pending = new List<IGameAction>();
        readonly TurnProcessor processor;

        public GameSession(GameData data, WorldState world, GameRandom random)
        {
            Data = data;
            World = world;
            rng = random;
            processor = TurnProcessor.CreateDefault(new ITurnPhase[]
            {
                new PlayerActionPhase(pending), new AiPhase(), new UnaffiliatedPhase(),
            });
        }

        public static GameSession NewGame(GameData d, NewGameOptions o)
        {
            var w = SimSetup.NewWorld(d, o, out var rng);
            return new GameSession(d, w, rng);
        }

        public int RemainingActions
        {
            get
            {
                var p = Player;
                if (p == null || !p.IsAlive || World.GameOver) return 0;
                if (p.BusyUntilMonth > World.Month) return 0;
                foreach (var a in pending)
                    if (a is IWholeMonthAction) return 0;
                return System.Math.Max(0, ActionBudget.Max(p) - pending.Count);
            }
        }

        public ActionCheck Queue(IGameAction a)
        {
            if (RemainingActions <= 0) return ActionCheck.Fail("이번 달에는 더 할 수 없습니다");
            if (a is IWholeMonthAction && pending.Count > 0) return ActionCheck.Fail("이동은 다른 행동과 함께 할 수 없습니다");
            var check = a.CanExecute(World, Data);
            if (!check.Ok) return check;
            pending.Add(a);
            return ActionCheck.Pass;
        }

        public void Unqueue(int index)
        {
            if (index >= 0 && index < pending.Count) pending.RemoveAt(index);
        }

        public void EndTurn()
        {
            var ctx = new TurnContext
            {
                World = World, Data = Data, Rng = rng, Generator = new CharacterGenerator(Data, rng), IgnoreGameOver = IgnoreGameOver,
            };
            processor.Advance(ctx);
            pending.Clear();
            if (AutosaveDirectory != null && World.Month % 12 == 0) Save(Path.Combine(AutosaveDirectory, "autosave.json"));
        }

        /// <summary>난수 상태까지 포함한 현재 세계의 JSON.</summary>
        public string Serialize()
        {
            World.RngState = rng.State;
            return SaveSystem.Serialize(World);
        }

        public SaveResult Save(string path)
        {
            World.RngState = rng.State;
            return SaveSystem.Write(World, path);
        }

        public static LoadResult Load(GameData d, string path)
        {
            var r = SaveSystem.Read(d, path);
            if (!r.Ok) return new LoadResult { Ok = false, Error = r.Error };
            var random = new GameRandom(0) { State = r.World.RngState };
            return new LoadResult { Ok = true, Session = new GameSession(d, r.World, random) };
        }
    }

    /// <summary>플레이어가 예약한 행동을 순서대로 실행한다. 그사이 조건이 바뀐 행동은 건너뛰고 기록한다.</summary>
    public class PlayerActionPhase : ITurnPhase
    {
        readonly List<IGameAction> pending;

        public PlayerActionPhase(List<IGameAction> pending) => this.pending = pending;

        public void Run(TurnContext ctx)
        {
            var w = ctx.World;
            foreach (var a in pending.ToArray())
            {
                var check = a.CanExecute(w, ctx.Data);
                if (check.Ok) a.Execute(w, ctx.Data, ctx.Rng);
                else
                {
                    var actor = w.Get(a.ActorId);
                    if (actor != null) Chronicle.ActionSkipped(w, actor, a.Describe(w), check.Reason);
                }
            }
            pending.Clear();
        }
    }
}
