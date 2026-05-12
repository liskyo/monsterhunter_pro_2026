using System.Collections;
using MonsterHunter.Combat;
using UnityEngine;

// =============================================================================
// 【安裝與 Inspector 設定說明】
// -----------------------------------------------------------------------------
// 1. 本專案目前使用「舊版 Input Manager」（無 com.unity.inputsystem 套件），
//    因此本腳本使用 Input.GetAxis / Input.GetMouseButton。
//
// 2. 在角色根物件上：
//    - Add Component → 搜尋「MonsterHunter.Player.PlayerController」並掛上
//    （若清單只顯示名稱，找「PlayerController」，注意舊專案內
//     Assets/Scripts/Controllers 下另有一支不同用途的 PlayerController，
//     請確認命名空間為 MonsterHunter.Player）
//
// 3. Animator 設定：
//    - 同一物件（或子物件）上必須有 Animator 元件。
//    - Animator Controller 內請新增一個 Trigger 類型參數，名稱必須為「Attack」
//      （與程式中 attackTriggerName 預設一致）。
//    - 建議在「攻擊」Animation Clip 的尾端加 Animation Event，呼叫
//      PlayerController.OnAttackAnimationEnd()，以便精準在動畫結束時解除鎖定。
//      若未加事件，則使用 Inspector 中的「攻擊鎖定時間」做後備計時。
//
// 4. Input Manager 軸向（Edit → Project Settings → Input Manager）：
//    - 預設已有 Horizontal / Vertical；若自訂名稱，請在 Inspector 修改
//      horizontalAxisName / verticalAxisName。
//
// 5. 移動元件：可選 Rigidbody（建議含 Freeze Rotation）或 CharacterController；
//    請在 Inspector 指定使用哪一種，並避免同時勾選兩種位移以免造成重複移動。
//
// 6. 3D 武器判定：在右手（例如 mixamorig:RightHand）子物件 WeaponHitbox 上掛
//    MonsterHunter.Combat.WeaponHitbox。
//    本腳本可於按左鍵攻擊時短暶呼叫 EnableHitbox／DisableHitbox；
//    若攻擊動畫已有 Animation Event 呼叫同一對方法，請取消勾選「從程式開啟武器判定窗」，
//    或只擇一種方式，避免判定窗被提早關閉。
//
// 7. 面向魔物：場景中需有 Tag「Monster」。僅在距離內（預設 10m）會平滑 yaw 轉向，
//    並鎖水平面（不依魔物高低仰俯）。請 Tag 標在可被搜尋的物件上（或單一元形根）。
// =============================================================================

namespace MonsterHunter.Player
{
    /// <summary>玩家控制器：簡易三態狀態機（閒置 / 移動 / 攻擊）。</summary>
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        const string MonsterTag = "Monster";
        /// <summary>行為狀態（狀態機三態）。</summary>
        public enum PlayerState
        {
            /// <summary>無移動輸入且未攻擊。</summary>
            Idle,
            /// <summary>有前後左右移動輸入。</summary>
            Move,
            /// <summary>攻擊中：鎖定移動輸入。</summary>
            Attack
        }

        [Header("狀態（唯讀）")]
        [SerializeField] PlayerState _currentState;

        [Header("移動")]
        [Tooltip("水平軸名稱，對應 Input Manager 的 Axis Name，預設 Horizontal")]
        [SerializeField] string horizontalAxisName = "Horizontal";
        [Tooltip("垂直軸名稱，對應 Input Manager 的 Axis Name，預設 Vertical")]
        [SerializeField] string verticalAxisName = "Vertical";
        [SerializeField] float moveSpeed = 5f;
        [Tooltip("移動方向是否沿世界座標（true）或依本物件 forward/right（false）")]
        [SerializeField] bool useWorldSpaceMove = true;

        [Header("物理移動方式（二擇一）")]
        [SerializeField] bool useRigidbody;
        [SerializeField] Rigidbody attachedRigidbody;
        [SerializeField] bool useCharacterController;
        [SerializeField] CharacterController characterController;

        [Header("動畫")]
        [SerializeField] Animator animator;
        [Tooltip("Animator 中 Trigger 參數名稱，須與 Controller 內一致")]
        [SerializeField] string attackTriggerName = "Attack";
        [Tooltip("移動時設定的 Bool 參數名稱；若 Animator 無此參數可留空")]
        [SerializeField] string movingBoolParameterName = "IsMoving";
        [Tooltip("攻擊期間若未使用 Animation Event 結束，則用此秒數強制結束攻擊鎖定")]
        [SerializeField] float attackLockDurationFallback = 0.6f;

