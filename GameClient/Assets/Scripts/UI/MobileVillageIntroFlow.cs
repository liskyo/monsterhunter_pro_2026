#pragma warning disable 0414 // 關閉欄位已指派但從未使用警告以保持 Prefab 序列化相容

using UnityEngine;

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
            HideLegacyPanelsForHub();
            if (GetComponent<VillageGameFlow>() == null)
                gameObject.AddComponent<VillageGameFlow>();

            _phase = Phase.TitleScreen;
        }

        void HideLegacyPanelsForHub()
        {
            if (_panelTitle != null) _panelTitle.SetActive(false);
            if (_panelCanteen != null) _panelCanteen.SetActive(false);
            if (_panelShop != null) _panelShop.SetActive(false);
        }

        /// <summary>舊版全螢幕點擊前進：改為進入村莊中樞。</summary>
        public void AdvanceFromTap()
        {
            GetComponent<VillageGameFlow>()?.ShowHub();
        }

        /// <summary>舊版商店「出發狩獵」相容：改為開啟「出戰整備」。</summary>
        public void ContinueFromShopToBattle()
        {
            GetComponent<VillageGameFlow>()?.OpenBattlePrep();
        }
    }
}
