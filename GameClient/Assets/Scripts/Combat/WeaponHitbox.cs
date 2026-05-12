using System.Collections;
using System.Collections.Generic;
using MonsterHunter.Core;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 武器攻擊盒（3D）：掛在 mixamorig:RightHand／WeaponHitbox 等子物件。
    /// <list type="bullet">
    ///   <item>需 <see cref="BoxCollider"/>（Is Trigger）＋ Kinematic <see cref="Rigidbody"/>（Trigger 偵測較穩）。</item>
    ///   <item>撞擊階層含 Tag <b>Monster</b> 的物件時，優先找 <see cref="MonsterHealth"/> 呼叫 <c>TakeDamage(int)</c>；否則走 <see cref="IHurtbox"/>（部位倍率）。</item>
    ///   <item><see cref="isAttacking"/>：僅在攻擊窗內結算，避免走路誤觸；<see cref="EnableHitbox"/>／<see cref="DisableHitbox"/> 會同步此旗標。</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WeaponHitbox : MonoBehaviour
    {
        const string MonsterTag = "Monster";

        [SerializeField] BoxCollider _box;
        [Tooltip("傳給 MonsterHealth.TakeDamage(int) 或 IHurtbox 的基礎值")]
        [SerializeField] float damage = 10f;

        [Header("攻擊窗口")]
        [Tooltip("勾選時：僅 isAttacking 為 true 才結算命中（建議 Y Bot 勾選）。")]
        [SerializeField] bool _requireAttackFlag = true;

        [Tooltip("僅在攻擊動畫／EnableHitbox 視窗內為 true；可由 PlayerController 或 Animation Event 開關。")]
        public bool isAttacking;

        [Header("無動畫揮擊（MonsterNavDemo 等）")]
        [SerializeField] bool _useSimpleAttackInput;
        [SerializeField] KeyCode _attackKey = KeyCode.Mouse0;
        [SerializeField] float _swingActiveDuration = 0.12f;
        [SerializeField] bool _triggerHitstopOnHit = true;

        Coroutine _simpleSwingRoutine;

        /// <summary>本次揮擊已結算過的目標（MonsterHealth 或 IHurtbox 元件 InstanceID）。</summary>
        readonly HashSet<int> _hitTargetsThisSwing = new HashSet<int>();

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

            isAttacking = false;
        }

        void Update()
        {
            if (!_useSimpleAttackInput)
                return;
            if (Input.GetKeyDown(_attackKey))
            {
                if (_simpleSwingRoutine != null)
                    StopCoroutine(_simpleSwingRoutine);
                _simpleSwingRoutine = StartCoroutine(CoSimpleSwing());
            }
        }

        IEnumerator CoSimpleSwing()
        {
            EnableHitbox();
            yield return new WaitForSeconds(_swingActiveDuration);
            DisableHitbox();
            _simpleSwingRoutine = null;
        }

        /// <summary>MonsterNavDemo：Primitive 無 Animator 時由程式開關判定幀。</summary>
        public void ConfigureNavDemoSwing(
            float baseDamage,
            KeyCode attackKey,
            float activeDurationSeconds,
            bool triggerHitstopOnHit = false)
        {
            damage = baseDamage;
            _attackKey = attackKey;
            _swingActiveDuration = activeDurationSeconds;
            _useSimpleAttackInput = true;
            _triggerHitstopOnHit = triggerHitstopOnHit;
        }

        /// <summary>動畫事件或 PlayerController：揮刀開始；清空本刀命中記錄並開啟碰撞。</summary>
        public void EnableHitbox()
        {
            _hitTargetsThisSwing.Clear();
            isAttacking = true;
            if (Box != null)
                Box.enabled = true;
        }

        /// <summary>動畫事件或 PlayerController：揮刀結束；關閉碰撞。</summary>
        public void DisableHitbox()
        {
            isAttacking = false;
            if (Box != null)
                Box.enabled = false;
        }

        /// <summary>只做旗標／同步用（碰撞仍依 BoxCollider.enabled）。</summary>
        public void SetAttackWindow(bool active)
        {
            isAttacking = active;
        }

        void OnTriggerEnter(Collider other)
        {
            if (Box == null || !Box.enabled)
                return;

            if (_requireAttackFlag && !isAttacking)
                return;

            if (!IsUnderMonsterTag(other.transform))
                return;

            int dmgInt = Mathf.Max(1, Mathf.RoundToInt(damage));

            var health = other.GetComponent<MonsterHealth>()
                ?? other.GetComponentInParent<MonsterHealth>();
            if (health != null)
            {
                int id = ((MonoBehaviour)health).GetInstanceID();
                if (!_hitTargetsThisSwing.Add(id))
                    return;

                health.TakeDamage(dmgInt);
                if (_triggerHitstopOnHit)
                    CombatFeedbackManager.Instance?.TriggerHitstop(0.1f);
                return;
            }

            var hurt = other.GetComponent<IHurtbox>()
                ?? other.GetComponentInParent<IHurtbox>();
            if (hurt == null)
                return;

            var hurtMb = hurt as MonoBehaviour;
            if (hurtMb == null)
                return;

            int hid = hurtMb.GetInstanceID();
            if (!_hitTargetsThisSwing.Add(hid))
                return;

            hurt.ApplyWeaponHit(this, damage);
            if (_triggerHitstopOnHit)
                CombatFeedbackManager.Instance?.TriggerHitstop(0.1f);

            float dealt = damage * (hurt is MonsterHurtbox mh ? mh.damageMultiplier : 1f);
            Debug.Log($"[WeaponHitbox] 擊中 {other.transform.root.name}（IHurtbox: {hurtMb.GetType().Name}），基礎傷害 {damage} → 結算 {dealt:0.##}");
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
