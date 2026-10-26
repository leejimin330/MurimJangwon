# 무림장원 4단계: 장원 건설 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**목표:** 플레이어 세력의 장원을 격자에 짓고, 건물이 수련·수입·정원에 영향을 주며, 장원 화면에서 제자의 하루를 실시간으로 본다.

**구조:** `Murim.Core.Manor`(정의·상태·검사·월 처리·일과) + `Faction.Manor`(저장 버전 4) + `ManorScreen`(uGUI 코드 생성, 실시간 일과).

**기술 스택:** Unity 6000.3.23f1, C# 9, Newtonsoft.Json, NUnit (Unity EditMode)

**설계 문서:** `docs/superpowers/specs/2026-10-27-murim-stage4-manor-design.md`

## 전역 제약

- 3단계 계획의 전역 제약을 그대로 따른다(Core는 UnityEngine 금지, `GameRandom`, Ordinal, 예외 없음, `Josa`, Unity batch 테스트).
- 격자 24×16, 본당 4×3을 (10,6)에, 정문 칸 (11,15)·(12,15)는 비운다.
- 건물 표(설계 3절)의 값을 그대로 쓴다. 기본 정원 12, 숙소당 +6.
- 놓기·허물기는 행동 횟수를 쓰지 않는다. 허물기 환불: 미완공 전액, 완공 절반.

## 검토할 점 (테스트로 고정할 것)

1. **장문인이 바뀌어 플레이어가 새 세력의 수장이 되는 경우**(후계, 문파 창설, 하극상). 장원이 없으면 그 자리에서 만들어져야 한다. 담당: Task 2 `EnsureManor_OnSuccessionAndFounding`.
2. **은자가 모자란데 놓기를 누르는 경우.** 은자가 음수가 되지 않고 사유가 나와야 한다. 담당: Task 2 `Place_NotEnoughSilver`.
3. **완공 전 건물의 보너스.** 미완공 건물은 아무것도 주지 않는다. 담당: Task 3 `Bonuses_OnlyCompleted`.
4. **버전 3 저장 불러오기.** 플레이어 세력에 본당만 있는 장원이 생기고 결정론이어야 한다. 담당: Task 2 `Load_Version3_CreatesManor`.
5. **정원을 넘는 영입.** 사건 `recruit`도 정원을 넘지 못한다(플레이어 세력). 담당: Task 3 `Capacity_BlocksRecruitAndEvent`.

---

### Task 1: 건물 데이터

**Files:** Create `Assets/Murim.Core/Manor/BuildingDef.cs`, `Assets/StreamingAssets/Data/buildings.json`; Modify `Data/GameData.cs`(`Buildings`), `Data/DataLoader.cs`(파일 목록, 없어도 됨), `Data/DataValidator.cs`; Test `BuildingDataTests.cs`

**Interfaces:**
```csharp
public class BuildingBonuses { int MasteryPercent, EnlightenmentPerYear, TrainingQiMultiplier, IncomePerMonth, Capacity, Defense;
  double BreakthroughBonus, DeviationReduction; bool NoDeviationDeath; [JsonExtensionData] Unknown;
  public void Add(BuildingBonuses o); }
public class BuildingDef { string Id, Name, Description; int Width, Height, Cost, BuildMonths, Max = 1; bool Fixed; BuildingBonuses Bonuses = new(); }
public static class BuildingValidator { public static List<string> Validate(GameData d); }
```
- [ ] 테스트: `Validate_UnknownBonusKey_Error`, `Validate_SizeOutOfRange_Error`, `Shipped_HasNineBuildingsAndMainHall`.
- [ ] 실패 확인 → 구현 → Unity 통과 → 커밋 `feat: 장원 건물 데이터`

### Task 2: 장원 상태, 놓기·허물기, 저장 버전 4

**Files:** Create `Manor/ManorState.cs`, `Manor/ManorOps.cs`; Modify `World/Faction.cs`(`ManorState Manor`), `World/WorldState.cs`(버전 4), `Persistence/SaveSystem.cs`(알 수 없는 건물 거부), `Session/GameSession.cs`(이관, `PlaceBuilding`, `Demolish`, 매 턴 끝·사건 해결 뒤 `ManorOps.EnsurePlayerManor`), `Generation/ScenarioSetup.cs`, `Actions/Personal/PersonalActions.cs`(`FoundSect`); Test `ManorOpsTests.cs`

**Interfaces:**
```csharp
public class ManorState { int Width = 24, Height = 16; List<BuildingInstance> Buildings = new(); int NextBuildingNumber = 1; }
public class BuildingInstance { string Id, DefId; int X, Y, CompleteMonth; }
public static class ManorOps {
  public const int GateY = 15; public static readonly int[] GateX = { 11, 12 };
  public static ManorState CreateDefault(GameData d);                 // 본당 (10,6), 완공
  public static void EnsurePlayerManor(WorldState w, GameData d);     // 플레이어가 이끄는 세력에 없으면 만든다
  public static ActionCheck CanPlace(WorldState w, GameData d, Faction f, string defId, int x, int y);
  public static BuildingInstance Place(WorldState w, GameData d, Faction f, string defId, int x, int y);
  public static ActionCheck CanDemolish(Faction f, string buildingId);
  public static int Demolish(WorldState w, GameData d, Faction f, string buildingId);  // 환불액
  public static bool IsComplete(WorldState w, BuildingInstance b);
  public static BuildingInstance At(ManorState m, GameData d, int x, int y);
}
// GameSession
public ActionCheck PlaceBuilding(string defId, int x, int y); public ActionCheck Demolish(string buildingId);
```
- 사유 문구: "장원 밖입니다", "다른 건물과 겹칩니다", "정문 자리는 비워 두어야 합니다", "{이름}은(는) {max}개까지 지을 수 있습니다"(Josa), "은자가 부족합니다", "본당은 허물 수 없습니다", "세력을 이끌 때만 지을 수 있습니다".
- [ ] 테스트: `CreateDefault_HasMainHall`, `Place_OutOfBounds/Overlap/Gate/Max/NotEnoughSilver`, `Place_DeductsSilverAndSetsCompleteMonth`, `Demolish_RefundRules`, `EnsureManor_OnSuccessionAndFounding`, `Load_Version3_CreatesManor`, `Load_UnknownBuilding_Rejected`.
- [ ] 실패 확인 → 구현 → Unity 통과(+ `SaveSystemTests`, `ScenarioTests`) → 커밋 `feat: 장원 상태와 건물 놓기·허물기, 저장 버전 4`

