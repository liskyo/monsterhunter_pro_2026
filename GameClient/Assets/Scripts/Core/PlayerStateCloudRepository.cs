using System;
using System.Collections;
using MonsterHunter.Network;
using UnityEngine;

namespace MonsterHunter.Core
{
    /// <summary>
    /// 雲端 PlayerState：daily_hunt_records 等。owner_id 由 RLS 強制等於 auth.uid()。
    /// 請勿再依賴 PlayerState/*.json 作為執行時資料來源。
    /// </summary>
    public sealed class PlayerStateCloudRepository : MonoBehaviour
    {
        [SerializeField] SupabaseRuntimeConfig _config;

        SupabaseRestClient _client;

        void Awake()
        {
            _config = SupabaseRuntimeConfig.ResolveFor(_config);
            if (_config != null)
                _client = new SupabaseRestClient(_config.SupabaseUrl, _config.SupabaseAnonKey);
        }

        /// <summary>
        /// 透過 RPC upsert（複合唯一鍵）；兩位玩家各自登入後 auth.uid() 不同，挑戰次數分開計算。
        /// </summary>
        public IEnumerator UpsertDailyHuntRecord(
            DateTime recordLocalDate,
            string dailyQuestId,
            int completionCount,
            bool completed,
            bool rewardsClaimed,
            Action<string> onError,
            Action onOk
        )
        {
            if (!AuthSession.IsSignedIn)
            {
                onError?.Invoke("尚未登入");
                yield break;
            }

            if (_client == null)
            {
                onError?.Invoke("缺少 SupabaseRuntimeConfig");
                yield break;
            }

            var d = recordLocalDate.ToString("yyyy-MM-dd");
            var qid = SupabaseJsonMini.EscapeJsonString(dailyQuestId ?? "");
            var json =
                "{\"p_record_date\":\""
                + d
                + "\",\"p_daily_quest_id\":\""
                + qid
                + "\",\"p_completion_count\":"
                + completionCount
                + ",\"p_completed\":"
                + (completed ? "true" : "false")
                + ",\"p_rewards_claimed\":"
                + (rewardsClaimed ? "true" : "false")
                + "}";

            string err = null;
            string raw = null;
            yield return _client.Rpc(
                "upsert_daily_hunt_record",
                json,
                AuthSession.AccessToken,
                e => err = e,
                r => raw = r
            );
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            _ = raw;
            onOk?.Invoke();
        }

        public IEnumerator FetchMyProfileJson(Action<string> onError, Action<string> onRawJson)
        {
            if (!AuthSession.IsSignedIn)
            {
                onError?.Invoke("尚未登入");
                yield break;
            }

            if (_client == null)
            {
                onError?.Invoke("缺少 SupabaseRuntimeConfig");
                yield break;
            }

            var uid = AuthSession.UserId;
            var path = $"profiles?id=eq.{uid}&select=*";
            yield return _client.GetJson(path, AuthSession.AccessToken, onError, onRawJson);
        }
    }
}
