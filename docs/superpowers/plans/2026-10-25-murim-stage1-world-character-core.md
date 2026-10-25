# 무림장원 1단계: 세계·인물 코어 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 시나리오로 시작해 중원 지도 위 약 130개 세력과 수백 명의 인물이 월 단위로 늙고, 혼인하고, 죽고, 대를 잇는 시뮬레이션을 Unity에서 플레이·저장할 수 있게 만든다.

**Architecture:** 게임 로직 전부를 UnityEngine을 참조하지 않는 순수 C# 어셈블리 `Murim.Core`에 둔다. 같은 소스를 `dotnet/` 아래 SDK 프로젝트가 그대로 컴파일해서 `dotnet test`로 빠르게 TDD하고, Unity EditMode 테스트로도 같은 테스트를 돌린다. 화면 `Murim.View`는 uGUI를 코드로 생성하며 Core 상태를 읽고 `IGameAction`만 전달한다.

**Tech Stack:** Unity 6000.3.23f1 (2D), C# 9, Newtonsoft.Json 13 (`com.unity.nuget.newtonsoft-json`), NUnit 3 (Unity Test Framework / NUnit 3.14 on .NET 10 SDK), uGUI.

**Spec:** `docs/superpowers/specs/2026-10-25-murim-stage1-world-character-core-design.md`

## Global Constraints

- `Murim.Core`는 `UnityEngine`을 참조하지 않는다 (asmdef `noEngineReferences: true`). Core 코드에 `using UnityEngine` 금지.
- C# 언어 버전 9.0 (Unity 6 기준). `record`, init-only setter, target-typed `new()`는 사용 가능, C# 10+ 문법(file-scoped namespace, global using, `required`) 금지.
- 모든 난수는 `GameRandom` 하나에서 나온다. `System.Random`, `Guid.NewGuid()`, `DateTime.Now`를 Core 로직에 쓰지 않는다.
- 같은 시드 + 같은 행동 순서 = 같은 결과. 컬렉션은 `SortedDictionary<string, T>`(`StringComparer.Ordinal`)로 저장하고, id는 0으로 채운 고정 길이(`c000001`, `f0001`)라 사전순 = 생성순이다.
- 테스트는 `Assert.That(...)` 형식만 쓴다 (Unity NUnit과 NUnit 3.14 모두 호환).
- Core는 행동 실패로 예외를 던지지 않는다. 실패는 `ActionCheck.Fail(사유)`로 돌려준다.
- 수치 기준값은 모두 `SimConstants`에 모은다. 장기 시뮬레이션 테스트가 실패하면 `SimConstants`만 조정한다.
- 사용자에게 보이는 문자열은 한국어. 인물 이름 뒤 조사는 `Josa` 헬퍼로 붙인다.
- 문파 이름은 실존 이름을 쓰고, 모두 `factions.json`에만 있다 (코드에 문파 이름 하드코딩 금지).
- 저장 파일 `saveVersion` = 1.
- Unity 실행 파일: `C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe`

## Review Focus

1. **후계자가 아무도 없는 AI 세력**: 장문인이 죽고 이어받을 인물이 없으면 세력이 해산되어 멤버는 무소속, 영향력은 반환되고 연대기에 기록된다. 예외가 나면 안 된다. → Task 8 `Succession_NoCandidate_DissolvesFaction`
2. **예약한 행동의 대상이 실행 전에 바뀜**: 혼담 대상이 같은 달 AI 행동이나 이전 행동 때문에 이미 혼인했거나 죽었거나 세력을 옮겼다면, 실행 시 `CanExecute`를 다시 확인해 건너뛰고 연대기에 "~하지 못했다"를 남긴다. → Task 6 `QueuedAction_TargetMarriedMeanwhile_SkippedWithChronicle`
3. **꽉 찬 지역에서 영향력 확장·문파 창설**: 지역 영향력 합이 이미 100이면 가장 센 경쟁 세력에서 빼앗고, 합은 절대 100을 넘지 않는다. → Task 6 `Expand_FullRegion_TakesFromStrongestAndNeverExceeds100`, Task 7 `FoundSect_FullRegion_KeepsSumAtMost100`
4. **어린 자식만 남긴 플레이어의 죽음**: 16세 미만 자식이 있으면 게임 오버가 아니라 그 자식으로 이어서 플레이한다. → Task 8 `PlayerDeath_MinorChild_BecomesPlayer`
5. **깨지거나 버전이 다른 저장 파일 불러오기**: 오류 메시지를 돌려주고 현재 게임 상태를 건드리지 않는다. → Task 10 `Load_CorruptFile_ReturnsErrorAndKeepsCurrentSession`

---

## 파일 구조

```
MurimJangwon/
  .gitignore
  Packages/manifest.json                       (Unity 생성 후 수정)
  Assets/
    Murim.Core/
      Murim.Core.asmdef
      Random/GameRandom.cs
      Text/Josa.cs
      Text/GameDate.cs
      Data/Enums.cs                            FactionType, Alignment, Sex, Role, TraitCategory, MemoryKind
      Data/Definitions.cs                      RegionDef, FactionDef, TraitDef, StatBlock, ScenarioDef, NameData, FactionNameParts
      Data/GameData.cs
      Data/DataLoader.cs
      Data/DataValidator.cs
      World/Character.cs
      World/Faction.cs
      World/RegionState.cs
      World/Memory.cs
      World/OpinionStore.cs
      World/ChronicleEntry.cs
      World/WorldState.cs
      Rules/SimConstants.cs
      Rules/Opinion.cs                         호감도 계산(특성 상성 + 저장값 + 기억)
      Rules/Influence.cs                       지역 영향력 증감 (합 ≤ 100 보장)
      Rules/FactionOps.cs                      세력 해산, 인물 이적, 장문인 조회
      Generation/CharacterGenerator.cs
      Generation/WorldGenerator.cs
      Generation/ScenarioSetup.cs
      Actions/IGameAction.cs                   IGameAction, ActionCheck, ActionKeys
      Actions/ActionBudget.cs
      Actions/Faction/*.cs                     세력 행동 (Task 6)
      Actions/Personal/*.cs                    떠돌이·개인 행동 (Task 7)
      Simulation/ITurnPhase.cs
      Simulation/TurnProcessor.cs
      Simulation/Phases/*.cs                   (Task 8, 9)
      Simulation/FactionAI.cs
      Session/NewGameOptions.cs
      Session/GameSession.cs
      Persistence/SaveSystem.cs
      Chronicle/Chronicle.cs                   연대기 기록 + 문장 템플릿
    Murim.View/
      Murim.View.asmdef
      Fonts/NotoSansKR.ttf
      Bootstrap.cs
      UI/UiFactory.cs
      UI/Screens/*.cs
      UI/Panels/*.cs
    Murim.Editor/
      Murim.Editor.asmdef
      SceneBuilder.cs
    Scenes/Main.unity                          (SceneBuilder가 생성)
    StreamingAssets/Data/
      regions.json  factions.json  faction_name_parts.json  scenarios.json  traits.json  names.json
    Tests/Murim.Core.Tests/
      Murim.Core.Tests.asmdef
      Support/TestPaths.cs
      Support/TestWorlds.cs
      *Tests.cs
  dotnet/
    Murim.Core/Murim.Core.csproj               ../../Assets/Murim.Core/**/*.cs 포함
    Murim.Core.Tests/Murim.Core.Tests.csproj   ../../Assets/Tests/Murim.Core.Tests/**/*.cs 포함
```

**테스트 명령 (모든 Task 공통)**
- 빠른 루프: `dotnet test dotnet/Murim.Core.Tests/Murim.Core.Tests.csproj --filter "FullyQualifiedName~<클래스명>"`
- Unity 확인 (Task 끝마다 1회): `& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile TestResults/unity.log` → `TestResults/editmode.xml`의 `<test-run ... result="Passed"`

---

### Task 1: 프로젝트 뼈대와 GameRandom

**Files:**
- Create: Unity 프로젝트 (Assets, Packages, ProjectSettings), `.gitignore`
- Create: `Assets/Murim.Core/Murim.Core.asmdef`, `Assets/Murim.Core/Random/GameRandom.cs`
- Create: `Assets/Tests/Murim.Core.Tests/Murim.Core.Tests.asmdef`, `Assets/Tests/Murim.Core.Tests/GameRandomTests.cs`
- Create: `dotnet/Murim.Core/Murim.Core.csproj`, `dotnet/Murim.Core.Tests/Murim.Core.Tests.csproj`

**Interfaces:**
- Produces: `Murim.Core.Random.GameRandom`
  - `GameRandom(ulong seed)`, `ulong State { get; set; }`
  - `int Next(int minInclusive, int maxExclusive)`, `double NextDouble()`, `bool Chance(double p)`
  - `T Pick<T>(IReadOnlyList<T> items)`, `void Shuffle<T>(IList<T> items)`

- [ ] **Step 1: Unity 프로젝트 생성**

임시 폴더에 만든 뒤 옮긴다 (기존 `docs/`, `.git` 보존):
```powershell
$u="C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe"
& $u -batchmode -nographics -createProject "$env:TEMP\MurimTmp" -quit | Out-Null
Move-Item "$env:TEMP\MurimTmp\Assets","$env:TEMP\MurimTmp\Packages","$env:TEMP\MurimTmp\ProjectSettings" .
```
Unity 라이선스 오류가 나면 Unity Hub에서 로그인 후 다시 실행한다.
기본 템플릿이 3D면 `ProjectSettings/EditorSettings.asset`의 `m_DefaultBehaviorMode: 1` (2D)로 바꾼다.

- [ ] **Step 2: 패키지와 .gitignore**

`Packages/manifest.json` dependencies에 `"com.unity.nuget.newtonsoft-json": "3.2.1"`, `"com.unity.test-framework"`(이미 있으면 유지), `"com.unity.ugui"`(이미 있으면 유지) 추가.
`.gitignore`: Unity 표준 (`/[Ll]ibrary/`, `/[Tt]emp/`, `/[Oo]bj/`, `/[Ll]ogs/`, `/[Uu]ser[Ss]ettings/`, `/[Bb]uild*/`, `/*.csproj`, `/*.sln`, `*.pidb`, `*.user`) + `dotnet/**/bin/`, `dotnet/**/obj/`, `TestResults/`.

- [ ] **Step 3: asmdef와 dotnet 프로젝트**

