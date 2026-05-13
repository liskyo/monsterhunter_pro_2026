using System;
using System.Collections;
using System.Collections.Generic;
using MonsterHunter.Combat;
using MonsterHunter.DataModels;
using MonsterHunter.UI;
using UnityEngine;

namespace MonsterHunter.Controllers
{
    /// <summary>
    /// 直立割草輸出手感：連段（招式表點擊陣列循環）、長按蓄力、專屬技冷卻；靜止才攻／蓄／技，移動中清空連段並取消蓄力。
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

        struct 獵人持續傷害狀態
        {
            public string 異常名稱;
            public float 每秒傷害;
            public float 結束時間;
        }

        readonly List<獵人持續傷害狀態> _ailments = new List<獵人持續傷害狀態>(4);
        float _dotHudTick;

        string _weaponMovesetsJsonText;
        MonsterAiController _directTarget;
        PortraitCombatTouchInput _touchInput;

        WeaponMovesetRuntime.ParsedMoveset _moves;

        float _playerOutgoingDamageMul = 1f;
        float _comboResetTimer;
        int _comboIndex;
        float _skillCd;

        float _chargeSeconds;
        bool _chargeHeldPrevFrame;
        bool _strikeCoroutineActive;

        public float MaxHp { get; private set; } = 150f;
        public float CurrentHp { get; private set; } = 150f;

        public event Action<float, bool> OnDamageReceived;

        public event Action OnDefeated;

        public Vector2 MoveInput { set => _move = value; }
        public Vector2 MoveInputSnapshot => _move;

        public bool IsDodging => _dodgeTimer > 0f;

        public void TryDodge(Vector2 dir)
        {
            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            if (tuning == null) return;
            if (_dodgeCd > 0f) return;

            var dodgeDist = tuning.閃避距離 > 0f ? tuning.閃避距離 : 4f;
            var invincSec = tuning.閃避無敵秒 > 0f ? tuning.閃避無敵秒 : 0.4f;
            var cooldown  = tuning.閃避冷卻秒 > 0f ? tuning.閃避冷卻秒 : 1.2f;

            if (dir.sqrMagnitude < 0.01f) dir = _move;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector2(-1f, 0f);
            dir.Normalize();

            var duration = invincSec;
            _dodgeVelocity = dir * (dodgeDist / Mathf.Max(duration, 0.05f));
            _dodgeTimer    = duration;
            _dodgeCd       = cooldown;
        }

        public void Inject(
            CombatTuningStore ts,
            PlayerCombatLoadout loadout,
            string weaponMovesetsJson,
            float maxHp = 1000f,
            PortraitCombatTouchInput touchInputForCombat = null,
            float outgoingDamageMultiplier = 1f)
        {
            _tuningStore              = ts;
            _loadout                  = loadout;
            _touchInput               = touchInputForCombat;
            _weaponMovesetsJsonText   = weaponMovesetsJson;
            _playerOutgoingDamageMul  = Mathf.Clamp(outgoingDamageMultiplier, 0.2f, 5f);
            MaxHp     = Mathf.Max(1f, maxHp);
            CurrentHp = MaxHp;

            RefreshMoveset();
        }

        public void RefreshMoveset()
        {
            _moves = null;
            var tun = _tuningStore != null ? _tuningStore.Active : null;
            var melee = tun != null && tun.近戰預設攻擊距離 > 0.05f ? tun.近戰預設攻擊距離 : 4f;
            var gate  = tun != null && tun.分段蓄力最小門檻秒 > 1e-3f ? tun.分段蓄力最小門檻秒 : 0.28f;
            var sigR  = tun != null && tun.專屬技預設攻擊距離 > 1e-3f ? tun.專屬技預設攻擊距離 : 5.5f;

            string json = null;
            if (!string.IsNullOrEmpty(_weaponMovesetsJsonText))
                json = _weaponMovesetsJsonText;
            else if (_weaponMovesetsJson != null)
                json = _weaponMovesetsJson.text;

            if (_loadout == null || string.IsNullOrEmpty(_loadout.武器類型) || string.IsNullOrEmpty(json))
                return;

            WeaponMovesetRuntime.TryParseMoveset(_loadout.武器類型, json, melee, gate, sigR, out _moves);
        }

        public void SetOutgoingDamageMultiplier(float m) =>
            _playerOutgoingDamageMul = Mathf.Clamp(m, 0.2f, 5f);

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

            TickActiveAilments();

