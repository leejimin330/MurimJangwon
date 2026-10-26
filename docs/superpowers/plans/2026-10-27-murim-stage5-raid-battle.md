# 무림장원 5단계: 습격전 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**목표:** 습격 사건이 장원 격자 위 실시간 + 일시정지 전투가 되고, 결과가 사상자·문도·사건 갈래·연대기로 돌아온다.

**구조:** `Murim.Core.Battle`(단위·지도·길찾기·0.1초 틱 시뮬레이션·배치·결과 적용) + 사건 `raid` 효과 + `WorldState.PendingRaid`(저장 버전 5) + `BattleScreen`·`RaidPrompt`.

**기술 스택:** Unity 6000.3.23f1, C# 9, Newtonsoft.Json, NUnit (Unity EditMode)

**설계 문서:** `docs/superpowers/specs/2026-10-27-murim-stage5-raid-battle-design.md`

## 전역 제약

- 3·4단계 전역 제약을 따른다.
- 틱 0.1초, 최대 1800틱. 사기 붕괴: 두목 쓰러짐 또는 습격자 60% 쓰러짐. 쓰러진 인물 25% 사망, 나머지 `injured` 6개월.
- 단위 수치는 설계 3절 표를 그대로. 피해 공식은 `Duel` 상수(RealmBase 1.1, Roll 0.65~1.35)를 공유한다.
- 전투 난수는 습격 시드의 별도 `GameRandom`. 세계 난수는 시드를 뽑을 때 한 번만 쓴다.

## 검토할 점 (테스트로 고정할 것)

1. **장원에 담장을 둘러 정문까지 막힌 경우.** 정문은 비어 있으니 길은 있다. 그래도 길이 없으면 가장 가까운 칸으로 가서 예외 없이 끝나야 한다. 담당: Task 2 `Path_FullyBlocked_GoesNearest`.
2. **전투 도중 일시정지하고 명령만 바꾸는 경우.** 일시정지 중에는 틱이 돌지 않고 명령은 쌓인다. 담당: Task 3 `Orders_ApplyOnNextTick`.
3. **대기 습격이 있는 채로 저장·불러오기.** 같은 시드로 같은 결과. 담당: Task 5 `PendingRaid_SaveLoad_SameOutcome`.
4. **습격 도중 플레이어(장문인)가 죽는 경우.** 후계가 바로 이어지고 남은 사건은 정리된다. 담당: Task 5 `Raid_PlayerKilled_SuccessionRuns`.
5. **방어자가 하나도 없는 세력(문도 0, 수장만)이 습격을 받는 경우.** 수장 혼자 싸우고, 지면 갈래가 적용된다. 담당: Task 5 `Raid_LoneLeader`.

---

### Task 1: 전투 단위와 지도 (`BattleUnit`, `BattleMap`)
- Create `Battle/BattleUnit.cs`, `Battle/BattleMap.cs`; Test `BattleMapTests.cs`
```csharp
public enum BattleSide { Defender, Attacker }
public class BattleUnit { int Id; string CharacterId; string Name; BattleSide Side; bool IsLeader;
  float X, Y; int Hp, MaxHp; float Force; int MaxForce; int Realm; List<(CardDef,ArtDef,int)> Deck; int MovementMastery;
  float Speed, AttackInterval, Cooldown; double Modifier; bool Down, Fled; BattleOrder Order; List<(int x,int y)> Path; int TargetId = -1; }
public class BattleOrder { public bool Attack; public int TargetId; public int X, Y; }
public class BattleMap { int Width, Height; bool[,] Blocked; bool[,] Tower;
  public static BattleMap FromManor(WorldState w, GameData d, ManorState m);
  public bool Walkable(int x, int y); public List<(int x,int y)> FindPath(int sx, int sy, int tx, int ty); }
```
- [ ] 테스트: `FromManor_CompletedBuildingsBlock_TowerWalkable`, `Path_AroundWall`, `Path_NoCornerCutting`, `Path_FullyBlocked_GoesNearest` → 구현 → 통과 → 커밋

