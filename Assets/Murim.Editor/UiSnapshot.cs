using System;
using System.IO;
using Murim.Core.Actions;
using Murim.Core.Session;
using Murim.View;
using Murim.View.UI.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.Editor
{
    /// <summary>
    /// 화면을 에디터 모드에서 만들어 PNG로 찍는다 (개발 확인용).
    /// batchmode (그래픽 있음): -executeMethod Murim.Editor.UiSnapshot.CaptureAll
    /// 결과: TestResults/snapshots/*.png
    /// </summary>
    public static class UiSnapshot
    {
        const int Width = 1920, Height = 1080;
        static string OutDir => Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "snapshots");

        public static void CaptureAll()
        {
            try
            {
                Directory.CreateDirectory(OutDir);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = SceneBuilder.CreateCamera();
                var rt = new RenderTexture(Width, Height, 24);
                camera.targetTexture = rt;
                var app = new GameObject("Bootstrap").AddComponent<Bootstrap>();
                app.sansFont = AssetDatabase.LoadAssetAtPath<Font>(SceneBuilder.SansPath);
                app.serifFont = AssetDatabase.LoadAssetAtPath<Font>(SceneBuilder.SerifPath);
                app.uiCamera = camera;
                app.Init(Path.Combine(Application.streamingAssetsPath, "Data"));
                Shot(app, camera, "01_title");

                app.Show(new NewGameScreen(app));
                Shot(app, camera, "02_scenarios");

                var session = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "minor_sect", RegionId = "henan", Seed = 42 });
                app.StartGame(session);
                Shot(app, camera, "03_game_minor_sect");

                for (int i = 0; i < 120; i++) session.EndTurn();
                app.Rebuild();
                Shot(app, camera, "04_game_10_years");

                var hero = GameSession.NewGame(app.Data, new NewGameOptions { ScenarioId = "wandering_hero", RegionId = "sichuan", Seed = 7 });
                hero.Queue(new Travel(hero.Player.Id, "hubei"));
                app.StartGame(hero);
                Shot(app, camera, "05_game_wanderer");

                ExtraShots?.Invoke(app, camera);
                Debug.Log("스냅샷 저장: " + OutDir);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>추가 화면을 찍는 훅 (패널 확인용).</summary>
        public static Action<Bootstrap, Camera> ExtraShots;

        public static void Shot(Bootstrap app, Camera camera, string name)
        {
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(app.Root);
            }
            camera.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
