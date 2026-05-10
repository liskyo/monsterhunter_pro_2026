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
        readonly System.Random _rng = new System.Random();

        public Vector2 MoveInput
        {
            set => _move = value;
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (_attackHitbox != null) _attackHitbox.SetEnabled(false);
        }

        void Update()
        {
            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            if (tuning == null) return;

            if (_attackCd > 0f) _attackCd -= Time.deltaTime;

            var moving = _move.sqrMagnitude > tuning.移動歸零閾值 * tuning.移動歸零閾值;
            _rb.velocity = moving ? _move.normalized * tuning.玩家移動速度 : Vector2.zero;

            if (moving)
            {
                if (_attackHitbox != null) _attackHitbox.SetEnabled(false);
                return;
            }

            if (_loadout == null || string.IsNullOrEmpty(_loadout.武器類型)) return;
            if (!WeaponMovesetRuntime.TryGetTapMoveStats(_loadout.武器類型, _weaponMovesetsJson, out var mv, out var atkRange))
                return;

            if (atkRange <= 0f) atkRange = tuning.近戰預設攻擊距離;

            var target = FindNearestMonster(transform.position, atkRange + tuning.自動尋敵額外射程);
            if (target == null) return;
            if (_attackCd > 0f) return;

            StartCoroutine(AttackRoutine(target, mv, atkRange, tuning));
        }

        MonsterAiController FindNearestMonster(Vector2 from, float maxDist)
        {
            var hits = Physics2D.OverlapCircleAll(from, maxDist, _monsterLayers);
            MonsterAiController best = null;
            var bestD = float.MaxValue;
            foreach (var c in hits)
            {
                var m = c.GetComponentInParent<MonsterAiController>();
                if (m == null) continue;
                var d = ((Vector2)m.transform.position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = m;
                }
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
            // 玩家受傷：接 UI／血量系統
            Debug.Log($"[Player] 受傷 {amount:F1} 會心={isCrit}");
        }
    }
}
