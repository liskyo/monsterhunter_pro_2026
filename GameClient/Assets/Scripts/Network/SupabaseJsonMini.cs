using System.Text.RegularExpressions;

namespace MonsterHunter.Network
{
    /// <summary>
    /// 最小 JSON 字串擷取（避免依賴 Newtonsoft）。僅供 Auth 回應使用。
    /// </summary>
    static class SupabaseJsonMini
    {
        public static bool TryGetString(string json, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return false;
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            value = m.Groups[1].Value;
            return true;
        }

        /// <summary>擷取 user 區塊中的 "id":"uuid"</summary>
        public static string EscapeJsonString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static bool TryGetUserId(string json, out string userId)
        {
            userId = null;
            if (string.IsNullOrEmpty(json)) return false;
            var m = Regex.Match(json, "\"user\"\\s*:\\s*\\{[^}]*\"id\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            userId = m.Groups[1].Value;
            return true;
        }
    }
}
