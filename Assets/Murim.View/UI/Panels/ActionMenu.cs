using System;
using System.Collections.Generic;
using Murim.Core.Actions;
using Murim.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>행동 버튼. 누르기 전에 실행 가능 여부를 확인하고, 안 되면 사유를 툴팁으로 보여 준다.</summary>
    public static class ActionMenu
    {
        public static Button Add(Transform parent, Bootstrap app, string label, IGameAction action, float width = -1)
        {
            string reason = BlockReason(app, action);
            return UiFactory.Button(parent, label, () => Queue(app, action), reason == null, reason, Theme.BodySize, width);
        }

        /// <summary>지금 예약할 수 없으면 그 사유, 할 수 있으면 null.</summary>
        public static string BlockReason(Bootstrap app, IGameAction action)
        {
            var s = app.Session;
            if (s.RemainingActions <= 0) return "이번 달에는 더 할 수 없습니다";
            var check = action.CanExecute(s.World, s.Data);
            if (!check.Ok) return check.Reason;
            if (action is IWholeMonthAction && s.Pending.Count > 0) return "이동은 다른 행동과 함께 할 수 없습니다";
            return null;
        }

        /// <summary>행동을 고르는 선택지. 예약할 수 없으면 비활성 + 사유.</summary>
        public static Picker.Option Option(Bootstrap app, string label, IGameAction action, string detail = null)
        {
            string reason = BlockReason(app, action);
            return new Picker.Option(label, () => Queue(app, action), detail, reason == null, reason);
        }

        /// <summary>남은 행동이 없을 때 선택 창을 여는 버튼을 막는다.</summary>
        public static Button Opener(Transform parent, Bootstrap app, string label, Action open, string extraReason = null)
        {
            string reason = extraReason ?? (app.Session.RemainingActions <= 0 ? "이번 달에는 더 할 수 없습니다" : null);
            return UiFactory.Button(parent, label, open, reason == null, reason);
        }

        public static void Queue(Bootstrap app, IGameAction action)
        {
            var r = app.Session.Queue(action);
            app.Rebuild();
            app.Toast(r.Ok ? "예약했습니다: " + action.Describe(app.Session.World) : r.Reason);
        }

        /// <summary>버튼 여러 개를 줄바꿈하며 놓는 칸.</summary>
        public static RectTransform Grid(Transform parent, float cellWidth = 196, int rows = 1)
        {
            var rt = UiFactory.Rect(parent, "Actions");
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellWidth, 44);
            g.spacing = new Vector2(10, 10);
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }
    }

    /// <summary>선택지 목록 창.</summary>
    public static class Picker
    {
        public struct Option
        {
            public string Label;
            public string Detail;
            public bool Enabled;
            public string Reason;
            public Action OnPick;

            public Option(string label, Action onPick, string detail = null, bool enabled = true, string reason = null)
            {
                Label = label;
                OnPick = onPick;
                Detail = detail;
                Enabled = enabled;
                Reason = reason;
            }
        }

        public static void Show(Bootstrap app, string title, IList<Option> options, string empty = "고를 수 있는 것이 없습니다")
        {
            var canvas = app.Root.parent;
            var shade = UiFactory.Panel(canvas, new Color(0, 0, 0, 0.55f), "PickerShade");
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            void Close() => UiFactory.Remove(shade.gameObject);
            shade.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            var box = UiFactory.Panel(shade.transform, Theme.Panel, "Picker");
            box.raycastTarget = true;
            box.rectTransform.Area(0.32f, 0.12f, 0.68f, 0.88f);
            var inner = UiFactory.Rect(box.transform, "Inner").Fill(28, 24, 28, 24);
            UiFactory.VStack(inner, 12);
            UiFactory.Label(inner, title, Theme.HeadingSize, Theme.Ink, true).Size(-1, 46);
            var list = UiFactory.ScrollList(inner, 8);
            ((RectTransform)list.parent.parent).Size(-1, -1).flexibleHeight = 1;
            if (options.Count == 0) UiFactory.Label(list, empty, Theme.BodySize, Theme.InkDim).Size(-1, 40);
            foreach (var o in options)
            {
                var opt = o;
                var b = UiFactory.Button(list, o.Detail == null ? o.Label : $"{o.Label}   <color=#A3A898>{o.Detail}</color>",
                    () => { Close(); opt.OnPick(); }, o.Enabled, o.Reason, Theme.BodySize, -1, 50);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.supportRichText = true;
            }
            UiFactory.Button(inner, "닫기", Close, width: 160);
        }
    }

    /// <summary>글자 하나를 입력받는 창.</summary>
    public static class TextPrompt
    {
        public static void Show(Bootstrap app, string title, string placeholder, string okLabel, Action<string> onOk)
        {
            var shade = UiFactory.Panel(app.Root.parent, new Color(0, 0, 0, 0.55f), "PromptShade");
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            void Close() => UiFactory.Remove(shade.gameObject);

            var box = UiFactory.Panel(shade.transform, Theme.Panel, "Prompt");
            box.raycastTarget = true;
            box.rectTransform.Area(0.34f, 0.38f, 0.66f, 0.62f);
            var inner = UiFactory.Rect(box.transform, "Inner").Fill(28, 24, 28, 24);
            UiFactory.VStack(inner, 14);
            UiFactory.Label(inner, title, Theme.HeadingSize, Theme.Ink, true).Size(-1, 46);
            string value = "";
            var input = UiFactory.Input(inner, "", placeholder, v => value = v);
            input.characterLimit = 12;
            var row = UiFactory.Row(inner, 12, 44);
            UiFactory.Rect(row, "Spacer").Size(-1, -1, 1);
            UiFactory.Button(row, "취소", Close, width: 120);
            UiFactory.Button(row, okLabel, () => { Close(); onOk(value.Trim()); }, width: 160);
            input.Select();
        }
    }
}
