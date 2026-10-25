using System.Collections.Generic;
using Murim.Core.Data;
using Murim.Core.Text;
using Murim.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>3대 가계도: 부모 / 본인·배우자 / 자식. 칸을 누르면 그 사람을 중심으로 다시 그린다.</summary>
    public static class FamilyTreePanel
    {
        const float CellWidth = 200, CellHeight = 84;

        public static void Build(RectTransform panel, Bootstrap app, INav nav, string characterId)
        {
            var w = app.Session.World;
            var c = w.Get(characterId);
            if (c == null) return;

            var head = UiFactory.Row(panel, 12, 54);
            UiFactory.Label(head, c.FullName + "의 가계도", Theme.HeadingSize + 4, Theme.Ink, true).Size(-1, 54, 1);
            UiFactory.Button(head, "인물 보기", () => nav.ShowCharacter(c.Id), width: 140);
            UiFactory.Label(panel, "칸을 누르면 그 사람을 중심으로 봅니다. 회색은 세상을 떠난 사람입니다.", Theme.SmallSize, Theme.InkFaint)
                .Size(-1, 28);

            var list = UiFactory.ScrollList(panel, 10);
            ((RectTransform)list.parent.parent).Size(-1, -1).flexibleHeight = 1;

            Generation(list, "부모", new[] { w.Get(c.FatherId), w.Get(c.MotherId) }, w, nav, null);
            Generation(list, "본인과 배우자", new[] { c, w.Get(c.SpouseId) }, w, nav, c.Id);
            var children = new List<Character>();
            foreach (var id in c.ChildIds) children.Add(w.Get(id));
            Generation(list, $"자식 ({children.Count})", children, w, nav, null);
        }

        static void Generation(RectTransform list, string title, IList<Character> people, WorldState w, INav nav, string centerId)
        {
            FactionPanel.Heading(list, title);
            var grid = UiFactory.Rect(list, "Generation");
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(CellWidth, CellHeight);
            g.spacing = new Vector2(10, 10);
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            int shown = 0;
            foreach (var p in people)
            {
                if (p == null) continue;
                Cell(grid, p, w, nav, p.Id == centerId);
                shown++;
            }
            if (shown == 0)
            {
                var empty = UiFactory.Panel(grid, Theme.Ground, "Empty");
                UiFactory.Label(empty.transform, "알 수 없음", Theme.BodySize, Theme.InkFaint, align: TextAnchor.MiddleCenter)
                    .rectTransform.Fill();
            }
        }

        static void Cell(RectTransform grid, Character p, WorldState w, INav nav, bool center)
        {
            var bg = UiFactory.Panel(grid, center ? Theme.Gold : Theme.PanelRaised, "Person");
            bg.raycastTarget = true;
            var id = p.Id;
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (center) nav.ShowCharacter(id);
                else nav.ShowFamily(id);
            });
            var inner = UiFactory.Rect(bg.transform, "Inner").Fill(12, 8, 12, 8);
            UiFactory.VStack(inner, 2);
            Color ink = center ? Theme.Ground : p.IsAlive ? Theme.Ink : Theme.InkFaint;
            Color dim = center ? Theme.Ground : p.IsAlive ? Theme.InkDim : Theme.InkFaint;
            UiFactory.Label(inner, p.FullName + (p.Sex == Sex.Male ? " ♂" : " ♀"), Theme.BodySize, ink).Size(-1, 30);
            string years = p.IsAlive
                ? $"{GameDate.Year(p.BirthMonth)}년생, {p.AgeYears(w.Month)}세"
                : $"{GameDate.Year(p.BirthMonth)} ~ {GameDate.Year(p.DeathMonth.Value)}년";
            UiFactory.Label(inner, years, Theme.SmallSize, dim).Size(-1, 24);
            var f = w.GetFaction(p.FactionId);
            if (f != null) UiFactory.Label(inner, f.Name, Theme.SmallSize, dim).Size(-1, 22);
        }
    }
}
