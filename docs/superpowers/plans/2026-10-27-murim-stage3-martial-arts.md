# 무림장원 3단계: 무공·경지 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**목표:** 인물이 무공을 익히고, 내공·깨달음을 쌓아 돌파하며, 초식 카드로 싸우고, 그 싸움이 한 합씩 글로 남는다.

**구조:**
- 순수 C# `Murim.Core.Martial` 네임스페이스에 무공 정의·로더·검증, 수련·돌파, 결투 엔진, 결투 서술, 무공 부여를 둔다.
- `Character`에 `Arts`·`Qi`·`Enlightenment`·`MainArtId`·`TrainingUntil`·`TrainingMonths`를 더하고 저장 버전을 3으로 올린다.
- 사건 `duel` 효과와 `Spar` 행동이 `Duel.Fight`를 쓰고, 결투 경과는 `ChronicleEntry.Detail`·`PendingEvent.Detail`로 화면까지 간다.
- 화면에는 인물 패널 무공 영역, 결투 경과 보기, 연대기 펼침, 상단 바 폐관 표시를 더한다.

**기술 스택:** Unity 6000.3.23f1, C# 9, Newtonsoft.Json, NUnit (Unity EditMode), uGUI 코드 생성

**설계 문서:** `docs/superpowers/specs/2026-10-26-murim-stage3-martial-arts-design.md`

## 전역 제약

- `Murim.Core`는 UnityEngine을 참조하지 않는다. 난수는 `GameRandom` 하나에서만 나온다. 사전은 `SortedDictionary(StringComparer.Ordinal)`, 반복은 Ordinal 순서.
- Core는 예외를 던지지 않는다. 실패는 `ActionCheck.Fail("한국어 사유")`.
- 화면 문장은 한국어, 조사는 `Josa`. "을(를)" 같은 표기가 화면·연대기에 남으면 안 된다.
- 테스트 실행: `dotnet test`는 Smart App Control에 막힌다. 컴파일 확인은 `dotnet build dotnet/Murim.Core.Tests`, 실행은 Unity batch:
  `"/c/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter <이름> -testResults "$(pwd -W)/TestResults/editmode.xml" -logFile TestResults/unity.log`
- 경지 문턱(내공 년): 삼→이 10, 이→일 20, 일→절정 40, 절정→초절정 60, 초절정→화경 100, 화경→현경 180. 기본 돌파 확률 0.70/0.55/0.40/0.25/0.12/0.05.
- 돌파 확률 = 기본 + (오성 − 10) × 0.02 + 폐관 0.15 + 사건 보너스, 범위 0.02~0.95. 실패: 깨달음 −50, 내공 −10%, 주화입마 0.15(+0.15 마공, −0.05 폐관).
- 주화입마: 표식 `qi_deviation` 12개월(결투 −25%), 근골 또는 오성 −1~2, 5% 사망(사인 "주화입마").
- 결투: 체력 = 60 + 경지×35 + 근골×3 + 심법 등급×10, 내력 = 20 + 경지×15 + min(내공년, 경지×40+40)/2, 최대 6합, 상성 쾌>강>유>쾌 ×1.3 / 역상성 ×0.8, 경지 보정 1.35^(경지 차), 난수 0.85~1.15.
- 결투 기록 최대 14줄, 같은 문장 반복 없음.
- 무공 최대 6개(내공심법 1개 포함). 성취 1~10.

## 검토할 점 (테스트로 고정할 것)

1. **버전 2 저장(2단계)을 불러오는 경우.** 모든 인물이 무공·내공을 받고, 같은 파일을 두 번 불러오면 결과가 같아야 한다. 담당: Task 2 `Load_Version2_MigratesDeterministically`.
2. **결투 도중 한쪽이 내력이 바닥나는 경우.** 내력이 모자란 카드는 내지 않고, `exhausted` 문장이 남고, 예외 없이 끝나야 한다. 담당: Task 4 `Exhausted_NoUnaffordableCards`.
3. **외공이 하나도 없는 인물(아이, 이관 실패)이 결투에 끌려 나오는 경우.** "주먹질" 기본 카드로 싸워야 한다. 담당: Task 4 `NoExternalArts_UsesBasicFists`.
4. **폐관 중인 플레이어에게 사건·행동이 오는 경우.** 이야기꾼 사건이 오지 않고, 행동 예약이 막히고, 끝나면 출관 사건이 한 번 뜬다. 담당: Task 8 `ClosedTraining_BlocksAndEndsWithEvent`.
5. **사건 결투로 플레이어가 죽는 경우.** 경과의 마지막 줄이 `death` 문장이고, 후계가 바로 이어진다. 담당: Task 6 `EventDuel_PlayerDies_DetailEndsWithDeath`.

