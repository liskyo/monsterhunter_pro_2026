using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 魔物部位受擊盒：掛在帶 Trigger 的碰撞器上，依 <see cref="damageMultiplier"/> 折算後交給根節點 <see cref="MonsterHealth"/>。
    /// </summary>
    public sealed class MonsterHurtbox : MonoBehaviour, IHurtbox
    {
        [Tooltip("部位肉質倍率：例如頭部 1.5、身體 0.8。最終傷害 = 武器基礎傷害 × 此值。")]
        public float damageMultiplier = 1f;

        MonsterHealth _health;

        void Awake()
        {
            CacheHealth();
        }

        void CacheHealth()
        {
            if (_health == null)
                _health = GetComponentInParent<MonsterHealth>();
        }

        /// <inheritdoc />
        public void ApplyWeaponHit(WeaponHitbox source, float baseDamage)
        {
            CacheHealth();
            if (_health == null)
            {
                Debug.LogWarning($"[MonsterHurtbox] 父鏈上找不到 {nameof(MonsterHealth)}，無法扣血：{name}");
                return;
            }

            float finalDamage = baseDamage * damageMultiplier;
            _health.TakeDamage(finalDamage);
        }
    }
}
