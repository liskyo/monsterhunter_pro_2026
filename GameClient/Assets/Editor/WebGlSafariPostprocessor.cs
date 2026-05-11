using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Callbacks;
using UnityEditor;
using UnityEngine;

namespace MonsterHunter.Editor
{
    /// <summary>
    /// WebGL 建置完成後補上 iPhone Safari 常用的 viewport／觸控樣式，避免縮放與捲動干擾全螢幕遊玩。
    /// </summary>
    public static class WebGlSafariPostprocessor
    {
        const string Marker = "monsterhunter-safari-webgl";

        [PostProcessBuild(999)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.WebGL)
                return;

            var indexPath = Path.Combine(pathToBuiltProject, "index.html");
            if (!File.Exists(indexPath))
                return;

            var html = File.ReadAllText(indexPath);
            if (html.Contains(Marker))
                return;

            var injection = new StringBuilder();
            injection.AppendLine();
            injection.Append("<!-- ").Append(Marker).AppendLine(" -->");
            injection.AppendLine(
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover\" />");
            injection.AppendLine("<meta name=\"apple-mobile-web-app-capable\" content=\"yes\" />");
            injection.AppendLine("<meta name=\"mobile-web-app-capable\" content=\"yes\" />");
            injection.Append("<style id=\"").Append(Marker).AppendLine("-style\">");
            injection.AppendLine(
                "  html, body { overflow: hidden; margin: 0; padding: 0; width: 100%; height: 100%; background: #000000;");
            injection.AppendLine(
                "    touch-action: none; -webkit-touch-callout: none; -webkit-user-select: none; user-select: none; }");
            injection.AppendLine("  canvas { display: block; touch-action: none; }");
            injection.AppendLine("</style>");

            var match = Regex.Match(html, @"<head[^>]*>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                Debug.LogWarning("[WebGlSafariPostprocessor] index.html 無 <head>，略過 Safari 注入。");
                return;
            }

            var sb = new StringBuilder(html);
            sb.Insert(match.Index + match.Length, injection.ToString());
            File.WriteAllText(indexPath, sb.ToString());
        }
    }
}
