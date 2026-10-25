using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Session;
using UnityEngine;

namespace Murim.View.UI.Screens
{
    /// <summary>시나리오 → 시작 지역 → 인물 설정, 세 단계.</summary>
    public class NewGameScreen : UiScreen
    {
        readonly Bootstrap app;
        readonly System.Random dice = new System.Random();
        int step;
        readonly NewGameOptions o = new NewGameOptions();
        string seedText;

        public NewGameScreen(Bootstrap app)
        {
            this.app = app;
            o.Seed = (ulong)dice.Next(1, int.MaxValue);
            seedText = o.Seed.ToString();
        }

        ScenarioDef Scenario => o.ScenarioId == null ? null : app.Data.Scenarios[o.ScenarioId];

        public override void Build(RectTransform root)
        {
            var page = UiFactory.Rect(root, "NewGame").Fill(120, 70, 120, 60);
            UiFactory.VStack(page, 20);
            var steps = new[] { "시나리오", "시작 지역", "인물" };
            UiFactory.Label(page, steps[step] + " 고르기", Theme.HeadingSize + 6, Theme.Ink, true).Size(-1, 56);
            var trail = UiFactory.Row(page, 24, 30);
            for (int i = 0; i < steps.Length; i++)
                UiFactory.Label(trail, $"{i + 1}. {steps[i]}", Theme.BodySize, i == step ? Theme.Gold : Theme.InkFaint).Size(160);

            var body = UiFactory.Rect(page, "Body");
            body.Size(-1, 760);
            if (step == 0) BuildScenarios(body);
            else if (step == 1) BuildRegions(body);
            else BuildCharacter(body);

            var nav = UiFactory.Row(page, 16, 56);
            UiFactory.Button(nav, step == 0 ? "처음 화면으로" : "이전", () =>
            {
                if (step == 0) app.Show(new TitleScreen(app));
                else { step--; app.Rebuild(); }
            }, width: 200, height: 52);
        }

        void BuildScenarios(RectTransform body)
        {
            var grid = UiFactory.Rect(body, "Grid").Fill();
            var g = grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            g.cellSize = new Vector2(530, 240);
            g.spacing = new Vector2(24, 24);
            foreach (var s in app.Data.Scenarios.Values.OrderBy(Order))
            {
                var card = UiFactory.Panel(grid, Theme.Panel, "Scenario");
                card.raycastTarget = true;
                var btn = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                var id = s.Id;
                btn.onClick.AddListener(() =>
                {
                    o.ScenarioId = id;
                    o.Alignment = app.Data.Scenarios[id].AllowedAlignments[0];
                    step = 1;
                    app.Rebuild();
                });
                var inner = UiFactory.Rect(card.transform, "Inner").Fill(28, 24, 28, 24);
                UiFactory.VStack(inner, 10);
                UiFactory.Label(inner, s.Name, Theme.HeadingSize, Theme.Ink, true).Size(-1, 44);
                UiFactory.Label(inner, s.Description, Theme.BodySize, Theme.InkDim);
                UiFactory.Label(inner, Summary(s), Theme.SmallSize, Theme.Gold);
            }
        }

        static int Order(ScenarioDef s) =>
            new[] { "minor_sect", "fallen_clan", "black_gang", "wandering_hero", "ronin" }.ToList().IndexOf(s.Id) is int i && i >= 0 ? i : 99;

        static string Summary(ScenarioDef s)
        {
            if (!s.HasFaction) return $"홀몸, 은자 {s.Silver}냥, {Core.Text.Titles.Realm(s.Realm)}";
            var debt = s.Debt > 0 ? $", 빚 {s.Debt}냥" : "";
            return $"제자 {s.NamedMembers}명, 문도 {s.UnnamedDisciples}명, 은자 {s.Silver}냥{debt}";
        }

        void BuildRegions(RectTransform body)
        {
            var grid = UiFactory.Rect(body, "Grid").Fill();
            var g = grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            g.cellSize = new Vector2(320, 120);
            g.spacing = new Vector2(20, 20);
            foreach (var r in app.Data.Regions.Values.OrderBy(r => r.Y).ThenBy(r => r.X))
            {
                var factions = app.Data.Factions.Values.Where(f => f.HomeRegion == r.Id).ToList();
                var card = UiFactory.Panel(grid, o.RegionId == r.Id ? Theme.PanelRaised : Theme.Panel, "Region");
                card.raycastTarget = true;
                var btn = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                var id = r.Id;
                btn.onClick.AddListener(() => { o.RegionId = id; step = 2; app.Rebuild(); });
                var inner = UiFactory.Rect(card.transform, "Inner").Fill(20, 16, 20, 16);
                UiFactory.VStack(inner, 6);
                UiFactory.Label(inner, r.Name, Theme.HeadingSize, Theme.Ink, true).Size(-1, 40);
                var big = factions.OrderByDescending(f => f.Influence).Take(2).Select(f => f.Name);
                UiFactory.Label(inner, string.Join(", ", big) + $" 외 {factions.Count - 2}곳", Theme.SmallSize, Theme.InkDim);
            }
        }

