using System;
using System.Collections.Generic;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Data
{
    /// <summary>
    /// 依企劃慣例由裝備編號推導 <c>MON_XXX</c>：<c>WEP_k</c>→<c>MON_k</c>；<c>ARM_n</c>→第 <c>(n-1)/5+1</c> 隻魔物套裝。
    /// </summary>
    public static class EquipmentMonsterIds
    {
        public static string TryGetMonsterIdFromEquipmentId(string equipId)
        {
            if (string.IsNullOrWhiteSpace(equipId)) return null;
            var t = equipId.Trim();
            if (t.Length < 8) return null;
            if (t.StartsWith("WEP_", StringComparison.Ordinal))
            {
                if (!int.TryParse(t.AsSpan(4), out var w) || w < 1)
                    return null;
                return "MON_" + w.ToString("D3");
            }

            if (t.StartsWith("ARM_", StringComparison.Ordinal))
            {
                if (!int.TryParse(t.AsSpan(4), out var a) || a < 1)
                    return null;
                var set = (a - 1) / 5 + 1;
                return "MON_" + set.ToString("D3");
            }

            return null;
        }
    }

    /// <summary>
    /// 依目前身上武器＋五防具所屬魔物與 <c>skills.json</c> 的「關聯魔物套裝」加總技能等級（權宜規則，待企劃改為逐裝備技能表後替換）。
    /// </summary>
    public static class EquipmentSkillAggregator
    {
        public struct 技能顯示列
        {
            public 技能資料列 定義;
            public int 顯示等級;
            public string 效果說明文字;
        }

        public static 技能資料列[] LoadSkillsOrEmpty()
        {
            if (!DesignDataReader.TryLoadDesignDataText(out var json, "02_Equipment", "skills.json"))
                return Array.Empty<技能資料列>();
            try
            {
                return JsonConvert.DeserializeObject<技能資料列[]>(json) ?? Array.Empty<技能資料列>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EquipmentSkillAggregator] skills.json：" + e.Message);
                return Array.Empty<技能資料列>();
            }
        }

        public static Dictionary<string, 裝備資料列> LoadEquipmentDictionary()
        {
            var map = new Dictionary<string, 裝備資料列>(StringComparer.Ordinal);
            if (!DesignDataReader.TryLoadDesignDataText(out var json, "02_Equipment", "equipment.json"))
                return map;
            try
            {
                var rows = JsonConvert.DeserializeObject<裝備資料列[]>(json);
                if (rows == null) return map;
                foreach (var r in rows)
                {
                    if (r != null && !string.IsNullOrEmpty(r.裝備編號))
                        map[r.裝備編號] = r;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EquipmentSkillAggregator] equipment.json：" + e.Message);
            }

            return map;
        }

        public static Dictionary<string, 裝備資料列> LoadArmorDictionary()
        {
            var map = new Dictionary<string, 裝備資料列>(StringComparer.Ordinal);
            if (!DesignDataReader.TryLoadDesignDataText(out var json, "02_Equipment", "armor.json"))
                return map;
            try
            {
                var rows = JsonConvert.DeserializeObject<裝備資料列[]>(json);
                if (rows == null) return map;
                foreach (var r in rows)
                {
                    if (r != null && !string.IsNullOrEmpty(r.裝備編號))
                        map[r.裝備編號] = r;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EquipmentSkillAggregator] armor.json：" + e.Message);
            }

            return map;
        }

        public static List<技能顯示列> BuildDisplayList(
            string weaponId,
            IReadOnlyList<string> armorSlotIds5,
            技能資料列[] skills)
        {
            var list = new List<技能顯示列>();
            var mons = new List<string>(6);
            mons.Add(EquipmentMonsterIds.TryGetMonsterIdFromEquipmentId(weaponId));
            if (armorSlotIds5 != null)
            {
                foreach (var a in armorSlotIds5)
                    mons.Add(EquipmentMonsterIds.TryGetMonsterIdFromEquipmentId(a));
            }

            if (skills == null) return list;

            foreach (var sk in skills)
            {
                if (sk == null || sk.關聯魔物套裝 == null || sk.關聯魔物套裝.Length == 0)
                    continue;
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var m in sk.關聯魔物套裝)
                {
                    if (!string.IsNullOrWhiteSpace(m))
                        set.Add(m.Trim());
                }

                var n = 0;
                foreach (var mon in mons)
                {
                    if (!string.IsNullOrEmpty(mon) && set.Contains(mon))
                        n++;
                }

                var lv = Mathf.Min(sk.最高等級, n);
                if (lv <= 0) continue;
                var desc = PickEffectDescription(sk, lv);
                list.Add(new 技能顯示列 { 定義 = sk, 顯示等級 = lv, 效果說明文字 = desc });
            }

            list.Sort((a, b) =>
                string.Compare(a.定義?.名稱, b.定義?.名稱, StringComparison.Ordinal));
            return list;
        }

        static string PickEffectDescription(技能資料列 sk, int level)
        {
            if (sk.各等級效果 != null)
            {
                foreach (var e in sk.各等級效果)
                {
                    if (e != null && e.等級 == level && !string.IsNullOrWhiteSpace(e.效果描述))
                        return e.效果描述;
                }
            }

            return sk.描述 ?? "";
        }
    }
}
