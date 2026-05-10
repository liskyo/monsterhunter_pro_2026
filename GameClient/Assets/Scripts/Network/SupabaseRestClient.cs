using System;
using System.Collections;
using System.Text;
using MonsterHunter.Core;
using UnityEngine.Networking;

namespace MonsterHunter.Network
{
    /// <summary>
    /// Supabase Auth + PostgREST（anon key + 使用者 access_token）。
    /// </summary>
    public sealed class SupabaseRestClient
    {
        readonly string _restUrl;
        readonly string _authUrl;
        readonly string _anonKey;

        public SupabaseRestClient(string supabaseUrl, string anonKey)
        {
            supabaseUrl = supabaseUrl?.TrimEnd('/') ?? "";
            _anonKey = anonKey ?? "";
            _restUrl = $"{supabaseUrl}/rest/v1";
            _authUrl = $"{supabaseUrl}/auth/v1";
        }

        public IEnumerator SignUpEmailPassword(string email, string password, Action<string> onError, Action<string> onOkRawJson)
        {
            var url = $"{_authUrl}/signup";
            var body = $"{{\"email\":\"{Escape(email)}\",\"password\":\"{Escape(password)}\"}}";
            yield return PostJson(url, body, null, onError, onOkRawJson);
        }

        public IEnumerator SignInEmailPassword(string email, string password, Action<string> onError, Action<string> onOkRawJson)
        {
            var url = $"{_authUrl}/token?grant_type=password";
            var body = $"{{\"email\":\"{Escape(email)}\",\"password\":\"{Escape(password)}\"}}";
            yield return PostJson(url, body, null, onError, onOkRawJson);
        }

        public IEnumerator Rpc(string rpcName, string jsonBody, string accessToken, Action<string> onError, Action<string> onOkRawJson)
        {
            var url = $"{_restUrl}/rpc/{rpcName}";
            yield return PostJson(url, jsonBody ?? "{}", accessToken, onError, onOkRawJson);
        }

        /// <summary>PostgREST Upsert：需資料表有 UNIQUE 約束配合 Prefer merge。</summary>
        public IEnumerator PostMerge(string pathAfterV1, string jsonArrayBody, string accessToken, Action<string> onError, Action onOk)
        {
            var url = $"{_restUrl}/{pathAfterV1.TrimStart('/')}";
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(jsonArrayBody ?? "[]");
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Prefer", "resolution=merge-duplicates");
                req.SetRequestHeader("apikey", _anonKey);
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onError?.Invoke(req.error + "\n" + req.downloadHandler.text);
                    yield break;
                }
                onOk?.Invoke();
            }
        }

        public IEnumerator GetJson(string pathAfterV1, string accessToken, Action<string> onError, Action<string> onOkRawJson)
        {
            var url = $"{_restUrl}/{pathAfterV1.TrimStart('/')}";
            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("apikey", _anonKey);
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                req.SetRequestHeader("Accept", "application/json");
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onError?.Invoke(req.error + "\n" + req.downloadHandler.text);
                    yield break;
                }
                onOkRawJson?.Invoke(req.downloadHandler.text);
            }
        }

        public IEnumerator PatchJson(string pathAfterV1, string jsonObjectBody, string accessToken, Action<string> onError, Action onOk)
        {
            var url = $"{_restUrl}/{pathAfterV1.TrimStart('/')}";
            using (var req = new UnityWebRequest(url, "PATCH"))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(jsonObjectBody ?? "{}");
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Prefer", "return=minimal");
                req.SetRequestHeader("apikey", _anonKey);
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onError?.Invoke(req.error + "\n" + req.downloadHandler.text);
                    yield break;
                }
                onOk?.Invoke();
            }
        }

        public IEnumerator Delete(string pathAfterV1, string accessToken, Action<string> onError, Action onOk)
        {
            var url = $"{_restUrl}/{pathAfterV1.TrimStart('/')}";
            using (var req = UnityWebRequest.Delete(url))
            {
                req.SetRequestHeader("apikey", _anonKey);
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onError?.Invoke(req.error + "\n" + req.downloadHandler.text);
                    yield break;
                }
                onOk?.Invoke();
            }
        }

        IEnumerator PostJson(string url, string jsonBody, string accessToken, Action<string> onError, Action<string> onOkRawJson)
        {
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", _anonKey);
                if (!string.IsNullOrEmpty(accessToken))
                    req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onError?.Invoke(req.error + "\n" + req.downloadHandler.text);
                    yield break;
                }
                onOkRawJson?.Invoke(req.downloadHandler.text);
            }
        }

        static string Escape(string s) => SupabaseJsonMini.EscapeJsonString(s);

        /// <summary>解析登入／註冊成功 JSON，寫入 AuthSession。</summary>
        public static bool TryApplyAuthResponse(string json, out string error)
        {
            error = null;
            if (!SupabaseJsonMini.TryGetString(json, "access_token", out var access))
            {
                error = "回應缺少 access_token";
                return false;
            }
            SupabaseJsonMini.TryGetString(json, "refresh_token", out var refresh);
            if (!SupabaseJsonMini.TryGetUserId(json, out var uid))
            {
                error = "回應缺少 user.id";
                return false;
            }
            AuthSession.SetSession(access, refresh ?? "", uid);
            return true;
        }
    }
}
