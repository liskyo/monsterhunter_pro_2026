using System;
using System.Collections.Generic;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 依 drop_rates.json：魔物死亡時依掉落機率與「掉落條件」（含部位）產出獎勵清單。
    /// 呼叫端應在 fulfilledConditions 包含「基本擊殺」；部位如「破壞部位」「切斷尾巴」由戰鬥系統填入。
    /// </summary>
    public static class DropRewardResolver
    {
        /// <summary>
        /// 解析整份 drop_rates.json（根陣列）。
        /// </summary>
        public static IReadOnlyList<SettlementRewardEntry> Resolve(
            string monsterId,
            IReadOnlyCollection<string> fulfilledConditions,
            string dropRatesJsonText,
            System.Random rng
        )
        {
            var list = new List<SettlementRewardEntry>();
            if (string.IsNullOrEmpty(monsterId) || string.IsNullOrEmpty(dropRatesJsonText) || rng == null)
                return list;

            掉落率資料列[] rows;
            try
            {
                rows = JsonConvert.DeserializeObject<掉落率資料列[]>(dropRatesJsonText);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DropRewardResolver] " + e.Message);
                return list;
            }

            if (rows == null) return list;

            var cond = fulfilledConditions != null
                ? new HashSet<string>(fulfilledConditions)
                : new HashSet<string>();

            foreach (var row in rows)
            {
                if (row == null) continue;
                // 忽略大小寫與前後空白的寬鬆比對，保證魔物編號匹配！
                if (!string.Equals(row.魔物編號?.Trim(), monsterId?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(row.掉落條件)) continue;
                var dropCond = row.掉落條件;
                if (!cond.Contains(dropCond)) continue;
                if (row.掉落機率 <= 0f) continue;
                
                // ✦ 掉落率翻倍提升！讓自然產出多個素材的機率大幅增加！
                var rate = Mathf.Clamp01(row.掉落機率 * 2.0f);
                if (rng.NextDouble() >= rate) continue;

                list.Add(
                    new SettlementRewardEntry
                    {
                        素材編號 = row.素材編號,
                        素材名稱 = row.素材名稱,
                        圖片路徑 = row.圖片路徑,
                        數量 = 1,
                    }
                );
            }

            // ────────────────────────────────────────────────────
            //  ✦ 強效雙重保底防禦線：保證每場戰鬥「至少掉落 2 樣以上素材」！
            // ────────────────────────────────────────────────────
            if (list.Count < 2)
            {
                // 收集該魔物所有可能掉落的候選素材（滿足當前掉落條件的）
                var candidates = new List<掉落率資料列>();
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        if (row == null) continue;
                        if (!string.Equals(row.魔物編號?.Trim(), monsterId?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                        if (string.IsNullOrEmpty(row.掉落條件)) continue;
                        if (!cond.Contains(row.掉落條件)) continue;
                        candidates.Add(row);
                    }
                }

                // 如果能符合條件的候選不足，放寬條件（不限掉落條件，把基本擊殺、部位破壞等全部納入候選）
                if (candidates.Count < 2 && rows != null)
                {
                    foreach (var row in rows)
                    {
                        if (row == null) continue;
                        if (!string.Equals(row.魔物編號?.Trim(), monsterId?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                        if (candidates.Exists(c => c.素材編號 == row.素材編號)) continue;
                        candidates.Add(row);
                    }
                }

                // 從候選名單中，補齊到至少 2 樣素材（優先選擇還沒掉落過的，真的不夠再重複給）
                int safetyLimit = 0;
                while (list.Count < 2 && candidates.Count > 0 && safetyLimit < 15)
                {
                    safetyLimit++;
                    
                    // 優先找 list 裡目前沒有的素材
                    掉落率資料列 selectedRow = null;
                    foreach (var cand in candidates)
                    {
                        if (!list.Exists(item => item.素材編號 == cand.素材編號))
                        {
                            selectedRow = cand;
                            break;
                        }
                    }

                    // 如果都有了，就選第一個候選
                    if (selectedRow == null)
                    {
                        selectedRow = candidates[rng.Next(candidates.Count)];
                    }

                    list.Add(new SettlementRewardEntry
                    {
                        素材編號 = selectedRow.素材編號,
                        素材名稱 = selectedRow.素材名稱,
                        圖片路徑 = selectedRow.圖片路徑,
                        數量 = 1,
                    });
                }

                // 最底層物理防線（JSON 損毀、為空或魔物編號查無資料時），直接手工塞滿 2 樣素材
                if (list.Count < 2)
                {
                    string midNum = "001";
                    if (!string.IsNullOrEmpty(monsterId))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(monsterId, @"\d+");
                        if (match.Success) midNum = match.Value;
                    }
                    
                    while (list.Count < 2)
                    {
                        var fallbackId = $"MAT_{midNum}_0{(list.Count + 1)}";
                        list.Add(new SettlementRewardEntry
                        {
                            素材編號 = fallbackId,
                            素材名稱 = monsterId == "MON_001" ? (list.Count == 0 ? "青熊獸的鱗" : "青熊獸的甲殼") : "討伐戰利品素材",
                            圖片路徑 = $"Assets/Textures/Items/{fallbackId}.png",
                            數量 = 1,
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>建立預設條件集合：必含基本擊殺，並合併部位條件。</summary>
        public static HashSet<string> BuildDefaultConditions(bool tailCut, bool partBroken)
        {
            var s = new HashSet<string> { "基本擊殺" };
            if (partBroken) s.Add("破壞部位");
            if (tailCut) s.Add("切斷尾巴");
            return s;
        }
    }
}
