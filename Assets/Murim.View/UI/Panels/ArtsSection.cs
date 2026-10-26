using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Martial;
using Murim.Core.Rules;
using Murim.Core.Text;
using Murim.Core.World;
using UnityEngine;

namespace Murim.View.UI.Panels
{
    /// <summary>인물 패널의 무공·경지 영역: 내공·깨달음 막대, 익힌 무공과 성취, 폐관·주무공·전수.</summary>
    public static class ArtsSection
    {
        public static void Build(RectTransform list, Bootstrap app, Character c)
        {
            var s = app.Session;
            var w = s.World;
            var d = s.Data;
            if (d.Arts.Count == 0) return;

            FactionPanel.Heading(list, "무공·경지");
            int qi = Cultivation.QiYears(c);
            int next = Cultivation.NextThresholdYears(c);
            bool top = c.Realm >= SimConstants.MaxRealm;
            Meter(list, top ? $"{Titles.Realm(c.Realm)}   내공 {qi}년" : $"{Titles.Realm(c.Realm)}   내공 {qi}년 / 다음 경지 {next}년",
                top ? 1f : Mathf.Clamp01(qi / (float)next), Theme.Righteous, "QiBar");
            bool ready = Cultivation.CanBreakthrough(c);
            Meter(list, ready ? $"깨달음 {c.Enlightenment}/100 — 벽을 넘을 때가 되었다" : $"깨달음 {c.Enlightenment}/100",
                c.Enlightenment / 100f, ready ? Theme.Gold : Theme.InkDim, "InsightBar");
            if (c.InTraining(w.Month))
                FactionPanel.Info(list, $"폐관 중 — 앞으로 {c.TrainingUntil - w.Month}개월", Theme.Gold);

            var main = ArtGranting.MainArt(d, c);
            foreach (var k in c.Arts.OrderBy(k => d.Arts.TryGetValue(k.ArtId, out var a) && a.IsInner ? 0 : 1))
            {
                if (!d.Arts.TryGetValue(k.ArtId, out var art)) continue;
                ArtRow(list, art, k.Mastery, main != null && main.Id == art.Id);
            }
            if (c.Arts.Count == 0) FactionPanel.Info(list, "익힌 무공이 없다");

            if (!c.IsAlive) return;
            var me = s.Player;
            var grid = ActionMenu.Grid(list);
            if (me != null && me.Id == c.Id)
            {
                ActionMenu.Opener(grid, app, "폐관수련", () => PickTraining(app));
                var externals = ArtGranting.Externals(d, c).Select(x => x.def).ToList();
                UiFactory.Button(grid, "주무공 바꾸기", () => PickMain(app), externals.Count > 1, "바꿀 외공이 없습니다");
            }
            var own = me == null ? null : Wallet.LedFaction(w, me);
            if (own != null && c.Id != me.Id && c.FactionId == own.Id)
                ActionMenu.Opener(grid, app, "무공 전수", () => PickTeach(app, c));
        }

        static void Meter(RectTransform list, string label, float value, Color color, string name)
        {
            var box = UiFactory.Rect(list, name);
            UiFactory.VStack(box, 4);
            box.Size(-1, 44);
            UiFactory.Label(box, label, Theme.BodySize, Theme.Ink).Size(-1, 28);
            UiFactory.Bar(box, value, color, 8);
        }

        static void ArtRow(RectTransform list, ArtDef art, int mastery, bool isMain)
        {
            var row = UiFactory.Panel(list, Theme.PanelRaised, "Art");
            row.Size(-1, 58);
            var inner = UiFactory.Rect(row.transform, "Inner").Fill(14, 6, 14, 6);
            UiFactory.VStack(inner, 4);
            string star = isMain ? "★ " : "";
            string kind = $"{ArtNames.Type(art.Type)} · {ArtNames.Grade(art.Grade)}" + (art.Faction != null ? " · 비전" : "");
            var head = UiFactory.Row(inner, 10, 26);
            UiFactory.Label(head, star + art.Name, Theme.BodySize, isMain ? Theme.Gold : Theme.Ink).Size(-1, 26, 1);
            UiFactory.Label(head, kind, Theme.SmallSize, Theme.InkDim, align: TextAnchor.MiddleRight).Size(220, 26);
            UiFactory.Label(head, $"{mastery}성", Theme.BodySize, Theme.Ink, align: TextAnchor.MiddleRight).Size(56, 26);
            var cells = UiFactory.Row(inner, 3, 10);
            for (int i = 1; i <= ArtGranting.MaxMastery; i++)
                UiFactory.Panel(cells, i <= mastery ? (art.Alignment == Murim.Core.Data.Alignment.Demonic ? Theme.Demonic : Theme.Gold) : Theme.Ground, "Cell")
                    .Size(-1, 10, 1);
            Tooltip.Attach(row.gameObject, Describe(art, mastery));
        }

        /// <summary>툴팁: 설명과 초식 카드 ("운중월 — 공격·유, 위력 14, 내력 4", 성취 미달이면 "(7성)").</summary>
        public static string Describe(ArtDef art, int mastery)
        {
            var lines = new List<string> { art.Description ?? art.Name };
            if (art.IsInner) lines.Add("내공이 쌓이는 속도와 체력을 높인다.");
            foreach (var card in art.Cards)
            {
                string locked = mastery < card.MasteryMin ? $"  ({card.MasteryMin}성)" : "";
                lines.Add($"{card.Name} — {ArtNames.Kind(card.Kind)}·{ArtNames.Style(card.Style)}, 위력 {card.Power}, 내력 {card.Cost}{locked}");
            }
            return string.Join("\n", lines);
        }

        static void PickTraining(Bootstrap app)
        {
            var me = app.Session.Player;
            var opts = new[] { 3, 6, 12 }.Select(m => ActionMenu.Option(app, $"{m}개월", new ClosedTraining(me.Id, m),
                $"깨달음 +{Cultivation.TrainingEnlightenment(m)}, 내공 ×3")).ToList();
            Picker.Show(app, "얼마나 폐관할까요", opts);
        }

        static void PickMain(Bootstrap app)
        {
            var s = app.Session;
            var me = s.Player;
            var main = ArtGranting.MainArt(s.Data, me);
            var opts = ArtGranting.Externals(s.Data, me).Select(x =>
            {
                var id = x.def.Id;
                return new Picker.Option(x.def.Name, () =>
                {
                    var r = s.SetMainArt(id);
                    app.Rebuild();
                    app.Toast(r.Ok ? "주무공: " + x.def.Name : r.Reason);
                }, $"{x.known.Mastery}성", main == null || main.Id != id, "이미 주무공입니다");
            }).ToList();
            Picker.Show(app, "주무공 바꾸기", opts);
        }

        static void PickTeach(Bootstrap app, Character student)
        {
            var s = app.Session;
            var me = s.Player;
            var opts = me.Arts.Where(k => s.Data.Arts.ContainsKey(k.ArtId)).Select(k =>
            {
                var art = s.Data.Arts[k.ArtId];
                return ActionMenu.Option(app, art.Name, new TeachArt(me.Id, student.Id, art.Id), $"{ArtNames.Type(art.Type)} · {ArtNames.Grade(art.Grade)}");
            }).ToList();
            Picker.Show(app, $"{student.FullName}에게 무엇을 가르칠까요", opts, "가르칠 무공이 없습니다");
        }
    }
}