### Task 2: 시뮬레이션 (`BattleState`, `BattleSim`, `BattleSetup`)
- Create `Battle/BattleState.cs`, `Battle/BattleSim.cs`, `Battle/BattleSetup.cs`; Test `BattleSimTests.cs`
```csharp
public class BattleState { BattleMap Map; List<BattleUnit> Units; GameRandom Rng; int Tick; bool Over; bool DefendersWon; bool Routed;
  List<string> Log; public const float Dt = 0.1f; public const int MaxTicks = 1800; }
public static class BattleSim {
  public static void Step(BattleState s);                // 한 틱
  public static void RunToEnd(BattleState s);
  public static void Order(BattleState s, int unitId, BattleOrder o);
}
public static class BattleSetup {
  public static BattleState Create(WorldState w, GameData d, ulong seed, ManorState map, Faction defender, IList<Character> defenders, int defenderGrunts,
                                   IList<Character> attackers, Character attackerLeader, int attackerGrunts);
  public static List<Character> PickDefenders(WorldState w, Faction f, int max = 10);
  public static List<Character> PickAllies(WorldState w, Character leader, int count);
}
```
- [ ] 테스트: `SameSeed_SameResult`, `Orders_ApplyOnNextTick`, `RealmAdvantage_Wins`(1000번 중 90%↑), `Master_BeatsMob`, `Rout_WhenLeaderDown`, `Timeout_DefendersWin`, `Watchtower_RangeSix`, `MoveOrder_Arrives` → 구현 → 통과 → 커밋

### Task 3: 습격 효과와 대기 습격, 결과 적용
- Create `Battle/RaidState.cs`, `Battle/RaidResolver.cs`; Modify `Events/EffectDef.cs`(`RaidFx`), `Events/Effects.cs`, `Events/EventValidator.cs`(최상위만), `World/WorldState.cs`(`PendingRaid`, 버전 5), `Session/GameSession.cs`(`PendingRaid`, `CanEndTurn`, `StartBattle`, `FinishRaid`, `AutoResolveRaid`, `AutoResolveEvents`), `Persistence/SaveSystem.cs`, `Chronicle/Chronicle.cs`(`RaidResolved`), `events.json`(`faction_gang_raid`, `raid_bandits`, `raid_grudge`); Test `RaidTests.cs`
```csharp
public class RaidFx : FxArgs { string Attacker, Target; int Grunts = 6, Allies = 2; List<EffectDef> Win = new(), Lose = new(); }
public class RaidState { string EventId; int ChoiceIndex, EffectIndex; SortedDictionary<string,string> Roles; ulong Seed;
  List<string> AttackerIds; string AttackerLeaderId; int AttackerGrunts; List<Character> Spawned; }
public class RaidOutcome { bool DefendersWon; List<string> Dead, Injured; int DefenderGruntsLost, AttackerDowned; string Summary; string[] Lines; }
public static class RaidResolver { public static RaidOutcome Apply(EventContext ctx, RaidFx fx, BattleState b, Faction defender); }
```
- [ ] 테스트: `PlayerTarget_CreatesPendingRaid_NoBranchYet`, `FinishRaid_AppliesBranchCasualtiesChronicle`, `AiTarget_ResolvesImmediately`, `Validator_RaidMustBeTopLevel`, `PendingRaid_SaveLoad_SameOutcome`, `Raid_PlayerKilled_SuccessionRuns`, `Raid_LoneLeader`, `Shipped_RaidEventsValidate` → 구현 → 통과 → 커밋

### Task 4: 장기 시뮬레이션
- [ ] `LongSimulationTests`·`EventBalanceTests`·`DeterminismTests` 통과(습격 자동 해결 포함). 느리면 AI 습격의 전투 틱 상한·인원 상한을 조정하고 설계에 적는다. → 커밋

### Task 5: 화면 (`RaidPrompt`, `BattleScreen`)
- Create `UI/Panels/RaidPrompt.cs`, `UI/Screens/BattleScreen.cs`; Modify `UI/Screens/GameScreen.cs`(대기 습격이면 습격 창), `UI/Panels/TopBar.cs`("습격" 단추)
- 테스트용 진입점: `BattleScreen.SelectUnit(int id, bool add)`, `BattleScreen.ClickCell(int x, int y)`, `BattleScreen.ClickUnit(int id)`, `BattleScreen.Advance(float seconds)`.
- [ ] 구현 → `dotnet build dotnet/Murim.View.Check` → 커밋

### Task 6: Unity 확인 (`RunTask16`)과 마무리
- [ ] 시나리오(설계 9절 화면) → `tools/unity-run.sh Murim.Editor.UiScenario.RunTask16` 통과, 스냅샷 확인, 전체 Unity 테스트 통과, RunTask12~15 통과 → 설계 상태 → 커밋