---

### Task 1: 무공 데이터 형식, 로더, 검증, `martial_arts.json`

**Files:**
- Create: `Assets/Murim.Core/Martial/ArtDef.cs`, `Assets/Murim.Core/Martial/ArtValidator.cs`, `Assets/StreamingAssets/Data/martial_arts.json`
- Modify: `Data/GameData.cs` (`Arts`, `Narration`), `Data/DataLoader.cs` (파일 목록에 `martial_arts.json`, `ArtFile` 읽기), `Data/DataValidator.cs` (`ArtValidator.Validate` 호출), `Tests/Support/TestJson.cs` (최소 무공 2개)
- Test: `Assets/Tests/Murim.Core.Tests/ArtDataTests.cs`

**Interfaces:**
- Produces:
```csharp
namespace Murim.Core.Martial {
  public enum ArtType { Sword, Saber, Spear, Fist, Finger, Movement, Inner, Hidden }
  public enum CardKind { Attack, Defense, Evade, Finisher }
  public enum CardStyle { Fast, Hard, Soft }
  public class ArtRequires { public int RealmMin; public int ConstitutionMin; public int InsightMin; }
  public class CardDef { string Id, Name; CardKind Kind; CardStyle Style; int Power, Cost, MasteryMin; }
  public class ArtDef { string Id, Name, Description, Faction; ArtType Type; int Grade; Alignment Alignment;
    ArtRequires Requires = new(); List<CardDef> Cards = new(); [JsonExtensionData] Unknown;
    bool IsInner => Type == ArtType.Inner; }
  public class NarrationDef { Dictionary<string, string[]> Lines; }   // 키: open hit graze block evade clash probe counter finisher exhausted win death timeout
  public class ArtFile { List<ArtDef> Arts; Dictionary<string,string[]> Narration; }
  public static class ArtValidator { public static List<string> Validate(GameData d); }
}
// GameData
public SortedDictionary<string, ArtDef> Arts (Ordinal); public SortedDictionary<string, string[]> Narration (Ordinal);
```

- [ ] 테스트를 쓴다:
  - `Validate_DuplicateCardId_Error`, `Validate_InnerWithCards_Error`, `Validate_ExternalWithOneCard_Error`, `Validate_UnknownFaction_Error`(factions.json에 없는 `faction`), `Validate_GradeOutOfRange_Error`(1~5 밖), `Validate_NarrationBadToken_Error`(`{zzz}`).
  - `Shipped_ArtsAbout60`: 55~70개, 내공심법은 등급 1~5마다 적어도 하나, 마도 무공(`Demonic`) 5개 이상.
  - `Shipped_SecretArts_Cover25Factions`: `Faction`이 있는 무공의 서로 다른 세력이 25개 이상.
  - `Shipped_EveryFactionArtHasStarterCard`: 비전 외공마다 `masteryMin ≤ 1`인 카드가 2장 이상.
  - `Shipped_NarrationHasAllKeys`: 13개 키 모두, 키마다 문장 3개 이상(`win`·`death`·`timeout`은 2개 이상).
- [ ] 테스트가 컴파일 실패하는지 확인: `dotnet build dotnet/Murim.Core.Tests` → `ArtDef` 없음.
- [ ] 구현한다. 서술 문구의 표기는 `{a}` `{b}` `{winner}` `{loser}` `{art}` `{card}` `{guard}`이고 뒤에 `:이 :을 :은 :와 :으로`를 붙인다. `{card}`·`{guard}`는 작은따옴표로 감싸 그리고 조사는 따옴표 안 이름 기준으로 단다(설계 3.3의 `{eul}` 표기 대신 이것을 쓴다).
  - `martial_arts.json`: 설계 3.2 목록대로 약 62개. 비전 약 32개(설계의 20개 + 태산 태산십팔반, 숭산 대숭양검, 형산 회풍락안검, 오대산 금강부동신공, 장백 장백검법, 남소림 남권, 무이 무이검, 대리단가 일양지, 오독문 오독장, 만독문 만독심법, 북해빙궁 빙백신공, 명교 성화신공, 백련교 백련심법), 일반 약 20개, 사·마 일반 약 10개. 외공 카드 3~5장, 절초는 `masteryMin` 7 이상, 위력 8~32, 내력 2~16.
- [ ] Unity 실행: `-testFilter ArtDataTests` → 통과. `ShippedDataTests`도 통과.
- [ ] 커밋: `feat: 무공 데이터 형식과 무공 약 60개, 결투 문구`

### Task 2: 인물 무공 상태, 무공 부여, 저장 버전 3

