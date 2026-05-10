using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 DesignData/03_Combat/combat_tuning.json（根為陣列，取 調校鍵==default）。</summary>
    [Serializable]
    public class 戰鬥調校列
    {
        public string 調校鍵;
        public float 移動歸零閾值;
        public float 玩家移動速度;
        public float 魔物追擊速度比例;
        public float 自動尋敵額外射程;
        public float 近戰預設攻擊距離;
        public float 玩家攻擊冷卻秒;
        public float 會心率;
        public float 會心傷害倍率;
        public float 預設物理肉質;
        public float 無屬性克制時屬性肉質;
        public float 動態難度_星級傷害衰減係數;
        public float 待機偵測半徑;
        public float 追擊放棄倍率;
        public float 魔物招式後僵直秒;
        /// <summary>當日第 3 次擊殺完成時，SESSION 難度倍率累加量（須與 DB <c>post_monster_kill_session</c> 邏輯同步）。</summary>
        public float 每日第三擊殺難度倍率增量;

        /// <summary>螢幕下方可操作高度佔比（0–1），對齊 60/40 時建議 0.4。</summary>
        public float 觸控操作區高度比例;

        /// <summary>虛擬搖桿最大拖拽半徑 = min(螢寬,螢高) * 本係數。</summary>
        public float 虛擬搖桿最大半徑_螢幕短邊比;
    }

    [Serializable]
    public class 戰鬥調校陣列根
    {
        public 戰鬥調校列[] 資料;
    }
}
