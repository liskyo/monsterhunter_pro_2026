using System;
using Newtonsoft.Json.Linq;

namespace MonsterHunter.DataModels
{
    /// <summary>
    /// 對應 DesignData/03_Combat/weapon_movesets.json（根為陣列）。
    /// 「點擊」在 JSON 中可能為單一物件或陣列，故使用 JToken；請安裝 com.unity.nuget.newtonsoft-json 並使用 DesignDataDeserialize.WeaponMovesetsFromJson。
    /// </summary>
    [Serializable]
    public class 武器招式表列
    {
        public string 武器類型;
        public string 核心機制;
        public 武器操作配置 操作配置;
    }

    [Serializable]
    public class 武器操作配置
    {
        /// <summary>可能為 JObject 或 JArray，與 JSON 鍵「點擊」對應。</summary>
        public JToken 點擊;

        public 武器長按招式 長按;
        public 武器專屬技能招式 專屬技能;
    }

    [Serializable]
    public class 武器長按招式
    {
        public string 招式名稱;
        public float 動作倍率;
        public string 特性;
        public int 消耗氣刃;
        public float 震動強度;
        public 武器分段倍率 分段倍率;
        public float[] 蓄力時間_秒;
        public float 每秒耐力消耗;
        public float 移動速度降低;
    }

    [Serializable]
    public class 武器分段倍率
    {
        public float 一段;
        public float 二段;
        public float 三段;
    }

    [Serializable]
    public class 武器專屬技能招式
    {
        public string 招式名稱;
        public int 冷卻時間;
        public float 動作倍率;
        public string 描述;
        public int 段數;
    }

    /// <summary>「點擊」為單一物件時可轉成此類型（輔助用）。</summary>
    [Serializable]
    public class 武器點擊單招
    {
        public string 招式名稱;
        public float 動作倍率;
        public string 特效;
        public string 銜接邏輯;
        public int 段數;
        public string 最佳距離;
        public int 氣刃增加;
    }
}
