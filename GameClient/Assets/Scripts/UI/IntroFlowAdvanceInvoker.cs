using UnityEngine;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 綁定在按鈕 <see cref="UnityEngine.Events.UnityEvent"/> 上呼叫 <see cref="MobileVillageIntroFlow.AdvanceFromTap"/>，
    /// 確保編輯器產場景時可把 listener 序列化進場景。
    /// </summary>
    public sealed class IntroFlowAdvanceInvoker : MonoBehaviour
    {
        [SerializeField] MobileVillageIntroFlow _flow;

        public void Advance()
        {
            if (_flow != null) _flow.AdvanceFromTap();
        }
    }
}
