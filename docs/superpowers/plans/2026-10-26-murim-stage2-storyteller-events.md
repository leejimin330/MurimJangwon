# 무림장원 2단계: 이야기꾼·사건 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**목표:** JSON으로 정의한 사건이 원한·은혜·특성을 읽어 일어나고, 이야기꾼이 플레이어에게 오는 사건의 빈도를 조절하며, 플레이어는 선택 창으로 답한다.

**구조:**
- 순수 C# `Murim.Core.Events` 네임스페이스에 사건 정의, 역할 묶기, 조건, 효과, 본문, AI, 이야기꾼, `EventPhase`를 둔다.
- 사건 상태는 `WorldState.Events`(`EventState`)에 저장한다. 저장 버전은 2이고, 버전 1 저장도 받아들인다.
- 화면에는 `EventDialog`를 두고, 상단 바·새 게임·인물 패널·연대기를 조금씩 고친다.

**기술 스택:**
- Unity 6000.3.23f1, C# 9, Newtonsoft.Json
- NUnit: `dotnet test`와 Unity EditMode 둘 다
- 화면: uGUI를 코드로 생성

**설계 문서:** `docs/superpowers/specs/2026-10-26-murim-stage2-storyteller-events-design.md`

## 전역 제약

- `Murim.Core`는 UnityEngine을 참조하지 않는다.
- 난수는 `GameRandom` 하나에서만 나온다.
- 사전은 `SortedDictionary(StringComparer.Ordinal)`를 쓰고, 반복은 Ordinal 순서로 한다.
- Core는 예외를 던지지 않는다. 실패는 `ActionCheck.Fail("한국어 사유")`로 돌려준다.
- 화면에 나오는 문장은 한국어이고, 조사는 `Josa`로 붙인다. "이(가)" 같은 문자열이 화면에 남으면 안 된다.
- 이야기꾼 수치(설계 문서 4절):
  - 평온: 2/60/0.25/18
  - 균형: 4/100/0.35/10
  - 파란만장: 6/160/0.45/5
  - 위기 기준은 `tension ≥ 20`이다.
  - 플레이어 사건은 한 달에 최대 2개다. 후속 사건을 포함하면 최대 3개다.
- 세계 사건은 한 달에 4번 시도한다. 결정자 표본은 최대 24명이다. 소식은 한 달에 최대 3개다.
- 모욕 쿨다운은 24개월이다.

## 검토할 점 (테스트로 고정할 것)

1. **답하지 않은 사건이 있는 상태로 저장했다가 불러오는 경우.** 같은 창이 다시 뜨고, 같은 선택은 같은 결과를 내야 한다. 담당: Task 6 `Pending_SaveLoad_SameOutcome`.
2. **사건 도중 플레이어가 결투나 역병으로 죽는 경우.** 남은 대기 사건은 버리고, 후계로 바로 이어져야 한다(다음 달까지 기다리지 않는다). 담당: Task 6 `Choose_PlayerDies_SuccessionRunsImmediately`.
3. **역할로 묶인 인물이 대기 중에 상태가 바뀌는 경우.** 예약된 후속 사건의 대상이 그 사이에 죽으면 사건을 조용히 버리고, 예외 없이 지나가야 한다. 담당: Task 6 `Scheduled_DeadRole_Dropped`.
4. **새로 만든 인물(산적 등)이 세계에 남는 경우.** 선택 뒤 영입하지 않았다면 `World.Characters`에 남지 않아야 한다. 담당: Task 5 `Spawned_NotRecruited_Removed`.
5. **버전 1 저장(1단계)을 불러오는 경우.** 이야기꾼은 "균형", 표식과 대기 사건은 비어 있는 상태로 열려야 한다. 담당: Task 1 `Load_Version1_Accepted`.

---

### Task 1: 사건 상태와 저장 버전 2

