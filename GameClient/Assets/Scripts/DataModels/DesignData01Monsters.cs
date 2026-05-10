using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 DesignData/01_Monsters/monsters.json（根為陣列）。</summary>
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
        public 魔物普通攻擊 普通攻擊;
        public 魔物異常屬性攻擊項[] 異常屬性攻擊;
        public string 圖片路徑;
        public string 圖示路徑;
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

    [Serializable]
    public class 魔物異常屬性攻擊項
    {
        public string 異常屬性;
        public int 每秒傷害;
        public float 觸發機率;
        /// <summary>異常狀態在獵人身上的持續時間（秒）；到期後應清除，避免永久存在。</summary>
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
