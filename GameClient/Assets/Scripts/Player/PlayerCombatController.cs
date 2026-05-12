using UnityEngine;

namespace MonsterHunter.Player
{
    /// <summary>
    /// 結合簡易移動與 Animator 戰鬥表現：<c>Idle</c>／<c>Strafe</c>／<c>Slash</c>。
    /// <list type="bullet">
    ///   <item>預設參數：<c>Bool isMoving</c>、<c>Trigger Attack</c>。</item>
    ///   <item>使用舊版 Input Manager 的 <c>Horizontal</c>／<c>Vertical</c> 以及滑鼠左鍵。</item>
    ///   <item>處於 <c>Slash</c> State（含切入該狀態的 Transition）期間會鎖住位移並禁止再次按下攻擊。</item>
    /// </list>
    /// <para>建議結構：<b>根節點</b>掛位移與本腳本；子物件 <b>Body</b> 僅負載 Skinned／Mesh Renderer + Animator。Slash 對 Body 施加旋轉時，根部水平位置與倒下感會正常許多。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCombatController : MonoBehaviour
    {
        [Header("動畫")]
        [Tooltip(
            "建議為子物件 Body 上的 Animator（與網格同層）。根節點掛本腳本 + CharacterController，可避免 Slash 旋轉整個位移控制器。")]
        [SerializeField] Animator _animator;

        [Tooltip("Animator Bool 名稱，請與 Controller 內定義一致（預設 isMoving）。")]
        [SerializeField] string _isMovingBoolName = "isMoving";

        [Tooltip("Animator Trigger 名稱（預設 Attack）。")]
        [SerializeField] string _attackTriggerName = "Attack";

        [Tooltip("對應 Animator 視窗中的「Slash」State 名稱，用以判斷是否鎖行為。")]
        [SerializeField] string _slashStateName = "Slash";

        [Tooltip("要讀取的 Animator Layer Index，一般基底層為 0。")]
        [SerializeField] int _animatorLayerIndex;

        [Header("移動")]
        [Tooltip("位移速度（單位：世界座標每秒；僅套用於水平 X／Z）。")]
        public float moveSpeed = 6f;

        CharacterController _characterController;

        void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            if (_animator == null)
                _animator = GetComponent<Animator>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_animator == null)
                Debug.LogError("[PlayerCombatController] 找不到 Animator。", this);
        }

        void Update()
        {
            if (_animator == null)
                return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool hasMoveInput =
                !Mathf.Approximately(h, 0f) || !Mathf.Approximately(v, 0f);

            bool inSlash = IsSlashDominantPeriod();

            // Slash 鎖定期：強制視為無移動、不發動新攻擊
            _animator.SetBool(_isMovingBoolName, !inSlash && hasMoveInput);

            if (!inSlash)
            {
                if (hasMoveInput)
                {
                    Vector3 dir = new Vector3(h, 0f, v).normalized;
                    Vector3 delta = dir * (moveSpeed * Time.deltaTime);
                    if (_characterController != null)
                        _characterController.Move(delta);
                    else
                        transform.position += delta;
                }

                if (Input.GetMouseButtonDown(0))
                    _animator.SetTrigger(_attackTriggerName);
            }
        }

        /// <summary>
        /// 判斷目前是否視為「在 Slash」：已在 Slash State，或是 Transition 準備進入 Slash。
        /// 如此可在揮砍動畫與 Idle 之間的 blending 區間也鎖住行為。
        /// </summary>
        bool IsSlashDominantPeriod()
        {
            int layer = _animatorLayerIndex;
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(layer);

            // 已由其它狀態切入 Slash（Transition 中，下一站為 Slash）
            if (_animator.IsInTransition(layer))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(layer);
                if (next.IsName(_slashStateName))
                    return true;
            }

            // 當前正在 Slash 本體動畫
            return current.IsName(_slashStateName);
        }
    }
}