**Files:**
- Create: `Assets/Murim.Core/World/EventState.cs`
- Modify:
  - `World/WorldState.cs`: `CurrentSaveVersion = 2`, `public EventState Events = new EventState();`
  - `World/Character.cs`: `Flags`
  - `World/Memory.cs`: `Inherited`
  - `Rules/Opinion.cs`: 대물림한 기억에 `Inherited = true`
  - `Persistence/SaveSystem.cs`: 버전 1과 2를 받는다. 1이면 `SaveVersion = 2`로 올린다.
  - `Simulation/Phases/LifePhases.cs` `YearlyPhase`: 만료된 표식과 쿨다운을 지운다.
- Test: `Assets/Tests/Murim.Core.Tests/EventStateTests.cs`

**Interfaces:**
- Produces:
```csharp
public class EventState {
  public string StorytellerId = "balanced"; public int Tension; public int LastCrisisMonth = -9999;
  public List<PendingEvent> Pending = new();  public List<ScheduledEvent> Scheduled = new();
  public SortedDictionary<string,int> Cooldowns (Ordinal);   // 키 → 다시 가능해지는 월
  public bool IsCooling(string key, int month); public void SetCooldown(string key, int untilMonth);
  public void PruneExpired(int month);
}
public class PendingEvent { string EventId; SortedDictionary<string,string> Roles; bool IsNotice; string Title;
  string Text; int Month; List<Character> Spawned = new(); }
public class ScheduledEvent { string EventId; int Month; SortedDictionary<string,string> Roles; }
// Character
public SortedDictionary<string,int> Flags (Ordinal);  // id → 만료 월 (0 = 영구)
public bool HasFlag(string id, int month);  public void SetFlag(string id, int month, int months); public void ClearFlag(string id);
// Memory
public bool Inherited;
```

- [ ] 테스트를 쓴다. 기대 결과는 다음과 같다.
  - `Flag_ExpiresAtMonth`: `SetFlag("injured", 100, 6)`이면 `HasFlag(.., 105)`는 참, `(.., 106)`은 거짓. `months=0`이면 영구.
  - `YearlyPhase_RemovesExpiredFlagsAndCooldowns`
  - `InheritMemories_MarksInherited`
  - `Load_Version1_Accepted`: 저장 파일 JSON에서 `saveVersion`을 1로 바꾸고 `events` 키를 지운 뒤 불러온다. 결과는 Ok, `Events.StorytellerId == "balanced"`, `SaveVersion == 2`.
  - `Load_Version3_Rejected`
- [ ] `dotnet test --filter EventStateTests`가 실패하는지 확인한다.
- [ ] 구현한다.
- [ ] `dotnet test`를 돌려 기존 236개를 포함해 모두 통과하는지 확인한다.
- [ ] 커밋: `feat: 사건 상태, 표식, 저장 버전 2`

### Task 2: 사건 정의, 불러오기, 검증

**Files:**
- Create:
  - `Assets/Murim.Core/Events/EventDef.cs`
  - `Events/EventValidator.cs`
- Modify:
  - `Data/GameData.cs`: `Events`, `Flags`, `Storytellers`
  - `Data/DataLoader.cs`: `events.json`(객체 `{flags:[], events:[]}`)와 `storytellers.json`(배열)
  - `Data/DataValidator.cs`: `EventValidator.Validate` 결과를 합친다.
  - `Tests/Support/TestJson.cs`: 최소 사건 1개와 이야기꾼 1개를 추가한다.
- Create (빈 데이터):
  - `StreamingAssets/Data/events.json`: 사건 하나(`street_quarrel`)
  - `StreamingAssets/Data/storytellers.json`: 세 이야기꾼
- Test: `EventDefTests.cs`

