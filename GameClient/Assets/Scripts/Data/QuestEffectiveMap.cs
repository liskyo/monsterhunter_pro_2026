using System.Collections.Generic;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Data
{
    /// <summary>
    /// 將 <see cref="任務資料列"/> 的「地圖」欄轉為戰鬥遠景用名稱；若 <see cref="任務資料列.地圖抽取"/> 為「依星級隨機」，
    /// 則依 <see cref="任務資料列.星級"/> 從 <c>DesignData/05_Systems/quest_map_pools_by_star.json</c> 擇一（同一場內穩定，由任務＋魔物鍵種子）。
    /// </summary>
    public static class QuestEffectiveMap
    {
        const string PoolsPath1 = "05_Systems";
        const string PoolsPath2 = "quest_map_pools_by_star.json";

        static readonly string[] DefaultPool =
        {
            "古代樹森林",
            "龍結晶之地",
            "塔之秘境",
            "瘴氣之谷",
            "大蟻塚荒地",
            "陸珊瑚台地",
            "地熱火山",
            "冰封群島",
        };

        static Dictionary<int, string[]> _poolsByStar;
        static bool _loadAttempted;

        sealed class PoolsFileDto
        {
            [JsonProperty("pools")] public Dictionary<string, List<string>> Pools;
        }

        /// <summary>
        /// 取得本場使用的地圖名（對應 <c>{地名}_背景.png</c>）。非隨機模式時回傳 <see cref="任務資料列.地圖"/>。
        /// </summary>
        public static string GetBattleMapForQuest(任務資料列 quest,
            string monsterIdForStableMapDice,
            string fallbackWhenUnresolved)
        {
            if (quest == null)
                return string.IsNullOrWhiteSpace(fallbackWhenUnresolved)
                    ? null
                    : fallbackWhenUnresolved.Trim();

            var fixedMap = quest.地圖?.Trim();
            if (!IsRandomByStar(quest))
            {
                if (!string.IsNullOrEmpty(fixedMap)) return fixedMap;
                return string.IsNullOrWhiteSpace(fallbackWhenUnresolved)
                    ? null
                    : fallbackWhenUnresolved.Trim();
            }

            EnsurePoolsLoaded();
            var tier = Mathf.Clamp(quest.星級, 1, 10);
            var picks = GetPoolArray(tier);
            if (picks.Length == 0 && !string.IsNullOrEmpty(fixedMap))
                return fixedMap;
            if (picks.Length == 0)
                return string.IsNullOrWhiteSpace(fallbackWhenUnresolved)
                    ? null
                    : fallbackWhenUnresolved.Trim();

            var seed =
                $"{quest.任務編號}\u241F{tier}\u241F{(monsterIdForStableMapDice ?? "").Trim()}";
            var ix = StablePickIndex(seed, picks.Length);
            var chosen = picks[ix]?.Trim();
            if (string.IsNullOrEmpty(chosen) && !string.IsNullOrEmpty(fixedMap))
                return fixedMap;
            return string.IsNullOrEmpty(chosen)
                ? (string.IsNullOrWhiteSpace(fallbackWhenUnresolved)
                    ? null
                    : fallbackWhenUnresolved.Trim())
                : chosen;
        }

        public static bool IsRandomByStar(任務資料列 quest)
        {
            if (quest == null) return false;
            var m = quest.地圖抽取?.Trim();
            return string.Equals(m, "依星級隨機", System.StringComparison.Ordinal);
        }

        static string[] GetPoolArray(int tier1To10)
        {
            if (_poolsByStar == null)
                return DefaultPool;
            if (_poolsByStar.TryGetValue(tier1To10, out var a) &&
                a != null &&
                a.Length > 0)
                return a;
            for (var t = tier1To10; t >= 1; t--)
            {
                if (_poolsByStar.TryGetValue(t, out a) &&
                    a != null &&
                    a.Length > 0)
                    return a;
            }

            return DefaultPool;
        }

        static void EnsurePoolsLoaded()
        {
            if (_loadAttempted) return;
            _loadAttempted = true;
            _poolsByStar = new Dictionary<int, string[]>();
            if (!DesignDataReader.TryLoadDesignDataText(out var json, PoolsPath1, PoolsPath2))
            {
                Debug.LogWarning($"[QuestEffectiveMap] 找不到 {PoolsPath1}/{PoolsPath2}，使用預設地圖池。");
                return;
            }

            try
            {
                var dto = JsonConvert.DeserializeObject<PoolsFileDto>(json);
                if (dto?.Pools == null) return;

                foreach (var kv in dto.Pools)
                {
                    if (kv.Key != null &&
                        kv.Key.StartsWith("_", System.StringComparison.Ordinal))
                        continue;
                    if (!int.TryParse(kv.Key.Trim(), out var star))
                        continue;
                    star = Mathf.Clamp(star, 1, 10);
                    if (kv.Value == null || kv.Value.Count == 0) continue;
                    var lst = new List<string>();
                    foreach (var x in kv.Value)
                    {
                        var s = x?.Trim();
                        if (!string.IsNullOrEmpty(s)) lst.Add(s);
                    }

                    if (lst.Count > 0)
                        _poolsByStar[star] = lst.ToArray();
                }
            }
            catch (JsonException e)
            {
                Debug.LogWarning($"[QuestEffectiveMap] 解析 pools 失敗：{e.Message}");
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
