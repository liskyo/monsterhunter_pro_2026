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
                        
                        // ✦ 招式釋放時間快慢區別！技能越強 (傷害倍率越高)，速度越快！
                        float baseTelegraph = tuning.魔物攻擊前搖秒 > 0f ? tuning.魔物攻擊前搖秒 : 0.42f;
                        float telegraph = baseTelegraph;
                        if (_currentSkill != null)
                        {
                            float strength = _currentSkill.傷害對普攻倍率 > 0f ? _currentSkill.傷害對普攻倍率 : 1f;
                            telegraph = baseTelegraph / strength;
                            // 防呆：前搖不低於 0.15s，不高於 1.2s
                            telegraph = Mathf.Clamp(telegraph, 0.15f, 1.2f);
                        }
                        
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
            var sr = GetComponentInChildren<SpriteRenderer>();
            var originalColor = sr != null ? sr.color : Color.white;
            
            // 讀取當前招式名稱，動態建立包含技能名稱的 Pill 提示框！
            string skillName = _currentSkill != null ? _currentSkill.名稱 : "";
            var warningGo = BuildWarningSign(skillName);

            var originalScale = transform.localScale;

            if (delay > 0f)
            {
                var elapsed = 0f;
                // ✦ 不同技能使用不同前搖警告混色與脈動頻率！
                Color warnColor = new Color(1f, 0.42f, 0.32f, 1f); // 預設橙紅
                float pulseFreq = 12f;

                if (skillName == "飛撲壓制") { warnColor = new Color(0.92f, 0.05f, 0.05f); pulseFreq = 18f; }
                else if (skillName == "熊掌橫掃") { warnColor = new Color(1f, 0.45f, 0f); pulseFreq = 22f; }
                else if (skillName == "落雷角") { warnColor = new Color(0f, 0.85f, 1f); pulseFreq = 28f; }
                else if (skillName == "岩塊投擲") { warnColor = new Color(0.6f, 0.4f, 0.2f); pulseFreq = 14f; }
                else if (skillName == "桃紅彈") { warnColor = new Color(1f, 0.25f, 0.72f); pulseFreq = 16f; }

                // ✦ 建立巨大且極度顯眼的「技能專屬顏色光環/光束」在魔物背後！絕對不可能看不到！
                var auraGo = new GameObject("TelegraphAura");
                auraGo.transform.SetParent(transform, false);
                auraGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                var auraSr = auraGo.AddComponent<SpriteRenderer>();
                auraSr.sprite = GetOrCreateAuraSprite();
                auraSr.sortingOrder = -5; // 位於魔物本體背後的巨大背光
                
                while (elapsed < delay)
                {
                    float ratio = elapsed / delay;
                    if (sr != null)
                    {
                        // ✦ 極度暴力的電玩風硬閃爍 (Strobe effect)！徹底解決看不清楚的問題！
                        float t = Mathf.PingPong(elapsed * pulseFreq, 1f);
                        // 當 t > 0.35 時，強制 100% 覆蓋為專屬高亮警戒色，否則為原本顏色
                        sr.color = t > 0.35f ? warnColor : originalColor;
                    }

                    // ✦ Aura 光環進行強烈的色彩與大小脈動！
                    if (auraSr != null)
                    {
                        float auraT = Mathf.PingPong(elapsed * pulseFreq * 0.4f, 1f);
                        auraSr.color = Color.Lerp(warnColor, new Color(1f, 1f, 1f, 0.85f), auraT); // 更明亮的漸變！
                        
                        // ✦ 必殺大招如「落雷角」或大體積攻擊（飛撲壓制、熊掌橫掃）展現更巨大的發光氣場！
                        float baseScale = (skillName == "落雷角" || skillName == "飛撲壓制" || skillName == "熊掌橫掃") ? 2.5f : 1.4f;
                        float auraScale = baseScale + Mathf.Sin(elapsed * pulseFreq) * 0.5f;
                        // 上下稍微拉長，營造出強烈的氣場或光束感
                        auraGo.transform.localScale = new Vector3(auraScale, auraScale * 2.2f, 1f);
                    }

                    // 統一的簡單震動效果，不要做過多形變，以免擾亂視覺
                    float shake = Mathf.Sin(elapsed * 24f) * 0.03f;
                    transform.localScale = new Vector3(originalScale.x + shake, originalScale.y, originalScale.z);

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (auraGo != null) UnityEngine.Object.Destroy(auraGo);
                if (sr != null) sr.color = originalColor;
                transform.localScale = originalScale;
            }

            // ✦ 招式發放瞬間物理動畫統一簡化
            bool isProjectile = _pendingProjectileMove != null && !string.IsNullOrWhiteSpace(_pendingProjectileMove.投射物型別);
            
            if (_player != null && !BattleCombatManager.IsBattleConcluded)
            {
                if (!isProjectile)
                {
                    // 近戰攻擊：統一的突進攻擊效果
                    Vector3 startPos = transform.position;
                    Vector3 dashPos = Vector3.Lerp(startPos, _player.position, 0.35f);
                    
                    float dashElapsed = 0f;
                    float dashDur = 0.12f;
                    while (dashElapsed < dashDur)
                    {
                        dashElapsed += Time.deltaTime;
                        transform.position = Vector3.Lerp(startPos, dashPos, dashElapsed / dashDur);
                        yield return null;
                    }

                    // 停頓打擊感
                    yield return new WaitForSeconds(0.05f);

                    // 收招撤回
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
                    // 遠程施法類：統一的簡單點頭施法姿勢
                    Vector3 startPos = transform.position;
                    Vector3 nodPos = startPos + (Vector3)(_player.position - startPos).normalized * 0.25f;

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
            // ✦ 針對近戰與橫掃特別技給予合理的大範圍判定（例如 1.35 倍半徑），普通攻擊為 0.8 倍，使大招極難站樁硬吃
            float checkMul = _isExecutingSpecialMove ? 1.35f : 0.8f;
            if (dist <= _plannedHitRadius * checkMul)
                PerformAttackOnPlayer();
            _pendingProjectileMove = null;
        }

        /// <summary>在魔物頭上建立包含技能名稱的 Pill 提示框物件。</summary>
        GameObject BuildWarningSign(string skillName)
        {
            var go = new GameObject("AttackWarning");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.45f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            
            bool isNormalAttack = string.IsNullOrEmpty(skillName);
            // 根據不同技能決定背景方塊顏色，微透明更有質感
            Color bgColor = new Color(1f, 0.9f, 0f, 0.8f); // 預設亮黃
            if (!isNormalAttack)
            {
                if (skillName == "飛撲壓制") bgColor = new Color(0.85f, 0f, 0.05f, 0.85f); // 亮血紅
                else if (skillName == "熊掌橫掃") bgColor = new Color(0.95f, 0.42f, 0f, 0.85f); // 亮橘色
                else if (skillName == "落雷角") bgColor = new Color(0f, 0.55f, 1f, 0.85f); // 閃電藍
                else if (skillName == "岩塊投擲") bgColor = new Color(0.55f, 0.35f, 0.15f, 0.85f); // 黏土褐
                else if (skillName == "桃紅彈") bgColor = new Color(0.95f, 0.15f, 0.65f, 0.85f); // 桃粉紅
            }

            sr.sprite = CreateSolidSprite(bgColor);
            sr.sortingOrder = 3000;
            
            // 動態依據是否為普攻來決定背景寬度
            float bgWidth = isNormalAttack ? 0.35f : 1.15f;
            float bgHeight = 0.46f;
            go.transform.localScale = new Vector3(bgWidth, bgHeight, 1f);

            var txtGo = new GameObject("WarnText");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            
            // 抵消父物件的比例，維持文字不變形
            txtGo.transform.localScale = new Vector3(1f / bgWidth * 0.35f, 1f / bgHeight * 0.35f, 1f);

            var tm = txtGo.AddComponent<TextMesh>();
            tm.text = isNormalAttack ? "!" : skillName;
            tm.fontSize = 24;
            tm.fontStyle = FontStyle.Bold;
            tm.color = (bgColor.r < 0.4f || bgColor.g < 0.4f) ? Color.white : Color.black;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            
            // ✦ 設定 TextMesh Renderer 的 SortingOrder 以保證在背景方塊之上！
            var mr = txtGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 3001;

            return go;
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
            var normDmg = Mathf.Max(0, Mathf.RoundToInt(norm.傷害 * 1.4f)); // ✦ 傷害下修 1/2（降為 1.4 倍），維持合理的高容錯挑戰！

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
                _isExecutingSpecialMove = false;
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
                    _isExecutingSpecialMove = true;
                    Debug.Log($"[MonsterAi] 選招「{(string.IsNullOrEmpty(s.名稱) ? $"特殊招式#{i}" : s.名稱)}」直傷={_plannedDirectDamageFlat}");
                    return;
                }
            }

            _plannedDirectDamageFlat = normDmg;
            _isExecutingSpecialMove = false;
        }

        /// <summary>追擊／站樁分界：只吃「近戰型」招式距離，不含投射物遠射程，並封頂避免遠距離站樁不動。</summary>
        float BuildApproachMeleeDistance(out float normalDistOnly)
        {
            var norm = _data?.魔物攻擊內容?.普通攻擊;
            // 配合貼臉攻擊，將接戰距離與上限皆砍半（乘上 0.5f）
            normalDistOnly = norm != null ? Mathf.Max(0.1f, norm.攻擊距離 * 0.5f) : 1f;
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
                    atkDist = Mathf.Max(atkDist, s.攻擊距離 * 0.5f);
                }
            }

            const float moveCap = 2.42f; // 上限亦砍半
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

        void OnHealthDepleted()
        {
            if (_defeatHandled) return;
            _defeatHandled = true;
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
}
