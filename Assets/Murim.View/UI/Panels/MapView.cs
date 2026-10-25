using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Rules;
using Murim.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>13개 지역 노드와 길. 노드 색 = 그 지역 영향력 1위 세력의 성향.</summary>
    public static class MapView
    {
        const float NodeSize = 52;

        public static void Build(RectTransform area, GameSession s, string selectedRegion, Action<string> onSelect)
        {
            var d = s.Data;
            var w = s.World;
            var map = UiFactory.Rect(area, "Map").Fill(60, 40, 60, 70);

            // 직접 만든 Graphic에는 CanvasRenderer가 자동으로 붙지 않아 길이 그려지지 않았다.
            var roadsGo = UiFactory.Rect(map, "Roads").Fill().gameObject;
            roadsGo.AddComponent<CanvasRenderer>();
            var roads = roadsGo.AddComponent<RoadGraphic>();
            roads.color = Theme.Line;
            roads.raycastTarget = false;
            var seen = new HashSet<string>();
            foreach (var r in d.Regions.Values)
                foreach (var n in r.Neighbors)
                    if (seen.Add(string.CompareOrdinal(r.Id, n) < 0 ? r.Id + n : n + r.Id))
                        roads.Segments.Add((Pos(r.X, r.Y), Pos(d.Regions[n].X, d.Regions[n].Y)));
            roads.SetVerticesDirty();

            string playerRegion = s.Player?.LocationRegionId;
            foreach (var r in d.Regions.Values)
            {
                var leading = w.GetFaction(Influence.Leading(w, r.Id));
                var color = leading == null ? Theme.Unclaimed : Theme.Of(leading.Alignment);

                var node = UiFactory.Rect(map, "Node:" + r.Id);
                node.anchorMin = node.anchorMax = Pos(r.X, r.Y);
                node.sizeDelta = new Vector2(220, NodeSize + 64);
                node.pivot = new Vector2(0.5f, 1 - NodeSize / 2 / (NodeSize + 64));

                if (r.Id == playerRegion) Disc(node, NodeSize + 20, Theme.Gold);
                if (r.Id == selectedRegion) Disc(node, NodeSize + 10, Theme.Ink);
                var dot = Disc(node, NodeSize, color);
                dot.raycastTarget = true;
                var id = r.Id;
                dot.gameObject.AddComponent<Button>().onClick.AddListener(() => onSelect(id));

                var count = w.Factions.Values.Count(f => !f.IsDissolved && f.HomeRegionId == r.Id);
                var name = UiFactory.Label(node, r.Name, Theme.SubheadingSize, Theme.Ink, true, TextAnchor.UpperCenter);
                name.rectTransform.anchorMin = new Vector2(0, 0);
                name.rectTransform.anchorMax = new Vector2(1, 0);
                name.rectTransform.pivot = new Vector2(0.5f, 0);
                name.rectTransform.anchoredPosition = new Vector2(0, 22);
                name.rectTransform.sizeDelta = new Vector2(0, 34);
                var sub = UiFactory.Label(node, $"세력 {count}", Theme.SmallSize, Theme.InkDim, false, TextAnchor.UpperCenter);
                sub.rectTransform.anchorMin = new Vector2(0, 0);
                sub.rectTransform.anchorMax = new Vector2(1, 0);
                sub.rectTransform.pivot = new Vector2(0.5f, 0);
                sub.rectTransform.anchoredPosition = new Vector2(0, 0);
                sub.rectTransform.sizeDelta = new Vector2(0, 24);
                // 길이 이름표를 지나가도 읽히도록 바탕색 테두리.
                foreach (var t in new[] { name, sub })
                {
                    var o = t.gameObject.AddComponent<Outline>();
                    o.effectColor = Theme.Ground;
                    o.effectDistance = new Vector2(2, -2);
                }
            }

            var legend = UiFactory.Row(area, 20, 30);
            legend.anchorMin = new Vector2(0, 0);
            legend.anchorMax = new Vector2(1, 0);
            legend.pivot = new Vector2(0, 0);
            legend.offsetMin = new Vector2(24, 16);
            legend.offsetMax = new Vector2(-24, 46);
            foreach (var (label, c) in new[] { ("정파 우세", Theme.Righteous), ("사파 우세", Theme.Unorthodox), ("마도 우세", Theme.Demonic), ("현재 위치", Theme.Gold) })
            {
                var chip = UiFactory.Panel(legend, c, "Chip");
                chip.sprite = UiFactory.Circle;
                chip.Size(16, 16);
                UiFactory.Label(legend, label, Theme.SmallSize, Theme.InkDim).Size(100);
            }
        }

        /// <summary>지도 좌표(y는 아래로 증가)를 앵커로.</summary>
        static Vector2 Pos(float x, float y) => new Vector2(x, 1 - y);

        static Image Disc(RectTransform parent, float size, Color color)
        {
            var img = UiFactory.Panel(parent, color, "Disc");
            img.sprite = UiFactory.Circle;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, (64 + NodeSize / 2) / (NodeSize + 64));
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            return img;
        }
    }
}
