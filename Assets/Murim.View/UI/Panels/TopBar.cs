using System;
using Murim.Core.Actions;
using Murim.Core.Session;
using Murim.Core.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>연월, 자원, 남은 행동, 예약 목록, 저장, "다음 달" 인장.</summary>
    public static class TopBar
    {
        public static void Build(RectTransform bar, GameSession s, Action onEndTurn, Action onSave, Action onTitle, Action onChanged,
            Action onEvents = null)
        {
            var w = s.World;
            var p = s.Player;
            UiFactory.HStack(bar, 18, 0).padding = new RectOffset(28, 20, 10, 10);

            UiFactory.Label(bar, GameDate.Format(w.Month), Theme.HeadingSize, Theme.Ink, true).Size(190);
            if (p != null)
            {
                UiFactory.Label(bar, Titles.Full(w, p), Theme.BodySize, Theme.InkDim).Size(340);
                UiFactory.Label(bar, $"은자 {Wallet.Silver(w, p):N0}냥", Theme.BodySize, Theme.Gold).Size(150);
                UiFactory.Label(bar, $"명성 {Wallet.Fame(w, p)}", Theme.BodySize, Theme.Ink).Size(100);
            }

            UiFactory.Rect(bar, "Spacer").Size(-1, -1, 1);

            int max = p == null ? 0 : ActionBudget.Max(p);
            string actions = p != null && p.InTraining(w.Month) ? $"폐관 중 ({p.TrainingUntil - w.Month}개월 남음)"
                : p != null && p.BusyUntilMonth > w.Month ? "표국 호위 중" : $"남은 행동 {s.RemainingActions}/{max}";
            UiFactory.Label(bar, actions, Theme.BodySize, p != null && p.InTraining(w.Month) ? Theme.Gold : Theme.Ink, align: TextAnchor.MiddleRight).Size(230).name = "ActionsLabel";

            var pendingBtn = UiFactory.Button(bar, $"예약 {s.Pending.Count}", null, s.Pending.Count > 0, "예약한 행동이 없습니다", width: 110);
            pendingBtn.onClick.AddListener(() => ShowPending(bar, s, onChanged));
            if (s.PendingEvents.Count > 0)
            {
                var ev = UiFactory.Button(bar, $"사건 {s.PendingEvents.Count}", onEvents, width: 110);
                ev.GetComponent<Image>().color = Theme.Gold;
                ev.GetComponentInChildren<Text>().color = Theme.Ground;
                ev.name = "EventsButton";
            }
            UiFactory.Button(bar, "저장", onSave, width: 90);
            UiFactory.Button(bar, "처음 화면", onTitle, width: 120);

            bool canEnd = !w.GameOver && s.CanEndTurn;
            var seal = UiFactory.Panel(bar, canEnd ? Theme.Seal : Theme.Disabled, "Seal");
            seal.Size(132, 60);
            seal.raycastTarget = true;
            var sealBtn = seal.gameObject.AddComponent<Button>();
            sealBtn.interactable = canEnd;
            if (canEnd) sealBtn.onClick.AddListener(() => onEndTurn());
            else if (!w.GameOver) Tooltip.Attach(seal.gameObject, s.PendingRaid != null && s.PendingEvents.Count == 0 ? "먼저 습격을 막아야 합니다" : "먼저 사건에 답해야 합니다");
            var sealText = UiFactory.Label(seal.transform, w.GameOver ? "대가 끊김" : "다음 달", Theme.SubheadingSize,
                canEnd ? Theme.SealInk : Theme.InkFaint, true, TextAnchor.MiddleCenter);
            sealText.rectTransform.Fill();
            var frame = UiFactory.Panel(seal.transform, new Color(0, 0, 0, 0), "Frame");
            frame.rectTransform.Fill(5, 5, 5, 5);
            frame.gameObject.AddComponent<Outline>().effectColor = new Color(Theme.SealInk.r, Theme.SealInk.g, Theme.SealInk.b, 0.6f);
        }

        static void ShowPending(RectTransform bar, GameSession s, Action onChanged)
        {
            var canvas = bar.GetComponentInParent<Canvas>().rootCanvas.transform;
            var shade = UiFactory.Panel(canvas, new Color(0, 0, 0, 0.45f), "PendingShade");
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            shade.gameObject.AddComponent<Button>().onClick.AddListener(() => UiFactory.Remove(shade.gameObject));
            var box = UiFactory.Panel(shade.transform, Theme.Panel, "Pending");
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(1, 1);
            box.rectTransform.pivot = new Vector2(1, 1);
            box.rectTransform.anchoredPosition = new Vector2(-420, -90);
            box.rectTransform.sizeDelta = new Vector2(520, 60 + s.Pending.Count * 56);
            box.raycastTarget = true;
            UiFactory.VStack(box.transform, 8, 16);
            UiFactory.Label(box.transform, "이번 달에 할 일", Theme.SubheadingSize, Theme.Ink, true).Size(-1, 32);
            for (int i = 0; i < s.Pending.Count; i++)
            {
                int index = i;
                var row = UiFactory.Row(box.transform, 10, 44);
                UiFactory.Label(row, s.Pending[i].Describe(s.World), Theme.BodySize).Size(-1, -1, 1);
                UiFactory.Button(row, "취소", () =>
                {
                    s.Unqueue(index);
                    UiFactory.Remove(shade.gameObject);
                    onChanged();
                }, width: 80);
            }
        }
    }
}
