using UnityEngine;

namespace MonsterHunter.Monster
{
    /// <summary>
    /// 示範用：WASD / 方向鍵在 XZ 平面上移動玩家。
    /// 若有 <see cref="CharacterController"/> 則使用 <see cref="CharacterController.Move"/>，可與靜態／Rigidbody 碰撞器擠開，避免整顆穿透魔物；
    /// 否則後備為直接改 <see cref="Transform.position"/>。
    /// </summary>
    public class PlayerNavDemoMove : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6f;

        CharacterController _cc;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
                return;

            Vector3 dir = new Vector3(h, 0f, v).normalized;
            Vector3 delta = dir * (moveSpeed * Time.deltaTime);
            if (_cc != null)
                _cc.Move(delta);
            else
                transform.position += delta;
        }
    }
}
