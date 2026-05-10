using MonsterHunter.DataModels;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>由 monsters.json 列推導物理／屬性肉質（HZV），供 DamageCalculator 使用。</summary>
    public static class MonsterStatResolver
    {
        /// <param name="weaponElement">武器屬性標籤，如「火」「無」「水」。</param>
        public static void ResolveHzv(魔物資料列 m, string weaponElement, 戰鬥調校列 tuning, out float physHzv, out float elemHzv)
        {
            physHzv = tuning != null ? tuning.預設物理肉質 : 0.45f;
            elemHzv = tuning != null ? tuning.無屬性克制時屬性肉質 : 1f;

            if (m == null) return;

            if (m.弱點 != null && !string.IsNullOrEmpty(weaponElement) && weaponElement != "無")
            {
                foreach (var w in m.弱點)
                {
                    if (w == null || string.IsNullOrEmpty(w.屬性)) continue;
                    if (w.屬性 != weaponElement) continue;
                    elemHzv = Mathf.Max(0f, w.傷害加成比例);
                    break;
                }
            }
        }
    }
}