`Murim.Core.asmdef`: `name: "Murim.Core"`, `noEngineReferences: true`, `overrideReferences: true`, `precompiledReferences: ["Newtonsoft.Json.dll"]`, `autoReferenced: true`.
`Murim.Core.Tests.asmdef`: `references: ["Murim.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll", "Newtonsoft.Json.dll"]`, `includePlatforms: ["Editor"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
`dotnet/Murim.Core/Murim.Core.csproj`: `netstandard2.1`, `LangVersion 9.0`, `Nullable disable`, `<Compile Include="../../Assets/Murim.Core/**/*.cs" />`, `EnableDefaultCompileItems false`, PackageReference `Newtonsoft.Json 13.0.3`.
`dotnet/Murim.Core.Tests/Murim.Core.Tests.csproj`: `net10.0`, `LangVersion 9.0`, ProjectReference Murim.Core, `<Compile Include="../../Assets/Tests/Murim.Core.Tests/**/*.cs" />`, PackageReference `NUnit 3.14.0`, `NUnit3TestAdapter 4.6.0`, `Microsoft.NET.Test.Sdk 17.11.1`, `Newtonsoft.Json 13.0.3`.

- [ ] **Step 4: 실패하는 테스트 작성** (`GameRandomTests.cs`)

```csharp
[Test] public void SameSeed_SameSequence() {
    var a = new GameRandom(42); var b = new GameRandom(42);
    for (int i = 0; i < 1000; i++) Assert.That(a.Next(0, 1000), Is.EqualTo(b.Next(0, 1000)));
}
[Test] public void RestoredState_ContinuesSameSequence() {
    var a = new GameRandom(7); a.Next(0, 10); var saved = a.State;
    var expected = a.Next(0, 1_000_000);
    var b = new GameRandom(1) { State = saved };
    Assert.That(b.Next(0, 1_000_000), Is.EqualTo(expected));
}
[Test] public void Next_StaysInRange() {
    var r = new GameRandom(3);
    for (int i = 0; i < 10000; i++) Assert.That(r.Next(-5, 5), Is.InRange(-5, 4));
}
[Test] public void Chance_ZeroNeverOneAlways() {
    var r = new GameRandom(9);
    for (int i = 0; i < 1000; i++) { Assert.That(r.Chance(0), Is.False); Assert.That(r.Chance(1), Is.True); }
}
[Test] public void SeedZero_StillProducesVariedValues() {
    var r = new GameRandom(0);
    var set = new HashSet<int>(); for (int i = 0; i < 100; i++) set.Add(r.Next(0, 1000));
    Assert.That(set.Count, Is.GreaterThan(50));
}
```

- [ ] **Step 5: 실패 확인** — `dotnet test ... --filter GameRandomTests` → 컴파일 오류 (GameRandom 없음)

- [ ] **Step 6: `GameRandom` 구현**

생성자는 seed를 SplitMix64로 한 번 섞어 `State`에 넣는다 (0 시드 대비). `NextULong()`은 xorshift64* (`x ^= x >> 12; x ^= x << 25; x ^= x >> 27; return x * 2685821657736338717UL`). `Next`는 `(int)(NextULong() % (ulong)(max - min)) + min`, `NextDouble`은 상위 53비트 / 2^53. `Chance(p)`: `p <= 0 → false`, `p >= 1 → true`, 아니면 `NextDouble() < p`.

- [ ] **Step 7: 통과 확인** — dotnet test 통과, Unity 테스트 명령 실행 후 `result="Passed"`

- [ ] **Step 8: 커밋** — `git add -A; git commit -m "feat: Unity 프로젝트 뼈대와 결정론 난수"`

---

### Task 2: 정적 데이터 정의, 로더, 검증기

**Files:**
- Create: `Assets/Murim.Core/Data/Enums.cs`, `Definitions.cs`, `GameData.cs`, `DataLoader.cs`, `DataValidator.cs`
- Create: `Assets/Tests/Murim.Core.Tests/DataLoaderTests.cs`, `DataValidatorTests.cs`

**Interfaces:**
- Produces (namespace `Murim.Core.Data`):
  - `enum FactionType { Sect, Clan }`, `enum Alignment { Righteous, Unorthodox, Demonic }`, `enum Sex { Male, Female }`
  - `enum Role { Leader, Heir, Elder, Disciple, Family, Guest, None }`, `enum TraitCategory { Personality, Constitution }`, `enum MemoryKind { Grudge, Debt }`
  - `class StatBlock { int Constitution, Insight, Cunning, Charisma }`
  - `class RegionDef { string Id, Name; string[] Neighbors; float X, Y; }`
  - `class FactionDef { string Id, Name; FactionType Type; Alignment Alignment; string HomeRegion; int Influence, NamedMembers, UnnamedDisciples, Silver, Fame, LeaderRealm; }`
  - `class TraitDef { string Id, Name, Description; TraitCategory Category; string[] Opposites; StatBlock StatMods; int YearlyChivalry; float DeathRiskMultiplier = 1; Dictionary<string, float> ActionWeights; }`
  - `class ScenarioDef { string Id, Name, Description; bool HasFaction; FactionType FactionType; Alignment[] AllowedAlignments; int FamilyMin, FamilyMax, NamedMembers, UnnamedDisciples, Silver, Debt, Fame, Chivalry, Realm, Influence, LeaderAgeMin, LeaderAgeMax; bool StartWithGrudge, StartWithRighteousFriend, StartWithNeighborGrudge; }`
  - `class NameData { string[] FamilyNames, MaleGivenNames, FemaleGivenNames; }`
  - `class FactionNameParts { string[] Prefixes, RighteousSuffixes, UnorthodoxSuffixes; }`
  - `class GameData { IReadOnlyDictionary<string, RegionDef> Regions; IReadOnlyDictionary<string, FactionDef> Factions; IReadOnlyDictionary<string, TraitDef> Traits; IReadOnlyDictionary<string, ScenarioDef> Scenarios; NameData Names; FactionNameParts FactionNameParts; }` (키는 Ordinal 정렬된 `SortedDictionary`)
  - `class DataLoadResult { GameData Data; List<string> Errors; bool Ok => Errors.Count == 0; }`
  - `static DataLoadResult DataLoader.LoadFromDirectory(string dir)`
  - `static DataLoadResult DataLoader.LoadFromStrings(IReadOnlyDictionary<string, string> filesByName)` (테스트용, 키는 `"regions.json"` 등 파일명)
  - `static List<string> DataValidator.Validate(GameData data)`

JSON은 camelCase (`"homeRegion"`), enum은 문자열 (`"Righteous"`). Newtonsoft `CamelCasePropertyNamesContractResolver` + `StringEnumConverter`.

- [ ] **Step 1: 실패하는 테스트 작성**

`DataLoaderTests`:
- `LoadFromStrings_ValidMinimalData_Ok`: 지역 2개, 세력 1개, 특성 2개, 시나리오 1개, 이름 데이터로 `Ok == true`, `Regions["hebei"].Neighbors`에 `"shandong"` 포함, `Factions["shaolin"].Alignment == Alignment.Righteous`
- `LoadFromStrings_MissingFile_ErrorNamesFile`: `traits.json` 빠짐 → `Errors`에 `"traits.json"` 포함한 문자열
- `LoadFromStrings_BrokenJson_ErrorNamesFileAndLine`: `factions.json` = `"[{\"id\": \"a\",,}]"` → 에러 문자열에 `"factions.json"`과 `"line"` (Newtonsoft `JsonReaderException.LineNumber`) 포함
- `LoadFromStrings_DuplicateId_Error`: 같은 id 세력 2개 → 에러에 그 id 포함

`DataValidatorTests` (유효한 최소 데이터를 만들고 하나씩 망가뜨림):
- `AsymmetricNeighbor_Reported`: A.neighbors=[B], B.neighbors=[] → 에러에 `"A"`, `"B"` 포함
- `UnknownHomeRegion_Reported`: faction.homeRegion="nowhere" → 에러에 faction id와 `"nowhere"`
- `UnknownOppositeTrait_Reported`
- `RegionInfluenceOver100_Reported`: 같은 지역 세력 영향력 60 + 50 → 에러에 지역 id
- `ValidData_NoErrors`

- [ ] **Step 2: 실패 확인** — dotnet test → 컴파일 오류

- [ ] **Step 3: 구현** — 각 파일은 JSON 배열을 `List<T>`로 읽어 id 사전으로 바꾼다. `names.json`, `faction_name_parts.json`은 단일 객체. 파일별 try/catch로 예외 메시지를 `"<파일명>: <메시지> (line N)"`으로 모은다. `LoadFromDirectory`는 6개 파일을 읽어 `LoadFromStrings`에 넘긴다.

- [ ] **Step 4: 통과 확인** — dotnet test 통과

- [ ] **Step 5: 커밋** — `feat: 정적 데이터 정의·로더·검증기`

---

### Task 3: 게임 콘텐츠 데이터 작성

**Files:**
- Create: `Assets/StreamingAssets/Data/regions.json`, `factions.json`, `faction_name_parts.json`, `scenarios.json`, `traits.json`, `names.json`
- Create: `Assets/Tests/Murim.Core.Tests/Support/TestPaths.cs`, `Assets/Tests/Murim.Core.Tests/ShippedDataTests.cs`

**Interfaces:**
- Consumes: Task 2 `DataLoader`, `DataValidator`
- Produces: `static string TestPaths.DataDir` — `TestContext.CurrentContext.TestDirectory`와 `Environment.CurrentDirectory`에서 위로 올라가며 `Assets/StreamingAssets/Data`가 있는 첫 경로
- Produces: `static GameData TestPaths.LoadShippedData()` (로드 실패 시 `Assert.Fail(에러 목록)`)
- Produces (데이터 id, 이후 Task가 씀): 지역 id 13개 `hebei, shandong, henan, shanxi, shaanxi, gansu, sichuan, hubei, hunan, jiangnan, fujian, yunnan, liaodong`; 시나리오 id `minor_sect, fallen_clan, wandering_hero, ronin, black_gang`; 특성 id (아래)

- [ ] **Step 1: 실패하는 테스트 작성** (`ShippedDataTests`)

```csharp
[Test] public void ShippedData_LoadsAndValidates() {
    var d = TestPaths.LoadShippedData();
    Assert.That(DataValidator.Validate(d), Is.Empty);
}
[Test] public void Regions_Are13AndConnected() { /* Count == 13, BFS from hebei reaches all 13 */ }
[Test] public void MajorFactions_About100_WithAlignmentMix() {
    var f = TestPaths.LoadShippedData().Factions.Values;
    Assert.That(f.Count(), Is.InRange(95, 105));
    Assert.That(f.Count(x => x.Alignment == Alignment.Righteous), Is.InRange(45, 55));
    Assert.That(f.Count(x => x.Alignment == Alignment.Unorthodox), Is.InRange(30, 40));
    Assert.That(f.Count(x => x.Alignment == Alignment.Demonic), Is.InRange(12, 18));
}
[Test] public void EveryRegion_Has5To10MajorFactions_AndInfluenceAtMost80() { }
[Test] public void Traits_About20_AndOppositesAreMutual() { /* 18~24개; a.opposites∋b ⇔ b.opposites∋a */ }
[Test] public void Scenarios_AllFivePresent() { }
[Test] public void Names_EnoughForVariety() { /* family ≥ 80, male ≥ 150, female ≥ 150 */ }
[Test] public void NameParts_EnoughForRandomFactions() { /* prefixes ≥ 40, each suffix list ≥ 10 */ }
```

- [ ] **Step 2: 실패 확인** — 데이터 파일 없음으로 실패

- [ ] **Step 3: 데이터 작성**

- **regions.json**: 13개, 지도 좌표 `x, y`는 0~1 정규화 (대략 실제 중국 배치: 요동 우상단, 신강 대신 감숙이 좌측 끝, 운남 좌하단, 복건 우하단). 인접은 실제 지리 기준으로 대칭.
- **factions.json**: 스펙 3.3 ①. 정파 약 50 (구파일방 10, 오대세가 5, 그 밖 문파·세가·표국 35), 사파 약 35 (녹림총채, 장강수로채, 하오문, 흑도 방회, 사파 세가), 마도 약 15 (천마신교, 혈교와 산하, 변방 지역 감숙·운남·요동·사천 위주). 구파일방은 실제 연고지 (소림·개방 하남, 무당 호북, 화산·종남 섬서, 아미·청성 사천, 곤륜·공동 감숙, 점창 운남). 각 지역 영향력 합 ≤ 80 (무작위 약소 세력 몫 확보). 대문파 `namedMembers` 10~14, `leaderRealm` 4~5; 중소 5~8, `leaderRealm` 2~3.
- **traits.json**: 성격 `bold`(호탕)↔`sly`(음험), `hot_tempered`(다혈질)↔`coward`(겁쟁이), `greedy`(탐욕)↔`chivalrous`(의협심), `arrogant`(오만)↔`gentle`(온후), `lustful`(색욕)↔`chaste`(금욕), `loyal`(충직)↔`ambitious`(야심), `diligent`(근면)↔`lazy`(나태); 체질 `heaven_bone`(천골지체), `sickly`(병약, `deathRiskMultiplier 2.0`)↔`long_lived`(장수, `0.6`), `yin_cold`(음한체질). `chivalrous.yearlyChivalry = 3`, `greedy = -3`. `actionWeights` 키는 Task 6 `ActionKeys` 상수 문자열 + `"accept_marriage"`. 예: `greedy: {plunder: 15, gift: -10}`, `chivalrous: {plunder: -30, insult_unorthodox: 15}`, `hot_tempered: {insult: 10}`, `ambitious: {expand: 10}`, `bold: {accept_marriage: 5}`.
- **scenarios.json**: 스펙 3.4 표 수치 그대로. `minor_sect`(Sect, 정·사 선택, family 1~3, named 4, unnamed 10, silver 500, fame 10, influence 5, realm 1, age 30~45), `fallen_clan`(Clan, 정, family 4~6, named 2, silver 200, debt 800, fame 30, `startWithNeighborGrudge`), `wandering_hero`(`hasFaction false`, silver 80, fame 5, chivalry 60, realm 1, age 18~28, `startWithRighteousFriend`), `ronin`(silver 50, fame 0, chivalry 0, realm 2, age 22~35, `startWithGrudge`), `black_gang`(Sect, 사 고정, named 5, unnamed 20, silver 300, fame 5, chivalry −50, influence 5, realm 1).
- **names.json / faction_name_parts.json**: 한자 느낌 한국어 독음 (성씨 "남궁", "제갈" 같은 복성 포함). 접두어 예: 청운, 흑풍, 철혈, 백학; 정파 접미어: 검문, 문, 파, 장, 세가; 사파 접미어: 채, 방, 회, 문.

- [ ] **Step 4: 통과 확인**

- [ ] **Step 5: 커밋** — `feat: 지역·주요 세력·특성·시나리오 데이터`

---

### Task 4: 세계 상태 모델, 호감도, 영향력 규칙

**Files:**
- Create: `Assets/Murim.Core/World/*.cs`, `Rules/SimConstants.cs`, `Rules/Opinion.cs`, `Rules/Influence.cs`, `Rules/FactionOps.cs`, `Text/Josa.cs`, `Text/GameDate.cs`, `Chronicle/Chronicle.cs`
- Create: `Assets/Tests/Murim.Core.Tests/Support/TestWorlds.cs`, `OpinionTests.cs`, `InfluenceTests.cs`, `JosaTests.cs`, `MemoryTests.cs`

**Interfaces:**
- Produces (namespace `Murim.Core.World`):
  - `class Character { string Id, FamilyName, GivenName; Sex Sex; int BirthMonth; int? DeathMonth; string DeathCause; string FactionId /*null=무소속*/; Role Role; string FatherId, MotherId, SpouseId; List<string> ChildIds; int Realm; int Constitution, Insight, Cunning, Charisma; List<string> TraitIds; List<Memory> Memories; string LocationRegionId; int Silver, Fame, Chivalry /*무소속일 때 개인 자원*/; int BusyUntilMonth; string FullName => FamilyName + GivenName; bool IsAlive => DeathMonth == null; int AgeYears(int month) => (month - BirthMonth) / 12; }`
  - `class Faction { string Id, Name; FactionType Type; Alignment Alignment; int Chivalry; string HomeRegionId; string LeaderId, HeirId; List<string> MemberIds; int UnnamedDisciples, Silver, Fame, Debt; bool IsDissolved; bool IsRandomMinor; }`
  - `class RegionState { string Id; int Stability; SortedDictionary<string,int> Influence; }`
  - `class Memory { MemoryKind Kind; string TargetId, Reason; int Month, Strength; }`
  - `class OpinionStore { int GetStored(string from, string to); void Add(string from, string to, int delta); }` — 키 `from + "|" + to`, 0이 되면 삭제, `SortedDictionary<string,int> Entries` 공개 (직렬화용)
  - `class ChronicleEntry { int Month; string Kind /*Task 8 템플릿 메서드 이름, 예 "Married"*/; string Text; string[] InvolvedIds; int Importance; }`
  - `enum RelationTag { Master, Disciple, Lover, Spouse, Rival, Nemesis, Benefactor, SwornSibling }`, `Character.Relations : SortedDictionary<string, List<RelationTag>>` (상대 id → 태그)
    - `Opinion.AddMemory`가 원한 강도 합 ≥ 60이면 `Nemesis`, 은혜 강도 합 ≥ 40이면 `Benefactor` 태그를 붙인다
    - 제자 영입·생성 시 장문인↔제자 `Master`/`Disciple`, `Marry` 시 `Spouse` (Task 5·6이 설정)
    - `Lover`, `Rival`, `SwornSibling`은 1단계에서 정의만 하고 2단계 이벤트가 붙인다
  - `class WorldState { int Month; ulong RngState; string PlayerCharacterId; SortedDictionary<string,Character> Characters; SortedDictionary<string,Faction> Factions; SortedDictionary<string,RegionState> Regions; OpinionStore Opinions; SortedDictionary<string,int> AllianceScores; SortedSet<string> Alliances; List<ChronicleEntry> Chronicle; int NextCharacterNumber, NextFactionNumber; bool GameOver; int SaveVersion = 1; string NewCharacterId(); string NewFactionId(); }`
    - `NewCharacterId()` → `"c" + n.ToString("D6")`, `NewFactionId()` → `"f" + n.ToString("D4")`. 주요 세력은 `factions.json` id를 그대로 쓴다.
    - 세력 쌍 키: `static string WorldState.PairKey(string a, string b)` = 사전순 작은 것 `|` 큰 것
- Produces (namespace `Murim.Core.Rules`):
  - `static class SimConstants` — 스펙의 모든 수치를 이름 붙은 상수로 (예: `MarriageMinAge = 16`, `FertileMaxAge = 45`, `MonthlyBirthChance = 0.015`, `MemoryDecayPercent = 10`, `MemoryMinStrength = 10`, `MemoryInheritThreshold = 50`, `AlignmentShiftThreshold = 30`, `DesertionChance = 0.25`, `DesertionOpinion = -50`, `RealmLifespanYears = 8`, `TraitInheritChance = 0.4`, `ClanMarriageBonus = 15`, `SameTraitOpinion = 10`, `OppositeTraitOpinion = -10`, `GuestStipend = 20`, `DebtInterestPercent = 10`, `DebtUnrestThreshold = 3000` 등)
  - `static int Opinion.Of(WorldState w, GameData d, string fromId, string toId)` = clamp(−100, 100, 특성 상성 + `Opinions.GetStored` + Σ(from의 기억 중 TargetId==toId: Grudge −Strength, Debt +Strength/2))
    - 특성 상성: from·to가 같은 특성 하나당 +10, from의 특성이 to 특성의 opposite면 하나당 −10
  - `static void Opinion.AddMemory(Character c, MemoryKind kind, string targetId, string reason, int month, int strength)`
  - `static void Opinion.DecayMemories(Character c)` — strength × 90% 내림, 10 미만 제거
  - `static void Opinion.InheritMemories(Character deceased, IEnumerable<Character> children, int month)` — strength ≥ 50만 절반 강도로
  - `static int Influence.Get(WorldState w, string regionId, string factionId)`
  - `static int Influence.Gain(WorldState w, string regionId, string factionId, int amount)` — 남은 몫(100 − 합)에서 먼저, 모자라면 그 지역 영향력 1위 경쟁 세력(자신 제외, 동률이면 id 사전순)에서 빼앗음. 실제 증가량 반환. 합 ≤ 100 보장
  - `static void Influence.Lose(WorldState w, string regionId, string factionId, int amount)` — 0 아래로 안 감, 0이면 항목 삭제
  - `static int Influence.Total(WorldState w, string factionId)`
  - `static Character FactionOps.Leader(WorldState w, Faction f)`
  - `static void FactionOps.MoveToFaction(WorldState w, Character c, string factionIdOrNull, Role role)` — 이전 세력 `MemberIds`에서 제거, 새 세력에 추가, 이전 세력의 `HeirId`였으면 비움
  - `static void FactionOps.Dissolve(WorldState w, Faction f, string reason)` — 살아 있는 멤버 무소속(`Role.None`, 위치 = 본거지), 모든 지역 영향력 삭제, 동맹 삭제, `IsDissolved = true`, 연대기 기록
- Produces: `static class Josa { string IGa(string word); string EunNeun(string word); string EulReul(string word); string WaGwa(string word); }` — 단어+조사 반환 (예: `IGa("장삼") == "장삼이"`, `IGa("무당파") == "무당파가"`). 한글 음절 아니면 `"이(가)"` 식 병기
- Produces: `static class GameDate { int Year(int month) => month / 12 + 1; int MonthOfYear(int month) => month % 12 + 1; string Format(int month) => $"{Year}년 {MonthOfYear}월"; }`
- Produces: `static void Chronicle.Add(WorldState w, string kind, int importance, string text, params string[] involvedIds)` — `"{GameDate.Format}, "` 접두는 View에서 붙이고 Core는 본문만 저장. importance 0 = 낮음, 1 = 보통, 2 = 높음
- Produces (테스트 지원): `TestWorlds.MinimalData()` — 지역 3개(a↔b↔c), 특성 4개(`bold`↔`sly`, `ambitious`, `sickly`), 시나리오 없음; `TestWorlds.Empty(GameData d)` — 지역 상태만 있는 `WorldState`; `TestWorlds.AddFaction(w, id, alignment, region, influence)`; `TestWorlds.AddCharacter(w, factionId, role, sex, age, params string[] traits)` (현재 월 기준 나이로 BirthMonth 역산, 능력치 10)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
// OpinionTests
[Test] public void SharedTrait_Plus10_OppositeTrait_Minus10() {
    // a: bold, b: bold → 10 ; a: bold, c: sly → -10
}
[Test] public void GrudgeMemory_Subtracts_DebtMemory_AddsHalf() {
    // AddMemory(a, Grudge, b, ..., 40) → Of(a,b) == -40 ; 추가로 Debt 30 → -40 + 15 = -25
}
[Test] public void Opinion_ClampedTo100() { /* stored +500 → 100 */ }
// MemoryTests
[Test] public void Decay_TakesTenPercent_AndRemovesBelow10() { /* 40→36, 10→9→제거 */ }
[Test] public void StrongGrudge_AddsNemesisTag_StrongDebt_AddsBenefactorTag() { /* Grudge 60 → Nemesis, Debt 40 → Benefactor */ }
[Test] public void Inherit_OnlyStrong_AtHalfStrength() { /* 60→자식 30, 40→안 물려줌 */ }
// InfluenceTests
[Test] public void Gain_UsesFreeShareFirst() { /* 지역 합 70, Gain(x, 10) → x +10, 다른 세력 그대로 */ }
[Test] public void Expand_FullRegion_TakesFromStrongestAndNeverExceeds100() {
    // a:60, b:40, x:0 → Gain(x, 8) == 8, a == 52, b == 40, 합 == 100
}
[Test] public void Lose_NeverBelowZero_RemovesEntry() { }
// JosaTests
[Test] public void IGa_ByFinalConsonant() {
    Assert.That(Josa.IGa("장삼"), Is.EqualTo("장삼이"));
    Assert.That(Josa.IGa("무당파"), Is.EqualTo("무당파가"));
    Assert.That(Josa.EulReul("남궁세가"), Is.EqualTo("남궁세가를"));
    Assert.That(Josa.WaGwa("소림"), Is.EqualTo("소림과"));
}
// FactionOps (InfluenceTests 파일에 같이)
[Test] public void Dissolve_ReleasesInfluenceAndMembers() { }
```

- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인**
- [ ] **Step 5: 커밋** — `feat: 세계 상태 모델, 호감도·기억·영향력 규칙`

---

### Task 5: 인물·세계 생성과 시나리오 시작

**Files:**
- Create: `Assets/Murim.Core/Generation/CharacterGenerator.cs`, `WorldGenerator.cs`, `ScenarioSetup.cs`, `Assets/Murim.Core/Session/NewGameOptions.cs`
- Create: `Assets/Tests/Murim.Core.Tests/CharacterGeneratorTests.cs`, `WorldGeneratorTests.cs`, `ScenarioTests.cs`

**Interfaces:**
- Consumes: Task 4 전부
- Produces:
  - `class NewGameOptions { string ScenarioId, RegionId; ulong Seed; Sex Sex; string FamilyName, GivenName /*null이면 무작위*/; string FactionName /*null이면 무작위*/; Alignment Alignment; List<string> ChosenTraitIds /*최대 2*/; }`
  - `class CharacterGenerator { CharacterGenerator(GameData d, GameRandom rng); Character Create(WorldState w, Sex sex, int ageYears, string factionId, Role role, int realm, string familyName = null); Character CreateChild(WorldState w, Character mother, Character father); void Marry(WorldState w, Character a, Character b); }`
    - 능력치 각 `rng.Next(1, 15)` + 특성 `StatMods`, 1~20 clamp. 특성 2~4개, 서로 opposite 아님
    - `CreateChild`: 능력치 = 부모 평균 ± `rng.Next(-4, 5)`; `TraitInheritChance`(0.4) 확률로 부모 특성 중 하나 상속, 나머지 무작위로 총 2~3개; 성 = 아버지 성; 세력 = 어머니 세력, Role = Family; 부모 `ChildIds`에 추가; 위치 = 어머니 위치
    - `Marry`: 양쪽 `SpouseId` 설정
  - `class WorldGenerator { static WorldState Generate(GameData d, GameRandom rng, string playerRegionId, int playerScaleNamed); }`
    - 주요 세력: `FactionDef` 수치로 `Faction` 생성. 인물: 장문인(나이 40~70, realm = `LeaderRealm`), 장로 `NamedMembers / 4`명(realm − 1), 후계자 1명(Role.Heir, `HeirId` 지정), 나머지 제자. 장문인 60% 확률로 배우자(같은 세력, Role.Family) + 자식 0~3명. 세가는 제자 대신 가족 비중 높게 (멤버 절반 이상 Family, 성 = 장문인 성)
    - 무작위 약소 세력: 지역마다 `rng.Next(1, 4)`개, 정·사 50%. 이름 = 접두어 + 성향별 접미어, 기존 이름과 겹치면 다시 뽑음. 규모 named 3~6, unnamed 5~20, influence 2~8 (`Influence.Gain`으로 할당). `IsRandomMinor = true`
    - `playerRegionId`가 null이 아니면 그 지역에 체급이 비슷한 정파 1, 사파 1을 **추가로 반드시** 생성 (named = `playerScaleNamed` ± 1, unnamed 8~15, influence 3~6)
    - 모든 인물 `LocationRegionId` = 소속 세력 본거지
  - `static class ScenarioSetup { static Character Apply(WorldState w, GameData d, GameRandom rng, NewGameOptions o); }` — 플레이어 인물 생성, `w.PlayerCharacterId` 설정, 플레이어 인물 반환
    - `HasFaction`: 새 세력 (`NewFactionId`, 이름 = `o.FactionName` 또는 무작위, `Alignment` = 시나리오 허용 목록 중 `o.Alignment`(없으면 첫 번째), Chivalry = 시나리오 값이 0이면 성향별 시작값 정 +50/사 −50/마 −80), 장문인 = 플레이어, 가족 `FamilyMin~FamilyMax`, 제자 `NamedMembers`, 수치 그대로, `Influence.Gain(region, Influence)`
    - 무소속: 플레이어 `Silver/Fame/Chivalry` = 시나리오 값, `Role.None`, 위치 = 시작 지역
    - `StartWithNeighborGrudge`: 같은 지역(없으면 인접 지역) 다른 세력 장문인 1명과 서로 원한 (플레이어→상대 60, 상대→플레이어 30), 이유 `"선대의 원한"`
    - `StartWithRighteousFriend`: 시작 지역 정파 인물 1명과 양방향 저장 호감도 +40
    - `StartWithGrudge`: 무작위 살아 있는 인물 1명에게 원한 60, 이유 `"과거의 원한"`
    - `ChosenTraitIds`는 앞 2개만, opposite끼리면 두 번째 무시
  - `static class SimSetup { static WorldState NewWorld(GameData d, NewGameOptions o, out GameRandom rng); }` — `rng = new GameRandom(o.Seed)`, `WorldGenerator.Generate(d, rng, o.RegionId, scenario.NamedMembers + scenario.FamilyMax)` 후 `ScenarioSetup.Apply`. (Task 10의 `GameSession.NewGame`이 이를 감쌈)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
