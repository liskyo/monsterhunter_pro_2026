using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MonsterHunter.Editor
{
    /// <summary>
    /// 一鍵輸出 WebGL 至倉庫 <c>Web/webgl/</c>，供 iPhone Safari 與其他瀏覽器開啟（須 HTTPS 正式環境）。
    /// </summary>
    public static class MonsterHunterWebGlBuildMenu
    {
        const string MenuPath = "Monster Hunter/Build WebGL（iPhone Safari）";

        [MenuItem(MenuPath)]
        static void BuildWebGl()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "無法建置 WebGL",
                    "請先在選單執行：\n「Monster Hunter → 準備建置：建立 Bootstrap 場景並加入 Build Settings」\n\n" +
                        "或手動：File → Build Settings → 加入並勾選至少一個場景。",
                    "確定");
                return;
            }

            ApplyRecommendedWebGlPlayerSettings();

            var outDir = GetWebGlOutputDirectory();
            Directory.CreateDirectory(outDir);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(
                    "WebGL 建置失敗",
                    report.summary.result + " — 詳見 Console。",
                    "確定");
                return;
            }

            EditorUtility.RevealInFinder(outDir);
            EditorUtility.DisplayDialog(
                "WebGL 建置完成",
                "輸出目錄：\n" + outDir +
                    "\n\n請將此資料夾部署到 HTTPS（例如 Vercel），再以 iPhone Safari 開啟網址。\n" +
                    "詳見倉庫 Web/README.md。",
                "確定");
        }

        static void ApplyRecommendedWebGlPlayerSettings()
        {
            // 直立手遊預設比例（實際畫面仍依 Canvas／Camera）
            PlayerSettings.defaultWebScreenWidth = 1080;
            PlayerSettings.defaultWebScreenHeight = 1920;

            PlayerSettings.runInBackground = true;

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.decompressionFallback = true;
#endif

#if UNITY_2022_1_OR_NEWER
            if (PlayerSettings.WebGL.initialMemorySize < 32)
                PlayerSettings.WebGL.initialMemorySize = 32;
#elif UNITY_2020_1_OR_NEWER
#pragma warning disable CS0618
            if (PlayerSettings.WebGL.memorySize < 32)
                PlayerSettings.WebGL.memorySize = 32;
#pragma warning restore CS0618
#endif
        }

        /// <summary>GameClient 上一層為倉庫根目錄，輸出至 Web/webgl。</summary>
        internal static string GetWebGlOutputDirectory()
        {
            var gameClient = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var repoRoot = Path.GetFullPath(Path.Combine(gameClient, ".."));
            return Path.Combine(repoRoot, "Web", "webgl");
        }
    }
}
