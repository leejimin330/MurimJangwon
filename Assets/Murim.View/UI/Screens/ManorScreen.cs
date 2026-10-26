using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Manor;
using Murim.Core.Rules;
using Murim.Core.Session;
using Murim.Core.World;
using Murim.View.UI.Panels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murim.View.UI.Screens
{
    /// <summary>격자 위 칸 하나. 마우스를 올리면 미리보기, 누르면 놓기·허물기.</summary>
    public class ManorCell : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public int X, Y;
        public Action<int, int> OnHover, OnClick;
        public void OnPointerEnter(PointerEventData e) => OnHover?.Invoke(X, Y);
        public void OnPointerClick(PointerEventData e) => OnClick?.Invoke(X, Y);
    }

    /// <summary>장원 화면: 격자 건설과 제자 하루 일과(실시간 + 일시정지). 설계 4단계 7절.</summary>
    public class ManorScreen : UiScreen
    {
        public const float CellSize = 40;
        static readonly Color Free = new Color(0.30f, 0.75f, 0.40f, 0.45f);
        static readonly Color Blocked = new Color(0.85f, 0.25f, 0.25f, 0.45f);

        readonly Bootstrap app;
        GameSession S => app.Session;

        /// <summary>장원 시계 (0~24시). 화면을 다시 그려도 이어진다.</summary>
        public float Hour { get; private set; } = 6f;
        public int Speed { get; private set; } = 1;
        public string Placing { get; private set; }
        public bool Demolishing { get; private set; }

        RectTransform grid;
        Image ghost;
        Text clock;
        RectTransform activityList;
        readonly Dictionary<string, RectTransform> dots = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
        int lastListedHour = -1;

        public ManorScreen(Bootstrap app) => this.app = app;

        Faction Own => S.OwnManorFaction;

        public override void Build(RectTransform root)
        {
            dots.Clear();
            lastListedHour = -1;
            var f = Own;
            if (f == null)
            {
                app.Show(new GameScreen(app));
                return;
            }
            BuildTop(root, f);

            var left = UiFactory.Panel(root, Theme.Panel, "GridArea").rectTransform;
            left.anchorMin = new Vector2(0, 0);
            left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 1);
            left.anchoredPosition = new Vector2(16, -96);
            left.sizeDelta = new Vector2(f.Manor.Width * CellSize + 32, -112);
            BuildGrid(left, f);

            var right = UiFactory.Panel(root, Theme.Panel, "Palette").rectTransform;
            right.anchorMin = new Vector2(0, 0);
            right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(f.Manor.Width * CellSize + 64, 16);
            right.offsetMax = new Vector2(-16, -96);
            var inner = UiFactory.Rect(right, "Inner").Fill(24, 18, 24, 18);
            UiFactory.VStack(inner, 8);
            BuildPalette(inner, f);
            UiFactory.Label(inner, "지금 일과", Theme.SubheadingSize, Theme.Ink, true).Size(-1, 40);
            activityList = UiFactory.ScrollList(inner, 2);
            ((RectTransform)activityList.parent.parent).Size(-1, -1).flexibleHeight = 1;
            UpdateResidents(0, true);
        }

        void BuildTop(RectTransform root, Faction f)
        {
            var bar = UiFactory.Panel(root, Theme.Panel, "TopBar").rectTransform;
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, 80);
            UiFactory.HStack(bar, 16).padding = new RectOffset(28, 20, 10, 10);
            var w = S.World;
            var bonus = ManorOps.Bonuses(w, app.Data, f);
            UiFactory.Label(bar, f.Name + " 장원", Theme.HeadingSize, Theme.Ink, true).Size(300);
            UiFactory.Label(bar, $"은자 {f.Silver:N0}냥", Theme.BodySize, Theme.Gold).Size(170);
            UiFactory.Label(bar, $"방어 {bonus.Defense}", Theme.BodySize, Theme.Ink).Size(110);
            UiFactory.Label(bar, $"정원 {FactionOps.LivingMembers(w, f).Count()}/{bonus.Capacity}", Theme.BodySize, Theme.Ink).Size(150);
            UiFactory.Rect(bar, "Spacer").Size(-1, -1, 1);
            clock = UiFactory.Label(bar, "", Theme.SubheadingSize, Theme.Ink, true, TextAnchor.MiddleRight);
            clock.name = "Clock";
            clock.Size(140);
            UpdateClock();
            foreach (var (label, speed) in new[] { ("일시정지", 0), ("1배", 1), ("2배", 2), ("3배", 3) })
            {
                int sp = speed;
                UiFactory.Choice(bar, label, Speed == sp, () => { Speed = sp; app.Rebuild(); }, width: speed == 0 ? 110 : 64, height: 44);
            }
            UiFactory.Button(bar, "지도로", () => app.Show(new GameScreen(app)), width: 120);
        }

        void BuildGrid(RectTransform area, Faction f)
        {
            var m = f.Manor;
            var w = S.World;
            var d = app.Data;
            grid = UiFactory.Rect(area, "ManorGrid");
            grid.anchorMin = grid.anchorMax = new Vector2(0, 1);
            grid.pivot = new Vector2(0, 1);
            grid.anchoredPosition = new Vector2(16, -16);
            grid.sizeDelta = new Vector2(m.Width * CellSize, m.Height * CellSize);

            for (int y = 0; y < m.Height; y++)
                for (int x = 0; x < m.Width; x++)
                {
                    bool gate = ManorOps.IsGate(x, y);
                    var cell = UiFactory.Panel(grid, gate ? Theme.Hex("#5A4A32") : ((x + y) % 2 == 0 ? Theme.Hex("#2E3D35") : Theme.Hex("#2B3832")), "Cell");
                    Place(cell.rectTransform, x, y, 1, 1);
                    cell.raycastTarget = true;
                    var handler = cell.gameObject.AddComponent<ManorCell>();
                    handler.X = x;
                    handler.Y = y;
                    handler.OnHover = (hx, hy) => HoverCell(hx, hy);
                    handler.OnClick = ClickCell;
                }
            var gateLabel = UiFactory.Label(grid, "정문", Theme.SmallSize, Theme.InkDim, align: TextAnchor.MiddleCenter);
            Place(gateLabel.rectTransform, ManorOps.GateX[0], ManorOps.GateY, 2, 1);

            DrawBuildings(grid, d, w, m);

            ghost = UiFactory.Panel(grid, Free, "Ghost");
            ghost.raycastTarget = false;
            ghost.gameObject.SetActive(false);
        }

        /// <summary>건물 사각형과 이름 (미완공은 반투명 + 남은 달). 전투 화면도 쓴다.</summary>
        public static void DrawBuildings(RectTransform grid, Murim.Core.Data.GameData d, WorldState w, ManorState m)
        {
            foreach (var b in m.Buildings)
            {
                var def = ManorOps.Def(d, b.DefId);
                if (def == null) continue;
                bool done = ManorOps.IsComplete(w, b);
                var color = Theme.Hex(def.Color ?? "#777777");
                if (!done) color.a = 0.45f;
                var img = UiFactory.Panel(grid, color, "Building:" + b.Id);
                img.raycastTarget = false;
                Place(img.rectTransform, b.X, b.Y, def.Width, def.Height, 2);
                string label = done ? def.Name : $"{def.Name}\n{b.CompleteMonth - w.Month}개월";
                var t = UiFactory.Label(img.transform, def.Width == 1 && def.Height == 1 ? def.Name.Substring(0, 1) : label,
                    def.Width == 1 ? Theme.SmallSize - 2 : Theme.SmallSize, done ? Theme.Ink : Theme.InkDim, align: TextAnchor.MiddleCenter);
                t.rectTransform.Fill();
            }
        }

        public static void Place(RectTransform rt, int x, int y, int wCells, int hCells, float inset = 0)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x * CellSize + inset, -y * CellSize - inset);
            rt.sizeDelta = new Vector2(wCells * CellSize - inset * 2, hCells * CellSize - inset * 2);
        }

        void BuildPalette(RectTransform inner, Faction f)
        {
            UiFactory.Label(inner, "건물", Theme.SubheadingSize, Theme.Ink, true).Size(-1, 40);
            var list = UiFactory.Rect(inner, "BuildList");
            UiFactory.VStack(list, 6);
            foreach (var def in app.Data.Buildings.Values.Where(b => !b.Fixed).OrderBy(b => b.Cost))
            {
                var id = def.Id;
                int count = f.Manor.Buildings.Count(b => b.DefId == id);
                string reason = count >= def.Max ? $"{def.Max}개까지 지을 수 있습니다" : f.Silver < def.Cost ? "은자가 부족합니다" : null;
                var btn = UiFactory.Choice(list, $"{def.Name}  {def.Width}×{def.Height}  {def.Cost}냥·{def.BuildMonths}개월   <color=#A3A898>{def.Bonuses.Describe()}</color>",
                    Placing == id, () => { Placing = Placing == id ? null : id; Demolishing = false; app.Rebuild(); }, reason == null, reason, height: 40);
                btn.name = def.Name;
                var t = btn.GetComponentInChildren<Text>();
                t.supportRichText = true;
                t.alignment = TextAnchor.MiddleLeft;
                t.fontSize = Theme.SmallSize;
                Tooltip.Attach(btn.gameObject, def.Description + (reason != null ? "\n" + reason : ""));
            }
            var row = UiFactory.Row(inner, 10, 44);
            UiFactory.Choice(row, "허물기", Demolishing, () => { Demolishing = !Demolishing; Placing = null; app.Rebuild(); }, width: 140).name = "허물기";
            UiFactory.Label(row, Placing != null ? "격자를 눌러 놓으세요" : Demolishing ? "허물 건물을 누르세요" : "건물을 고르세요",
                Theme.SmallSize, Theme.InkDim).Size(-1, 44, 1);
        }

        /// <summary>미리보기: 놓을 수 있으면 초록, 아니면 빨강. 테스트도 부른다.</summary>
        public bool HoverCell(int x, int y)
        {
            if (Placing == null || ghost == null) return false;
            var def = ManorOps.Def(app.Data, Placing);
            var check = ManorOps.CanPlace(S.World, app.Data, Own, Placing, x, y);
            ghost.gameObject.SetActive(true);
            ghost.color = check.Ok ? Free : Blocked;
            Place(ghost.rectTransform, x, y, def.Width, def.Height);
            ghost.transform.SetAsLastSibling();
            return check.Ok;
        }

        public void ClickCell(int x, int y)
        {
            if (Placing != null)
            {
                var name = ManorOps.Def(app.Data, Placing)?.Name;
                var r = S.PlaceBuilding(Placing, x, y);
                if (r.Ok) Placing = null;
                app.Rebuild();
                app.Toast(r.Ok ? $"{name} 공사를 시작했습니다" : r.Reason);
                return;
            }
            if (Demolishing)
            {
                var b = ManorOps.At(Own.Manor, app.Data, x, y);
                if (b == null) return;
                var def = ManorOps.Def(app.Data, b.DefId);
                var r = S.Demolish(b.Id, out int refund);
                app.Rebuild();
                app.Toast(r.Ok ? $"{Murim.Core.Text.Josa.EulReul(def.Name)} 허물고 {refund}냥을 돌려받았습니다" : r.Reason);
            }
        }

        public override void Tick(float dt)
        {
            if (Speed <= 0 || grid == null) return;
            Hour = (Hour + dt * Speed / ManorLife.SecondsPerHour) % 24f;
            UpdateClock();
            UpdateResidents(dt * Speed, false);
        }

        /// <summary>테스트용: 화면 시간 seconds초를 0.1초씩 흘린다.</summary>
        public void Advance(float seconds)
        {
            for (float t = 0; t < seconds; t += 0.1f) Tick(0.1f);
        }

        void UpdateClock()
        {
            if (clock != null) clock.text = $"{(int)Hour:00}시" + (Speed == 0 ? " (멈춤)" : "");
        }

        /// <summary>사람 점을 일과 자리로 걷게 한다. snap이면 바로 자리에 놓는다.</summary>
        void UpdateResidents(float dt, bool snap)
        {
            var f = Own;
            if (f == null || grid == null) return;
            var w = S.World;
            var d = app.Data;
            int hour = (int)Hour;
            var residents = ManorLife.Residents(w, f).ToList();
            foreach (var c in residents)
            {
                var spot = ManorLife.Spot(w, d, f.Manor, c, hour);
                var target = new Vector2(spot.X * CellSize, -spot.Y * CellSize);
                if (!dots.TryGetValue(c.Id, out var dot) || dot == null)
                {
                    dot = MakeDot(c);
                    dots[c.Id] = dot;
                    if (!positions.ContainsKey(c.Id)) positions[c.Id] = target;
                }
                var pos = snap ? target : Vector2.MoveTowards(positions[c.Id], target, dt * CellSize * 3f);
                positions[c.Id] = pos;
                dot.anchoredPosition = pos;
            }
            if (hour != lastListedHour) ListActivities(residents, hour);
        }

        RectTransform MakeDot(Character c)
        {
            var img = UiFactory.Panel(grid, c.Id == S.Player?.Id ? Theme.Gold : c.Role == Murim.Core.Data.Role.Elder ? Theme.Righteous : Theme.Ink,
                "Resident:" + c.Id);
            img.sprite = UiFactory.Circle;
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(14, 14);
            var id = c.Id;
            img.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                var gs = new GameScreen(app);
                app.Show(gs);
                gs.ShowCharacter(id);
            });
            Tooltip.Attach(img.gameObject, c.FullName);
            return rt;
        }

        void ListActivities(List<Character> residents, int hour)
        {
            lastListedHour = hour;
            if (activityList == null) return;
            UiFactory.Clear(activityList);
            var w = S.World;
            foreach (var c in residents.Take(40))
            {
                var spot = ManorLife.Spot(w, app.Data, Own.Manor, c, hour);
                var where = spot.Building == null ? "마당" : ManorOps.Def(app.Data, spot.Building.DefId)?.Name;
                UiFactory.Label(activityList, $"{c.FullName} — {where}에서 {spot.Activity}", Theme.SmallSize, Theme.InkDim).name = "Activity";
            }
        }

        /// <summary>테스트용: 지금 점이 있는 칸 좌표.</summary>
        public Vector2 DotCell(string characterId) =>
            positions.TryGetValue(characterId, out var p) ? new Vector2(p.x / CellSize, -p.y / CellSize) : new Vector2(-1, -1);
    }
}