// CharacterGeneratorTests
[Test] public void Create_StatsInRange_TraitsNotOpposed() { /* 500명 생성, 능력치 1~20, 특성 2~4, opposite 쌍 없음 */ }
[Test] public void CreateChild_InheritsFamilyNameAndFaction_LinksParents() { }
[Test] public void CreateChild_TraitInheritanceRate_Near40Percent() {
    // 부모에게만 있는 특성 1개씩 부여, 2000번 → 상속률 0.30~0.50 (무작위 채움으로 우연히 같은 특성이 뽑히는 몫 포함)
}
// WorldGeneratorTests (출하 데이터 사용)
[Test] public void Generate_AllMajorFactionsPresent_WithLeaderAndHeir() { }
[Test] public void Generate_EveryRegionHasRandomMinorFaction_AndInfluenceAtMost100() { }
[Test] public void Generate_PlayerRegion_HasMatchingRighteousAndUnorthodoxRival() {
    // playerRegion=henan, scale=7 → IsRandomMinor && 정파 && named 6~8 인 세력 ≥1, 사파도 ≥1
}
[Test] public void Generate_SameSeed_SameWorld() { /* JSON 직렬화 문자열 비교 */ }
// ScenarioTests
[TestCaseSource(nameof(AllScenarioRegionPairs))]  // 5 시나리오 × 13 지역
public void Scenario_StartsInEveryRegion(string scenarioId, string regionId) {
    // 예외 없음, PlayerCharacterId 존재, 지역 영향력 합 ≤ 100
}
[Test] public void MinorSect_MatchesSpecNumbers() { /* 은자 500, 명성 10, 이름 없는 제자 10, 영향력 5, named 제자 4 + 가족 1~3 */ }
[Test] public void FallenClan_HasDebtAndNeighborGrudge() { /* Debt 800, 플레이어 원한 기억 1개 강도 60 */ }
[Test] public void WanderingHero_NoFaction_PersonalResources() { /* FactionId null, Silver 80, Fame 5, Chivalry 60, Realm 1, 정파 친구 호감도 ≥ 40 */ }
[Test] public void Ronin_HasPastGrudge() { }
[Test] public void BlackGang_IsUnorthodox_With20Thugs() { }
[Test] public void ChosenTraits_AppliedMax2_OppositeIgnored() { }
```

- [ ] **Step 2~4: 실패 확인 → 구현 → 통과**
- [ ] **Step 5: 커밋** — `feat: 인물·세계 생성과 다섯 시나리오`

---

### Task 6: 행동 틀과 세력 행동

**Files:**
- Create: `Assets/Murim.Core/Actions/IGameAction.cs`, `ActionBudget.cs`, `Actions/Faction/ProposeMarriage.cs`, `ArrangeInternalMarriage.cs`, `ProposeAlliance.cs`, `SendGift.cs`, `Insult.cs`, `RecruitNamed.cs`, `RecruitUnnamed.cs`, `Dismiss.cs`, `DesignateHeir.cs`, `ExpandInfluence.cs`, `DoQuest.cs`, `RepayDebt.cs`, `Actions/MarriageRules.cs`
- Create: `Assets/Tests/Murim.Core.Tests/FactionActionTests.cs`, `MarriageRulesTests.cs`

**Interfaces:**
- Consumes: Task 4, Task 5 `CharacterGenerator`
- Produces (namespace `Murim.Core.Actions`):
  - `readonly struct ActionCheck { bool Ok; string Reason; static ActionCheck Pass; static ActionCheck Fail(string reason); }`
  - `interface IGameAction { string ActorId { get; } string Key { get; } ActionCheck CanExecute(WorldState w, GameData d); void Execute(WorldState w, GameData d, GameRandom rng); string Describe(WorldState w); }` — `ActorId` = 행동하는 인물 id (세력 행동은 장문인). `Execute`는 `CanExecute`가 Ok일 때만 호출된다고 가정
  - `static class ActionKeys` — `Marriage="marriage", InternalMarriage="internal_marriage", Alliance="alliance", Gift="gift", Insult="insult", RecruitNamed="recruit_named", RecruitUnnamed="recruit_unnamed", Dismiss="dismiss", DesignateHeir="designate_heir", Expand="expand", Quest="quest", Plunder="plunder", RepayDebt="repay_debt", Travel="travel", QuestChivalrous="quest_chivalrous", QuestContract="quest_contract", Escort="escort", Spar="spar", Befriend="befriend", JoinAsGuest="join_guest", FoundSect="found_sect", LeaveFaction="leave_faction"`
  - `static int ActionBudget.Max(Character actor) => 2 + actor.Cunning / 8`
  - `static class MarriageRules`
    - `static int AcceptanceScore(WorldState w, GameData d, Character proposerHead, Character targetHead, int proposerFame, int targetFame, Alignment? proposerAlign, Alignment? targetAlign, bool targetIsClan)` = `Opinion.Of(targetHead→proposerHead)` + (proposerFame − targetFame)/10 + 성향 보너스(같으면 +20, 한쪽 정·다른쪽 사/마면 −40, 그 외 0, 한쪽이 무소속(null)이면 0) + (targetIsClan ? 15 : 0) + Σ targetHead 특성 `ActionWeights["accept_marriage"]`
    - `static ActionCheck Eligible(WorldState w, Character a, Character b)` — 둘 다 살아 있음, 16세 이상, 미혼(배우자가 없거나 배우자가 사망), 이성, 직계(부모·자식·형제) 아님
  - `ProposeMarriage(string actorId, string ourMemberId, string theirMemberId, bool spouseJoinsUs)` — 비용 은자 50 (행위자 세력, 무소속이면 개인). 대상이 다른 세력 소속이면 그 세력 장문인 판정; 대상이 무소속이면 `Opinion.Of(대상→우리 쪽 인물) ≥ 30`. 우리 쪽 인물이 행위자 자신일 수 있음 (떠돌이 혼인). 수락: `CharacterGenerator.Marry`, 배우자 이적(`spouseJoinsUs`면 상대가 우리 세력으로, Role.Family), 두 장문인 저장 호감도 양방향 +20, `AllianceScores[pair]` +15, 연대기. 거절: 행위자에게 원한 30 (`"혼담 거절"`), 연대기
  - `ArrangeInternalMarriage(actorId, aId, bId)` — 같은 세력 두 멤버, 비용 없음, 장문인만 가능
  - `ProposeAlliance(actorId, targetFactionId)` — 명성 5, `AllianceScores[pair] ≥ 30`이면 `Alliances`에 추가 + 연대기, 아니면 명성만 소모하고 "거절"
  - `SendGift(actorId, targetFactionId, int silver)` — silver 30~200, 상대 장문인 저장 호감도 + silver/10, 상대 장문인에게 Debt 기억 (강도 10 + silver/10, 이유 `"선물"`), AllianceScore +5
  - `Insult(actorId, targetFactionId)` — 상대 장문인 저장 호감도 −30 + Grudge 40 (`"모욕"`), AllianceScore −20, 행위자 세력이 사파면 명성 +2
  - `RecruitNamed(actorId)` — 은자 20. 같은 지역 무소속 성인이 있으면 그중 매력 차 가장 작은 1명을 우선 영입, 없으면 새로 생성: 나이 14~25, realm 0, 각 능력치 `rng.Next(1, 12) + 장문인 매력 / 4`
  - `RecruitUnnamed(actorId, int count)` — 은자 5×count, 상한 `Influence.Total × 5 + 10`
  - `Dismiss(actorId, memberId)` — 명성 −3 (0 아래 허용 안 함, 0에서 멈춤), 대상 무소속 + 대상→장문인 Grudge 40 (`"파문"`), 연대기
  - `DesignateHeir(actorId, memberId)` — `HeirId` 설정, 대상 Role.Heir (이전 후계자는 원래 역할로 Disciple/Family), 나머지 성인 멤버 각자 → 장문인 저장 호감도 −10 + Grudge 20 (`"후계 탈락"`)
  - `ExpandInfluence(actorId, regionId)` — 은자 100, 본거지 또는 이미 영향력 있는 지역 또는 그 인접 지역만, `Influence.Gain(3 + 장문인 심계 × 5 / 20)` (3~8), 그 지역 다른 세력 장문인 → 행위자 저장 호감도 −5
  - `DoQuest(actorId, bool plunder)` — 일반: 은자 +`rng.Next(30, 81)`, 정파면 Chivalry +2. 약탈: 은자 ×2, Chivalry −5, 본거지 Stability −5
  - `RepayDebt(actorId, int amount)` — Debt와 Silver에서 차감 (amount ≤ min(Debt, Silver))
  - 세력 행동 공통 `CanExecute`: 행위자 살아 있음, 행위자가 세력 장문인, 세력 미해산, 자원 충분, 대상 존재·생존·자기 세력 아님(해당 시). 실패 사유는 한국어 (`"은자가 부족합니다"`)

- [ ] **Step 1: 실패하는 테스트 작성** (TestWorlds 사용)

```csharp
// MarriageRulesTests
[Test] public void Score_FollowsSpecFormula() {
    // 호감도 10, 명성 50 vs 20, 같은 정파, 상대 세가, 특성 보정 0 → 10 + 3 + 20 + 15 == 48
}
[Test] public void Score_RighteousVsUnorthodox_Minus40() { }
[Test] public void Eligible_RejectsMinorMarriedSameSexAndSiblings() { }
// FactionActionTests
[Test] public void ProposeMarriage_Accepted_MarriesMovesSpouseRaisesOpinionAndAlliance() { }
[Test] public void ProposeMarriage_Rejected_AddsGrudge30ToProposer() { }
[Test] public void ProposeMarriage_CostsSilver50_FailsWhenPoor() { }
[Test] public void Alliance_RequiresScore30() { }
[Test] public void Gift_RaisesOpinionBySilverOver10_AndAddsDebtMemory() { /* 100 은자 → 저장 +10, Debt 강도 20 */ }
[Test] public void Insult_Minus30AndGrudge40_UnorthodoxGainsFame2() { }
[Test] public void RecruitUnnamed_RespectsInfluenceCap() { }
[Test] public void Dismiss_MakesUnaffiliatedWithGrudge() { }
[Test] public void DesignateHeir_PassedOverMembersResent() { }
[Test] public void ExpandInfluence_GainScalesWithCunning() { /* 심계 0 → 3, 심계 20 → 8 */ }
[Test] public void Expand_NotAdjacentRegion_CannotExecute() { }
[Test] public void Plunder_DoublesSilverLowersChivalryAndStability() { }
[Test] public void ActionBudget_TwoPlusCunningOver8() { /* 심계 7 → 2, 8 → 3, 20 → 4 */ }
[Test] public void NonLeader_CannotUseFactionActions() { }
```

`QueuedAction_TargetMarriedMeanwhile_SkippedWithChronicle`는 Task 10 `GameSession` 테스트에서 작성한다 (예약 큐가 그 Task 소유).

- [ ] **Step 2~4: 실패 확인 → 구현 → 통과**
- [ ] **Step 5: 커밋** — `feat: 행동 틀과 세력 행동`

---

### Task 7: 떠돌이·개인 행동

**Files:**
- Create: `Assets/Murim.Core/Actions/Personal/Travel.cs`, `PersonalQuest.cs`, `Escort.cs`, `Spar.cs`, `Befriend.cs`, `JoinAsGuest.cs`, `FoundSect.cs`, `LeaveFaction.cs`, `Actions/Personal/PersonalRules.cs`
- Create: `Assets/Tests/Murim.Core.Tests/PersonalActionTests.cs`

**Interfaces:**
- Consumes: Task 4, Task 6 `IGameAction`, `ActionCheck`, `ActionKeys`, `ActionBudget`
- Produces:
  - `static bool PersonalRules.UsesPersonalActions(WorldState w, Character c)` — 무소속, 또는 Role이 Guest/Elder/Disciple/Family/Heir (장문인이 아님)
  - `static int PersonalRules.SparScore(Character a, Character b, int roll)` = (a.Realm − b.Realm) × 25 + (a.Constitution + a.Insight − b.Constitution − b.Insight) + roll; roll = `rng.Next(-20, 21)`, 결과 > 0이면 a 승리
  - `Travel(actorId, toRegionId)` — 인접 지역만, `LocationRegionId` 변경 (예산 처리는 아래 참고)
  - `interface IWholeMonthAction { }` 표식 — Travel, Escort가 구현
  - `PersonalQuest(actorId, bool contract)` — 은자 +`rng.Next(20, 61)` (청부면 ×1.5 내림), 협행: Chivalry +3, Fame +1; 청부: Chivalry −4. 무소속이면 개인 자원, 객경 등 세력 소속이면 개인 자원 (세력 금고 아님)
  - `Escort(actorId, toRegionId)` — 인접 지역, 은자 +80, 위치 이동, `BusyUntilMonth = w.Month + 2` (다음 달 행동 불가)
  - `Spar(actorId, targetId)` — 같은 지역 살아 있는 성인, 승리: Fame +`rng.Next(2, 6)`, 상대→행위자 저장 호감도 +10; 패배: Fame −1 (0 하한), 상대→행위자 −10. 연대기 (중요도 낮음)
  - `Befriend(actorId, targetId)` — 은자 10, 같은 지역, 대상→행위자 저장 호감도 +min(20, 10 + 매력/2)
  - `JoinAsGuest(actorId, factionId)` — 무소속만, 세력 본거지 = 현재 위치, `Opinion.Of(장문인→행위자) ≥ 20 || 행위자.Fame ≥ 20`이면 Role.Guest로 이적, 아니면 실패 판정만 (비용 없음, 연대기 "거절")
  - `FoundSect(actorId, string name)` — 무소속만, 은자 1000, 명성 30, 같은 지역 무소속 생존 인물 중 `Opinion.Of(그 인물→행위자) ≥ 40`인 사람 2명 이상. 효과: 새 세력 (Sect, 성향 = Chivalry ≥ 0 ? 정 : 사, Chivalry·Silver(−1000 후 잔액)·Fame = 개인 값 이전, 본거지 = 현재 지역), 행위자 장문인, 따르는 인물 전원 Disciple로 이적, `Influence.Gain(3)`, 개인 자원 0으로, 연대기 중요도 높음
  - `LeaveFaction(actorId)` — 장문인이 아닌 플레이어가 세력을 떠나 무소속이 됨 (이전 장문인 → 행위자 저장 호감도 −20)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Test] public void Travel_OnlyAdjacent_MovesLocation() { }
[Test] public void SparScore_FollowsSpecFormula() {
    // a: realm 2, con 10, ins 10 / b: realm 1, con 12, ins 12, roll -20 → 25 - 4 - 20 = 1 > 0 승리
}
[Test] public void Spar_WinRaisesFame_LossLowersFameNotBelowZero() { }
[Test] public void ContractQuest_PaysOneAndHalf_LowersChivalry4() { }
[Test] public void ChivalrousQuest_RaisesChivalry3AndFame1() { }
[Test] public void Escort_PaysAndBlocksNextMonth() { }
[Test] public void Befriend_ScalesWithCharismaCappedAt20() { /* 매력 4 → +12, 매력 20 → +20 */ }
[Test] public void JoinAsGuest_AcceptsByOpinionOrFame() { }
[Test] public void FoundSect_ConditionsMet_CreatesFactionWithFollowers() { }
[Test] public void FoundSect_MissingFollowers_CannotExecute() { }
[Test] public void FoundSect_FullRegion_KeepsSumAtMost100() { }
[Test] public void FoundSect_AlignmentFromChivalry() { }
```

