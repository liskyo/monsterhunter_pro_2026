import fs from 'fs';

const filePath = 'C:/Users/liskyo/Desktop/MonsterHunter_2026/GameClient/Assets/Scripts/Controllers/MonsterAiController.cs';
let content = fs.readFileSync(filePath, 'utf8');

// Normalize line endings
content = content.replace(/\r\n/g, '\n');

// 1. In InjectData, add PickNextMeleePlan()
content = content.replace(
`                var m = Mathf.Max(0.1f, monsterMaxHpMultiplier);
                InitializeMonsterHealthFromData(Mathf.Max(1f, data.最大血量 * m));
            }
        }`,
`                var m = Mathf.Max(0.1f, monsterMaxHpMultiplier);
                InitializeMonsterHealthFromData(Mathf.Max(1f, data.最大血量 * m));
                PickNextMeleePlan(); // ✦ 初次遇敵直接心裡想好第一招
            }
        }`);

// 2. Add Start() method after Awake()
content = content.replace(
`        void Awake()
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
        }`,
`        void Awake()
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

        void Start()
        {
            if (_currentSkill == null) PickNextMeleePlan();
        }`);

// 3. Update case AiState.追擊: add zig-zag sway
content = content.replace(
`                    var dir = dist > 0.05f ? (pl - self).normalized : Vector2.zero;
                    var baseChase = tuning.玩家移動速度 *
                                    Mathf.Max(0.1f, tuning.魔物追擊速度比例);
                    var chaseMag = baseChase;
                    if (tuning.魔物追擊低速底線 > 1e-3f)
                        chaseMag = Mathf.Max(tuning.魔物追擊低速底線, baseChase);
                    _rb.linearVelocity = dir * chaseMag;
                    break;`,
`                    var dir = dist > 0.05f ? (pl - self).normalized : Vector2.zero;
                    var baseChase = tuning.玩家移動速度 *
                                    Mathf.Max(0.1f, tuning.魔物追擊速度比例);
                    var chaseMag = baseChase;
                    if (tuning.魔物追擊低速底線 > 1e-3f)
                        chaseMag = Mathf.Max(tuning.魔物追擊低速底線, baseChase);

                    // ✦ 加入蛇行 / 側步偏移量
                    var perp = new Vector2(-dir.y, dir.x);
                    var sway = Mathf.Sin(Time.time * 2.5f) * 0.4f * chaseMag; // 隨時間左右橫移
                    
                    _rb.linearVelocity = dir * chaseMag + perp * sway;
                    break;`);

// 4. Update case AiState.發動招式: remove PickNextMeleePlan()
content = content.replace(
`                case AiState.發動招式:
                    if (dist > atkDist * 1.1f)
                    {
                        _state = AiState.追擊;
                        break;
                    }

                    if (!_telegraphing)
                    {
                        PickNextMeleePlan();
                        _telegraphing = true;`,
`                case AiState.發動招式:
                    if (dist > atkDist * 1.15f) // ✦ 稍微放寬 1.15f 避免距離邊緣判斷浮動抖動
                    {
                        _state = AiState.追擊;
                        break;
                    }

                    if (!_telegraphing)
                    {
                        // ✦ 已經提早選好招式了，不須再 PickNextMeleePlan()
                        _telegraphing = true;`);

// 5. TelegraphAndAttack end: add PickNextMeleePlan()
content = content.replace(
`            if (dist <= _plannedHitRadius * checkMul)
                PerformAttackOnPlayer();
            _pendingProjectileMove = null;
        }`,
`            if (dist <= _plannedHitRadius * checkMul)
                PerformAttackOnPlayer();
            _pendingProjectileMove = null;

            // ✦ 當前攻擊結束，立刻「心裡想好下一招」！
            PickNextMeleePlan();
        }`);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('AI upgrade successfully applied!');
