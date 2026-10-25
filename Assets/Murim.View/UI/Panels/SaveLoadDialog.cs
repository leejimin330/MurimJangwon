using System.IO;
using Murim.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>슬롯 1~3 저장·불러오기와 자동 저장 불러오기. 불러오기에 실패하면 지금 게임을 그대로 둔다.</summary>
    public static class SaveLoadDialog
    {
        public static void Show(Bootstrap app)
        {
            var shade = UiFactory.Panel(app.Root.parent, new Color(0, 0, 0, 0.55f), "SaveShade");
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            void Close() => Object.Destroy(shade.gameObject);
            shade.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            var box = UiFactory.Panel(shade.transform, Theme.Panel, "SaveLoad");
            box.raycastTarget = true;
            box.rectTransform.Area(0.3f, 0.22f, 0.7f, 0.78f);
            var inner = UiFactory.Rect(box.transform, "Inner").Fill(28, 24, 28, 24);
            UiFactory.VStack(inner, 12);
            UiFactory.Label(inner, "저장 · 불러오기", Theme.HeadingSize, Theme.Ink, true).Size(-1, 46);
            var status = UiFactory.Label(inner, "", Theme.BodySize, Theme.Unorthodox);
            status.Size(-1, 30);

            for (int slot = 0; slot <= 3; slot++)
            {
                int n = slot;
                string path = Bootstrap.SlotPath(n);
                bool exists = File.Exists(path);
                var row = UiFactory.Row(inner, 10, 56);
                var col = UiFactory.Rect(row, "Col");
                col.Size(-1, 56, 1);
                UiFactory.VStack(col, 0);
                UiFactory.Label(col, n == 0 ? "자동 저장 (매년 1월)" : $"{n}번 칸", Theme.BodySize, Theme.Ink).Size(-1, 30);
                UiFactory.Label(col, exists ? File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm") + " 저장" : "비어 있음",
                    Theme.SmallSize, Theme.InkFaint).Size(-1, 24);
                if (n > 0)
                    UiFactory.Button(row, "저장", () =>
                    {
                        var r = app.Session.Save(path);
                        if (r.Ok)
                        {
                            Close();
                            app.Toast($"{n}번 칸에 저장했습니다");
                        }
                        else status.text = "저장하지 못했습니다: " + r.Error;
                    }, width: 110);
                UiFactory.Button(row, "불러오기", () =>
                {
                    var r = GameSession.Load(app.Data, path);
                    if (!r.Ok)
                    {
                        status.text = "불러오지 못했습니다: " + r.Error;
                        return;
                    }
                    Close();
                    app.StartGame(r.Session);
                    app.Toast("불러왔습니다");
                }, exists, "비어 있는 칸입니다", width: 130);
            }
            UiFactory.Rect(inner, "Gap").Size(-1, -1).flexibleHeight = 1;
            UiFactory.Button(inner, "닫기", Close, width: 160);
        }
    }
}