            if (_attackCd > 0f) _attackCd -= Time.deltaTime;
            if (_dodgeCd   > 0f) _dodgeCd -= Time.deltaTime;
            if (_skillCd   > 0f) _skillCd -= Time.deltaTime;

            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= Time.deltaTime;
                _rb.linearVelocity = _dodgeVelocity;
                if (_attackHitbox != null) _attackHitbox.SetEnabled(false);
                return;
            }

            var moving = _move.sqrMagnitude > tuning.移動歸零閾值 * tuning.移動歸零閾值;
            _rb.linearVelocity = moving ? _move.normalized * tuning.玩家移動速度 : Vector2.zero;

            if (_attackHitbox != null && !_strikeCoroutineActive)
                _attackHitbox.SetEnabled(false);

            if (moving)
            {
                _comboIndex = 0;
                _comboResetTimer       = 0f;
                _chargeSeconds = 0f;
                _chargeHeldPrevFrame   = false;
                return;
            }

            var stationary = !moving && CurrentHp > 0f;

            if (!stationary || _strikeCoroutineActive || _loadout == null ||
                string.IsNullOrEmpty(_loadout.武器類型))
                return;

            var comboDecay = Mathf.Max(0.3f, tuning.連段重置秒);
            _comboResetTimer -= Time.deltaTime;
            if (_comboResetTimer <= 0f)
                _comboIndex = 0;

            if (ConsumeSkillPulse() && TryStartSkillStrike(tuning))
                return;

            var chargeHeld    = GestureChargeHeld();
            var hasChargeStages = _moves?.Charge != null && (_moves.Charge?.門檻秒?.Count ?? 0) > 0;

            if (hasChargeStages)
            {
                if (chargeHeld)
                {
                    _comboResetTimer = comboDecay;
                    _chargeSeconds += Time.deltaTime;
                    _chargeHeldPrevFrame = true;
                    return;
                }

                if (_chargeHeldPrevFrame && !chargeHeld)
                {
                    TryReleaseCharge(tuning);
                    _chargeHeldPrevFrame = false;
                    _chargeSeconds = 0f;
                }
                else
                    _chargeHeldPrevFrame = chargeHeld;
            }
            else
            {
                _chargeSeconds       = 0f;
                _chargeHeldPrevFrame = false;
            }

            if (_attackCd > 0f)
                return;

            var atkRangeGuess = FallbackReach(tuning);

            var target =
                FindNearestMonster(transform.position, atkRangeGuess + tuning.自動尋敵額外射程);
            if (target == null || !target.isActiveAndEnabled)
                return;

            var taps = TapChainOrSynthetic(tuning);

            StartCoroutine(BeginAutoComboSwing(tuning, target, taps[_comboIndex % taps.Length]));
        }

        WeaponMovesetRuntime.TapComboStep[] TapChainOrSynthetic(戰鬥調校列 tun)
        {
            if (_moves != null && _moves.TapChain.Length > 0)
                return _moves.TapChain;
            float mv = FallbackTapMv(tun), rg = tun.近戰預設攻擊距離;
            if (!string.IsNullOrEmpty(_weaponMovesetsJsonText) &&
                WeaponMovesetRuntime.TryGetTapMoveStats(_loadout.武器類型, _weaponMovesetsJsonText, out var m2,
                    out var r2))
            {
                mv = m2 > 0f ? m2 : mv;
                rg = r2 > 0f ? r2 : rg;
            }

            return new[]
            {
                new WeaponMovesetRuntime.TapComboStep
                    { 動作倍率 = mv, 攻擊距離 = Mathf.Max(0.5f, rg), 段數 = 1 },
            };
        }

        float FallbackTapMv(戰鬥調校列 tun)
        {
            if (_moves?.TapChain != null && _moves.TapChain.Length > 0 &&
                _moves.TapChain[0].動作倍率 > 0f)
                return _moves.TapChain[0].動作倍率;

            var j = string.IsNullOrEmpty(_weaponMovesetsJsonText) ? "" : _weaponMovesetsJsonText;
            if (!string.IsNullOrEmpty(j) &&
                WeaponMovesetRuntime.TryGetTapMoveStats(_loadout.武器類型, j, out var mv, out _))
                return Mathf.Max(0.01f, mv);
            return 0.45f;
        }

        float FallbackReach(戰鬥調校列 tun)
        {
            var t = TapChainOrSynthetic(tun);
            return t is { Length: > 0 } && t[0].攻擊距離 > 0.05f
                ? t[0].攻擊距離
                : tun.近戰預設攻擊距離;
        }

        bool GestureChargeHeld()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetKey(KeyCode.R))
                return true;

            var th = Mathf.Clamp(Screen.height * 0.52f, 4f, Screen.height - 4f);
            if (Input.GetMouseButton(1) && Input.mousePosition.y >= th)
                return true;
#endif
            return _touchInput != null &&
                   Input.touchCount >= 2;
        }

        bool ConsumeSkillPulse()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetKeyDown(KeyCode.F))
                return true;
