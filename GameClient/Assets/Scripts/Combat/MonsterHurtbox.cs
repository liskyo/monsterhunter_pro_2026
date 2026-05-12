using System.Collections;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 魔物部位受擊盒：掛在帶 Trigger 的碰撞器上。優先交給父鏈 <see cref="MonsterHealth"/>（2D 預覽戰），
    /// 否則改交 <see cref="IDamageReceiver"/>（例如 3D <see cref="Monster.MonsterController"/>，血量來自 DesignData/01_Monsters/monsters.json）。
    /// </summary>
    public sealed class MonsterHurtbox : MonoBehaviour, IHurtbox
    {
        [Tooltip("部位肉質倍率：最終傷害 = 武器基礎傷害 × 此值。")]
        public float damageMultiplier = 1f;

        [Header("命中回饋（示範）")]
        [SerializeField] float _hitFlashSeconds = 0.1f;
        [SerializeField] Color _flashColor = Color.red;

        MonsterHealth _health;
        IDamageReceiver _damageReceiver;

        Renderer _cachedRenderer;
        Color _baseColor;
        bool _cachedBaseColor;
        Coroutine _flashRoutine;

        void Awake()
        {
            CacheTargets();
            CacheRendererForFlash();
        }

        void CacheTargets()
        {
            if (_health == null)
                _health = GetComponentInParent<MonsterHealth>();
            if (_damageReceiver == null)
                _damageReceiver = GetComponentInParent<IDamageReceiver>();
        }

        void CacheRendererForFlash()
        {
            if (_cachedRenderer != null)
                return;
            _cachedRenderer = GetComponentInParent<Renderer>();
            if (_cachedRenderer != null)
            {
                _baseColor = _cachedRenderer.material.color;
                _cachedBaseColor = true;
            }
        }

        /// <inheritdoc />
        public void ApplyWeaponHit(WeaponHitbox source, float baseDamage)
        {
            CacheTargets();

            float finalDamage = baseDamage * damageMultiplier;
            if (finalDamage <= 0f)
                return;

            if (_health != null)
            {
                _health.TakeDamage(finalDamage);
                Debug.Log(
                    $"[MonsterHurtbox] {_health.gameObject.name} 受擊（MonsterHealth）−{finalDamage:0.##}（肉質×{damageMultiplier}）",
                    this);
            }
            else if (_damageReceiver != null)
            {
                _damageReceiver.ApplyDamage(finalDamage, false);
                var mc = _damageReceiver as MonsterHunter.Monster.MonsterController;
                if (mc != null)
                    Debug.Log(
                        $"[MonsterHurtbox] {mc.DisplayName}（{mc.MonsterId}）受擊 −{finalDamage:0.##} HP，剩餘 {mc.CurrentHp:0.##}/{mc.MaxHp:0.##}",
                        this);
                else
                    Debug.Log($"[MonsterHurtbox] {name} 受擊（IDamageReceiver）−{finalDamage:0.##}", this);
            }
            else
            {
                Debug.LogWarning(
                    $"[MonsterHurtbox] 父鏈上找不到 {nameof(MonsterHealth)} 或 {nameof(IDamageReceiver)}，無法扣血：{name}",
                    this);
                return;
            }

            PlayHitFlash();
        }

        void PlayHitFlash()
        {
            CacheRendererForFlash();
            if (_cachedRenderer == null || !_cachedBaseColor)
                return;

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            _cachedRenderer.material.color = _flashColor;
            yield return new WaitForSeconds(_hitFlashSeconds);
            _cachedRenderer.material.color = _baseColor;
            _flashRoutine = null;
        }
    }
}
