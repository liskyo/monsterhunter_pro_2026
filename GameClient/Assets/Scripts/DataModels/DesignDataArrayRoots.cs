using System;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.DataModels
{
    /// <summary>
    /// JsonUtility 不支援「根節點為陣列」的 JSON。將檔案內容包成 {"資料":[ ... ]} 後即可 FromJson。
    /// 下列類別即對應該物件；欄位名固定為「資料」。
    /// </summary>
    [Serializable]
    public class 魔物資料陣列根
    {
        public 魔物資料列[] 資料;
    }

    [Serializable]
    public class 掉落率資料陣列根
    {
        public 掉落率資料列[] 資料;
    }

    [Serializable]
    public class 裝備資料陣列根
    {
        public 裝備資料列[] 資料;
    }

    [Serializable]
    public class 技能資料陣列根
    {
        public 技能資料列[] 資料;
    }

    [Serializable]
    public class 裝備升級規則陣列根
    {
        public 裝備升級規則列[] 資料;
    }

    [Serializable]
    public class 武器招式表陣列根
    {
        public 武器招式表列[] 資料;
    }

    [Serializable]
    public class 素材資料陣列根
    {
        public 素材資料列[] 資料;
    }

    [Serializable]
    public class 染色球資料陣列根
    {
        public 染色球資料列[] 資料;
    }

    [Serializable]
    public class 魔物痕跡資料陣列根
    {
        public 魔物痕跡資料列[] 資料;
    }

    [Serializable]
    public class 貓飯資料陣列根
    {
        public 貓飯資料列[] 資料;
    }

    [Serializable]
    public class 寵物資料陣列根
    {
        public 寵物資料列[] 資料;
    }

    [Serializable]
    public class 任務資料陣列根
    {
        public 任務資料列[] 資料;
    }

    /// <summary>
    /// 將「根為陣列」的原始 JSON 字串包成陣列根物件後解析（JsonUtility）。
    /// weapon_movesets 含 JToken，仍須 Newtonsoft，見 Parse武器招式表。
    /// </summary>
    public static class DesignDataJsonArrayUtility
    {
        static string WrapArrayJson(string jsonArrayFileContents)
        {
            var t = jsonArrayFileContents?.Trim() ?? "";
            if (t.Length == 0 || t[0] != '[')
                Debug.LogWarning("[DesignDataJsonArrayUtility] 預期根節點為 '[' 的陣列 JSON。");
            return "{\"資料\":" + t + "}";
        }

        public static 魔物資料列[] Parse魔物資料(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<魔物資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<魔物資料列>();
        }

        public static 掉落率資料列[] Parse掉落率(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<掉落率資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<掉落率資料列>();
        }

        public static 裝備資料列[] Parse裝備資料(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<裝備資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<裝備資料列>();
        }

        public static 技能資料列[] Parse技能資料(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<技能資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<技能資料列>();
        }

        public static 裝備升級規則列[] Parse裝備升級規則(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<裝備升級規則陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<裝備升級規則列>();
        }

        /// <summary>內含 JToken，使用 Newtonsoft 解析包裝後 JSON。</summary>
        public static 武器招式表列[] Parse武器招式表(string jsonArrayFileContents)
        {
            var wrapped = WrapArrayJson(jsonArrayFileContents);
            var root = JsonConvert.DeserializeObject<武器招式表陣列根>(wrapped);
            return root?.資料 ?? Array.Empty<武器招式表列>();
        }

        public static 素材資料列[] Parse素材資料(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<素材資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<素材資料列>();
        }

        public static 染色球資料列[] Parse染色球資料(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<染色球資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<染色球資料列>();
        }

        public static 魔物痕跡資料列[] Parse魔物痕跡(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<魔物痕跡資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<魔物痕跡資料列>();
        }

        public static 貓飯資料列[] Parse貓飯資料(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<貓飯資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<貓飯資料列>();
        }

        public static 寵物資料列[] Parse寵物資料(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<寵物資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<寵物資料列>();
        }

        public static 任務資料列[] Parse任務資料(string jsonArrayFileContents)
        {
            var root = JsonUtility.FromJson<任務資料陣列根>(WrapArrayJson(jsonArrayFileContents));
            return root?.資料 ?? Array.Empty<任務資料列>();
        }
    }
}
