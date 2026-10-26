# 무림장원 — 4단계: 장원 건설 설계

작성일: 2026-10-27
상태: 구현·검증 완료 (2026-10-27, 사용자가 "5단계까지 쭉" 진행을 맡겨 검토 없이 추천안으로 확정)
선행:
- [1단계 설계](2026-10-25-murim-stage1-world-character-core-design.md)
- [3단계 설계](2026-10-26-murim-stage3-martial-arts-design.md) (구현·검증 완료)

## 1. 목표

지금까지 장원은 이름뿐이었다. 4단계에서는 **플레이어가 이끄는 세력의 본거지를 격자 위에 직접 짓고, 그 건물이 수련·수입·방어에 실제로 영향을 주며, 제자들이 그 안에서 하루를 보내는 모습을 실시간으로 본다**.

4단계가 끝나면 플레이어는:
- 세력 패널에서 "장원" 화면으로 들어가 탑다운 격자(24×16)에 지어진 건물을 보고
- 건물 목록에서 연무장·장경각·석실·약방·전장·숙소·담장·망루를 골라 빈 땅에 놓는다(은자를 내고, 몇 달 뒤 완공).
- 건물을 허물면 비용의 절반을 돌려받는다.
- 완공된 건물은 소속 인물의 성취·깨달음·돌파·주화입마·폐관, 세력 수입, (5단계) 방어에 보너스를 준다.
- 장원 화면에서는 시간이 실시간으로 흐르고(일시정지·1배·2배·3배), 이름 있는 제자들이 시간대에 따라 연무장에서 수련하고, 장경각에서 책을 읽고, 숙소에서 쉬는 모습을 본다. 이 하루는 보여 주기용이고 월 처리에는 영향을 주지 않는다.

**포함하지 않는 것**
- AI 세력의 건설. AI 세력은 장원이 없고 보너스도 없다(기본값 1).
- 습격전. 담장·망루는 5단계 전투에서 쓴다. 4단계에서는 "방어" 수치만 보여 준다.
- 제자 개인에게 일을 시키는 조작(림월드식 작업 지정).

## 2. 아키텍처

```
Assets/Murim.Core/Manor/
  BuildingDef.cs      건물 정의 (buildings.json)
  ManorState.cs       격자 크기, 건물 목록 (Faction.Manor)
  ManorOps.cs         놓기·허물기 검사와 실행, 완공 처리, 보너스 합산(ManorBonuses)
  ManorPhase.cs       ITurnPhase: 매달 완공 처리, 건물 수입
  ManorLife.cs        제자 하루 일과(시각 → 활동·목표 건물), 결정론
Assets/StreamingAssets/Data/buildings.json
Assets/Murim.View/UI/Screens/ManorScreen.cs   격자, 건물 목록, 놓기 미리보기, 실시간 일과
```

- Core는 UnityEngine을 쓰지 않는다. 난수는 `GameRandom`만, 반복은 Ordinal 순서.
- 건물 놓기·허물기는 행동 횟수를 쓰지 않는다(주무공 바꾸기처럼 바로 일어난다). 은자는 바로 나간다.
- 월 처리 순서: `… 사건 → 수련 → 장원 → 출생 …`. `ManorPhase`가 완공과 수입을 처리한다.

## 3. 건물 (`buildings.json`)

```json
{ "id": "training_ground", "name": "연무장", "width": 3, "height": 3, "cost": 300, "buildMonths": 3, "max": 2,
  "description": "제자들이 아침저녁으로 초식을 다듬는 마당.", "bonuses": { "masteryPercent": 25 } }
```