**Interfaces:**
- Produces (모두 `Murim.Core.Events`. JSON은 camelCase이고, 알 수 없는 키는 `[JsonExtensionData] Unknown`에 모은다):
```csharp
class EventDef { Id, Name, Category, float Weight=10, int Tension, CooldownMonths, GlobalCooldownMonths, string Decider,
  bool FollowupOnly, PlayerOnly, WorldOnly, int Importance=1, int Month /*1~12, 0=언제든*/, string Text,
  Dictionary<string, RoleDef> Roles, List<ChoiceDef> Choices; List<string> BindOrder /*검증기가 채움: 뿌리 → 의존*/ }
class Conditions { bool? Alive; bool IncludeDead; int? AgeMin, AgeMax; Sex? Sex; string[] TraitsAll, TraitsAny, TraitsNone;
  Role[] Roles; Alignment[] Alignment; bool? Married; int? RealmMin, RealmMax, SilverMin, FameMin, ChivalryMin, ChivalryMax,
  FactionSizeMin, RegionStabilityMin, RegionStabilityMax; string HasFlag, NotFlag; MemoryCond Memory; OpinionCond OpinionOf;
  double? Chance; IDictionary<string,JToken> Unknown }
class RoleDef : Conditions { string Pick, Of; MemoryKind MemoryKind = Grudge; SpawnDef Spawn }
class SpawnDef { int AgeMin=20, AgeMax=45; Sex? Sex; int RealmMin, RealmMax; int StatBonus; string Epithet }
class MemoryCond { MemoryKind Kind; int MinStrength; string Target; bool? Inherited }
class OpinionCond { string Role; int? Min, Max }
class ChoiceDef { Text, Result, Hint; Dictionary<string, Conditions> Requires; float Ai; Dictionary<string,float> AiTraits; List<EffectDef> Effects }
class EffectDef { /* 효과마다 null 가능한 필드 하나 */ Opinion, Memory, WeakenMemory, RemoveMemory, Silver, Fame, Chivalry,
  Influence, Stability, Unnamed, Stat, Kill, Flag, ClearFlag, Relation, SetAlignment, Recruit, Leave, Move, AllianceScore,
  Usurp, Duel, Contest, Chance, Schedule, Chronicle; IDictionary<string,JToken> Unknown; string Kind /*null 아닌 필드 이름*/ }
class FlagDef { Id, Name, Description; bool Visible; int YearlyChivalry; int DuelModifier }
class StorytellerDef { Id, Name, Description; int TensionGain, TensionMax; double BaseChance; int CrisisGapMonths;
  Dictionary<string,float> CategoryMultipliers }
static class EventPicks { const string AnyAdult="anyAdult", Leader="leader", GrudgeHolder="grudgeHolder", DebtHolder="debtHolder",
  MemoryTarget="memoryTarget", Spouse="spouse", Child="child", LeaderOf="leaderOf", SameFaction="sameFaction",
  SameRegion="sameRegion", RivalLeader="rivalLeader", DemonicNeighborLeader="demonicNeighborLeader", New="new";
  static bool IsRoot(string pick) }
static class EventValidator { static List<string> Validate(GameData d) }
```
- 오류 문장은 `events.json: 사건 '{id}' …` 형식으로 쓴다. 검사 항목은 설계 문서 8.2와 같고, 다음을 더 확인한다.
  - 뿌리(절대 `pick`) 역할이 하나 이상 있다.
  - `of`가 순환하지 않는다.
  - 상대 `pick`에는 `of`가 있다.
  - `new` 역할에는 `spawn`이 있다.
  - 효과 하나에 키가 정확히 하나다.

- [ ] 테스트를 쓴다.
  - `Load_ShippedEvents_Ok`
  - `UnknownEffectKey_Error`: 오류에 `"알 수 없는 효과 'explode'"`가 들어 있다.
  - `UnknownRoleInText_Error`: `"본문의 '{ghost}'"`
  - `AllChoicesRequire_Error`: `"항상 고를 수 있는 선택지가 없습니다"`
  - `CyclicOf_Error`, `MissingDecider_Error`, `UnknownTrait_Error`, `ScheduleUnknownEvent_Error`
  - `FollowupNeverScheduled_Error`
  - `BindOrder_RootsFirst`
- [ ] 실패하는지 확인 → 구현 → 테스트 통과를 확인한다.
- [ ] 커밋: `feat: 사건 정의 형식과 데이터 검증`

