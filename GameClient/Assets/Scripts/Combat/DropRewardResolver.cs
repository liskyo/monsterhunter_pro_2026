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
                if (row == null || row.魔物編號 != monsterId) continue;
                if (string.IsNullOrEmpty(row.掉落條件)) continue;
                if (!cond.Contains(row.掉落條件)) continue;
                if (row.掉落機率 <= 0f) continue;
                if (rng.NextDouble() >= row.掉落機率) continue;

                list.Add(
                    new SettlementRewardEntry
                    {
                        素材編號 = row.素材編號,
                        素材名稱 = row.素材名稱,
                        數量 = 1,
                    }
                );
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
