using System.Collections.Generic;
using System.Linq;
using Murim.Core.Actions;
using Murim.Core.Rules;
using Murim.Core.Text;
using Murim.Core.World;
using UnityEngine;

namespace Murim.View.UI.Panels
{
    /// <summary>세력 수장이 아닌 플레이어의 기본 패널: 지금 있는 곳과 개인 행동.</summary>
    public static class LocalPanel
    {
        public static void Build(RectTransform panel, Bootstrap app, INav nav)
        {
            var s = app.Session;
            var w = s.World;
            var d = s.Data;
            var me = s.Player;
            if (me == null) return;
            var here = me.LocationRegionId;
            d.Regions.TryGetValue(here ?? "", out var region);

            var head = UiFactory.Row(panel, 12, 54);
            UiFactory.Label(head, region?.Name ?? "어딘가", Theme.HeadingSize + 4, Theme.Ink, true).Size(-1, 54, 1);
            if (region != null) UiFactory.Button(head, "지역 정보", () => nav.ShowRegion(region.Id), width: 140);
            UiFactory.Label(panel, $"{Titles.Full(w, me)}, 협의 {me.Chivalry}", Theme.BodySize, Theme.InkDim).Size(-1, 30);

            var list = UiFactory.ScrollList(panel, 8);
            ((RectTransform)list.parent.parent).Size(-1, -1).flexibleHeight = 1;

            FactionPanel.Heading(list, "할 수 있는 일");
            var grid = ActionMenu.Grid(list);
            ActionMenu.Opener(grid, app, "이동", () => PickRegion(app, "어디로 갈까요", (to, name) => new Travel(me.Id, to, name), "한 달이 걸립니다"));
            ActionMenu.Opener(grid, app, $"표국 호위 ({Escort.Pay}냥)",
                () => PickRegion(app, "어디까지 호위할까요", (to, name) => new Escort(me.Id, to, name), "두 달 동안 다른 일을 못 합니다"));
            ActionMenu.Add(grid, app, "협행", new PersonalQuest(me.Id, false));
            ActionMenu.Add(grid, app, "청부", new PersonalQuest(me.Id, true));
            var nearby = Nearby(w, me);
            ActionMenu.Opener(grid, app, "비무", () => PickPerson(app, "누구와 겨룰까요", nearby, t => new Spar(me.Id, t)),
                nearby.Count == 0 ? "이 지역에 겨룰 사람이 없습니다" : null);
            ActionMenu.Opener(grid, app, $"교분 ({Befriend.Cost}냥)",
                () => PickPerson(app, "누구와 사귈까요", nearby, t => new Befriend(me.Id, t)),
                nearby.Count == 0 ? "이 지역에 사귈 사람이 없습니다" : null);
            if (me.FactionId == null)
            {
                ActionMenu.Opener(grid, app, "객경 되기", () => PickGuest(app, here));
                ActionMenu.Opener(grid, app, $"문파 창설 ({FoundSect.Cost}냥)", () => TextPrompt.Show(app, "새 문파의 이름", "예: 청풍문", "세우기",
                    name => ActionMenu.Queue(app, new FoundSect(me.Id, name))));
            }
            else ActionMenu.Add(grid, app, "세력 떠나기", new LeaveFaction(me.Id));

            FactionPanel.Heading(list, $"이 지역 사람들 ({nearby.Count})");
            if (nearby.Count == 0) FactionPanel.Info(list, "눈에 띄는 사람이 없다");
            foreach (var c in nearby)
            {
                var id = c.Id;
                var f = w.GetFaction(c.FactionId);
                FactionPanel.Link(list, f == null ? c.FullName : $"{f.Name} {Titles.Role(f, c.Role)} {c.FullName}",
                    () => nav.ShowCharacter(id), $"{c.AgeYears(w.Month)}세, {Titles.Realm(c.Realm)}");
            }

            var based = w.Factions.Values.Where(f => !f.IsDissolved && f.HomeRegionId == here).OrderBy(f => f.Name).ToList();
            FactionPanel.Heading(list, $"이곳에 본거지를 둔 세력 ({based.Count})");
            foreach (var f in based)
            {
                var id = f.Id;
                FactionPanel.Link(list, f.Name, () => nav.ShowFaction(id), $"{Theme.AlignmentName(f.Alignment)}, 영향력 {Influence.Get(w, here, f.Id)}");
            }
        }

        static List<Character> Nearby(WorldState w, Character me) =>
            w.Characters.Values.Where(c => PersonalRules.IsNearbyAdult(w, me, c))
                .OrderByDescending(c => c.Realm).ThenBy(c => c.FullName).ToList();

        static void PickRegion(Bootstrap app, string title, System.Func<string, string, IGameAction> make, string note)
        {
            var s = app.Session;
            var from = s.Data.Regions[s.Player.LocationRegionId];
            var opts = from.Neighbors.Select(id => ActionMenu.Option(app, s.Data.Regions[id].Name, make(id, s.Data.Regions[id].Name), note)).ToList();
            Picker.Show(app, title, opts, "갈 수 있는 곳이 없습니다");
        }

        static void PickPerson(Bootstrap app, string title, List<Character> people, System.Func<string, IGameAction> make)
        {
            var w = app.Session.World;
            var me = app.Session.Player;
            var opts = people.Select(c => ActionMenu.Option(app, c.FullName, make(c.Id),
                $"{Titles.Realm(c.Realm)}, 호감 {Opinion.Of(w, app.Session.Data, c.Id, me.Id)}")).ToList();
            Picker.Show(app, title, opts);
        }

        static void PickGuest(Bootstrap app, string here)
        {
            var w = app.Session.World;
            var me = app.Session.Player;
            var opts = w.Factions.Values.Where(f => !f.IsDissolved && f.HomeRegionId == here).OrderBy(f => f.Name)
                .Select(f => ActionMenu.Option(app, f.Name, new JoinAsGuest(me.Id, f.Id), Theme.AlignmentName(f.Alignment))).ToList();
            Picker.Show(app, "어느 세력에 몸을 의탁할까요", opts, "이 지역에 본거지를 둔 세력이 없습니다");
        }
    }
}