        [Header("武器 Hitbox（3D，選用）")]
        [Tooltip("留空則執行時會 GetComponentInChildren 尋找 WeaponHitbox（含未啟用子物件）。")]
        [SerializeField] WeaponHitbox _weaponHitbox3D;
        [Tooltip("按攻擊鍵時，開啟 BoxCollider 與 isAttacking 的秒數；設為 0 則不由此腳本開窗（改由動畫事件）。")]
        [SerializeField] float _weaponHitboxActiveSeconds = 0.22f;
        [Tooltip("若攻擊 Clip 已用 Animation Event 呼叫 WeaponHitbox.EnableHitbox／DisableHitbox，請取消勾選，避免與程式計時打架。")]
        [SerializeField] bool _driveWeaponHitboxFromInput = true;

        [Header("面向魔物（水平）")]
        [SerializeField] bool _autoFaceNearestMonster = true;
        [Tooltip("與任一 Monster 標籤物件之水平距離≤此值才自動轉身（公尺）。")]
        [SerializeField] float _autoFaceMonsterMaxRange = 10f;
        [Tooltip("每秒最多轉動的偏航角（度），越大越快對準。")]
        [SerializeField] float _autoFaceYawDegreesPerSecond = 720f;
        [Tooltip("攻擊硬直／揮砍中是否仍對準魔物（建議開啟以避免背對揮空）。")]
        [SerializeField] bool _autoFaceDuringAttack = true;
        [Tooltip("重新搜尋「最近魔物」的間隔秒數（避免每幀 FindGameObjectsWithTag）。")]
        [SerializeField] float _autoFaceMonsterRescanSeconds = 0.2f;

        Coroutine _weaponHitboxRoutine;

        Transform _nearestMonsterTagged;
        float _monsterScanTimer;

        /// <summary>攻擊鎖定計時（剩餘秒數，>0 表示仍在攻擊狀態鎖定移動）。</summary>
        float _attackTimer;

        /// <summary>目前對外唯讀狀態。</summary>
        public PlayerState CurrentState => _currentState;

        void Reset()
        {
            TryGetComponent(out attachedRigidbody);
            TryGetComponent(out characterController);
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        void Awake()
        {
            if (attachedRigidbody == null)
                TryGetComponent(out attachedRigidbody);
            if (characterController == null)
                TryGetComponent(out characterController);
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (useRigidbody && useCharacterController)
            {
                Debug.LogWarning(
                    "[MonsterHunter.Player.PlayerController] 同時啟用 Rigidbody 與 CharacterController，請只選一種，避免角色重複位移。");
            }
        }

        void Update()
        {
            bool attackLocked = _attackTimer > 0f;

            // 滑鼠左鍵：僅在「當前狀態不是 Attack」時可進入攻擊（避免連砍或重設 Trigger）
            if (Input.GetMouseButtonDown(0) && _currentState != PlayerState.Attack)
            {
                BeginAttack();
            }

            Vector2 input = ReadMoveInput();

            if (attackLocked)
            {
                // 攻擊期間：不讀取移動來改狀態，並在 FixedUpdate 內停止水平速度
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0f)
                    EndAttackFromTimer();
                SetAnimatorMoving(false);
                if (_autoFaceDuringAttack)
                    TryAutoFaceNearestMonster();
                return;
            }

            bool hasMove = input.sqrMagnitude > 0.0001f;
            if (hasMove)
            {
                _currentState = PlayerState.Move;
                SetAnimatorMoving(true);
            }
            else
            {
                _currentState = PlayerState.Idle;
                SetAnimatorMoving(false);
            }

            TryAutoFaceNearestMonster();
        }

        void FixedUpdate()
        {
            if (_attackTimer > 0f)
            {
                StopHorizontalMotion();
                return;
            }

            Vector2 input = ReadMoveInput();
            if (input.sqrMagnitude < 0.0001f)
                return;

            Vector3 dir = useWorldSpaceMove
                ? new Vector3(input.x, 0f, input.y).normalized
                : (transform.right * input.x + transform.forward * input.y).normalized;

            ApplyMovement(dir * moveSpeed);
        }

        Vector2 ReadMoveInput()
        {
            if (_attackTimer > 0f)
                return Vector2.zero;

            float h = Input.GetAxisRaw(horizontalAxisName);
            float v = Input.GetAxisRaw(verticalAxisName);
            return new Vector2(h, v);
        }

        void BeginAttack()
        {
            if (_currentState == PlayerState.Attack)
                return;

            _currentState = PlayerState.Attack;
            _attackTimer = Mathf.Max(attackLockDurationFallback, 0.05f);
            StopHorizontalMotion();

            if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
                animator.SetTrigger(attackTriggerName);

            SetAnimatorMoving(false);

            StartWeaponHitboxWindow();
        }

        void StartWeaponHitboxWindow()
        {
            if (!_driveWeaponHitboxFromInput || _weaponHitboxActiveSeconds <= 0f)
                return;

            if (_weaponHitbox3D == null)
                _weaponHitbox3D = GetComponentInChildren<WeaponHitbox>(true);

            if (_weaponHitbox3D == null)
                return;

            if (_weaponHitboxRoutine != null)
                StopCoroutine(_weaponHitboxRoutine);

            _weaponHitboxRoutine = StartCoroutine(CoWeaponHitboxWindow());
        }

