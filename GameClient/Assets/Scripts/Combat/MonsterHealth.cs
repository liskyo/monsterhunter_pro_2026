using System;
using System.Collections;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 魔物血量：2D／部位受擊走 <see cref="TakeDamage(float)"/>；
    /// 教學用方塊可改呼叫 <see cref="TakeDamage(int)"/>（會輸出「砍中了！剩餘血量」並可作受擊閃紅）。
    /// </summary>
    public sealed class MonsterHealth : MonoBehaviour
    {
        [SerializeField] float maxHp = 100f;

        [Header("（選用）受擊視覺")]
        [SerializeField] Renderer _damageFlashRenderer;
        [SerializeField] Color _damageFlashTint = Color.red;
        [SerializeField] float _damageFlashHoldSeconds = 0.1f;

        float _currentHp;
        bool _depletedInvoked;

        Color? _flashBaseTint;
        Coroutine _flashRoutine;

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
            EnsureCapacityInitialized();

            CacheRendererDefault();
        }

        void EnsureCapacityInitialized()
        {
            if (_currentHp <= 0f && maxHp > 0f)
            {
                _currentHp = maxHp;
                _depletedInvoked = false;
            }
        }

        void CacheRendererDefault()
        {
            if (_damageFlashRenderer == null)
                _damageFlashRenderer = GetComponent<Renderer>()
                    ?? GetComponentInChildren<Renderer>();

            if (_damageFlashRenderer == null || _damageFlashRenderer.material == null)
                return;

            try
            {
                _flashBaseTint = _damageFlashRenderer.material.color;
            }
            catch
            {
                _flashBaseTint = null;
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

        /// <summary>integer 入口：適合 WeaponHitbox 教學；成功扣血會閃紅並在 Console 顯示剩餘血量。</summary>
        public void TakeDamage(int damageAmount)
        {
            float amount = Mathf.Max(0f, damageAmount);
            if (ApplyDamageCore(amount, isCrit: false) && amount > 0f)
                OnTutorialStyleHitApplied();
        }

        /// <summary>受到傷害（一般戰鬥流程）。</summary>
        public void TakeDamage(float amount, bool isCrit = false)
        {
            ApplyDamageCore(amount, isCrit);
        }

        bool ApplyDamageCore(float amount, bool isCrit)
        {
            if (amount <= 0f || !IsAlive)
                return false;

            _currentHp = Mathf.Max(0f, _currentHp - amount);
            DamageApplied?.Invoke(amount, isCrit);

            if (_currentHp <= 0f && !_depletedInvoked)
            {
                _depletedInvoked = true;
                HpDepleted?.Invoke();
            }

            return true;
        }

        void OnTutorialStyleHitApplied()
        {
            TriggerDamageFlashRoutine();
            Debug.Log($"砍中了！剩餘血量：{_currentHp:0}", this);
        }

        void TriggerDamageFlashRoutine()
        {
            if (_damageFlashRenderer == null || _damageFlashRenderer.material == null)
                return;

            if (_flashBaseTint == null)
                CacheRendererDefault();

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);

            _flashRoutine = StartCoroutine(HitFlashRoutine());
        }

        IEnumerator HitFlashRoutine()
        {
            var mat = _damageFlashRenderer.material;
            Color baseTint = _flashBaseTint ?? mat.color;
            mat.color = _damageFlashTint;
            yield return new WaitForSeconds(_damageFlashHoldSeconds);
            mat.color = baseTint;
            _flashRoutine = null;
        }
    }
}
