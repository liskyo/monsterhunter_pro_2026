using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 玩家戰鬥面板：數值應由裝備／DesignData 載入後寫入，不在程式中寫死企劃常數。
    /// </summary>
    public sealed class PlayerCombatLoadout : MonoBehaviour
    {
        [Tooltip("對應 weapon_movesets.json 的「武器類型」字串")]
        public string 武器類型 = "";

        [Tooltip("對應 equipment 基礎數值等")]
        public float 武器基礎物理;

        /// <summary>若由 equipment.json 綁定寫入，記錄當前武器「裝備編號」（除錯／結算銜接）。</summary>
        public string BoundWeaponEquipmentId = "";

        public float 武器屬性;
        public string 武器屬性標籤 = "無";

        void OnValidate()
        {
            if (武器屬性標籤 == null) 武器屬性標籤 = "無";
        }
    }
}