**Files:**
- Create: `Assets/Murim.Core/Martial/KnownArt.cs`, `Assets/Murim.Core/Martial/ArtGranting.cs`
- Modify: `World/Character.cs`, `World/ChronicleEntry.cs` (`string[] Detail`), `World/EventState.cs` (`PendingEvent.Detail`), `World/WorldState.cs` (`CurrentSaveVersion = 3`), `Persistence/SaveSystem.cs` (`ReadResult.LoadedVersion`, 알 수 없는 무공 거부), `Session/GameSession.cs` (`Load`에서 버전 1·2면 `ArtGranting.Migrate`), `Generation/CharacterGenerator.cs` (`Create`·`CreateChild` 끝에서 `ArtGranting.Grant`), `Generation/WorldGenerator.cs` (자식 경지를 바꾼 뒤 `ArtGranting.Regrant`)
- Test: `Assets/Tests/Murim.Core.Tests/ArtGrantingTests.cs`, `SaveSystemTests.cs`에 추가

**Interfaces:**
- Produces:
```csharp
public class KnownArt { public string ArtId; public int Mastery; }
// Character
public List<KnownArt> Arts = new(); public int Qi;            // 개월 단위
public int Enlightenment; public string MainArtId; public int TrainingUntil; public int TrainingMonths;
public bool InTraining(int month) => month < TrainingUntil;
public static class ArtGranting {
  public const int MaxArts = 6;
  public static void Grant(WorldState w, GameData d, GameRandom rng, Character c);   // 비어 있을 때만
  public static void Regrant(WorldState w, GameData d, GameRandom rng, Character c);  // 지우고 다시
  public static void Migrate(WorldState w, GameData d, GameRandom rng);              // Arts가 빈 인물 모두
  public static bool Learn(GameData d, Character c, string artId, int mastery);      // 이미 알면 false, 6개면 false, 내공심법은 더 높은 등급만 교체
  public static ArtDef Inner(GameData d, Character c); public static ArtDef MainArt(GameData d, Character c);
  public static IEnumerable<ArtDef> Learnable(WorldState w, GameData d, Character c, Func<ArtDef,bool> filter); // requires 만족 + 정사마 규칙
}
```
- 부여 규칙(설계 4.4): 내공심법 1 + 외공 1~2(10세 미만은 외공 1). 주요 세력이면 그 세력 비전 우선, 남는 칸은 경지에 맞는 등급(경지 0~1 → 1~2, 2~3 → 2~3, 4+ → 3~4)의 일반 무공. 정파는 `Righteous` 일반 무공, 사파는 `Righteous|Unorthodox`, 마도는 셋 다. 성취 = clamp(경지×1.5 ± 1, 1, 10)(반올림). 내공 = 현재 경지 문턱~다음 문턱 사이(개월). 깨달음 0~60. `requires`는 부여 시 무시하지 않는다(맞는 무공이 없으면 등급을 낮춘다).
- 사망한 인물은 이관 때 빈 목록으로 둔다(저장 크기).

- [ ] 테스트:
  - `Grant_GivesInnerAndExternal`(경지 2 무당파 인물 → 내공심법 1, 외공 1~2, 무당 비전 하나 이상, 내공 20~40년 사이), `Grant_Child_OneExternalZeroQi`, `Grant_MinimalDataWithoutArts_NoOp`.
  - `Learn_RespectsMaxAndInnerReplace`.
  - `Load_Version2_MigratesDeterministically`: 2단계 세계(무공 없는 인물)를 저장 버전 2로 직렬화 → 두 번 `GameSession.Load` → `Serialize()`가 같고, 살아 있는 인물 모두 `Arts.Count ≥ 1`.
  - `Load_UnknownArt_Rejected`: 저장 파일의 무공 id가 데이터에 없으면 `Ok == false`, 오류에 "알 수 없는 무공".
- [ ] 컴파일 실패 확인 → 구현 → Unity `-testFilter "ArtGrantingTests|SaveSystemTests"` 통과.
- [ ] 커밋: `feat: 인물 무공 상태와 부여, 저장 버전 3`

### Task 3: 수련과 돌파 (`Cultivation`, `CultivationPhase`)