`Travel`·`Escort` 예산 처리: `IGameAction`에는 예산 정보를 넣지 않는다. `GameSession.Queue`가 `IWholeMonthAction`이면 "이번 달 예약 0개일 때만 허용, 예약 후 남은 예산 0"으로 처리하며, 그 테스트는 Task 10 `Travel_WithOtherPending_Rejected_AndConsumesWholeBudget`이 소유한다.

- [ ] **Step 2~4: 실패 확인 → 구현 → 통과**
- [ ] **Step 5: 커밋** — `feat: 떠돌이·개인 행동과 문파 창설`

---

### Task 8: 턴 처리와 생애 단계 (출생·사망·후계·연간·성향·수입)

**Files:**
- Create: `Assets/Murim.Core/Simulation/ITurnPhase.cs`, `TurnProcessor.cs`, `Simulation/Phases/BirthPhase.cs`, `DeathPhase.cs`, `SuccessionPhase.cs`, `YearlyPhase.cs`, `AlignmentPhase.cs`, `IncomePhase.cs`
- Modify: `Assets/Murim.Core/Chronicle/Chronicle.cs` (문장 템플릿 추가)
- Create: `Assets/Tests/Murim.Core.Tests/LifePhaseTests.cs`, `SuccessionTests.cs`, `TurnProcessorTests.cs`

