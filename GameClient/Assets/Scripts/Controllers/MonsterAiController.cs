using System;
using System.Collections;
using MonsterHunter.Combat;
using MonsterHunter.DataModels;
using UnityEngine;

namespace MonsterHunter.Controllers
{
    /// <summary>
    /// 魔物基礎 AI（狀態機）：待機 → 追擊 → 發動招式。數值來自 monsters.json 與 combat_tuning.json。
    /// 註：.cursorrules 建議長期以行為樹取代 FSM；此為需求指定之最小狀態機。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MonsterAiController : MonoBehaviour, IDamageReceiver
    {
        enum AiState
        {
            待機,
            追擊,
            發動招式,
        }

        [SerializeField] CombatTuningStore _tuningStore;
        [SerializeField] TextAsset _monstersJson;
        [SerializeField] string _魔物編號 = "MON_001";
        [SerializeField] Transform _player;
        [SerializeField] HuntSettlementService _settlement;

        魔物資料列 _data;
        readonly MonsterBreakState _breakState = new MonsterBreakState();
        Rigidbody2D _rb;
        AiState _state = AiState.待機;
        float _stun;
        MonsterHealth _health;
        bool _defeatHandled;
        bool _telegraphing;
        bool _healthEventsHooked;

        /// <summary>本次前搖結束後要結算的直傷數值。</summary>
        int _plannedDirectDamageFlat;
        /// <summary>本次招式判定距離。</summary>
        float _plannedHitRadius;

        /// <summary>若非 null，前搖結束後改發射投射物而非近身直傷。</summary>
        魔物招式攻擊項 _pendingProjectileMove;

        readonly System.Collections.Generic.Dictionary<int, float> _specialMoveCdUntil =
            new System.Collections.Generic.Dictionary<int, float>(8);

        public 魔物資料列 DataRow => _data;
        public MonsterBreakState BreakState => _breakState;

        /// <summary>最大血量（與 <see cref="MonsterHealth"/> 同步）。</summary>
        public float MaxHp => _health != null ? _health.MaxHp : 0f;

        /// <summary>目前血量（與 <see cref="MonsterHealth"/> 同步）。</summary>
        public float CurrentHp => _health != null ? _health.CurrentHp : 0f;

        /// <summary>執行期可取得根節點血量元件（供 UI／除錯）。</summary>
        public MonsterHealth Health => _health;

        /// <summary>近戰判定用：本體碰撞近似半徑（世界空間）。</summary>
        public float BodyHitRadius
        {
            get
            {
                var c = GetComponent<CircleCollider2D>();
                if (c == null || !c.enabled) return 0.82f;
                return Mathf.Max(0.18f, c.bounds.extents.x);
            }
        }

        /// <summary>受傷事件：(傷害量, 是否會心)</summary>
        public event Action<float, bool> OnDamageReceived;
        /// <summary>魔物死亡事件</summary>
        public event Action OnDefeated;

        /// <summary>BattleCombatManager 直接注入資料（不需 TextAsset）。</summary>
        public void InjectData(魔物資料列 data, Transform player, CombatTuningStore tuningStore = null,
            float monsterMaxHpMultiplier = 1f)
        {
            _data = data;
            _player = player;
            if (tuningStore != null) _tuningStore = tuningStore;
            if (data != null)
            {
                var m = Mathf.Max(0.1f, monsterMaxHpMultiplier);
                InitializeMonsterHealthFromData(Mathf.Max(1f, data.最大血量 * m));
            }
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
            // 若已由 InjectData 注入（執行期 BattleCombatManager 模式）→ 不重複載入
            if (_data != null) return;
            // 若沒有 TextAsset（執行期注入模式）→ 等 InjectData 呼叫，不印錯誤
            if (_monstersJson == null) return;

            if (!MonsterDataLookup.TryFind(_monstersJson, _魔物編號, out _data))
                Debug.LogError($"[MonsterAi] 找不到魔物 {_魔物編號}");
            else
                InitializeMonsterHealthFromData(_data.最大血量);
        }

        void Update()
        {
            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            if (tuning == null || _data == null || _player == null) return;

            if (_stun > 0f)
            {
                _stun -= Time.deltaTime;
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            var self = (Vector2)transform.position;
            var pl = (Vector2)_player.position;
            var dist = Vector2.Distance(self, pl);
            var detect = tuning.待機偵測半徑;
            var atkDist = BuildApproachMeleeDistance(out _);
            var abandon = detect * Mathf.Max(1f, tuning.追擊放棄倍率);

            switch (_state)
            {
                case AiState.待機:
                    if (dist <= detect)
                    {
                        _state = AiState.追擊;
                        break;
                    }

                    var stalkSpd = tuning.魔物踱步速度 > 1e-3f ? tuning.魔物踱步速度 : 1.25f;
                    var toPl = dist > 0.05f ? (pl - self).normalized : Vector2.zero;
                    _rb.linearVelocity = toPl * stalkSpd;
                    break;

                case AiState.追擊:
                    if (dist > abandon)
                    {
                        _state = AiState.待機;
                        _rb.linearVelocity = Vector2.zero;
                        break;
                    }

                    if (dist <= atkDist)
                    {
                        _state = AiState.發動招式;
                        _rb.linearVelocity = Vector2.zero;
                        break;
                    }

                    var dir = dist > 0.05f ? (pl - self).normalized : Vector2.zero;
                    var baseChase = tuning.玩家移動速度 *
                                    Mathf.Max(0.1f, tuning.魔物追擊速度比例);
                    var chaseMag = baseChase;
                    if (tuning.魔物追擊低速底線 > 1e-3f)
                        chaseMag = Mathf.Max(tuning.魔物追擊低速底線, baseChase);
                    _rb.linearVelocity = dir * chaseMag;
                    break;

                case AiState.發動招式:
                    if (dist > atkDist * 1.1f)
                    {
                        _state = AiState.追擊;
                        break;
                    }

                    if (!_telegraphing)
                    {
                        PickNextMeleePlan();
                        _telegraphing = true;
                        var telegraph = tuning.魔物攻擊前搖秒 > 0f ? tuning.魔物攻擊前搖秒 : 0f;
                        var postStun  = tuning.魔物招式後僵直秒 > 0f ? tuning.魔物招式後僵直秒 : 1.5f;
                        // 前搖 + 後搖都透過 _stun 凍結移動
                        _stun  = telegraph + postStun;
                        StartCoroutine(TelegraphAndAttack(telegraph));
                        _state = AiState.追擊;
                    }
                    break;
            }
        }

        IEnumerator TelegraphAndAttack(float delay)
        {
            // 前搖視覺：柔和紅橙脈動＝「即將出手」，不是受傷；受傷看血條／浮字。
            var sr = GetComponentInChildren<SpriteRenderer>();
            var originalColor = sr != null ? sr.color : Color.white;
            var warningGo = BuildWarningSign();

            if (delay > 0f)
            {
                var elapsed = 0f;
                var warn = new Color(1f, 0.42f, 0.32f);
                while (elapsed < delay)
                {
                    if (sr != null)
                    {
                        var t = 0.5f + 0.5f * Mathf.Sin(elapsed * 12f);
                        sr.color = Color.Lerp(originalColor, warn, t * 0.42f);
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (sr != null) sr.color = originalColor;
            }

            if (warningGo != null) UnityEngine.Object.Destroy(warningGo);
            _telegraphing = false;

            if (_player == null) yield break;

            if (BattleCombatManager.IsBattleConcluded)
                yield break;

            if (_pendingProjectileMove != null &&
                !string.IsNullOrWhiteSpace(_pendingProjectileMove.投射物型別))
            {
                MonsterProjectile2D.Fire(transform, _player, _pendingProjectileMove,
                    _plannedDirectDamageFlat);
                _pendingProjectileMove = null;
                yield break;
            }

            var dist = Vector2.Distance(transform.position, _player.position);
            // 只在玩家仍在範圍內才造成傷害（玩家閃避離開即無效）
            if (dist <= _plannedHitRadius)
                PerformAttackOnPlayer();
            _pendingProjectileMove = null;
        }

        /// <summary>在魔物頭上建立世界空間「！」提示物件。</summary>
        GameObject BuildWarningSign()
        {
            var go = new GameObject("AttackWarning");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            // 用純色方塊當底（黃底）
            sr.sprite  = CreateSolidSprite(new Color(1f, 0.9f, 0f));
            sr.sortingOrder = 10;
            go.transform.localScale = new Vector3(0.5f, 0.6f, 1f);

            // 「！」文字用 TextMesh（世界空間）
            var txtGo = new GameObject("WarnText");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localPosition = Vector3.zero;
            txtGo.transform.localScale    = new Vector3(2f, 2f, 1f);

            var tm = txtGo.AddComponent<TextMesh>();
            tm.text      = "!";
            tm.fontSize  = 24;
            tm.fontStyle = FontStyle.Bold;
            tm.color     = Color.red;
            tm.anchor    = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            return go;
        }

        static Sprite CreateSolidSprite(Color c)
        {
            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (var i = 0; i < 16; i++) pixels[i] = c;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        void PickNextMeleePlan()
        {
            _pendingProjectileMove = null;

            var norm = _data?.魔物攻擊內容?.普通攻擊;
            if (norm == null)
            {
                _plannedDirectDamageFlat = 0;
                _plannedHitRadius = 2f;
                return;
            }

            BuildApproachMeleeDistance(out var meleeDist);
            var slack = ComputeMonsterMeleeHitSlack(norm.攻擊距離);
            _plannedHitRadius = meleeDist + slack;

            var specs = _data.魔物攻擊內容.特殊招式;
            var normDmg = Mathf.Max(0, norm.傷害);

            float sum = 0f;
            if (specs != null && specs.Length > 0)
            {
                var now = Time.time;
                for (var i = 0; i < specs.Length; i++)
                {
                    var s = specs[i];
                    if (s == null || s.使用權重 <= 0f) continue;
                    if (_specialMoveCdUntil.TryGetValue(i, out var cd) && now < cd) continue;
                    sum += s.使用權重;
                }
            }

            if (sum <= 1e-3f || specs == null)
            {
                _plannedDirectDamageFlat = normDmg;
                return;
            }

            var pick = UnityEngine.Random.value * sum;
            var acc = 0f;
            for (var i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                if (s == null || s.使用權重 <= 0f) continue;
                if (_specialMoveCdUntil.TryGetValue(i, out var cd) && Time.time < cd) continue;
                acc += s.使用權重;
                if (pick <= acc)
                {
                    var mul = s.傷害對普攻倍率 > 0f ? s.傷害對普攻倍率 : 1f;
                    _plannedDirectDamageFlat = Mathf.Max(1, Mathf.RoundToInt(normDmg * mul));
                    var hitR = s.攻擊距離 > 0f ? Mathf.Max(norm.攻擊距離, s.攻擊距離) : norm.攻擊距離;
                    _plannedHitRadius = meleeDist + Mathf.Max(slack, ComputeMonsterMeleeHitSlack(hitR));
                    if (!string.IsNullOrWhiteSpace(s.投射物型別))
                        _pendingProjectileMove = s;
                    if (s.冷卻秒 > 0f)
                        _specialMoveCdUntil[i] = Time.time + s.冷卻秒;
                    Debug.Log($"[MonsterAi] 選招「{(string.IsNullOrEmpty(s.名稱) ? $"特殊招式#{i}" : s.名稱)}」直傷={_plannedDirectDamageFlat}");
                    return;
                }
            }

            _plannedDirectDamageFlat = normDmg;
        }

        /// <summary>追擊／站樁分界：只吃「近戰型」招式距離，不含投射物遠射程，並封頂避免遠距離站樁不動。</summary>
        float BuildApproachMeleeDistance(out float normalDistOnly)
        {
            var norm = _data?.魔物攻擊內容?.普通攻擊;
            normalDistOnly = norm != null ? Mathf.Max(0.1f, norm.攻擊距離) : 2f;
            var atkDist = normalDistOnly;
            var specs = _data?.魔物攻擊內容?.特殊招式;
            if (specs != null)
            {
                for (var i = 0; i < specs.Length; i++)
                {
                    var s = specs[i];
                    if (s == null || s.攻擊距離 <= 0f) continue;
                    // 投射物「攻擊距離」常在 8～10：若混入接戰距離，AI 會在畫邊就判定已到位而不再逼近。
                    if (!string.IsNullOrWhiteSpace(s.投射物型別)) continue;
                    atkDist = Mathf.Max(atkDist, s.攻擊距離);
                }
            }

            const float moveCap = 4.85f;
            return Mathf.Min(atkDist, moveCap);
        }

        /// <summary>中心距離判定的容差：僅略大於雙方碰撞體，避免舊版 ×1.65 導致隔空受擊。</summary>
        static float ComputeMonsterMeleeHitSlack(float attackRangeWorld)
        {
            attackRangeWorld = Mathf.Max(0.1f, attackRangeWorld);
            return Mathf.Clamp(attackRangeWorld * 0.1f + 0.22f, 0.22f, 0.5f);
        }

        void PerformAttackOnPlayer()
        {
            if (BattleCombatManager.IsBattleConcluded)
                return;
            if (_plannedDirectDamageFlat <= 0 || _player == null) return;
            var receiver = _player.GetComponent<IDamageReceiver>();
            if (receiver == null) return;

            receiver.ApplyDamage(_plannedDirectDamageFlat, false);

            var pc = _player.GetComponent<PlayerController>();
            var specials = _data?.魔物攻擊內容?.特殊攻擊;
            if (pc != null && specials != null)
                TryApplySpecialAttackProcs(pc, specials);
        }

        /// <summary>普攻結算後：魔物「特殊攻擊」列表中每一筆依自身 <c>觸發機率</c> 獨立擲骰。</summary>
        static void TryApplySpecialAttackProcs(PlayerController pc, 魔物特殊攻擊項[] specials)
        {
            foreach (var s in specials)
            {
                if (s == null) continue;
                if (s.觸發機率 <= 0f) continue;
                if (UnityEngine.Random.value >= s.觸發機率) continue;
                pc.ApplyMonsterSpecialAttack(s);
            }
        }

        /// <summary>部位破壞時呼叫，供掉落「破壞部位」條件。</summary>
        public void NotifyPartBreak() => _breakState.MarkPartBreak();

        /// <summary>切尾成功時呼叫，供掉落「切斷尾巴」條件。</summary>
        public void NotifyTailCut() => _breakState.MarkTailCut();

        public void ApplyDamage(float amount, bool isCrit)
        {
            if (_defeatHandled) return;
            EnsureMonsterHealthReady();
            if (_health == null) return;
            _health.TakeDamage(amount, isCrit);
        }

        /// <summary>確保根節點有 <see cref="MonsterHealth"/> 並訂閱事件（不重置血量）。</summary>
        void EnsureMonsterHealthReady()
        {
            if (_health == null)
                _health = GetComponent<MonsterHealth>();
            if (_health == null)
                _health = gameObject.AddComponent<MonsterHealth>();

            if (!_healthEventsHooked)
            {
                _health.DamageApplied += OnHealthDamaged;
                _health.HpDepleted += OnHealthDepleted;
                _healthEventsHooked = true;
            }
        }

        /// <summary>由企劃最大血量初始化（僅在載入資料／注入時呼叫，避免每次受傷補滿血）。</summary>
        void InitializeMonsterHealthFromData(float maxHp)
        {
            EnsureMonsterHealthReady();
            if (_health != null && maxHp > 0f)
                _health.Initialize(maxHp, true);
        }

        void OnDestroy()
        {
            if (_health != null && _healthEventsHooked)
            {
                _health.DamageApplied -= OnHealthDamaged;
                _health.HpDepleted -= OnHealthDepleted;
            }
        }

        void OnHealthDamaged(float amount, bool isCrit)
        {
            OnDamageReceived?.Invoke(amount, isCrit);
            Debug.Log($"[Monster {_魔物編號}] HP {CurrentHp:F0}/{MaxHp:F0} (-{amount:F1} crit={isCrit})");
        }

        void OnHealthDepleted()
        {
            if (_defeatHandled) return;
            _defeatHandled = true;
            OnDefeated?.Invoke();
            StartCoroutine(DefeatFlow());
        }

        IEnumerator DefeatFlow()
        {
            if (_settlement != null && _data != null)
            {
                var cond = _breakState.ToDropConditions();
                string err = null;
                yield return StartCoroutine(
                    _settlement.RunKillSettlement(
                        _魔物編號,
                        _data.名稱 ?? _魔物編號,
                        cond,
                        System.DateTime.Today,
                        e => err = e,
                        (_, kills, diffMul) =>
                            Debug.Log($"[MonsterAi] 結算完成 當日擊殺數={kills} 難度倍率={diffMul:F3}")
                    )
                );
                if (err != null) Debug.LogWarning("[MonsterAi] 結算：" + err);
            }

            gameObject.SetActive(false);
        }
    }
}