**Files:**
- Create: `Assets/Murim.Core/Martial/Cultivation.cs`, `Assets/Murim.Core/Martial/CultivationPhase.cs`
- Modify: `Simulation/TurnProcessor.cs` (사건 다음에 `CultivationPhase`), `Simulation/Phases/LifePhases.cs` (`YearlyPhase` 무작위 경지 상승 삭제), `Rules/SimConstants.cs` (수련 상수), `Chronicle/Chronicle.cs` (`Breakthrough`, `QiDeviation`, `TrainingEnded`), `StreamingAssets/Data/events.json` (표식 `qi_deviation`)
- Test: `Assets/Tests/Murim.Core.Tests/CultivationTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum BreakthroughOutcome { Success, Failed, Deviation, Died }
public static class Cultivation {
  public static readonly int[] ThresholdYears = { 10, 20, 40, 60, 100, 180 };
  public static readonly double[] BaseChance = { 0.70, 0.55, 0.40, 0.25, 0.12, 0.05 };
  public static int QiYears(Character c) => c.Qi / 12;
  public static bool CanBreakthrough(Character c);               // 경지 < 6, 내공 ≥ 문턱, 깨달음 ≥ 100
  public static int MonthlyQi(GameData d, Character c, int month); // 1 + ⌊(근골 + 심법등급×4)/10⌋, 마공 심법 +1등급, 10세 미만·70세 이상 ½, 폐관 ×3
  public static double BreakthroughChance(Character c, double bonus, bool closed);
  public static BreakthroughOutcome TryBreakthrough(WorldState w, GameData d, GameRandom rng, Character c, double bonus, bool closed);
  public static bool KnowsDemonic(GameData d, Character c);
  public static void YearlyGrowth(WorldState w, GameData d, GameRandom rng, Character c); // 깨달음·성취
  public const string DeviationFlag = "qi_deviation";
}
public class CultivationPhase : ITurnPhase { } // 매달 내공·폐관 종료, 1월 성장·입문 전수·AI 돌파·플레이어 돌파 기회 예약
```
- 성장 규칙(설계 4.3): 깨달음 += 오성/2 + 2×그해 이긴 결투 수(최대 3). 이긴 결투 수는 표식 없이 `Character.DuelWinsThisYear`(1월에 0으로)로 센다. 성취 +1 확률 = (오성+근골)/40 × (6−등급)/5, 주무공 ×1.5.
- 입문 전수(설계 4.4 "입문"): 1월에 소속 세력(객경 제외)의 비전을 하나도 모르면 조건 맞는 것 하나를 1성으로 배운다 — 입문 즉시가 아니라 다음 1월에 처리한다(구현 편의, 설계 12절에 적는다).
- AI 돌파: 1월, 플레이어가 아닌 인물이 `CanBreakthrough`면 시도. 수장·후계자는 `closed: true`.
- 플레이어 돌파 기회: 매달 플레이어가 `CanBreakthrough`이고 폐관 중이 아니고 쿨다운 `art_breakthrough_chance|{id}`가 없고, 대기·예약 목록에 같은 사건이 없으면 다음 달 `art_breakthrough_chance`를 예약한다(Task 7의 사건).
- 연대기: 돌파 성공은 `BirthPhase.IsNotable`이면 기록(경지 4 이상이면 중요도 2), 주화입마·사망도 같은 기준.

- [ ] 테스트: `Threshold_BlocksAndOpens`, `Chance_FormulaAndClamp`(오성 20, 폐관, 보너스 0.5 → 0.95), `Fail_LosesEnlightenmentAndQi`, `Deviation_DemonicDoubles`(10000회 실패 중 주화입마 비율 0.13~0.17 / 0.28~0.32), `MonthlyQi_TrainingTriples`, `MonthlyQi_DemonicInnerPlusOneGrade`, `YearlyGrowth_MasteryProbability`(근골·오성 10, 등급 1 → 2000회 중 0.45~0.55), `YearlyPhase_NoRandomRealmGain`, `Join_LearnsFactionSecretNextJanuary`.
- [ ] 실패 확인 → 구현 → Unity `-testFilter CultivationTests` 통과, `LifePhaseTests` 통과(무작위 경지 상승 테스트가 있으면 이 규칙에 맞게 고친다).
- [ ] 커밋: `feat: 내공·깨달음·성취 성장과 경지 돌파, 주화입마`

### Task 4: 결투 엔진 (`Duel.Fight`)

**Files:**
- Create: `Assets/Murim.Core/Martial/Duel.cs`
- Test: `Assets/Tests/Murim.Core.Tests/DuelTests.cs`