**Interfaces:**
- Consumes: Task 4, 5 (`CharacterGenerator.CreateChild`), 6
- Produces:
  - `interface ITurnPhase { void Run(TurnContext ctx); }`
  - `class TurnContext { WorldState World; GameData Data; GameRandom Rng; CharacterGenerator Generator; bool IgnoreGameOver /*테스트 전용, 기본 false*/; }`
  - `class TurnProcessor { TurnProcessor(params ITurnPhase[] phases); static TurnProcessor CreateDefault(IEnumerable<ITurnPhase> actionPhases); void Advance(TurnContext ctx); }` — `CreateDefault`는 [actionPhases…, Birth, Death, Succession, Yearly(1월만), Alignment, Income] 순서. `Advance` 끝에 `World.Month++`. `GameOver && !IgnoreGameOver`면 아무것도 안 함
  - `static double DeathPhase.MonthlyDeathChance(int ageYears, int realm, float traitMultiplier)` — 유효 나이 e = age − realm × 8; 연간 위험 h = 0.0003 × exp(0.1 × e), 1.0 상한; 월 확률 = h / 12 × traitMultiplier + 0.0004 (사고)
    - 원인: 사고 몫(0.0004)에 해당하면 `"사고"`, 아니면 e ≥ 55면 `"노환"`, 미만이면 `"병사"`
  - 사망 처리: `DeathMonth`, `DeathCause`, `Opinion.InheritMemories`(자식에게), 배우자 `SpouseId`는 유지 (Task 6 `Eligible`이 사망한 배우자를 미혼으로 보므로 재혼 가능), 장문인이면 `SuccessionPhase`가 처리하도록 표시만, 연대기
  - `static Character SuccessionPhase.PickSuccessor(WorldState w, Faction f)` — `HeirId` 살아 있고 멤버면 그 사람; 아니면 문파: 살아 있는 이름 있는 멤버(Guest 제외) 중 Realm×3 + Cunning + Charisma 최대 (동률 id 사전순); 세가: 죽은 장문인의 살아 있는 자식 중 최연장 → 없으면 문파 규칙
  - 계승: 새 장문인 Role.Leader, `HeirId` 비움, 다른 멤버 중 `Opinion.Of(멤버→새 장문인) ≤ −50 && 야심(ambitious)`이면 25% 이탈(무소속, 연대기). 후보 없음 → `FactionOps.Dissolve`
  - **플레이어 승계**: 플레이어 인물이 죽으면 — 세력 장문인이었으면 그 세력 새 장문인이 플레이어; 세력이 해산되거나 무소속/비장문인이었으면 살아 있는 자식 중 최연장(나이 무관) → 없으면 `GameOver = true`. 새 플레이어 자식이 무소속이면 부모의 개인 은자를 물려받음
  - `YearlyPhase` (Month % 12 == 0): 모든 생존 인물 `DecayMemories`; 세력 Chivalry += 장문인 특성 `YearlyChivalry` 합, 무소속 인물은 개인 Chivalry에; 경지 상승: 15~60세, realm < 6, 확률 (Constitution + Insight) / 200 × 0.5^realm; 빚 이자 Debt += Debt × 10 / 100, Debt > 3000이면 가족 멤버 → 장문인 저장 호감도 −10; 객경 승격: Role.Guest이고 `Opinion.Of(장문인→객경) ≥ 60`이면 Role.Elder; 명성: Fame += sign(Influence.Total × 2 − Fame)
  - `AlignmentPhase`: 정파 Chivalry < −30 → 사파, 사파 > +30 → 정파, 마도 불변. 연대기
  - `IncomePhase`: 세력 Silver += Influence.Total × 2 + UnnamedDisciples; 객경은 개인 Silver += 20 (세력 Silver에서 차감, 부족하면 지급 안 함)
  - `BirthPhase`: 살아 있는 기혼 여성 16~45세, 배우자 생존 → `MonthlyBirthChance` 확률로 `CreateChild`, 연대기 (플레이어 세력이면 중요도 높게)
  - 연대기 문장 (Chronicle.cs 정적 메서드): `Born`, `Died`, `Married`, `MarriageRejected`, `Allied`, `Succeeded`, `Deserted`, `AlignmentShifted`, `Dismissed`, `Dissolved`, `ActionSkipped`. 예: `Died` → `"{세력명} {직함} {Josa.IGa(이름)} {원인}(으)로 세상을 떠났다."` (원인별 조사는 고정 문장으로: "노환으로", "병으로", "사고로")

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
// LifePhaseTests
[Test] public void DeathChance_RisesWithAge_RealmShiftsBy8Years() {
    Assert.That(DeathPhase.MonthlyDeathChance(70, 0, 1), Is.GreaterThan(DeathPhase.MonthlyDeathChance(40, 0, 1)));
    Assert.That(DeathPhase.MonthlyDeathChance(68, 1, 1), Is.EqualTo(DeathPhase.MonthlyDeathChance(60, 0, 1)).Within(1e-12));
}
[Test] public void MeanLifespan_Realm0_About60() {
    // 고정 시드로 2000명 시뮬(0세부터, 월 루프) → 평균 사망 나이 55~66
}
[Test] public void SicklyTrait_DoublesRisk() { }
[Test] public void Birth_OnlyMarriedFertileWomen() { /* 50세 기혼, 30세 미혼 → 1000달 동안 출생 0 */ }
[Test] public void Yearly_DecaysMemories_OnlyInJanuary() { }
[Test] public void Yearly_ChivalrousLeader_RaisesFactionChivalry3() { }
[Test] public void Yearly_DebtInterest10Percent() { /* 800 → 880 */ }
[Test] public void Yearly_GuestWithHighOpinion_BecomesElder() { }
[Test] public void Alignment_RighteousBelowMinus30_BecomesUnorthodox_DemonicNeverShifts() { }
[Test] public void Alignment_BetweenThresholds_Unchanged() { /* 정파 −20 → 정파 유지 */ }
[Test] public void Income_InfluenceTimes2PlusUnnamed() { /* 영향력 합 10, 제자 15 → +35 */ }
// SuccessionTests
[Test] public void Succession_DesignatedHeirFirst() { }
[Test] public void Succession_SectPicksBestScore() { }
[Test] public void Succession_ClanPicksEldestChild() { }
[Test] public void Succession_AmbitiousHater_MayDesert() { /* 시드 고정, 1000회 반복 시 이탈률 0.20~0.30 */ }
[Test] public void Succession_NoCandidate_DissolvesFaction() { /* 멤버 무소속, 영향력 0, IsDissolved, 연대기에 해산 */ }
[Test] public void PlayerDeath_AsLeader_HeirBecomesPlayer() { }
[Test] public void PlayerDeath_MinorChild_BecomesPlayer() { /* 무소속 플레이어, 10세 자식 → PlayerCharacterId == 자식, GameOver false, 자식 Silver == 부모 Silver */ }
[Test] public void PlayerDeath_NoOneLeft_GameOver() { }
[Test] public void DeadSpouse_AllowsRemarriage() { /* MarriageRules.Eligible Ok */ }
// TurnProcessorTests
[Test] public void Advance_IncrementsMonth_RunsPhasesInOrder() { /* 기록용 가짜 phase 3개 */ }
[Test] public void Advance_GameOver_DoesNothing() { }
```

- [ ] **Step 2~4: 실패 확인 → 구현 → 통과**
- [ ] **Step 5: 커밋** — `feat: 턴 처리와 출생·사망·후계·연간 단계`

---

### Task 9: AI 세력과 무소속 인물

**Files:**
- Create: `Assets/Murim.Core/Simulation/FactionAI.cs`, `Simulation/Phases/AiPhase.cs`, `Simulation/Phases/UnaffiliatedPhase.cs`
- Create: `Assets/Tests/Murim.Core.Tests/FactionAITests.cs`

**Interfaces:**
- Consumes: Task 6 세력 행동 전부, Task 8 `ITurnPhase`, `TurnContext`
- Produces:
  - `class FactionAI { static List<IGameAction> Choose(WorldState w, GameData d, GameRandom rng, Faction f); }`
    - 후보 생성 → 각 후보 `CanExecute` Ok인 것만 → 점수 = 기본값 + 장문인 특성 `ActionWeights[action.Key]` 합 + 상황 보정 + `rng.Next(0, 6)` → 내림차순 상위 N개 (N = 1, 장문인 Cunning ≥ 12면 2). 점수 ≤ 0은 버림
    - 후보와 기본값/상황 보정:
      - `InternalMarriage` 20 (+15 세가): 같은 세력 미혼 성인 이성 쌍이 있을 때, 가장 나이 차 작은 쌍
      - `Marriage` 15 (+20 세가): 우리 미혼 성인 1명 × 동맹/같은 성향 세력 미혼 성인 이성 1명, `MarriageRules.AcceptanceScore` > 0 예상인 경우만 (AI는 거절당할 혼담을 안 넣음). 대상이 플레이어 세력이면 제외 (플레이어 수락 UI는 2단계)
      - `RecruitNamed` 10 + (FactionDef.NamedMembers 또는 무작위 세력 생성 규모 − 현재 성인 멤버 수) × 5, 은자 100 이상일 때
      - `RecruitUnnamed` 5, 은자 200 이상, count = min(10, 상한까지)
      - `Expand` 8, 은자 300 이상, 본거지 또는 인접 지역 중 영향력 가장 낮은 곳
      - `Quest`(정·사 공통) 6, 은자 < 200이면 +10
      - `Plunder` 사·마만, 4 (+ greedy 가중치)
      - `Gift` 3, 은자 400 이상, 같은 성향 세력 중 AllianceScore 가장 높은 곳, 100 은자
      - `Alliance` 12, AllianceScore ≥ 30인데 아직 동맹 아닌 세력
      - `Insult` 2, 같은 지역 반대 성향(정↔사·마) 세력 (+ `insult_unorthodox` 가중치는 정파 장문인이 사·마 대상일 때)
      - `DesignateHeir` 25, HeirId가 비었고 장문인 나이 ≥ 50일 때 `SuccessionPhase.PickSuccessor` 결과
  - `class AiPhase : ITurnPhase` — 해산 안 된 AI 세력(플레이어 세력 제외)을 id 순으로 `Choose` → 각 행동 `CanExecute` 재확인 후 `Execute`
  - `class UnaffiliatedPhase : ITurnPhase` — 무소속 생존 성인(플레이어 제외)은 매년 1월 30% 확률로 현재 지역 세력 중 성향이 개인 Chivalry와 맞는 곳(≥0 정, <0 사)에 Disciple로 이적; 없으면 인접 지역으로 이동 10%

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Test] public void Choose_NeverReturnsUnexecutableAction() { /* 은자 0 세력 → 비용 있는 행동 없음 */ }
[Test] public void Choose_GreedyUnorthodoxLeader_PrefersPlunder() { /* 200회 반복, Plunder 선택 비율 > 정파 탐욕 없는 장문인 */ }
[Test] public void Choose_ChivalrousRighteous_InsultsLocalUnorthodoxMoreThanNeutral() { }
[Test] public void Choose_NeverProposesToPlayerFaction() { }
[Test] public void Choose_UnderstaffedFaction_Recruits() { }
[Test] public void Choose_OldLeaderWithoutHeir_DesignatesHeir() { }
[Test] public void Choose_CunningLeader_UpToTwoActions() { }
[Test] public void Unaffiliated_JoinMatchingAlignmentFaction() { }
```

