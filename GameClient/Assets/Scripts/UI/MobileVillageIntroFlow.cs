using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 手機開場：標題畫有「進入遊戲」按鈕進入貓飯；貓飯螢幕點畫面載入戰鬥
    /// <see cref="_nextSceneName"/>（預設 Bootstrap）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileVillageIntroFlow : MonoBehaviour
    {
        public enum Phase
        {
            TitleScreen = 0,
            CanteenScreen = 1,
        }

        [SerializeField] GameObject _panelTitle;
        [SerializeField] GameObject _panelCanteen;
        [Tooltip("需在 File → Build Settings 內並使用「場景檔案名」（不含副檔名）。")]
        [SerializeField] string _nextSceneName = "Bootstrap";

        Phase _phase = Phase.TitleScreen;

        void Awake()
        {
            if (_panelTitle != null) _panelTitle.SetActive(true);
            if (_panelCanteen != null) _panelCanteen.SetActive(false);
            _phase = Phase.TitleScreen;
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
                    if (!string.IsNullOrWhiteSpace(_nextSceneName))
                        SceneManager.LoadScene(_nextSceneName.Trim());
                    break;
            }
        }
    }
}