**Interfaces:**
- Produces:
```csharp
public class DuelOptions { public int MaxRounds = 6; public double DeathChance; }
public enum RoundEvent { Hit, Graze, Block, Evade, Clash, Probe, Counter, Exhausted }
public class DuelRound { public int Number; public string ActorId; public string TargetId;
  public CardDef Card; public ArtDef Art; public CardDef Guard; public RoundEvent Event; public int Damage; public bool IsFinisher; }
public class DuelResult { public Character Winner, Loser; public int Rounds; public bool TimedOut; public bool LoserDied;
  public List<DuelRound> Log = new(); public List<string> Lines = new(); public string Summary; }
public static class Duel {
  public static DuelResult Fight(WorldState w, GameData d, GameRandom rng, Character a, Character b, DuelOptions o = null);
  public static int MaxHp(GameData d, Character c); public static int MaxForce(GameData d, Character c);
  public static List<(CardDef card, ArtDef art)> Deck(GameData d, Character c);  // 외공 없으면 BasicFists 3장
  public static readonly ArtDef BasicFists;   // id "basic_fists", 이름 "주먹질": 공격 쾌 8/0, 방어 강 8/0, 공격 강 10/2
}
```
- 한 합: 두 사람이 동시에 카드를 고르고, a→b, b→a 순으로 판정한다. 공격 값 = 위력 × (1 + 성취/10) × 1.35^(경지 차) × 상성 × 난수(0.85~1.15) × (1 + 표식 `DuelModifier` 합/100).
  - 상대가 방어: 막는 값 = 방어 위력 × (1+성취/10). 내 공격 상성이 상대 방어 상성을 이기면 막는 값 ×0.5(`Counter`). 피해 = max(0, 공격 − 막는 값). 피해가 공격의 30% 이하면 `Block`.
  - 상대가 회피: 성공 확률 = 0.25 + 0.03 × 신법 성취(신법 없으면 0) − 0.1 × max(0, 경지 차), 0.05~0.70. 성공이면 `Evade`(0), 실패면 절반 피해 `Graze`.
  - 둘 다 공격: 둘 다 맞는다. 같은 상성이면 둘 다 ×0.7이고 `Clash`.
  - 둘 다 방어·회피: `Probe`, 피해 없음, 둘 다 내력 +5.
  - 낼 수 있는 카드가 없으면 그 합은 쉬며 내력 +10(`Exhausted`), 상대 공격을 그대로 맞는다.
- AI 카드 선택: 가중치 = 위력 × 상성 기대값(상대 덱의 상성 분포에 대한 평균 배율, 음험이면 제곱) × 상황(상대 체력 ≤30%면 절초 ×3, 내 체력 ≤35%면 방어·회피 ×2, 다혈질 공격 ×1.4, 겁쟁이 회피 ×1.6). 가중 무작위.
- 끝: 체력 0이면 패배. 6합이 지나면 남은 체력 비율이 높은 쪽 승(`TimedOut`). 사망은 `DeathChance`로 판정만 하고 죽이지는 않는다(`LoserDied` 표시, 호출자가 `Kill`).
- 1월 깨달음용으로 승자의 `DuelWinsThisYear`를 1 올린다.

- [ ] 테스트:
  - `SameSeed_SameResult`: 결과와 `Log`가 같다.
  - `WinRate_RealmDiff`: 같은 능력치(근골·오성 10, 같은 무공·성취 5, 내공은 각자 경지 문턱) 1000회. 경지 +1 → 0.75~0.85, +2 → ≥0.90, 같음 → 0.40~0.60. 맞을 때까지 Task 범위에서 상수(난수 폭, 경지 보정 밑)를 조정하고 바꾼 값은 설계 12절에 적는다.
  - `Exhausted_NoUnaffordableCards`: 내력 0으로 시작시킨 인물(테스트용 `DuelOptions.StartForceA = 0`)은 `Exhausted` 합이 있고 내력을 넘는 카드를 낸 합이 없다.
  - `NoExternalArts_UsesBasicFists`, `Finisher_RequiresMastery`(성취 6이면 `masteryMin 7` 카드가 덱에 없다), `TimedOut_HigherHpRatioWins`.
- [ ] 실패 확인 → 구현 → Unity `-testFilter DuelTests` 통과.
- [ ] 커밋: `feat: 초식 카드 자동 결투 엔진`

### Task 5: 결투 서술 (`DuelNarrator`)

**Files:**
- Create: `Assets/Murim.Core/Martial/DuelNarrator.cs`
- Modify: `Martial/Duel.cs` (`Fight` 끝에서 `DuelNarrator.Narrate`)
- Test: `Assets/Tests/Murim.Core.Tests/DuelNarratorTests.cs`

