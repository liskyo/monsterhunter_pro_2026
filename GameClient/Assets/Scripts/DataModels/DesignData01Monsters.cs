using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 DesignData/01_Monsters/monsters.json（根為陣列）。魔物招式皆掛在 <see cref="魔物攻擊內容列"/>。</summary>
    [Serializable]
    public class 魔物資料列
    {
        public string 魔物編號;
        public string 名稱;
        public int 星級;
        public string[] 屬性;
        public 魔物弱點項[] 弱點;
        public float 身材面積;
        public int 最大血量;

        /// <summary>普攻與特殊攻擊（原「異常屬性攻擊」資料列為企劃定義的特殊效果／DoT／附加招）。</summary>
        public 魔物攻擊內容列 魔物攻擊內容;

        public string 圖片路徑;
        public string 圖示路徑;
    }

    /// <summary>monsters.json 內嵌於每只魔物的攻擊模組。</summary>
    [Serializable]
    public class 魔物攻擊內容列
    {
        public 魔物普通攻擊 普通攻擊;

        /// <summary>特殊攻擊／附加效果列表（JSON 鍵：<c>特殊攻擊</c>）。命中後依「觸發機率」判定是否施加。</summary>
        public 魔物特殊攻擊項[] 特殊攻擊;
    }

    [Serializable]
    public class 魔物弱點項
    {
        public string 屬性;
        public float 傷害加成比例;
    }

    [Serializable]
    public class 魔物普通攻擊
    {
        public int 傷害;
        public float 攻擊距離;
    }

    /// <summary>特殊攻擊一筆；欄位 <c>異常屬性</c> 為效果顯示名稱／分類標籤（例：裂傷、中毒）。</summary>
    [Serializable]
    public class 魔物特殊攻擊項
    {
        public string 異常屬性;
        public int 每秒傷害;
        public float 觸發機率;
        public float 持續時間秒;
    }

    /// <summary>對應 DesignData/01_Monsters/drop_rates.json（根為陣列）。</summary>
    [Serializable]
    public class 掉落率資料列
    {
        public string 魔物編號;
        public string 素材編號;
        public string 素材名稱;
        public float 掉落機率;
        public string 掉落條件;
    }
}
