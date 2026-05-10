using MonsterHunter.DataModels;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 進入戰鬥場景前設定，供 <see cref="MonsterHunter.UI.BattleBackgroundDisplay"/> 等讀取。
    /// 單機／連線主機在載入戰鬥 Scene 前指派，場景內元件於 <c>Start</c> 消耗一次後清空。
    /// </summary>
    public static class HuntSessionContext
    {
        public static 任務資料列 PendingQuest { get; set; }
    }
}