**Interfaces:**
- Produces:
```csharp
public static class DuelNarrator {
  public const int MaxLines = 14;
  public static void Narrate(GameData d, GameRandom rng, DuelResult r, Character a, Character b);  // r.Lines, r.Summary 채움
  public static string Format(string template, IDictionary<string,string> words);  // {key} {key:을}, {card}/{guard}는 따옴표
  public static IEnumerable<string> BadTokens(string template);
}
```
- 합마다 사건 종류에 맞는 키(`hit graze block evade clash probe counter exhausted`), 절초면 먼저 `finisher` 줄. 처음 `open`, 끝 `win`/`timeout`, 사망이면 마지막에 `death`.
- 14줄을 넘으면 덜 중요한 줄부터 뺀다: 중요도 = 절초 3, 가장 큰 피해 3, 마지막 일격 3, 상성 깨뜨림 2, 명중 1, 그 밖 0. 같으면 앞 합부터 남긴다.
- `Summary`: 절초로 끝났으면 "{승자:이} 절초 '{카드}'{으로} {패자:을} 꺾었다 ({n}합)", 아니면 "{승자:이} {주무공}{으로} {패자:을} 꺾었다 ({n}합)", 판정이면 "… 판정으로 우세를 점했다".

- [ ] 테스트: `Lines_AtMost14_NoDuplicates`(서로 다른 시드 200번), `DecisiveRoundsKept`(가장 큰 피해 합의 카드 이름이 Lines에 있다), `Death_LastLineIsDeath`, `NoRawTokensOrJosaMarks`(모든 줄에 `{`, `(을)`, `(이)` 없음), `Summary_UsesFinisherName`.
- [ ] 실패 확인 → 구현 → Unity `-testFilter DuelNarratorTests` 통과.
- [ ] 커밋: `feat: 결투 경과를 한 합씩 문장으로 남긴다`

### Task 6: 결투 연결 (사건 `duel`, 비무, 연대기 경과, 소식)

**Files:**
- Modify: `Events/Effects.cs` (`duel`이 `Duel.Fight` 사용, `ctx.DuelLines`·`ctx.DuelSummary`), `Events/EventContext.cs`, `Events/EventResolver.cs` (`Detail` 저장: 플레이어가 얽혔거나 중요도 2일 때만), `Session/GameSession.cs` (`EventOutcome.DuelLines`), `Events/EventPhase.cs` (소식에 `Detail` 복사), `Actions/Personal/PersonalActions.cs` (`Spar`가 `Duel.Fight`, 플레이어면 소식 추가), `Actions/Personal/PersonalActions.cs`의 `PersonalRules.SparScore` 삭제, `Chronicle/Chronicle.cs` (`Sparred`에 detail)
- Test: `Assets/Tests/Murim.Core.Tests/DuelIntegrationTests.cs`

**Interfaces:**
- Consumes: `Duel.Fight`, `DuelResult.Lines/Summary/LoserDied`.
- Produces: `EventContext.DuelLines: List<string>`; `EventOutcome.DuelLines: string[]`; `PendingEvent.Detail: string[]`; `ChronicleEntry.Detail: string[]`; `Chronicle.Add(..., string[] detail)` 오버로드.
- `duel` 효과: `Summary`를 `ctx.Log`에, `Lines`를 `ctx.DuelLines`에 이어 붙인다(한 사건에 결투가 둘이면 사이에 빈 줄 하나). 사망은 `DeathChance`로 엔진이 판정하고 `Effects.Kill`.

- [ ] 테스트: `EventDuel_WritesDetail`(플레이어 결정 사건의 연대기 줄 `Detail`이 결투 Lines와 같다), `WorldDuel_LowImportance_NoDetail`, `Spar_UsesEngine_AndNotifiesPlayer`(플레이어 비무 후 `PendingEvents`에 소식 1개, `Detail` 있음), `EventDuel_PlayerDies_DetailEndsWithDeath`(deathChance 1, 플레이어가 지게 경지 0 대 6 → Detail 마지막 줄이 narration `death` 중 하나, 후계자가 플레이어), `Outcome_HasDuelLines`.
- [ ] 실패 확인 → 구현 → Unity `-testFilter "DuelIntegrationTests|EffectTests|PersonalActionTests|GameSessionEventTests"` 통과.
- [ ] 커밋: `feat: 사건 결투·비무가 결투 엔진과 경과 기록을 쓴다`

### Task 7: 무공 사건 (조건, 효과, 사건 12개)

**Files:**
- Modify: `Events/EventDef.cs` (조건 `KnowsArt NotKnowsArt ArtGradeMin QiMin CanBreakthrough MasteryMin`, 사건 `System` 플래그), `Events/ConditionCheck.cs`, `Events/EffectDef.cs` (효과 `LearnArt Mastery Qi Enlighten Breakthrough ClosedTraining`), `Events/Effects.cs`, `Events/EventValidator.cs` (무공 id 검사, `system` 사건은 후속 전용 검사 제외), `Events/EventPhase.cs`·`Events/Storyteller.cs` (`System` 사건은 고르지 않는다), `StreamingAssets/Data/events.json` (새 사건 12개, `demonic_manual`이 `learnArt alignment Demonic`), `Session/GameSession.cs` (`ScheduleSystemEvent`)
- Test: `Assets/Tests/Murim.Core.Tests/ArtEventTests.cs`

