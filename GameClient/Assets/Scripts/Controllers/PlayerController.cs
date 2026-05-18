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

        // ✦ MH NOW 完美閃避與 SP 大招全域及實例欄位
        public static bool NextHitIsPerfectCounter = false;
        public static bool NextHitIsSP = false;
        public static bool NextHitIsWeakness = false;

        private float _spGauge = 0f;
        public float SpGauge => _spGauge;
        private bool _isExecutingSpSkill = false;
        public bool IsExecutingSpSkill => _isExecutingSpSkill;
        private bool _perfectDodgeBuffActive = false;

        public struct 獵人持續傷害狀態
        {
            public string 異常名稱;
            public float 每秒傷害;
            public float 結束時間;
            public string 圖片路徑; // ✦ 新增圖片路徑關聯
        }

        readonly List<獵人持續傷害狀態> _ailments = new List<獵人持續傷害狀態>(4);
        float _dotHudTick;

        string _weaponMovesetsJsonText;
        MonsterAiController _directTarget;
        PortraitCombatTouchInput _touchInput;

        WeaponMovesetRuntime.ParsedMoveset _moves;

        float _playerOutgoingDamageMul = 1f;
        float _playerIncomingDamageMul = 1f;
        float _playerMoveSpeedMul = 1f;
        float _comboResetTimer;
        int _comboIndex;
        float _skillCd;

        float _chargeSeconds;
        bool _chargeHeldPrevFrame;
        bool _strikeCoroutineActive;

        /// <summary>攻擊框繞身半徑（由 BattleCombatManager 注入），搜敵／距離判定的補正用。</summary>
        float _meleeOrbitRadius;

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

            // ✦ 下調閃避距離至原先的 58%，使翻滾動作更加緊湊緊貼魔物，方便進行近戰弱點輸出與完美反擊！
            var dodgeDist = (tuning.閃避距離 > 0f ? tuning.閃避距離 : 4f) * 0.58f;
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
            float outgoingDamageMultiplier = 1f,
            float moveSpeedMultiplier = 1f)
        {
            _tuningStore              = ts;
            _loadout                  = loadout;
            _touchInput               = touchInputForCombat;
            _weaponMovesetsJsonText   = weaponMovesetsJson;
            _playerOutgoingDamageMul  = Mathf.Clamp(outgoingDamageMultiplier, 0.2f, 5f);
            _playerMoveSpeedMul       = Mathf.Clamp(moveSpeedMultiplier, 0.5f, 2.5f);
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

            var star = Mathf.Clamp(_loadout.武器星級 <= 0 ? 5 : _loadout.武器星級, 1, 10);
            WeaponMovesetRuntime.TryParseMoveset(_loadout.武器類型, json, melee, gate, sigR, star, out _moves);
        }

        public void SetOutgoingDamageMultiplier(float m) =>
            _playerOutgoingDamageMul = Mathf.Clamp(m, 0.2f, 5f);

        public void SetIncomingDamageMultiplier(float m) =>
            _playerIncomingDamageMul = Mathf.Clamp(m, 0.05f, 2.0f);

        public void SetDirectTarget(MonsterAiController monster) => _directTarget = monster;

        public void Heal(float amount)
        {
            if (CurrentHp <= 0f || amount <= 0f) return;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        }

        /// <summary>割草軌道：攻擊框離獵人中心的半徑。</summary>
        public void SetMeleeOrbitRadius(float worldRadius) => _meleeOrbitRadius = Mathf.Max(0f, worldRadius);

        Vector2 MeleeStrikeOrigin() =>
            _attackHitbox != null ? (Vector2)_attackHitbox.transform.position : (Vector2)transform.position;

        float EstimateBladeContactRadius()
        {
            if (_attackHitbox == null) return Mathf.Max(0.12f, _meleeOrbitRadius * 0.08f);

            var c = _attackHitbox.GetComponent<CircleCollider2D>();
            if (c == null) return 0.26f;

            var ls = Mathf.Max(Mathf.Abs(_attackHitbox.transform.lossyScale.x),
                Mathf.Abs(_attackHitbox.transform.lossyScale.y));
            return Mathf.Max(0.06f, c.radius * Mathf.Max(ls, 1f));
        }

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

        void Start()
        {
            AddSelfGlowAura(transform, 0.72f);
        }

        void Update()
        {
            // ✦ MH NOW 按下 X 鍵釋放 SP 超能量大招
            if (Input.GetKeyDown(KeyCode.X))
            {
                TriggerSpUltimate();
            }

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
            // ✦ 戰鬥時玩家移動速度減半！提供優質的戰鬥拉扯與閃避體驗
            _rb.linearVelocity = moving
                ? _move.normalized * (tuning.玩家移動速度 * _playerMoveSpeedMul * 0.5f)
                : Vector2.zero;

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

            var searchExtra = tuning.自動尋敵額外射程 + Mathf.Clamp(_meleeOrbitRadius * 0.4f, 0.2f, 1.1f);
            var target =
                FindNearestMonster(MeleeStrikeOrigin(), atkRangeGuess + searchExtra);
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
            var star = Mathf.Clamp(_loadout != null && _loadout.武器星級 > 0 ? _loadout.武器星級 : 5, 1, 10);
            if (!string.IsNullOrEmpty(_weaponMovesetsJsonText) &&
                _loadout != null && !string.IsNullOrEmpty(_loadout.武器類型) &&
                WeaponMovesetRuntime.TryGetTapMoveStats(_loadout.武器類型, _weaponMovesetsJsonText, star, out var m2,
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
            var star = Mathf.Clamp(_loadout != null && _loadout.武器星級 > 0 ? _loadout.武器星級 : 5, 1, 10);
            if (!string.IsNullOrEmpty(j) && _loadout != null && !string.IsNullOrEmpty(_loadout.武器類型) &&
                WeaponMovesetRuntime.TryGetTapMoveStats(_loadout.武器類型, j, star, out var mv, out _))
                return Mathf.Max(0.01f, mv);
            return 0.45f;
        }

        float FallbackReach(戰鬥調校列 tun)
        {
            var t = TapChainOrSynthetic(tun);
            if (t != null && t.Length > 0 && t[0].攻擊距離 > 0.05f)
                return t[0].攻擊距離;
            return tun.近戰預設攻擊距離;
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
                FindNearestMonster(MeleeStrikeOrigin(),
                    range + tuning.自動尋敵額外射程 + 3f + Mathf.Clamp(_meleeOrbitRadius * 0.35f, 0f, 0.8f));
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
            var tgt = FindNearestMonster(MeleeStrikeOrigin(),
                sk.攻擊距離 + tuning.自動尋敵額外射程 + 2f + Mathf.Clamp(_meleeOrbitRadius * 0.35f, 0f, 1f));
            if (tgt == null || !tgt.isActiveAndEnabled)
                return false;

            var step = new WeaponMovesetRuntime.TapComboStep
            {
                動作倍率 = sk.動作倍率,
                攻擊距離 = sk.攻擊距離 > 0.05f ? sk.攻擊距離 : FallbackReach(tuning),
                段數 = Mathf.Max(1, sk.段數),
            };

            _skillCd = Mathf.Max(1f, sk.冷卻秒);

            // ✦ 視覺回饋：播放獵人發招閃爍與魔物處的大型青藍色能量衝擊波！
            StartCoroutine(AnimatePlayerSkillCast());
            SpawnSkillVisualEffect(tgt.transform.position, step.攻擊距離);

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
                    Vector2.Distance(MeleeStrikeOrigin(), target.transform.position);
                var bodyR = Mathf.Max(0.2f, target.BodyHitRadius);
                var bladeR = EstimateBladeContactRadius();
                var slack = Mathf.Max(0f, tuning.自動尋敵額外射程 * 0.22f) + 0.11f +
                    Mathf.Clamp(_meleeOrbitRadius * 0.16f, 0f, 0.62f);
                if (dist > bodyR + bladeR + slack)
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

            // ✦ MH NOW 弱點部位打擊判定（前後 45 度）
            var monsterRb = tgt.GetComponent<Rigidbody2D>();
            var monsterFace = monsterRb != null && monsterRb.linearVelocity.sqrMagnitude > 0.01f 
                ? monsterRb.linearVelocity.normalized 
                : (Vector2)(transform.position - tgt.transform.position).normalized;

            var toPlayer = (Vector2)(transform.position - tgt.transform.position).normalized;
            var dot = Vector2.Dot(monsterFace, toPlayer);
            bool isWeakness = Mathf.Abs(dot) >= 0.707f;

            if (isWeakness)
            {
                dmg *= 1.35f;
            }

            // ✦ MH NOW 完美閃避反擊加成
            bool isPerfectCounter = _perfectDodgeBuffActive;
            if (_perfectDodgeBuffActive)
            {
                _perfectDodgeBuffActive = false; // 消耗
                dmg *= 2.5f;
                crit = true; // 強制會心視覺
            }

            // ✦ SP 大招加成標籤
            bool isSpHit = _isExecutingSpSkill;

            // ✦ 通過全域靜態標記傳遞給 HUD
            NextHitIsPerfectCounter = isPerfectCounter;
            NextHitIsSP = isSpHit;
            NextHitIsWeakness = isWeakness;

            tgt.ApplyDamage(dmg, crit);

            // ✦ 填充 SP 能量 (在大招執行期間不累加)
            if (!_isExecutingSpSkill)
            {
                float spCharge = _chargeHeldPrevFrame ? 6f : 2.5f;
                _spGauge = Mathf.Min(100f, _spGauge + spCharge);
            }
        }

        MonsterAiController FindNearestMonster(Vector2 from, float maxDist)
        {
            if (_directTarget != null && _directTarget.isActiveAndEnabled)
            {
                var tuningPick = _tuningStore != null ? _tuningStore.Active : null;
                var d = Vector2.Distance(from, (Vector2)_directTarget.transform.position);
                if (tuningPick == null)
                    return d <= maxDist ? _directTarget : null;

                var bodyR = Mathf.Max(0.22f, _directTarget.BodyHitRadius);
                var bladeR = EstimateBladeContactRadius();
                var slack = Mathf.Max(0f, tuningPick.自動尋敵額外射程 * 0.22f) + 0.12f +
                    Mathf.Clamp(_meleeOrbitRadius * 0.14f, 0f, 0.52f);
                var pickMax = Mathf.Min(maxDist, bodyR + bladeR + slack);
                return d <= pickMax ? _directTarget : null;
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
            if (BattleCombatManager.IsBattleConcluded)
                return;
            if (CurrentHp <= 0f || amount <= 0f) return;
            if (respectDodgeInvuln && IsDodging)
            {
                var tuning = _tuningStore != null ? _tuningStore.Active : null;
                float invincSec = tuning != null && tuning.閃避無敵秒 > 0f ? tuning.閃避無敵秒 : 0.4f;
                float elapsedDodge = invincSec - _dodgeTimer;

                // ✦ Perfect Dodge (完美閃避)：如果在翻滾開始後的 0.15 秒內受擊
                if (elapsedDodge <= 0.15f)
                {
                    TriggerPerfectDodge();
                    return;
                }

                Debug.Log("[Player] 避開了攻擊（普通無敵）");
                return;
            }

            // ✦ 配合使用者要求，大幅提高魔物打擊傷害，保證玩家受擊時至少扣除 2/3 的最大生命值（67%），極致拉滿生死一線的緊張感與閃避成就感！
            float minDmg = MaxHp * 0.67f;
            float finalDmg = Mathf.Max(amount, minDmg) * _playerIncomingDamageMul;

            CurrentHp = Mathf.Max(0f, CurrentHp - finalDmg);
            OnDamageReceived?.Invoke(finalDmg, isCrit);
            Debug.Log($"[Player] 受傷 {finalDmg:F1} (原始={amount:F1}) 會心={isCrit} → HP {CurrentHp:F0}/{MaxHp:F0}");

            // ✦ 觸發獵人受擊視覺紅光閃爍
            StartCoroutine(PlayerHitFlashRoutine());

            if (CurrentHp <= 0f)
                OnDefeated?.Invoke();
        }

        IEnumerator PlayerHitFlashRoutine()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) yield break;
            
            Color orig = sr.color;
            sr.color = new Color(1f, 0.2f, 0.2f, 1f); // 閃爍紅光
            yield return new WaitForSeconds(0.12f);
            if (sr != null) sr.color = orig;
        }

        public void ApplyMonsterSpecialAttack(魔物特殊攻擊項 row)
        {
            if (row == null || CurrentHp <= 0f || row.每秒傷害 <= 0 || row.持續時間秒 <= 0f) return;

            var tag = string.IsNullOrEmpty(row.異常屬性) ? "異常" : row.異常屬性;
            var end = Time.time + Mathf.Max(0.05f, row.持續時間秒);
            // 削弱 DoT 傷害：將每秒異常狀態傷害限制在最大 2.0 點，避免玩家快速大扣血
            var dps = Mathf.Clamp(row.每秒傷害, 0f, 2f);
            var pic = row.圖片路徑;

            for (var i = 0; i < _ailments.Count; i++)
            {
                if (_ailments[i].異常名稱 != tag) continue;

                var mergedEnd = Mathf.Max(_ailments[i].結束時間, end);

                _ailments[i] = new 獵人持續傷害狀態
                {
                    異常名稱 = tag, 每秒傷害 = dps, 結束時間 = mergedEnd, 圖片路徑 = pic
                };
                return;
            }

            _ailments.Add(new 獵人持續傷害狀態 { 異常名稱 = tag, 每秒傷害 = dps, 結束時間 = end, 圖片路徑 = pic });
            Debug.Log($"[Player] 特殊攻擊「{tag}」{row.持續時間秒:F1}s（{dps}/秒 DoT）");
        }

        void TickActiveAilments()
        {
            if (BattleCombatManager.IsBattleConcluded)
                return;
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

        public List<string> GetActiveAilmentNames()
        {
            var list = new List<string>();
            var now = Time.time;
            for (var i = 0; i < _ailments.Count; i++)
            {
                if (now < _ailments[i].結束時間)
                    list.Add(_ailments[i].異常名稱);
            }
            return list;
        }

        public List<獵人持續傷害狀態> GetActiveAilments()
        {
            var list = new List<獵人持續傷害狀態>();
            var now = Time.time;
            for (var i = 0; i < _ailments.Count; i++)
            {
                if (now < _ailments[i].結束時間)
                    list.Add(_ailments[i]);
            }
            return list;
        }

        // ✦ 新增：動態生成技能衝擊波
        void SpawnSkillVisualEffect(Vector2 position, float range)
        {
            var wave = new GameObject("SkillShockwave");
            wave.transform.position = position;
            
            var sr = wave.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2200; // 高於角色和魔物
            
            // 動態生成 32x32 的能量圓環 Sprite
            var tex = new Texture2D(32, 32);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = x - 15.5f;
                    float dy = y - 15.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // 建立一個青藍色發光圓環
                    if (dist >= 11f && dist <= 15.5f)
                    {
                        float alpha = Mathf.Clamp01((15.5f - dist) / 4.5f);
                        tex.SetPixel(x, y, new Color(0f, 0.95f, 1f, alpha));
                    }
                    else if (dist < 11f)
                    {
                        float alpha = Mathf.Clamp01(dist / 11f) * 0.28f;
                        tex.SetPixel(x, y, new Color(0f, 0.8f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            wave.transform.localScale = Vector3.zero;

            StartCoroutine(AnimateSkillWave(wave, sr, range));
        }

        IEnumerator AnimateSkillWave(GameObject go, SpriteRenderer sr, float targetRange)
        {
            float elapsed = 0f;
            float duration = 0.38f;
            // 衝擊波大小取決於武器攻擊距離，極具魄力！
            float finalScaleSize = Mathf.Max(1.5f, targetRange * 1.6f);
            Vector3 targetScale = new Vector3(finalScaleSize, finalScaleSize, 1f);

            while (elapsed < duration)
            {
                if (go == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 緩動展開
                float tEase = Mathf.Sin(t * Mathf.PI * 0.5f);
                go.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, tEase);
                
                // 漸變淡出
                var c = sr.color;
                c.a = Mathf.Clamp01(1f - t * t);
                sr.color = c;
                
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        IEnumerator AnimatePlayerSkillCast()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            var originalColor = sr != null ? sr.color : Color.white;
            var originalScale = transform.localScale;

            float elapsed = 0f;
            float duration = 0.32f;
            var skillColor = new Color(0f, 0.95f, 1f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                if (sr != null)
                {
                    // 青藍色耀眼閃光
                    sr.color = Color.Lerp(skillColor, originalColor, t);
                }
                
                // 帥氣的發招微微擴張震動
                float scaleMul = 1f + Mathf.Sin(t * Mathf.PI) * 0.22f;
                transform.localScale = originalScale * scaleMul;
                
                yield return null;
            }

            if (sr != null) sr.color = originalColor;
            transform.localScale = originalScale;
        }

        // ✦ MH NOW 完美閃避與 SP 大招實作
        private void TriggerPerfectDodge()
        {
            _perfectDodgeBuffActive = true;
            Debug.Log("[Player] ✦ 完美閃避 (Perfect Dodge) 成功！下一擊傷害 2.5 倍！");
            
            // 生成完美閃避飄字
            SpawnPerfectDodgeFloatText();
            
            // 完美閃避黃金色幻影閃爍
            StartCoroutine(PerfectDodgeVisualFlashRoutine());
        }

        private void SpawnPerfectDodgeFloatText()
        {
            var go = new GameObject("PerfectDodgeText");
            go.transform.position = transform.position + new Vector3(0f, 1.2f, 0f);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = "⚡ 完美閃避 PERFECT DODGE! ⚡";
            mesh.characterSize = 0.08f;
            mesh.fontSize = 80;
            mesh.color = new Color(1f, 0.85f, 0f); // 金黃色
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            
            var mr = mesh.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 5600;
            
            StartCoroutine(FloatAndFadeBreakText(mesh));
        }

        IEnumerator FloatAndFadeBreakText(TextMesh mesh)
        {
            float elapsed = 0f;
            float duration = 1.0f;
            var orig = mesh.color;
            Vector3 startPos = mesh.transform.position;

            while (elapsed < duration)
            {
                if (mesh == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                mesh.transform.position = startPos + new Vector3(0f, t * 0.8f, 0f);
                var c = orig;
                c.a = Mathf.Clamp01(1f - t * t);
                mesh.color = c;
                
                yield return null;
            }
            if (mesh != null && mesh.gameObject != null) Destroy(mesh.gameObject);
        }

        IEnumerator PerfectDodgeVisualFlashRoutine()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null) yield break;
            
            var origColor = sr.color;
            for (int i = 0; i < 4; i++)
            {
                sr.color = new Color(1f, 0.9f, 0f, 1f); // 亮黃色
                yield return new WaitForSeconds(0.06f);
                sr.color = origColor;
                yield return new WaitForSeconds(0.06f);
            }
        }

        public void TriggerSpUltimate()
        {
            if (_spGauge < 99.9f || _isExecutingSpSkill || CurrentHp <= 0f) return;
            _spGauge = 0f;
            StartCoroutine(ExecuteSpUltimateRoutine());
        }

        IEnumerator ExecuteSpUltimateRoutine()
        {
            _isExecutingSpSkill = true;
            Debug.Log("[Player] ✦ 釋放 SP 超能量大招 SP ULTIMATE!!! ✦");

            // 1. 全螢幕時空凍結 (0.3s 慢動作，營造電影感)
            float originalTimeScale = Time.timeScale;
            Time.timeScale = 0.35f;
            
            // 2. 獵人完全無敵
            _dodgeTimer = 2.2f; // 無敵持續 2.2s

            var sr = GetComponentInChildren<SpriteRenderer>();
            var originalColor = sr != null ? sr.color : Color.white;

            // 3. 播放發光特效
            if (sr != null) sr.color = new Color(0f, 0.9f, 1f, 1f); // 耀眼青藍色

            yield return new WaitForSecondsRealtime(0.45f);
            Time.timeScale = originalTimeScale; // 恢復正常時空

            // 4. 連環幻影斬擊 (6段斬擊)
            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            float reach = tuning != null ? tuning.近戰預設攻擊距離 + 3f : 7f;
            var target = FindNearestMonster(MeleeStrikeOrigin(), reach);

            if (target != null && target.isActiveAndEnabled)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (target == null || !target.isActiveAndEnabled) break;
                    
                    Vector3 startPos = transform.position;
                    transform.position = Vector3.Lerp(startPos, target.transform.position, 0.4f);
                    
                    SpawnSkillVisualEffect(target.transform.position, 3.5f);
                    ApplyOneDamageTick(target, tuning, FallbackTapMv(tuning) * 0.8f);

                    yield return new WaitForSeconds(0.14f);
                    transform.position = startPos;
                    yield return new WaitForSeconds(0.04f);
                }

                if (target != null && target.isActiveAndEnabled)
                {
                    Vector3 startPos = transform.position;
                    transform.position = Vector3.Lerp(startPos, target.transform.position, 0.75f);
                    
                    SpawnSkillVisualEffect(target.transform.position, 6.0f);
                    ApplyOneDamageTick(target, tuning, FallbackTapMv(tuning) * 3.5f);
                    
                    yield return new WaitForSeconds(0.25f);
                    transform.position = startPos;
                }
            }

            if (sr != null) sr.color = originalColor;
            _dodgeTimer = 0f; // 結束無敵
            _isExecutingSpSkill = false;
        }

        private void AddSelfGlowAura(Transform parent, float scale)
        {
            var glowGo = new GameObject("PlayerGlowAura", typeof(SpriteRenderer));
            glowGo.transform.SetParent(parent, false);
            glowGo.transform.localPosition = new Vector3(0f, -0.65f, 0.05f); // 位於腳底偏後
            glowGo.transform.localScale = new Vector3(scale * 1.6f, scale * 0.5f, 1f); // 橢圓形光圈

            var sr = glowGo.GetComponent<SpriteRenderer>();
            sr.sprite = CreateSoftGlowSprite();
            sr.drawMode = SpriteDrawMode.Simple;
            sr.color = new Color(1f, 1f, 1f, 0.6f); // 亮白透明度 0.6

            var parentSr = parent.GetComponent<SpriteRenderer>();
            if (parentSr == null) parentSr = parent.GetComponentInChildren<SpriteRenderer>();
            if (parentSr != null) sr.sortingOrder = parentSr.sortingOrder - 1;

            glowGo.AddComponent<GlowBreather>();
        }

        private Sprite CreateSoftGlowSprite()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float maxDist = size * 0.5f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(dist / maxDist);
                    
                    float alpha = Mathf.Clamp01(1f - t);
                    alpha = Mathf.Pow(alpha, 1.8f);
                    
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    public sealed class GlowBreather : MonoBehaviour
    {
        private float _t;
        private SpriteRenderer _sr;
        private Vector3 _baseScale;

        void Start()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        void Update()
        {
            _t += Time.deltaTime * 2.8f;
            // 微微呼吸起伏
            float s = 1.0f + Mathf.Sin(_t) * 0.12f;
            transform.localScale = _baseScale * s;

            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = 0.5f + Mathf.Sin(_t) * 0.12f;
                _sr.color = c;
            }
        }
    }
}
