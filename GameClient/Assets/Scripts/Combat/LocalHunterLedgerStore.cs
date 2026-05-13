using System;
using System.Collections.Generic;
using System.IO;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 本地獵場狀態：倉庫素材累積／預覽用染料球與痕跡／穿戴武器（無 Supabase 時由結算與 Bootstrap 共用）。
    /// </summary>
    [Serializable]
    public sealed class LocalHunterLedger
    {
        public string PreviewPaintballItemId = "";
        public string PreviewTraceId = "";
        public string PreviewCanteenFoodId = "";

        /// <summary>對應 equipment.json「裝備編號」（武器）。</summary>
        public string EquippedWeaponEquipmentId = "WEP_001";

        /// <summary>鍵：<c>裝備編號</c>；值：目前強化階級（1=未強化資料表基礎值）。</summary>
        public Dictionary<string, int> EquipmentLevels = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>鍵：<c>素材編號</c>。</summary>
        public Dictionary<string, int> Warehouse = new Dictionary<string, int>(StringComparer.Ordinal);

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, "mh_local_hunter_ledger.json");

        public static LocalHunterLedger LoadOrCreate()
        {
            try
            {
                var p = FilePath;
                if (File.Exists(p))
                {
                    var txt = File.ReadAllText(p);
                    var dto = JsonConvert.DeserializeObject<LocalHunterLedger>(txt);
                    if (dto != null)
                    {
                        dto.Warehouse ??= new Dictionary<string, int>(StringComparer.Ordinal);
                        dto.EquipmentLevels ??= new Dictionary<string, int>(StringComparer.Ordinal);
                        return dto;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LocalHunterLedger] 讀取失敗：" + e.Message);
            }

            return new LocalHunterLedger();
        }

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LocalHunterLedger] 寫入失敗：" + e.Message);
            }
        }

        public void MergeSettlementRewards(IEnumerable<SettlementRewardEntry> rewards)
        {
            if (rewards == null) return;
            foreach (var r in rewards)
            {
                if (string.IsNullOrEmpty(r.素材編號)) continue;
                if (r.數量 <= 0) continue;
                if (!Warehouse.TryGetValue(r.素材編號, out var n))
                    n = 0;
                Warehouse[r.素材編號] = Mathf.Max(0, n + r.數量);
            }

            Save();
        }

        /// <summary>取得裝備等級（缺省視為 1）。</summary>
        public int GetEquipmentLevel(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId)) return 1;
            return EquipmentLevels != null &&
                   EquipmentLevels.TryGetValue(equipmentId, out var lv) &&
                   lv > 0
                ? lv
                : 1;
        }
    }
}