### Task 3: 본문 템플릿

**Files:** Create `Events/EventText.cs`; Test `EventTextTests.cs`

**Interfaces:**
- Consumes: `EventContext.Get(role)` (Task 4). Task 3은 본문 치환이 쓰는 최소한의 `EventContext` 뼈대(`W`, `D`, `Roles`, `Spawned`, `Get`)를 먼저 만든다.
- Produces:
  - `static string EventText.Format(string template, EventContext ctx)`
  - `static IEnumerable<string> EventText.RoleRefs(string template)`: 검증기가 쓴다.
  - `static bool EventText.IsKnownToken(string token)`
- 표기와 결과(설계 문서 3.6):
  - `{r}`: 이름
  - `{r.title}`: 새로 만든 인물이면 `Epithet + " " + 이름`, 아니면 `Titles.Full`
  - `{r.faction}`: 세력 이름, 없으면 "강호"
  - `{r.region}`: 지역 이름
  - `:이 :을 :은 :와 :으로`: 각각 `Josa.IGa / EulReul / EunNeun / WaGwa / EuroRo`로 붙인다.

- [ ] 테스트를 쓴다.
  - `Name_WithJosa`: "장삼"이면 `{a:이}` → "장삼이". "장사"이면 "장사가".
  - `Title_Spawned_UsesEpithet`
  - `Faction_None_Gangho`
  - `UnknownToken_LeftForValidator`: `IsKnownToken("a.foo")`는 거짓.
- [ ] 실패하는지 확인 → 구현 → 통과를 확인한다 → 커밋: `feat: 사건 본문 템플릿`

### Task 4: 조건과 역할 묶기

**Files:**
- Create:
  - `Events/EventContext.cs`
  - `Events/Conditions.cs` (`ConditionCheck`)
  - `Events/RoleBinder.cs`
  - `Events/CandidateIndex.cs`
- Test: `ConditionTests.cs`, `RoleBinderTests.cs`

**Interfaces:**
- Produces:
```csharp
class EventContext { WorldState W; GameData D; GameRandom Rng; EventDef Def;
  SortedDictionary<string,string> Roles; List<Character> Spawned; HashSet<string> Keep; List<string> Log;
  Character Get(string role) /* 세계 → Spawned 순으로 찾음 */; Character Decider => Get(Def.Decider); bool IsPlayerDecider; }
static class ConditionCheck {
  /// null이면 통과. 실패하면 화면에 보일 사유 ("은자 200냥이 필요합니다", "다혈질만 고를 수 있습니다", "조건이 맞지 않습니다").
  static string Why(Conditions c, Character ch, EventContext ctx);
  static string RegionOf(WorldState w, Character c);   // LocationRegionId ?? 세력 본거지
}
class CandidateIndex { static CandidateIndex Build(WorldState w); List<Character> Adults, Leaders, GrudgeHolders, DebtHolders;
  IReadOnlyList<Character> InRegion(string regionId); }
static class RoleBinder {
  /// fixedRoles의 역할은 그 인물로 고정. sampleRoot면 첫 뿌리 역할 후보를 최대 24명 표본으로 시도.
  static EventContext TryBind(WorldState w, GameData d, GameRandom rng, EventDef e, IDictionary<string,string> fixedRoles,
     CandidateIndex index, bool sampleRoot, string excludeDeciderId = null);
}
```
- 묶기 알고리즘:
  1. `BindOrder`대로 묶는다.
  2. 후보 = 뿌리 `pick`이면 인덱스 목록, 상대 `pick`이면 `of` 인물 기준 목록. 여기서 살아 있음, 조건 통과, 다른 역할에 이미 쓰이지 않음으로 거른다.
  3. 고정 역할이 이 역할에 직접 의존하면, 그 고정 인물을 상대 후보로 갖는 후보만 남긴다.
  4. 후보 순서는 `rng`로 섞는다. 첫 뿌리만 앞에서부터 차례로 시도(역추적)하고, 나머지 역할은 첫 후보로 정한다.
  5. `new`는 마지막에 `CharacterGenerator.Create`로 만든다. 위치는 `of` 역할(없으면 결정자)의 지역이다. 만든 뒤 `World.Characters`에서 빼서 `Spawned`에 넣는다.