#endif
            return _touchInput != null && _touchInput.ConsumeCombatSkillPulse();
        }

        void TryReleaseCharge(戰鬥調校列 tuning)
        {
            if (_moves?.Charge == null)
                return;

            var tier = WeaponMovesetRuntime.ResolveChargeTierIndex(_chargeSeconds, _moves.Charge);
            _chargeSeconds = 0f;
            if (tier < 0) return;

            var mv    = _moves.Charge.動作倍率[tier];
            var range = FallbackReach(tuning);
            var target =
                FindNearestMonster(transform.position,
                    range + tuning.自動尋敵額外射程 + 3f);
            if (target == null || !target.isActiveAndEnabled)
                return;

            var swing = new WeaponMovesetRuntime.TapComboStep
            {
                動作倍率 = mv,
                攻擊距離 = range,
                段數 = 1,
            };

            StartCoroutine(StrikeCoroutine(tuning, target, swing,
                Mathf.Max(tuning.玩家攻擊冷卻秒, 0.25f),
                Mathf.Max(tuning.多段命中間隔秒, 0.02f)));

            Debug.Log($"[Player] 蓄力第 {tier + 1} 段 MV={mv:F3}");
        }

        bool TryStartSkillStrike(戰鬥調校列 tuning)
        {
            if (_moves?.Skill == null || _skillCd > 1e-3f || _strikeCoroutineActive)
                return false;

            var sk  = _moves.Skill;
            var tgt = FindNearestMonster(transform.position,
                sk.攻擊距離 + tuning.自動尋敵額外射程 + 2f);
            if (tgt == null || !tgt.isActiveAndEnabled)
                return false;

            var step = new WeaponMovesetRuntime.TapComboStep
            {
                動作倍率 = sk.動作倍率,
                攻擊距離 = sk.攻擊距離 > 0.05f ? sk.攻擊距離 : FallbackReach(tuning),
                段數 = Mathf.Max(1, sk.段數),
            };

            _skillCd = Mathf.Max(1f, sk.冷卻秒);

            StartCoroutine(StrikeCoroutine(tuning, tgt, step,
                Mathf.Max(tuning.玩家攻擊冷卻秒 * 1.05f, 0.35f),
                Mathf.Max(tuning.多段命中間隔秒, 0.04f)));

            Debug.Log($"[Player] 專屬技：共 {Mathf.Max(1, sk.段數)} 命中段");
            return true;
        }

        IEnumerator BeginAutoComboSwing(戰鬥調校列 tuning, MonsterAiController target,
            WeaponMovesetRuntime.TapComboStep step)
        {
            yield return StrikeCoroutine(tuning, target, step,
                Mathf.Max(tuning.玩家攻擊冷卻秒, 0.15f),
                Mathf.Max(tuning.多段命中間隔秒, 0.03f));

            _comboResetTimer = Mathf.Max(0.3f, tuning.連段重置秒);
            var tapLen       = Mathf.Max(1, TapChainOrSynthetic(tuning).Length);
            _comboIndex      = (_comboIndex + 1) % tapLen;
        }

        IEnumerator StrikeCoroutine(戰鬥調校列 tuning, MonsterAiController target,
            WeaponMovesetRuntime.TapComboStep swing, float postCooldownSeconds, float betweenHitsSeconds)
        {
            _strikeCoroutineActive = true;
            _attackCd              = Mathf.Max(_attackCd, postCooldownSeconds);
            var hits = Mathf.Max(1, swing.段數);
            var mvPer =
                WeaponMovesetRuntime.MvPerSwing(Mathf.Max(0.02f, swing.動作倍率), hits);

            if (_attackHitbox != null) _attackHitbox.SetEnabled(true);

            for (var h = 0; h < hits; h++)
            {
                if (target == null || !target.isActiveAndEnabled || CurrentHp <= 0f)
                    break;

                var dist =
                    Vector2.Distance(transform.position, target.transform.position);
                var limit = Mathf.Max(swing.攻擊距離, tuning.近戰預設攻擊距離 * 0.6f);
                limit += tuning.自動尋敵額外射程;

                if (dist > limit)
                    break;

                ApplyOneDamageTick(target, tuning, mvPer);

                betweenHitsSeconds = Mathf.Clamp(betweenHitsSeconds, 0.02f, 0.55f);
                yield return hits > 1 ? new WaitForSeconds(betweenHitsSeconds) : null;
            }

            if (_attackHitbox != null)
                _attackHitbox.SetEnabled(false);

            _strikeCoroutineActive = false;
        }

        void ApplyOneDamageTick(MonsterAiController tgt, 戰鬥調校列 tuning, float motionValue)
        {
            var mRow = tgt.DataRow;
            MonsterStatResolver.ResolveHzv(mRow, _loadout.武器屬性標籤, tuning, out var physHzv, out var elemHzv);

            var diffMul = DamageCalculator.ComputeDynamicDifficultyMultiplier(
                mRow != null ? mRow.星級 : 1,
                tuning);

            var basePhysical = Mathf.Max(1f, _loadout.武器基礎物理 * _playerOutgoingDamageMul);
            var elemMul      =
                Mathf.Sqrt(Mathf.Clamp(_playerOutgoingDamageMul, 0.2f, 5f));

            var input = new DamageCalculator.DamageCalculationInput
            {
                武器基礎物理               = basePhysical,
                武器屬性                 = _loadout.武器屬性 * elemMul,
                動作值MV                 = Mathf.Max(0.01f, motionValue),
                物理肉質HZV              = physHzv,
                屬性肉質HZV              = elemHzv,
                動態難度對玩家輸出倍率 = diffMul,
                會心率                  = tuning.會心率,
                會心傷害倍率               = tuning.會心傷害倍率,
            };

            var dmg = Mathf.Max(1f, DamageCalculator.ComputeFinalDamage(input, _rng, out var crit));
            tgt.ApplyDamage(dmg, crit);
        }

        MonsterAiController FindNearestMonster(Vector2 from, float maxDist)
        {
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
                if (d2 >= bestD) continue;

                bestD = d2;
                best  = m;
            }

            return best;
        }

        public void ApplyDamage(float amount, bool isCrit) =>
            ApplyDamageInternal(amount, isCrit, respectDodgeInvuln: true);

        public void ApplyDamageIgnoringDodge(float amount, bool isCrit) =>
            ApplyDamageInternal(amount, isCrit, respectDodgeInvuln: false);

        void ApplyDamageInternal(float amount, bool isCrit, bool respectDodgeInvuln)
        {
            if (CurrentHp <= 0f || amount <= 0f) return;
            if (respectDodgeInvuln && IsDodging)
            {
                Debug.Log("[Player] 閃避成功！傷害無效");
                return;
            }

            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
            OnDamageReceived?.Invoke(amount, isCrit);
            Debug.Log($"[Player] 受傷 {amount:F1} 會心={isCrit} → HP {CurrentHp:F0}/{MaxHp:F0}");

            if (CurrentHp <= 0f)
                OnDefeated?.Invoke();
        }

        public void ApplyMonsterSpecialAttack(魔物特殊攻擊項 row)
        {
            if (row == null || CurrentHp <= 0f || row.每秒傷害 <= 0 || row.持續時間秒 <= 0f) return;

            var tag = string.IsNullOrEmpty(row.異常屬性) ? "異常" : row.異常屬性;
            var end = Time.time + Mathf.Max(0.05f, row.持續時間秒);
            var dps = Mathf.Max(0f, row.每秒傷害);

            for (var i = 0; i < _ailments.Count; i++)
            {
                if (_ailments[i].異常名稱 != tag) continue;

                var mergedEnd = Mathf.Max(_ailments[i].結束時間, end);

                _ailments[i] = new 獵人持續傷害狀態
                {
                    異常名稱 = tag, 每秒傷害 = dps, 結束時間 = mergedEnd,
                };
                return;
            }

            _ailments.Add(new 獵人持續傷害狀態 { 異常名稱 = tag, 每秒傷害 = dps, 結束時間 = end });
            Debug.Log($"[Player] 特殊攻擊「{tag}」{row.持續時間秒:F1}s（{dps}/秒 DoT）");
        }

        void TickActiveAilments()
        {
            if (_ailments.Count == 0 || CurrentHp <= 0f)
                return;

            var now  = Time.time;
            var dt   = Time.deltaTime;
            float totalDot = 0f;
            var i = 0;

            while (i < _ailments.Count)
            {
                var a = _ailments[i];

                if (now >= a.結束時間)
                {
                    _ailments.RemoveAt(i);
                    continue;
                }

                totalDot += a.每秒傷害 * dt;
                i++;
            }

            if (totalDot > 0f)
                ApplyDamageIgnoringDodge(totalDot, false);

            _dotHudTick += dt;
            if (!(_dotHudTick >= 2f && _ailments.Count > 0)) return;

            _dotHudTick = 0f;

            Debug.Log($"[Player] 持續異常中：{string.Join("、", AilmentDebugSummary())}");
        }

        IEnumerable<string> AilmentDebugSummary()
        {
            var now = Time.time;
            foreach (var a in _ailments)
            {
                if (now >= a.結束時間) continue;
                yield return $"{a.異常名稱} ({a.每秒傷害}/s, {a.結束時間 - now:F1}s)";
            }
        }
    }
}
