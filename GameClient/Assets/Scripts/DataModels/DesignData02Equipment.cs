using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 DesignData/02_Equipment/equipment.json（根為陣列）。</summary>
    [Serializable]
    public class 裝備資料列
    {
        public string 裝備編號;
        public string 名稱;
        public string 裝備類型;
        public int 星級;
        public string 裝備屬性;
        public 裝備基礎數值 基礎數值;
        public string 圖片路徑;
        public string 圖示路徑;
        public 裝備合成配方 合成配方;
    }

    [Serializable]
    public class 裝備基礎數值
    {
        public int 物理傷害;
        public int 屬性傷害;
    }

    [Serializable]
    public class 裝備合成配方
    {
        public int 所需金幣;
        public 裝備合成需求素材[] 需求素材;
    }

    [Serializable]
    public class 裝備合成需求素材
    {
        public string 素材編號;
        /// <summary>可省略；UI 請用 <see cref="MaterialIdDisplay"/> 依 <see cref="素材編號"/> 對照 drop_rates。</summary>
        public string 素材名稱;
        public int 需求數量;
    }

    /// <summary>對應 DesignData/02_Equipment/upgrade_rules.json（根為陣列）。</summary>
    [Serializable]
    public class 裝備升級規則列
    {
        public string 裝備編號;
        public int 最高等級;
        public 裝備升級路徑項[] 升級路徑;
    }

    [Serializable]
    public class 裝備升級路徑項
    {
        public int 等級;
        public float 數值加成倍率;
        public 裝備升級花費 升級花費;
    }

    [Serializable]
    public class 裝備升級花費
    {
        public int 金幣;
        public 裝備升級需求素材[] 需求素材;
    }

    [Serializable]
    public class 裝備升級需求素材
    {
        public string 素材編號;
        public int 需求數量;
    }
}
