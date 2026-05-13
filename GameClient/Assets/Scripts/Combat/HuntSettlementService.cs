using System;
using System.Collections;
using System.Collections.Generic;
using MonsterHunter.Network;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 戰鬥結算：掉落 → 倉庫；圖鑑擊殺 + 當日 SESSION（daily_hunt_records.__SESSION__）與第 3 擊難度倍率（RPC）。
    /// Supabase 未設定時：<see cref="LocalHunterLedger"/> 仍可累積素材。
    /// </summary>
    public sealed class HuntSettlementService : MonoBehaviour
    {
        [SerializeField] SupabaseService _supabase;
        [SerializeField] TextAsset _dropRatesJson;

        readonly System.Random _rng = new System.Random();

        /// <param name="localDate">玩家裝置當日日期（與 daily_hunt record_date 一致）。</param>
        public IEnumerator RunKillSettlement(
            string monsterId,
            string monsterName,
            IReadOnlyCollection<string> fulfilledDropConditions,
            DateTime localDate,
            Action<string> onError,
            Action<IReadOnlyList<SettlementRewardEntry>, int, float> onComplete
        )
        {
            if (_dropRatesJson == null)
            {
                onError?.Invoke("未指定 drop_rates.json TextAsset。");
                yield break;
            }

            var rewards = DropRewardResolver.Resolve(
                monsterId,
                fulfilledDropConditions,
                _dropRatesJson.text,
                _rng
            );

            if (_supabase != null)
            {
                foreach (var r in rewards)
                {
                    string err = null;
                    yield return _supabase.UpdateWarehouse(r.素材編號, r.數量, e => err = e, () => { });
                    if (err != null)
                    {
                        onError?.Invoke($"倉庫寫入失敗 {r.素材編號}: {err}");
                        yield break;
                    }
                }

                string metaErr = null;
                var kills   = 0;
                var diffMul = 1f;

                yield return _supabase.PostMonsterKillSession(
                    localDate,
                    monsterId,
                    monsterName,
                    e => metaErr = e,
                    (k, d) =>
                    {
                        kills   = k;
                        diffMul = d;
                    });

                if (metaErr != null)
                {
                    onError?.Invoke(metaErr);
                    yield break;
                }

                PersistLocalWarehouse(rewards);
                onComplete?.Invoke(rewards, kills, diffMul);
                yield break;
            }

            Debug.LogWarning(
                "[HuntSettlementService] 未指定 SupabaseService：解析掉落並寫入本機 mh_local_hunter_ledger.json。");

            PersistLocalWarehouse(rewards);
            onComplete?.Invoke(rewards, 0, 1f);
        }

        static void PersistLocalWarehouse(IEnumerable<SettlementRewardEntry> rewards)
        {
            var ledger = LocalHunterLedger.LoadOrCreate();
            ledger.MergeSettlementRewards(rewards);
        }
    }
}
