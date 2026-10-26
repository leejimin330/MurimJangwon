using System.Collections;
using System.Collections.Generic;
using System.IO;
using Murim.Core.Data;
using Murim.Core.Session;
using Murim.View.UI;
using Murim.View.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View
{
    /// <summary>씬에 하나. 데이터를 읽고 화면을 바꾼다.</summary>
    public class Bootstrap : MonoBehaviour
    {
        public Font sansFont;
        public Font serifFont;
        public Camera uiCamera;

        public GameData Data { get; private set; }
        public GameSession Session { get; private set; }
        public RectTransform Root { get; private set; }

        UiScreen current;
        Text toast;
        float toastUntil;

        public static string SaveDir => Path.Combine(Application.persistentDataPath, "saves");
        public static string SlotPath(int slot) => Path.Combine(SaveDir, slot == 0 ? "autosave.json" : $"slot{slot}.json");

        void Start() => Init(Path.Combine(Application.streamingAssetsPath, "Data"));

        public void Init(string dataDir)
        {
            UiFactory.Sans = sansFont;
            UiFactory.Serif = serifFont != null ? serifFont : sansFont;
            if (Root == null)
            {
                var canvas = UiFactory.CreateCanvas(transform, uiCamera);
                var bg = UiFactory.Panel(canvas.transform, Theme.Ground, "Root");
                bg.rectTransform.Fill();
                Root = bg.rectTransform;
            }
            var load = DataLoader.LoadFromDirectory(dataDir);
            var errors = new List<string>(load.Errors);
            if (load.Ok) errors.AddRange(DataValidator.Validate(load.Data));
            if (errors.Count > 0)
            {
                Show(new ErrorScreen(errors));
                return;
            }
            Data = load.Data;
            Show(new TitleScreen(this));
        }

        void Update() => current?.Tick(Time.unscaledDeltaTime);

        public UiScreen Current => current;

        public void Show(UiScreen screen)
        {
            UiFactory.Clear(Root);
            current = screen;
            screen.Build(Root);
        }

        public void Rebuild() => Show(current);

        public void StartGame(GameSession session)
        {
            Session = session;
            session.AutosaveDirectory = SaveDir;
            Show(new GameScreen(this));
        }

        public void Toast(string text)
        {
            if (toast == null)
            {
                var bg = UiFactory.Panel(Root.parent, Theme.Ground, "Toast");
                bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 1);
                bg.rectTransform.pivot = new Vector2(0.5f, 1);
                bg.rectTransform.anchoredPosition = new Vector2(0, -84);
                var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                UiFactory.VStack(bg.transform, 0, 14);
                toast = UiFactory.Label(bg.transform, "", Theme.BodySize, Theme.Ink);
            }
            toast.text = text;
            toast.transform.parent.gameObject.SetActive(true);
            toast.transform.parent.SetAsLastSibling();
            toastUntil = Time.unscaledTime + 3f;
            if (Application.isPlaying) StartCoroutine(HideToast());
        }

        IEnumerator HideToast()
        {
            while (Time.unscaledTime < toastUntil) yield return null;
            if (toast != null) toast.transform.parent.gameObject.SetActive(false);
        }
    }
}
