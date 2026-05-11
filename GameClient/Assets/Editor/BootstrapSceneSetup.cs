using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonsterHunter.Editor
{
    /// <summary>
    /// 建立最小 Bootstrap 場景並加入 <see cref="EditorBuildSettings"/>（解決尚無 .unity / 未勾選場景的問題）。
    /// </summary>
    public static class BootstrapSceneSetup
    {
        const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        const string MenuPath = "Monster Hunter/準備建置：建立 Bootstrap 場景並加入 Build Settings";

        [MenuItem(MenuPath)]
        static void EnsureBootstrapSceneInBuild()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var fullPath = Path.Combine(Application.dataPath, "Scenes", "Bootstrap.unity");
            if (!File.Exists(fullPath))
            {
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(newScene, ScenePath);
            }

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var idx = list.FindIndex(s => s.path == ScenePath);
            if (idx >= 0)
                list[idx] = new EditorBuildSettingsScene(ScenePath, true);
            else
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            EditorBuildSettings.scenes = list.ToArray();

            EditorUtility.DisplayDialog(
                "Build Settings 已更新",
                "場景：\n" + ScenePath +
                    "\n\n已加入「File → Build Settings → Scenes In Build」並勾選。\n\n" +
                    "接下來請在 Unity Hub 為此 Editor 版本安裝「WebGL Build Support」（見倉庫 docs/第一次建置WebGL.md），\n" +
                    "再使用選單「Monster Hunter → Build WebGL（iPhone Safari）」。",
                "確定");
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateMenu()
        {
            return !Application.isPlaying;
        }
    }
}
