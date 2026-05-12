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
// =============================================================================

namespace MonsterHunter.Player
{
    /// <summary>玩家控制器：簡易三態狀態機（閒置 / 移動 / 攻擊）。</summary>
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
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
    }
}
