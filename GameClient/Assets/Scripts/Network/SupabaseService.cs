using System;
using System.Collections;
using System.Text;
using MonsterHunter.Core;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MonsterHunter.Network
{
    /// <summary>
    /// 雲端 Supabase 存取：對應 public.profiles、public.warehouse_items（RLS：authenticated + auth.uid()）。
    /// 玩家欄位對照見 migration 20260512100000_player_state_join_lobbies.sql；
    /// 遊戲內結構對齊 PlayerState/players.json（經 <see cref="玩家狀態本機範例"/>）。
    /// </summary>
    public sealed class SupabaseService : MonoBehaviour
    {
        [SerializeField] SupabaseRuntimeConfig _config;

        SupabaseRestClient _client;

        void Awake()
        {
            AuthSession.EnsureLoaded();
            if (_config != null)
                _client = new SupabaseRestClient(_config.SupabaseUrl, _config.SupabaseAnonKey);
        }

        SupabaseRestClient Client =>
            _client ??= _config != null
                ? new SupabaseRestClient(_config.SupabaseUrl, _config.SupabaseAnonKey)
                : null;

        /// <summary>
        /// 讀取目前登入使用者的 <c>profiles</c>，轉成與 players.json 相同的欄位語意（player_id=name uuid、zenny=zeni…）。
        /// </summary>
        public IEnumerator LoadPlayerProfile(Action<string> onError, Action<玩家狀態本機範例> onOk)
        {
            if (!AuthSession.IsSignedIn)
            {
                onError?.Invoke("未登入，無法讀取 profiles。");
                yield break;
            }

            if (Client == null)
            {
                onError?.Invoke("缺少 SupabaseRuntimeConfig。");
                yield break;
            }

            var uid = AuthSession.UserId;
            var path = $"profiles?id=eq.{uid}&select=*";
            string err = null;
            string json = null;
            yield return Client.GetJson(path, AuthSession.AccessToken, e => err = e, j => json = j);
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            ProfileRow[] rows = null;
            try
            {
                rows = JsonConvert.DeserializeObject<ProfileRow[]>(json);
            }
            catch (Exception e)
            {
                onError?.Invoke("profiles JSON 解析失敗：" + e.Message);
                yield break;
            }

            if (rows == null || rows.Length == 0)
            {
                onError?.Invoke("profiles 無資料列（新帳號可能尚未寫入，請確認 trigger handle_new_user）。");
                yield break;
            }

            onOk?.Invoke(To玩家狀態本機(rows[0]));
        }

        /// <summary>
        /// 倉庫素材增減：讀取 <c>warehouse_items</c>（owner_id + item_id 唯一鍵），合併數量後 upsert；數量 ≤ 0 則刪除該列。
        /// 雲端表僅存 item_id／quantity／slot_index；與本機 warehouse.json 的 name、storage_slots 無對應欄位。
        /// </summary>
        /// <param name="itemId">素材或道具編號（如 MAT_COM_001）。</param>
        /// <param name="quantityDelta">正為獲得、負為消耗。</param>
        /// <param name="slotIndex">可選；僅在新建列或一併更新時寫入。</param>
        public IEnumerator UpdateWarehouse(
            string itemId,
            int quantityDelta,
            Action<string> onError,
            Action onOk,
            int? slotIndex = null
        )
        {
            if (!AuthSession.IsSignedIn)
            {
                onError?.Invoke("未登入，無法更新 warehouse_items。");
                yield break;
            }

            if (Client == null)
            {
                onError?.Invoke("缺少 SupabaseRuntimeConfig。");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                onError?.Invoke("itemId 不可為空。");
                yield break;
            }

            var uid = AuthSession.UserId;
            var idEnc = Uri.EscapeDataString(itemId.Trim());
            var getPath = $"warehouse_items?owner_id=eq.{uid}&item_id=eq.{idEnc}&select=quantity";
            string err = null;
            string json = null;
            yield return Client.GetJson(getPath, AuthSession.AccessToken, e => err = e, j => json = j);
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            WarehouseQuantityRow[] existing = null;
            try
            {
                existing = JsonConvert.DeserializeObject<WarehouseQuantityRow[]>(json);
            }
            catch (Exception e)
            {
                onError?.Invoke("warehouse_items 查詢解析失敗：" + e.Message);
                yield break;
            }

            var current = existing != null && existing.Length > 0 ? existing[0].quantity : 0;
            var next = current + quantityDelta;
            if (next <= 0)
            {
                var delPath = $"warehouse_items?owner_id=eq.{uid}&item_id=eq.{idEnc}";
                yield return Client.Delete(delPath, AuthSession.AccessToken, onError, onOk);
                yield break;
            }

            var row = new StringBuilder();
            row.Append("{\"owner_id\":\"").Append(SupabaseJsonMini.EscapeJsonString(uid)).Append('"');
            row.Append(",\"item_id\":\"").Append(SupabaseJsonMini.EscapeJsonString(itemId.Trim())).Append('"');
            row.Append(",\"quantity\":").Append(next);
            if (slotIndex.HasValue)
                row.Append(",\"slot_index\":").Append(slotIndex.Value);
            row.Append(",\"updated_at\":\"").Append(DateTime.UtcNow.ToString("o")).Append("\"}");
            var body = "[" + row + "]";

            yield return Client.PostMerge("warehouse_items", body, AuthSession.AccessToken, onError, onOk);
        }

        /// <summary>PostgREST <c>profiles</c> 列（欄位名與資料庫一致）。</summary>
        [Serializable]
        public class ProfileRow
        {
            public string id;
            public string display_name;
            public int hr_rank;
            public long experience;
            public long zeni;
            public 玩家裝備配置 current_equipment;
            public 玩家寵物配置 active_pets;
            public 玩家腰包項目[] pouch;
            public JToken current_canteen_buff;
            public string last_login_at;
        }

        [Serializable]
        class WarehouseQuantityRow
        {
            public int quantity;
        }

        public static 玩家狀態本機範例 To玩家狀態本機(ProfileRow p)
        {
            if (p == null) return null;

            string buff = null;
            if (p.current_canteen_buff != null && p.current_canteen_buff.Type != JTokenType.Null)
                buff = p.current_canteen_buff.ToString(Formatting.None);

            return new 玩家狀態本機範例
            {
                player_id = p.id ?? "",
                name = p.display_name ?? "",
                rank = p.hr_rank,
                experience = ClampToInt(p.experience),
                zenny = ClampToInt(p.zeni),
                current_equipment = p.current_equipment,
                active_pets = p.active_pets,
                pouch = p.pouch ?? Array.Empty<玩家腰包項目>(),
                current_canteen_buff = buff,
                last_login = p.last_login_at ?? "",
            };
        }

        static int ClampToInt(long v)
        {
            if (v > int.MaxValue) return int.MaxValue;
            if (v < int.MinValue) return int.MinValue;
            return (int)v;
        }

        /// <summary>
        /// 戰鬥結算：圖鑑擊殺 +1、<c>daily_hunt_records</c> 當日 <c>__SESSION__</c> 擊殺數；
        /// 若當日第 3 擊則 <c>difficulty_multiplier</c> +0.05（見 migration 20260513130000）。
        /// </summary>
        public IEnumerator PostMonsterKillSession(
            DateTime localDate,
            string monsterId,
            string monsterName,
            Action<string> onError,
            Action<int, float> onOk
        )
        {
            if (!AuthSession.IsSignedIn)
            {
                onError?.Invoke("未登入。");
                yield break;
            }

            if (Client == null)
            {
                onError?.Invoke("缺少 SupabaseRuntimeConfig。");
                yield break;
            }

            var d = localDate.ToString("yyyy-MM-dd");
            var mid = SupabaseJsonMini.EscapeJsonString(monsterId ?? "");
            var mname = SupabaseJsonMini.EscapeJsonString(monsterName ?? "");
            var body = $"{{\"p_local_date\":\"{d}\",\"p_monster_id\":\"{mid}\",\"p_monster_name\":\"{mname}\"}}";

            string err = null;
            string json = null;
            yield return Client.Rpc("post_monster_kill_session", body, AuthSession.AccessToken, e => err = e, j => json = j);
            if (err != null)
            {
                onError?.Invoke(err);
                yield break;
            }

            try
            {
                var rows = JsonConvert.DeserializeObject<KillSessionRpcRow[]>(json);
                if (rows == null || rows.Length == 0)
                {
                    onError?.Invoke("post_monster_kill_session 回應為空。");
                    yield break;
                }

                onOk?.Invoke(rows[0].kill_count, rows[0].difficulty_multiplier);
            }
            catch (Exception e)
            {
                onError?.Invoke("解析結算回應失敗：" + e.Message);
            }
        }

        [Serializable]
        class KillSessionRpcRow
        {
            public int kill_count;
            public float difficulty_multiplier;
        }
    }
}
