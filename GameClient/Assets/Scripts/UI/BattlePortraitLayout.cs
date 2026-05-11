using UnityEngine;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 直立式戰鬥視角：下方為世界攝影機視口（預設 90%），上方窄條留给 HUD（預覽常用約 10%）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattlePortraitLayout : MonoBehaviour
    {
        [SerializeField] Camera _worldCamera;
        [Range(0.5f, 0.95f)] [SerializeField] float _battlefieldViewportHeight = 0.9f;

        bool _configuredExternally;

        void Start()
        {
            // 若已由 BattlePreviewBootstrap.Configure() 設過，不再覆寫
            if (!_configuredExternally)
                Apply();
        }

        /// <summary>執行期設定戰鬥區高度比例並套用（例如由 <see cref="BattlePreviewBootstrap"/>）。</summary>
        public void Configure(Camera worldCam, float viewportHeight01)
        {
            _worldCamera = worldCam;
            _battlefieldViewportHeight = Mathf.Clamp(viewportHeight01, 0.5f, 0.95f);
            _configuredExternally = true;
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
