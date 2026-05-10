using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.DataModels
{
    /// <summary>
    /// 依 <c>DesignData/01_Monsters/drop_rates.json</c> 將 <c>素材編號</c>（如 MAT_001_01）對應顯示名。
    /// 合成配方等資料表僅保留素材編號，避免與掉落表雙軌不同步。
    /// </summary>
    public sealed class MaterialIdDisplay
    {
        readonly Dictionary<string, string> _map;

        public MaterialIdDisplay(TextAsset dropRatesJson)
            : this(dropRatesJson != null ? dropRatesJson.text : null)
        {
        }

        public MaterialIdDisplay(string dropRatesJsonText)
        {
            _map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(dropRatesJsonText)) return;

            掉落率資料列[] rows;
            try
            {
                rows = JsonConvert.DeserializeObject<掉落率資料列[]>(dropRatesJsonText);
            }
            catch
            {
                return;
            }

            if (rows == null) return;
            foreach (var r in rows)
            {
                if (r == null || string.IsNullOrEmpty(r.素材編號)) continue;
                var id = r.素材編號.Trim();
                if (_map.ContainsKey(id)) continue;
                _map[id] = string.IsNullOrEmpty(r.素材名稱) ? id : r.素材名稱;
            }
        }

        public bool TryGet(string itemId, out string displayName)
        {
            displayName = null;
            if (string.IsNullOrEmpty(itemId)) return false;
            return _map.TryGetValue(itemId.Trim(), out displayName);
        }

        /// <summary>優先 drop_rates；其次舊版配方內嵌名稱；最後回傳編號。</summary>
        public string Resolve(string itemId, string legacyRecipeName = null)
        {
            if (TryGet(itemId, out var n)) return n;
            if (!string.IsNullOrEmpty(legacyRecipeName)) return legacyRecipeName;
            return itemId ?? "";
        }

        public string Resolve(裝備合成需求素材 m) =>
            m == null ? "" : Resolve(m.素材編號, m.素材名稱);
    }
}
