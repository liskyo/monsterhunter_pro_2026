using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 DesignData/04_Items/materials.json（根為陣列）。</summary>
    [Serializable]
    public class 素材資料列
    {
        public string 素材編號;
        public string 名稱;
        public int 稀有度;
        public string 分類;
        public string 描述;
        public int 出售價格;
        public int 攜帶上限;
    }

    /// <summary>對應 DesignData/04_Items/paintballs.json（根為陣列）。</summary>
    [Serializable]
    public class 染色球資料列
    {
        public string 道具編號;
        public string 名稱;
        public int 稀有度;
        public string 分類;
        public int 吸引星級_最低;
        public int 吸引星級_最高;
        public float 成功率;
        public string 描述;
        public int 出售價格;
        public int 購買價格;
    }

    /// <summary>對應 DesignData/04_Items/monster_traces.json（根為陣列）。</summary>
    [Serializable]
    public class 魔物痕跡資料列
    {
        public string 痕跡編號;
        public string 名稱;
        public string 對應魔物編號;
        public int 魔物星級;
        public string 描述;
        public string 取得途徑;
    }
}
