using Murim.Core.Actions;
using Murim.Core.Session;
using Murim.View.UI.Panels;
using UnityEngine;

namespace Murim.View.UI.Screens
{
    /// <summary>상단 바 + 지도 + 오른쪽 패널 + 연대기.</summary>
    public class GameScreen : UiScreen, INav
    {
        enum SideMode { Home, Region, Faction, Character, Family }

        readonly Bootstrap app;
        GameSession S => app.Session;
        string selectedRegion;
        SideMode mode = SideMode.Home;
        string selectedId;
        bool chronicleOwnOnly;

        public GameScreen(Bootstrap app)
        {
            this.app = app;
            selectedRegion = S.Player?.LocationRegionId ?? S.World.GetFaction(S.Player?.FactionId)?.HomeRegionId;
        }

        public override void Build(RectTransform root)
        {
            var bar = UiFactory.Panel(root, Theme.Panel, "TopBar").rectTransform;
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, 80);
            TopBar.Build(bar, S, EndTurn, () => SaveLoadDialog.Show(app), () => app.Show(new TitleScreen(app)), app.Rebuild);

            var mapArea = UiFactory.Rect(root, "MapArea");
            mapArea.anchorMin = new Vector2(0, 0.3f);
            mapArea.anchorMax = new Vector2(0.62f, 1);
            mapArea.offsetMin = Vector2.zero;
            mapArea.offsetMax = new Vector2(0, -80);
            MapView.Build(mapArea, S, selectedRegion, ShowRegion);

            var chronicle = UiFactory.Panel(root, Theme.Panel, "Chronicle").rectTransform;
            chronicle.anchorMin = new Vector2(0, 0);
            chronicle.anchorMax = new Vector2(0.62f, 0.3f);
            chronicle.offsetMin = new Vector2(16, 16);
            chronicle.offsetMax = new Vector2(-8, -8);
            ChroniclePanel.Build(chronicle, S, chronicleOwnOnly, ownOnly =>
            {
                chronicleOwnOnly = ownOnly;
                app.Rebuild();
            });

            var side = UiFactory.Panel(root, Theme.Panel, "Side").rectTransform;
            side.anchorMin = new Vector2(0.62f, 0);
            side.anchorMax = new Vector2(1, 1);
            side.offsetMin = new Vector2(8, 16);
            side.offsetMax = new Vector2(-16, -96);
            var inner = UiFactory.Rect(side, "Inner").Fill(28, 20, 28, 24);
            UiFactory.VStack(inner, 10);
            BuildTabs(inner);
            BuildSide(inner);
        }

        /// <summary>어느 패널에 있든 "나"와 기본 패널로 바로 돌아오는 단추.</summary>
        void BuildTabs(RectTransform panel)
        {
            var p = S.Player;
            if (p == null) return;
            var row = UiFactory.Row(panel, 10, 40);
            bool personal = PersonalRules.UsesPersonalActions(S.World, p);
            UiFactory.Choice(row, personal ? "주변" : "우리 세력", mode == SideMode.Home, ShowHome, width: 150, height: 40);
            UiFactory.Choice(row, "나", mode == SideMode.Character && selectedId == p.Id, () => ShowCharacter(p.Id), width: 90, height: 40);
        }

        void BuildSide(RectTransform panel)
        {
            switch (mode)
            {
                case SideMode.Region:
                    RegionPanel.Build(panel, S, selectedRegion, ShowFaction);
                    return;
                case SideMode.Faction:
                    FactionPanel.Build(panel, app, this, selectedId);
                    return;
                case SideMode.Character:
                    CharacterPanel.Build(panel, app, this, selectedId);
                    return;
                case SideMode.Family:
                    FamilyTreePanel.Build(panel, app, this, selectedId);
                    return;
            }
            var p = S.Player;
            if (p == null) return;
            if (PersonalRules.UsesPersonalActions(S.World, p)) LocalPanel.Build(panel, app, this);
            else FactionPanel.Build(panel, app, this, p.FactionId);
        }

        void Go(SideMode m, string id)
        {
            mode = m;
            selectedId = id;
            app.Rebuild();
        }

        public void ShowHome() => Go(SideMode.Home, null);

        public void ShowRegion(string regionId)
        {
            selectedRegion = regionId;
            Go(SideMode.Region, null);
        }

        public void ShowFaction(string factionId) => Go(SideMode.Faction, factionId);
        public void ShowCharacter(string characterId) => Go(SideMode.Character, characterId);
        public void ShowFamily(string characterId) => Go(SideMode.Family, characterId);

        void EndTurn()
        {
            S.EndTurn();
            // 이동했다면 지도 선택도 따라간다.
            if (mode == SideMode.Home) selectedRegion = S.Player?.LocationRegionId ?? selectedRegion;
            app.Rebuild();
        }
    }
}
