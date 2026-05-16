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
        }

        void BuildTitle()
        {
            _goTitle = CreateFullScreenPanel("Flow_Title", PathTitle, out _);

            var chrome = new GameObject("Chrome", typeof(RectTransform));
            chrome.transform.SetParent(_goTitle.transform, false);
            StretchFull(chrome.GetComponent<RectTransform>());

            AddOutlinedTitle(chrome.transform, "MONSTER HUNTER", 52, new Vector2(0f, -160f));
            AddOutlinedTitle(chrome.transform, "試玩村莊", 36, new Color(1f, 0.88f, 0.38f), new Vector2(0f, -240f));

            CreateMhPrimaryButton(_goTitle.transform, "進入村莊", new Vector2(0f, 280f), new Vector2(520f, 112f),
                () => HideAllExcept(_goHub));
        }

        void BuildHub()
        {
            _goHub = CreateFullScreenPanel("Flow_Hub", PathHub, out var contentParent);
            AddHubHeader(contentParent.transform, "集會區域", ShowHub);

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(contentParent.transform, false);
            var gr = gridGo.GetComponent<RectTransform>();
            gr.anchorMin = new Vector2(0.06f, 0.12f);
            gr.anchorMax = new Vector2(0.94f, 0.78f);
            gr.offsetMin = gr.offsetMax = Vector2.zero;
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 100f);
            grid.spacing = new Vector2(16f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset(8, 8, 8, 8);

            void AddNav(string label, Action open)
            {
                CreateMhPrimaryButton(gridGo.transform, label, Vector2.zero, Vector2.zero, open, fillCell: true);
            }

            AddNav("商店", () =>
            {
                EnsureShopPanel();
                HideAllExcept(_goShop);
            });
            AddNav("任務板", () => HideAllExcept(_goQuest));
            AddNav("加工屋", () =>
            {
                EnsureWorkshopPanel();
                HideAllExcept(_goWorkshop);
            });
            AddNav("貓飯食堂", () => HideAllExcept(_goCanteen));
            AddNav("獵人倉庫", () => HideAllExcept(_goWarehouse));
            AddNav("寵物小屋", () => HideAllExcept(_goPet));
            AddNav("出戰整備", OpenBattlePrep);
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

            var info = new GameObject("PrepInfo", typeof(RectTransform));
            info.transform.SetParent(body.transform, false);
            var irt = info.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.06f, 0.72f);
            irt.anchorMax = new Vector2(0.94f, 0.92f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            _prepStatus = info.AddComponent<Text>();
            _prepStatus.font = _font;
            _prepStatus.fontSize = 22;
            _prepStatus.color = new Color(1f, 0.92f, 0.7f);
            _prepStatus.alignment = TextAnchor.UpperLeft;
            _prepStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            _prepStatus.verticalOverflow = VerticalWrapMode.Overflow;

            CreateSectionLabel(body.transform, "持有染色球（點選）", new Vector2(0.04f, 0.58f), new Vector2(0.48f, 0.63f));
            var scrollPaint = CreateScrollAreaAnchored(body.transform, new Vector2(0.04f, 0.12f), new Vector2(0.48f, 0.56f));

            CreateSectionLabel(body.transform, "持有魔物痕跡（點選）", new Vector2(0.52f, 0.58f), new Vector2(0.96f, 0.63f));
            var scrollTrace = CreateScrollAreaAnchored(body.transform, new Vector2(0.52f, 0.12f), new Vector2(0.96f, 0.56f));

            scrollPaint.content.gameObject.AddComponent<PaintPickHolder>().Holder = this;
            scrollTrace.content.gameObject.AddComponent<TracePickHolder>().Holder = this;

            var foot = new GameObject("PrepFoot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            foot.transform.SetParent(body.transform, false);
            var fr = foot.GetComponent<RectTransform>();
            fr.anchorMin = new Vector2(0.1f, 0.02f);
            fr.anchorMax = new Vector2(0.9f, 0.1f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            var hg = foot.GetComponent<HorizontalLayoutGroup>();
            hg.spacing = 20f;
            hg.childAlignment = TextAnchor.MiddleCenter;
            hg.childForceExpandHeight = true;
            hg.childForceExpandWidth = true;

            CreateMhSecondaryButton(foot.transform, "套用選擇", () =>
            {
                var led = LocalHunterLedger.LoadOrCreate();
                led.PreviewPaintballItemId = _prepPickPaint ?? "";
                led.PreviewTraceId = _prepPickTrace ?? "";
                led.Save();
                RefreshBattlePrepUi();
            }, anchored: Vector2.zero, size: new Vector2(240f, 64f), flexible: true);

            CreateMhPrimaryButton(foot.transform, "整備完成・出發討伐", Vector2.zero, new Vector2(340f, 72f), () =>
            {
                var led = LocalHunterLedger.LoadOrCreate();
                if (!VillageBattlePrepRules.TryValidate(led, out var err))
                {
                    _prepStatus.text = err;
                    return;
                }

                var q = VillageBattlePrepRules.FindQuestRow(led.ActiveQuestId.Trim());
                HuntSessionContext.PendingQuest = q;
                SceneManager.LoadScene(string.IsNullOrWhiteSpace(_battleSceneName) ? "Bootstrap" : _battleSceneName.Trim());
            }, fillLayout: true);

            _goBattlePrep.SetActive(false);
        }

        internal void RefreshBattlePrepUi()
        {
            if (_prepStatus == null) return;
            var led = LocalHunterLedger.LoadOrCreate();
            _prepPickPaint = (led.PreviewPaintballItemId ?? "").Trim();
            _prepPickTrace = (led.PreviewTraceId ?? "").Trim();
            var q = VillageBattlePrepRules.FindQuestRow(led.ActiveQuestId);
            var qline = q != null
                ? $"進行中任務：{q.標題}（{led.ActiveQuestId}）"
                : "尚未承接任務";
            var paintLine = string.IsNullOrEmpty(_prepPickPaint) ? "染色球：（未選）" : $"染色球：{_prepPickPaint}";
            var traceLine = string.IsNullOrEmpty(_prepPickTrace) ? "痕跡：（未選）" : $"痕跡：{_prepPickTrace}";
            _prepStatus.text =
                $"{qline}\n{paintLine}　｜　{traceLine}\n（痕跡星級須落在染色球吸引範圍內，且痕跡魔物須為任務目標。）";

            PaintPickHolder.Refresh(_goBattlePrep);
            TracePickHolder.Refresh(_goBattlePrep);
        }

        internal void SetPrepPaint(string id)
        {
            _prepPickPaint = id ?? "";
            RefreshBattlePrepUi();
        }

        internal void SetPrepTrace(string id)
        {
            _prepPickTrace = id ?? "";
            RefreshBattlePrepUi();
        }

        internal string GetPrepPickPaint() => _prepPickPaint;

        internal string GetPrepPickTrace() => _prepPickTrace;

        GameObject CreateFullScreenPanel(string name, string bgPath, out RectTransform contentArea)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            StretchFull(go.GetComponent<RectTransform>());

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            StretchFull(bgGo.GetComponent<RectTransform>());
            var img = bgGo.GetComponent<Image>();
            img.sprite = SafeSpriteLoader.TryLoadSprite(bgPath);
            img.type = img.sprite != null ? Image.Type.Simple : Image.Type.SolidColor;
            img.color = img.sprite != null ? Color.white : new Color(0.04f, 0.05f, 0.08f, 1f);

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(go.transform, false);
            StretchFull(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            var crt = content.GetComponent<RectTransform>();
            StretchFull(crt);
            contentArea = crt;
            return go;
        }

        void AddHubHeader(Transform parent, string title, Action onBack)
        {
            var bar = new GameObject("TopBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var br = bar.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 1f);
            br.anchorMax = new Vector2(1f, 1f);
            br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(0f, -110f);
            br.offsetMax = new Vector2(0f, 0f);
            bar.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(bar.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(800f, 64f);
            var tt = titleGo.AddComponent<Text>();
            tt.font = _font;
            tt.fontSize = 38;
            tt.fontStyle = FontStyle.Bold;
            tt.alignment = TextAnchor.MiddleCenter;
            tt.color = new Color(1f, 0.92f, 0.55f);
            tt.text = title;

            CreateMhSecondaryButton(bar.transform, "← 村莊", () => onBack?.Invoke(),
                anchored: new Vector2(-430f, 0f), size: new Vector2(200f, 56f));
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
            t.font = _font;
            t.fontSize = 24;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.96f, 0.82f);
            t.alignment = TextAnchor.MiddleLeft;
            t.text = msg;
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
            txt.text = line;
            txt.font = _font;
            txt.fontSize = size;
            txt.fontStyle = FontStyle.Bold;
            txt.color = fill;
            txt.alignment = TextAnchor.MiddleCenter;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(4f, -4f);
        }

        void CreateMhPrimaryButton(Transform parent, string label, Vector2 anchored, Vector2 size, Action onClick,
            bool fillCell = false, bool fillLayout = false)
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

            var img = go.GetComponent<Image>();
            img.color = new Color(0.62f, 0.38f, 0.12f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var tg = new GameObject("T", typeof(RectTransform));
            tg.transform.SetParent(go.transform, false);
            StretchFull(tg.GetComponent<RectTransform>());
            var t = tg.AddComponent<Text>();
            t.font = _font;
            t.text = label;
            t.fontSize = fillCell ? 26 : 30;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.96f, 0.88f);
            t.alignment = TextAnchor.MiddleCenter;
        }

        void CreateMhSecondaryButton(Transform parent, string label, Action onClick, Vector2 anchored,
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
            img.color = new Color(0.25f, 0.28f, 0.34f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var tg = new GameObject("T", typeof(RectTransform));
            tg.transform.SetParent(go.transform, false);
            StretchFull(tg.GetComponent<RectTransform>());
            var t = tg.AddComponent<Text>();
            t.font = _font;
            t.text = label;
            t.fontSize = 22;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
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
                go.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.92f);
                var h = go.GetComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(12, 12, 10, 10);
                h.spacing = 12f;
                go.AddComponent<LayoutElement>().minHeight = 96f;

                var tgo = new GameObject("Tx", typeof(RectTransform));
                tgo.transform.SetParent(go.transform, false);
                tgo.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var nt = tgo.AddComponent<Text>();
                nt.font = font;
                nt.fontSize = 22;
                nt.color = Color.white;
                nt.alignment = TextAnchor.MiddleLeft;
                var cost = row.花費;
                var z = cost != null ? cost.金幣 : 0;
                nt.text = $"{row.名稱}　　花費 {z} z";

                var btnGo = new GameObject("Buy", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(go.transform, false);
                btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 56f);
                btnGo.GetComponent<Image>().color = new Color(0.55f, 0.32f, 0.1f, 1f);
                var btn = btnGo.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (!TryBuyMeal(row, out var err))
                    {
                        Debug.LogWarning(err);
                        return;
                    }

                    var flow = FindObjectOfType<VillageGameFlow>();
                    if (flow != null) flow.RefreshCanteenStatus();
                });
                var bt = new GameObject("L", typeof(RectTransform));
                bt.transform.SetParent(btnGo.transform, false);
                StretchFull(bt.GetComponent<RectTransform>());
                var btx = bt.AddComponent<Text>();
                btx.font = font;
                btx.text = "購買";
                btx.fontSize = 22;
                btx.alignment = TextAnchor.MiddleCenter;
                btx.color = Color.white;
            }

            static bool TryBuyMeal(貓飯資料列 row, out string err)
            {
                err = null;
                var ledger = LocalHunterLedger.LoadOrCreate();
                var cost = row?.花費;
                if (cost == null)
                {
                    err = "資料錯誤";
                    return false;
                }

                if (ledger.Zenny < cost.金幣)
                {
                    err = "金幣不足";
                    return false;
                }

                if (cost.需求素材 != null)
                {
                    foreach (var m in cost.需求素材)
                    {
                        if (m == null || string.IsNullOrEmpty(m.素材編號)) continue;
                        var need = Mathf.Max(1, m.數量);
                        if (ledger.GetWarehouseQuantity(m.素材編號) < need)
                        {
                            err = $"素材不足：{m.素材編號}";
                            return false;
                        }
                    }
                }

                ledger.Zenny -= cost.金幣;
                ledger.Warehouse ??= new Dictionary<string, int>(StringComparer.Ordinal);
                if (cost.需求素材 != null)
                {
                    foreach (var m in cost.需求素材)
                    {
                        if (m == null || string.IsNullOrEmpty(m.素材編號)) continue;
                        var id = m.素材編號;
                        ledger.Warehouse[id] =
                            Mathf.Max(0, ledger.GetWarehouseQuantity(id) - Mathf.Max(1, m.數量));
                    }
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

                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var names = ResolveNameLookup();
                var keys = ledger.Warehouse.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(s => s).ToList();
                foreach (var id in keys)
                {
                    var qty = ledger.Warehouse[id];
                    var go = new GameObject(id, typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(Content, false);
                    go.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.92f);
                    go.AddComponent<LayoutElement>().minHeight = 64f;
                    var txt = new GameObject("T", typeof(RectTransform));
                    txt.transform.SetParent(go.transform, false);
                    StretchFull(txt.GetComponent<RectTransform>());
                    var t = txt.AddComponent<Text>();
                    t.font = font;
                    t.fontSize = 22;
                    t.color = Color.white;
                    t.alignment = TextAnchor.MiddleLeft;
                    t.text = $"{(names.TryGetValue(id, out var nm) ? nm : id)}　×{qty}";
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

            void Rebuild()
            {
                if (Content == null) return;
                for (var i = Content.childCount - 1; i >= 0; i--)
                    Destroy(Content.GetChild(i).gameObject);

                var ledger = LocalHunterLedger.LoadOrCreate();
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var pets = OwnedPetBattleBuffs.LoadAllPets();
                var chosen = (ledger.SelectedBattlePetId ?? "").Trim();

                var rowNone = new GameObject("Pet_None", typeof(RectTransform), typeof(Image), typeof(Button));
                rowNone.transform.SetParent(Content, false);
                rowNone.GetComponent<Image>().color =
                    string.IsNullOrEmpty(chosen) ? new Color(0.3f, 0.45f, 0.3f, 1f) : new Color(0.14f, 0.15f, 0.18f, 1f);
                rowNone.AddComponent<LayoutElement>().minHeight = 72f;
                rowNone.GetComponent<Button>().onClick.AddListener(() =>
                {
                    ledger.SelectedBattlePetId = "";
                    ledger.Save();
                    RefreshHolder(gameObject.transform.root.gameObject);
                });
                AddRowLabel(rowNone.transform, font, "（不指定，戰鬥隨機）");

                foreach (var p in pets)
                {
                    if (p == null) continue;
                    if (ledger.GetWarehouseQuantity(p.寵物編號) <= 0) continue;
                    var pick = p.寵物編號;
                    var row = new GameObject(pick, typeof(RectTransform), typeof(Image), typeof(Button));
                    row.transform.SetParent(Content, false);
                    row.GetComponent<Image>().color = chosen == pick
                        ? new Color(0.3f, 0.45f, 0.3f, 1f)
                        : new Color(0.14f, 0.15f, 0.18f, 1f);
                    row.AddComponent<LayoutElement>().minHeight = 72f;
                    row.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        ledger.SelectedBattlePetId = pick;
                        ledger.Save();
                        RefreshHolder(gameObject.transform.root.gameObject);
                    });
                    AddRowLabel(row.transform, font, $"{p.名稱}（{pick}）");
                }
            }

            static void AddRowLabel(Transform row, Font font, string msg)
            {
                var tgo = new GameObject("L", typeof(RectTransform));
                tgo.transform.SetParent(row, false);
                StretchFull(tgo.GetComponent<RectTransform>());
                var t = tgo.AddComponent<Text>();
                t.font = font;
                t.fontSize = 24;
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleLeft;
                t.text = msg;
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
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                foreach (var r in rows)
                {
                    if (r == null) continue;
                    if (ledger.GetWarehouseQuantity(r.道具編號) <= 0) continue;
                    var id = r.道具編號;
                    var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button));
                    go.transform.SetParent(content, false);
                    go.GetComponent<Image>().color = pick == id
                        ? new Color(0.35f, 0.42f, 0.55f, 1f)
                        : new Color(0.12f, 0.13f, 0.16f, 1f);
                    go.AddComponent<LayoutElement>().minHeight = 64f;
                    go.GetComponent<Button>().onClick.AddListener(() => Holder.SetPrepPaint(id));
                    PickRowText(go.transform, font,
                        $"{r.名稱}　吸引 {r.吸引星級_最低}～{r.吸引星級_最高} 星　持有×{ledger.GetWarehouseQuantity(id)}");
                }
            }

            static 染色球資料列[] LoadPaintRows()
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

            static void PickRowText(Transform p, Font font, string s)
            {
                var t = new GameObject("t", typeof(RectTransform));
                t.transform.SetParent(p, false);
                StretchFull(t.GetComponent<RectTransform>());
                var tx = t.AddComponent<Text>();
                tx.font = font;
                tx.fontSize = 20;
                tx.color = Color.white;
                tx.alignment = TextAnchor.MiddleLeft;
                tx.text = s;
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
                魔物痕跡資料列[] rows;
                if (!DesignDataReader.TryLoadDesignDataText(out var j, "04_Items", "monster_traces.json"))
                    rows = Array.Empty<魔物痕跡資料列>();
                else
                    try
                    {
                        rows = JsonConvert.DeserializeObject<魔物痕跡資料列[]>(j) ?? Array.Empty<魔物痕跡資料列>();
                    }
                    catch
                    {
                        rows = Array.Empty<魔物痕跡資料列>();
                    }

                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                foreach (var r in rows)
                {
                    if (r == null) continue;
                    if (ledger.GetWarehouseQuantity(r.痕跡編號) <= 0) continue;
                    var id = r.痕跡編號;
                    var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button));
                    go.transform.SetParent(content, false);
                    go.GetComponent<Image>().color = pick == id
                        ? new Color(0.35f, 0.42f, 0.55f, 1f)
                        : new Color(0.12f, 0.13f, 0.16f, 1f);
                    go.AddComponent<LayoutElement>().minHeight = 64f;
                    go.GetComponent<Button>().onClick.AddListener(() => Holder.SetPrepTrace(id));
                    PickRowText(go.transform, font,
                        $"{r.名稱}　星級 {r.魔物星級}　對應 {r.對應魔物編號}　×{ledger.GetWarehouseQuantity(id)}");
                }
            }

            static void PickRowText(Transform p, Font font, string s)
            {
                var t = new GameObject("t", typeof(RectTransform));
                t.transform.SetParent(p, false);
                StretchFull(t.GetComponent<RectTransform>());
                var tx = t.AddComponent<Text>();
                tx.font = font;
                tx.fontSize = 20;
                tx.color = Color.white;
                tx.alignment = TextAnchor.MiddleLeft;
                tx.text = s;
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
                btx.font = font;
                btx.fontSize = 24;
                btx.color = new Color(1f, 0.9f, 0.55f);
                btx.alignment = TextAnchor.MiddleCenter;
                if (string.IsNullOrEmpty(active))
                    btx.text = "目前無進行中任務";
                else
                {
                    var q0 = rows.FirstOrDefault(r => r != null && r.任務編號 == active);
                    btx.text = q0 != null ? $"進行中：{q0.標題}（{active}）" : $"進行中：{active}";
                }

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
                    tx.font = font;
                    tx.fontSize = 22;
                    tx.color = Color.white;
                    tx.alignment = TextAnchor.MiddleLeft;
                    var tgt = q.目標魔物 != null && q.目標魔物.Length > 0
                        ? string.Join("、", q.目標魔物.Select(t => t?.魔物名稱 ?? t?.魔物編號))
                        : "—";
                    tx.text = $"{q.標題}\n{tgt}　★{q.星級}";

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
                        var ab = new GameObject("Accept", typeof(RectTransform), typeof(Image), typeof(Button));
                        ab.transform.SetParent(row.transform, false);
                        ab.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 56f);
                        ab.GetComponent<Image>().color = new Color(0.25f, 0.4f, 0.28f, 1f);
                        ab.GetComponent<Button>().onClick.AddListener(() =>
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
                        var tx2 = new GameObject("Lock", typeof(RectTransform));
                        tx2.transform.SetParent(row.transform, false);
                        tx2.AddComponent<LayoutElement>().preferredWidth = 120f;
                        var lt = tx2.AddComponent<Text>();
                        lt.font = font;
                        lt.fontSize = 20;
                        lt.color = new Color(0.7f, 0.7f, 0.75f);
                        lt.alignment = TextAnchor.MiddleCenter;
                        lt.text = used ? "今日已接" : "已有任務";
                    }
                }
            }

            static void AddMiniBtnLabel(Transform p, Font font, string s)
            {
                var g = new GameObject("l", typeof(RectTransform));
                g.transform.SetParent(p, false);
                StretchFull(g.GetComponent<RectTransform>());
                var t = g.AddComponent<Text>();
                t.font = font;
                t.fontSize = 22;
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleCenter;
                t.text = s;
            }
        }
    }
}