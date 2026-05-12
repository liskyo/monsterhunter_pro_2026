using System.Collections.Generic;
using MonsterHunter.Core;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 武器攻擊盒（3D）：掛在武器子物件上，搭配 <see cref="BoxCollider"/>（Is Trigger）。
    /// 請在 Animator 揮刀有效幀用 Animation Event 呼叫 <see cref="EnableHitbox"/> / <see cref="DisableHitbox"/>。
    /// </summary>
    /// <remarks>
    /// 本專案戰鬥預覽多為 2D 碰撞（<c>Collider2D</c>）。若場上僅有 2D 魔物碰撞器，
    /// 3D 的 <c>OnTriggerEnter</c> 不會與之互動；屆時請改為 <c>BoxCollider2D</c> + <c>OnTriggerEnter2D</c>，
    /// 或統一將魔物改為 3D Trigger。以下維持需求指定的 3D 寫法。
    /// </remarks>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WeaponHitbox : MonoBehaviour
    {
        const string MonsterTag = "Monster";

        [SerializeField] BoxCollider _box;
        [Tooltip("單次揮刀對同一 IHurtbox（同一部位）只結算一次")]
        [SerializeField] float damage = 10f;

        /// <summary>本次「攻擊開啟區間」已經打過的 Hurtbox 實例（以元件 InstanceID 去重）。</summary>
        readonly HashSet<int> _hitHurtboxIdsThisSwing = new HashSet<int>();

        public BoxCollider Box => _box != null ? _box : (_box = GetComponent<BoxCollider>());

        void Reset()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        void Awake()
        {
            if (_box == null)
                _box = GetComponent<BoxCollider>();
            if (_box != null)
            {
                _box.isTrigger = true;
                _box.enabled = false;
            }
        }

        /// <summary>Animation Event：揮刀開始有效判定幀時呼叫；會清空本刀已命中記錄並開啟碰撞。</summary>
        public void EnableHitbox()
        {
            _hitHurtboxIdsThisSwing.Clear();
            if (Box != null)
                Box.enabled = true;
        }

        /// <summary>Animation Event：揮刀結束有效判定幀時呼叫；關閉碰撞。</summary>
        public void DisableHitbox()
        {
            if (Box != null)
                Box.enabled = false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (Box == null || !Box.enabled)
                return;

            if (!IsUnderMonsterTag(other.transform))
                return;

            var hurt = other.GetComponent<IHurtbox>() ?? other.GetComponentInParent<IHurtbox>();
            if (hurt == null)
                return;

            // 以「實作 IHurtbox 的元件」為部位去重單位，避免同一刀多段Collider重複扣血
            var hurtMb = hurt as MonoBehaviour;
            if (hurtMb == null)
                return;

            int id = hurtMb.GetInstanceID();
            if (!_hitHurtboxIdsThisSwing.Add(id))
                return;

            hurt.ApplyWeaponHit(this, damage);
            CombatFeedbackManager.Instance.TriggerHitstop(0.1f);
            Debug.Log("擊中魔物！");
        }

        static bool IsUnderMonsterTag(Transform t)
        {
            for (Transform x = t; x != null; x = x.parent)
            {
                if (x.CompareTag(MonsterTag))
                    return true;
            }

            return false;
        }
    }
}
