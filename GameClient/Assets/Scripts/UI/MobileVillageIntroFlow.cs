using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 手機開場：標題→貓飯食堂→商店（可選）→<see cref="_nextSceneName"/>（預設 Bootstrap）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileVillageIntroFlow : MonoBehaviour
    {
        public enum Phase
        {
            TitleScreen = 0,
            CanteenScreen = 1,
            ShopScreen = 2,
        }

        [SerializeField] GameObject _panelTitle;
        [SerializeField] GameObject _panelCanteen;
        [SerializeField] GameObject _panelShop;
        [Tooltip("需在 File → Build Settings 內並使用「場景檔案名」（不含副檔名）。")]
        [SerializeField] string _nextSceneName = "Bootstrap";

        Phase _phase = Phase.TitleScreen;

        void Awake()
        {
            if (_panelShop == null)
                CreateBuiltInShopPanel();

            if (_panelTitle != null) _panelTitle.SetActive(true);
            if (_panelCanteen != null) _panelCanteen.SetActive(false);
            if (_panelShop != null) _panelShop.SetActive(false);
            _phase = Phase.TitleScreen;
        }

        void CreateBuiltInShopPanel()
        {
            var go = new GameObject("Panel_Shop", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            StretchFull(rt);
            go.SetActive(false);
            var shop = go.AddComponent<VillageShopScreen>();
            shop.Initialize(this);
            _panelShop = go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>由全螢幕透明按鈕／<see cref="IntroScreenTapAdvance"/> 呼叫。</summary>
        public void AdvanceFromTap()
        {
            switch (_phase)
            {
                case Phase.TitleScreen:
                    if (_panelTitle != null) _panelTitle.SetActive(false);
                    if (_panelCanteen != null) _panelCanteen.SetActive(true);
                    _phase = Phase.CanteenScreen;
                    break;
                case Phase.CanteenScreen:
                    if (_panelShop != null)
                    {
                        if (_panelCanteen != null) _panelCanteen.SetActive(false);
                        _panelShop.SetActive(true);
                        _phase = Phase.ShopScreen;
                    }
                    else if (!string.IsNullOrWhiteSpace(_nextSceneName))
                        SceneManager.LoadScene(_nextSceneName.Trim());
                    break;
                case Phase.ShopScreen:
                    break;
            }
        }

        /// <summary>商店「出發狩獵」：載入戰鬥／下一段場景。</summary>
        public void ContinueFromShopToBattle()
        {
            if (!string.IsNullOrWhiteSpace(_nextSceneName))
                SceneManager.LoadScene(_nextSceneName.Trim());
        }
    }
}