        IEnumerator CoWeaponHitboxWindow()
        {
            _weaponHitbox3D.EnableHitbox();
            yield return new WaitForSeconds(_weaponHitboxActiveSeconds);
            _weaponHitbox3D.DisableHitbox();
            _weaponHitboxRoutine = null;
        }

        void EndAttackFromTimer()
        {
            _attackTimer = 0f;
            // 回到 Idle 或 Move 由下一個 Update 依輸入決定
            Vector2 input = ReadMoveInput();
            _currentState = input.sqrMagnitude > 0.0001f ? PlayerState.Move : PlayerState.Idle;
        }

        /// <summary>
        /// 建議在「攻擊」動畫最後一幀加入 Animation Event 指向此方法，
        /// 可早於 fallback 計時結束攻擊硬直（精準對齊動畫）。
        /// </summary>
        public void OnAttackAnimationEnd()
        {
            if (_currentState != PlayerState.Attack && _attackTimer <= 0f)
                return;

            _attackTimer = 0f;
            Vector2 input = ReadMoveInput();
            _currentState = input.sqrMagnitude > 0.0001f ? PlayerState.Move : PlayerState.Idle;
        }

        void SetAnimatorMoving(bool moving)
        {
            if (animator == null || string.IsNullOrEmpty(movingBoolParameterName))
                return;
            animator.SetBool(movingBoolParameterName, moving);
        }

        void ApplyMovement(Vector3 velocityXZ)
        {
            if (useRigidbody && attachedRigidbody != null)
            {
                var v = attachedRigidbody.linearVelocity;
                attachedRigidbody.linearVelocity = new Vector3(velocityXZ.x, v.y, velocityXZ.z);
                return;
            }

            if (useCharacterController && characterController != null)
            {
                Vector3 motion = velocityXZ * Time.fixedDeltaTime;
                characterController.Move(motion);
                return;
            }

            // 無 Rigidbody / CharacterController 時的簡易位移（僅供原型）
            transform.position += velocityXZ * Time.fixedDeltaTime;
        }

        void StopHorizontalMotion()
        {
            if (useRigidbody && attachedRigidbody != null)
            {
                var v = attachedRigidbody.linearVelocity;
                attachedRigidbody.linearVelocity = new Vector3(0f, v.y, 0f);
                return;
            }

            // CharacterController：不施加持續速度即可，不額外「滑行」
        }

        void TryAutoFaceNearestMonster()
        {
            if (!_autoFaceNearestMonster || _autoFaceMonsterMaxRange <= 0f)
                return;

            RefreshNearestTaggedMonster();

            Transform target = _nearestMonsterTagged;
            if (target == null)
                return;

            Vector3 to = target.position - transform.position;
            to.y = 0f;
            float maxR = _autoFaceMonsterMaxRange;
            if (to.sqrMagnitude > maxR * maxR || to.sqrMagnitude < 0.0001f)
                return;

            float targetYaw = Quaternion.LookRotation(to.normalized, Vector3.up).eulerAngles.y;
            float yaw = Mathf.MoveTowardsAngle(
                transform.eulerAngles.y,
                targetYaw,
                _autoFaceYawDegreesPerSecond * Time.deltaTime);

            Vector3 e = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(e.x, yaw, e.z);
        }

        void RefreshNearestTaggedMonster()
        {
            _monsterScanTimer -= Time.deltaTime;
            if (_monsterScanTimer > 0f
                && _nearestMonsterTagged != null
                && _nearestMonsterTagged.gameObject.activeInHierarchy)
                return;

            _monsterScanTimer = Mathf.Max(_autoFaceMonsterRescanSeconds, 0.05f);

            try
            {
                GameObject[] tagged = GameObject.FindGameObjectsWithTag(MonsterTag);
                Vector3 p = transform.position;
                float maxSq = _autoFaceMonsterMaxRange * _autoFaceMonsterMaxRange;
                float bestSq = float.MaxValue;
                Transform pick = null;

                for (int i = 0; i < tagged.Length; i++)
                {
                    GameObject go = tagged[i];
                    if (go == null || !go.activeInHierarchy)
                        continue;

                    Vector3 d = go.transform.position - p;
                    float dxz = d.x * d.x + d.z * d.z;
                    if (dxz > maxSq)
                        continue;
                    if (dxz < bestSq)
                    {
                        bestSq = dxz;
                        pick = go.transform;
                    }
                }

                _nearestMonsterTagged = pick;
            }
            catch (UnityException)
            {
                // 未定義 Monster tag
                _nearestMonsterTagged = null;
            }
        }
    }
}
