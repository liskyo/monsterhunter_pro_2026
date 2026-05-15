using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MonsterHunter.Editor
{
    /// <summary>
    /// 將倉庫根 <c>DesignData/</c> 複製到 <c>Assets/StreamingAssets/DesignData</c>，供執行期與 WebGL 一併封裝。
    /// </summary>
    public static class DesignDataStreamingAssetsSync
    {
        const string MenuPath = "Monster Hunter/Sync DesignData → StreamingAssets";

        [MenuItem(MenuPath)]
        static void MenuSync()
        {
            if (TrySync(log: true))
                EditorUtility.DisplayDialog("DesignData 同步完成", "已複製至 Assets/StreamingAssets/DesignData。", "確定");
            else
                EditorUtility.DisplayDialog("DesignData 同步失敗", "請查看 Console：可能找不到倉庫根目錄的 DesignData 資料夾。", "確定");
        }

        /// <returns>是否成功找到來源並完成複製（或來源與目標已一致）。</returns>
        public static bool TrySync(bool log = false)
        {
            if (!TryGetPaths(out var srcRoot, out var dstRoot))
            {
                if (log)
                    Debug.LogError("[DesignDataStreamingAssetsSync] 找不到倉庫 DesignData 目錄。");
                return false;
            }

            CopyDirectoryRecursive(srcRoot, dstRoot);
            AssetDatabase.Refresh();
            if (log)
                Debug.Log($"[DesignDataStreamingAssetsSync] {srcRoot} → {dstRoot}");
            return true;
        }

        internal static bool TryGetPaths(out string srcRoot, out string dstRoot)
        {
            srcRoot = null;
            dstRoot = Path.Combine(Application.dataPath, "StreamingAssets", "DesignData");

            var gameClient = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var repoRoot = Path.GetFullPath(Path.Combine(gameClient, ".."));
            var candidate = Path.Combine(repoRoot, "DesignData");
            if (!Directory.Exists(candidate))
                return false;

            srcRoot = candidate;
            return true;
        }

        static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destFile = Path.Combine(destDir, rel);
                var destParent = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destParent))
                    Directory.CreateDirectory(destParent);
                File.Copy(file, destFile, true);
            }
        }
    }

    sealed class DesignDataStreamingAssetsBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -2000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!DesignDataStreamingAssetsSync.TrySync(log: false))
                Debug.LogWarning(
                    "[DesignDataStreamingAssetsBuildPreprocessor] 建置前未能同步 DesignData；" +
                    "請確認倉庫根有 DesignData 資料夾，或手動執行「Monster Hunter/Sync DesignData → StreamingAssets」。");
        }
    }
}
