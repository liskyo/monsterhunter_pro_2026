using UnityEngine;

namespace MonsterHunter.Core
{
    /// <summary>
    /// 將 Supabase URL 與 anon key 放在 Resources（勿提交含正式 key 的資產至公開倉庫）。
    /// Create: Assets → Create → Monster Hunter → Supabase Runtime Config
    /// </summary>
    [CreateAssetMenu(fileName = "SupabaseRuntimeConfig", menuName = "Monster Hunter/Supabase Runtime Config")]
    public sealed class SupabaseRuntimeConfig : ScriptableObject
    {
        [Tooltip("例如 https://xxxx.supabase.co")]
        public string SupabaseUrl = "";

        [Tooltip("Dashboard → Project Settings → API → Publishable（anon）")]
        public string SupabaseAnonKey = "";
    }
}
