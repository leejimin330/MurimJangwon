using System.Collections.Generic;
using System.Linq;
using Murim.Core.Battle;
using Murim.Core.Martial;
using Murim.Core.Text;
using Murim.View.UI.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Screens
{
    /// <summary>습격전 화면: 장원 격자 위 실시간 + 일시정지 전투 (5단계 설계 8절).</summary>
    public class BattleScreen : UiScreen
    {
        const float Cell = ManorScreen.CellSize;
        static readonly Color Ally = Theme.Hex("#6FA0D8");
        static readonly Color Enemy = Theme.Hex("#D2645A");

        readonly Bootstrap app;
        readonly BattleState battle;
        readonly HashSet<int> selected = new HashSet<int>();
        public int Speed { get; private set; }
        float accumulated;
        RaidOutcome outcome;
        bool finished;

        RectTransform grid;
        Text clock;
        RectTransform info;
        RectTransform logList;
        int loggedLines = -1;
        readonly Dictionary<int, (RectTransform root, Image body, Image ring, RectTransform hp)> views =
            new Dictionary<int, (RectTransform, Image, Image, RectTransform)>();

        public BattleState Battle => battle;
        public IReadOnlyCollection<int> Selected => selected;

        public BattleScreen(Bootstrap app, BattleState battle)
        {
            this.app = app;
            this.battle = battle;
            Speed = 0; // 처음에는 멈춘 채로 자리를 잡는다.
        }

        public override void Build(RectTransform root)
        {
            views.Clear();
            loggedLines = -1;
            if (battle == null)
            {
                app.Show(new GameScreen(app));
                return;
            }
            var s = app.Session;
            BuildTop(root);

            var left = UiFactory.Panel(root, Theme.Panel, "GridArea").rectTransform;
            left.anchorMin = new Vector2(0, 0);
            left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 1);
            left.anchoredPosition = new Vector2(16, -96);
            left.sizeDelta = new Vector2(battle.Map.Width * Cell + 32, -112);
            grid = UiFactory.Rect(left, "BattleGrid");
            grid.anchorMin = grid.anchorMax = new Vector2(0, 1);
            grid.pivot = new Vector2(0, 1);
            grid.anchoredPosition = new Vector2(16, -16);
            grid.sizeDelta = new Vector2(battle.Map.Width * Cell, battle.Map.Height * Cell);
            for (int y = 0; y < battle.Map.Height; y++)
                for (int x = 0; x < battle.Map.Width; x++)
                {
                    var cell = UiFactory.Panel(grid, (x + y) % 2 == 0 ? Theme.Hex("#2E3D35") : Theme.Hex("#2B3832"), "Cell");
                    ManorScreen.Place(cell.rectTransform, x, y, 1, 1);
                    cell.raycastTarget = true;
                    var h = cell.gameObject.AddComponent<ManorCell>();
                    h.X = x;
                    h.Y = y;
                    h.OnClick = ClickCell;
                }
            var manor = s.OwnManorFaction?.Manor;
            if (manor != null) ManorScreen.DrawBuildings(grid, app.Data, s.World, manor);
            foreach (var u in battle.Units) views[u.Id] = MakeUnit(u);

            var right = UiFactory.Panel(root, Theme.Panel, "BattleSide").rectTransform;
            right.anchorMin = new Vector2(0, 0);
            right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(battle.Map.Width * Cell + 64, 16);
            right.offsetMax = new Vector2(-16, -96);
            var inner = UiFactory.Rect(right, "Inner").Fill(24, 18, 24, 18);
            UiFactory.VStack(inner, 10);
            var row = UiFactory.Row(inner, 10, 44);
            UiFactory.Button(row, "모두 고르기", SelectAll, width: 170);
            UiFactory.Button(row, "고른 것 풀기", () => { selected.Clear(); RefreshView(); }, width: 170);
            info = UiFactory.Rect(inner, "UnitInfo");
            UiFactory.VStack(info, 4);
            info.Size(-1, 170);
            UiFactory.Label(inner, "전투 기록", Theme.SubheadingSize, Theme.Ink, true).Size(-1, 36);
            logList = UiFactory.ScrollList(inner, 2);
            logList.name = "BattleLog";
            ((RectTransform)logList.parent.parent).Size(-1, -1).flexibleHeight = 1;
            RefreshView();
            if (finished) ShowResult(root);
        }

        void BuildTop(RectTransform root)
        {
            var bar = UiFactory.Panel(root, Theme.Panel, "TopBar").rectTransform;
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, 80);
            UiFactory.HStack(bar, 16).padding = new RectOffset(28, 20, 10, 10);
            var raid = app.Session.PendingRaid;
            UiFactory.Label(bar, "습격전" + (raid != null ? " — " + raid.AttackerName : ""), Theme.HeadingSize, Theme.Ink, true).Size(520);
            UiFactory.Rect(bar, "Spacer").Size(-1, -1, 1);
            clock = UiFactory.Label(bar, "", Theme.SubheadingSize, Theme.Ink, true, TextAnchor.MiddleRight);
            clock.name = "Clock";
            clock.Size(230);
            foreach (var (label, sp) in new[] { ("일시정지", 0), ("1배", 1), ("2배", 2), ("3배", 3) })
            {
                int speed = sp;
                UiFactory.Choice(bar, label, Speed == speed, () => { Speed = speed; app.Rebuild(); }, width: sp == 0 ? 110 : 64, height: 44);
            }
            UiFactory.Button(bar, "자동으로 끝내기", () =>
            {
                BattleSim.RunToEnd(battle);
                Finish();
            }, !finished, "전투가 끝났습니다", width: 190);
        }

        (RectTransform, Image, Image, RectTransform) MakeUnit(BattleUnit u)
        {
            var root = UiFactory.Rect(grid, "Unit:" + u.Id);
            root.anchorMin = root.anchorMax = new Vector2(0, 1);
            root.pivot = new Vector2(0.5f, 0.5f);
            float size = u.Named ? 24 : 16;
            root.sizeDelta = new Vector2(size, size);
            var ring = UiFactory.Panel(root, Theme.Gold, "Ring");
            ring.sprite = UiFactory.Circle;
            ring.rectTransform.anchorMin = Vector2.zero;
            ring.rectTransform.anchorMax = Vector2.one;
            ring.rectTransform.offsetMin = new Vector2(-4, -4);
            ring.rectTransform.offsetMax = new Vector2(4, 4);
            ring.raycastTarget = false;
            var body = UiFactory.Panel(root, u.Side == BattleSide.Defender ? Ally : Enemy, "Body");
            body.sprite = UiFactory.Circle;
            body.rectTransform.Fill();
            body.raycastTarget = true;
            int id = u.Id;
            body.gameObject.AddComponent<Button>().onClick.AddListener(() => ClickUnit(id));
            Tooltip.Attach(body.gameObject, $"{u.Name} ({Titles.Realm(u.Realm)})");
            var hpBg = UiFactory.Panel(root, Theme.Ground, "HpBg");
            hpBg.raycastTarget = false;
            hpBg.rectTransform.anchorMin = new Vector2(0, 0);
            hpBg.rectTransform.anchorMax = new Vector2(1, 0);
            hpBg.rectTransform.pivot = new Vector2(0.5f, 1);
            hpBg.rectTransform.anchoredPosition = new Vector2(0, -4);
            hpBg.rectTransform.sizeDelta = new Vector2(8, 4);
            var hp = UiFactory.Panel(hpBg.transform, Theme.Hex("#8FCB7A"), "Hp");
            hp.raycastTarget = false;
            hp.rectTransform.anchorMin = Vector2.zero;
            hp.rectTransform.anchorMax = Vector2.one;
            hp.rectTransform.offsetMin = hp.rectTransform.offsetMax = Vector2.zero;
            return (root, body, ring, hp.rectTransform);
        }

        void SelectAll()
        {
            selected.Clear();
            foreach (var u in battle.Units.Where(u => u.Side == BattleSide.Defender && u.Active)) selected.Add(u.Id);
            RefreshView();
        }

        /// <summary>아군이면 고르기(Shift면 더하기), 적이면 고른 아군에게 공격 명령. 테스트도 부른다.</summary>
        public void ClickUnit(int id, bool add = false)
        {
            var u = battle.Unit(id);
            if (u == null || finished) return;
            if (u.Side == BattleSide.Defender)
            {
                if (!u.Active) return;
                bool shift = add || (Application.isPlaying && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));
                if (!shift) selected.Clear();
                if (!selected.Remove(id) || !shift) selected.Add(id);
            }
            else if (u.Active)
            {
                foreach (var sid in selected) BattleSim.Order(battle, sid, new BattleOrder { Attack = true, TargetId = id });
                if (selected.Count > 0) app.Toast($"{selected.Count}명이 {u.Name}에게 달려든다");
            }
            RefreshView();
        }

        /// <summary>고른 아군에게 이동 명령.</summary>
        public void ClickCell(int x, int y)
        {
            if (finished || selected.Count == 0) return;
            if (!battle.Map.Walkable(x, y))
            {
                app.Toast("갈 수 없는 곳입니다");
                return;
            }
            foreach (var sid in selected) BattleSim.Order(battle, sid, new BattleOrder { X = x, Y = y });
            RefreshView();
        }

        public override void Tick(float dt)
        {
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.Space))
            {
                Speed = Speed == 0 ? 1 : 0;
                app.Rebuild();
                return;
            }
            if (finished || Speed <= 0) return;
            accumulated += dt * Speed;
            while (accumulated >= BattleState.Dt && !battle.Over)
            {
                BattleSim.Step(battle);
                accumulated -= BattleState.Dt;
            }
            if (battle.Over) Finish();
            else RefreshView();
        }

        /// <summary>테스트용: 화면 시간 seconds초를 0.1초씩 흘린다 (배속 적용).</summary>
        public void Advance(float seconds)
        {
            for (float t = 0; t < seconds && !finished; t += 0.1f) Tick(0.1f);
        }

        public void SetSpeed(int speed) => Speed = speed;

        void Finish()
        {
            if (finished) return;
            finished = true;
            outcome = app.Session.FinishRaid(battle);
            app.Rebuild();
        }

        void RefreshView()
        {
            foreach (var u in battle.Units)
            {
                if (!views.TryGetValue(u.Id, out var v) || v.root == null) continue;
                v.root.gameObject.SetActive(!u.Fled);
                v.root.anchoredPosition = new Vector2(u.X * Cell, -u.Y * Cell);
                var baseColor = u.Side == BattleSide.Defender ? Ally : Enemy;
                v.body.color = u.Down ? Theme.InkFaint : baseColor;
                bool isSel = selected.Contains(u.Id);
                v.ring.gameObject.SetActive(isSel || u.IsLeader);
                v.ring.color = isSel ? Color.white : Theme.Gold;
                v.hp.anchorMax = new Vector2(Mathf.Clamp01(u.MaxHp <= 0 ? 0 : u.Hp / (float)u.MaxHp), 1);
            }
            if (clock != null)
            {
                int left = Mathf.CeilToInt(battle.SecondsLeft);
                clock.text = finished ? "끝" : $"남은 시간 {left / 60}:{left % 60:00}" + (Speed == 0 ? " (멈춤)" : "");
            }
            RefreshInfo();
            if (logList != null && battle.Log.Count != loggedLines)
            {
                loggedLines = battle.Log.Count;
                UiFactory.Clear(logList);
                for (int i = battle.Log.Count - 1; i >= 0; i--)
                    UiFactory.Label(logList, battle.Log[i], Theme.SmallSize, i == battle.Log.Count - 1 ? Theme.Ink : Theme.InkDim).name = "LogLine";
            }
        }

        void RefreshInfo()
        {
            if (info == null) return;
            UiFactory.Clear(info);
            var first = selected.Select(battle.Unit).FirstOrDefault(u => u != null);
            if (first == null)
            {
                int ours = battle.Units.Count(u => u.Side == BattleSide.Defender && u.Active);
                int theirs = battle.Units.Count(u => u.Side == BattleSide.Attacker && u.Active);
                UiFactory.Label(info, $"아군 {ours}명, 적 {theirs}명", Theme.BodySize, Theme.Ink).Size(-1, 30);
                UiFactory.Label(info, "아군을 눌러 고르고, 빈 땅을 누르면 그리로 가고, 적을 누르면 그 적을 친다.", Theme.SmallSize, Theme.InkDim).Size(-1, 60);
                return;
            }
            string more = selected.Count > 1 ? $" 외 {selected.Count - 1}명" : "";
            UiFactory.Label(info, first.Name + more, Theme.SubheadingSize, Theme.Ink, true).Size(-1, 34);
            UiFactory.Label(info, $"{Titles.Realm(first.Realm)}   체력 {first.Hp}/{first.MaxHp}   내력 {(int)first.Force}/{first.MaxForce}",
                Theme.BodySize, Theme.InkDim).Size(-1, 28);
            var cards = first.Deck.Where(x => x.card.IsOffense).Select(x => x.card.Name).Distinct().Take(6);
            UiFactory.Label(info, "초식: " + string.Join(", ", cards), Theme.SmallSize, Theme.InkDim).Size(-1, 50);
            string order = first.Order == null ? "알아서 싸운다"
                : first.Order.Attack ? $"{Josa.EulReul(battle.Unit(first.Order.TargetId)?.Name ?? "적")} 친다"
                : $"({first.Order.X}, {first.Order.Y}) 자리로 간다";
            UiFactory.Label(info, "명령: " + order, Theme.SmallSize, Theme.Gold).Size(-1, 26);
        }

        void ShowResult(RectTransform root)
        {
            var shade = UiFactory.Panel(root, new Color(0, 0, 0, 0.55f), "ResultShade");
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var box = UiFactory.Panel(shade.transform, Theme.Panel, "ResultBox");
            box.rectTransform.Area(0.3f, 0.18f, 0.7f, 0.82f);
            var inner = UiFactory.Rect(box.transform, "Inner").Fill(36, 32, 36, 28);
            UiFactory.VStack(inner, 12);
            UiFactory.Label(inner, outcome == null || outcome.DefendersWon ? "습격을 막아냈다" : "장원이 짓밟혔다", Theme.HeadingSize, Theme.Ink, true).Size(-1, 50);
            var area = UiFactory.Rect(inner, "Area");
            UiFactory.VStack(area, 8);
            area.Size(-1, -1).flexibleHeight = 1;
            RaidPrompt.ShowOutcome(app, area, outcome);
            UiFactory.Button(inner, "확인", () => app.Show(new GameScreen(app)), width: 200, height: 52).name = "BattleOk";
        }
    }
}
