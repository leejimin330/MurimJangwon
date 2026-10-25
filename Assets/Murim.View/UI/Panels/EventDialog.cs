using System.Linq;
using Murim.Core.Events;
using Murim.Core.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>답을 기다리는 사건을 하나씩 보여 주는 선택 창. 바깥을 눌러도 닫히지 않는다.</summary>
    public static class EventDialog
    {
        /// <param name="parent">화면 루트. 화면을 다시 그리면 창도 함께 지워지고, 남은 사건이 있으면 다시 뜬다.</param>
        public static void Show(Bootstrap app, INav nav, RectTransform parent)
        {
            var s = app.Session;
            if (s == null || s.PendingEvents.Count == 0) return;
            var p = s.PendingEvents[0];
            var ctx = s.PendingContext(0);

            var shade = UiFactory.Panel(parent, new Color(0, 0, 0, 0.6f), "EventShade");
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var box = UiFactory.Panel(shade.transform, Theme.Panel, "EventDialog");
            box.raycastTarget = true;
            box.rectTransform.Area(0.27f, 0.16f, 0.73f, 0.84f);

            var strip = UiFactory.Panel(box.transform, Theme.OfCategory(ctx?.Def.Category ?? Category(app, p.EventId)), "Strip");
            strip.rectTransform.anchorMin = new Vector2(0, 1);
            strip.rectTransform.anchorMax = new Vector2(1, 1);
            strip.rectTransform.pivot = new Vector2(0.5f, 1);
            strip.rectTransform.sizeDelta = new Vector2(0, 8);

            var inner = UiFactory.Rect(box.transform, "Inner").Fill(36, 32, 36, 28);
            UiFactory.VStack(inner, 14);
            var head = UiFactory.Row(inner, 12, 48);
            UiFactory.Label(head, p.Title ?? "사건", Theme.HeadingSize, Theme.Ink, true).Size(-1, 48, 1);
            string count = s.PendingEvents.Count > 1 ? $"  (남은 일 {s.PendingEvents.Count})" : "";
            UiFactory.Label(head, (p.IsNotice ? "소식" : GameDate.Format(p.Month)) + count, Theme.SmallSize, Theme.InkFaint,
                align: TextAnchor.MiddleRight).Size(260, 48);

            if (ctx != null) People(inner, app, nav, ctx);
            var body = UiFactory.Label(inner, p.Text, Theme.BodySize + 2, Theme.Ink, align: TextAnchor.UpperLeft);
            body.Size(-1, -1).flexibleHeight = 1;

            var choices = UiFactory.Rect(inner, "Choices");
            UiFactory.VStack(choices, 10);
            choices.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (ctx == null || p.IsNotice)
            {
                Ok(choices, app, () => s.Choose(0, 0));
                // 소식·흐지부지된 사건은 확인만 누르면 된다.
                return;
            }
            for (int i = 0; i < ctx.Def.Choices.Count; i++)
            {
                int index = i;
                var check = s.CanChoose(0, i);
                var b = UiFactory.Button(choices, EventResolver.ChoiceLabel(ctx, i), () =>
                {
                    var outcome = s.Choose(0, index);
                    if (!outcome.Ok)
                    {
                        app.Toast(outcome.Reason);
                        return;
                    }
                    ShowResult(app, choices, body, outcome.Text);
                }, check.Ok, check.Reason, height: 52);
                b.name = "Choice" + i;
                var hint = ctx.Def.Choices[i].Hint;
                if (check.Ok && !string.IsNullOrEmpty(hint)) Tooltip.Attach(b.gameObject, hint);
            }
        }

        static string Category(Bootstrap app, string eventId) =>
            eventId != null && app.Data.Events.TryGetValue(eventId, out var e) ? e.Category : null;

        /// <summary>결정자와 상대 한 명의 초상화. 누르면 인물 패널로 간다.</summary>
        static void People(RectTransform inner, Bootstrap app, INav nav, EventContext ctx)
        {
            var w = app.Session.World;
            var shown = ctx.Def.BindOrder.Select(r => ctx.Get(r)).Where(c => c != null && !ctx.IsSpawned(c)).Take(2).ToList();
            var spawned = ctx.Def.BindOrder.Where(r => ctx.IsSpawned(ctx.Get(r))).Select(r => (role: r, c: ctx.Get(r))).FirstOrDefault();
            if (shown.Count == 0) return;
            var row = UiFactory.Row(inner, 18, 92);
            foreach (var c in shown)
            {
                var cell = UiFactory.Rect(row, "Person");
                cell.Size(250, 92);
                UiFactory.HStack(cell, 10);
                Portrait.Build(cell, c, w.Month, 84);
                var id = c.Id;
                var name = UiFactory.Button(cell, c.FullName, () =>
                {
                    nav?.ShowCharacter(id);
                }, size: Theme.SmallSize + 1, width: 150, height: 40);
                name.name = "PersonLink";
            }
            if (spawned.c != null)
            {
                var cell = UiFactory.Rect(row, "Spawned");
                cell.Size(250, 92);
                UiFactory.HStack(cell, 10);
                Portrait.Build(cell, spawned.c, w.Month, 84);
                UiFactory.Label(cell, (ctx.EpithetOf(spawned.role) ?? "") + "\n" + spawned.c.FullName, Theme.SmallSize, Theme.InkDim).Size(150, 84);
            }
        }

        /// <summary>선택 결과를 보여 주고 "확인"을 기다린다.</summary>
        static void ShowResult(Bootstrap app, RectTransform choices, Text body, string result)
        {
            UiFactory.Clear(choices);
            if (!string.IsNullOrEmpty(result)) body.text += "\n\n" + result;
            Ok(choices, app, null);
        }

        static void Ok(RectTransform choices, Bootstrap app, System.Action before)
        {
            var ok = UiFactory.Button(choices, "확인", () =>
            {
                before?.Invoke();
                app.Rebuild();
            }, width: 200, height: 52);
            ok.name = "EventOk";
        }
    }
}
