using System.Collections.Generic;

namespace MonsterHunter.Combat
{
    /// <summary>戰鬥中部位狀態，供結算時比對 drop_rates「掉落條件」。</summary>
    public sealed class MonsterBreakState
    {
        public bool 尾巴已切斷 { get; private set; }
        public bool 曾破壞部位 { get; private set; }

        public void MarkTailCut() => 尾巴已切斷 = true;

        public void MarkPartBreak() => 曾破壞部位 = true;

        public HashSet<string> ToDropConditions()
        {
            return DropRewardResolver.BuildDefaultConditions(尾巴已切斷, 曾破壞部位);
        }
    }
}