| id | 이름 | 크기 | 비용 | 기간 | 최대 | 보너스 |
|---|---|---|---|---|---|---|
| `main_hall` | 본당 | 4×3 | — | — | 1 | 처음부터 있다. 허물 수 없다. |
| `training_ground` | 연무장 | 3×3 | 300 | 3 | 2 | 성취 증가 확률 +25%(개당) |
| `library` | 장경각 | 3×2 | 400 | 4 | 1 | 깨달음 +3/년 |
| `meditation_cave` | 석실 | 2×2 | 350 | 3 | 1 | 폐관 내공 ×4(기본 ×3), 돌파 +0.05 |
| `apothecary` | 약방 | 2×2 | 250 | 2 | 1 | 주화입마 확률 −0.05, 주화입마 사망 없음 |
| `market_stall` | 전장 | 2×2 | 200 | 2 | 3 | 수입 +15냥/월(개당) |
| `dormitory` | 숙소 | 3×2 | 150 | 2 | 3 | 제자 정원 +6(개당, 정원 넘는 제자 받기 불가) |
| `wall` | 담장 | 1×1 | 10 | 1 | 120 | 방어 +1 (5단계 습격전에서 길을 막는다) |
| `watchtower` | 망루 | 1×1 | 120 | 2 | 4 | 방어 +5 (5단계: 올라선 인물의 원거리 사거리·피해 증가) |

- `bonuses` 키: `masteryPercent enlightenmentPerYear trainingQiMultiplier breakthroughBonus deviationReduction noDeviationDeath incomePerMonth capacity defense`. 알 수 없는 키는 검증 오류.
- 정원: 본당만 있으면 이름 있는 제자 12명, 숙소마다 +6. 정원이 차면 `RecruitNamed`가 "숙소가 모자랍니다"로 막힌다(플레이어 세력만).

## 4. 장원 상태

```csharp
public class ManorState { public int Width = 24, Height = 16; public List<BuildingInstance> Buildings; public int NextBuildingNumber = 1; }
public class BuildingInstance { public string Id; public string DefId; public int X, Y; public int CompleteMonth; }  // CompleteMonth ≤ 현재 월이면 완공
```

- `Faction.Manor`: 플레이어가 세력을 이끌게 되는 순간(시나리오 시작, 문파 창설, 후계로 장문인이 됨, 저장 이관) 없으면 만든다. 본당을 가운데(10,6)에 놓는다.
- 놓기 검사: 격자 안, 다른 건물과 겹치지 않음, 최대 개수, 은자, 정문 칸(아래 가장자리 가운데 2칸: (11,15),(12,15))은 비워 둔다(5단계 습격자가 들어오는 길).
- 허물기: 본당 불가. 미완공이면 전액, 완공이면 절반 환불.

## 5. 보너스 연결 (`ManorBonuses`)

`ManorOps.Bonuses(WorldState w, GameData d, Character c)`: 인물이 속한 세력의 장원에서 **완공된** 건물 보너스를 합산한다. 장원이 없으면 기본값.

| 쓰는 곳 | 바뀌는 것 |
|---|---|
| `Cultivation.YearlyGrowth` | 성취 확률 × (1 + masteryPercent/100), 깨달음 + enlightenmentPerYear |
| `Cultivation.MonthlyQi` | 폐관 배율 = 3 + trainingQiMultiplier |
| `Cultivation.TryBreakthrough` | 확률 + breakthroughBonus |
| `Cultivation.DeviationChance` | − deviationReduction, noDeviationDeath면 사망 판정 없음 |
| `ManorPhase` (수입) | 세력 은자 + incomePerMonth |
| `RecruitNamed`·사건 `recruit` | 플레이어 세력이면 정원 검사 |
| 화면 | 방어 = defense 합 |

## 6. 제자 하루 일과 (`ManorLife`)

- 장원 화면의 시계는 하루 24시를 화면 시간 48초(1배)에 돈다. 날짜는 바뀌지 않는다(월 처리와 무관).
- `ManorLife.Activity(Character c, int hour, ManorState m, GameData d)` → `(string activity, BuildingInstance target)`:
  - 0~5시 잠 → 숙소(없으면 본당), 6~8시 수련 → 연무장(없으면 본당 앞마당), 9~11시 공부 → 장경각(없으면 본당), 12~13시 식사 → 본당, 14~17시 수련 → 연무장, 18~20시 자유 → 성격 따라(호탕 → 전장, 근면 → 연무장, 그 밖 → 본당), 21~23시 휴식 → 숙소.
  - 폐관 중이면 종일 석실(없으면 본당). 수장은 9~11시와 14~17시에 본당.
