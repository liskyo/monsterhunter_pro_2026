using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MonsterHunter.Data
{
    /// <summary>
    /// 跨平台讀取企劃 JSON／文字：編輯器先走倉庫根 <c>DesignData</c>，其餘以 <c>StreamingAssets/DesignData</c> 為準
    /// （建置前由 <c>IPreprocessBuild</c> 同步）；WebGL／部分裝置以 <c>UnityWebRequest</c> 讀 StreamingAssets。
    /// </summary>
    public static class DesignDataReader
    {
        /// <summary>
        /// 路徑片段相對於 <c>DesignData/</c>，例如 <c>"03_Combat", "combat_tuning.json"</c>。
        /// </summary>
        public static bool TryLoadDesignDataText(out string text, params string[] relativeSegments)
        {
            text = null;
            if (relativeSegments == null || relativeSegments.Length == 0)
                return false;

            var relFs = Path.Combine(relativeSegments);

#if UNITY_EDITOR
            if (TryLoadFromRepoWalk(relFs, out text))
                return true;
#endif

            var saPath = Path.Combine(Application.streamingAssetsPath, "DesignData", relFs);

#if !UNITY_WEBGL
            if (TryReadAllTextIfExists(saPath, out text))
                return true;
#endif

            var relUrl = string.Join("/",
                relativeSegments
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s.Replace('\\', '/').TrimStart('/')));
            if (string.IsNullOrEmpty(relUrl))
                return false;

            var url = BuildStreamingAssetsDesignDataUrl(relUrl);
            return TryGetViaUnityWebRequest(url, out text);
        }

#if UNITY_EDITOR
        static bool TryLoadFromRepoWalk(string relativeUnderDesignData, out string text)
        {
            text = null;
            var dir = new DirectoryInfo(Application.dataPath);
            for (var i = 0; i < 8 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "DesignData", relativeUnderDesignData);
                if (TryReadAllTextIfExists(candidate, out text))
                    return true;
                dir = dir.Parent;
            }

            return false;
        }
#endif

        static bool TryReadAllTextIfExists(string fullPath, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return false;
            text = File.ReadAllText(fullPath);
            return true;
        }

        static string BuildStreamingAssetsDesignDataUrl(string relativeSlashJoined)
        {
            var baseSa = Application.streamingAssetsPath.TrimEnd('/', '\\').Replace('\\', '/');
            var tail = "DesignData/" + relativeSlashJoined.TrimStart('/');

            if (baseSa.Contains("://"))
                return $"{baseSa}/{tail}";

            var fullPath = Path.GetFullPath(
                Path.Combine(Application.streamingAssetsPath, "DesignData",
                    relativeSlashJoined.Replace('/', Path.DirectorySeparatorChar)));
            return new System.Uri(fullPath).AbsoluteUri;
        }

        static bool TryGetViaUnityWebRequest(string url, out string text)
        {
            text = null;
            using var req = UnityWebRequest.Get(url);
#if UNITY_EDITOR
            req.timeout = 60;
#endif
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
            }

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogWarning($"[DesignDataReader] 無法載入 {url}：{req.error}");
                return false;
            }

            text = req.downloadHandler != null ? req.downloadHandler.text : "";
            return true;
        }
    }
}
