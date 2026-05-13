using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 將子物件攻擊框沿獵人身周固定半徑旋轉（割草／倖存者類手感）。
    /// </summary>
    public sealed class MeleeOrbitPivot : MonoBehaviour
    {
        [SerializeField] float _degreesPerSecond = 342f;

        public float DegreesPerSecond { get => _degreesPerSecond; set => _degreesPerSecond = value; }

        void Update()
        {
            transform.Rotate(0f, 0f, _degreesPerSecond * Time.deltaTime);
        }
    }
}
