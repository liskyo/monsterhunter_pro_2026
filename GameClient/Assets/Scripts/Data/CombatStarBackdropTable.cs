using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Data
{
    /// <summary>
    /// 載入星級遠景「地圖短名」：<c>CS{tier}_{地圖名}.png</c>。
    /// 對應 <c>DesignData/03_Combat/combat_star_backdrops_by_tier.json</c>。
    /// </summary>
    public static class CombatStarBackdropTable
    {
        const string RelDir = "03_Combat";
        const string RelFile = "combat_star_backdrops_by_tier.json";

        static Dictionary<int, string[]> _mapsByTier;
        static bool _attemptedLoad;

        static readonly string[][] DefaultTierMaps =
        {
            new[] { "古代樹森林", "龍結晶之地" },
            new[] { "大蟻塚荒地", "地熱火山" },
            new[] { "塔之秘境", "瘴氣之谷" },
            new[] { "陸珊瑚台地", "古代樹森林" },
            new[] { "龍結晶之地", "塔之秘境" },
            new[] { "瘴氣之谷", "冰封群島" },
            new[] { "地熱火山", "大蟻塚荒地" },
            new[] { "古代樹森林", "地熱火山" },
            new[] { "龍結晶之地", "陸珊瑚台地" },
            new[] { "冰封群島", "瘴氣之谷" },
        };

        sealed class FileDto
        {
            [JsonProperty("byStar")] public Dictionary<string, List<string>> ByStar;
        }

        /// <summary>為該場依種子挑出地圖名（不含 CS 前置與副檔名）；失敗時回傳 null。</summary>
        public static string PickMapSlugForTier(int tier1To10, string questId, string monsterId)
        {
            var tier = Mathf.Clamp(tier1To10, 1, 10);
            EnsureLoaded();
            var picks = ResolveArray(tier);
            if (picks == null || picks.Length == 0)
                return null;

            // 改為完全隨機選取該星級的背景，讓每次進關都能體驗不同的美麗地圖
            var ix = UnityEngine.Random.Range(0, picks.Length);
            var s = picks[ix]?.Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }

        static string[] ResolveArray(int tier1To10)
        {
            if (_mapsByTier != null &&
                _mapsByTier.TryGetValue(tier1To10, out var a) &&
                a != null &&
                a.Length > 0)
                return a;
            var idx = Mathf.Clamp(tier1To10 - 1, 0, DefaultTierMaps.Length - 1);
            return DefaultTierMaps[idx];
        }

        static void EnsureLoaded()
        {
            if (_attemptedLoad) return;
            _attemptedLoad = true;
            _mapsByTier = new Dictionary<int, string[]>();

            if (!DesignDataReader.TryLoadDesignDataText(out var json, RelDir, RelFile))
                return;

            try
            {
                var dto = JsonConvert.DeserializeObject<FileDto>(json);
                if (dto?.ByStar == null)
                    return;

                foreach (var kv in dto.ByStar)
                {
                    if (kv.Key != null &&
                        kv.Key.StartsWith("_", System.StringComparison.Ordinal))
                        continue;

                    if (!int.TryParse(kv.Key.Trim(), out var star))
                        continue;
                    star = Mathf.Clamp(star, 1, 10);

                    var list = kv.Value == null ? new List<string>() : kv.Value;

                    var acc = new List<string>();

                    foreach (var x in list)
                    {
                        var sx = x?.Trim();
                        if (!string.IsNullOrEmpty(sx)) acc.Add(sx);
                    }

                    if (acc.Count > 0)
                        _mapsByTier[star] = acc.ToArray();
                }
            }
            catch (JsonException)
            {
                // 保留為空：ResolveArray 將用程式內預設
            }
        }

        static int StablePickIndex(string seed, int modulus)
        {
            if (modulus <= 0) return 0;
            unchecked
            {
                var h = 17;
                foreach (var ch in seed ?? "")
                    h = h * 31 + ch;
                h ^= h >> 15;
                h = (int)((uint)h * 2246822519u);
                h ^= h >> 13;
                return ((h & 0x7fffffff) % modulus + modulus) % modulus;
            }
        }
    }
}