**Interfaces:**
- Produces:
```csharp
public class LearnArtFx : WhoFx { public string Art; public string From; public int? GradeMin, GradeMax; public Alignment? Alignment; }
public class MasteryFx : WhoAmountFx { public string Art; }
public class QiFx : WhoFx { public int Years; }
public class BreakthroughFx : WhoFx { public double Bonus; public List<EffectDef> Success = new(), Fail = new(); }
public class ClosedTrainingFx : WhoFx { public int Months; }
// enlighten = WhoAmountFx
// EventDef: public bool System;
```
- 사건(설계 6.3): `art_breakthrough_chance`(system, 플레이어), `art_closed_training_end`(system), 나머지 10개는 이야기꾼·세계 사건. 범주는 새 범주 `martial`(`EventCategories.All`에 추가, 이야기꾼 배율 1). `art_master_teaching`·`art_stolen_manual`·`art_rival_demonstration`·`art_sect_tournament`는 `duel`을 쓴다.
- `breakthrough` 효과는 `Cultivation.TryBreakthrough`를 부르고 결과 문장을 `ctx.Log`에 넣는다. 성공 → `Success`, 그 밖 → `Fail`.

- [ ] 테스트: 조건 6개 각각 통과·실패, `LearnArt_RandomRespectsGradeAndAlignment`, `LearnArt_FromRole_PicksKnownArt`, `Breakthrough_Effect_Branches`(확률 0.95·0.02 고정 상황), `ClosedTraining_Effect_SetsTraining`, `SystemEvents_NeverPickedByStoryteller`(1000달), `Shipped_ArtEventsValidate`.
- [ ] 실패 확인 → 구현 → Unity `-testFilter "ArtEventTests|EventDefTests|ShippedDataTests"` 통과.
- [ ] 커밋: `feat: 무공 사건 12개와 무공 조건·효과`

### Task 8: 폐관수련, 무공 전수, 주무공 바꾸기

**Files:**
- Create: `Assets/Murim.Core/Actions/Personal/ClosedTraining.cs`, `Assets/Murim.Core/Actions/Faction/TeachArt.cs`
- Modify: `Actions/IGameAction.cs` (`ActionKeys.ClosedTraining`, `TeachArt`), `Session/GameSession.cs` (`SetMainArt(string artId)`, 폐관 중 `RemainingActions` 0), `Events/Storyteller.cs` (폐관 중이면 플레이어 사건 없음), `Martial/CultivationPhase.cs` (폐관 끝: 깨달음 +15/+25/+40, 성취 +1~3, 출관 사건 예약)
- Test: `Assets/Tests/Murim.Core.Tests/TrainingActionTests.cs`

**Interfaces:**
- Produces:
```csharp
public class ClosedTraining : IGameAction, IWholeMonthAction { public ClosedTraining(string actorId, int months); } // 3·6·12, 수장·개인 모두
public class TeachArt : FactionAction { public TeachArt(string actorId, string targetId, string artId); } // 쿨다운 "teach|{대상}" 12개월, 호감 +10
public ActionCheck GameSession.SetMainArt(string artId);  // 외공만, 아는 무공만
```
- 폐관 시작: `TrainingUntil = Month + months`, `TrainingMonths = months`, `BusyUntilMonth = TrainingUntil`. 상단 바 문구는 Task 10.

- [ ] 테스트: `ClosedTraining_BlocksAndEndsWithEvent`(3개월: 다음 두 달 `RemainingActions == 0`, 이야기꾼 사건 없음, 3개월째 끝에 `art_closed_training_end` 대기, 깨달음 +15 이상), `ClosedTraining_InvalidMonths_Fail`, `TeachArt_CooldownAndRequires`, `TeachArt_NotLeader_Fail`, `SetMainArt_OnlyKnownExternal`.
- [ ] 실패 확인 → 구현 → Unity `-testFilter TrainingActionTests` 통과.
- [ ] 커밋: `feat: 폐관수련, 무공 전수, 주무공 바꾸기`

### Task 9: 장기 균형 (경지 분포, 인구, 사건 빈도)

**Files:**
- Modify: `Rules/SimConstants.cs`(수련 상수), `Tests/Murim.Core.Tests/LongSimulationTests.cs`
- Test: `LongSimulationTests.TwoHundredYears_RealmDistribution`

