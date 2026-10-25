using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Rules;
using Murim.Core.Session;
using Murim.Core.Text;
using Murim.View;
using Murim.View.UI;
using Murim.View.UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.Editor
{
    /// <summary>
    /// Task 12 수동 확인 시나리오를 실제 UI 버튼을 눌러 재현한다 (개발 확인용).
    /// batchmode (그래픽 있음): -executeMethod Murim.Editor.UiScenario.RunTask12
    /// 결과: TestResults/snapshots/12_*.png, TestResults/snapshots/task12.txt
    /// 저장 칸 1·2는 실행 전에 백업하고 끝나면 되돌린다.
    /// </summary>
    public static class UiScenario
    {
        static readonly List<string> report = new List<string>();
        static int failures;
        static Bootstrap app;
        static Camera camera;

        public static void RunTask12()
        {
            report.Clear();
            failures = 0;
            var backups = Backup(1, 2);
            try
            {
                app = UiSnapshot.CreateApp(out camera);
                MinorSect();
                Wanderer();
            }
            catch (Exception e)
            {
                Check(false, "예외: " + e);
            }
            finally
            {
                Restore(backups);
            }
            report.Add(failures == 0 ? "결과: 모두 통과" : $"결과: 실패 {failures}개");
            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "snapshots", "task12.txt");
            File.WriteAllLines(outPath, report);
            Debug.Log("[T12]\n" + string.Join("\n", report));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void MinorSect()
        {
            var s = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 42 });
            app.StartGame(s);
            var gs = new GameScreen(app);
            app.Show(gs);
            var w = s.World;
            var me = s.Player;
            var own = Wallet.LedFaction(w, me);
            Shot("12_00_home");

            // 혼담이 가능한 이웃 세력을 고른다.
            var target = w.Factions.Values.Where(f => f.Id != own.Id && !f.IsDissolved && f.HomeRegionId == own.HomeRegionId
                    && FactionOps.Leader(w, f) != null)
                .FirstOrDefault(f => FactionOps.LivingMembers(w, own).Any(o => FactionOps.LivingMembers(w, f)
                    .Any(t => !MarriageRules.IsMarried(w, o) && o.AgeYears(w.Month) >= 16 && MarriageRules.Eligible(w, o, t).Ok)));
            Check(target != null, "혼담 가능한 이웃 세력: " + target?.Name);
            if (target == null) return;

            gs.ShowFaction(target.Id);
            Shot("12_01_faction_other");
            Click("선물 30냥");
            Check(s.Pending.Count == 1, $"선물 예약 (예약 {s.Pending.Count})");
            Click("혼담 넣기 (50냥)");
            PickFirst("우리 쪽 인물");
            PickFirst("상대 인물");
            PickFirst("혼인 방향");
            Check(s.Pending.Count == 2, $"혼담 예약 (예약 {s.Pending.Count}: {string.Join(", ", s.Pending.Select(p => p.Describe(w)))})");
            Shot("12_02_queued");

            int before = w.Chronicle.Count;
            Click("다음 달");
            Check(w.Month == 1, "다음 달 → " + GameDate.Format(w.Month));
            var mine = w.Chronicle.Skip(before).Where(e => e.InvolvedIds != null
                && e.InvolvedIds.Any(id => id == me.Id || id == own.Id || own.MemberIds.Contains(id))).ToList();
            Check(mine.Count > 0, "이번 달 우리와 얽힌 연대기:");
            foreach (var e in mine) report.Add("    " + e.Text);
            Shot("12_03_after_marriage");

            gs.ShowCharacter(me.Id);
            var tips = app.Root.GetComponentsInChildren<Tooltip>(true).Where(t => t.name == "Trait" && !string.IsNullOrEmpty(t.Text)).ToList();
            Check(tips.Count > 0, $"인물 패널 특성 툴팁 {tips.Count}개 (예: {tips.FirstOrDefault()?.Text})");
            Shot("12_04_character");

            // 기억이 있는 인물 (이웃 장문인: 선물 받음)
            var leader = FactionOps.Leader(w, w.GetFaction(target.Id));
            gs.ShowCharacter(leader.Id);
            Check(leader.Memories.Count > 0, $"{leader.FullName} 기억 {leader.Memories.Count}개");
            Shot("12_05_character_memories");

            gs.ShowFamily(me.Id);
            Shot("12_06_family");
            var childId = me.ChildIds.FirstOrDefault();
            var child = w.Get(childId);
            if (child != null)
            {
                ClickPrefix(child.FullName);
                Shot("12_07_family_child");
                var dad = w.Get(child.FatherId) ?? w.Get(child.MotherId);
                ClickPrefix(dad.FullName);
                Check(true, $"가계도 이동: {me.FullName} → {child.FullName} → {dad.FullName}");
            }
            else report.Add("  (참고) 시작 인물에게 자식이 없어 가계도 이동은 부모 쪽만 확인");

            // 10년
            for (int i = 0; i < 119; i++) s.EndTurn();
            gs.ShowHome();
            Click("다음 달");
            int born = w.Chronicle.Count(e => e.Kind == "Born"), died = w.Chronicle.Count(e => e.Kind == "Died"),
                married = w.Chronicle.Count(e => e.Kind == "Married");
            Check(w.Month == 121 && born > 0 && died > 0 && married > 0,
                $"10년 뒤 {GameDate.Format(w.Month)}: 출생 {born}, 사망 {died}, 혼인 {married}");
            int allRows = ChronicleRows();
            Shot("12_08_ten_years");
            Click("우리 세력만");
            int ownRows = ChronicleRows();
            Check(ownRows > 0 && ownRows < allRows, $"\"우리 세력만\" 필터: {allRows}줄 → {ownRows}줄");
            Shot("12_09_ten_years_own");

            // 저장 → 처음 화면 → 불러오기
            int month = w.Month, silver = Wallet.Silver(w, s.Player), lines = w.Chronicle.Count;
            Click("저장");
            ClickIn("SaveLoad", "저장", 0);
            Check(File.Exists(Bootstrap.SlotPath(1)), "1번 칸 저장 파일 생김");
            Click("처음 화면");
            Shot("12_10_title");
            Click("불러오기 1번 칸");
            var loaded = app.Session;
            Check(loaded != s && loaded.World.Month == month && Wallet.Silver(loaded.World, loaded.Player) == silver
                  && loaded.World.Chronicle.Count == lines,
                $"불러오기: {GameDate.Format(loaded.World.Month)}, 은자 {Wallet.Silver(loaded.World, loaded.Player)} (저장 시 {silver}), 연대기 {loaded.World.Chronicle.Count}줄 (저장 시 {lines})");

            // 깨진 파일
            File.WriteAllText(Bootstrap.SlotPath(2), "{ \"saveVersion\": 1, \"month\": 이건 깨진 파일");
            var current = app.Session;
            Click("저장");
            var loadButtons = Buttons("SaveLoad", "불러오기");
            // 순서: 자동 저장, 1번, 2번, 3번
            loadButtons[2].onClick.Invoke();
            var status = Find("SaveLoad").GetComponentsInChildren<Text>().FirstOrDefault(t => t.text.StartsWith("불러오지 못했습니다"));
            Check(status != null && app.Session == current, "깨진 파일: " + (status?.text ?? "오류 문구 없음") + ", 현재 게임 유지");
            Shot("12_11_corrupt_load");
            ClickIn("SaveLoad", "닫기", 0);
        }

        static void Wanderer()
        {
            var s = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "wandering_hero", RegionId = "sichuan", Seed = 7 });
            app.StartGame(s);
            var gs = new GameScreen(app);
            app.Show(gs);
            var me = s.Player;
            string from = me.LocationRegionId;
            Shot("12_20_wanderer");

            Click("이동");
            PickFirst("이동할 곳");
            Check(s.Pending.Count == 1, "이동 예약: " + s.Pending.FirstOrDefault()?.Describe(s.World));
            var quest = Buttons(null, "협행").FirstOrDefault();
            var tip = quest?.GetComponent<Tooltip>()?.Text;
            Check(quest != null && !quest.interactable && !string.IsNullOrEmpty(tip), "이동 예약 뒤 협행 비활성, 툴팁: " + tip);
            Shot("12_21_travel_queued");

            Click("다음 달");
            Check(me.LocationRegionId != from, $"이동: {app.Data.Regions[from].Name} → {app.Data.Regions[me.LocationRegionId].Name}");
            Shot("12_22_after_travel");

            int before = s.World.Chronicle.Count;
            var spar = Buttons(null, "비무").FirstOrDefault();
            if (spar != null && spar.interactable)
            {
                Click("비무");
                PickFirst("비무 상대");
                if (Buttons(null, $"교분 ({Befriend.Cost}냥)").FirstOrDefault()?.interactable == true)
                {
                    Click($"교분 ({Befriend.Cost}냥)");
                    PickFirst("교분 상대");
                }
                Check(s.Pending.Count >= 1, "비무·교분 예약: " + string.Join(", ", s.Pending.Select(p => p.Describe(s.World))));
                Click("다음 달");
                foreach (var e in s.World.Chronicle.Skip(before).Where(e => e.InvolvedIds != null && e.InvolvedIds.Contains(me.Id)))
                    report.Add("    " + e.Text);
                Shot("12_23_after_spar");
            }
            else report.Add("  (참고) 도착한 지역에 겨룰 사람이 없어 비무 생략: " + spar?.GetComponent<Tooltip>()?.Text);
        }

        // ---- UI 조작 도우미 ----

        static Transform Canvas => app.Root.parent;

        static Transform Find(string name) =>
            Canvas.GetComponentsInChildren<Transform>().LastOrDefault(t => t.name == name);

        static string LabelOf(Button b) => b.GetComponentInChildren<Text>()?.text ?? "";

        static List<Button> Buttons(string within, string label)
        {
            var root = within == null ? Canvas : Find(within);
            if (root == null) return new List<Button>();
            return root.GetComponentsInChildren<Button>().Where(b => LabelOf(b) == label).ToList();
        }

        static void Press(Button b, string what)
        {
            if (b == null) { Check(false, "버튼 없음: " + what); return; }
            if (!b.interactable) { Check(false, "버튼 비활성: " + what + " (" + b.GetComponent<Tooltip>()?.Text + ")"); return; }
            b.onClick.Invoke();
        }

        static void Click(string label) => Press(Buttons(null, label).LastOrDefault(), label);

        static void ClickIn(string within, string label, int index) =>
            Press(Buttons(within, label).ElementAtOrDefault(index), within + "/" + label);

        /// <summary>이름으로 시작하는 칸 (가계도 칸처럼 Text가 여러 개인 버튼).</summary>
        static void ClickPrefix(string prefix)
        {
            var b = Canvas.GetComponentsInChildren<Button>()
                .FirstOrDefault(x => x.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith(prefix)) && x.name == "Person");
            Press(b, prefix);
        }

        static void PickFirst(string what)
        {
            var picker = Find("Picker");
            var b = picker?.GetComponentsInChildren<Button>().FirstOrDefault(x => x.interactable && LabelOf(x) != "닫기");
            if (b != null) report.Add($"  {what} 고름: {LabelOf(b)}");
            Press(b, what + " 선택지");
        }

        static int ChronicleRows() =>
            Find("Chronicle").GetComponentsInChildren<Text>().Count(t => t.text.Contains("년 ") && t.text.Contains("월, "));

        static void Check(bool ok, string text)
        {
            if (!ok) failures++;
            report.Add((ok ? "[통과] " : "[실패] ") + text);
        }

        static void Shot(string name) => UiSnapshot.Shot(app, camera, name);

        static Dictionary<int, byte[]> Backup(params int[] slots)
        {
            var d = new Dictionary<int, byte[]>();
            foreach (var n in slots)
            {
                var p = Bootstrap.SlotPath(n);
                d[n] = File.Exists(p) ? File.ReadAllBytes(p) : null;
            }
            return d;
        }

        static void Restore(Dictionary<int, byte[]> backups)
        {
            foreach (var pair in backups)
            {
                var p = Bootstrap.SlotPath(pair.Key);
                if (pair.Value != null) File.WriteAllBytes(p, pair.Value);
                else if (File.Exists(p)) File.Delete(p);
            }
        }
    }
}
