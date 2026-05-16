using System;
using System.Linq;
using MonsterHunter.Combat;
using MonsterHunter.Data;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 離村進入戰鬥前：須已接任務、持有並選好染色球與對應星級之魔物痕跡，且痕跡目標魔物須為任務目標之一。
    /// </summary>
    public static class VillageBattlePrepRules
    {
        public static bool TryValidate(LocalHunterLedger ledger, out string error)
        {
            error = null;
            if (ledger == null)
            {
                error = "讀取獵人紀錄失敗。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ledger.ActiveQuestId))
            {
                error = "請先在「任務板」承接一項任務。";
                return false;
            }

            var quest = FindQuestRow(ledger.ActiveQuestId.Trim());
            if (quest == null)
            {
                error = "找不到進行中的任務資料，請回任務板重新承接。";
                return false;
            }

            var paintId = (ledger.PreviewPaintballItemId ?? "").Trim();
            var traceId = (ledger.PreviewTraceId ?? "").Trim();
            if (string.IsNullOrEmpty(paintId))
            {
                error = "請在「出戰整備」選擇染色球（須持有至少 1 個）。";
                return false;
            }

            if (string.IsNullOrEmpty(traceId))
            {
                error = "請在「出戰整備」選擇魔物痕跡（須持有至少 1 個）。";
                return false;
            }

            if (ledger.GetWarehouseQuantity(paintId) <= 0)
            {
                error = "染色球數量不足，請先至商店補貨。";
                return false;
            }

            if (ledger.GetWarehouseQuantity(traceId) <= 0)
            {
                error = "魔物痕跡數量不足（討伐掉落取得）。";
                return false;
            }

            var paint = FindPaintballRow(paintId);
            if (paint == null)
            {
                error = "找不到染色球資料：" + paintId;
                return false;
            }

            var trace = FindTraceRow(traceId);
            if (trace == null)
            {
                error = "找不到魔物痕跡資料：" + traceId;
                return false;
            }

            var star = trace.魔物星級;
            var lo = paint.吸引星級_最低;
            var hi = paint.吸引星級_最高;
            if (star < lo || star > hi)
            {
                error =
                    $"痕跡「{trace.名稱}」魔物星級為 {star}，與染色球「{paint.名稱}」可吸引範圍 {lo}～{hi} 不符。請更換組合。";
                return false;
            }

            var mid = (trace.對應魔物編號 ?? "").Trim();
            if (quest.目標魔物 == null ||
                !quest.目標魔物.Any(t => t != null && (t.魔物編號 ?? "").Trim() == mid))
            {
                error =
                    $"痕跡對應魔物（{mid}）不在此任務目標內。請選擇與「{quest.標題}」相符的痕跡。";
                return false;
            }

            return true;
        }

        public static 任務資料列 FindQuestRow(string questId)
        {
            if (string.IsNullOrEmpty(questId) ||
                !DesignDataReader.TryLoadDesignDataText(out var text, "05_Systems", "quests.json"))
                return null;
            try
            {
                var rows = JsonConvert.DeserializeObject<任務資料列[]>(text);
                return rows?.FirstOrDefault(r => r != null && r.任務編號 == questId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VillageBattlePrepRules] quests.json：" + e.Message);
                return null;
            }
        }

        static 染色球資料列 FindPaintballRow(string id)
        {
            if (string.IsNullOrEmpty(id) ||
                !DesignDataReader.TryLoadDesignDataText(out var json, "04_Items", "paintballs.json"))
                return null;
            try
            {
                var rows = JsonConvert.DeserializeObject<染色球資料列[]>(json);
                return rows?.FirstOrDefault(r => r != null && r.道具編號 == id);
            }
            catch
            {
                return null;
            }
        }

        static 魔物痕跡資料列 FindTraceRow(string id)
        {
            if (string.IsNullOrEmpty(id) ||
                !DesignDataReader.TryLoadDesignDataText(out var json, "04_Items", "monster_traces.json"))
                return null;
            try
            {
                var rows = JsonConvert.DeserializeObject<魔物痕跡資料列[]>(json);
                return rows?.FirstOrDefault(r => r != null && r.痕跡編號 == id);
            }
            catch
            {
                return null;
            }
        }
    }
}