- [ ] 테스트를 더한다: 200년 동안 10년마다 살아 있는 성인 중 절정 이상 3~12%, 현경 0~3명, 삼류 30% 이상. 기존 인구 500~4000, 세력 20개 이상 유지. 결과는 `TestResults/balance.txt`에 남긴다.
- [ ] Unity `-testFilter "LongSimulationTests|EventBalanceTests"` 실행. 범위를 벗어나면 `SimConstants`의 수련 상수(깨달음 연간 기본값, 문턱 배율, 돌파 확률 배율)만 조정해 다시 돈다. 바꾼 값과 이유를 설계 12절에 적는다.
- [ ] 커밋: `test: 200년 경지 분포 검증과 수련 상수 조정`

### Task 10: 화면 (무공 영역, 결투 경과, 연대기 펼침, 폐관 표시)

**Files:**
- Create: `Assets/Murim.View/UI/Panels/ArtsSection.cs`, `Assets/Murim.View/UI/Panels/DuelLogView.cs`
- Modify: `Panels/CharacterPanel.cs` (능력치 아래 `ArtsSection.Build`), `Panels/EventDialog.cs` (결과에 `DuelLogView`, 소식의 `Detail`), `Panels/ChroniclePanel.cs` (`Detail` 있는 줄 앞 "⚔", 누르면 펼침/접힘 — 펼친 줄 인덱스는 `GameScreen`이 기억), `Panels/TopBar.cs` ("폐관 중 (n개월 남음)"), `Screens/GameScreen.cs`
- 확인: `dotnet build dotnet/Murim.View.Check`

**Interfaces:**
- Produces: `ArtsSection.Build(RectTransform list, Bootstrap app, Character c)`; `DuelLogView.Build(Transform parent, IList<string> lines, float width)` — 첫 줄과 마지막 줄 강조색(`Theme.Gold`, 사망은 `Theme.Seal`), "절초" 줄 굵게.
- 무공 영역(설계 8절): 경지 이름 + 내공 막대(다음 문턱까지, "내공 n년 / m년"), 깨달음 막대(돌파 가능이면 금색), 무공 줄(이름, 종류, 등급 이름, 성취 막대 10칸, 주무공 "★"), 툴팁에 설명과 카드 목록("운중월 — 공격·유, 위력 14, 내력 4" / 성취 미달이면 "(7성)"). 플레이어 본인이면 버튼: "폐관수련"(3·6·12개월 선택 창), 외공마다 "주무공으로". 자기 세력 수장이 소속 인물을 볼 때 "무공 전수"(자기가 아는 무공 선택 창).
- 등급 이름: 1 삼류, 2 이류, 3 일류, 4 절정, 5 신공. 종류 이름: 검법·도법·창법·권장·지공·신법·내공심법·암기.

- [ ] 구현 → `dotnet build dotnet/Murim.View.Check` 경고 외 오류 없음.
- [ ] 커밋: `feat: 인물 무공 영역, 결투 경과 보기, 연대기 펼침`

### Task 11: Unity 확인 (`RunTask14`)과 마무리

**Files:**
- Modify: `Assets/Murim.Editor/UiScenario.cs` (`RunTask14`), `docs/superpowers/specs/2026-10-26-murim-stage3-martial-arts-design.md` (상태, 12절 구현 중 바뀐 점)

- [ ] `RunTask14` 시나리오: 소문파 시작(시드 42) → "나" 패널 무공 영역 스냅샷, 카드 툴팁 텍스트 확인 → 주무공 바꾸기 → 폐관수련 3개월 예약 → 다음 달 ×3, 상단 바 "폐관 중" 확인 → 출관 사건 창 → `ForceEvent("faction_martial_contest")` 출전 → 결과 창에 결투 경과(줄 4개 이상) 스냅샷 → 연대기 "⚔" 줄 누르면 펼침 확인 → 소속 제자에게 무공 전수 예약 → 무소속 시나리오에서 비무 → 소식 창 경과 확인. 각 단계 `TestResults/snapshots/14_*.png`, `task14.txt`.
- [ ] 실행: `tools/unity-run.sh Murim.Editor.UiScenario.RunTask14` → "결과: 모두 통과". 스냅샷을 열어 글자 넘침·겹침을 눈으로 확인하고 고친다.
- [ ] 전체 Unity 테스트(`tools/unity-test.sh`) 통과, `RunTask12`·`RunTask13` 통과.
- [ ] 설계 문서 상태를 "구현·검증 완료"로, 12절에 구현 중 바뀐 점을 적는다.
- [ ] 커밋: `fix: Unity 확인 — 3단계 화면; RunTask14`