- [ ] **Step 2~4: 실패 확인 → 구현 → 통과**
- [ ] **Step 5: 커밋** — `feat: 세력 AI와 무소속 인물 행동`

---

### Task 10: GameSession, 저장, 결정론·장기 시뮬레이션

**Files:**
- Create: `Assets/Murim.Core/Session/GameSession.cs`, `Simulation/Phases/PlayerActionPhase.cs`, `Assets/Murim.Core/Persistence/SaveSystem.cs`
- Create: `Assets/Tests/Murim.Core.Tests/GameSessionTests.cs`, `SaveSystemTests.cs`, `DeterminismTests.cs`, `LongSimulationTests.cs`

**Interfaces:**
- Consumes: Task 1~9 전부
- Produces:
  - `class GameSession`
    - `static GameSession NewGame(GameData d, NewGameOptions o)` — `SimSetup.NewWorld`, `TurnProcessor.CreateDefault([PlayerActionPhase, AiPhase, UnaffiliatedPhase])`
    - `WorldState World { get; }`, `GameData Data { get; }`, `Character Player { get; }`
    - `IReadOnlyList<IGameAction> Pending { get; }`, `int RemainingActions { get; }` = `ActionBudget.Max(Player)` − Pending 수 (`IWholeMonthAction` 있으면 0); `Player.BusyUntilMonth > World.Month`면 0
    - `ActionCheck Queue(IGameAction a)` — 남은 예산 0 → Fail(`"이번 달에는 더 할 수 없습니다"`); `IWholeMonthAction`인데 Pending 있음 → Fail(`"이동은 다른 행동과 함께 할 수 없습니다"`); `a.CanExecute` 실패 → 그대로 반환; 성공 시 추가
    - `void Unqueue(int index)`
    - `void EndTurn()` — `TurnProcessor.Advance`. `World.Month % 12 == 0`이 되고 `AutosaveDirectory != null`이면 `autosave.json` 저장
    - `string AutosaveDirectory { get; set; }`
    - `SaveResult Save(string path)` / `static LoadResult Load(GameData d, string path)` (`SaveSystem` 위임). `LoadResult { bool Ok; string Error; GameSession Session; }`
  - `class PlayerActionPhase : ITurnPhase` — 세션의 Pending을 순서대로: `CanExecute` 재확인 → Ok면 `Execute`, 아니면 `Chronicle.ActionSkipped(행동.Describe, 사유)`. 끝나면 Pending 비움
  - `static class SaveSystem`
    - `static string Serialize(WorldState w)` — Newtonsoft, `Formatting.Indented`, camelCase, enum 문자열
    - `static SaveResult Write(WorldState w, string path)` — 기존 파일 있으면 `.bak`으로 복사 후 임시 파일에 쓰고 교체. 예외는 `SaveResult { Ok=false, Error }`
    - `static LoadResult Read(GameData d, string path)` — 파싱 실패/`saveVersion != 1`/존재하지 않는 특성·지역 id 참조 → `Ok=false`와 한국어 오류. 성공 시 `GameRandom { State = w.RngState }`로 세션 복원
    - 저장 직전 `w.RngState = rng.State`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