- `memoryTarget`은 `of` 인물이 가진 기억 가운데 `MemoryKind`가 같고 `of` 역할의 `memory` 조건(강도, 대물림)을 만족하는 기억들의 대상이다. 그 대상 중 하나를 고른다.
- `Event.Month`가 있으면 `(w.Month % 12) + 1 == Month`일 때만 성립한다.

- [ ] 테스트를 쓴다.
  - 조건: `Age`, `Traits`, `Roles_NoneMeansUnaffiliated`, `Alignment_UnaffiliatedByChivalry`, `Silver_UsesWallet`, `HasFlag`, `Memory_MinStrengthAndInherited`, `OpinionOf`, `RegionStability`, `Chance_Zero_Fails`, `Why_SilverReason`(→ "은자 200냥이 필요합니다"), `Why_TraitReason`(→ "다혈질만 고를 수 있습니다")
  - 역할: `MemoryTarget_PicksGrudgeTarget`, `FixedDecider_RelativePick_FindsHolder`(플레이어를 원수로 둔 인물이 하나뿐이면 그를 찾는다), `NoCandidate_ReturnsNull`, `SameSeed_SameBinding`, `RolesDistinct`, `New_SpawnedNotInWorld`, `Month_Restricts`
- [ ] 실패하는지 확인 → 구현 → 통과를 확인한다 → 커밋: `feat: 사건 조건과 역할 묶기`

### Task 5: 효과, AI 선택, 사건 해결

**Files:**
- Create:
  - `Events/Effects.cs`
  - `Events/EventAI.cs`
  - `Events/EventResolver.cs`
- Modify:
  - `Chronicle/Chronicle.cs`:
    - `Died`: 처음 보는 원인은 `Josa.EuroRo(cause)`로 쓴다.
    - `AlignmentShifted(w, f, Alignment from)`: 마도로 가거나 마도에서 나올 때의 문장을 넣는다.
    - `EventLine(w, kind, importance, text, ids)` 추가
  - `Simulation/Phases/LifePhases.cs` `AlignmentPhase`: 호출을 바뀐 서명에 맞춘다.
  - `YearlyPhase`: 표식의 `YearlyChivalry`를 반영한다.
- Test: `EffectTests.cs`, `EventResolverTests.cs`

**Interfaces:**
- Produces:
```csharp
static class Effects { static void Apply(IList<EffectDef> list, EventContext ctx); }
static class EventAI { static int Choose(EventContext ctx); }   // 점수 = Ai + Σ AiTraits[결정자 특성] + rng 0~5, 동점이면 앞
static class EventResolver {
  static string CanChoose(EventContext ctx, int choiceIndex);   // null = 가능
  /// 효과 적용, 연대기 한 줄 기록, 결정자 쿨다운, Keep 안 된 Spawned 정리. 결과 문장(본문 제외)을 돌려준다.
  static string Resolve(EventContext ctx, int choiceIndex);
  static void MarkFired(EventContext ctx);                       // 쿨다운 + (위기면) LastCrisisMonth
}
```
- 효과의 의미는 설계 문서 3.4를 따르고, 다음을 덧붙인다.
  - `usurp {who}`: `who`가 자기 세력 수장이 된다. 옛 수장은 무소속이 되고, `who`에 대해 "하극상" 원한(강도 60)을 가진다. 연대기에 남긴다.
  - `child` pick: 살아 있는 자식이다.
  - `contest`: `{ who, stat, against?: int, againstRole?, againstStat?, win[], lose[] }`
  - `duel`: 점수에 양쪽 표식의 `DuelModifier` 합을 더한다.
  - `kill`: `DeathPhase.Kill`을 그대로 쓴다.
  - `schedule`: 묶인 역할 중 `roles`에 적힌 것만 넘긴다. 예약 월은 `w.Month + rng.Next(min, max+1)`이다.
