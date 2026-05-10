using System;
using MonsterHunter.DataModels;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 傷害公式（.cursorrules）：最終 = (武器基礎物理 * MV * 物理HZV) + (武器屬性 * 屬性HZV)，
    /// 再乘「動態難度倍率」、會心。
    /// HZV 與星級係數由 <see cref="MonsterStatResolver"/> / 調校 JSON 提供，不在此硬編碼。
    /// </summary>
    public static class DamageCalculator
    {
        [Serializable]
        public struct DamageCalculationInput
        {
            public float 武器基礎物理;
            public float 武器屬性;
            public float 動作值MV;
            public float 物理肉質HZV;
            public float 屬性肉質HZV;
            public float 動態難度對玩家輸出倍率;
            public float 會心率;
            public float 會心傷害倍率;
        }

        /// <summary>
        /// 動態難度：星級越高，玩家對魔物有效輸出略降（係數存於 JSON）。
        /// 倍率 = 1 / (1 + k * max(0, 星級 - 1))。
        /// </summary>
        public static float ComputeDynamicDifficultyMultiplier(int monsterStars, 戰鬥調校列 tuning)
        {
            if (tuning == null) return 1f;
            var k = tuning.動態難度_星級傷害衰減係數;
            var s = Mathf.Max(0, monsterStars - 1);
            return 1f / (1f + k * s);
        }

        public static bool RollCrit(float critRate, System.Random rng)
        {
            if (critRate <= 0f) return false;
            return rng.NextDouble() < critRate;
        }

        /// <summary>回傳最終傷害（已含會心與動態難度倍率）。</summary>
        public static float ComputeFinalDamage(DamageCalculationInput input, System.Random rng, out bool isCrit)
        {
            var phys = input.武器基礎物理 * input.動作值MV * input.物理肉質HZV;
            var elem = input.武器屬性 * input.屬性肉質HZV;
            var sum = phys + elem;
            sum *= Mathf.Max(0f, input.動態難度對玩家輸出倍率);

            isCrit = RollCrit(input.會心率, rng);
            if (isCrit)
                sum *= Mathf.Max(1f, input.會心傷害倍率);

            return Mathf.Max(0f, sum);
        }
    }
}
