using System;
using System.Collections;
using MonsterHunter.Network;
using UnityEngine;

namespace MonsterHunter.Core
{
    /// <summary>電子郵件／密碼註冊與登入；成功後寫入 AuthSession。</summary>
    public sealed class EmailPasswordAuth : MonoBehaviour
    {
        [SerializeField] SupabaseRuntimeConfig _config;

        SupabaseRestClient _client;

        void Awake()
        {
            _config = SupabaseRuntimeConfig.ResolveFor(_config);
            if (_config != null)
                _client = new SupabaseRestClient(_config.SupabaseUrl, _config.SupabaseAnonKey);
        }

        public IEnumerator SignUp(string email, string password, Action<string> onError, Action onOk)
        {
            if (_client == null)
            {
                onError?.Invoke("缺少 SupabaseRuntimeConfig");
                yield break;
            }

            string err = null;
            string raw = null;
            yield return _client.SignUpEmailPassword(email, password, e => err = e, r => raw = r);
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            if (!SupabaseRestClient.TryApplyAuthResponse(raw, out var parseErr))
            {
                onError?.Invoke(parseErr + "（若專案開啟信箱驗證，請改由登入流程完成）");
                yield break;
            }

            onOk?.Invoke();
        }

        public IEnumerator SignIn(string email, string password, Action<string> onError, Action onOk)
        {
            if (_client == null)
            {
                onError?.Invoke("缺少 SupabaseRuntimeConfig");
                yield break;
            }

            string err = null;
            string raw = null;
            yield return _client.SignInEmailPassword(email, password, e => err = e, r => raw = r);
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            if (!SupabaseRestClient.TryApplyAuthResponse(raw, out var parseErr))
            {
                onError?.Invoke(parseErr);
                yield break;
            }

            onOk?.Invoke();
        }

        public void SignOut() => AuthSession.Clear();
    }
}
