using UnityEngine;

namespace MonsterHunter.Monster
{
    /// <summary>示範用：WASD / 方向鍵在 XZ 平面上移動玩家，供 <see cref="MonsterController"/> 追擊測試。</summary>
    public class PlayerNavDemoMove : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6f;

        void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
                return;

            Vector3 dir = new Vector3(h, 0f, v).normalized;
            transform.position += dir * (moveSpeed * Time.deltaTime);
        }
    }
}