- 같은 활동 안에서 인물마다 건물 칸 안의 자리는 id 해시로 정해 겹치지 않게 흩는다.
- 화면은 인물 점을 현재 자리에서 목표 자리로 걸어가게 그린다(직선 보간, 담장은 신경 쓰지 않는다). 점을 누르면 인물 패널로 간다.

## 7. 화면

- 세력 패널(내가 이끄는 세력)에 "장원 보기" 단추. 장원 화면 상단: 은자, 방어, 정원(현재/최대), 시계와 속도(⏸ 일시정지·1배·2배·3배 — 글꼴에 없는 기호는 글자로 쓴다), "지도로".
- 왼쪽: 격자(칸 40px). 건물은 색 사각형 + 이름, 미완공은 반투명 + "n개월 남음". 정문 칸은 표시.
- 오른쪽: 건물 목록(이름, 크기, 비용, 기간, 보너스 한 줄). 고르면 "놓기" 상태: 격자 위에 마우스를 올리면 초록(가능)/빨강(불가) 미리보기, 누르면 놓는다. 불가 사유는 토스트. "허물기" 상태에서는 건물을 누르면 확인 창.
- 아래: 지금 일과 목록("장삼 — 연무장에서 수련").

## 8. 저장 (saveVersion 4)

- 추가: `Faction.Manor`. 버전 1~3 저장을 불러오면 플레이어가 이끄는 세력에 기본 장원을 만든다.
- 알 수 없는 건물 id가 있으면 불러오기를 거부한다.

## 9. 검증·테스트

- 데이터: 건물 id 중복, 크기 1~5, 비용·기간 0 이상, 알 수 없는 보너스 키, `main_hall` 존재.
- 놓기: 격자 밖, 겹침, 최대 개수, 은자 부족, 정문 칸 → 각 사유. 성공하면 은자 차감, 완공 월.
- 허물기: 본당 불가, 미완공 전액·완공 절반 환불.
- 완공 전에는 보너스 없음, 완공 뒤 보너스 적용. 연무장 2개면 +50%.
- 수련 연결: 장경각이 있으면 1월 깨달음 +3, 석실이 있으면 폐관 내공 ×4, 약방이 있으면 주화입마 사망 없음.
- 수입: 전장 2개면 매달 +30냥.
- 정원: 숙소 없이 12명이면 `RecruitNamed` 막힘, 숙소 하나 지으면 풀림.
- 일과: 시각별 활동, 폐관 중 석실, 같은 입력이면 같은 자리.
- 저장: 버전 3 이관, 왕복.
- 화면: `UiScenario.RunTask15` — 장원 화면 진입, 연무장 놓기(미리보기 초록·빨강), 3개월 뒤 완공, 허물기, 실시간 일과(시계 진행, 점 이동), 일시정지.

## 10. 이후 단계를 위한 연결점

- 5단계 습격전은 `ManorState`를 전투 지도로 쓴다: 건물·담장은 막힌 칸, 정문 칸으로 습격자가 들어온다. 망루 위 인물은 원거리 보너스.

## 11. 구현 중 바뀐 점

- 기본 정원 12는 본당의 `capacity: 12` 보너스로 데이터에 둔다(숙소 +6과 같은 방식). 본당은 방어 +5도 준다.
- 완공 연대기는 그 건물이 완공되는 달로 넘어가는 월 처리에서 남긴다(`CompleteMonth == 현재 월 + 1`). 담장은 한 칸씩 지으므로 연대기에 남기지 않는다.
- 장원 보너스는 `Cultivation`이 세계(`WorldState`)를 직접 받아 계산한다(전역 상태 없이).
- 장원 화면 사람 점은 칸/초 3의 속도로 걷는다(1배). 6~8시 수련처럼 시간대가 바뀌면 다음 자리로 걸어간다.
