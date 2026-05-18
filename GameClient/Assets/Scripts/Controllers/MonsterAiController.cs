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

        Transform _overrideTarget;
        float _overrideTargetTimer;

        public void SetOverrideTarget(Transform target, float duration)
        {
            _overrideTarget = target;
            _overrideTargetTimer = duration;
        }

        魔物資料列 _data;
        readonly MonsterBreakState _breakState = new MonsterBreakState();
        Rigidbody2D _rb;
        AiState _state = AiState.待機;
        float _stun;
        MonsterHealth _health;
        bool _defeatHandled;
        bool _telegraphing;
        public bool IsTelegraphing => _telegraphing;
        bool _healthEventsHooked;
        bool _isExecutingSpecialMove;
        魔物招式攻擊項 _currentSkill;

        static Sprite _cachedAuraSprite;

        /// <summary>本次前搖結束後要結算的直傷數值。</summary>
        int _plannedDirectDamageFlat;
        /// <summary>本次招式判定距離。</summary>
        float _plannedHitRadius;

        /// <summary>若非 null，前搖結束後改發射投射物而非近身直傷。</summary>
        魔物招式攻擊項 _pendingProjectileMove;

        private struct PendingAttackInfo
        {
            public float releaseTime;
            public 魔物招式攻擊項 skill;
            public 魔物招式攻擊項 pendingProjectileMove;
            public int plannedDirectDamageFlat;
            public float plannedHitRadius;
            public bool isExecutingSpecialMove;
        }

        private System.Collections.Generic.List<PendingAttackInfo> _queuedAttacks = new System.Collections.Generic.List<PendingAttackInfo>();
        private float _flashCooldownTimer = 0f;

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

        /// <summary>結算獲得的掉落素材清單（供 UI 結算畫面顯示）。</summary>
        public System.Collections.Generic.IReadOnlyList<MonsterHunter.Combat.SettlementRewardEntry> SettlementRewards { get; private set; }

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
            if (data != null && !string.IsNullOrEmpty(data.魔物編號))
            {
                _魔物編號 = data.魔物編號; // ✦ 更新底層識別碼，確保掉落結算正確對應
            }
            if (tuningStore != null) _tuningStore = tuningStore;
            if (data != null)
            {
                var m = Mathf.Max(0.1f, monsterMaxHpMultiplier);
                // ✦ 配合使用者要求，增加魔物血量 1/3 (乘上 1.3333f)
                InitializeMonsterHealthFromData(Mathf.Max(1f, data.最大血量 * m * 1.3333f));
                PickNextMeleePlan(); // ✦ 初次遇敵直接心裡想好第一招
            }
        }

        void Awake()
        {
            // ✦ 新增：為魔物附加 3D 立體恐怖特效與雙層光影效果，使其極具立體感與壓迫感
            if (gameObject.GetComponent<Monster3DEffectController>() == null)
            {
                gameObject.AddComponent<Monster3DEffectController>();
            }

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
                // ✦ 配合使用者要求，增加魔物血量 1/3 (乘上 1.3333f)
                InitializeMonsterHealthFromData(_data.最大血量 * 1.3333f);
        }

        void Start()
        {
            if (_currentSkill == null) PickNextMeleePlan();
        }

        void Update()
        {
            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            if (tuning == null || _data == null || _player == null) return;

            // ✦ 扣除閃光冷卻時間
            if (_flashCooldownTimer > 0f)
            {
                _flashCooldownTimer -= Time.deltaTime;
            }

            // ✦ 檢查是否有排定的延遲出招已到期，並執行出招釋放！
            if (_queuedAttacks != null && _queuedAttacks.Count > 0)
            {
                for (int i = 0; i < _queuedAttacks.Count; i++)
                {
                    if (Time.time >= _queuedAttacks[i].releaseTime)
                    {
                        var attack = _queuedAttacks[i];
                        _queuedAttacks.RemoveAt(i);
                        i--;
                        StartCoroutine(ReleaseAttackRoutine(attack));
                    }
                }
            }

            if (_stun > 0f)
            {
                _stun -= Time.deltaTime;
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            // ✦ 寵物挑釁仇恨覆蓋目標邏輯
            Transform currentTarget = _player;
            if (_overrideTarget != null && _overrideTargetTimer > 0f)
            {
                _overrideTargetTimer -= Time.deltaTime;
                currentTarget = _overrideTarget;
                if (_overrideTargetTimer <= 0f)
                {
                    _overrideTarget = null;
                }
            }

            var self = (Vector2)transform.position;
            var pl = (Vector2)currentTarget.position;
            var dist = Vector2.Distance(self, pl);
            var detect = tuning.待機偵測半徑;
            var atkDist = BuildApproachMeleeDistance(out _);
            var abandon = detect * Mathf.Max(1f, tuning.追擊放棄倍率);

            // ✦ 取得魔物星級，用於跨狀態速度與動作冷卻加速計算
            int star = _data != null ? _data.星級 : 1;

            switch (_state)
            {
                case AiState.待機:
                    if (dist <= detect)
                    {
                        _state = AiState.追擊;
                        break;
                    }

                    // ✦ 戰鬥時踱步速度減半！
                    var stalkSpd = (tuning.魔物踱步速度 > 1e-3f ? tuning.魔物踱步速度 : 1.25f) * 0.5f;
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
                    
                    // ✦ 配合使用者要求，星級越高的魔物速度越快，越難以拉開距離！
                    float starSpeedMultiplier = 1f + (star - 1) * 0.12f;
                    
                    var baseChase = tuning.玩家移動速度 *
                                    Mathf.Max(0.1f, tuning.魔物追擊速度比例) * 0.5f * starSpeedMultiplier;
                    var chaseMag = baseChase;
                    if (tuning.魔物追擊低速底線 > 1e-3f)
                        chaseMag = Mathf.Max(tuning.魔物追擊低速底線 * 0.5f, baseChase);

                    // ✦ 加入蛇行 / 側步偏移量
                    var perp = new Vector2(-dir.y, dir.x);
                    var sway = Mathf.Sin(Time.time * 2.5f) * 0.4f * chaseMag; // 隨時間左右橫移
                    
                    _rb.linearVelocity = dir * chaseMag + perp * sway;
                    break;

                case AiState.發動招式:
                    if (dist > atkDist * 1.15f) // ✦ 稍微放寬 1.15f 避免距離邊緣判斷浮動抖動
                    {
                        _state = AiState.追擊;
                        break;
                    }

                    if (!_telegraphing && _flashCooldownTimer <= 0f)
                    {
                        _telegraphing = true;
                        
                        // ✦ 配合使用者要求，每隻魔物的出招方式稍有不同，且星等越高、出招延遲越短、節奏越詭譎難測！
                        int mHash = Mathf.Abs(_魔物編號.GetHashCode());
                        
                        // ❶ 根據星級，高星魔物打擊前搖延遲越短（1星：2.0s~4.0s；10星：1.1s~2.2s），反應視窗極具壓迫！
                        float delayMin = Mathf.Clamp(2.0f - (star - 1) * 0.1f, 1.1f, 2.0f);
                        float delayMax = Mathf.Clamp(4.0f - (star - 1) * 0.2f, 2.2f, 4.0f);
                        
                        // ❷ 加入每隻魔物專屬的出招時間特異性偏移（Uniqueness Offset），打破死板規律！
                        float monsterOffset = ((mHash % 5) - 2) * 0.15f; 
                        float releaseDelay = UnityEngine.Random.Range(delayMin, delayMax) + monsterOffset;
                        releaseDelay = Mathf.Clamp(releaseDelay, 0.82f, 4.5f);
                        
                        // ✦ 閃光結束後，出招排入佇列在 releaseDelay 秒後正式引爆！
                        StartCoroutine(TelegraphAndAttack(releaseDelay));
                        _state = AiState.追擊;
                    }
                    break;
            }
        }

        IEnumerator TelegraphAndAttack(float releaseDelay)
        {
            int star = _data != null ? _data.星級 : 1;
            var sr = GetComponentInChildren<SpriteRenderer>();
            var originalColor = sr != null ? sr.color : Color.white;
            var originalScale = transform.localScale;

            // ✦ 配合使用者要求，星等越高的魔物，閃光警示時間越短、出手極速！
            // 1 星怪閃光 0.5s，10 星怪縮短至 0.32s，留給玩家完美閃避的黃金反應時間極度縮窄，大幅增加刺激感！
            float flashDuration = Mathf.Clamp(0.5f - (star - 1) * 0.02f, 0.32f, 0.5f);

            // ✦ 配合使用者要求，以明亮度與對比極強的【紅、黃、紫、綠】四色大分流，完全區隔魔物招式類型！
            Color mhnFlashColor = new Color(1f, 0.05f, 0.05f, 1f); // 預設致命猩紅
            if (_currentSkill != null)
            {
                if (!string.IsNullOrEmpty(_currentSkill.投射物型別))
                {
                    mhnFlashColor = new Color(0.85f, 0.0f, 0.95f, 1f); // ❶ 遠程投射/咆哮：【霓虹魅幻紫】
                }
                else if (_currentSkill.傷害對普攻倍率 >= 1.2f)
                {
                    mhnFlashColor = new Color(1f, 0.0f, 0.0f, 1f);     // ❷ 蓄力重擊/捕食大招：【狂暴烈焰紅】（門檻下修至1.2倍，更容易看見！）
                }
                else
                {
                    mhnFlashColor = new Color(1f, 0.85f, 0.0f, 1f);    // ❸ 快速突進/連段招式：【璀璨金黃色】
                }
            }
            else
            {
                mhnFlashColor = new Color(0.12f, 1f, 0.22f, 1f);       // ❹ 普通基礎揮擊：【翡翠流光綠】（代表低危險性普攻）
            }

            // 建立魔物背後的預警光暈 (Aura)
            var auraGo = new GameObject("TelegraphAura");
            auraGo.transform.SetParent(transform, false);
            auraGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var auraSr = auraGo.AddComponent<SpriteRenderer>();
            auraSr.sprite = GetOrCreateAuraSprite();
            auraSr.sortingOrder = -5; // 位於魔物本體背後

            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                float ratio = elapsed / flashDuration;
                
                if (sr != null)
                {
                    float pulseFreq = Mathf.Lerp(12f, 25f, ratio);
                    float t = (Mathf.Sin(elapsed * pulseFreq) + 1f) * 0.5f; 
                    float redAmount = Mathf.Lerp(0.25f, 0.95f, t);
                    sr.color = Color.Lerp(originalColor, mhnFlashColor, redAmount); 
                }

                if (auraSr != null)
                {
                    float pulseFreq = Mathf.Lerp(8f, 20f, ratio);
                    float auraT = (Mathf.Sin(elapsed * pulseFreq) + 1f) * 0.5f;
                    auraSr.color = new Color(mhnFlashColor.r, mhnFlashColor.g, mhnFlashColor.b, auraT * 0.5f + 0.15f); 
                    float auraScale = 1.6f + auraT * 0.6f;
                    auraGo.transform.localScale = new Vector3(auraScale, auraScale * 1.5f, 1f);
                }

                float shakeAmt = Mathf.Lerp(0.01f, 0.04f, ratio);
                float shake = Mathf.Sin(elapsed * 35f) * shakeAmt;
                transform.localScale = new Vector3(originalScale.x + shake, originalScale.y, originalScale.z);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (auraGo != null) UnityEngine.Object.Destroy(auraGo);
            if (sr != null) sr.color = originalColor;
            transform.localScale = originalScale;

            // ✦ 0.5秒閃光預警完成，此時正式將攻擊排入延遲釋放佇列 (Queued Attacks Pipeline)
            var releaseTime = Time.time + releaseDelay;
            var newAttack = new PendingAttackInfo
            {
                releaseTime = releaseTime,
                skill = _currentSkill,
                pendingProjectileMove = _pendingProjectileMove,
                plannedDirectDamageFlat = _plannedDirectDamageFlat,
                plannedHitRadius = _plannedHitRadius,
                isExecutingSpecialMove = _isExecutingSpecialMove
            };
            _queuedAttacks.Add(newAttack);

            Debug.Log($"[Monster] ✦ 招式 {_currentSkill?.名稱 ?? "普通攻擊"} 閃光結束！排定在 {releaseDelay} 秒後（實玩第 {releaseTime:F1} 秒）出招！");

            // ✦ 閃光一結束，立刻重設 `_telegraphing = false` 並啟動閃光冷卻
            _telegraphing = false;
            
            // ✦ 配合使用者要求，星等越高的魔物招式冷卻越短、預備閃光越快，發動雨後春筍般的連綿攻勢！
            // 同時融入魔物特異節奏雜湊偏移，使出招節奏各具特色！
            float baseCd = Mathf.Clamp(1.0f - (star - 1) * 0.072f, 0.36f, 1.0f);
            int mHash2 = Mathf.Abs(_魔物編號.GetHashCode());
            _flashCooldownTimer = Mathf.Clamp(baseCd + ((mHash2 % 3) - 1) * 0.08f, 0.25f, 1.25f);

            // ✦ 心裡想好下一招，為下一招的決策做準備
            PickNextMeleePlan();
        }

        IEnumerator ReleaseAttackRoutine(PendingAttackInfo attack)
        {
            Transform activeTarget = _player;
            if (_overrideTarget != null && _overrideTargetTimer > 0f)
                activeTarget = _overrideTarget;

            if (activeTarget == null) yield break;

            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            var postStun = tuning != null && tuning.魔物招式後僵直秒 > 0f ? tuning.魔物招式後僵直秒 : 1.5f;

            // ✦ 在釋放出招期間（出招動畫+後搖僵直），魔物動作被鎖定
            bool isProjectile = attack.pendingProjectileMove != null && !string.IsNullOrWhiteSpace(attack.pendingProjectileMove.投射物型別);
            float animDuration = isProjectile ? 0.2f : 0.32f;
            _stun = animDuration + postStun;

            if (!BattleCombatManager.IsBattleConcluded)
            {
                if (!isProjectile)
                {
                    // 近戰攻擊：突進攻擊效果
                    Vector3 startPos = transform.position;
                    Vector3 dashPos = Vector3.Lerp(startPos, activeTarget.position, 0.35f);
                    
                    float dashElapsed = 0f;
                    float dashDur = 0.12f;
                    while (dashElapsed < dashDur)
                    {
                        dashElapsed += Time.deltaTime;
                        transform.position = Vector3.Lerp(startPos, dashPos, dashElapsed / dashDur);
                        yield return null;
                    }

                    yield return new WaitForSeconds(0.05f);

                    float pullElapsed = 0f;
                    float pullDur = 0.15f;
                    while (pullElapsed < pullDur)
                    {
                        pullElapsed += Time.deltaTime;
                        transform.position = Vector3.Lerp(dashPos, startPos, pullElapsed / pullDur);
                        yield return null;
                    }
                    transform.position = startPos;
                }
                else
                {
                    // 遠程施法類：點頭施法姿勢
                    Vector3 startPos = transform.position;
                    Vector3 nodPos = startPos + (Vector3)(activeTarget.position - startPos).normalized * 0.25f;

                    float nodElapsed = 0f;
                    while (nodElapsed < 0.08f)
                    {
                        nodElapsed += Time.deltaTime;
                        transform.position = Vector3.Lerp(startPos, nodPos, nodElapsed / 0.08f);
                        yield return null;
                    }
                    float returnElapsed = 0f;
                    while (returnElapsed < 0.12f)
                    {
                        returnElapsed += Time.deltaTime;
                        transform.position = Vector3.Lerp(nodPos, startPos, returnElapsed / 0.12f);
                        yield return null;
                    }
                    transform.position = startPos;
                }
            }

            if (activeTarget == null) yield break;
            if (BattleCombatManager.IsBattleConcluded) yield break;

            if (isProjectile)
            {
                MonsterProjectile2D.Fire(transform, activeTarget, attack.pendingProjectileMove, attack.plannedDirectDamageFlat);
            }
            else
            {
                var dist = Vector2.Distance(transform.position, activeTarget.position);
                float checkMul = attack.isExecutingSpecialMove ? 1.35f : 0.8f;
                if (dist <= attack.plannedHitRadius * checkMul)
                {
                    if (activeTarget == _player)
                    {
                        PerformAttackOnPlayer();
                    }
                    else
                    {
                        var receiver = activeTarget.GetComponent<IDamageReceiver>();
                        if (receiver != null)
                        {
                            receiver.ApplyDamage(attack.plannedDirectDamageFlat, false);
                        }
                        else
                        {
                            Debug.Log($"[Monster] 攻擊擊中了隨行寵物，但寵物沒有 IDamageReceiver！");
                        }
                    }
                }
            }
        }



        static Sprite GetOrCreateAuraSprite()
        {
            if (_cachedAuraSprite != null) return _cachedAuraSprite;
            var tex = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 31.5f;
                    float dy = y - 31.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - (dist / 31.5f)); // 圓心到邊緣的柔和漸變光暈
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.9f));
                }
            }
            tex.Apply();
            _cachedAuraSprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 12f); // 12 pixels per unit，製造巨大的光束
            return _cachedAuraSprite;
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
            _currentSkill = null;

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
            var normDmg = Mathf.Max(0, Mathf.RoundToInt(norm.傷害 * 0.7f)); // ✦ 傷害再下修 1/2（降為 0.7 倍），極致提高容錯！

            // ✦ 引入普通基礎攻擊（翡翠綠光）的判定權重，防止魔物 100% 瘋狂出特殊大招，提供合理的近戰連擊與完美防反空隙！
            float normWeight = 32f; 
            float sum = normWeight;

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

            var pick = UnityEngine.Random.value * sum;
            if (pick <= normWeight)
            {
                // ✦ 普通基礎攻擊：清空當前技能綁定，使其正確發出【翡翠流光綠】警示光！
                _plannedDirectDamageFlat = normDmg;
                _isExecutingSpecialMove = false;
                _currentSkill = null;
                _pendingProjectileMove = null;
                Debug.Log($"[MonsterAi] 選招「普通基礎攻擊」直傷={_plannedDirectDamageFlat}，發射綠色警示光！");
                return;
            }

            var acc = normWeight;
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
                    _isExecutingSpecialMove = true;
                    _currentSkill = s; // ✦ 綁定技能
                    Debug.Log($"[MonsterAi] 選招「{(string.IsNullOrEmpty(s.名稱) ? $"特殊招式#{i}" : s.名稱)}」直傷={_plannedDirectDamageFlat}");
                    return;
                }
            }

            _plannedDirectDamageFlat = normDmg;
            _isExecutingSpecialMove = false;
            _currentSkill = null;
        }

        /// <summary>動態距離分界：如果已經選好大招（尤其是遠程），就以大招射程作為追擊終點！</summary>
        float BuildApproachMeleeDistance(out float normalDistOnly)
        {
            var norm = _data?.魔物攻擊內容?.普通攻擊;
            // 配合貼臉攻擊，將接戰距離與上限皆砍半（乘上 0.5f）
            normalDistOnly = norm != null ? Mathf.Max(0.1f, norm.攻擊距離 * 0.5f) : 1f;

            if (_currentSkill != null)
            {
                // 如果已經決定好放特殊招式
                if (!string.IsNullOrWhiteSpace(_currentSkill.投射物型別))
                {
                    // ✦ 若為投射物，直接使用投射物的超大射程作為追擊終點（打個 85 折，確保一定在射程內）！
                    return Mathf.Max(normalDistOnly, _currentSkill.攻擊距離 * 0.85f);
                }
                else
                {
                    // 近戰特殊技，依然拉近
                    float specDist = Mathf.Max(0.1f, _currentSkill.攻擊距離 * 0.5f);
                    return Mathf.Min(Mathf.Max(normalDistOnly, specDist), 2.42f); // 上限亦砍半
                }
            }

            // 如果還沒選招，預設維持近戰距離上限
            return Mathf.Min(normalDistOnly, 2.42f);
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
        public void NotifyPartBreak()
        {
            _breakState.MarkPartBreak();
            TriggerPartBreakStagger("部位破壞！大倒地！");
        }

        /// <summary>切尾成功時呼叫，供掉落「切斷尾巴」條件。</summary>
        public void NotifyTailCut()
        {
            _breakState.MarkTailCut();
            TriggerPartBreakStagger("尾巴切斷！大倒地！");
        }

        private void TriggerPartBreakStagger(string message)
        {
            ClearQueuedAttacks(); // ✦ 打斷目前與蓄力招式
            _stun = 4.5f;        // ✦ 倒地大僵直 4.5 秒
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            
            StartCoroutine(MonsterStaggerVisualRoutine());
            Debug.Log($"[Monster] ✦ {message}，魔物失衡大倒地 4.5 秒！");
        }

        IEnumerator MonsterStaggerVisualRoutine()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null) yield break;
            
            Color orig = sr.color;
            float elapsed = 0f;
            while (elapsed < 4.5f)
            {
                elapsed += Time.deltaTime;
                // 脈動灰色與半透明度
                float alpha = 0.5f + (Mathf.Sin(elapsed * 12f) + 1f) * 0.25f;
                sr.color = new Color(0.6f, 0.6f, 0.6f, alpha);
                yield return null;
            }
            if (sr != null) sr.color = orig;
        }

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

            // ✦ 暴擊時有機率直接觸發部位破壞與斷尾機制！
            if (isCrit && _breakState != null)
            {
                if (!_breakState.曾破壞部位 && UnityEngine.Random.value < 0.15f)
                {
                    NotifyPartBreak();
                    SpawnBreakText("部位破壞！", new Color(1f, 0.6f, 0f)); // 橘黃色
                }
                if (!_breakState.尾巴已切斷 && UnityEngine.Random.value < 0.10f)
                {
                    NotifyTailCut();
                    SpawnBreakText("尾巴切斷！", new Color(0.2f, 0.8f, 1f)); // 亮藍色
                }
            }
        }

        void SpawnBreakText(string text, Color color)
        {
            var go = new GameObject("BreakText");
            go.transform.position = transform.position + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 1.2f, 0f);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = 0.1f;
            mesh.fontSize = 90;
            mesh.color = color;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            
            var mr = mesh.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 5500; // 顯示在最上層

            StartCoroutine(FloatAndFadeBreakText(mesh));
        }

        IEnumerator FloatAndFadeBreakText(TextMesh mesh)
        {
            float dur = 1.5f;
            float elapsed = 0f;
            Vector3 startPos = mesh.transform.position;
            Color startColor = mesh.color;
            Vector3 startScale = Vector3.one * 0.5f;
            Vector3 endScale = Vector3.one * 1.5f;
            
            while (elapsed < dur)
            {
                if (mesh == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                
                // 彈出放大效果
                float scaleT = Mathf.Clamp01(elapsed / 0.15f);
                mesh.transform.localScale = Vector3.Lerp(startScale, endScale, scaleT);
                
                mesh.transform.position = startPos + new Vector3(0f, t * 1.2f, 0f);
                mesh.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
                yield return null;
            }
            if (mesh != null) Destroy(mesh.gameObject);
        }

        private void ClearQueuedAttacks()
        {
            if (_queuedAttacks != null) _queuedAttacks.Clear();
            _telegraphing = false;
            StopAllCoroutines();
        }

        void OnHealthDepleted()
        {
            if (_defeatHandled) return;
            _defeatHandled = true;
            ClearQueuedAttacks();
            OnDefeated?.Invoke();
            StartCoroutine(DefeatFlow());
        }

        IEnumerator DefeatFlow()
        {
            if (_settlement == null)
            {
                _settlement = UnityEngine.Object.FindAnyObjectByType<HuntSettlementService>();
                if (_settlement == null)
                {
                    var go = new GameObject("HuntSettlementService");
                    _settlement = go.AddComponent<HuntSettlementService>();
                }
            }

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
                        (rewards, kills, diffMul) =>
                        {
                            SettlementRewards = rewards;
                            Debug.Log($"[MonsterAi] 結算完成 獲得 {rewards.Count} 個掉落物，當日擊殺數={kills} 難度倍率={diffMul:F3}");
                        }
                    )
                );
                if (err != null) Debug.LogWarning("[MonsterAi] 結算：" + err);
            }

            gameObject.SetActive(false);
        }
    }

    public sealed class Monster3DEffectController : MonoBehaviour
    {
        private SpriteRenderer _mainSr;
        private SpriteRenderer _rimGlowSr;
        private SpriteRenderer _dropShadowSr;
        private float _t;
        private Vector3 _baseScale = Vector3.one;

        void Start()
        {
            _mainSr = GetComponent<SpriteRenderer>();
            if (_mainSr == null) _mainSr = GetComponentInChildren<SpriteRenderer>();

            if (_mainSr != null)
            {
                _baseScale = _mainSr.transform.localScale;
            }

            // ✦ 1. 建立惡魔紅色邊緣背光高光 (Rim Glow / Backlight)，增添立體感與恐懼感
            if (_mainSr != null)
            {
                var glowGo = new GameObject("MonsterRimGlow", typeof(SpriteRenderer));
                glowGo.transform.SetParent(_mainSr.transform, false);
                glowGo.transform.localPosition = new Vector3(0.015f, 0.015f, 0.05f); // 略微往後與偏移
                glowGo.transform.localScale = new Vector3(1.05f, 1.05f, 1f); // 稍微放大

                _rimGlowSr = glowGo.GetComponent<SpriteRenderer>();
                _rimGlowSr.sprite = _mainSr.sprite;
                _rimGlowSr.color = new Color(0.9f, 0.15f, 0.05f, 0.35f); // 鮮紅色惡魔背光
                _rimGlowSr.sortingOrder = _mainSr.sortingOrder - 1; // 剛好在魔物背後
            }

            // ✦ 2. 建立立體感十足的軟黑地面投影陰影 (Drop Shadow)
            var shadowGo = new GameObject("MonsterDropShadow", typeof(SpriteRenderer));
            shadowGo.transform.SetParent(transform, false);
            // 投影在魔物腳下
            shadowGo.transform.localPosition = new Vector3(0f, -0.92f, 0.1f);
            shadowGo.transform.localScale = new Vector3(1.8f, 0.42f, 1f);

            _dropShadowSr = shadowGo.GetComponent<SpriteRenderer>();
            _dropShadowSr.sprite = CreateSoftShadowSprite();
            _dropShadowSr.color = new Color(0f, 0f, 0f, 0.65f); // 透黑
            
            // 確保陰影在最底層
            _dropShadowSr.sortingOrder = _mainSr != null ? _mainSr.sortingOrder - 2 : -10;
        }

        void Update()
        {
            if (_mainSr == null) return;

            // 同步邊緣光精靈與朝向，防止魔物換圖或翻轉時錯位
            if (_rimGlowSr != null)
            {
                _rimGlowSr.sprite = _mainSr.sprite;
                _rimGlowSr.flipX = _mainSr.flipX;
                _rimGlowSr.flipY = _mainSr.flipY;
            }

            _t += Time.deltaTime;

            // ✦ 3. 仿 3D 呼吸與輕微透視旋轉 (Parallax / Breathing)
            // 微微扭動 Y 軸與 Z 軸，模擬在 3D 空間中立體呼吸的立體深度！
            float breatheY = 1.0f + Mathf.Sin(_t * 1.8f) * 0.035f;
            float breatheX = 1.0f + Mathf.Cos(_t * 1.4f) * 0.02f;
            _mainSr.transform.localScale = new Vector3(_baseScale.x * breatheX, _baseScale.y * breatheY, _baseScale.z);

            // 微微傾斜 Y 軸（3D 歐拉角傾斜），在 Unity 2D 中精緻模擬魔物面對我們時的 3D 立體側身感與厚重感！
            float rotY = Mathf.Sin(_t * 1.5f) * 7.5f;  // Y軸左右立體偏擺
            float rotZ = Mathf.Cos(_t * 2.1f) * 2.8f;  // Z軸微微歪斜
            _mainSr.transform.localRotation = Quaternion.Euler(0f, rotY, rotZ);

            // ✦ 4. 惡魔紅色邊緣背光呼吸脈動，營造恐怖壓迫氛圍
            if (_rimGlowSr != null)
            {
                float pulse = 0.35f + Mathf.Sin(_t * 3.5f) * 0.15f;
                // 根據魔物是否正在前搖/出招(閃紅光)來加強壓迫感
                var ai = GetComponent<MonsterAiController>();
                if (ai != null && ai.IsTelegraphing)
                {
                    pulse = 0.65f + Mathf.Sin(_t * 8f) * 0.25f; // 快閃紅光！
                    _rimGlowSr.color = new Color(1f, 0.05f, 0f, pulse);
                }
                else
                {
                    _rimGlowSr.color = new Color(0.9f, 0.15f, 0.05f, pulse);
                }
            }

            // ✦ 5. 地面陰影隨呼吸微微收縮
            if (_dropShadowSr != null)
            {
                float shadowPulse = 1.0f + Mathf.Sin(_t * 1.8f) * 0.08f;
                _dropShadowSr.transform.localScale = new Vector3(1.8f * shadowPulse, 0.42f, 1f);
            }
        }

        private Sprite CreateSoftShadowSprite()
        {
            int size = 64;
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

                    // 軟質放射狀漸變衰減
                    float alpha = Mathf.Clamp01(1f - t);
                    alpha = Mathf.Pow(alpha, 2f) * 0.75f;

                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
