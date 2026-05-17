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
            string jsonText = null;
            if (_dropRatesJson != null && !string.IsNullOrEmpty(_dropRatesJson.text))
            {
                jsonText = _dropRatesJson.text;
            }
            else
            {
                // 嘗試從本機路徑或 Resources 載入作為安全備份，保證 100% 能加載！
                string[] fallbackPaths = new[]
                {
                    System.IO.Path.Combine(Application.dataPath, "../DesignData/01_Monsters/drop_rates.json"),
                    System.IO.Path.Combine(Application.dataPath, "DesignData/01_Monsters/drop_rates.json"),
                    System.IO.Path.Combine(Application.dataPath, "Resources/DesignData/01_Monsters/drop_rates.json")
                };
                foreach (var path in fallbackPaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        try
                        {
                            jsonText = System.IO.File.ReadAllText(path);
                            Debug.Log($"[HuntSettlementService] 成功從本機備用路徑載入 drop_rates.json: {path}");
                            break;
                        }
                        catch {}
                    }
                }
            }

            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[HuntSettlementService] 無法以任何方式加載 drop_rates.json。使用空 JSON 開始解析保底。");
                jsonText = "[]";
            }

            var rewards = DropRewardResolver.Resolve(
                monsterId,
                fulfilledDropConditions,
                jsonText,
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
                        // 絕對不能 yield break！即便網路或資料庫異常，依然要在本機與 UI 結算掉落物！
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
                    // 絕對不能 yield break！
                }

                // 即使雲端存檔失敗，仍要寫入本機備份，並呼叫 onComplete 讓 UI 顯示掉落畫面
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
            ledger.ClearActiveQuestAfterComplete();
        }
    }
}