- 연대기: `Kind = "Event:" + id`, 문장은 `본문 — 결과`이고 `Log`에 쌓인 문장을 뒤에 붙인다. 관련 id는 묶인 인물들과 결정자 세력이다.

- [ ] 테스트를 쓴다.
  - 효과마다 하나씩: `Opinion_Adds`, `Memory_AddsAndNemesis`, `WeakenMemory`, `RemoveMemory`, `Silver_ClampsAtZero`, `Influence_Gain`, `Stability_Clamped0to100`, `Unnamed_NotNegative`, `Stat_Clamped1to20`, `Kill_InheritsMemories`, `Flag_SetAndClear`, `Relation_BothSides`, `SetAlignment_DemonicChronicle`, `Recruit_SpawnedKept`, `Leave`, `Move_ToNeighbor`, `Usurp_SwapsLeader`, `Duel_DeathChance1_LoserDies`, `Duel_DeathChance0_NoDeath`, `Duel_InjuredPenalty`, `Contest_Branches`, `Chance_Branches`, `Schedule_AddsWithRoles`, `DeadRole_EffectSkipped`
  - 해결: `Spawned_NotRecruited_Removed`, `Resolve_WritesChronicleWithResult`, `Resolve_SetsCooldown`, `CanChoose_RequiresReason`, `AI_PrefersTraitWeighted`(다혈질 결정자는 "칼을 뽑는다"를 고른다)
- [ ] 실패하는지 확인 → 구현 → 통과를 확인한다 → 커밋: `feat: 사건 효과와 해결`

### Task 6: 이야기꾼, 사건 단계, 세션 API

**Files:**
- Create:
  - `Events/Storyteller.cs`
  - `Events/EventPhase.cs`
- Modify:
  - `Session/GameSession.cs`
  - `Session/NewGameOptions.cs`: `StorytellerId = "balanced"`
  - `Generation/ScenarioSetup.cs` `SimSetup.NewWorld`: `w.Events.StorytellerId`를 설정한다.
  - `Simulation/TurnProcessor.cs`: 기본 단계 목록의 행동 단계 뒤에 `EventPhase`를 넣는다(설계 문서 2.2).
  - 기존 테스트에서 `EndTurn`을 여러 번 부르는 곳에 `AutoResolveEvents = true`를 켠다: `GameSessionTests`, `LongSimulationTests`, `SaveSystemTests`.
- Test: `StorytellerTests.cs`, `EventPhaseTests.cs`, `GameSessionEventTests.cs`

**Interfaces:**
- Produces:
```csharp
static class Storyteller {
  static void Tick(EventState s, StorytellerDef def);   // 긴장도 += gain, 상한 적용
  static bool IsCandidate(EventDef e, EventState s, StorytellerDef def, int month);   // tension ≤ 긴장도, 위기 간격
  static float Weight(EventDef e, StorytellerDef def);
  static List<EventContext> PickForPlayer(TurnContext t, CandidateIndex index, int alreadyThisMonth); // 0~2개
}
class EventPhase : ITurnPhase { void Run(TurnContext ctx); }   // 후속 → 세계 4회 → 플레이어
public class EventOutcome { public bool Ok; public string Reason; public string Text; }
// GameSession
IReadOnlyList<PendingEvent> PendingEvents; bool CanEndTurn; bool AutoResolveEvents { get; set; }   // 테스트·시뮬레이션용
ActionCheck CanChoose(int pendingIndex, int choiceIndex); EventOutcome Choose(int pendingIndex, int choiceIndex);
ActionCheck ForceEvent(string eventId, IDictionary<string,string> fixedRoles = null);   // 결정자 기본값 = 플레이어
```
- `Choose`의 순서:
  1. 대기 사건에서 `EventContext`를 다시 만든다(묶인 인물이 죽었으면 소식으로 바꿔 끝낸다).
  2. `Resolve`한다.
  3. 대기 목록에서 지운다.
  4. `SuccessionPhase`를 바로 한 번 돌린다.
  5. 플레이어가 바뀌었거나 게임 오버면 남은 대기 사건을 모두 지운다.
