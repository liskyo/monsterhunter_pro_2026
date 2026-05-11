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
        float _currentHp;
        bool _defeatHandled;

        public 魔物資料列 DataRow => _data;
        public MonsterBreakState BreakState => _breakState;
        public float MaxHp { get; private set; }
        public float CurrentHp => _currentHp;

        /// <summary>受傷事件：(傷害量, 是否會心)</summary>
        public event Action<float, bool> OnDamageReceived;
        /// <summary>魔物死亡事件</summary>
        public event Action OnDefeated;

        /// <summary>BattleCombatManager 直接注入資料（不需 TextAsset）。</summary>
        public void InjectData(魔物資料列 data, Transform player, CombatTuningStore tuningStore = null)
        {
            _data = data;
            _player = player;
            if (tuningStore != null) _tuningStore = tuningStore;
            if (data != null)
            {
                _currentHp = data.最大血量;
                MaxHp = data.最大血量;
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
            {
                _currentHp = _data.最大血量;
                MaxHp = _data.最大血量;
            }
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
            var atkDist = _data.普通攻擊 != null ? _data.普通攻擊.攻擊距離 : 2f;
            var abandon = detect * Mathf.Max(1f, tuning.追擊放棄倍率);

            switch (_state)
            {
                case AiState.待機:
                    _rb.linearVelocity = Vector2.zero;
                    if (dist <= detect) _state = AiState.追擊;
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

                    var dir = (pl - self).normalized;
                    _rb.linearVelocity = dir * (tuning.玩家移動速度 * Mathf.Max(0.1f, tuning.魔物追擊速度比例));
                    break;

                case AiState.發動招式:
                    if (dist > atkDist * 1.1f)
                    {
                        _state = AiState.追擊;
                        break;
                    }

                    PerformAttackOnPlayer();
                    _stun = tuning.魔物招式後僵直秒;
                    _state = AiState.追擊;
                    break;
            }
        }

        void PerformAttackOnPlayer()
        {
            if (_data?.普通攻擊 == null || _player == null) return;
            var receiver = _player.GetComponent<IDamageReceiver>();
            if (receiver == null) return;
            var raw = _data.普通攻擊.傷害;
            receiver.ApplyDamage(raw, false);
        }

        /// <summary>部位破壞時呼叫，供掉落「破壞部位」條件。</summary>
        public void NotifyPartBreak() => _breakState.MarkPartBreak();

        /// <summary>切尾成功時呼叫，供掉落「切斷尾巴」條件。</summary>
        public void NotifyTailCut() => _breakState.MarkTailCut();

        public void ApplyDamage(float amount, bool isCrit)
        {
            if (_defeatHandled) return;
            _currentHp = Mathf.Max(0f, _currentHp - amount);
            OnDamageReceived?.Invoke(amount, isCrit);
            Debug.Log($"[Monster {_魔物編號}] HP {_currentHp:F0} (-{amount:F1} crit={isCrit})");
            if (_currentHp <= 0f && !_defeatHandled)
            {
                _defeatHandled = true;
                OnDefeated?.Invoke();
                StartCoroutine(DefeatFlow());
            }
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
