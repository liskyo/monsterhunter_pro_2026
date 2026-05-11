using System;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>從 combat_tuning.json 載入調校（資料驅動，程式內不寫死戰鬥常數）。</summary>
    public sealed class CombatTuningStore : MonoBehaviour
    {
        public const string DefaultKey = "default";

        [SerializeField] TextAsset _combatTuningJson;

        戰鬥調校列 _row;

        public 戰鬥調校列 Active => _row;

        void Awake()
        {
            if (_combatTuningJson != null) Load();
        }

        /// <summary>BattleCombatManager 於執行期注入 JSON 字串（不需要 TextAsset）。</summary>
        public void InjectJson(string json)
        {
            try
            {
                var wrapped = "{\"資料\":" + json.Trim() + "}";
                var root = JsonUtility.FromJson<戰鬥調校陣列根>(wrapped);
                if (root?.資料 == null) { Debug.LogError("[CombatTuningStore] InjectJson 解析失敗"); return; }
                foreach (var r in root.資料)
                {
                    if (r != null && r.調校鍵 == DefaultKey) { _row = r; return; }
                }
                _row = root.資料.Length > 0 ? root.資料[0] : null;
            }
            catch (Exception e) { Debug.LogError("[CombatTuningStore] InjectJson: " + e.Message); }
        }

        public void Load()
        {
            if (_combatTuningJson == null)
            {
                Debug.LogError("[CombatTuningStore] 未指定 combat_tuning.json TextAsset。");
                return;
            }

            try
            {
                var wrapped = "{\"資料\":" + _combatTuningJson.text.Trim() + "}";
                var root = JsonUtility.FromJson<戰鬥調校陣列根>(wrapped);
                if (root?.資料 == null)
                {
                    Debug.LogError("[CombatTuningStore] 解析失敗。");
                    return;
                }

                foreach (var r in root.資料)
                {
                    if (r != null && r.調校鍵 == DefaultKey)
                    {
                        _row = r;
                        return;
                    }
                }

                _row = root.資料.Length > 0 ? root.資料[0] : null;
            }
            catch (Exception e)
            {
                Debug.LogError("[CombatTuningStore] " + e.Message);
            }
        }
    }
}
