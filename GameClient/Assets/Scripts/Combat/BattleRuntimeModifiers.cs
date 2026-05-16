using System;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>一趟戰鬥的暫態修正（貓飯、任務詞條、bootstrap 除錯欄位等）。</summary>
    [Serializable]
    public struct BattleRuntimeModifiers
    {
        /// <summary>玩家最大生命值倍率（基於魔物血量公式之上再乘）。</summary>
        public float PlayerMaxHpMultiplier;

        /// <summary>玩家武器結算輸出（含 MV）最終乘法（接近 攻擊力加成 語意）。</summary>
        public float PlayerOutgoingDamageMultiplier;

        /// <summary>魔物企劃最大血量倍率。</summary>
        public float MonsterMaxHpMultiplier;

        /// <summary>玩家移動速度倍率（如加爾克騎乘詞條）。</summary>
        public float PlayerMoveSpeedMultiplier;

        public static BattleRuntimeModifiers Neutral => new BattleRuntimeModifiers
        {
            PlayerMaxHpMultiplier = 1f,
            PlayerOutgoingDamageMultiplier = 1f,
            MonsterMaxHpMultiplier = 1f,
            PlayerMoveSpeedMultiplier = 1f,
        };

        public BattleRuntimeModifiers Clamp()
        {
            return new BattleRuntimeModifiers
            {
                PlayerMaxHpMultiplier = Mathf.Clamp(PlayerMaxHpMultiplier, 0.2f, 5f),
                PlayerOutgoingDamageMultiplier = Mathf.Clamp(PlayerOutgoingDamageMultiplier, 0.2f, 5f),
                MonsterMaxHpMultiplier = Mathf.Clamp(MonsterMaxHpMultiplier, 0.2f, 10f),
                PlayerMoveSpeedMultiplier = Mathf.Clamp(PlayerMoveSpeedMultiplier, 0.5f, 2.5f),
            };
        }
    }
}
