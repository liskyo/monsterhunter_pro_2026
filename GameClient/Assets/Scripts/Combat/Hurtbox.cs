using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>受擊判定；大型魔物可於子物件掛多個（頭、翼、尾）。</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hurtbox : MonoBehaviour
    {
        [SerializeField] Collider2D _collider;
        [SerializeField] MonoBehaviour _damageHost;

        public Collider2D Collider => _collider != null ? _collider : (_collider = GetComponent<Collider2D>());
        public IDamageReceiver Receiver => _damageHost as IDamageReceiver ?? GetComponentInParent<IDamageReceiver>();

        void Reset()
        {
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = true;
        }
    }
}
