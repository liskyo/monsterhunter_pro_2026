using System;
using UnityEngine;

namespace MonsterHunter.Core
{
    /// <summary>
    /// 保存登入後的 JWT 與使用者 id（PlayerPrefs）。正式版請改加密儲存或使用平台金鑰庫。
    /// </summary>
    public static class AuthSession
    {
        const string KAccess = "mh_access_token";
        const string KRefresh = "mh_refresh_token";
        const string KUserId = "mh_user_id";

        public static string AccessToken { get; private set; }
        public static string RefreshToken { get; private set; }
        public static string UserId { get; private set; }

        public static bool IsSignedIn => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(UserId);

        static AuthSession()
        {
            LoadFromDisk();
        }

        public static void SetSession(string accessToken, string refreshToken, string userId)
        {
            AccessToken = accessToken ?? "";
            RefreshToken = refreshToken ?? "";
            UserId = userId ?? "";
            PlayerPrefs.SetString(KAccess, AccessToken);
            PlayerPrefs.SetString(KRefresh, RefreshToken);
            PlayerPrefs.SetString(KUserId, UserId);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            AccessToken = "";
            RefreshToken = "";
            UserId = "";
            PlayerPrefs.DeleteKey(KAccess);
            PlayerPrefs.DeleteKey(KRefresh);
            PlayerPrefs.DeleteKey(KUserId);
            PlayerPrefs.Save();
        }

        static void LoadFromDisk()
        {
            AccessToken = PlayerPrefs.GetString(KAccess, "");
            RefreshToken = PlayerPrefs.GetString(KRefresh, "");
            UserId = PlayerPrefs.GetString(KUserId, "");
        }

        public static void EnsureLoaded()
        {
            LoadFromDisk();
        }
    }
}
