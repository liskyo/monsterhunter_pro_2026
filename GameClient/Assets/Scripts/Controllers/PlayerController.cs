using System;
using System.Collections;
using MonsterHunter.Combat;
using MonsterHunter.DataModels;
using UnityEngine;

namespace MonsterHunter.Controllers
{
    /// <summary>
    /// 動靜結合：移動中不攻擊；靜止時自動尋找最近魔物並在招式距離內攻擊。
    /// 動作倍率／距離來自 weapon_movesets.json；調校來自 combat_tuning.json。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour, IDamageReceiver
    {
        [SerializeField] CombatTuningStore _tuningStore;
        [SerializeField] PlayerCombatLoadout _loadout;
        [SerializeField] TextAsset _weaponMovesetsJson;
        [SerializeField] LayerMask _monsterLayers;
        [SerializeField] Hitbox _attackHitbox;

        Rigidbody2D _rb;
        Vector2 _move;
        float _attackCd;
        float _dodgeCd;
        float _dodgeTimer;
        Vector2 _dodgeVelocity;
        readonly System.Random _rng = new System.Random();

        // ── 執行期注入 ──
        string _weaponMovesetsJsonText;
        MonsterAiController _directTarget;

        public float MaxHp { get; private set; } = 150f;
        public float CurrentHp { get; private set; } = 150f;

        /// <summary>受傷事件：(傷害量, 是否會心)</summary>
        public event Action<float, bool> OnDamageReceived;
        /// <summary>死亡事件</summary>
        public event Action OnDefeated;

        public Vector2 MoveInput { set => _move = value; }
        /// <summary>供外部讀取上一幀的移動方向（閃避方向判斷用）。</summary>
        public Vector2 MoveInputSnapshot => _move;

        /// <summary>閃避中（無敵幀有效期）。</summary>
        public bool IsDodging => _dodgeTimer > 0f;

        /// <summary>
        /// 觸發閃避衝刺，方向為 dir（零向量時退後離開魔物）。
        /// 閃避冷卻期間呼叫無效。
        /// </summary>
        public void TryDodge(Vector2 dir)
        {
            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            if (tuning == null) return;
            if (_dodgeCd > 0f) return;

            var dodgeDist = tuning.閃避距離 > 0f ? tuning.閃避距離 : 4f;
            var invincSec = tuning.閃避無敵秒 > 0f ? tuning.閃避無敵秒 : 0.4f;
            var cooldown  = tuning.閃避冷卻秒 > 0f ? tuning.閃避冷卻秒 : 1.2f;

            // 方向：優先傳入值，否則用當前移動方向，都沒有則向後
            if (dir.sqrMagnitude < 0.01f) dir = _move;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector2(-1f, 0f);
            dir.Normalize();

            var duration = invincSec;
            _dodgeVelocity = dir * (dodgeDist / duration);
            _dodgeTimer    = duration;
            _dodgeCd       = cooldown;
        }

        /// <summary>BattleCombatManager 注入依賴（不需要 Inspector 拖拉）。</summary>
        public void Inject(CombatTuningStore ts, PlayerCombatLoadout loadout, string weaponMovesetsJson, float maxHp = 1000f)
        {
            _tuningStore = ts;
            _loadout = loadout;
            _weaponMovesetsJsonText = weaponMovesetsJson;
            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        /// <summary>直接指定攻擊目標，跳過 Physics2D 圓形掃描。</summary>
        public void SetDirectTarget(MonsterAiController monster) => _directTarget = monster;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if ((object)_rb == null || _rb.Equals(null))
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
                _rb.gravityScale = 0f;
                _rb.freezeRotation = true;
            }
            if (_attackHitbox != null) _attackHitbox.SetEnabled(false);
        }

        void Update()
        {
            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            if (tuning == null) return;

            if (_attackCd > 0f) _attackCd -= Time.deltaTime;
            if (_dodgeCd   > 0f) _dodgeCd  -= Time.deltaTime;

            // 閃避衝刺（無敵幀期間接管移動）
            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= Time.deltaTime;
                _rb.linearVelocity = _dodgeVelocity;
                if (_attackHitbox != null) _attackHitbox.SetEnabled(false);
                return;
            }

