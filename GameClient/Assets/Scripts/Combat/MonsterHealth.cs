using System;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 魔物血量（單一真相來源）：所有對魔物的扣血最終都應經由此類別。
    /// <see cref="MonsterAiController"/>（玩家自動攻擊）、<see cref="MonsterHurtbox"/>（武器判定）皆呼叫 <see cref="TakeDamage"/>。
    /// </summary>
    public sealed class MonsterHealth : MonoBehaviour
    {
        [SerializeField] float maxHp = 100f;

        float _currentHp;
        bool _depletedInvoked;

        /// <summary>目前血量。</summary>
        public float CurrentHp => _currentHp;

        /// <summary>最大血量。</summary>
        public float MaxHp => maxHp;

        /// <summary>是否仍存活（血量 &gt; 0）。</summary>
        public bool IsAlive => _currentHp > 0f;

        /// <summary>成功扣血後觸發（數值為本次實際扣除量，可能與傳入相同；isCrit 供 HUD／浮字）。</summary>
        public event Action<float, bool> DamageApplied;

        /// <summary>血量首次歸零時觸發一次（死亡／討伐流程入口）。</summary>
        public event Action HpDepleted;

        void Awake()
        {
            if (_currentHp <= 0f && maxHp > 0f)
            {
                _currentHp = maxHp;
                _depletedInvoked = false;
            }
        }

        /// <summary>執行期設定最大血量（例如從 monsters.json 注入）。</summary>
        public void Initialize(float maximumHp, bool refillCurrent = true)
        {
            maxHp = Mathf.Max(0f, maximumHp);
            _depletedInvoked = false;
            if (refillCurrent)
                _currentHp = maxHp;
            else
                _currentHp = Mathf.Min(_currentHp, maxHp);
        }

        /// <summary>受到傷害（最終數值；可選會心供介面顯示）。</summary>
        public void TakeDamage(float amount, bool isCrit = false)
        {
            if (amount <= 0f || !IsAlive)
                return;

            _currentHp = Mathf.Max(0f, _currentHp - amount);
            DamageApplied?.Invoke(amount, isCrit);

            if (_currentHp <= 0f && !_depletedInvoked)
            {
                _depletedInvoked = true;
                HpDepleted?.Invoke();
            }
        }
    }
}
