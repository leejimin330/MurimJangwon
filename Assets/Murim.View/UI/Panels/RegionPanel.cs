using System;
using System.Linq;
using Murim.Core.Rules;
using Murim.Core.Session;
using UnityEngine;

namespace Murim.View.UI.Panels
{
    /// <summary>지역의 치안과 세력별 영향력.</summary>
    public static class RegionPanel
    {
        public static void Build(RectTransform panel, GameSession s, string regionId, Action<string> onFaction)
        {
            var w = s.World;
            var def = s.Data.Regions[regionId];
            var state = w.Regions[regionId];
            UiFactory.Label(panel, def.Name, Theme.HeadingSize + 4, Theme.Ink, true).Size(-1, 50);

            var stab = UiFactory.Row(panel, 12, 28);
            UiFactory.Label(stab, $"치안 {state.Stability}", Theme.BodySize, Theme.InkDim).Size(110);
            UiFactory.Bar(stab, state.Stability / 100f, Theme.InkDim).Size(-1, 8, 1);

            var neighbors = string.Join(", ", def.Neighbors.Select(n => s.Data.Regions[n].Name));
            UiFactory.Label(panel, "이웃 지역: " + neighbors, Theme.SmallSize, Theme.InkFaint).Size(-1, 28);

            int free = Influence.Free(w, regionId);
            UiFactory.Label(panel, $"이 지역의 세력 (주인 없는 몫 {free})", Theme.SubheadingSize, Theme.Ink, true).Size(-1, 44);

            var list = UiFactory.ScrollList(panel, 6);
            ((RectTransform)list.parent.parent).Size(-1, -1).flexibleHeight = 1;
            var factions = w.Factions.Values.Where(f => !f.IsDissolved
                    && (f.HomeRegionId == regionId || Influence.Get(w, regionId, f.Id) > 0))
                .OrderByDescending(f => Influence.Get(w, regionId, f.Id)).ThenBy(f => f.Name);
            foreach (var f in factions)
            {
                int inf = Influence.Get(w, regionId, f.Id);
                var row = UiFactory.Panel(list, Theme.Panel, "Faction");
                row.raycastTarget = true;
                var id = f.Id;
                row.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => onFaction(id));
                row.Size(-1, 64);
                var inner = UiFactory.Rect(row.transform, "Inner").Fill(14, 8, 14, 8);
                UiFactory.HStack(inner, 12);
                var chip = UiFactory.Panel(inner, Theme.Of(f.Alignment), "Chip");
                chip.Size(6, 44);
                var col = UiFactory.Rect(inner, "Col");
                col.Size(-1, 48, 1);
                UiFactory.VStack(col, 4);
                bool mine = s.Player?.FactionId == f.Id;
                UiFactory.Label(col, f.Name + (mine ? " (우리)" : ""), Theme.BodySize, mine ? Theme.Gold : Theme.Ink).Size(-1, 26);
                UiFactory.Bar(col, inf / 100f, Theme.Of(f.Alignment), 6);
                UiFactory.Label(inner, inf.ToString(), Theme.BodySize, Theme.InkDim, align: TextAnchor.MiddleRight).Size(44);
            }
        }
    }
}
