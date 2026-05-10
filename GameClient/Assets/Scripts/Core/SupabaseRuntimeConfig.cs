using UnityEngine;

namespace MonsterHunter.Core
{
    /// <summary>
    /// 將 Supabase URL 與 anon key 放在 Resources（勿提交含正式 key 的資產至公開倉庫）。
    /// 預設資產：<c>Assets/Resources/SupabaseRuntimeConfig.asset</c>（本機 Docker：<c>http://127.0.0.1:54321</c>）。
    /// 實機請在該資產改為 <c>http://區網IP:54321</c>；若 <c>npx supabase stop/start</c> 後 key 變了，請對照 <c>supabase status</c> 更新 Publishable。
    /// Create: Assets → Create → Monster Hunter → Supabase Runtime Config
    /// </summary>
    [CreateAssetMenu(fileName = "SupabaseRuntimeConfig", menuName = "Monster Hunter/Supabase Runtime Config")]
    public sealed class SupabaseRuntimeConfig : ScriptableObject
    {
        public const string ResourcesAssetName = "SupabaseRuntimeConfig";

        [Tooltip("例如 https://xxxx.supabase.co 或本機 http://127.0.0.1:54321")]
        public string SupabaseUrl = "http://127.0.0.1:54321";

        [Tooltip("Dashboard → API → Publishable（anon）；本機見 supabase status")]
        public string SupabaseAnonKey = "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH";

        public static SupabaseRuntimeConfig LoadFromResources() =>
            Resources.Load<SupabaseRuntimeConfig>(ResourcesAssetName);

        /// <summary>Inspector 未指派或欄位空白時，改用 Resources 預設資產。</summary>
        public static SupabaseRuntimeConfig ResolveFor(SupabaseRuntimeConfig field)
        {
            if (field != null
                && !string.IsNullOrWhiteSpace(field.SupabaseUrl)
                && !string.IsNullOrWhiteSpace(field.SupabaseAnonKey))
                return field;
            return LoadFromResources();
        }
    }
}
