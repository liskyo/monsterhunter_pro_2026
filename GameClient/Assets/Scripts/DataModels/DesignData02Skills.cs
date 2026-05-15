using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 DesignData/02_Equipment/skills.json（根為陣列）。</summary>
    [Serializable]
    public class 技能資料列
    {
        public string 技能編號;
        public string 名稱;
        public string 描述;
        /// <summary>技能圖示；預設 <c>Assets/Textures/Skills/{技能編號}.png</c>。</summary>
        public string 圖片路徑;
        public int 最高等級;
        public 技能等級效果項[] 各等級效果;
        public string[] 關聯魔物套裝;
    }

    [Serializable]
    public class 技能等級效果項
    {
        public int 等級;
        public string 效果描述;
        public 技能數值加成 數值加成;
    }

    /// <summary>
    /// JSON 中「數值加成」每次僅含一個統計鍵；此類聚合所有曾出現鍵，未出現欄位在 JsonUtility 下為 0。
    /// </summary>
    [Serializable]
    public class 技能數值加成
    {
        public float ATK_Flat;
        public float Crit_Rate;
        public float Weak_Point_Crit;
        public float Crit_Multiplier;
        public float Ele_Fire_Boost;
        public float Ele_Thunder_Boost;
        public float Ele_Ice_Boost;
        public float Ele_Water_Boost;
        public float Ele_Dragon_Boost;
        public float Earplug_Level;
        public float Damage_Reduction_Chance;
        public float HP_Flat;
        public float DEF_Flat;
        public float Charge_Speed_Boost;
        public float IFrame_Count;
        public float Part_Break_Multiplier;
        public float Enrage_ATK_Boost;
        public float Stun_Resistance;
        public float Poison_Resistance;
        public float Sharpness_Bonus;
        public float Utility_Value;
    }
}