        void BuildCharacter(RectTransform body)
        {
            var s = Scenario;
            var form = UiFactory.Rect(body, "Form").Area(0, 0, 0.55f, 1);
            UiFactory.VStack(form, 14);

            Field(form, "성별", row =>
            {
                UiFactory.Choice(row, "남", o.Sex == Sex.Male, () => { o.Sex = Sex.Male; o.GivenName = null; app.Rebuild(); }, width: 90);
                UiFactory.Choice(row, "여", o.Sex == Sex.Female, () => { o.Sex = Sex.Female; o.GivenName = null; app.Rebuild(); }, width: 90);
            });
            if (o.FamilyName == null) Reroll();
            Field(form, "이름", row =>
            {
                UiFactory.Input(row, o.FamilyName, "성", v => o.FamilyName = v, 120);
                UiFactory.Input(row, o.GivenName, "이름", v => o.GivenName = v, 180);
                UiFactory.Button(row, "다시 짓기", () => { Reroll(); app.Rebuild(); }, width: 140);
            });
            if (s.HasFaction)
                Field(form, s.FactionType == FactionType.Clan ? "세가 이름" : "문파 이름", row =>
                    UiFactory.Input(row, o.FactionName, "비워 두면 무작위", v => o.FactionName = v, 320));
            if (s.AllowedAlignments.Length > 1)
                Field(form, "성향", row =>
                {
                    foreach (var a in s.AllowedAlignments)
                    {
                        var al = a;
                        UiFactory.Choice(row, Theme.AlignmentName(a), o.Alignment == a, () => { o.Alignment = al; app.Rebuild(); }, width: 120);
                    }
                });

            UiFactory.Label(form, "타고난 특성 (최대 2개, 나머지는 무작위)", Theme.BodySize, Theme.InkDim).Size(-1, 34);
            var traits = UiFactory.Rect(form, "Traits");
            traits.Size(-1, 300);
            var tg = traits.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            tg.cellSize = new Vector2(150, 44);
            tg.spacing = new Vector2(10, 10);
            foreach (var t in app.Data.Traits.Values)
            {
                bool chosen = o.ChosenTraitIds.Contains(t.Id);
                bool blocked = !chosen && (o.ChosenTraitIds.Count >= 2
                    || o.ChosenTraitIds.Any(c => app.Data.Traits[c].Opposites.Contains(t.Id)));
                var id = t.Id;
                var b = UiFactory.Choice(traits, t.Name, chosen, () =>
                {
                    if (!o.ChosenTraitIds.Remove(id)) o.ChosenTraitIds.Add(id);
                    app.Rebuild();
                }, !blocked, blocked ? "더 고를 수 없거나 고른 특성과 맞지 않습니다" : null);
                if (!blocked) Tooltip.Attach(b.gameObject, t.Description);
            }

            Field(form, "시드", row => UiFactory.Input(row, seedText, "숫자", v => seedText = v, 260));

            var side = UiFactory.Panel(body, Theme.Panel, "Summary");
            side.rectTransform.Area(0.6f, 0.25f, 1, 1);
            var inner = UiFactory.Rect(side.transform, "Inner").Fill(32, 28, 32, 28);
            UiFactory.VStack(inner, 12);
            UiFactory.Label(inner, s.Name, Theme.HeadingSize, Theme.Ink, true).Size(-1, 44);
            UiFactory.Label(inner, app.Data.Regions[o.RegionId].Name + "에서 시작", Theme.SubheadingSize, Theme.Gold).Size(-1, 34);
            UiFactory.Label(inner, s.Description, Theme.BodySize, Theme.InkDim);
            UiFactory.Label(inner, Summary(s), Theme.SmallSize, Theme.InkDim);
            UiFactory.Rect(inner, "Gap").Size(-1, 20);
            UiFactory.Button(inner, "이 인물로 시작", Start, width: 280, height: 56);
        }

        void Field(RectTransform parent, string label, System.Action<RectTransform> content)
        {
            var row = UiFactory.Row(parent, 12, 48);
            UiFactory.Label(row, label, Theme.BodySize, Theme.InkDim).Size(130);
            content(row);
        }

        void Reroll()
        {
            var names = app.Data.Names;
            o.FamilyName = names.FamilyNames[dice.Next(names.FamilyNames.Length)];
            var given = o.Sex == Sex.Male ? names.MaleGivenNames : names.FemaleGivenNames;
            o.GivenName = given[dice.Next(given.Length)];
        }

        void Start()
        {
            if (!ulong.TryParse(seedText?.Trim(), out var seed))
            {
                app.Toast("시드는 숫자로 적어야 합니다");
                return;
            }
            o.Seed = seed;
            app.StartGame(GameSession.NewGame(app.Data, o));
        }
    }
}
