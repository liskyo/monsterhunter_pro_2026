using UnityEngine;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 直立式 60/40 戰鬥視角：下方 60% 為世界攝影機視口，上方 40% 預留给 HUD（需另放 Screen Space Camera 或 Overlay）。
    /// 符合 .cursorrules「60/40 戰鬥視角模型」；HUD 請配合九宮格錨點與 Safe Area。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattlePortraitLayout : MonoBehaviour
    {
        [SerializeField] Camera _worldCamera;
        [Range(0.5f, 0.85f)] [SerializeField] float _battlefieldViewportHeight = 0.6f;

        void Start()
        {
            Apply();
        }

        public void Apply()
        {
            if (_worldCamera == null) _worldCamera = Camera.main;
            if (_worldCamera == null) return;

            var h = Mathf.Clamp01(_battlefieldViewportHeight);
            _worldCamera.rect = new Rect(0f, 0f, 1f, h);
        }
    }
}
