using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Manor;
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
    /// Task 12(1단계)·Task 13(2단계 사건) 수동 확인 시나리오를 실제 UI 버튼을 눌러 재현한다 (개발 확인용).
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

        /// <summary>2단계: 사건 선택 창, 비활성 사유, 소식, 대기 중 저장·불러오기, 표식 칩, "사건만" 필터, 2년 진행.
        /// -executeMethod Murim.Editor.UiScenario.RunTask13 → TestResults/snapshots/13_*.png, task13.txt</summary>
        public static void RunTask13()
        {
            report.Clear();
            failures = 0;
            var backups = Backup(1);
            try
            {
                app = UiSnapshot.CreateApp(out camera);
                Events();
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
            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "snapshots", "task13.txt");
            File.WriteAllLines(outPath, report);
            Debug.Log("[T13]\n" + string.Join("\n", report));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void Events()
        {
            var s = GameSession.NewGame(app.Data, new NewGameOptions
            {
                ScenarioId = "minor_sect", RegionId = "henan", Seed = 42, StorytellerId = "turbulent",
            });
            app.StartGame(s);
            var w = s.World;
            var me = s.Player;
            var own = Wallet.LedFaction(w, me);
            Check(w.Events.StorytellerId == "turbulent", "이야기꾼: 파란만장");

            // 원한을 품은 이웃 수장 → 생사결 신청. 은자가 모자라 "달랜다"는 비활성이어야 한다.
            var rival = w.Factions.Values.Where(f => f.Id != own.Id && !f.IsDissolved && f.HomeRegionId == own.HomeRegionId)
                .Select(f => FactionOps.Leader(w, f)).First(c => c != null && c.IsAlive && c.AgeYears(w.Month) <= 70);
            Opinion.AddMemory(rival, Murim.Core.Data.MemoryKind.Grudge, me.Id, "모욕", w.Month, 70);
            own.Silver = 100;
            var forced = s.ForceEvent("grudge_duel_challenge");
            Check(forced.Ok, "생사결 신청 사건 강제: " + (forced.Reason ?? "성립"));
            app.Rebuild();
            var dialog = Find("EventDialog");
            Check(dialog != null, "선택 창이 뜸: " + Texts(dialog).FirstOrDefault());
            report.Add("    본문: " + s.PendingEvents.FirstOrDefault()?.Text);
            var soothe = Named<Button>("Choice1");
            var why = soothe?.GetComponent<Tooltip>()?.Text;
            Check(soothe != null && !soothe.interactable && why == "은자 200냥이 필요합니다", "\"은자로 달랜다\" 비활성, 사유: " + why);
            var seal = Find("Seal")?.GetComponent<Button>();
            Check(seal != null && !seal.interactable && Find("Seal").GetComponent<Tooltip>()?.Text == "먼저 사건에 답해야 합니다",
                "답하기 전 \"다음 달\" 비활성");
            Check(Named<Button>("EventsButton") != null, "상단 바 \"사건 1\" 단추");
            Shot("13_01_duel_challenge");

            Press(Named<Button>("Choice2"), "거절하고 몸을 피한다");
            var ok = Named<Button>("EventOk");
            Check(ok != null, "결과 문장과 확인: " + Texts(Find("EventDialog")).Skip(2).FirstOrDefault());
            Check(w.Events.Scheduled.Any(x => x.EventId == "grudge_ambush"), "몸을 피하자 암습이 예약됨");
            Shot("13_02_result");
            Press(ok, "확인");
            Check(Find("EventDialog") == null && s.CanEndTurn, "확인 → 창 닫힘, 다음 달 가능");

            // 소식: 원한 품은 이웃이 플레이어를 헐뜯는다 (결정자는 이웃).
            var rumor = s.ForceEvent("grudge_rumor", new Dictionary<string, string> { ["spreader"] = rival.Id, ["target"] = me.Id });
            app.Rebuild();
            var notice = s.PendingEvents.FirstOrDefault();
            Check(rumor.Ok && notice != null && notice.IsNotice, "소식: " + notice?.Text);
            Shot("13_03_notice");
            Press(Named<Button>("EventOk"), "소식 확인");

            // 답을 기다리는 사건이 있는 채로 저장 → 처음 화면 → 불러오기
            Check(s.ForceEvent("faction_volunteers").Ok, "입문 희망자 사건 강제");
            app.Rebuild();
            Click("저장");
            ClickIn("SaveLoad", "저장", 0);
            Click("처음 화면");
            Click("불러오기 1번 칸");
            var loaded = app.Session;
            Check(loaded != s && loaded.PendingEvents.Count == 1 && Find("EventDialog") != null,
                "불러오자 대기 사건 창이 다시 뜸: " + loaded.PendingEvents.FirstOrDefault()?.Title);
            Shot("13_04_reloaded");
            Press(Named<Button>("Choice0"), "받아들인다");
            Press(Named<Button>("EventOk"), "확인");

            // 표식 칩
            loaded.Player.SetFlag("injured", loaded.World.Month, 6);
            Click("나");
            var chip = Canvas.GetComponentsInChildren<Tooltip>().FirstOrDefault(t => t.name == "Flag");
            Check(chip != null, "인물 패널 표식 칩: " + chip?.Text);
            Shot("13_05_flags");

            // 2년 진행: 뜨는 창마다 고를 수 있는 첫 선택지
            int dialogs = 0, start = loaded.World.Month;
            var titles = new List<string>();
            for (int m = 0; m < 24; m++)
            {
                for (int guard = 0; guard < 10 && loaded.PendingEvents.Count > 0; guard++)
                {
                    dialogs++;
                    titles.Add(loaded.PendingEvents[0].Title);
                    var first = Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name.StartsWith("Choice") && b.interactable);
                    if (first != null) Press(first, "첫 선택지");
                    Press(Named<Button>("EventOk"), "확인");
                }
                if (dialogs == 1 && m == 0) Shot("13_06_first_storyteller_event");
                Click("다음 달");
            }
            Check(loaded.World.Month == start + 24, $"2년 진행: {GameDate.Format(start)} → {GameDate.Format(loaded.World.Month)}");
            Check(dialogs > 0, $"그동안 받은 창 {dialogs}개: {string.Join(", ", titles)}");
            if (loaded.PendingEvents.Count > 0)
            {
                Shot("13_07_event_dialog");
                var first = Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name.StartsWith("Choice") && b.interactable);
                if (first != null) Press(first, "첫 선택지");
                Press(Named<Button>("EventOk"), "확인");
            }

            Click("사건만");
            int rows = ChronicleRows();
            int events = loaded.World.Chronicle.Count(e => e.Kind.StartsWith("Event:"));
            Check(rows > 0 && rows == System.Math.Min(events, 300), $"연대기 \"사건만\": {rows}줄 (사건 기록 {events}개)");
            Shot("13_08_chronicle_events");
            Click("전체");
        }

        /// <summary>3단계: 무공 영역, 주무공, 폐관, 출관, 사건 결투 경과, 연대기 펼침, 무공 전수, 비무 소식, 돌파의 기회.
        /// -executeMethod Murim.Editor.UiScenario.RunTask14 → TestResults/snapshots/14_*.png, task14.txt</summary>
        public static void RunTask14()
        {
            report.Clear();
            failures = 0;
            var backups = Backup(1);
            try
            {
                app = UiSnapshot.CreateApp(out camera);
                MartialArts();
                Wandering();
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
            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "snapshots", "task14.txt");
            File.WriteAllLines(outPath, report);
            Debug.Log("[T14]\n" + string.Join("\n", report));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        /// <summary>떠 있는 소식·사건 창을 모두 닫는다 (사건은 고를 수 있는 첫 선택지).</summary>
        static void DismissAll()
        {
            for (int guard = 0; guard < 12 && app.Session.PendingEvents.Count > 0; guard++)
            {
                if (Find("EventDialog") == null) app.Rebuild();
                var first = Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name.StartsWith("Choice") && b.interactable);
                if (first != null) first.onClick.Invoke();
                var ok = Named<Button>("EventOk");
                if (ok != null) ok.onClick.Invoke();
            }
        }

        static void MartialArts()
        {
            var s = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 42 });
            app.StartGame(s);
            var gs = new GameScreen(app);
            app.Show(gs);
            var w = s.World;
            var me = s.Player;
            Murim.Core.Martial.ArtGranting.Learn(app.Data, me, "breeze_sword", 3);
            DismissAll();

            Click("나");
            Check(Find("QiBar") != null && Find("InsightBar") != null, "무공 영역: 내공·깨달음 막대");
            var artTips = Canvas.GetComponentsInChildren<Tooltip>().Where(t => t.name == "Art").ToList();
            var cardTip = artTips.FirstOrDefault(t => t.Text.Contains("위력"));
            Check(artTips.Count == me.Arts.Count && cardTip != null,
                $"무공 줄 {artTips.Count}개, 카드 툴팁: {cardTip?.Text.Split('\n').Skip(1).FirstOrDefault()}");
            Shot("14_01_arts");

            var before = Murim.Core.Martial.ArtGranting.MainArt(app.Data, me)?.Id;
            Click("주무공 바꾸기");
            PickFirst("주무공");
            var after = Murim.Core.Martial.ArtGranting.MainArt(app.Data, me)?.Id;
            Check(after != null && after != before, $"주무공 바꾸기: {before} → {after}");

            Click("폐관수련");
            Shot("14_02_training_picker");
            PickFirst("폐관 기간");
            Check(s.Pending.Count == 1 && s.Pending[0] is ClosedTraining, "폐관 3개월 예약");
            Click("다음 달");
            var actions = Find("ActionsLabel")?.GetComponent<Text>()?.text;
            Check(actions != null && actions.StartsWith("폐관 중"), "상단 바: " + actions);
            Check(s.PendingEvents.All(p => p.IsNotice), "폐관 중 이야기꾼 사건 없음");
            Shot("14_03_in_training");
            DismissAll();
            Click("다음 달");
            DismissAll();
            Click("다음 달");
            var exit = s.PendingEvents.FirstOrDefault(p => p.EventId == "art_closed_training_end");
            Check(exit != null && Find("EventDialog") != null, "출관 사건 창: " + exit?.Text);
            Shot("14_04_training_end");
            DismissAll();

            // 사건 결투: 공개 시범
            var forced = s.ForceEvent("art_rival_demonstration");
            Check(forced.Ok, "공개 시범 사건: " + (forced.Reason ?? "성립"));
            if (forced.Ok)
            {
                app.Rebuild();
                Press(Named<Button>("Choice0"), "받아들인다");
                var log = Find("DuelLog");
                var duelLines = log == null ? new List<Text>() : log.GetComponentsInChildren<Text>().Where(t => t.name == "DuelLine").ToList();
                Check(log != null && duelLines.Count >= 4, $"결과 창에 결투 경과 {duelLines.Count}줄");
                foreach (var t in duelLines.Take(4)) report.Add("    " + t.text);
                Shot("14_05_duel_result");
                Press(Named<Button>("EventOk"), "확인");
            }
            DismissAll();

            gs.ShowHome();
            var detailRow = Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "DetailRow");
            Check(detailRow != null, "연대기에 [결투 경과] 줄");
            Press(detailRow, "연대기 줄 펼치기");
            Check(Find("ChronicleDuelLog") != null, "연대기 펼침");
            Shot("14_06_chronicle_expanded");
            Press(Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "DetailRow"), "연대기 줄 접기");
            Check(Find("ChronicleDuelLog") == null, "다시 누르면 접힘");

            // 무공 전수: 조건이 맞는 제자에게
            var own = Wallet.LedFaction(w, me);
            var student = FactionOps.LivingMembers(w, own).FirstOrDefault(m => m.Id != me.Id && me.Arts.Any(k =>
                new TeachArt(me.Id, m.Id, k.ArtId).CanExecute(w, app.Data).Ok));
            Check(student != null, "전수할 수 있는 제자: " + student?.FullName);
            if (student != null)
            {
                gs.ShowCharacter(student.Id);
                Shot("14_07_student");
                Click("무공 전수");
                PickFirst("전수할 무공");
                Check(s.Pending.Any(p => p is TeachArt), "무공 전수 예약: " + s.Pending.LastOrDefault()?.Describe(w));
                Click("다음 달");
                Check(w.Chronicle.Any(e => e.Kind == "ArtTaught"), "연대기: " + w.Chronicle.LastOrDefault(e => e.Kind == "ArtTaught")?.Text);
                DismissAll();
            }

            // 돌파의 기회
            me.Qi = Murim.Core.Martial.Cultivation.ThresholdYears[me.Realm] * 12;
            me.Enlightenment = 100;
            w.Events.Cooldowns.Clear();
            gs.ShowHome();
            Click("다음 달");
            var chance = s.PendingEvents.FirstOrDefault(p => p.EventId == "art_breakthrough_chance");
            Check(chance != null, "돌파의 기회 사건: " + chance?.Text);
            if (chance != null)
            {
                while (s.PendingEvents.Count > 0 && s.PendingEvents[0].EventId != "art_breakthrough_chance")
                {
                    var first = Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name.StartsWith("Choice") && b.interactable);
                    if (first != null) first.onClick.Invoke();
                    Press(Named<Button>("EventOk"), "앞선 창 확인");
                }
                Shot("14_08_breakthrough_chance");
                int realm = me.Realm;
                Press(Named<Button>("Choice0"), "지금 벽을 두드린다");
                Check(Named<Button>("EventOk") != null, $"돌파 결과: 경지 {realm} → {me.Realm}");
                Shot("14_09_breakthrough_result");
                Press(Named<Button>("EventOk"), "확인");
            }
            DismissAll();
            Click("나");
            Shot("14_10_arts_after");
        }

        static void Wandering()
        {
            var s = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "wandering_hero", RegionId = "henan", Seed = 7 });
            app.StartGame(s);
            var gs = new GameScreen(app);
            app.Show(gs);
            DismissAll();
            var spar = Buttons(null, "비무").FirstOrDefault();
            Check(spar != null && spar.interactable, "비무 단추");
            if (spar == null || !spar.interactable) return;
            Click("비무");
            PickFirst("비무 상대");
            Click("다음 달");
            var notice = s.PendingEvents.FirstOrDefault(p => p.IsNotice && p.Title == "비무");
            Check(notice != null && notice.Detail != null, "비무 소식: " + notice?.Text);
            while (s.PendingEvents.Count > 0 && !(s.PendingEvents[0].IsNotice && s.PendingEvents[0].Title == "비무"))
            {
                var first = Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name.StartsWith("Choice") && b.interactable);
                if (first != null) first.onClick.Invoke();
                Press(Named<Button>("EventOk"), "앞선 창 확인");
            }
            Check(Find("DuelLog") != null, "소식 창에 결투 경과");
            Shot("14_11_spar_notice");
            DismissAll();
        }

        /// <summary>4단계: 장원 화면 진입, 연무장 놓기(미리보기), 완공, 실시간 일과, 일시정지, 허물기.
        /// -executeMethod Murim.Editor.UiScenario.RunTask15 → TestResults/snapshots/15_*.png, task15.txt</summary>
        public static void RunTask15()
        {
            report.Clear();
            failures = 0;
            try
            {
                app = UiSnapshot.CreateApp(out camera);
                ManorFlow();
            }
            catch (Exception e)
            {
                Check(false, "예외: " + e);
            }
            report.Add(failures == 0 ? "결과: 모두 통과" : $"결과: 실패 {failures}개");
            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "snapshots", "task15.txt");
            File.WriteAllLines(outPath, report);
            Debug.Log("[T15]\n" + string.Join("\n", report));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void ManorFlow()
        {
            var s = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 42 });
            app.StartGame(s);
            var w = s.World;
            var own = Wallet.LedFaction(w, s.Player);
            own.Silver = 2000;
            DismissAll();
            var gs = new GameScreen(app);
            app.Show(gs);
            gs.ShowFaction(own.Id);
            Click("장원 보기");
            var manor = app.Current as ManorScreen;
            Check(manor != null && Find("ManorGrid") != null, "장원 화면: " + Find("TopBar")?.GetComponentInChildren<Text>()?.text);
            Shot("15_01_manor");

            Press(Named<Button>("연무장"), "연무장");
            Check(manor.Placing == "training_ground", "연무장 고름");
            bool ok = manor.HoverCell(2, 2);
            var ghost = Find("Ghost")?.GetComponent<Image>();
            Check(ok && ghost != null && ghost.color.g > ghost.color.r, "빈 땅 (2,2) 미리보기 초록");
            Shot("15_02_ghost_ok");
            bool bad = manor.HoverCell(10, 6);
            ghost = Find("Ghost")?.GetComponent<Image>();
            Check(!bad && ghost.color.r > ghost.color.g, "본당 자리 (10,6) 미리보기 빨강");
            Shot("15_03_ghost_blocked");
            manor.ClickCell(2, 2);
            Check(own.Silver == 1700 && own.Manor.Buildings.Any(b => b.DefId == "training_ground"), $"연무장 공사 시작, 은자 {own.Silver}");
            Shot("15_04_under_construction");

            Click("지도로");
            for (int i = 0; i < 3; i++)
            {
                DismissAll();
                Click("다음 달");
            }
            DismissAll();
            var done = w.Chronicle.LastOrDefault(e => e.Kind == "BuildingCompleted");
            Check(done != null, "완공 연대기: " + done?.Text);

            gs = new GameScreen(app);
            app.Show(gs);
            gs.ShowFaction(own.Id);
            Click("장원 보기");
            manor = (ManorScreen)app.Current;
            Check(Find("Building:" + own.Manor.Buildings.First(b => b.DefId == "training_ground").Id) != null, "완공된 연무장");
            var disciple = ManorLife.Residents(w, own).First(c => c.Id != s.Player.Id && !c.InTraining(w.Month));
            float h0 = manor.Hour;
            manor.Advance(10f);
            Check(manor.Hour > h0, $"시계 {h0:0.0}시 → {manor.Hour:0.0}시");
            // 6~8시는 수련 시간: 연무장으로 걸어간다.
            while ((int)manor.Hour != 6) manor.Advance(0.2f);
            manor.Advance(4f);
            var cell = manor.DotCell(disciple.Id);
            var tg = own.Manor.Buildings.First(b => b.DefId == "training_ground");
            Check(cell.x >= tg.X && cell.x <= tg.X + 3 && cell.y >= tg.Y && cell.y <= tg.Y + 3,
                $"{disciple.FullName} {(int)manor.Hour}시에 연무장 안 ({cell.x:0.0},{cell.y:0.0})");
            int activities = Canvas.GetComponentsInChildren<Text>().Count(t => t.name == "Activity" && t.text.Contains("연무장에서 수련"));
            Check(activities > 0, $"일과 목록에 연무장 수련 {activities}명");
            Shot("15_05_morning_training");

            Click("일시정지");
            manor = (ManorScreen)app.Current;
            float paused = manor.Hour;
            manor.Advance(5f);
            Check(manor.Hour == paused && Find("Clock").GetComponent<Text>().text.Contains("멈춤"), "일시정지: 시계 멈춤");
            Click("1배");
            manor = (ManorScreen)app.Current;

            Click("허물기");
            int before = own.Silver;
            manor.ClickCell(tg.X, tg.Y);
            Check(own.Silver == before + 150 && own.Manor.Buildings.All(b => b.DefId != "training_ground"), $"허물기: 환불 {own.Silver - before}냥");
            Shot("15_06_demolished");
            Click("지도로");
            Check(app.Current is GameScreen, "지도로 돌아옴");
        }

        /// <summary>5단계: 습격 사건 → 습격 창 → 직접 지휘(고르기·이동·공격·진행) → 결과 → 연대기, 그리고 자동으로 맞서기.
        /// -executeMethod Murim.Editor.UiScenario.RunTask16 → TestResults/snapshots/16_*.png, task16.txt</summary>
        public static void RunTask16()
        {
            report.Clear();
            failures = 0;
            try
            {
                app = UiSnapshot.CreateApp(out camera);
                RaidFlow();
            }
            catch (Exception e)
            {
                Check(false, "예외: " + e);
            }
            report.Add(failures == 0 ? "결과: 모두 통과" : $"결과: 실패 {failures}개");
            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "snapshots", "task16.txt");
            File.WriteAllLines(outPath, report);
            Debug.Log("[T16]\n" + string.Join("\n", report));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void RaidFlow()
        {
            var s = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 42 });
            app.StartGame(s);
            DismissAll();
            var w = s.World;
            var own = Wallet.LedFaction(w, s.Player);
            own.Silver = 2000;
            // 담장 몇 칸과 망루를 세워 둔다.
            foreach (var x in new[] { 8, 9, 10, 13, 14, 15 }) s.PlaceBuilding("wall", x, 12);
            s.PlaceBuilding("watchtower", 11, 11);
            foreach (var b in own.Manor.Buildings) b.CompleteMonth = w.Month;
            w.Regions[own.HomeRegionId].Stability = 30;

            Check(s.ForceEvent("raid_bandits").Ok, "산적떼 습격 사건");
            app.Rebuild();
            Shot("16_01_raid_event");
            Press(Named<Button>("Choice0"), "맞서 싸운다");
            Press(Named<Button>("EventOk"), "확인");
            Check(s.PendingRaid != null && Find("RaidPrompt") != null, "습격 창: " + s.PendingRaid?.AttackerName);
            var seal = Find("Seal")?.GetComponent<Button>();
            Check(seal != null && !seal.interactable, "습격 중에는 다음 달 불가");
            Shot("16_02_raid_prompt");

            Click("직접 지휘한다");
            var bs = app.Current as BattleScreen;
            Check(bs != null && Find("BattleGrid") != null, "전투 화면");
            var battle = bs.Battle;
            Check(battle.Units.Count(u => u.Side == Murim.Core.Battle.BattleSide.Attacker) == 9, "습격자 9 (두목 + 졸개 8)");
            Shot("16_03_battle_start");

            var hero = battle.Units.First(u => u.Side == Murim.Core.Battle.BattleSide.Defender && u.Named);
            bs.ClickUnit(hero.Id);
            Check(bs.Selected.Contains(hero.Id), "아군 고르기: " + hero.Name);
            bs.ClickCell(11, 11);
            Check(hero.Order != null && !hero.Order.Attack && hero.Order.X == 11 && hero.Order.Y == 11, "망루로 이동 명령");
            var second = battle.Units.First(u => u.Side == Murim.Core.Battle.BattleSide.Defender && u.Named && u.Id != hero.Id);
            bs.ClickUnit(second.Id);
            var boss = battle.Units.First(u => u.Side == Murim.Core.Battle.BattleSide.Attacker && u.IsLeader);
            bs.ClickUnit(boss.Id);
            Check(second.Order != null && second.Order.Attack && second.Order.TargetId == boss.Id, $"{second.Name} → 두목 {boss.Name} 공격 명령");
            Shot("16_04_orders");

            int tick = battle.Tick;
            bs.Advance(3f);
            Check(battle.Tick == tick, "멈춤 상태에서는 시간이 흐르지 않는다");
            Click("1배");
            bs = (BattleScreen)app.Current;
            bs.Advance(6f);
            Check(battle.Tick > tick, $"진행: {battle.Tick}틱");
            Check(hero.CellX == 11 && hero.CellY == 11, $"{hero.Name} 망루 위 ({hero.CellX},{hero.CellY})");
            Shot("16_05_battle_mid");

            Click("자동으로 끝내기");
            var result = Find("BattleResult")?.GetComponent<Text>()?.text;
            Check(result != null && s.PendingRaid == null, "결과: " + result);
            foreach (var t in Canvas.GetComponentsInChildren<Text>().Where(t => t.name == "DuelLine").Take(5)) report.Add("    " + t.text);
            Shot("16_06_battle_result");
            Click("확인");
            Check(app.Current is GameScreen && w.Chronicle.Any(e => e.Kind == "Raid" && e.Detail != null), "연대기에 습격 기록과 경과");
            DismissAll();

            // 자동으로 맞서기
            w.Events.Cooldowns.Clear();
            Check(s.ForceEvent("raid_bandits").Ok, "두 번째 습격");
            app.Rebuild();
            Press(Named<Button>("Choice0"), "맞서 싸운다");
            Press(Named<Button>("EventOk"), "확인");
            Click("자동으로 맞선다");
            Check(Find("BattleResult") != null && s.PendingRaid == null, "자동 결과: " + Find("BattleResult")?.GetComponent<Text>()?.text);
            Shot("16_07_auto_result");
            Click("확인");
            Check(Find("RaidPrompt") == null && s.CanEndTurn, "확인 → 다음 달 가능");
        }

        static T Named<T>(string name) where T : Component =>
            Canvas.GetComponentsInChildren<T>().LastOrDefault(x => x.name == name);

        static IEnumerable<string> Texts(Transform t) =>
            t == null ? Enumerable.Empty<string>() : t.GetComponentsInChildren<Text>().Select(x => x.text);

        static void MinorSect()
        {
            var s = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 42 });
            s.AutoResolveEvents = true; // 1단계 흐름만 본다 (사건 창은 RunTask13)
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
            s.AutoResolveEvents = true;
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
