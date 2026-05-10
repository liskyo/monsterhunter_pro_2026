using System;
using System.Collections;
using MonsterHunter.Core;
using UnityEngine;

namespace MonsterHunter.Network
{
    /// <summary>
    /// Host–Client 配對入口：透過 Supabase RPC 建立／加入 Join Code 房間。
    /// 實際 P2P／Relay 連線請在取得 host_user_id／guest_user_id 後另行實作。
    /// </summary>
    public sealed class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [SerializeField] SupabaseRuntimeConfig _config;

        SupabaseRestClient _client;

        public string LastJoinCode { get; private set; }
        public string LastLobbyJson { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            AuthSession.EnsureLoaded();
        }

        void Start()
        {
            if (_config == null)
                Debug.LogWarning("[NetworkManager] 請在 Inspector 指定 SupabaseRuntimeConfig（Resources 或欄位指派）。");
            else
                _client = new SupabaseRestClient(_config.SupabaseUrl, _config.SupabaseAnonKey);
        }

        SupabaseRestClient Client =>
            _client ??= _config != null
                ? new SupabaseRestClient(_config.SupabaseUrl, _config.SupabaseAnonKey)
                : null;

        /// <summary>Host：建立房間，回傳 6 碼 Join Code（Coroutine）。需已登入。</summary>
        public IEnumerator CreateJoinLobby(int ttlMinutes, Action<string> onError, Action<string> onJoinCode)
        {
            if (!AuthSession.IsSignedIn)
            {
                onError?.Invoke("尚未登入");
                yield break;
            }
            if (Client == null)
            {
                onError?.Invoke("Supabase 設定缺失");
                yield break;
            }

            var body = $"{{\"p_ttl_minutes\":{Mathf.Clamp(ttlMinutes, 1, 1440)}}}";
            string err = null;
            string raw = null;
            yield return Client.Rpc("create_join_lobby", body, AuthSession.AccessToken, e => err = e, j => raw = j);
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            raw = raw?.Trim().Trim('"');
            LastJoinCode = raw;
            onJoinCode?.Invoke(raw);
        }

        /// <summary>Guest：依代碼預覽房間是否存在。</summary>
        public IEnumerator PeekLobby(string joinCode, Action<string> onError, Action<bool> onAvailable)
        {
            if (!AuthSession.IsSignedIn)
            {
                onError?.Invoke("尚未登入");
                yield break;
            }
            if (Client == null)
            {
                onError?.Invoke("Supabase 設定缺失");
                yield break;
            }

            var body = $"{{\"p_code\":\"{SupabaseJsonMini.EscapeJsonString(joinCode)}\"}}";
            string err = null;
            string raw = null;
            yield return Client.Rpc("peek_open_lobby_by_code", body, AuthSession.AccessToken, e => err = e, j => raw = j);
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            var t = raw?.Trim() ?? "";
            var ok = t.Length > 2 && t.StartsWith("[") && !t.StartsWith("[]");
            onAvailable?.Invoke(ok);
        }

        /// <summary>Guest：加入房間；成功後 LastLobbyJson 為 RPC 回傳列。</summary>
        public IEnumerator JoinLobby(string joinCode, Action<string> onError, Action onOk)
        {
            if (!AuthSession.IsSignedIn)
            {
                onError?.Invoke("尚未登入");
                yield break;
            }
            if (Client == null)
            {
                onError?.Invoke("Supabase 設定缺失");
                yield break;
            }

            var body = $"{{\"p_code\":\"{SupabaseJsonMini.EscapeJsonString(joinCode)}\"}}";
            string err = null;
            string raw = null;
            yield return Client.Rpc("join_lobby_by_code", body, AuthSession.AccessToken, e => err = e, j => raw = j);
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            LastLobbyJson = raw;
            onOk?.Invoke();
        }

    }
}
