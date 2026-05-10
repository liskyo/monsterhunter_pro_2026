namespace MonsterHunter.UI
{
    /// <summary>
    /// 進入村莊／大廳 UI 場景前設定，供 <see cref="VillageBackgroundUiDisplay"/> 讀取。
    /// 值須與 <c>Assets/UI/Backgrounds/Village/</c> 下檔名一致（不含「_背景.png」）。
    /// </summary>
    public static class VillageSessionContext
    {
        public static string PendingBackgroundKey { get; set; }
    }
}
