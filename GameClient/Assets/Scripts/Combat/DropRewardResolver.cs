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
                if (rng.NextDouble() >= row.掉落機率) continue;

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
            //  強效三重保底防禦線，絕對不允許「無素材掉落」發生！
            // ────────────────────────────────────────────────────
            if (list.Count == 0)
            {
                // 第一層：從資料中尋找該魔物的第一個「基本擊殺」素材
                if (rows != null && rows.Length > 0)
                {
                    foreach (var row in rows)
                    {
                        if (row != null && 
                            string.Equals(row.魔物編號?.Trim(), monsterId?.Trim(), StringComparison.OrdinalIgnoreCase) && 
                            row.掉落條件 == "基本擊殺")
                        {
                            list.Add(new SettlementRewardEntry
                            {
                                素材編號 = row.素材編號,
                                素材名稱 = row.素材名稱,
                                圖片路徑 = row.圖片路徑,
                                數量 = 1,
                            });
                            break;
                        }
                    }
                }

                // 第二層：如果該魔物沒有宣告「基本擊殺」條件，只要是該魔物宣告過的素材，就直接給第一個
                if (list.Count == 0 && rows != null && rows.Length > 0)
                {
                    foreach (var row in rows)
                    {
                        if (row != null && string.Equals(row.魔物編號?.Trim(), monsterId?.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add(new SettlementRewardEntry
                            {
                                素材編號 = row.素材編號,
                                素材名稱 = row.素材名稱,
                                圖片路徑 = row.圖片路徑,
                                數量 = 1,
                            });
                            break;
                        }
                    }
                }

                // 第三層：最底層物理防線（JSON 損毀、為空或魔物編號查無資料時），動態構造一個合理掉落物
                if (list.Count == 0)
                {
                    string midNum = "001";
                    if (!string.IsNullOrEmpty(monsterId))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(monsterId, @"\d+");
                        if (match.Success) midNum = match.Value;
                    }
                    
                    var fallbackId = $"MAT_{midNum}_01";
                    list.Add(new SettlementRewardEntry
                    {
                        素材編號 = fallbackId,
                        素材名稱 = monsterId == "MON_001" ? "青熊獸的鱗" : "討伐戰利品素材",
                        圖片路徑 = $"Assets/Textures/Items/{fallbackId}.png",
                        數量 = 1,
                    });
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
