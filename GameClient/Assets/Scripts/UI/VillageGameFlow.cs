using System;
using System.Collections.Generic;
using System.Linq;
using MonsterHunter.Combat;
using MonsterHunter.Data;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 村莊主流程：標題 → 村莊中樞 → 商店／任務板／加工屋／貓飯／倉庫／寵物小屋／出戰整備。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class VillageGameFlow : MonoBehaviour
    {
        const string PathTitle = "Assets/UI/Backgrounds/Village/遊戲標題_背景.png";
        const string PathHub = "Assets/UI/Backgrounds/Village/村莊.png";
        const string PathQuest = "Assets/UI/Backgrounds/Village/任務板_背景.png";
        const string PathCanteen = "Assets/UI/Backgrounds/Village/貓飯食堂_背景.png";
        const string PathWarehouse = "Assets/UI/Backgrounds/Village/獵人倉庫_背景.png";
        const string PathPet = "Assets/UI/Backgrounds/Village/寵物小屋.png";
        const string PathPrep = "Assets/UI/Backgrounds/Village/村莊.png";

        [SerializeField] string _battleSceneName = "Bootstrap";

        Font _font;

        GameObject _goTitle;
        GameObject _goHub;
        GameObject _goShop;
        GameObject _goQuest;
        GameObject _goWorkshop;
        GameObject _goCanteen;
        GameObject _goWarehouse;
        GameObject _goPet;
        GameObject _goBattlePrep;

        WorkshopEquipmentScreen _workshop;

        Text _prepStatus;
        string _prepPickPaint = "";
        string _prepPickTrace = "";

        void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft JhengHei", "Segoe UI", "Arial" }, 18);

            HideLegacyScenePanels();

            BuildTitle();
            BuildHub();
            BuildQuestBoard();
            BuildCanteen();
            BuildWarehouse();
            BuildPetHouse();
            BuildBattlePrep();
            EnsureWorkshopPanel();

            HideAllExcept(_goTitle);
        }

        void HideLegacyScenePanels()
        {
            foreach (Transform c in transform)
            {
                var n = c.name ?? "";
                if (n == "Panel_Title" || n == "Panel_Canteen" || n == "Panel_Shop")
                    c.gameObject.SetActive(false);
            }
        }

        public void ShowHub() => HideAllExcept(_goHub);

        public void OpenBattlePrep() => HideAllExcept(_goBattlePrep);

        void HideAllExcept(GameObject keep)
        {
            foreach (var g in new[]
                     {
                         _goTitle, _goHub, _goShop, _goQuest, _goWorkshop, _goCanteen, _goWarehouse, _goPet,
                         _goBattlePrep,
                     })
            {
                if (g == null) continue;
                g.SetActive(g == keep);
            }

            if (keep == _goBattlePrep)
                RefreshBattlePrepUi();
            if (keep == _goQuest)
                RefreshQuestUi();
            if (keep == _goCanteen)
                RefreshCanteenStatus();
            if (keep == _goPet)
                RefreshPetUi();
            if (keep == _goWarehouse)
                RefreshWarehouseUi();
            if (keep == _goWorkshop && _workshop != null)
                _workshop.Show();

            // ✦ 每次切換面板時，全域強制同步右上角的高清金幣顯示
            RefreshAllZennyDisplays();
        }

        void BuildTitle()
        {
            _goTitle = CreateFullScreenPanel("Flow_Title", PathTitle, out _);

            // ✦ 背景呼吸放大縮小效果 (增加臨場感與生命力)
            var bgT = _goTitle.transform.Find("Bg");
            if (bgT != null) bgT.gameObject.AddComponent<BgBreather>();

            var chrome = new GameObject("Chrome", typeof(RectTransform));
            chrome.transform.SetParent(_goTitle.transform, false);
            StretchFull(chrome.GetComponent<RectTransform>());

            // ✦ 字體極大化並套用 3x 超取樣高清外掛
            AddOutlinedTitle(chrome.transform, "MONSTER HUNTER", 86, new Vector2(0f, -220f));
            AddOutlinedTitle(chrome.transform, "試玩村莊", 52, new Color(1f, 0.88f, 0.38f), new Vector2(0f, -320f));

            // ✦ 改為「前往討伐」科技感大按鈕
            CreateMhPrimaryButton(_goTitle.transform, "前往討伐", new Vector2(0f, 320f), new Vector2(600f, 140f),
                () => HideAllExcept(_goHub), false, false, true, 46);
        }

        void BuildHub()
        {
            _goHub = CreateFullScreenPanel("Flow_Hub", PathHub, out var contentParent);
            AddHubHeader(contentParent.transform, "集會區域", ShowHub);

            // ✦ 移除佔據畫面的黑色舊式 Grid 區塊，改為極具科技感的透明懸浮 HUD 排版
            // 底部正中央 - 常用主功能 (任務板、出戰整備)
            var centerBottom = new GameObject("CenterBottom", typeof(RectTransform));
            centerBottom.transform.SetParent(contentParent.transform, false);
            var cbRt = centerBottom.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(0.1f, 0.04f);
            cbRt.anchorMax = new Vector2(0.9f, 0.28f);
            cbRt.offsetMin = cbRt.offsetMax = Vector2.zero;
            var cbGroup = centerBottom.AddComponent<VerticalLayoutGroup>();
            cbGroup.spacing = 16f;
            cbGroup.childAlignment = TextAnchor.LowerCenter;
            cbGroup.childForceExpandHeight = false;
            cbGroup.childForceExpandWidth = false;

            // 兩側容器 - 輔助選單功能
            var leftSide = new GameObject("LeftSide", typeof(RectTransform));
            leftSide.transform.SetParent(contentParent.transform, false);
            var lsRt = leftSide.GetComponent<RectTransform>();
            lsRt.anchorMin = new Vector2(0.04f, 0.32f);
            lsRt.anchorMax = new Vector2(0.35f, 0.78f);
            lsRt.offsetMin = lsRt.offsetMax = Vector2.zero;
            var lsGroup = leftSide.AddComponent<VerticalLayoutGroup>();
            lsGroup.spacing = 20f;
            lsGroup.childAlignment = TextAnchor.LowerCenter;
            lsGroup.childForceExpandHeight = false;
            lsGroup.childForceExpandWidth = false;
            
            var rightSide = new GameObject("RightSide", typeof(RectTransform));
            rightSide.transform.SetParent(contentParent.transform, false);
            var rsRt = rightSide.GetComponent<RectTransform>();
            rsRt.anchorMin = new Vector2(0.65f, 0.32f);
            rsRt.anchorMax = new Vector2(0.96f, 0.78f);
            rsRt.offsetMin = rsRt.offsetMax = Vector2.zero;
            var rsGroup = rightSide.AddComponent<VerticalLayoutGroup>();
            rsGroup.spacing = 20f;
            rsGroup.childAlignment = TextAnchor.LowerCenter;
            rsGroup.childForceExpandHeight = false;
            rsGroup.childForceExpandWidth = false;

            GameObject CreateSideBtn(Transform p, string label, Action act)
            {
                var btn = CreateMhPrimaryButton(p, label, Vector2.zero, new Vector2(180f, 96f), act, false, false, false, 32);
                var img = btn.GetComponent<Image>();
                img.color = new Color(0.06f, 0.08f, 0.12f, 0.85f); // 科技感透黑底色
                return btn;
            }

            // ✦ 擺放常用與不常用功能
            CreateSideBtn(leftSide.transform, "商店", () => { EnsureShopPanel(); HideAllExcept(_goShop); });
            CreateSideBtn(leftSide.transform, "貓飯食堂", () => HideAllExcept(_goCanteen));
            
            CreateSideBtn(rightSide.transform, "加工屋", () => { EnsureWorkshopPanel(); HideAllExcept(_goWorkshop); });
            CreateSideBtn(rightSide.transform, "獵人倉庫", () => HideAllExcept(_goWarehouse));
            CreateSideBtn(rightSide.transform, "寵物小屋", () => HideAllExcept(_goPet));

            // 正下方主按鈕 (現代科技感亮黃高光按鈕)
            CreateMhPrimaryButton(centerBottom.transform, "任務板", Vector2.zero, new Vector2(400f, 96f), () => HideAllExcept(_goQuest), false, false, false, 36);
            CreateMhPrimaryButton(centerBottom.transform, "出戰整備", Vector2.zero, new Vector2(500f, 116f), OpenBattlePrep, false, false, true, 46);
        }

        void EnsureShopPanel()
        {
            if (_goShop != null) return;
            var go = new GameObject("Flow_Shop", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            StretchFull(go.GetComponent<RectTransform>());
            var shop = go.AddComponent<VillageShopScreen>();
            shop.Initialize(ShowHub);
            go.SetActive(false);
            _goShop = go;
        }

        void EnsureWorkshopPanel()
        {
            if (_goWorkshop != null) return;
            var go = new GameObject("Flow_Workshop", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            StretchFull(go.GetComponent<RectTransform>());
            _workshop = go.AddComponent<WorkshopEquipmentScreen>();
            _workshop.Setup(transform, ShowHub);
            go.SetActive(false);
            _goWorkshop = go;
        }

        void BuildQuestBoard()
        {
            _goQuest = CreateFullScreenPanel("Flow_QuestBoard", PathQuest, out var body);
            AddHubHeader(body.transform, "任務板", ShowHub);

            var scroll = CreateScrollAreaMargins(body.transform, new Vector2(32f, 120f), new Vector2(-32f, -160f));
            var holder = scroll.content.gameObject.AddComponent<QuestBoardUiHolder>();
            holder.Content = scroll.content;
            holder.Flow = this;
            _goQuest.SetActive(false);
        }

        internal void RefreshQuestUi()
        {
            var h = _goQuest?.GetComponentInChildren<QuestBoardUiHolder>(true);
            if (h != null) h.Rebuild();
        }

        void BuildCanteen()
        {
            _goCanteen = CreateFullScreenPanel("Flow_Canteen", PathCanteen, out var body);
            AddHubHeader(body.transform, "貓飯食堂", ShowHub);

            var status = new GameObject("CanteenStatus", typeof(RectTransform));
            status.transform.SetParent(body.transform, false);
            var srt = status.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.08f, 0.82f);
            srt.anchorMax = new Vector2(0.92f, 0.9f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var st = status.AddComponent<Text>();
            st.font = _font;
            st.fontSize = 22;
            st.color = new Color(1f, 0.95f, 0.75f);
            st.alignment = TextAnchor.MiddleCenter;
            st.text = "";
            status.AddComponent<CanteenStatusLabel>().Label = st;

            var scroll = CreateScrollAreaMargins(body.transform, new Vector2(32f, 140f), new Vector2(-32f, -120f));
            scroll.content.gameObject.AddComponent<CanteenListHolder>().Content = scroll.content;
            _goCanteen.SetActive(false);
        }

        void RefreshCanteenStatus()
        {
            var ledger = LocalHunterLedger.LoadOrCreate();
            var label = _goCanteen?.GetComponentInChildren<CanteenStatusLabel>(true);
            if (label?.Label == null) return;
            var foodId = (ledger.PreviewCanteenFoodId ?? "").Trim();
            if (string.IsNullOrEmpty(foodId))
            {
                label.Label.text = "尚未選購飯糰。購買後將於下一場戰鬥生效（一場僅能套用一種）。";
                return;
            }

            var row = LookupCanteenRow(foodId);
            label.Label.text = row != null
                ? $"已預定貓飯：{row.名稱}（下一場討伐生效）"
                : $"已預定貓飯編號：{foodId}";
        }

        void BuildWarehouse()
        {
            _goWarehouse = CreateFullScreenPanel("Flow_Warehouse", PathWarehouse, out var body);
            AddHubHeader(body.transform, "獵人倉庫", ShowHub);
            var scroll = CreateScrollAreaMargins(body.transform, new Vector2(28f, 120f), new Vector2(-28f, -100f));
            scroll.content.gameObject.AddComponent<WarehouseListHolder>().Content = scroll.content;
            _goWarehouse.SetActive(false);
        }

        void RefreshWarehouseUi() => WarehouseListHolder.RefreshHolder(_goWarehouse);

        void BuildPetHouse()
        {
            _goPet = CreateFullScreenPanel("Flow_PetHouse", PathPet, out var body);
            AddHubHeader(body.transform, "寵物小屋", ShowHub);

            var tip = new GameObject("Tip", typeof(RectTransform));
            tip.transform.SetParent(body.transform, false);
            var trt = tip.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.08f, 0.8f);
            trt.anchorMax = new Vector2(0.92f, 0.88f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tt = tip.AddComponent<Text>();
            tt.font = _font;
            tt.fontSize = 22;
            tt.color = new Color(0.92f, 0.94f, 1f);
            tt.alignment = TextAnchor.MiddleCenter;
            tt.text = "選擇一隻已擁有的隨行寵物（每次討伐僅能帶一隻）；未選則戰鬥中隨機一隻已擁有寵物。";

            var scroll = CreateScrollAreaMargins(body.transform, new Vector2(32f, 130f), new Vector2(-32f, -100f));
            scroll.content.gameObject.AddComponent<PetListHolder>().Content = scroll.content;
            _goPet.SetActive(false);
        }

        void RefreshPetUi() => PetListHolder.RefreshHolder(_goPet);

        void BuildBattlePrep()
        {
            _goBattlePrep = CreateFullScreenPanel("Flow_BattlePrep", PathPrep, out var body);
            AddHubHeader(body.transform, "出戰整備", ShowHub);

            var ledger = LocalHunterLedger.LoadOrCreate();
            _prepPickPaint = (ledger.PreviewPaintballItemId ?? "").Trim();
            _prepPickTrace = (ledger.PreviewTraceId ?? "").Trim();

            // ✦ 玻璃擬態科技感任務目標卡片背景 (Quest Card)
            var info = new GameObject("PrepInfo", typeof(RectTransform), typeof(Image));
            info.transform.SetParent(body.transform, false);
            var irt = info.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.08f, 0.69f);
            irt.anchorMax = new Vector2(0.92f, 0.91f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            
            var cardImg = info.GetComponent<Image>();
            cardImg.color = new Color(0.1f, 0.12f, 0.16f, 0.9f); // 透黑鋼底色
            
            var cardOutl = info.AddComponent<Outline>();
            cardOutl.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.45f); // 黃金外框邊界
            cardOutl.effectDistance = new Vector2(1.5f, -1.5f);
            
            var cardShad = info.AddComponent<Shadow>();
            cardShad.effectColor = new Color(0f, 0f, 0f, 0.6f);
            cardShad.effectDistance = new Vector2(3f, -3f);

            var hgCard = info.AddComponent<HorizontalLayoutGroup>();
            hgCard.padding = new RectOffset(24, 24, 12, 12);
            hgCard.spacing = 24f;
            hgCard.childAlignment = TextAnchor.MiddleLeft;
            hgCard.childControlWidth = true;
            hgCard.childControlHeight = true;
            hgCard.childForceExpandWidth = false;
            hgCard.childForceExpandHeight = false;

            // ✦ 左側魔物高清圓形圖示卡
            var questIconGo = new GameObject("QuestMonsterIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            questIconGo.transform.SetParent(info.transform, false);
            var qRt = questIconGo.GetComponent<RectTransform>();
            qRt.sizeDelta = new Vector2(100f, 100f);
            var qLe = questIconGo.GetComponent<LayoutElement>();
            qLe.preferredWidth = 100f;
            qLe.preferredHeight = 100f;
            var qImg = questIconGo.GetComponent<Image>();
            qImg.color = Color.white;
            qImg.preserveAspect = true;
            
            var qiOutl = questIconGo.AddComponent<Outline>();
            qiOutl.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.5f);
            qiOutl.effectDistance = new Vector2(1.2f, -1.2f);

            // ✦ 右側 Rich-Text 文字容器
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
            textGo.transform.SetParent(info.transform, false);
            textGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _prepStatus = textGo.AddComponent<Text>();
            _prepStatus.font = _font;
            _prepStatus.fontSize = 22;
            _prepStatus.color = new Color(0.96f, 0.97f, 1f);
            _prepStatus.alignment = TextAnchor.MiddleLeft;
            _prepStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            _prepStatus.verticalOverflow = VerticalWrapMode.Overflow;

            var textShad = textGo.AddComponent<Shadow>();
            textShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textShad.effectDistance = new Vector2(1.2f, -1.2f);

            CreateSectionLabel(body.transform, "持有染色球（點選）", new Vector2(0.04f, 0.58f), new Vector2(0.48f, 0.63f));
            var scrollPaint = CreateScrollAreaAnchored(body.transform, new Vector2(0.04f, 0.28f), new Vector2(0.48f, 0.56f));

            CreateSectionLabel(body.transform, "持有魔物痕跡（點選）", new Vector2(0.52f, 0.58f), new Vector2(0.96f, 0.63f));
            var scrollTrace = CreateScrollAreaAnchored(body.transform, new Vector2(0.52f, 0.28f), new Vector2(0.96f, 0.56f));

            scrollPaint.content.gameObject.AddComponent<PaintPickHolder>().Holder = this;
            scrollTrace.content.gameObject.AddComponent<TracePickHolder>().Holder = this;

            // ✦ 攜帶道具選取區 Y: 0.12 ~ 0.26
            var itemSection = new GameObject("PrepItemsSection", typeof(RectTransform), typeof(Image));
            itemSection.transform.SetParent(body.transform, false);
            var isRt = itemSection.GetComponent<RectTransform>();
            isRt.anchorMin = new Vector2(0.04f, 0.12f);
            isRt.anchorMax = new Vector2(0.96f, 0.26f);
            isRt.offsetMin = isRt.offsetMax = Vector2.zero;
            itemSection.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.9f);
            
            var isOutl = itemSection.AddComponent<Outline>();
            isOutl.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.45f);
            isOutl.effectDistance = new Vector2(1.2f, -1.2f);

            var itemLabelGo = new GameObject("Label", typeof(RectTransform));
            itemLabelGo.transform.SetParent(itemSection.transform, false);
            var ilRt = itemLabelGo.GetComponent<RectTransform>();
            ilRt.anchorMin = new Vector2(0f, 0.72f);
            ilRt.anchorMax = new Vector2(1f, 1f);
            ilRt.offsetMin = ilRt.offsetMax = Vector2.zero;
            var ilTxt = itemLabelGo.AddComponent<Text>();
            ilTxt.font = _font;
            ilTxt.fontSize = 16;
            ilTxt.fontStyle = FontStyle.Bold;
            ilTxt.alignment = TextAnchor.MiddleCenter;
            ilTxt.color = new Color(0.85f, 0.65f, 0.2f);
            ilTxt.text = "✦ 出戰攜帶道具 (最多3項，點擊選格) ✦";

            var gridGo = new GameObject("ItemGrid", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            gridGo.transform.SetParent(itemSection.transform, false);
            var gRt = gridGo.GetComponent<RectTransform>();
            gRt.anchorMin = new Vector2(0.05f, 0.05f);
            gRt.anchorMax = new Vector2(0.95f, 0.7f);
            gRt.offsetMin = gRt.offsetMax = Vector2.zero;
            
            var hgGrid = gridGo.GetComponent<HorizontalLayoutGroup>();
            hgGrid.spacing = 20f;
            hgGrid.childAlignment = TextAnchor.MiddleCenter;
            hgGrid.childForceExpandWidth = true;
            hgGrid.childForceExpandHeight = true;
            
            gridGo.AddComponent<BattleItemPickHolder>().Holder = this;

            var foot = new GameObject("PrepFoot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            foot.transform.SetParent(body.transform, false);
            var fr = foot.GetComponent<RectTransform>();
            fr.anchorMin = new Vector2(0.15f, 0.02f);
            fr.anchorMax = new Vector2(0.85f, 0.1f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            var hg = foot.GetComponent<HorizontalLayoutGroup>();
            hg.spacing = 20f;
            hg.childAlignment = TextAnchor.MiddleCenter;
            hg.childForceExpandHeight = true;
            hg.childForceExpandWidth = true;

            // ✦ 刪除過時的「套用選擇」，僅提供最霸氣的「出發討伐」按鈕
            CreateMhPrimaryButton(foot.transform, "整備完成・出發討伐", Vector2.zero, new Vector2(500f, 76f), () =>
            {
                var led = LocalHunterLedger.LoadOrCreate();
                if (!VillageBattlePrepRules.TryValidate(led, out var err))
                {
                    RefreshBattlePrepUi(); // 強制刷新顯示阻擋資訊
                    return;
                }

                var q = VillageBattlePrepRules.FindQuestRow(led.ActiveQuestId.Trim());
                HuntSessionContext.PendingQuest = q;
                SceneManager.LoadScene(string.IsNullOrWhiteSpace(_battleSceneName) ? "Bootstrap" : _battleSceneName.Trim());
            }, fillLayout: true, overrideFontSize: 32);

            _goBattlePrep.SetActive(false);
        }

        internal void RefreshBattlePrepUi()
        {
            if (_prepStatus == null) return;
            var ledger = LocalHunterLedger.LoadOrCreate();
            _prepPickPaint = (ledger.PreviewPaintballItemId ?? "").Trim();
            _prepPickTrace = (ledger.PreviewTraceId ?? "").Trim();
            
            var q = VillageBattlePrepRules.FindQuestRow(ledger.ActiveQuestId);
            
            // ✦ 1. 動態更新頂部魔物大頭貼
            var qImg = _prepStatus.transform.parent.Find("QuestMonsterIcon")?.GetComponent<Image>();
            if (qImg != null)
            {
                if (q != null && q.目標魔物 != null && q.目標魔物.Length > 0 && q.目標魔物[0] != null)
                {
                    var mid = (q.目標魔物[0].魔物編號 ?? "").Trim();
                    var sp = SafeSpriteLoader.TryLoadSprite("Textures/Monsters/" + mid);
                    qImg.sprite = sp ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
                    qImg.gameObject.SetActive(true);
                }
                else
                {
                    qImg.gameObject.SetActive(false);
                }
            }

            // ✦ 2. 高階反應式 Rich-Text 文字更新
            var targetMonsterName = "";
            var questStar = 1;
            if (q != null)
            {
                questStar = q.星級;
                if (q.目標魔物 != null && q.目標魔物.Length > 0 && q.目標魔物[0] != null)
                    targetMonsterName = q.目標魔物[0].魔物名稱 ?? q.目標魔物[0].魔物編號;
            }

            var pbName = "未選擇";
            if (!string.IsNullOrEmpty(_prepPickPaint))
            {
                var rows = PaintPickHolder.LoadPaintRows();
                var match = rows.FirstOrDefault(r => r != null && r.道具編號 == _prepPickPaint);
                if (match != null) pbName = $"{match.名稱} ({match.吸引星級_最低}~{match.吸引星級_最高}★)";
            }

            var trName = "未選擇";
            if (!string.IsNullOrEmpty(_prepPickTrace))
            {
                var rows = TracePickHolder.LoadTraceRows();
                var match = rows.FirstOrDefault(r => r != null && r.痕跡編號 == _prepPickTrace);
                if (match != null) trName = $"{match.名稱} (★{match.魔物星級})";
            }

            var qName = q != null ? q.標題 : "尚未承接任務";
            var validationError = "";
            var isValid = VillageBattlePrepRules.TryValidate(ledger, out validationError);

            var statusHint = isValid
                ? "<color=#27AE60><b>✔ 討伐準備就緒！點擊下方按鈕出發</b></color>"
                : $"<color=#EB5757><b>⚠ 整備條件未滿足：{validationError}</b></color>";

            var questText = q != null
                ? $"<color=#F2C94C>★{questStar} {qName}</color>"
                : "<color=#56CCF2>無（自由討伐模式，將消耗物品）</color>";

            _prepStatus.text = $"<b>進行中任務：</b>{questText}\n" +
                               $"<b>已選染色球：</b><color=#56CCF2>{pbName}</color>　｜　" +
                               $"<b>已選魔物痕跡：</b><color=#BB6BD9>{trName}</color>\n" +
                               $"{statusHint}";

            PaintPickHolder.Refresh(_goBattlePrep);
            TracePickHolder.Refresh(_goBattlePrep);
            BattleItemPickHolder.Refresh(_goBattlePrep);
        }

        internal void SetPrepPaint(string id)
        {
            _prepPickPaint = id ?? "";
            var led = LocalHunterLedger.LoadOrCreate();
            led.PreviewPaintballItemId = _prepPickPaint;
            led.Save();
            RefreshBattlePrepUi();
        }

        internal void SetPrepTrace(string id)
        {
            _prepPickTrace = id ?? "";
            var led = LocalHunterLedger.LoadOrCreate();
            led.PreviewTraceId = _prepPickTrace;
            led.Save();
            RefreshBattlePrepUi();
        }

        internal string GetPrepPickPaint() => _prepPickPaint;

        internal string GetPrepPickTrace() => _prepPickTrace;

        GameObject CreateFullScreenPanel(string name, string bgPath, out RectTransform contentArea)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            var img = go.GetComponent<Image>();
            img.sprite = SafeSpriteLoader.TryLoadSprite(bgPath);
            img.color = Color.white;
            img.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            var crt = content.GetComponent<RectTransform>();
            StretchFull(crt);
            contentArea = crt;
            return go;
        }

        void AddHubHeader(Transform parent, string title, Action onBack)
        {
            var bar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var br = bar.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 1f);
            br.anchorMax = new Vector2(1f, 1f);
            br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(0f, -110f);
            br.offsetMax = new Vector2(0f, 0f);
            bar.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.95f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(bar.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(800f, 64f);
            var tt = titleGo.AddComponent<Text>();
            SetSharpText(tt, title, 48, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);

            CreateMhSecondaryButton(bar.transform, "← 村莊", () => onBack?.Invoke(),
                anchored: new Vector2(-430f, 0f), size: new Vector2(200f, 56f));

            var zennyGo = new GameObject("ZennyText", typeof(RectTransform));
            zennyGo.transform.SetParent(bar.transform, false);
            var zrt = zennyGo.GetComponent<RectTransform>();
            zrt.anchorMin = zrt.anchorMax = new Vector2(1f, 0.5f);
            zrt.pivot = new Vector2(1f, 0.5f);
            zrt.anchoredPosition = new Vector2(-28f, 0f);
            zrt.sizeDelta = new Vector2(300f, 64f);
            var zt = zennyGo.AddComponent<Text>();
            
            var ledger = LocalHunterLedger.LoadOrCreate();
            SetSharpText(zt, $"金幣: <color=#F2C94C>{ledger.Zenny} z</color>", 32, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleRight, FontStyle.Bold);
            
            var textShad = zennyGo.AddComponent<Shadow>();
            textShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textShad.effectDistance = new Vector2(1.2f, -1.2f);
        }

        public void RefreshAllZennyDisplays()
        {
            var ledger = LocalHunterLedger.LoadOrCreate();
            var zTexts = FindObjectsByType<Text>();
            foreach (var zt in zTexts)
            {
                if (zt != null && zt.gameObject.name == "ZennyText")
                {
                    zt.text = $"金幣: <color=#F2C94C>{ledger.Zenny} z</color>";
                }
            }
        }

        void CreateSectionLabel(Transform parent, string msg, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Sec", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            SetSharpText(t, msg, 24, new Color(1f, 0.96f, 0.82f), TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        (RectTransform viewport, RectTransform content) CreateScrollAreaMargins(Transform parent, Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            StretchFull(srt);
            srt.offsetMin = offsetMin;
            srt.offsetMax = offsetMax;
            scrollGo.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.1f, 0.72f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            StretchFull(vpRt);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var cRt = content.GetComponent<RectTransform>();
            StretchFull(cRt);
            var v = content.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childForceExpandHeight = false;
            v.spacing = 8f;
            var fit = content.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = vpRt;
            sr.content = cRt;
            sr.horizontal = false;
            sr.vertical = true;
            return (vpRt, cRt);
        }

        (RectTransform viewport, RectTransform content) CreateScrollAreaAnchored(Transform parent, Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = anchorMin;
            srt.anchorMax = anchorMax;
            srt.offsetMin = new Vector2(8f, 8f);
            srt.offsetMax = new Vector2(-8f, -8f);
            scrollGo.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.1f, 0.72f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            StretchFull(vpRt);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var cRt = content.GetComponent<RectTransform>();
            StretchFull(cRt);
            var v = content.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childForceExpandHeight = false;
            v.spacing = 8f;
            var fit = content.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = vpRt;
            sr.content = cRt;
            sr.horizontal = false;
            sr.vertical = true;
            return (vpRt, cRt);
        }

        void AddOutlinedTitle(Transform parent, string line, int size, Vector2 yPos) =>
            AddOutlinedTitle(parent, line, size, Color.white, yPos);

        void AddOutlinedTitle(Transform parent, string line, int size, Color fill, Vector2 yPos)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = yPos;
            rt.sizeDelta = new Vector2(1000f, size + 24f);
            var txt = go.AddComponent<Text>();
            SetSharpText(txt, line, size, fill, TextAnchor.MiddleCenter, FontStyle.Bold);
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(4f, -4f);
        }

        GameObject CreateMhPrimaryButton(Transform parent, string label, Vector2 anchored, Vector2 size, Action onClick,
            bool fillCell = false, bool fillLayout = false, bool isHighlight = false, int overrideFontSize = 0)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (!fillCell && !fillLayout)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = anchored;
                rt.sizeDelta = size;
            }
            else
                StretchFull(rt);

            var le = go.GetComponent<LayoutElement>();
            if (fillLayout) le.flexibleWidth = 1f;
            if (fillCell) le.minHeight = 96f;
            
            if (!fillCell && !fillLayout)
            {
                le.preferredWidth = size.x;
                le.preferredHeight = size.y;
            }

            var img = go.GetComponent<Image>();
            if (isHighlight)
            {
                img.color = new Color(0.95f, 0.79f, 0.18f, 1f); // MHN 招牌亮黃色
                var outl = go.AddComponent<Outline>();
                outl.effectColor = new Color(1f, 0.88f, 0.35f, 0.8f);
                outl.effectDistance = new Vector2(1f, -1f);
            }
            else
            {
                img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f); // 現代極簡深灰卡片底色
                var outl = go.AddComponent<Outline>();
                outl.effectColor = new Color(1f, 1f, 1f, 0.15f); // 細緻白銀外框
                outl.effectDistance = new Vector2(1.2f, -1.2f);
            }
            
            var shad = go.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;

            var tg = new GameObject("T", typeof(RectTransform));
            tg.transform.SetParent(go.transform, false);
            StretchFull(tg.GetComponent<RectTransform>());
            
            var t = tg.AddComponent<Text>();
            int finalSize = overrideFontSize > 0 ? overrideFontSize : (fillCell ? 28 : 34);
            SetSharpText(t, label, finalSize, isHighlight ? new Color(0.06f, 0.07f, 0.1f) : new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            
            if (!isHighlight)
            {
                var textShad = tg.AddComponent<Shadow>();
                textShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
                textShad.effectDistance = new Vector2(1.2f, -1.2f);
            }

            return go;
        }

        GameObject CreateMhSecondaryButton(Transform parent, string label, Action onClick, Vector2 anchored,
            Vector2 size, bool flexible = false)
        {
            var go = new GameObject("Btn2_" + label, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchored;
            if (flexible) go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            
            var outl = go.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outl.effectDistance = new Vector2(1.2f, -1.2f);
            
            var shad = go.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = colors;

            var tg = new GameObject("T", typeof(RectTransform));
            tg.transform.SetParent(go.transform, false);
            StretchFull(tg.GetComponent<RectTransform>());
            
            var t = tg.AddComponent<Text>();
            SetSharpText(t, label, 28, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            
            var textShad = tg.AddComponent<Shadow>();
            textShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textShad.effectDistance = new Vector2(1.2f, -1.2f);

            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static 貓飯資料列 LookupCanteenRow(string id)
        {
            if (string.IsNullOrEmpty(id) ||
                !DesignDataReader.TryLoadDesignDataText(out var json, "05_Systems", "canteen.json"))
                return null;
            try
            {
                var rows = JsonConvert.DeserializeObject<貓飯資料列[]>(json);
                return rows?.FirstOrDefault(r => r != null && r.料理編號 == id);
            }
            catch
            {
                return null;
            }
        }

        sealed class CanteenStatusLabel : MonoBehaviour
        {
            public Text Label;
        }

        sealed class CanteenListHolder : MonoBehaviour
        {
            public RectTransform Content;

            void OnEnable() => Rebuild();

            void Rebuild()
            {
                if (Content == null) return;
                for (var i = Content.childCount - 1; i >= 0; i--)
                    Destroy(Content.GetChild(i).gameObject);

                if (!DesignDataReader.TryLoadDesignDataText(out var json, "05_Systems", "canteen.json"))
                    return;
                貓飯資料列[] rows;
                try
                {
                    rows = JsonConvert.DeserializeObject<貓飯資料列[]>(json) ?? Array.Empty<貓飯資料列>();
                }
                catch
                {
                    return;
                }

                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                foreach (var row in rows)
                {
                    if (row == null) continue;
                    CreateMealRow(Content, font, row);
                }
            }

            static void CreateMealRow(RectTransform list, Font font, 貓飯資料列 row)
            {
                var go = new GameObject(row.料理編號, typeof(RectTransform), typeof(Image),
                    typeof(HorizontalLayoutGroup));
                go.transform.SetParent(list, false);
                
                // ✦ 貓飯卡片背景：黑鋼底色、細緻白銀外框與立體投影
                var img = go.GetComponent<Image>();
                img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
                
                var outl = go.AddComponent<Outline>();
                outl.effectColor = new Color(1f, 1f, 1f, 0.15f);
                outl.effectDistance = new Vector2(1.2f, -1.2f);
                
                var shad = go.AddComponent<Shadow>();
                shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
                shad.effectDistance = new Vector2(2f, -2f);

                var h = go.GetComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(16, 16, 10, 10);
                h.spacing = 16f;
                h.childAlignment = TextAnchor.MiddleLeft;
                
                // ✦ 核心排版控制：依據子物件 Layout 屬性計算，且不強行拉伸
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;

                go.AddComponent<LayoutElement>().minHeight = 96f;

                // ✦ 1. 貓飯圖片 (解決 icon 沒出現問題)
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(go.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.sizeDelta = new Vector2(68f, 68f);
                
                var iconLe = iconGo.GetComponent<LayoutElement>();
                iconLe.preferredWidth = 68f;
                iconLe.preferredHeight = 68f;
                
                var iconImg = iconGo.GetComponent<Image>();
                var path = string.IsNullOrWhiteSpace(row.圖片路徑) ? $"Assets/Textures/Canteen/{row.料理編號}.png" : row.圖片路徑.Trim();
                var sp = SafeSpriteLoader.TryLoadSprite(path);
                iconImg.sprite = sp ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
                iconImg.color = Color.white;
                iconImg.preserveAspect = true; // 鎖定比例，防止圖片壓扁

                var iconOutl = iconGo.AddComponent<Outline>();
                iconOutl.effectColor = new Color(0.85f, 0.65f, 0.3f, 0.4f);
                iconOutl.effectDistance = new Vector2(1f, -1f);

                // ✦ 2. 貓飯料理名稱與說明（免費享用，下場生效）
                var tgo = new GameObject("Tx", typeof(RectTransform));
                tgo.transform.SetParent(go.transform, false);
                tgo.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var nt = tgo.AddComponent<Text>();
                SetSharpText(nt, $"{row.名稱}　　<color=#27AE60>【免費享用，下場生效】</color>", 24, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
                
                var textShad = tgo.AddComponent<Shadow>();
                textShad.effectColor = new Color(0f, 0f, 0f, 0.75f);
                textShad.effectDistance = new Vector2(1.2f, -1.2f);

                // ✦ 3. 享用按鈕 (免費享用，綠色奢華設計)
                var btnGo = new GameObject("Buy", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                btnGo.transform.SetParent(go.transform, false);
                
                var btnLe = btnGo.GetComponent<LayoutElement>();
                btnLe.preferredWidth = 160f; // 強制固定按鈕寬度
                btnLe.preferredHeight = 56f;
                btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 56f);
                
                var btnImg = btnGo.GetComponent<Image>();
                btnImg.color = new Color(0.15f, 0.68f, 0.37f, 0.95f); // 奢華翠綠享用底色
                
                var btnOutl = btnGo.AddComponent<Outline>();
                btnOutl.effectColor = new Color(0.46f, 0.84f, 0.57f, 0.8f); // 亮綠外框
                btnOutl.effectDistance = new Vector2(1.5f, -1.5f);
                
                var btnShad = btnGo.AddComponent<Shadow>();
                btnShad.effectColor = new Color(0f, 0f, 0f, 0.5f);
                btnShad.effectDistance = new Vector2(2f, -2f);

                var btn = btnGo.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (!TryBuyMeal(row, out var err))
                    {
                        Debug.LogWarning(err);
                        return;
                    }

                    var flow = FindAnyObjectByType<VillageGameFlow>();
                    if (flow != null)
                    {
                        flow.RefreshCanteenStatus();
                        flow.RefreshAllZennyDisplays(); // 即時刷新金幣顯示！
                    }
                });
                var bt = new GameObject("L", typeof(RectTransform));
                bt.transform.SetParent(btnGo.transform, false);
                StretchFull(bt.GetComponent<RectTransform>());
                var btx = bt.AddComponent<Text>();
                SetSharpText(btx, "享用", 24, new Color(1f, 1f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            static bool TryBuyMeal(貓飯資料列 row, out string err)
            {
                err = null;
                var ledger = LocalHunterLedger.LoadOrCreate();
                if (row == null)
                {
                    err = "資料錯誤";
                    return false;
                }

                ledger.PreviewCanteenFoodId = row.料理編號 ?? "";
                ledger.Save();
                return true;
            }
        }

        sealed class WarehouseListHolder : MonoBehaviour
        {
            public RectTransform Content;

            public static void RefreshHolder(GameObject root)
            {
                if (root == null) return;
                var h = root.GetComponentInChildren<WarehouseListHolder>(true);
                h?.Rebuild();
            }

            void OnEnable() => Rebuild();

            void Rebuild()
            {
                if (Content == null) return;
                for (var i = Content.childCount - 1; i >= 0; i--)
                    Destroy(Content.GetChild(i).gameObject);

                var ledger = LocalHunterLedger.LoadOrCreate();
                if (ledger.Warehouse == null) return;

                var names = ResolveNameLookup();
                var keys = ledger.Warehouse.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(s => s).ToList();
                foreach (var id in keys)
                {
                    var qty = ledger.Warehouse[id];
                    var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
                    go.transform.SetParent(Content, false);
                    
                    var img = go.GetComponent<Image>();
                    img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f); // MHN 深灰底
                    
                    var outl = go.AddComponent<Outline>();
                    outl.effectColor = new Color(1f, 1f, 1f, 0.15f); // 白銀外框
                    outl.effectDistance = new Vector2(1.2f, -1.2f);
                    
                    var shad = go.AddComponent<Shadow>();
                    shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
                    shad.effectDistance = new Vector2(2f, -2f);

                    var hg = go.GetComponent<HorizontalLayoutGroup>();
                    hg.padding = new RectOffset(16, 16, 0, 0);

                    go.AddComponent<LayoutElement>().minHeight = 64f;
                    
                    var txt = new GameObject("T", typeof(RectTransform));
                    txt.transform.SetParent(go.transform, false);
                    StretchFull(txt.GetComponent<RectTransform>());
                    var t = txt.AddComponent<Text>();
                    SetSharpText(t, $"{(names.TryGetValue(id, out var nm) ? nm : id)}", 24, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
                    
                    var textShad = txt.AddComponent<Shadow>();
                    textShad.effectColor = new Color(0f, 0f, 0f, 0.75f);
                    textShad.effectDistance = new Vector2(1.2f, -1.2f);
 
                    var countTxt = new GameObject("C", typeof(RectTransform));
                    countTxt.transform.SetParent(go.transform, false);
                    StretchFull(countTxt.GetComponent<RectTransform>());
                    var ct = countTxt.AddComponent<Text>();
                    SetSharpText(ct, $"× {qty}", 24, new Color(0.95f, 0.79f, 0.18f), TextAnchor.MiddleRight, FontStyle.Bold);
                    
                    var ctShad = countTxt.AddComponent<Shadow>();
                    ctShad.effectColor = new Color(0f, 0f, 0f, 0.75f);
                    ctShad.effectDistance = new Vector2(1.2f, -1.2f);
                }
            }

            static Dictionary<string, string> ResolveNameLookup()
            {
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                if (DesignDataReader.TryLoadDesignDataText(out var mj, "04_Items", "materials.json"))
                    foreach (var r in DesignDataJsonArrayUtility.Parse素材資料(mj) ?? Array.Empty<素材資料列>())
                        if (r != null && !string.IsNullOrEmpty(r.素材編號))
                            d[r.素材編號] = r.名稱;

                TryAddJsonArray(d, "04_Items", "paintballs.json",
                    (染色球資料列 r) => r.道具編號, r => r.名稱);
                TryAddJsonArray(d, "04_Items", "monster_traces.json",
                    (魔物痕跡資料列 r) => r.痕跡編號, r => r.名稱);
                foreach (var p in OwnedPetBattleBuffs.LoadAllPets())
                    if (p != null && !string.IsNullOrEmpty(p.寵物編號))
                        d[p.寵物編號] = p.名稱;
                return d;
            }

            static void TryAddJsonArray<T>(Dictionary<string, string> d, string folder, string file,
                Func<T, string> idSel, Func<T, string> nameSel)
            {
                if (!DesignDataReader.TryLoadDesignDataText(out var json, folder, file)) return;
                try
                {
                    var rows = JsonConvert.DeserializeObject<T[]>(json);
                    if (rows == null) return;
                    foreach (var r in rows)
                    {
                        if (r == null) continue;
                        var i = idSel(r);
                        var n = nameSel(r);
                        if (!string.IsNullOrEmpty(i))
                            d[i] = n ?? i;
                    }
                }
                catch
                {
                    /* ignore */
                }
            }
        }

        sealed class PetListHolder : MonoBehaviour
        {
            public RectTransform Content;

            public static void RefreshHolder(GameObject root)
            {
                if (root == null) return;
                root.GetComponentInChildren<PetListHolder>(true)?.Rebuild();
            }

            void OnEnable() => Rebuild();

            public void Rebuild()
            {
                if (Content == null) return;
                for (var i = Content.childCount - 1; i >= 0; i--)
                    Destroy(Content.GetChild(i).gameObject);

                var ledger = LocalHunterLedger.LoadOrCreate();
                var pets = OwnedPetBattleBuffs.LoadAllPets();
                var chosen = (ledger.SelectedBattlePetId ?? "").Trim();

                // 1. 不指定（戰鬥隨機）列
                var rowNone = new GameObject("Pet_None", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                rowNone.transform.SetParent(Content, false);
                rowNone.GetComponent<LayoutElement>().minHeight = 90f;
                
                var imgNone = rowNone.GetComponent<Image>();
                imgNone.color = string.IsNullOrEmpty(chosen)
                    ? new Color(0.08f, 0.28f, 0.52f, 0.95f) // selected blue
                    : new Color(0.12f, 0.13f, 0.16f, 0.95f); // normal dark
                
                var hgNone = rowNone.GetComponent<HorizontalLayoutGroup>();
                hgNone.padding = new RectOffset(16, 16, 8, 8);
                hgNone.spacing = 16f;
                hgNone.childAlignment = TextAnchor.MiddleLeft;
                hgNone.childControlWidth = true;
                hgNone.childControlHeight = true;
                hgNone.childForceExpandWidth = false;
                hgNone.childForceExpandHeight = false;

                var iconNone = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconNone.transform.SetParent(rowNone.transform, false);
                iconNone.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 64f);
                var imgIco = iconNone.GetComponent<Image>();
                imgIco.sprite = PlaceholderSpriteFactory.GetSharedPlaceholder();
                imgIco.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

                AddRowLabel(rowNone.transform, "（不指定，戰鬥隨機）", 24, new Color(0.96f, 0.97f, 1f));

                var isNoneSelected = string.IsNullOrEmpty(chosen);
                CreatePetActionButton(rowNone.transform, isNoneSelected ? "已選定" : "選擇隨機", !isNoneSelected, () =>
                {
                    ledger.SelectedBattlePetId = "";
                    ledger.Save();
                    Rebuild();
                });

                // 2. 擁有之隨行寵物列
                foreach (var p in pets)
                {
                    if (p == null) continue;
                    if (ledger.GetWarehouseQuantity(p.寵物編號) <= 0) continue;
                    var pick = p.寵物編號;
                    
                    var row = new GameObject(pick, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                    row.transform.SetParent(Content, false);
                    row.GetComponent<LayoutElement>().minHeight = 90f;
                    
                    var img = row.GetComponent<Image>();
                    var isSelected = chosen == pick;
                    img.color = isSelected
                        ? new Color(0.08f, 0.28f, 0.52f, 0.95f) // selected blue
                        : new Color(0.12f, 0.13f, 0.16f, 0.95f); // normal dark
                    
                    var hg = row.GetComponent<HorizontalLayoutGroup>();
                    hg.padding = new RectOffset(16, 16, 8, 8);
                    hg.spacing = 16f;
                    hg.childAlignment = TextAnchor.MiddleLeft;
                    hg.childControlWidth = true;
                    hg.childControlHeight = true;
                    hg.childForceExpandWidth = false;
                    hg.childForceExpandHeight = false;

                    // 加載寵物圖示
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(row.transform, false);
                    iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 64f);
                    var iconImg = iconGo.GetComponent<Image>();
                    
                    var path = !string.IsNullOrWhiteSpace(p.圖片路徑) 
                        ? p.圖片路徑.Trim() 
                        : $"Assets/Textures/Pets/{p.寵物編號}.png";
                    
                    var sp = SafeSpriteLoader.TryLoadSprite(path);
                    iconImg.sprite = sp ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
                    iconImg.color = Color.white;
                    iconImg.preserveAspect = true;

                    AddRowLabel(row.transform, $"{p.名稱}（{pick}）", 24, new Color(0.96f, 0.97f, 1f));

                    CreatePetActionButton(row.transform, isSelected ? "休息" : "出戰", true, () =>
                    {
                        if (isSelected)
                        {
                            ledger.SelectedBattlePetId = "";
                        }
                        else
                        {
                            ledger.SelectedBattlePetId = pick;
                        }
                        ledger.Save();
                        Rebuild();
                    });
                }
            }

            static Text AddRowLabel(Transform parent, string msg, int size, Color c)
            {
                var go = new GameObject("Lbl", typeof(RectTransform), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                go.GetComponent<LayoutElement>().flexibleWidth = 1f;
                var t = go.AddComponent<Text>();
                SetSharpText(t, msg, size, c, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.verticalOverflow = VerticalWrapMode.Overflow;
                return t;
            }

            static void CreatePetActionButton(Transform parent, string label, bool active, Action onClick)
            {
                var go = new GameObject("ActionBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(160f, 54f);
                
                var le = go.GetComponent<LayoutElement>();
                le.preferredWidth = 160f;
                le.preferredHeight = 54f;
                
                var img = go.GetComponent<Image>();
                if (active)
                {
                    if (label == "休息")
                    {
                        img.color = new Color(0.75f, 0.22f, 0.17f, 0.95f); // red
                    }
                    else
                    {
                        img.color = new Color(0.18f, 0.54f, 0.34f, 0.95f); // green
                    }
                }
                else
                {
                    img.color = new Color(0.25f, 0.28f, 0.32f, 0.8f); // gray
                }

                var btn = go.GetComponent<Button>();
                btn.interactable = active;
                if (active && onClick != null)
                {
                    btn.onClick.AddListener(() => onClick());
                }

                var txtGo = new GameObject("Txt", typeof(RectTransform));
                txtGo.transform.SetParent(go.transform, false);
                StretchFull(txtGo.GetComponent<RectTransform>());
                var txt = txtGo.AddComponent<Text>();
                SetSharpText(txt, label, 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                txt.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        sealed class PaintPickHolder : MonoBehaviour
        {
            public VillageGameFlow Holder;

            public static void Refresh(GameObject battleRoot)
            {
                battleRoot?.GetComponentInChildren<PaintPickHolder>(true)?.Rebuild();
            }

            void OnEnable() => Rebuild();

            void Rebuild()
            {
                if (Holder == null) return;
                var content = transform as RectTransform;
                if (content == null) return;
                for (var i = content.childCount - 1; i >= 0; i--)
                    Destroy(content.GetChild(i).gameObject);

                var ledger = LocalHunterLedger.LoadOrCreate();
                var pick = Holder.GetPrepPickPaint();
                var rows = LoadPaintRows();

                // ✦ 智慧適應排序：如果當前任務的星級落在染色球吸引星級內，優先排在最頂端
                var activeQuest = VillageBattlePrepRules.FindQuestRow(ledger.ActiveQuestId);
                var questStar = activeQuest != null ? activeQuest.星級 : 1;
                var sortedRows = rows.Where(r => r != null && ledger.GetWarehouseQuantity(r.道具編號) > 0)
                    .OrderByDescending(r =>
                    {
                        return (questStar >= r.吸引星級_最低 && questStar <= r.吸引星級_最高);
                    })
                    .ThenBy(r => r.道具編號)
                    .ToList();

                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                foreach (var r in sortedRows)
                {
                    var id = r.道具編號;
                    // ✦ 改為帶有水平佈局的卡片
                    var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
                    go.transform.SetParent(content, false);

                    var isPick = (pick == id);
                    var matchesQuest = (questStar >= r.吸引星級_最低 && questStar <= r.吸引星級_最高);

                    var goImg = go.GetComponent<Image>();
                    goImg.color = isPick
                        ? new Color(0.08f, 0.28f, 0.52f, 0.95f) // 亮藍選定底色
                        : (matchesQuest 
                            ? new Color(0.15f, 0.16f, 0.2f, 0.95f) // 推薦底色
                            : new Color(0.1f, 0.11f, 0.13f, 0.85f)); // 正常底色

                    // ✦ 炫酷霓虹邊框
                    if (isPick)
                    {
                        var outl = go.AddComponent<Outline>();
                        outl.effectColor = new Color(0.15f, 0.75f, 1f, 0.95f); // 亮藍霓虹邊框
                        outl.effectDistance = new Vector2(1.5f, -1.5f);
                    }
                    else if (matchesQuest)
                    {
                        var outl = go.AddComponent<Outline>();
                        outl.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.6f); // 黃金推薦框
                        outl.effectDistance = new Vector2(1.2f, -1.2f);
                    }
                    else
                    {
                        var outl = go.AddComponent<Outline>();
                        outl.effectColor = new Color(1f, 1f, 1f, 0.08f); // 極細暗白邊
                        outl.effectDistance = new Vector2(1f, -1f);
                    }

                    var shad = go.AddComponent<Shadow>();
                    shad.effectColor = new Color(0f, 0f, 0f, 0.5f);
                    shad.effectDistance = new Vector2(2f, -2f);

                    var hg = go.GetComponent<HorizontalLayoutGroup>();
                    hg.padding = new RectOffset(16, 16, 8, 8);
                    hg.spacing = 16f;
                    hg.childAlignment = TextAnchor.MiddleLeft;
                    hg.childControlWidth = true;
                    hg.childControlHeight = true;
                    hg.childForceExpandWidth = false;
                    hg.childForceExpandHeight = false;

                    go.AddComponent<LayoutElement>().minHeight = 86f;
                    go.GetComponent<Button>().onClick.AddListener(() => Holder.SetPrepPaint(id));

                    // ✦ 1. 染色球左側精緻 Icon
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    iconGo.transform.SetParent(go.transform, false);
                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.sizeDelta = new Vector2(56f, 56f);
                    var iconLe = iconGo.GetComponent<LayoutElement>();
                    iconLe.preferredWidth = 56f;
                    iconLe.preferredHeight = 56f;
                    var iconImg = iconGo.GetComponent<Image>();
                    iconImg.sprite = SafeSpriteLoader.TryLoadSprite("Textures/Items/" + r.道具編號) ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
                    iconImg.color = Color.white;
                    iconImg.preserveAspect = true;
                    iconGo.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.15f);

                    // ✦ 2. 右側文字 Rich-Text
                    var tgo = new GameObject("TextContainer", typeof(RectTransform), typeof(LayoutElement));
                    tgo.transform.SetParent(go.transform, false);
                    tgo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                    var tx = tgo.AddComponent<Text>();
                    
                    var prefix = matchesQuest ? "<color=#27AE60>[推薦]</color> " : "";
                    SetSharpText(tx, $"<b>{prefix}{r.名稱}</b>\n<size=20>吸引：{r.吸引星級_最低}～{r.吸引星級_最高} ★　持有×{ledger.GetWarehouseQuantity(id)}</size>", 22, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
                    
                    var tShad = tgo.AddComponent<Shadow>();
                    tShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
                    tShad.effectDistance = new Vector2(1f, -1f);
                }
            }

            public static 染色球資料列[] LoadPaintRows()
            {
                if (!DesignDataReader.TryLoadDesignDataText(out var j, "04_Items", "paintballs.json"))
                    return Array.Empty<染色球資料列>();
                try
                {
                    return JsonConvert.DeserializeObject<染色球資料列[]>(j) ?? Array.Empty<染色球資料列>();
                }
                catch
                {
                    return Array.Empty<染色球資料列>();
                }
            }
        }

        sealed class TracePickHolder : MonoBehaviour
        {
            public VillageGameFlow Holder;

            public static void Refresh(GameObject battleRoot)
            {
                battleRoot?.GetComponentInChildren<TracePickHolder>(true)?.Rebuild();
            }

            void OnEnable() => Rebuild();

            void Rebuild()
            {
                if (Holder == null) return;
                var content = transform as RectTransform;
                if (content == null) return;
                for (var i = content.childCount - 1; i >= 0; i--)
                    Destroy(content.GetChild(i).gameObject);

                var ledger = LocalHunterLedger.LoadOrCreate();
                var pick = Holder.GetPrepPickTrace();
                var rows = LoadTraceRows();

                // ✦ 智慧適應排序：如果該痕跡對應的魔物在當前任務目標清單中，優先排在最頂端（第一項）
                var activeQuest = VillageBattlePrepRules.FindQuestRow(ledger.ActiveQuestId);
                var sortedRows = rows.Where(r => r != null && ledger.GetWarehouseQuantity(r.痕跡編號) > 0)
                    .OrderByDescending(r =>
                    {
                        if (activeQuest == null || activeQuest.目標魔物 == null) return false;
                        return activeQuest.目標魔物.Any(t => t != null && (t.魔物編號 ?? "").Trim() == (r.對應魔物編號 ?? "").Trim());
                    })
                    .ThenBy(r => r.痕跡編號)
                    .ToList();

                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                foreach (var r in sortedRows)
                {
                    var id = r.痕跡編號;
                    // ✦ 改為帶有水平佈局的卡片
                    var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
                    go.transform.SetParent(content, false);

                    var isPick = (pick == id);
                    var matchesQuest = activeQuest != null && activeQuest.目標魔物 != null &&
                                       activeQuest.目標魔物.Any(t => t != null && (t.魔物編號 ?? "").Trim() == (r.對應魔物編號 ?? "").Trim());

                    var goImg = go.GetComponent<Image>();
                    goImg.color = isPick
                        ? new Color(0.08f, 0.28f, 0.52f, 0.95f) // 亮藍選定底色
                        : (matchesQuest 
                            ? new Color(0.15f, 0.16f, 0.2f, 0.95f) // 推薦底色
                            : new Color(0.1f, 0.11f, 0.13f, 0.85f)); // 正常底色

                    // ✦ 炫酷霓霓虹邊框
                    if (isPick)
                    {
                        var outl = go.AddComponent<Outline>();
                        outl.effectColor = new Color(0.15f, 0.75f, 1f, 0.95f); // 亮藍高光邊框
                        outl.effectDistance = new Vector2(1.5f, -1.5f);
                    }
                    else if (matchesQuest)
                    {
                        var outl = go.AddComponent<Outline>();
                        outl.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.6f); // 黃金推薦邊框
                        outl.effectDistance = new Vector2(1.2f, -1.2f);
                    }
                    else
                    {
                        var outl = go.AddComponent<Outline>();
                        outl.effectColor = new Color(1f, 1f, 1f, 0.08f); // 極細暗白邊
                        outl.effectDistance = new Vector2(1f, -1f);
                    }

                    var shad = go.AddComponent<Shadow>();
                    shad.effectColor = new Color(0f, 0f, 0f, 0.5f);
                    shad.effectDistance = new Vector2(2f, -2f);

                    var hg = go.GetComponent<HorizontalLayoutGroup>();
                    hg.padding = new RectOffset(16, 16, 8, 8);
                    hg.spacing = 16f;
                    hg.childAlignment = TextAnchor.MiddleLeft;
                    hg.childControlWidth = true;
                    hg.childControlHeight = true;
                    hg.childForceExpandWidth = false;
                    hg.childForceExpandHeight = false;

                    go.AddComponent<LayoutElement>().minHeight = 86f;
                    go.GetComponent<Button>().onClick.AddListener(() => Holder.SetPrepTrace(id));

                    // ✦ 1. 痕跡左側直接載入該魔物的超高清圓頭像！
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    iconGo.transform.SetParent(go.transform, false);
                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.sizeDelta = new Vector2(56f, 56f);
                    var iconLe = iconGo.GetComponent<LayoutElement>();
                    iconLe.preferredWidth = 56f;
                    iconLe.preferredHeight = 56f;
                    var iconImg = iconGo.GetComponent<Image>();
                    iconImg.sprite = SafeSpriteLoader.TryLoadSprite("Textures/Monsters/" + r.對應魔物編號) ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
                    iconImg.color = Color.white;
                    iconImg.preserveAspect = true;

                    var icoOutl = iconGo.AddComponent<Outline>();
                    icoOutl.effectColor = matchesQuest ? new Color(0.85f, 0.65f, 0.2f, 0.5f) : new Color(1f, 1f, 1f, 0.15f);
                    icoOutl.effectDistance = new Vector2(1.2f, -1.2f);

                    // ✦ 2. 右側文字 Rich-Text
                    var tgo = new GameObject("TextContainer", typeof(RectTransform), typeof(LayoutElement));
                    tgo.transform.SetParent(go.transform, false);
                    tgo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                    var tx = tgo.AddComponent<Text>();

                    var prefix = matchesQuest ? "<color=#E2B93C>[任務目標]</color> " : "";
                    SetSharpText(tx, $"<b>{prefix}{r.名稱}</b>\n<size=20>星級：{r.魔物星級}★　魔物：{r.對應魔物編號}　持有×{ledger.GetWarehouseQuantity(id)}</size>", 22, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);

                    var tShad = tgo.AddComponent<Shadow>();
                    tShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
                    tShad.effectDistance = new Vector2(1f, -1f);
                }
            }

            public static 魔物痕跡資料列[] LoadTraceRows()
            {
                if (!DesignDataReader.TryLoadDesignDataText(out var j, "04_Items", "monster_traces.json"))
                    return Array.Empty<魔物痕跡資料列>();
                try
                {
                    return JsonConvert.DeserializeObject<魔物痕跡資料列[]>(j) ?? Array.Empty<魔物痕跡資料列>();
                }
                catch
                {
                    return Array.Empty<魔物痕跡資料列>();
                }
            }
        }

        sealed class QuestBoardUiHolder : MonoBehaviour
        {
            public RectTransform Content;
            public VillageGameFlow Flow;

            void OnEnable() => Rebuild();

            internal void Rebuild()
            {
                if (Content == null) return;
                for (var i = Content.childCount - 1; i >= 0; i--)
                    Destroy(Content.GetChild(i).gameObject);

                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var ledger = LocalHunterLedger.LoadOrCreate();
                ledger.TouchDailyQuestRollover();

                任務資料列[] rows;
                if (!DesignDataReader.TryLoadDesignDataText(out var json, "05_Systems", "quests.json"))
                    rows = Array.Empty<任務資料列>();
                else
                    try
                    {
                        rows = JsonConvert.DeserializeObject<任務資料列[]>(json) ?? Array.Empty<任務資料列>();
                    }
                    catch
                    {
                        rows = Array.Empty<任務資料列>();
                    }

                var active = (ledger.ActiveQuestId ?? "").Trim();

                var banner = new GameObject("ActiveBanner", typeof(RectTransform), typeof(Image));
                banner.transform.SetParent(Content, false);
                banner.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
                banner.AddComponent<LayoutElement>().minHeight = 72f;
                var bt = new GameObject("Txt", typeof(RectTransform));
                bt.transform.SetParent(banner.transform, false);
                StretchFull(bt.GetComponent<RectTransform>());
                var btx = bt.AddComponent<Text>();
                // ✦ 頂部顯示獵人目前等級、測試越級狀態與任務進行狀態
                string activeText = string.IsNullOrEmpty(active)
                    ? "目前無進行中任務"
                    : (rows.FirstOrDefault(r => r != null && r.任務編號 == active) != null
                        ? $"進行中：{rows.FirstOrDefault(r => r != null && r.任務編號 == active).標題}（{active}）"
                        : $"進行中：{active}");
                string bypassLabel = ledger.BypassStarLevelRestriction ? " <color=#F2C94C>[測試越級免檢]</color>" : "";
                string hrHeaderStr = $"<b><color=#56CCF2>獵人等級 (HR): {ledger.HunterLevel}</color></b>{bypassLabel}　｜　{activeText}";
                SetSharpText(btx, hrHeaderStr, 24, new Color(1f, 0.96f, 0.9f), TextAnchor.MiddleCenter, FontStyle.Bold);

                foreach (var q in rows)
                {
                    if (q == null) continue;
                    var row = new GameObject(q.任務編號, typeof(RectTransform), typeof(Image));
                    row.transform.SetParent(Content, false);
                    row.GetComponent<Image>().color = new Color(0.11f, 0.12f, 0.15f, 0.95f);
                    var h = row.AddComponent<HorizontalLayoutGroup>();
                    h.padding = new RectOffset(10, 10, 8, 8);
                    h.spacing = 8f;
                    row.AddComponent<LayoutElement>().minHeight = 100f;

                    var txtGo = new GameObject("Desc", typeof(RectTransform));
                    txtGo.transform.SetParent(row.transform, false);
                    txtGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    var tx = txtGo.AddComponent<Text>();
                    var tgt = q.目標魔物 != null && q.目標魔物.Length > 0
                        ? string.Join("、", q.目標魔物.Select(t => t?.魔物名稱 ?? t?.魔物編號))
                        : "—";
                    // ✦ 智慧顯示關卡星級所需的最低獵人等級
                    int reqHr = LocalHunterLedger.GetRequiredHrForStar(q.星級);
                    string hrStr = "";
                    if (ledger.HunterLevel >= reqHr)
                    {
                        hrStr = $" <color=#27AE60>(HR {ledger.HunterLevel} 已解鎖)</color>";
                    }
                    else if (ledger.BypassStarLevelRestriction)
                    {
                        hrStr = $" <color=#F2C94C>(需 HR {reqHr}｜測試越級中)</color>";
                    }
                    else
                    {
                        hrStr = $" <color=#EB5757>(需 HR {reqHr}｜等級不足)</color>";
                    }
                    SetSharpText(tx, $"{q.標題}\n{tgt}　★{q.星級}{hrStr}", 22, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);

                    var used = ledger.QuestIdsUsedToday != null && ledger.QuestIdsUsedToday.Contains(q.任務編號);
                    var isActive = active == q.任務編號;

                    if (isActive)
                    {
                        var ab = new GameObject("Abandon", typeof(RectTransform), typeof(Image), typeof(Button));
                        ab.transform.SetParent(row.transform, false);
                        ab.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 56f);
                        ab.GetComponent<Image>().color = new Color(0.42f, 0.23f, 0.18f, 1f);
                        ab.GetComponent<Button>().onClick.AddListener(() =>
                        {
                            ledger.AbandonActiveQuest();
                            Flow?.RefreshQuestUi();
                        });
                        AddMiniBtnLabel(ab.transform, font, "放棄");
                    }
                    else if (string.IsNullOrEmpty(active) && !used)
                    {
                        bool canAccept = ledger.CanChallengeStar(q.星級, out var reqHrTemp);
                        
                        var ab = new GameObject("Accept", typeof(RectTransform), typeof(Image), typeof(Button));
                        ab.transform.SetParent(row.transform, false);
                        ab.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 56f);
                        
                        var btnImg = ab.GetComponent<Image>();
                        var btn = ab.GetComponent<Button>();
                        
                        if (canAccept)
                        {
                            btnImg.color = new Color(0.25f, 0.4f, 0.28f, 1f); // 翡翠綠承接
                            btn.interactable = true;
                            btn.onClick.AddListener(() =>
                            {
                                if (ledger.TryAcceptQuest(q.任務編號, out var err))
                                    Flow?.RefreshQuestUi();
                                else
                                    Debug.LogWarning(err);
                            });
                            AddMiniBtnLabel(ab.transform, font, "承接");
                        }
                        else
                        {
                            btnImg.color = new Color(0.25f, 0.28f, 0.32f, 0.8f); // 禁用灰
                            btn.interactable = false;
                            AddMiniBtnLabel(ab.transform, font, "等級不足");
                        }
                    }
                    else
                    {
                        var tx2 = new GameObject("Lock", typeof(RectTransform));
                        tx2.transform.SetParent(row.transform, false);
                        tx2.AddComponent<LayoutElement>().preferredWidth = 120f;
                        var lt = tx2.AddComponent<Text>();
                        SetSharpText(lt, used ? "今日已接" : "已有任務", 20, new Color(0.7f, 0.7f, 0.75f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    }
                }
            }

            static void AddMiniBtnLabel(Transform p, Font font, string s)
            {
                var g = new GameObject("l", typeof(RectTransform));
                g.transform.SetParent(p, false);
                StretchFull(g.GetComponent<RectTransform>());
                var t = g.AddComponent<Text>();
                SetSharpText(t, s, 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }
        // ✦ 3x 超取樣高清文字外掛 (Super-Sampling sharp text helper)
        public static void SetSharpText(Text t, string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var flow = FindAnyObjectByType<VillageGameFlow>();
            t.font = flow != null ? flow._font : Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Arial" }, fontSize * 3);
            t.text = text;
            t.fontSize = fontSize * 3; // 渲染解析度放大 3 倍
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.transform.localScale = new Vector3(0.3333f, 0.3333f, 1f); // 縮放回 1/3，完美抗鋸齒
        }

        public static List<素材資料列> LoadAllItems()
        {
            var list = new List<素材資料列>();
            if (DesignDataReader.TryLoadDesignDataText(out var mj, "04_Items", "materials.json"))
            {
                var arr = DesignDataJsonArrayUtility.Parse素材資料(mj);
                if (arr != null)
                {
                    list.AddRange(arr);
                }
            }
            return list;
        }

        // ✦ 新增的背景呼吸特效控制器 (增強生命感與臨場感)
        sealed class BgBreather : MonoBehaviour
        {
            float _t = 0f;
            void Update()
            {
                _t += Time.deltaTime * 0.4f;
                // 產生 1.0 到 1.06 的平滑縮放 (大約 6% 的放大縮小)
                float scale = 1.03f + Mathf.Sin(_t) * 0.03f;
                transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void OpenItemSelectPopup(int slotIndex)
        {
            var pop = new GameObject("ItemSelectPopup", typeof(RectTransform), typeof(Image));
            pop.transform.SetParent(_goBattlePrep.transform, false);
            var prt = pop.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.1f, 0.15f);
            prt.anchorMax = new Vector2(0.9f, 0.85f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;

            var popImg = pop.GetComponent<Image>();
            popImg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);

            var popOutl = pop.AddComponent<Outline>();
            popOutl.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.8f);
            popOutl.effectDistance = new Vector2(1.5f, -1.5f);

            // 標題
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(pop.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0.9f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = _font;
            titleTxt.fontSize = 20;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.yellow;
            titleTxt.text = $"選擇欄位 {slotIndex + 1} 的道具";

            // 滑動內容區
            var scroll = CreateScrollAreaAnchored(pop.transform, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.88f));
            
            var ledger = LocalHunterLedger.LoadOrCreate();
            var allItems = LoadAllItems();

            // 只列出消耗品與特定可用道具
            var validItems = allItems.Where(x => x.分類 == "消耗品" || x.素材編號 == "ITM_001" || x.素材編號 == "ITM_002" || x.素材編號 == "ITM_017" || x.素材編號 == "ITM_018" || x.素材編號 == "ITM_026" || x.素材編號 == "ITM_030" || x.素材編號 == "ITM_031").ToList();

            // 增加一個「清除選擇」按鈕
            {
                var rowGo = new GameObject("ClearItem", typeof(RectTransform), typeof(Image), typeof(Button));
                rowGo.transform.SetParent(scroll.content, false);
                var rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.sizeDelta = new Vector2(500f, 54f);
                rowGo.GetComponent<Image>().color = new Color(0.2f, 0.1f, 0.1f, 0.8f);

                var txtGo = new GameObject("Txt", typeof(RectTransform));
                txtGo.transform.SetParent(rowGo.transform, false);
                var tRt = txtGo.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
                tRt.offsetMin = tRt.offsetMax = Vector2.zero;
                var txt = txtGo.AddComponent<Text>();
                txt.font = _font;
                txt.fontSize = 16;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.red;
                txt.text = "❌ 卸下當前道具";

                rowGo.GetComponent<Button>().onClick.AddListener(() =>
                {
                    var led = LocalHunterLedger.LoadOrCreate();
                    if (led.SelectedBattleItemIds == null) led.SelectedBattleItemIds = new List<string> { "", "", "" };
                    while (led.SelectedBattleItemIds.Count < 3) led.SelectedBattleItemIds.Add("");
                    led.SelectedBattleItemIds[slotIndex] = "";
                    led.Save();

                    Destroy(pop);
                    RefreshBattlePrepUi();
                });
            }

            foreach (var r in validItems)
            {
                int qty = ledger.GetWarehouseQuantity(r.素材編號);
                if (qty <= 0) continue; // 只有倉庫擁有的道具才可攜帶

                var rowGo = new GameObject(r.素材編號, typeof(RectTransform), typeof(Image), typeof(Button));
                rowGo.transform.SetParent(scroll.content, false);
                var rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.sizeDelta = new Vector2(500f, 54f);
                rowGo.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 0.85f);

                // 道具詳情文字
                var txtGo = new GameObject("Txt", typeof(RectTransform));
                txtGo.transform.SetParent(rowGo.transform, false);
                var tRt = txtGo.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
                tRt.offsetMin = tRt.offsetMax = Vector2.zero;
                var txt = txtGo.AddComponent<Text>();
                txt.font = _font;
                txt.fontSize = 15;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.text = $"{r.名稱} (持有: {qty} 個) - {r.描述}";

                string targetId = r.素材編號;
                rowGo.GetComponent<Button>().onClick.AddListener(() =>
                {
                    var led = LocalHunterLedger.LoadOrCreate();
                    if (led.SelectedBattleItemIds == null) led.SelectedBattleItemIds = new List<string> { "", "", "" };
                    while (led.SelectedBattleItemIds.Count < 3) led.SelectedBattleItemIds.Add("");

                    // 檢查是否重複攜帶
                    if (led.SelectedBattleItemIds.Contains(targetId))
                    {
                        int oldIdx = led.SelectedBattleItemIds.IndexOf(targetId);
                        led.SelectedBattleItemIds[oldIdx] = "";
                    }

                    led.SelectedBattleItemIds[slotIndex] = targetId;
                    led.Save();

                    Destroy(pop);
                    RefreshBattlePrepUi();
                });
            }

            // 關閉按鈕
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(pop.transform, false);
            var crt = closeGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.35f, 0.03f);
            crt.anchorMax = new Vector2(0.65f, 0.11f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            closeGo.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

            var closeTxtGo = new GameObject("Txt", typeof(RectTransform));
            closeTxtGo.transform.SetParent(closeGo.transform, false);
            var ctRt = closeTxtGo.GetComponent<RectTransform>();
            ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one;
            ctRt.offsetMin = ctRt.offsetMax = Vector2.zero;
            var closeTxt = closeTxtGo.AddComponent<Text>();
            closeTxt.font = _font;
            closeTxt.fontSize = 15;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            closeTxt.text = "取消關閉";

            closeGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                Destroy(pop);
            });
        }

        sealed class BattleItemPickHolder : MonoBehaviour
        {
            public VillageGameFlow Holder;

            public static void Refresh(GameObject battleRoot)
            {
                battleRoot?.GetComponentInChildren<BattleItemPickHolder>(true)?.Rebuild();
            }

            void OnEnable() => Rebuild();

            public void Rebuild()
            {
                if (Holder == null) return;
                var content = transform as RectTransform;
                if (content == null) return;
                
                for (var i = content.childCount - 1; i >= 0; i--)
                    Destroy(content.GetChild(i).gameObject);

                var ledger = LocalHunterLedger.LoadOrCreate();
                var items = ledger.SelectedBattleItemIds ?? new List<string>();
                
                while (items.Count < 3) items.Add("");

                var allItems = VillageGameFlow.LoadAllItems();

                for (int slotIdx = 0; slotIdx < 3; slotIdx++)
                {
                    int currentSlot = slotIdx;
                    var itemId = items[slotIdx];
                    var itemRow = allItems.FirstOrDefault(x => x.素材編號 == itemId);

                    // 槽按鈕
                    var slotGo = new GameObject($"Slot_{slotIdx}", typeof(RectTransform), typeof(Image), typeof(Button));
                    slotGo.transform.SetParent(content, false);
                    var sRt = slotGo.GetComponent<RectTransform>();
                    sRt.sizeDelta = new Vector2(80f, 80f);

                    var slotImg = slotGo.GetComponent<Image>();
                    slotImg.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
                    
                    var slotOutl = slotGo.AddComponent<Outline>();
                    slotOutl.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.5f);
                    slotOutl.effectDistance = new Vector2(1f, -1f);

                    var container = new GameObject("Container", typeof(RectTransform));
                    container.transform.SetParent(slotGo.transform, false);
                    var cRt = container.GetComponent<RectTransform>();
                    cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one;
                    cRt.offsetMin = Vector2.zero; cRt.offsetMax = Vector2.zero;

                    if (itemRow != null)
                    {
                        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                        iconGo.transform.SetParent(container.transform, false);
                        var iRt = iconGo.GetComponent<RectTransform>();
                        iRt.anchorMin = new Vector2(0.15f, 0.35f);
                        iRt.anchorMax = new Vector2(0.85f, 0.9f);
                        iRt.offsetMin = iRt.offsetMax = Vector2.zero;
                        
                        var iImg = iconGo.GetComponent<Image>();
                        iImg.preserveAspect = true;
                        var sp = SafeSpriteLoader.TryLoadSprite(itemRow.圖片路徑);
                        if (sp != null)
                        {
                            iImg.sprite = sp;
                            iImg.color = Color.white;
                        }
                        else
                        {
                            iImg.color = itemRow.分類 == "消耗品" ? new Color(0.1f, 0.65f, 0.25f, 0.7f) : new Color(0.85f, 0.5f, 0.05f, 0.7f);
                        }

                        var nameGo = new GameObject("Text", typeof(RectTransform));
                        nameGo.transform.SetParent(container.transform, false);
                        var nRt = nameGo.GetComponent<RectTransform>();
                        nRt.anchorMin = new Vector2(0f, 0f);
                        nRt.anchorMax = new Vector2(1f, 0.3f);
                        nRt.offsetMin = nRt.offsetMax = Vector2.zero;
                        
                        var txt = nameGo.AddComponent<Text>();
                        txt.font = Holder._font;
                        txt.fontSize = 11;
                        txt.fontStyle = FontStyle.Bold;
                        txt.alignment = TextAnchor.MiddleCenter;
                        txt.color = Color.white;

                        int qty = ledger.GetWarehouseQuantity(itemId);
                        txt.text = $"{itemRow.名稱.Substring(0, Mathf.Min(itemRow.名稱.Length, 4))} (x{qty})";
                    }
                    else
                    {
                        var emptyGo = new GameObject("Text", typeof(RectTransform));
                        emptyGo.transform.SetParent(container.transform, false);
                        var eRt = emptyGo.GetComponent<RectTransform>();
                        eRt.anchorMin = Vector2.zero; eRt.anchorMax = Vector2.one;
                        eRt.offsetMin = eRt.offsetMax = Vector2.zero;

                        var txt = emptyGo.AddComponent<Text>();
                        txt.font = Holder._font;
                        txt.fontSize = 13;
                        txt.alignment = TextAnchor.MiddleCenter;
                        txt.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
                        txt.text = "【未選擇】";
                    }

                    var btn = slotGo.GetComponent<Button>();
                    btn.onClick.AddListener(() =>
                    {
                        Holder.OpenItemSelectPopup(currentSlot);
                    });
                }
            }
        }
    }
}