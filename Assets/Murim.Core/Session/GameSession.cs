using System.Collections.Generic;
using System.IO;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Data;
using Murim.Core.Events;
using Murim.Core.Generation;
using Murim.Core.Persistence;
using Murim.Core.Random;
using Murim.Core.Simulation;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Session
{
    public class EventOutcome
    {
        public bool Ok;
        public string Reason;
        /// <summary>선택 결과 문장 (창에 보여 준다).</summary>
        public string Text;
    }

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
        /// <summary>플레이어의 답을 기다리는 사건과 소식.</summary>
        public IReadOnlyList<PendingEvent> PendingEvents => World.Events.Pending;
        public bool CanEndTurn => World.Events.Pending.Count == 0;
        /// <summary>테스트·시뮬레이션 전용: 대기 사건을 AI 방식으로 바로 답한다.</summary>
        public bool AutoResolveEvents { get; set; }

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

        TurnContext NewTurnContext() => new TurnContext
        {
            World = World, Data = Data, Rng = rng, Generator = new CharacterGenerator(Data, rng), IgnoreGameOver = IgnoreGameOver,
        };

        /// <summary>대기 사건이 남아 있으면 진행하지 않는다 (AutoResolveEvents면 먼저 답한다).</summary>
        public void EndTurn()
        {
            if (!CanEndTurn)
            {
                if (!AutoResolveEvents) return;
                ResolveAllAuto();
            }
            processor.Advance(NewTurnContext());
            pending.Clear();
            if (AutoResolveEvents) ResolveAllAuto();
            if (AutosaveDirectory != null && World.Month % 12 == 0) Save(Path.Combine(AutosaveDirectory, "autosave.json"));
        }

        EventContext Rebuild(PendingEvent p)
        {
            if (p == null || p.IsNotice || !Data.Events.TryGetValue(p.EventId ?? "", out var def)) return null;
            var ctx = new EventContext { W = World, D = Data, Rng = rng, Def = def, Spawned = p.Spawned.ToList() };
            foreach (var kv in p.Roles) ctx.Roles[kv.Key] = kv.Value;
            return ctx;
        }

        static bool Stale(EventContext ctx) => ctx == null || ctx.Decider == null || !ctx.Decider.IsAlive;

        /// <summary>대기 사건의 문맥 (화면이 선택지 문구·초상화를 그릴 때 쓴다). 소식이면 null.</summary>
        public EventContext PendingContext(int pendingIndex) =>
            pendingIndex >= 0 && pendingIndex < World.Events.Pending.Count ? Rebuild(World.Events.Pending[pendingIndex]) : null;

        public ActionCheck CanChoose(int pendingIndex, int choiceIndex)
        {
            if (pendingIndex < 0 || pendingIndex >= World.Events.Pending.Count) return ActionCheck.Fail("없는 사건입니다");
            var p = World.Events.Pending[pendingIndex];
            var ctx = Rebuild(p);
            if (p.IsNotice || Stale(ctx)) return choiceIndex == 0 ? ActionCheck.Pass : ActionCheck.Fail("없는 선택지입니다");
            var why = EventResolver.CanChoose(ctx, choiceIndex);
            return why == null ? ActionCheck.Pass : ActionCheck.Fail(why);
        }

        public EventOutcome Choose(int pendingIndex, int choiceIndex)
        {
            var check = CanChoose(pendingIndex, choiceIndex);
            if (!check.Ok) return new EventOutcome { Ok = false, Reason = check.Reason };
            var p = World.Events.Pending[pendingIndex];
            var ctx = Rebuild(p);
            World.Events.Pending.RemoveAt(pendingIndex);
            if (p.IsNotice) return new EventOutcome { Ok = true };
            if (Stale(ctx)) return new EventOutcome { Ok = true, Text = "상황이 바뀌어 일이 흐지부지되었다." };

            var playerBefore = World.PlayerCharacterId;
            var text = EventResolver.Resolve(ctx, choiceIndex, p.Text);
            // 사건으로 수장이 죽었다면 다음 달을 기다리지 않고 바로 잇는다.
            new SuccessionPhase().Run(NewTurnContext());
            if (World.PlayerCharacterId != playerBefore || World.GameOver) World.Events.Pending.Clear();
            return new EventOutcome { Ok = true, Text = text };
        }

        /// <summary>대기 사건을 AI 방식으로 모두 답한다 (테스트·시뮬레이션용).</summary>
        public void ResolveAllAuto()
        {
            while (World.Events.Pending.Count > 0)
            {
                var ctx = Rebuild(World.Events.Pending[0]);
                int choice = Stale(ctx) ? 0 : EventAI.Choose(ctx);
                if (!Choose(0, choice).Ok) World.Events.Pending.RemoveAt(0);
            }
        }

        /// <summary>테스트·에디터 전용: 사건을 바로 일으킨다. 결정자 기본값은 플레이어.</summary>
        public ActionCheck ForceEvent(string eventId, IDictionary<string, string> fixedRoles = null)
        {
            if (!Data.Events.TryGetValue(eventId ?? "", out var e)) return ActionCheck.Fail("없는 사건입니다");
            var roles = fixedRoles != null ? new Dictionary<string, string>(fixedRoles) : new Dictionary<string, string>();
            if (fixedRoles == null && World.PlayerCharacterId != null) roles[e.Decider] = World.PlayerCharacterId;
            var ctx = RoleBinder.TryBind(World, Data, rng, e, roles, CandidateIndex.Build(World), false, ignoreCooldown: true);
            if (ctx == null) return ActionCheck.Fail("지금은 이 사건이 일어날 수 없습니다");
            if (ctx.IsPlayerDecider) EventPhase.Enqueue(ctx);
            else EventPhase.ResolveWorld(ctx, 0, out _);
            return ActionCheck.Pass;
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
            // 3단계 이전 저장: 모든 인물에게 무공·내공을 준다. 저장된 난수 상태에서 이어 쓰므로 결과가 같다.
            if (r.LoadedVersion < 3) Martial.ArtGranting.Migrate(r.World, d, random);
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