            var moving = _move.sqrMagnitude > tuning.移動歸零閾值 * tuning.移動歸零閾值;
            _rb.linearVelocity = moving ? _move.normalized * tuning.玩家移動速度 : Vector2.zero;

            if (moving)
            {
                if (_attackHitbox != null) _attackHitbox.SetEnabled(false);
                return;
            }

            if (_loadout == null || string.IsNullOrEmpty(_loadout.武器類型)) return;

            float mv, atkRange;
            bool gotMoves;
            if (!string.IsNullOrEmpty(_weaponMovesetsJsonText))
                gotMoves = WeaponMovesetRuntime.TryGetTapMoveStats(_loadout.武器類型, _weaponMovesetsJsonText, out mv, out atkRange);
            else if (_weaponMovesetsJson != null)
                gotMoves = WeaponMovesetRuntime.TryGetTapMoveStats(_loadout.武器類型, _weaponMovesetsJson, out mv, out atkRange);
            else
            { mv = 0.45f; atkRange = 4.5f; gotMoves = true; }
            if (!gotMoves) return;

            if (atkRange <= 0f) atkRange = tuning.近戰預設攻擊距離;

            var target = FindNearestMonster(transform.position, atkRange + tuning.自動尋敵額外射程);
            if (target == null) return;
            if (_attackCd > 0f) return;

            StartCoroutine(AttackRoutine(target, mv, atkRange, tuning));
        }

        MonsterAiController FindNearestMonster(Vector2 from, float maxDist)
        {
            // 優先使用直接指定目標（BattleCombatManager 注入）
            if (_directTarget != null && _directTarget.isActiveAndEnabled)
            {
                var d = Vector2.Distance(from, _directTarget.transform.position);
                return d <= maxDist ? _directTarget : null;
            }

            var hits = Physics2D.OverlapCircleAll(from, maxDist, _monsterLayers);
            MonsterAiController best = null;
            var bestD = float.MaxValue;
            foreach (var c in hits)
            {
                var m = c.GetComponentInParent<MonsterAiController>();
                if (m == null) continue;
                var d2 = ((Vector2)m.transform.position - from).sqrMagnitude;
                if (d2 < bestD) { bestD = d2; best = m; }
            }
            return best;
        }

        IEnumerator AttackRoutine(MonsterAiController target, float mv, float range, 戰鬥調校列 tuning)
        {
            _attackCd = tuning.玩家攻擊冷卻秒;
            if (_attackHitbox != null) _attackHitbox.SetEnabled(true);

            var dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist > range + tuning.自動尋敵額外射程)
            {
                if (_attackHitbox != null) _attackHitbox.SetEnabled(false);
                yield break;
            }

            var mRow = target.DataRow;
            MonsterStatResolver.ResolveHzv(mRow, _loadout.武器屬性標籤, tuning, out var physHzv, out var elemHzv);
            var diffMul = DamageCalculator.ComputeDynamicDifficultyMultiplier(
                mRow != null ? mRow.星級 : 1,
                tuning
            );

            var input = new DamageCalculator.DamageCalculationInput
            {
                武器基礎物理 = _loadout.武器基礎物理,
                武器屬性 = _loadout.武器屬性,
                動作值MV = mv,
                物理肉質HZV = physHzv,
                屬性肉質HZV = elemHzv,
                動態難度對玩家輸出倍率 = diffMul,
                會心率 = tuning.會心率,
                會心傷害倍率 = tuning.會心傷害倍率,
            };

            var dmg = DamageCalculator.ComputeFinalDamage(input, _rng, out var crit);
            target.ApplyDamage(dmg, crit);

            yield return null;
            if (_attackHitbox != null) _attackHitbox.SetEnabled(false);
        }

        public void ApplyDamage(float amount, bool isCrit)
        {
            if (CurrentHp <= 0f) return;
            if (IsDodging) { Debug.Log("[Player] 閃避成功！傷害無效"); return; }
            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
            OnDamageReceived?.Invoke(amount, isCrit);
            Debug.Log($"[Player] 受傷 {amount:F1} 會心={isCrit} → HP {CurrentHp:F0}/{MaxHp:F0}");
            if (CurrentHp <= 0f) OnDefeated?.Invoke();
        }
    }
}
