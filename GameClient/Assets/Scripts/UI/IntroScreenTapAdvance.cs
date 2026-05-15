using UnityEngine;
using UnityEngine.EventSystems;

namespace MonsterHunter.UI
{
    /// <summary>掛在全螢幕可點擊層（需 <see cref="UnityEngine.UI.Image.raycastTarget"/> = true）。</summary>
    [DisallowMultipleComponent]
    public sealed class IntroScreenTapAdvance : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] MobileVillageIntroFlow _flow;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_flow != null) _flow.AdvanceFromTap();
        }
    }
}
