using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 DesignData/05_Systems/canteen.json（根為陣列）。</summary>
    [Serializable]
    public class 貓飯資料列
    {
        public string 料理編號;
        public string 名稱;
        public string 類型;
        public 貓飯花費 花費;
        public 貓飯增益效果 增益效果;
        public 貓飯特殊技能 特殊貓飯技能;
    }

    [Serializable]
    public class 貓飯花費
    {
        public int 金幣;
        public 貓飯需求素材[] 需求素材;
    }

    [Serializable]
    public class 貓飯需求素材
    {
        public string 素材編號;
        public int 數量;
        public string 名稱;
    }

    /// <summary>各料理僅會填部分欄位；JsonUtility 未出現的數值為 0。</summary>
    [Serializable]
    public class 貓飯增益效果
    {
        public float 體力上限;
        public float 耐力上限;
        public float 持續時間_秒;
        public float 攻擊力加成;
        public float 防禦力加成;
        public float 雷屬性耐性;
        public float 耐力消耗倍率;
        public float 火屬性耐性;
        public float 移動速度加成;
        public float 爆破傷害加成;
        public float 冰屬性耐性;
        public float 反傷倍率;
        public float 會心率加成;
        public float 體力恢復速度;
        public float 全屬性耐性;
        public float 擊中回血;
        public float 防性加成;
        public float 採集速度加成;
        public float 全能力加成;
        public float 迴避無敵幀加成;
        public float 中毒耐性;
        public float 裂傷耐性;
        public float 狂龍症抗性;
        public float 物理攻擊加成;
        public float 爆破機率加成;
        public float 風壓耐性;
        public float 毒屬性攻擊加成;
        public float 遠程傷害加成;
        public float 閃光彈有效範圍;
    }

    [Serializable]
    public class 貓飯特殊技能
    {
        public string 技能名稱;
        public float 觸發機率;
        public string 效果描述;
    }

    /// <summary>對應 DesignData/05_Systems/pets.json（根為陣列）。</summary>
    [Serializable]
    public class 寵物資料列
    {
        public string 寵物編號;
        public string 名稱;
        public string 種類;
        public string 定位;
        public string 對應魔物編號;
        public 寵物基礎數值 基礎數值;
        public 寵物技能組 技能組;
    }

    [Serializable]
    public class 寵物基礎數值
    {
        public int 體力;
        public int 攻擊力;
        public int 防禦力;
    }

    [Serializable]
    public class 寵物技能組
    {
        public string 主動技能;
        public string 被動技能;
    }

    /// <summary>對應 DesignData/05_Systems/quests.json（根為陣列）。</summary>
    [Serializable]
    public class 任務資料列
    {
        public string 任務編號;
        public string 標題;
        public string 任務分類;
        public int 星級;
        public string 地圖;
        public 任務目標魔物項[] 目標魔物;
        public int 限制時間_秒;
        public 任務報酬 報酬;
        public string 前置任務要求;
        public string 描述;
    }

    [Serializable]
    public class 任務目標魔物項
    {
        public string 魔物編號;
        public string 魔物名稱;
        public int 數量;
    }

    [Serializable]
    public class 任務報酬
    {
        public int 金幣;
        public int 獵人經驗值;
        public string[] 額外獎勵;
    }
}
