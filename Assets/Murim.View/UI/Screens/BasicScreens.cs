using System.Collections.Generic;
using System.IO;
using Murim.Core.Session;
using Murim.Core.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Screens
{
    public abstract class UiScreen
    {
        public abstract void Build(RectTransform root);

        /// <summary>매 프레임 (실시간 화면만 쓴다). dt는 실제 초.</summary>
        public virtual void Tick(float dt) { }
    }

    public class ErrorScreen : UiScreen
    {
        readonly List<string> errors;

        public ErrorScreen(List<string> errors) => this.errors = errors;

        public override void Build(RectTransform root)
        {
            var box = UiFactory.Rect(root, "Errors").Fill(160, 120, 160, 120);
            UiFactory.VStack(box, 16);
            UiFactory.Label(box, "게임 데이터를 읽지 못했습니다", Theme.HeadingSize, Theme.Ink, true).Size(-1, 48);
            UiFactory.Label(box, "아래 파일을 고친 뒤 게임을 다시 시작하세요. (StreamingAssets/Data)", Theme.BodySize, Theme.InkDim).Size(-1, 32);
            var list = UiFactory.ScrollList(box, 8);
            ((RectTransform)list.parent.parent).Size(-1, 700);
            foreach (var e in errors) UiFactory.Label(list, e, Theme.BodySize, Theme.Unorthodox);
        }
    }

    public class TitleScreen : UiScreen
    {
        readonly Bootstrap app;

        public TitleScreen(Bootstrap app) => this.app = app;

        public override void Build(RectTransform root)
        {
            var col = UiFactory.Rect(root, "Title").Area(0.1f, 0.12f, 0.45f, 0.88f);
            var v = UiFactory.VStack(col, 14);
            v.childAlignment = TextAnchor.MiddleLeft;

            UiFactory.Label(col, "무림장원", 112, Theme.Ink, true).Size(-1, 150);
            UiFactory.Label(col, "작은 문파 하나, 칼 한 자루로 시작해 대를 잇는 강호의 이야기", Theme.SubheadingSize, Theme.InkDim)
                .Size(-1, 40);
            UiFactory.Rect(col, "Gap").Size(-1, 40);

            UiFactory.Button(col, "새 게임", () => app.Show(new NewGameScreen(app)), width: 360, height: 56);
            bool hasAuto = File.Exists(Bootstrap.SlotPath(0));
            UiFactory.Button(col, "이어하기 (자동 저장)", () => Load(0), hasAuto, "자동 저장이 아직 없습니다", width: 360, height: 56);
            for (int slot = 1; slot <= 3; slot++)
            {
                int s = slot;
                bool exists = File.Exists(Bootstrap.SlotPath(s));
                UiFactory.Button(col, $"불러오기 {s}번 칸", () => Load(s), exists, "비어 있는 칸입니다", width: 360, height: 48);
            }
            UiFactory.Button(col, "종료", Application.Quit, width: 360, height: 48);
        }

        void Load(int slot)
        {
            var r = GameSession.Load(app.Data, Bootstrap.SlotPath(slot));
            if (r.Ok) app.StartGame(r.Session);
            else app.Toast(r.Error);
        }
    }
}
