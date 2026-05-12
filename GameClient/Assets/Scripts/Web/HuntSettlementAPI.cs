using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MonsterHunter.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MonsterHunter.Web
{
    /// <summary>
    /// 呼叫 Supabase Edge Function <c>hunt_settlement</c>，將狩獵結算同步後端。
    /// URL 預設為 <c>{SupabaseUrl}/functions/v1/hunt_settlement</c>，亦可指定完整 Function URL。
    /// </summary>
    public sealed class HuntSettlementAPI : MonoBehaviour
    {
        public const string DefaultPlayerIdPlayerPrefsKey = "hunt_settlement_player_id";

        [Header("端點")]
        [Tooltip("若填寫，優先使用此完整 URL（例：https://xxx.supabase.co/functions/v1/hunt_settlement）。")]
        [SerializeField] string _explicitEdgeFunctionUrl;

        [Tooltip("未指定完整 URL 時，與 Function 名稱組出端點")]
        [SerializeField] SupabaseRuntimeConfig _config;

        [SerializeField] string _functionName = "hunt_settlement";

        [Header("驗證")]
        [Tooltip("填入 Supabase Dashboard → Project Settings → API → anon（公開）key；空白則嘗試從 SupabaseRuntimeConfig 讀取。")]
        [SerializeField] string _supabaseAnonKey;

        [Tooltip("後端若要求同時帶 apikey，則一併送出（與 anon key 相同值，常見於 Supabase Functions）。")]
        [SerializeField] bool _alsoSendApiKeyHeader = true;

        [Header("玩家 ID")]
        [Tooltip("非空白時固定使用此值；否則依序：PlayerPrefs、AuthSession.UserId、後援字串。")]
        [SerializeField] string _playerIdOverride;

        [SerializeField] string _playerIdPlayerPrefsKey = DefaultPlayerIdPlayerPrefsKey;

        [SerializeField] string _fallbackPlayerIdIfUnset = "local_dev_player";

        /// <summary>POST 結算；成功／失敗皆透過 callback 回傳。</summary>
        public void PostSettlement(
            HuntSettlementRequestPayload payload,
            Action<HuntSettlementResponse> onSuccess,
            Action<string> onError)
        {
            StartCoroutine(CoPostSettlement(payload, onSuccess, onError));
        }

        /// <summary>供協程鏈 <c>yield return</c> 使用。</summary>
        public IEnumerator PostSettlementAsync(
            HuntSettlementRequestPayload payload,
            Action<HuntSettlementResponse> onSuccess,
            Action<string> onError)
        {
            yield return CoPostSettlement(payload, onSuccess, onError);
        }

        IEnumerator CoPostSettlement(
            HuntSettlementRequestPayload payload,
            Action<HuntSettlementResponse> onSuccess,
            Action<string> onError)
        {
            AuthSession.EnsureLoaded();

            string url = ResolveEdgeFunctionUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                onError?.Invoke("[HuntSettlementAPI] 未設定 Edge Function URL，請填 _explicitEdgeFunctionUrl 或 SupabaseRuntimeConfig。");
                yield break;
            }

            string bearer = ResolveAnonKey();
            if (string.IsNullOrWhiteSpace(bearer))
            {
                onError?.Invoke("[HuntSettlementAPI] 未設定 Supabase anon key（Inspector 或 SupabaseRuntimeConfig）。");
                yield break;
            }

            if (payload == null)
            {
                onError?.Invoke("[HuntSettlementAPI] payload 為 null。");
                yield break;
            }

            payload.PlayerId ??= ResolvePlayerId();

            string jsonBody = JsonConvert.SerializeObject(
                payload,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + bearer);
                if (_alsoSendApiKeyHeader)
                    req.SetRequestHeader("apikey", bearer);

                yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                bool failed = req.result != UnityWebRequest.Result.Success;
#else
                bool failed = req.isNetworkError || req.isHttpError;
#endif
                string responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

                if (failed)
                {
                    string err = $"[HuntSettlementAPI] HTTP 失敗：{(int)req.responseCode} {req.error}\n{responseText}";
                    Debug.LogWarning(err);
                    onError?.Invoke(err);
                    yield break;
                }

                HuntSettlementResponse parsed = null;
                try
                {
                    parsed = JsonConvert.DeserializeObject<HuntSettlementResponse>(responseText);
                }
                catch (Exception e)
                {
                    string err = "[HuntSettlementAPI] 回應 JSON 解析失敗：" + e.Message + "\n" + responseText;
                    Debug.LogWarning(err);
                    onError?.Invoke(err);
                    yield break;
                }

                LogSettlementMaterials(parsed, responseText);
                onSuccess?.Invoke(parsed);
            }
        }

        string ResolveEdgeFunctionUrl()
        {
            if (!string.IsNullOrWhiteSpace(_explicitEdgeFunctionUrl))
                return _explicitEdgeFunctionUrl.Trim().TrimEnd('/');

            var cfg = SupabaseRuntimeConfig.ResolveFor(_config);
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SupabaseUrl))
                return null;

            string baseUrl = cfg.SupabaseUrl.Trim().TrimEnd('/');
            string fn = string.IsNullOrWhiteSpace(_functionName) ? "hunt_settlement" : _functionName.Trim();
            return $"{baseUrl}/functions/v1/{fn}";
        }

        string ResolveAnonKey()
        {
            if (!string.IsNullOrWhiteSpace(_supabaseAnonKey))
                return _supabaseAnonKey.Trim();

            var cfg = SupabaseRuntimeConfig.ResolveFor(_config);
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.SupabaseAnonKey))
                return cfg.SupabaseAnonKey.Trim();

            cfg = SupabaseRuntimeConfig.LoadFromResources();
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.SupabaseAnonKey))
                return cfg.SupabaseAnonKey.Trim();

            return null;
        }

        string ResolvePlayerId()
        {
            if (!string.IsNullOrWhiteSpace(_playerIdOverride))
                return _playerIdOverride.Trim();

            string key = string.IsNullOrWhiteSpace(_playerIdPlayerPrefsKey)
                ? DefaultPlayerIdPlayerPrefsKey
                : _playerIdPlayerPrefsKey.Trim();
            if (PlayerPrefs.HasKey(key))
            {
                string v = PlayerPrefs.GetString(key, null);
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }

            if (AuthSession.IsSignedIn && !string.IsNullOrWhiteSpace(AuthSession.UserId))
                return AuthSession.UserId;

            return _fallbackPlayerIdIfUnset;
        }

        static void LogSettlementMaterials(HuntSettlementResponse parsed, string rawJson)
        {
            if (parsed == null)
            {
                Debug.Log("[HuntSettlementAPI] 結算回應（無解析物件）：\n" + rawJson);
                return;
            }

            Debug.Log($"[HuntSettlementAPI] 結算回應 ok={parsed.Ok} message={parsed.Message}");

            IReadOnlyList<SettlementMaterialEntry> list = parsed.Materials;
            if (list == null || list.Count == 0)
                list = parsed.Rewards;

            if (list != null && list.Count > 0)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    SettlementMaterialEntry m = list[i];
                    if (m == null)
                        continue;
                    string id = !string.IsNullOrEmpty(m.ItemId) ? m.ItemId : m.MaterialId;
                    string namePart = string.IsNullOrEmpty(m.Name) ? "" : $" ({m.Name})";
                    Debug.Log($"[HuntSettlementAPI] 結算素材 [{i + 1}/{list.Count}] {id}{namePart} × {m.Quantity}");
                }
            }
            else
            {
                try
                {
                    JObject jo = JObject.Parse(rawJson);
                    JToken mats = jo["materials"] ?? jo["rewards"] ?? jo["drops"];
                    if (mats != null && mats.Type != JTokenType.Null)
                        Debug.Log("[HuntSettlementAPI] 結算素材（原始 JSON 節點）：\n" + mats.ToString(Formatting.Indented));
                    else
                        Debug.Log("[HuntSettlementAPI] 後端未回傳 materials／rewards 陣列，完整內容：\n" + rawJson);
                }
                catch
                {
                    Debug.Log("[HuntSettlementAPI] 完整回應內容：\n" + rawJson);
                }
            }
        }
    }

    /// <summary>POST body，欄位名稱對應 JSON snake_case 供 Edge Function 解析。</summary>
    [Serializable]
    public class HuntSettlementRequestPayload
    {
        [JsonProperty("player_id")]
        public string PlayerId;

        [JsonProperty("monster_id")]
        public string MonsterId;

        [JsonProperty("battle_duration_seconds")]
        public float BattleDurationSeconds;

        [JsonProperty("consumed_items")]
        public List<ConsumedItemEntry> ConsumedItems = new List<ConsumedItemEntry>();

        /// <summary>本場達成的掉落條件（與 drop_rates「掉落條件」欄位一致）；未送時後端預設僅「基本擊殺」。</summary>
        [JsonProperty("fulfilled_drop_conditions")]
        public List<string> FulfilledDropConditions;
    }

    [Serializable]
    public class ConsumedItemEntry
    {
        [JsonProperty("item_id")]
        public string ItemId;

        [JsonProperty("quantity")]
        public int Quantity;
    }

    /// <summary>Edge Function <c>hunt_settlement</c> 回應；<c>materials</c>／<c>rewards</c> 為結算掉落列表（內容相同時擇一讀取即可）。</summary>
    [Serializable]
    public class HuntSettlementResponse
    {
        [JsonProperty("ok")]
        public bool? Ok;

        [JsonProperty("message")]
        public string Message;

        [JsonProperty("materials")]
        public List<SettlementMaterialEntry> Materials;

        [JsonProperty("rewards")]
        public List<SettlementMaterialEntry> Rewards;
    }

    [Serializable]
    public class SettlementMaterialEntry
    {
        [JsonProperty("item_id")]
        public string ItemId;

        [JsonProperty("material_id")]
        public string MaterialId;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("quantity")]
        public int Quantity;
    }
}