- 소식은 `Choose(i, 0)`으로 지우기만 한다.
- `EndTurn`은 `!CanEndTurn && !AutoResolveEvents`면 아무것도 하지 않는다. `AutoResolveEvents`가 켜져 있으면 진행한 뒤 대기 사건을 `EventAI`로 모두 해결한다.

- [ ] 테스트를 쓴다.
  - `Tick_CapsAtMax`, `Crisis_RespectsGap`, `Weight_CategoryMultiplier`
  - `MaxTwoStorytellerEventsPerMonth`, `FollowupCountsTowardThree`
  - `WorldEvents_ExcludePlayerAsDecider`
  - `Notice_WhenPlayerInvolved_Importance1`, `Notice_CappedAtThree`
  - `EndTurn_BlockedWhilePending`, `Choose_RemovesAndReturnsText`, `Choose_DisabledReason`
  - `Pending_SaveLoad_SameOutcome`, `Choose_PlayerDies_SuccessionRunsImmediately`, `Scheduled_DeadRole_Dropped`
  - `ForceEvent_NoBinding_Fails`, `Deterministic_SameSeed_SameEvents`(10년, `AutoResolveEvents`)
- [ ] 실패하는지 확인 → 구현 → 전체 `dotnet test` 통과를 확인한다 → 커밋: `feat: 이야기꾼과 사건 단계, 세션 API`

### Task 7: 모욕 쿨다운과 청부 표식

**Files:**
- Modify:
  - `Actions/Faction/DiplomacyActions.cs` `Insult`
  - `Simulation/FactionAI.cs`: 모욕 대상은 쿨다운이 지난 곳만
  - `Actions/Personal/PersonalActions.cs` `PersonalQuest`: 청부 시 `recent_contract` 24개월
- Test: `FactionActionTests.cs`, `FactionAITests.cs`, `PersonalActionTests.cs`에 추가

**Interfaces:**
- Produces: `public const int Insult.CooldownMonths = 24;`, `static string Insult.CooldownKey(string fromFactionId, string toFactionId)` → `"insult|a|b"`

- [ ] 테스트를 쓴다.
  - `Insult_Twice_Within24Months_Fails`(사유: "2년 안에 다시 모욕할 수 없습니다")
  - `Insult_After24Months_Ok`
  - `AI_TenYears_InsultsPlayerAtMost5`(출하 데이터, minor_sect, henan, seed 42)
  - `Contract_SetsRecentContractFlag`
- [ ] 실패하는지 확인 → 구현 → 통과를 확인한다 → 커밋: `fix: 같은 세력 반복 모욕 방지`

### Task 8: 사건 41개와 이야기꾼 데이터, 균형 검증

**Files:**
- Modify:
  - `StreamingAssets/Data/events.json`: 표식 8개와 설계 문서 6절의 사건 41개
  - `storytellers.json`
  - 필요하면 `traits.json`
- Test: `EventBalanceTests.cs` (`[Category("Long")]`), `ShippedDataTests.cs`에 추가

- 사건 id는 범주 접두어를 붙인 영어 snake_case로 짓는다. 예: `trait_insult_rage`, `grudge_duel_challenge`, `grudge_ambush`, `grudge_enemy_child`, `debt_repay`, `demonic_manual`, `wanderer_bandits`.
- 각 사건의 결정자·역할·선택지는 설계 문서 6절의 줄을 그대로 옮긴다. 거기 없는 수치는 같은 범주의 기존 사건 수치를 따른다.

