using Newtonsoft.Json;

namespace MonsterHunter.DataModels
{
    /// <summary>
    /// DesignData JSON 還原輔助。weapon_movesets 需 Newtonsoft（「點擊」為物件或陣列）。
    /// 若專案已有 Packages/manifest.json，請合併加入 com.unity.nuget.newtonsoft-json，勿覆蓋既有依賴。
    /// </summary>
    public static class DesignDataDeserialize
    {
        /// <summary>根節點為陣列 [ ... ] 的 weapon_movesets.json 全文。</summary>
        public static 武器招式表列[] WeaponMovesetsFromJson(string jsonArrayFileContents) =>
            DesignDataJsonArrayUtility.Parse武器招式表(jsonArrayFileContents);

        /// <summary>已包成 {"資料":[...]} 的 JSON（例如自訂存檔格式）。</summary>
        public static 武器招式表列[] WeaponMovesetsFromWrappedJson(string jsonObjectWith資料鍵) =>
            JsonConvert.DeserializeObject<武器招式表陣列根>(jsonObjectWith資料鍵)?.資料
            ?? System.Array.Empty<武器招式表列>();
    }
}
