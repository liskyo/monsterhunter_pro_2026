using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>攻擊判定：僅負責觸發，與 Hurtbox 分離。</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField] Collider2D _collider;

        public Collider2D Collider => _collider != null ? _collider : (_collider = GetComponent<Collider2D>());

        void Reset()
        {
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = true;
        }

        public void SetEnabled(bool on)
        {
            if (Collider != null) Collider.enabled = on;
        }
    }
}
