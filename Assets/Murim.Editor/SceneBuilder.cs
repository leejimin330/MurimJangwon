using Murim.View;
using Murim.View.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Murim.Editor
{
    /// <summary>Main 씬을 코드로 만든다. 메뉴 또는 batchmode -executeMethod로 실행.</summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string SansPath = "Assets/Murim.View/Fonts/NotoSansKR-Regular.otf";
        public const string SerifPath = "Assets/Murim.View/Fonts/NotoSerifKR-SemiBold.otf";

        [MenuItem("Murim/Build Main Scene")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var app = new GameObject("Bootstrap").AddComponent<Bootstrap>();
            app.sansFont = AssetDatabase.LoadAssetAtPath<Font>(SansPath);
            app.serifFont = AssetDatabase.LoadAssetAtPath<Font>(SerifPath);
            app.uiCamera = camera;
            if (app.sansFont == null) Debug.LogError("폰트를 찾을 수 없습니다: " + SansPath);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "무림장원";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            Debug.Log("Main 씬을 만들었습니다: " + ScenePath);
        }

        public static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Ground;
            camera.transform.position = new Vector3(0, 0, -10);
            return camera;
        }
    }
}