- [ ] 테스트를 쓴다.
  - `Shipped_41Events_8Flags_3Storytellers`
  - `EveryEvent_TextFormatsWithoutBraces`: 모든 사건을 바인딩 가능한 세계에서 만들어 본다. 본문과 결과에 `{`, `(가)`, `(를)`가 없어야 한다.
  - `PlayerEventsPerYear_ByStoryteller`: 각 이야기꾼으로 시나리오 5개 × 50년을 돌린다. 연평균이 평온 1~4, 균형 3~8, 파란만장 6~14이고, 평온 < 균형 < 파란만장이다.
  - `NoMonthOverThreePlayerEvents`
  - `WorldEventsPerMonth_1to4`
  - `TwoHundredYears_NarrativeChains`: `grudge_duel_challenge` 또는 `grudge_ambush`, 그리고 `grudge_enemy_child`, `debt_repay`, 마도 전환이 각각 1회 이상. 마도 세력은 40% 이하.
  - 기존 `TwoHundredYears_StaysHealthy`가 계속 통과해야 한다.
- [ ] 데이터를 쓰고, 테스트를 돌리고, 범위를 벗어나면 가중치·긴장도를 조정한다. 조정은 데이터 파일에서만 한다.
- [ ] 커밋: `feat: 2단계 사건 41개와 이야기꾼`

### Task 9: 화면

**Files:**
- Create: `Assets/Murim.View/UI/Panels/EventDialog.cs`
- Modify:
  - `TopBar.cs`: 대기 사건 수 단추, 대기 중이면 "다음 달" 비활성
  - `GameScreen.cs`: 화면을 그린 뒤 대기 사건이 있으면 창을 띄운다.
  - `NewGameScreen.cs`: 이야기꾼 선택
  - `CharacterPanel.cs`: 표식 칩, 대물림 기억 표기
  - `ChroniclePanel.cs`: "사건만" 필터와 범주 색 점
  - `Theme.cs`: 범주 색
- 확인: `dotnet build dotnet/Murim.View.Check`

**Interfaces:**
- Produces:
  - `EventDialog.Show(Bootstrap app, Action onClosed)`: `app.Session.PendingEvents[0]`부터 하나씩 보여 준다. 창 이름은 `"EventDialog"`, 선택지 단추 이름은 `"Choice{i}"`, 확인 단추 이름은 `"EventOk"`다(UiScenario가 이 이름으로 찾는다).
  - `ChroniclePanel.Build(area, session, ChronicleFilter filter, Action<ChronicleFilter> onChange)`, `enum ChronicleFilter { All, Own, Events }`

- [ ] 구현하고 View.Check 빌드에서 경고·오류가 0개인지 확인한다.
- [ ] 커밋: `feat: 사건 선택 창, 이야기꾼 선택, 표식 표시`

### Task 10: Unity 확인

**Files:**
- Modify: `Assets/Murim.Editor/UiScenario.cs`(`RunTask13` 추가)
- 확인: Unity EditMode 테스트, 장면 다시 만들기

- [ ] `RunTask13` 항목:
  1. 새 게임(파란만장, minor_sect, henan, seed 42)
  2. `ForceEvent("grudge_duel_challenge")`로 창이 뜬다.
  3. 은자 조건 선택지가 비활성이고 사유 툴팁이 있다.
  4. "다음 달"이 비활성이다.
  5. 선택 → 결과 문장 → 확인하면 창이 닫힌다.
  6. 사건을 하나 더 띄우고 1번 칸에 저장 → 처음 화면 → 불러오기 → 창이 다시 뜬다.
  7. 표식 칩이 보인다.
  8. 연대기 "사건만"
  9. 2년 진행(창마다 첫 선택지)
  10. 스크린샷 `13_*.png`
- [ ] Unity 배치로 EditMode 테스트를 돌려 모두 통과하는지 확인한다.
- [ ] `RunTask13`이 "결과: 모두 통과"를 내는지 확인한다.
- [ ] 스크린샷에서 겹침, 넘침, 조사 문제를 확인하고 고친다.
- [ ] 커밋: `test: 2단계 Unity 화면 확인`
