using System;
using System.Collections.Generic;
using Murim.Core.Session;
using Murim.Core.Text;
using Murim.Core.World;
using UnityEngine;

namespace Murim.View.UI.Panels
{
    public enum ChronicleFilter { All, Own, Events }

    /// <summary>최신순 연대기. "우리 세력만"이면 플레이어나 플레이어 세력 사람이 얽힌 줄만.</summary>
    public static class ChroniclePanel
    {
        /// <summary>한 번에 그리는 최대 줄 수. 오래 플레이해도 화면이 느려지지 않게.</summary>
        const int MaxRows = 300;

        /// <summary>펼친 연대기 줄 (World.Chronicle 인덱스). 화면을 다시 그려도 유지한다.</summary>
        public static readonly HashSet<int> Expanded = new HashSet<int>();

        public static void Build(RectTransform area, GameSession s, ChronicleFilter filter, Action<ChronicleFilter> onChange)
        {
            bool ownOnly = filter == ChronicleFilter.Own;
            var w = s.World;
            var inner = UiFactory.Rect(area, "Inner").Fill(20, 12, 20, 12);
            UiFactory.VStack(inner, 8);

            var head = UiFactory.Row(inner, 12, 40);
            UiFactory.Label(head, "연대기", Theme.SubheadingSize, Theme.Ink, true).Size(-1, 40, 1);
            UiFactory.Choice(head, "전체", filter == ChronicleFilter.All, () => onChange(ChronicleFilter.All), width: 90, height: 36);
            UiFactory.Choice(head, "우리 세력만", ownOnly, () => onChange(ChronicleFilter.Own), width: 150, height: 36);
            UiFactory.Choice(head, "사건만", filter == ChronicleFilter.Events, () => onChange(ChronicleFilter.Events), width: 110, height: 36);

            var list = UiFactory.ScrollList(inner, 4);
            ((RectTransform)list.parent.parent).Size(-1, -1).flexibleHeight = 1;

            var ours = ownOnly ? OurIds(w) : null;
            int shown = 0;
            for (int i = w.Chronicle.Count - 1; i >= 0 && shown < MaxRows; i--)
            {
                var e = w.Chronicle[i];
                if (ours != null && !Involves(e, ours)) continue;
                bool isEvent = e.Kind != null && e.Kind.StartsWith("Event:");
                if (filter == ChronicleFilter.Events && !isEvent) continue;
                var color = e.Importance >= 2 ? Theme.Ink : e.Importance == 1 ? Theme.InkDim : Theme.InkFaint;
                var style = e.Importance >= 2 ? FontStyle.Bold : FontStyle.Normal;
                var row = UiFactory.Row(list, 8);
                row.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                var dot = UiFactory.Panel(row, isEvent ? Theme.OfCategory(Category(s, e.Kind)) : new Color(0, 0, 0, 0), "Dot");
                dot.sprite = UiFactory.Circle;
                dot.Size(10, 10);
                bool hasDetail = e.Detail != null && e.Detail.Length > 0;
                bool open = hasDetail && Expanded.Contains(i);
                string mark = hasDetail ? (open ? "<color=#C9A24A>[접기]</color> " : "<color=#C9A24A>[결투 경과]</color> ") : "";
                var label = UiFactory.Label(row, $"{mark}{GameDate.Format(e.Month)}, {e.Text}", Theme.SmallSize + 1, color, style: style);
                label.supportRichText = true;
                label.Size(-1, -1, 1);
                if (hasDetail)
                {
                    int index = i;
                    row.name = "DetailRow";
                    var hit = row.gameObject.AddComponent<UnityEngine.UI.Image>();
                    hit.color = new Color(0, 0, 0, 0);
                    row.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
                    {
                        if (!Expanded.Remove(index)) Expanded.Add(index);
                        onChange(filter);
                    });
                    if (open)
                    {
                        var log = DuelLogView.Build(list, s.Data, e.Detail);
                        log.name = "ChronicleDuelLog";
                    }
                }
                shown++;
            }
            if (shown == 0)
                UiFactory.Label(list, ownOnly ? "아직 우리와 얽힌 일이 없습니다." : "아직 기록된 일이 없습니다.", Theme.BodySize, Theme.InkFaint);
        }

        static string Category(GameSession s, string kind) =>
            s.Data.Events.TryGetValue(kind.Substring("Event:".Length), out var e) ? e.Category : null;

        static HashSet<string> OurIds(WorldState w)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var p = w.Player;
            if (p == null) return ids;
            ids.Add(p.Id);
            var f = w.GetFaction(p.FactionId);
            if (f != null)
            {
                ids.Add(f.Id);
                foreach (var m in f.MemberIds) ids.Add(m);
            }
            return ids;
        }

        static bool Involves(ChronicleEntry e, HashSet<string> ids)
        {
            if (e.InvolvedIds == null) return false;
            foreach (var id in e.InvolvedIds)
                if (id != null && ids.Contains(id)) return true;
            return false;
        }
    }
}