// GameSessionTests
[Test] public void Queue_RespectsBudget() { }
[Test] public void Travel_WithOtherPending_Rejected_AndConsumesWholeBudget() { }
[Test] public void Escort_BlocksNextMonthBudget() { }
[Test] public void QueuedAction_TargetMarriedMeanwhile_SkippedWithChronicle() {
    // 혼담 2개를 같은 대상에게 예약 → 첫 번째 수락되도록 호감도 설정 → EndTurn 후 두 번째는 연대기에 ActionSkipped
}
[Test] public void EndTurn_January_WritesAutosave() { }
// SaveSystemTests
[Test] public void SaveLoadSave_RoundTripIdentical() { /* 5년 진행 → Serialize == Load 후 Serialize */ }
[Test] public void Load_ThenContinue_MatchesUninterruptedRun() { /* 10년 진행 저장 → 불러와 10년 더 == 중단 없이 20년 */ }
[Test] public void Save_ExistingFile_KeepsBak() { }
[Test] public void Load_CorruptFile_ReturnsErrorAndKeepsCurrentSession() { /* 깨진 JSON, saveVersion 99 각각 → Ok false, Error 비어있지 않음, 기존 session.World.Month 그대로 */ }
// DeterminismTests
[Test] public void SameSeed_50Years_IdenticalWorld() { /* 행동 없이 600턴 × 2 → Serialize 문자열 동일 */ }
// LongSimulationTests  [Category("Long")]
[Test] public void TwoHundredYears_StaysHealthy() {
    // minor_sect, henan, seed 12345, 2400턴 (플레이어는 행동 없음 → 플레이어 죽고 GameOver면 그 뒤로도 세계는 계속 돌려야 하므로
    //  테스트는 TurnProcessor를 GameOver 무시 모드로: TurnContext.IgnoreGameOver = true)
    // 매 10년: 살아 있는 이름 있는 인물 500~4000, 해산 안 된 세력 ≥ 20
    // 끝: 연대기에 Married, Succeeded, AlignmentShifted 종류 각각 ≥ 1 (ChronicleEntry에 Kind 문자열 필드 추가)
}
[TestCaseSource(nameof(AllScenarios))] [Category("Long")]
public void EachScenario_50Years_NoException(string scenarioId) { }
```

- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인 및 밸런스 조정** — `LongSimulationTests`가 인구 범위를 벗어나면 `SimConstants`(`MonthlyBirthChance`, 사망 계수, AI 혼인 기본 점수)만 조정해 다시 돌린다. 조정한 값과 결과(10년마다 인구)를 커밋 메시지에 적는다.
- [ ] **Step 5: Unity EditMode 전체 테스트 실행** — 공통 명령, `result="Passed"`
- [ ] **Step 6: 커밋** — `feat: 게임 세션, 저장·불러오기, 장기 시뮬레이션 검증`

---

### Task 11: Unity 화면 뼈대 — 부트스트랩, 시작 화면, 상단 바, 지도

**Files:**
- Create: `Assets/Murim.View/Murim.View.asmdef`, `Bootstrap.cs`, `UI/UiFactory.cs`, `UI/Theme.cs`, `UI/Screens/ErrorScreen.cs`, `UI/Screens/TitleScreen.cs`, `UI/Screens/NewGameScreen.cs`, `UI/Screens/GameScreen.cs`, `UI/Panels/TopBar.cs`, `UI/Panels/MapView.cs`, `UI/Panels/RegionPanel.cs`
- Create: `Assets/Murim.View/Fonts/NotoSansKR.ttf`
- Create: `Assets/Murim.Editor/Murim.Editor.asmdef`, `Assets/Murim.Editor/SceneBuilder.cs`, `Assets/Scenes/Main.unity` (생성됨)

**Interfaces:**
- Consumes: `GameSession`, `GameData`, `DataLoader`, `DataValidator`, `GameDate`, `Influence`, `FactionOps`
- Produces:
  - `Murim.View.asmdef`: references `Murim.Core`, `UnityEngine.UI`; precompiled `Newtonsoft.Json.dll`
  - `class Bootstrap : MonoBehaviour` — `Awake`: `DataLoader.LoadFromDirectory(Path.Combine(Application.streamingAssetsPath, "Data"))` + `DataValidator.Validate` → 오류 있으면 `ErrorScreen`(오류 목록 스크롤), 없으면 `TitleScreen`. 현재 `GameSession`을 보관, `ShowScreen(Screen)` 전환
  - `static class UiFactory` — `Canvas CreateCanvas()` (Screen Space Overlay, `CanvasScaler` 1920×1080 기준 match 0.5), `Text Label(Transform parent, string text, int size)`, `Button Button(Transform parent, string text, Action onClick)`, `RectTransform Panel(Transform parent, Color bg)`, `ScrollRect ScrollList(Transform parent)`, `Tooltip` (비활성 버튼 사유 표시용 hover 라벨). 폰트는 `Resources`가 아닌 `Bootstrap`이 인스펙터 없이 쓰도록 `SceneBuilder`가 `Bootstrap.font` 필드에 할당
  - `abstract class Screen { abstract void Build(Transform root); virtual void Refresh(); }`
  - `TitleScreen`: 새 게임 / 불러오기(슬롯 1~3 + 자동 저장, 없는 슬롯 비활성) / 종료
  - `NewGameScreen`: 3단계 — 시나리오 카드 5개(이름·설명) → 지역 목록 13개 → 이름(성·이름 입력칸 + "다시 굴리기"), 세력 이름(세력 시나리오만), 성향(허용 목록), 특성 최대 2개 토글(opposite 비활성), 시드 입력칸(기본 무작위) → 시작
  - `GameScreen`: 상단 `TopBar`, 왼쪽 `MapView`, 오른쪽 패널 영역(지역/세력/인물 패널 교체), 아래 연대기 영역(Task 12)
  - `TopBar`: `GameDate.Format(World.Month)`, 은자·명성(세력 소속이면 세력 값, 아니면 개인 값), `남은 행동 {RemainingActions}`, 예약 목록 드롭다운(취소 가능), "다음 달" 버튼 (`GameOver`면 비활성 + "가문이 끊겼습니다" 표시), 저장 버튼
  - `MapView`: 지역마다 `RegionDef.X/Y`에 원형 버튼 + 이름, 인접 연결선(UI Image 회전으로 선 그리기). 색 = 그 지역 영향력 1위 세력 성향 (정 `#4A7BD0`, 사 `#C08A2E`, 마 `#B03030`, 없음 회색). 플레이어 위치 지역 테두리 강조
  - `RegionPanel`: 치안, 세력 목록(이름·성향·영향력 막대) — 클릭 시 세력 패널(Task 12)
  - `SceneBuilder` (Editor): 메뉴 `Murim/Build Main Scene` 및 batchmode용 `public static void BuildMainScene()` — 새 씬에 `Bootstrap` GameObject, `EventSystem` + `StandaloneInputModule`, 카메라(2D, 배경색 `#1B1B22`) 생성, 폰트 할당, `Assets/Scenes/Main.unity` 저장, Build Settings에 등록

- [ ] **Step 1: 폰트 준비**

`Invoke-WebRequest "https://github.com/google/fonts/raw/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf" -OutFile Assets/Murim.View/Fonts/NotoSansKR.ttf` (OFL 라이선스). 실패하면 `C:\Windows\Fonts\malgun.ttf`를 복사하되 개인 개발용이며 배포 전 교체해야 함을 커밋 메시지에 적는다.

- [ ] **Step 2: 코드 작성** — 위 인터페이스대로 화면 구현

- [ ] **Step 3: 씬 생성과 컴파일 확인**

`& $u -batchmode -nographics -projectPath . -executeMethod Murim.Editor.SceneBuilder.BuildMainScene -quit -logFile TestResults/build-scene.log` → 로그에 `error CS` 없음, `Assets/Scenes/Main.unity` 생성됨

- [ ] **Step 4: 수동 확인 (에디터에서 Play)**
- 데이터 폴더 파일 하나 이름을 바꾸면 오류 화면에 그 파일명이 보인다 (확인 후 되돌림)
- 새 게임 → 떠돌이 협객 → 사천 → 시작: 지도에 13개 지역, 사천 테두리 강조, 상단 바에 "1년 1월", 은자 80
- "다음 달" 12번 → "2년 1월"
- 약소문파로 새 게임 → 지역 클릭 시 세력 목록과 영향력 막대

- [ ] **Step 5: 커밋** — `feat: Unity 시작 화면·지도·상단 바`

---

### Task 12: 세력·인물 패널, 행동 버튼, 가계도, 연대기, 저장 UI

**Files:**
- Create: `Assets/Murim.View/UI/Panels/FactionPanel.cs`, `CharacterPanel.cs`, `FamilyTreePanel.cs`, `ChroniclePanel.cs`, `ActionMenu.cs`, `LocalPanel.cs`, `SaveLoadDialog.cs`, `Portrait.cs`
- Modify: `Assets/Murim.View/UI/Screens/GameScreen.cs`, `Assets/Murim.View/UI/Panels/TopBar.cs`

**Interfaces:**
- Consumes: Task 6·7 행동 클래스 생성자, `GameSession.Queue`, `Opinion.Of`, `Josa`, `GameDate`
- Produces:
  - `FactionPanel(Faction)`: 이름, 성향·협의 점수, 장문인·후계자(클릭 → 인물 패널), 은자·명성·빚, 동맹 목록, 멤버 목록(역할·나이·경지), 행동 버튼 — 플레이어가 장문인일 때: 자기 세력이면 [제자 받아들이기, 제자 모집(수량 입력), 영향력 확장(지역 선택), 의뢰, 약탈(사·마만), 빚 갚기(빚 있을 때)], 남의 세력이면 [혼담(우리 쪽 인물·상대 인물·이적 방향 선택), 동맹, 선물(은자 슬라이더 30~200), 모욕]
  - `CharacterPanel(Character)`: `Portrait`(성별 × 나이대 소년/청년/장년/노년 단색 실루엣 도형), 이름·나이·경지명(삼류, 이류, 일류, 절정, 초절정, 화경, 현경)·역할·소속, 능력치 4개, 특성(이름 + 툴팁 설명), 플레이어에 대한 호감도와 그 근거(특성 상성/저장/기억 합계), 관계 목록(배우자·부모·자식·사제), 기억 목록(종류·대상·이유·강도), 버튼: [가계도], 플레이어 세력 멤버면 [후계자 지정, 파문, 내부 혼인(상대 선택)]
  - `FamilyTreePanel(Character)`: 3줄 — 부모 / 본인·배우자 / 자식, 각 칸 클릭 시 인물 패널, 사망자 회색 + 생몰년
  - `ChroniclePanel`: 최신순 스크롤, 각 줄 `"{GameDate.Format(month)}, {text}"`, 토글 "우리 세력만" (InvolvedIds에 플레이어 또는 플레이어 세력 멤버 포함), 중요도 높은 줄 굵게
  - `LocalPanel` (플레이어가 `PersonalRules.UsesPersonalActions`일 때 세력 패널 대신 기본 표시): 현재 지역 인물·세력 목록 + 개인 행동 버튼 [이동/표국 호위(인접 지역 선택), 협행, 청부, 비무(인물 선택), 교분(인물 선택), 객경 가입(세력 선택, 무소속만), 문파 창설(이름 입력, 무소속만), 세력 떠나기(소속일 때)]
  - `ActionMenu`: 모든 버튼은 누르기 전 `action.CanExecute`와 `session.RemainingActions`로 활성/비활성 결정, 비활성이면 `Reason`을 툴팁으로. 누르면 `session.Queue`, 결과 Fail이면 토스트로 사유
  - `SaveLoadDialog`: 슬롯 1~3 저장/불러오기 (`Application.persistentDataPath/saves/slot{n}.json`), 자동 저장 불러오기, 실패 시 오류 문구 표시, 불러오기 실패 시 현재 게임 유지
  - `GameScreen`에서 `session.AutosaveDirectory = persistentDataPath/saves`

- [ ] **Step 1: 코드 작성** — 위 인터페이스대로

- [ ] **Step 2: 컴파일 확인** — Task 11 Step 3 명령 재실행, `error CS` 없음

- [ ] **Step 3: 수동 확인 (스펙 9절 시나리오)**
- 약소문파로 시작 → 이웃 세력에 선물 → 혼담 → 다음 달 → 연대기에 결과 문장(조사 자연스러움 확인)
- 인물 패널에서 특성 툴팁, 기억 목록, 가계도 3대 이동
- 10년(120턴) 진행 → 연대기에 출생·사망·혼인 기록, "우리 세력만" 필터 동작
- 슬롯 1 저장 → 타이틀로 → 불러오기 → 연월·은자·연대기 동일
- 떠돌이 협객으로 시작 → 이동 예약 시 다른 행동 버튼 비활성 + 툴팁 → 이동 후 지도 강조 지역 변경 → 비무·교분 → (테스트용 시드로 은자 확보가 어려우면 확인 생략하고 Task 7 테스트로 갈음함을 기록)
- 저장 파일을 메모장으로 깨뜨린 뒤 불러오기 → 오류 문구, 현재 게임 유지

- [ ] **Step 4: Unity EditMode 전체 테스트 재실행** — `result="Passed"`

- [ ] **Step 5: 커밋** — `feat: 세력·인물 패널, 행동, 가계도, 연대기, 저장 UI`
