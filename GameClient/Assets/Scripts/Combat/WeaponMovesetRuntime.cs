using System;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>從 weapon_movesets.json 解析「點擊」首招的動作倍率、攻擊距離（資料驅動）。</summary>
    public static class WeaponMovesetRuntime
    {
        public static bool TryGetTapMoveStats(string weaponType, string jsonText, out float 動作倍率, out float 攻擊距離)
        {
            動作倍率 = 0f; 攻擊距離 = 0f;
            if (string.IsNullOrEmpty(jsonText) || string.IsNullOrEmpty(weaponType)) return false;
            武器招式表列[] rows;
            try { rows = JsonConvert.DeserializeObject<武器招式表列[]>(jsonText); }
            catch (Exception e) { Debug.LogWarning("[WeaponMovesetRuntime] " + e.Message); return false; }
            if (rows == null) return false;
            foreach (var row in rows)
            {
                if (row == null || row.武器類型 != weaponType || row.操作配置 == null) continue;
                var tap = row.操作配置.點擊;
                if (tap == null || tap.Type == JTokenType.Null) return false;
                if (tap.Type == JTokenType.Array)
                {
                    var first = tap.First; if (first == null) return false;
                    動作倍率 = first.Value<float?>("動作倍率") ?? 0f;
                    攻擊距離 = first.Value<float?>("攻擊距離") ?? 0f;
                    return 動作倍率 > 0f;
                }
                if (tap.Type == JTokenType.Object)
                {
                    動作倍率 = tap.Value<float?>("動作倍率") ?? 0f;
                    攻擊距離 = tap.Value<float?>("攻擊距離") ?? 0f;
                    return 動作倍率 > 0f;
                }
                return false;
            }
            return false;
        }

        public static bool TryGetTapMoveStats(string weaponType, TextAsset movesetsJson, out float 動作倍率, out float 攻擊距離)
        {
            動作倍率 = 0f;
            攻擊距離 = 0f;
            if (movesetsJson == null || string.IsNullOrEmpty(weaponType)) return false;

            武器招式表列[] rows;
            try
            {
                rows = JsonConvert.DeserializeObject<武器招式表列[]>(movesetsJson.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WeaponMovesetRuntime] " + e.Message);
                return false;
            }

            if (rows == null) return false;

            foreach (var row in rows)
            {
                if (row == null || row.武器類型 != weaponType || row.操作配置 == null) continue;

                var tap = row.操作配置.點擊;
                if (tap == null || tap.Type == JTokenType.Null) return false;

                if (tap.Type == JTokenType.Array)
                {
                    var first = tap.First;
                    if (first == null) return false;
                    動作倍率 = first.Value<float?>("動作倍率") ?? 0f;
                    攻擊距離 = first.Value<float?>("攻擊距離") ?? 0f;
                    return 動作倍率 > 0f;
                }

                if (tap.Type == JTokenType.Object)
                {
                    動作倍率 = tap.Value<float?>("動作倍率") ?? 0f;
                    攻擊距離 = tap.Value<float?>("攻擊距離") ?? 0f;
                    return 動作倍率 > 0f;
                }

                return false;
            }

            return false;
        }
    }
}