### Task 3: 월 처리, 보너스, 정원

**Files:** Create `Manor/ManorPhase.cs`; Modify `Simulation/TurnProcessor.cs`(수련 다음), `Martial/Cultivation.cs`, `Martial/CultivationPhase.cs`, `Actions/Faction/PersonnelActions.cs`(`RecruitNamed` 정원), `Events/Effects.cs`(`recruit` 정원), `Chronicle/Chronicle.cs`(`BuildingCompleted`); Test `ManorBonusTests.cs`

**Interfaces:**
```csharp
public static BuildingBonuses ManorOps.Bonuses(WorldState w, GameData d, Faction f);   // 완공 건물 합, 장원 없으면 빈 값
public static BuildingBonuses ManorOps.BonusesFor(WorldState w, GameData d, Character c);
public static int ManorOps.Capacity(WorldState w, GameData d, Faction f);   // 12 + 숙소; 장원 없으면 int.MaxValue
public class ManorPhase : ITurnPhase { }  // 이번 달 완공 → 연대기, 수입
```
- [ ] 테스트: `Bonuses_OnlyCompleted`, `TwoTrainingGrounds_Plus50Percent`, `Library_AddsEnlightenment`, `Cave_TrainingQiTimesFour_AndBreakthroughBonus`, `Apothecary_NoDeviationDeath`, `MarketStalls_Income`, `Capacity_BlocksRecruitAndEvent`, `Phase_CompletionChronicle`.
- [ ] 실패 확인 → 구현 → Unity 통과(+ `CultivationTests`, `FactionActionTests`, `TurnProcessorTests`) → 커밋 `feat: 장원 건물 보너스, 수입, 정원`

### Task 4: 제자 하루 일과 (`ManorLife`)

**Files:** Create `Manor/ManorLife.cs`; Test `ManorLifeTests.cs`

**Interfaces:**
```csharp
public struct LifeSpot { public string Activity; public BuildingInstance Building; public float X, Y; }  // X,Y 칸 좌표(소수)
public static class ManorLife {
  public const float SecondsPerHour = 2f;
  public static LifeSpot Spot(WorldState w, GameData d, ManorState m, Character c, int hour);
  public static IEnumerable<Character> Residents(WorldState w, Faction f);   // 살아 있는 소속 인물(객경 포함), 이름순
}
```
- [ ] 테스트: `Schedule_ByHour`(6시 연무장 수련, 2시 숙소 잠, 12시 본당 식사), `NoLibrary_FallsBackToMainHall`, `Training_StaysInCave`, `SameInput_SameSpot_DifferentPeople_DifferentSpots`.
- [ ] 실패 확인 → 구현 → Unity 통과 → 커밋 `feat: 장원 제자 하루 일과`

### Task 5: 장원 화면

**Files:** Create `Assets/Murim.View/UI/Screens/ManorScreen.cs`(MonoBehaviour 시계는 `Bootstrap`의 `Update`에서 화면에 넘김); Modify `Bootstrap.cs`(현재 화면 `Tick(float dt)` 호출), `UI/Screens/BasicScreens.cs`(`UiScreen.Tick` 기본 빈 메서드), `UI/Panels/FactionPanel.cs`("장원 보기")
- 화면 요소 이름(시나리오가 찾는다): `ManorGrid`, 칸 `Cell`, 건물 `Building:{id}`, 미리보기 `Ghost`, 목록 단추 이름은 건물 이름, `Clock`, 속도 단추 "일시정지"·"1배"·"2배"·"3배", 사람 점 `Resident:{id}`, "허물기", "지도로".
- 테스트용 진입점: `ManorScreen.HoverCell(int x, int y)`, `ManorScreen.ClickCell(int x, int y)`, `ManorScreen.Advance(float seconds)`.
- [ ] 구현 → `dotnet build dotnet/Murim.View.Check` 오류 없음 → 커밋 `feat: 장원 화면 (격자 건설, 실시간 일과)`

### Task 6: Unity 확인 (`RunTask15`)과 마무리

- [ ] 시나리오: 소문파 시작 → "장원 보기" → 스냅샷 → 연무장 고르고 (2,2) 미리보기 초록, (10,6) 빨강 → (2,2)에 놓기 → 은자 −300 → 3개월 진행 → 완공 연대기 → 장원 화면 다시, 일과 시계 6시로 `Advance` → 연무장 칸 안에 사람 점 있음 → 일시정지면 시계 멈춤 → 허물기 → 환불 → "지도로".
- [ ] `tools/unity-run.sh Murim.Editor.UiScenario.RunTask15` 통과, 스냅샷 눈으로 확인, 전체 Unity 테스트 통과.
- [ ] 설계 문서 상태·구현 중 바뀐 점 → 커밋 `fix: Unity 확인 — 4단계 장원 화면; RunTask15`
