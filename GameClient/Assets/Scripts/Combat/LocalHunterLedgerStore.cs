using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// <summary>本機試玩用金幣（未登入 Supabase 時與商店／結算共用）。</summary>
        public long Zenny = 99999;

        public string PreviewPaintballItemId = "";
        public string PreviewTraceId = "";
        public string PreviewCanteenFoodId = "";

        /// <summary>對應 equipment.json「裝備編號」（武器）。</summary>
        public string EquippedWeaponEquipmentId = "WEP_001";

        /// <summary>
        /// 五件防具編號，依序：頭、胸、腕、腰、腳（對應 armor.json 裝備類型頭部／胸部／腕部／腰部／腳部）。
        /// </summary>
        public string[] EquippedArmorSlotIds =
        {
            "ARM_001", "ARM_002", "ARM_003", "ARM_004", "ARM_005",
        };

        /// <summary>鍵：<c>裝備編號</c>；值：目前強化階級（1=未強化資料表基礎值）。</summary>
        public Dictionary<string, int> EquipmentLevels = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>鍵：<c>素材編號</c>。</summary>
        public Dictionary<string, int> Warehouse = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>任務板：目前進行中任務（同時最多一則）。</summary>
        public string ActiveQuestId = "";

        /// <summary>配合 <see cref="QuestIdsUsedToday"/>：上次重設「每日接任務」的日期（yyyy-MM-dd）。</summary>
        public string QuestDailyRolloverDate = "";

        /// <summary>今日曾接取過的任務編號（含進行中）；同一任務當日不可再接。</summary>
        public List<string> QuestIdsUsedToday = new List<string>();

        /// <summary>寵物小屋：隨行出戰寵物（須在倉庫擁有）。留空則戰鬥端改為隨機已擁有寵物。</summary>
        public string SelectedBattlePetId = "";

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, "mh_local_hunter_ledger.json");

        public static LocalHunterLedger LoadOrCreate()
        {
            LocalHunterLedger dto = null;
            try
            {
                var p = FilePath;
                if (File.Exists(p))
                {
                    var txt = File.ReadAllText(p);
                    dto = JsonConvert.DeserializeObject<LocalHunterLedger>(txt);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LocalHunterLedger] 讀取失敗：" + e.Message);
            }

            if (dto == null)
            {
                dto = new LocalHunterLedger();
            }

            dto.Warehouse ??= new Dictionary<string, int>(StringComparer.Ordinal);
            dto.EquipmentLevels ??= new Dictionary<string, int>(StringComparer.Ordinal);
            
            // ✦ 測試環境：保證金幣至少為 99999，讓玩家永遠免於金幣不足
            if (dto.Zenny < 99999)
            {
                dto.Zenny = 99999;
            }

            dto.NormalizeEquippedArmorSlots();
            dto.NormalizeQuestTrackingFields();
            dto.InjectFreeTestItems(); // ✦ 自動贈送/補滿測試道具
            return dto;
        }

        /// <summary>
        /// 自動補發測試用染色球與魔物痕跡（每個種類各 10 個），方便戰鬥整備測試。
        /// </summary>
        public void InjectFreeTestItems()
        {
            Warehouse ??= new Dictionary<string, int>(StringComparer.Ordinal);
            bool modified = false;

            // 1. 贈送染色球：使用通用 dynamic/dictionary 解析以 100% 預防任何 schema 類型拋錯
            if (MonsterHunter.Data.DesignDataReader.TryLoadDesignDataText(out var pj, "04_Items", "paintballs.json"))
            {
                try
                {
                    var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(pj);
                    if (rows != null)
                    {
                        foreach (var r in rows)
                        {
                            if (r != null && r.TryGetValue("道具編號", out var valObj) && valObj != null)
                            {
                                var id = valObj.ToString().Trim();
                                if (!string.IsNullOrEmpty(id))
                                {
                                    if (!Warehouse.TryGetValue(id, out var qty) || qty < 10)
                                    {
                                        Warehouse[id] = 10;
                                        modified = true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[LocalHunterLedger] 解析 paintballs.json 失敗：" + ex.Message);
                }
            }

            // 2. 贈送魔物痕跡：使用通用 dynamic/dictionary 解析以 100% 預防任何 schema 類型拋錯
            if (MonsterHunter.Data.DesignDataReader.TryLoadDesignDataText(out var tj, "04_Items", "monster_traces.json"))
            {
                try
                {
                    var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(tj);
                    if (rows != null)
                    {
                        foreach (var r in rows)
                        {
                            if (r != null && r.TryGetValue("痕跡編號", out var valObj) && valObj != null)
                            {
                                var id = valObj.ToString().Trim();
                                if (!string.IsNullOrEmpty(id))
                                {
                                    if (!Warehouse.TryGetValue(id, out var qty) || qty < 10)
                                    {
                                        Warehouse[id] = 10;
                                        modified = true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[LocalHunterLedger] 解析 monster_traces.json 失敗：" + ex.Message);
                }
            }

            if (modified)
            {
                Save();
            }
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

        public void AddWarehouseItems(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity == 0) return;
            Warehouse ??= new Dictionary<string, int>(StringComparer.Ordinal);
            if (!Warehouse.TryGetValue(itemId, out var n))
                n = 0;
            Warehouse[itemId] = Mathf.Max(0, n + quantity);
            Save();
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

        /// <summary>確保武器與五格防具陣列存在且填妥初始值；舊存檔沒有欄位或毀損時補預設。</summary>
        public void NormalizeEquippedArmorSlots()
        {
            if (string.IsNullOrWhiteSpace(EquippedWeaponEquipmentId))
            {
                EquippedWeaponEquipmentId = "WEP_001";
            }

            if (EquippedArmorSlotIds == null || EquippedArmorSlotIds.Length != 5 || EquippedArmorSlotIds.Any(string.IsNullOrWhiteSpace))
            {
                EquippedArmorSlotIds = new[]
                {
                    "ARM_001", "ARM_002", "ARM_003", "ARM_004", "ARM_005",
                };
            }
        }

        public void NormalizeQuestTrackingFields()
        {
            QuestIdsUsedToday ??= new List<string>();
            QuestIdsUsedToday = QuestIdsUsedToday.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        }

        /// <summary>跨日時清空「今日已接」名單。</summary>
        public void TouchDailyQuestRollover()
        {
            NormalizeQuestTrackingFields();
            var d = DateTime.Now.ToString("yyyy-MM-dd");
            if (QuestDailyRolloverDate != d)
            {
                QuestDailyRolloverDate = d;
                QuestIdsUsedToday.Clear();
                Save();
            }
        }

        /// <summary>接任務：同時僅能一則；同一任務當日僅能接一次（含已放棄）。</summary>
        public bool TryAcceptQuest(string questId, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(questId))
            {
                error = "任務編號無效";
                return false;
            }

            var id = questId.Trim();
            TouchDailyQuestRollover();
            NormalizeQuestTrackingFields();

            if (!string.IsNullOrEmpty(ActiveQuestId))
            {
                error = "已有一項進行中的任務，請先放棄或完成後再承接其他任務。";
                return false;
            }

            if (QuestIdsUsedToday.Contains(id))
            {
                error = "今日已接取過此任務。";
                return false;
            }

            ActiveQuestId = id;
            QuestIdsUsedToday.Add(id);
            Save();
            return true;
        }

        public void AbandonActiveQuest()
        {
            ActiveQuestId = "";
            Save();
        }

        /// <summary>討伐完成結算後呼叫：清空進行中（當日仍不可再接同一任務）。</summary>
        public void ClearActiveQuestAfterComplete()
        {
            ActiveQuestId = "";
            Save();
        }

        public int GetWarehouseQuantity(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || Warehouse == null) return 0;
            return Warehouse.TryGetValue(itemId, out var q) ? q : 0;
        }
    }
}
