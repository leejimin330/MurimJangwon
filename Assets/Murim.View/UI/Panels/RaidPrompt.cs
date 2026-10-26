using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Battle;
using Murim.Core.Text;
using Murim.View.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>"습격!" 창: 직접 지휘하거나 자동으로 맞선다. 자동이면 결과를 이 창에서 보여 준다.</summary>
    public static class RaidPrompt
    {
        public static void Show(Bootstrap app, RectTransform parent)
        {
            var s = app.Session;
            var raid = s?.PendingRaid;
            if (raid == null) return;
            var w = s.World;

            var shade = UiFactory.Panel(parent, new Color(0, 0, 0, 0.6f), "RaidShade");
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var box = UiFactory.Panel(shade.transform, Theme.Panel, "RaidPrompt");
            box.raycastTarget = true;
            box.rectTransform.Area(0.3f, 0.2f, 0.7f, 0.8f);
            var strip = UiFactory.Panel(box.transform, Theme.Seal, "Strip");
            strip.rectTransform.anchorMin = new Vector2(0, 1);
            strip.rectTransform.anchorMax = new Vector2(1, 1);
            strip.rectTransform.pivot = new Vector2(0.5f, 1);
            strip.rectTransform.sizeDelta = new Vector2(0, 8);

            var inner = UiFactory.Rect(box.transform, "Inner").Fill(36, 32, 36, 28);
            UiFactory.VStack(inner, 14);
            UiFactory.Label(inner, "습격!", Theme.HeadingSize + 6, Theme.Ink, true).Size(-1, 54);
            UiFactory.Label(inner, $"{raid.AttackerName}의 무리가 장원으로 몰려온다!", Theme.BodySize + 2, Theme.Ink).Size(-1, 34);

            var leader = w.Get(raid.AttackerLeaderId);
            var allies = raid.AttackerIds.Select(w.Get).Where(c => c != null && c.IsAlive).ToList();
            string enemy = leader == null ? "두목 없음" : $"두목 {leader.FullName} ({Titles.Realm(leader.Realm)})";
            UiFactory.Label(inner, $"적: {enemy}, 이름난 무인 {allies.Count}명, 졸개 {raid.AttackerGrunts}명", Theme.BodySize, Theme.Unorthodox).Size(-1, 30);
            var own = s.Player == null ? null : Wallet.LedFaction(w, s.Player);
            int named = own == null ? 1 : BattleSetup.PickDefenders(w, own).Count;
            int grunts = own == null ? 0 : System.Math.Min(raid.DefenderGrunts, own.UnnamedDisciples);
            UiFactory.Label(inner, $"아군: 이름 있는 인물 {named}명, 문도 {grunts}명", Theme.BodySize, Theme.Righteous).Size(-1, 30);

            var area = UiFactory.Rect(inner, "Body");
            UiFactory.VStack(area, 8);
            area.Size(-1, -1).flexibleHeight = 1;
            UiFactory.Label(area, "직접 지휘하면 장원 지도에서 인물을 골라 자리와 상대를 정한다. 일시정지(Space) 중에도 명령을 내릴 수 있다.",
                Theme.SmallSize, Theme.InkDim).Size(-1, 50);

            var row = UiFactory.Row(inner, 14, 56);
            var command = UiFactory.Button(row, "직접 지휘한다", () => app.Show(new BattleScreen(app, s.StartBattle())), width: 260, height: 56);
            command.GetComponent<Image>().color = Theme.Seal;
            command.GetComponentInChildren<Text>().color = Theme.SealInk;
            UiFactory.Button(row, "자동으로 맞선다", () =>
            {
                var outcome = s.AutoResolveRaid();
                UiFactory.Clear(area);
                UiFactory.Clear(row);
                ShowOutcome(app, area, outcome);
                UiFactory.Button(row, "확인", app.Rebuild, width: 200, height: 56).name = "RaidOk";
            }, width: 260, height: 56);
        }

        /// <summary>결과 요약과 전투 기록. 전투 화면 끝에도 쓴다.</summary>
        public static void ShowOutcome(Bootstrap app, RectTransform area, RaidOutcome outcome)
        {
            if (outcome == null)
            {
                UiFactory.Label(area, "습격은 흐지부지 끝났다.", Theme.BodySize, Theme.Ink).Size(-1, 34);
                return;
            }
            var result = UiFactory.Label(area, outcome.Result, Theme.BodySize + 1, outcome.DefendersWon ? Theme.Gold : Theme.Seal);
            result.name = "BattleResult";
            if (outcome.Dead.Count > 0) UiFactory.Label(area, "죽은 이: " + string.Join(", ", outcome.Dead), Theme.SmallSize + 1, Theme.Seal);
            if (outcome.Injured.Count > 0) UiFactory.Label(area, "다친 이: " + string.Join(", ", outcome.Injured), Theme.SmallSize + 1, Theme.InkDim);
            if (outcome.Lines.Length > 0) DuelLogView.Build(area, app.Data, outcome.Lines, 260);
        }
    }
}
