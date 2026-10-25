using Murim.Core.Session;
using Murim.View.UI.Panels;
using UnityEngine;

namespace Murim.View.UI.Screens
{
    /// <summary>상단 바 + 지도 + 오른쪽 패널 + 연대기.</summary>
    public class GameScreen : UiScreen
    {
        readonly Bootstrap app;
        GameSession S => app.Session;
        string selectedRegion;

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
            TopBar.Build(bar, S, EndTurn, Save, () => app.Show(new TitleScreen(app)), app.Rebuild);

            var mapArea = UiFactory.Rect(root, "MapArea");
            mapArea.anchorMin = new Vector2(0, 0.3f);
            mapArea.anchorMax = new Vector2(0.62f, 1);
            mapArea.offsetMin = Vector2.zero;
            mapArea.offsetMax = new Vector2(0, -80);
            MapView.Build(mapArea, S, selectedRegion, id => { selectedRegion = id; app.Rebuild(); });

            var chronicle = UiFactory.Panel(root, Theme.Panel, "Chronicle").rectTransform;
            chronicle.anchorMin = new Vector2(0, 0);
            chronicle.anchorMax = new Vector2(0.62f, 0.3f);
            chronicle.offsetMin = new Vector2(16, 16);
            chronicle.offsetMax = new Vector2(-8, -8);
            BuildChronicle(chronicle);

            var side = UiFactory.Panel(root, Theme.Panel, "Side").rectTransform;
            side.anchorMin = new Vector2(0.62f, 0);
            side.anchorMax = new Vector2(1, 1);
            side.offsetMin = new Vector2(8, 16);
            side.offsetMax = new Vector2(-16, -96);
            var inner = UiFactory.Rect(side, "Inner").Fill(28, 24, 28, 24);
            UiFactory.VStack(inner, 10);
            BuildSide(inner);
        }

        protected virtual void BuildChronicle(RectTransform area) { }

        protected virtual void BuildSide(RectTransform panel)
        {
            if (selectedRegion != null) RegionPanel.Build(panel, S, selectedRegion, id => { });
        }

        void EndTurn()
        {
            S.EndTurn();
            app.Rebuild();
        }

        void Save()
        {
            var r = S.Save(Bootstrap.SlotPath(1));
            app.Toast(r.Ok ? "1번 칸에 저장했습니다" : r.Error);
        }
    }
}
