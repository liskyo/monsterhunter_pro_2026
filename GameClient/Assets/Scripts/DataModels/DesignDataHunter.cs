using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 DesignData/hunter.json（根為物件：套裝規則 + 未齊套裝預設 + 獵人陣列）。</summary>
    [Serializable]
    public class 獵人企劃根
    {
        public 獵人套裝規則 套裝規則;
        public 獵人未齊套裝預設 未齊套裝預設;
        public 獵人企劃列[] 獵人;
    }

    [Serializable]
    public class 獵人套裝規則
    {
        public string 說明;
        public int 每星級基礎數值加成百分比;
        public string 加成公式備註;
    }

    [Serializable]
    public class 獵人未齊套裝預設
    {
        public string 說明;
        public string[] 隨機獵人編號;
        public string[] 隨機出場圖片路徑;
    }

    /// <summary>hunter.json「獵人」陣列單筆（避免與其他領域「獵人資料」混淆）。</summary>
    [Serializable]
    public class 獵人企劃列
    {
        public string 獵人編號;
        public string 對應魔物編號;
        public string[] 套裝防具編號;
        public string 獵人出場圖片路徑;
    }
}
