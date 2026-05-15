using System;
using System.Collections.Generic;
using MonsterHunter.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 讀取 <c>DesignData/03_Combat/battle_background_labels.json</c>：檔 key（地圖名或 <c>CS{tier}_{地圖}</c> 碼）→ 顯示名。
    /// </summary>
    public static class BattleBackgroundLabelTable
    {
        [Serializable]
        sealed class FileDto
        {
            [JsonProperty("labels")] public Dictionary<string, string> Labels;
        }

        static Dictionary<string, string> _labels;
        static bool _attempted;

        /// <summary>編輯器或熱更新後可呼叫以重新讀檔。</summary>
        public static void Reload()
        {
            _attempted = false;
            _labels = null;
        }

        static void EnsureLoaded()
        {
            if (_attempted) return;
            _attempted = true;
            _labels = new Dictionary<string, string>(StringComparer.Ordinal);

            if (!DesignDataReader.TryLoadDesignDataText(out var json, "03_Combat",
                    "battle_background_labels.json"))
                return;

            try
            {
                var dto = JsonConvert.DeserializeObject<FileDto>(json);
                if (dto?.Labels == null) return;
                foreach (var kv in dto.Labels)
                {
                    if (kv.Key == null || kv.Value == null) continue;
                    var k = kv.Key.Trim();
                    if (k.Length == 0 || k.StartsWith("_", StringComparison.Ordinal)) continue;
                    _labels[k] = kv.Value.Trim();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BattleBackgroundLabelTable] 讀取失敗：" + e.Message);
            }
        }

        /// <summary>依多個候選 key 嘗試取得顯示名（含 <c>檔名去副檔</c>、<c>CS01_古代樹森林</c> 類短碼）。</summary>
        public static bool TryPickDisplayName(IEnumerable<string> candidateKeys, out string displayName)
        {
            displayName = null;
            EnsureLoaded();
            if (_labels == null || _labels.Count == 0) return false;

            foreach (var raw in candidateKeys)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var k = raw.Trim();
                if (TryOneKey(k, out displayName)) return true;
            }

            return false;
        }

        static bool TryOneKey(string key, out string displayName)
        {
            displayName = null;
            if (_labels.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                displayName = v.Trim();
                return true;
            }

            // 允許檔 stem 為「古代樹森林_背景」或舊 CS「CS03_瘴氣之谷_背景」時剝掉尾綴查標籤
            const string suf = "_背景";
            if (key.EndsWith(suf, StringComparison.Ordinal) && key.Length > suf.Length)
            {
                var shortKey = key.Substring(0, key.Length - suf.Length);
                if (_labels.TryGetValue(shortKey, out v) && !string.IsNullOrWhiteSpace(v))
                {
                    displayName = v.Trim();
                    return true;
                }
            }

            return false;
        }
    }
}
