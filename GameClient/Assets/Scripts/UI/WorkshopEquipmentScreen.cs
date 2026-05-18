using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterHunter.Combat;
using MonsterHunter.Data;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 加工屋：支援「當前裝備與技能 (裝備摘要＋聚合技能)」、「武器生產強化」與「防具生產強化」三大功能。
    /// 背景為 <c>Assets/UI/Backgrounds/Village/加工屋_背景.png</c>。
    /// 資料來源 <see cref="LocalHunterLedger"/>＋<c>equipment.json</c>／<c>armor.json</c>／<c>upgrade_rules.json</c>／<c>upgrade_rules_armor.json</c>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkshopEquipmentScreen : MonoBehaviour
    {
        const string BackgroundPath = "Assets/UI/Backgrounds/Village/加工屋_背景.png";

        static readonly string[] SlotLabels = { "武器", "頭", "身", "手", "腰", "腳" };

        enum WorkshopTab
        {
            LoadoutSkills,
            WeaponForge,
            ArmorForge,
        }

        System.Action _onClose;

        Font _font;
        RectTransform _loadoutList;
        RectTransform _skillsContent;
        Dictionary<string, 裝備資料列> _weapons = new Dictionary<string, 裝備資料列>(System.StringComparer.Ordinal);
        Dictionary<string, 裝備資料列> _armors = new Dictionary<string, 裝備資料列>(System.StringComparer.Ordinal);
        技能資料列[] _skills = System.Array.Empty<技能資料列>();

        WorkshopTab _tab = WorkshopTab.LoadoutSkills;
        string _statusLine = "";
        Dictionary<string, 裝備升級規則列> _upgradeRules = new Dictionary<string, 裝備升級規則列>(System.StringComparer.Ordinal);
        int _currentPage = 0;
        const int ItemsPerPage = 6;
        GameObject _pagerGo;
        Text _pagerText;
        Button _prevBtn;
        Button _nextBtn;

        /// <summary>建立加工屋 UI。</summary>
        public void Setup(Transform parent, System.Action onClose)
        {
            _onClose = onClose;
            transform.SetParent(parent, false);
            var rt = GetComponent<RectTransform>();
            StretchFull(rt);
            BuildUi();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        void BuildUi()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = GetComponent<RectTransform>();
            StretchFull(root);

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(root, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            StretchFull(bgRt);
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = SafeSpriteLoader.TryLoadSprite(BackgroundPath);
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
            bgImg.raycastTarget = true;

            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(root, false);
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.offsetMin = new Vector2(0f, -140f);
            hRt.offsetMax = new Vector2(0f, 0f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(header.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -20f);
            titleRt.sizeDelta = new Vector2(900f, 52f);
            var title = titleGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(title, "加工屋", 36, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            // ✦ 頂部右側金幣顯示欄
            var zennyGo = new GameObject("ZennyText", typeof(RectTransform));
            zennyGo.transform.SetParent(header.transform, false);
            var zrt = zennyGo.GetComponent<RectTransform>();
            zrt.anchorMin = zrt.anchorMax = new Vector2(1f, 0.5f);
            zrt.pivot = new Vector2(1f, 0.5f);
            zrt.anchoredPosition = new Vector2(-28f, 10f);
            zrt.sizeDelta = new Vector2(300f, 64f);
            var zt = zennyGo.AddComponent<Text>();
            
            var ledger = LocalHunterLedger.LoadOrCreate();
            VillageGameFlow.SetSharpText(zt, $"金幣: <color=#F2C94C>{ledger.Zenny} z</color>", 32, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleRight, FontStyle.Bold);
            
            var textShad = zennyGo.AddComponent<Shadow>();
            textShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textShad.effectDistance = new Vector2(1.2f, -1.2f);

            // ✦ 狀態指示列
            var statusGo = new GameObject("StatusText", typeof(RectTransform));
            statusGo.transform.SetParent(header.transform, false);
            var srt = statusGo.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 8f);
            srt.sizeDelta = new Vector2(900f, 40f);
            var st = statusGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(st, "", 22, new Color(1f, 0.65f, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
            
            var stShad = statusGo.AddComponent<Shadow>();
            stShad.effectColor = new Color(0f, 0f, 0f, 0.75f);
            stShad.effectDistance = new Vector2(1f, -1f);

            var closeBtn = CreateHeaderButton(header.transform, "返回村莊",
                new Vector2(0f, -84f), new Vector2(240f, 50f), () =>
                {
                    gameObject.SetActive(false);
                    _onClose?.Invoke();
                });

            // ✦ 頁籤列
            var tabBar = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabBar.transform.SetParent(root, false);
            var tabRt = tabBar.GetComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0f, 1f);
            tabRt.anchorMax = new Vector2(1f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.offsetMin = new Vector2(24f, -200f);
            tabRt.offsetMax = new Vector2(-24f, -140f);
            var hg = tabBar.GetComponent<HorizontalLayoutGroup>();
            hg.childAlignment = TextAnchor.MiddleCenter;
            hg.spacing = 12f;
            hg.padding = new RectOffset(8, 8, 4, 4);

            AddTabButton(tabBar.transform, "當前裝備與技能", () => SetTab(WorkshopTab.LoadoutSkills));
            AddTabButton(tabBar.transform, "武器生產・強化", () => SetTab(WorkshopTab.WeaponForge));
            AddTabButton(tabBar.transform, "防具生產・強化", () => SetTab(WorkshopTab.ArmorForge));

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(root, false);
            var bRt = body.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.offsetMin = new Vector2(24f, 24f);
            bRt.offsetMax = new Vector2(-24f, -210f);

            var left = new GameObject("LoadoutColumn", typeof(RectTransform));
            left.transform.SetParent(body.transform, false);
            var lRt = left.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0f, 0f);
            lRt.anchorMax = new Vector2(0.34f, 1f);
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            var leftBg = left.AddComponent<Image>();
            leftBg.color = new Color(0f, 0f, 0f, 0.35f);
            var leftPad = new GameObject("Pad", typeof(RectTransform), typeof(VerticalLayoutGroup));
            leftPad.transform.SetParent(left.transform, false);
            var lpRt = leftPad.GetComponent<RectTransform>();
            StretchFull(lpRt);
            lpRt.offsetMin = new Vector2(12f, 12f);
            lpRt.offsetMax = new Vector2(-12f, -12f);
            var lv = leftPad.GetComponent<VerticalLayoutGroup>();
            lv.spacing = 10f;
            lv.childAlignment = TextAnchor.UpperCenter;
            lv.childControlHeight = false;
            lv.childForceExpandHeight = false;
            _loadoutList = leftPad.GetComponent<RectTransform>();

            var right = new GameObject("SkillsArea", typeof(RectTransform));
            right.transform.SetParent(body.transform, false);
            var rRt = right.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(0.35f, 0f);
            rRt.anchorMax = new Vector2(1f, 1f);
            rRt.offsetMin = new Vector2(8f, 0f);
            rRt.offsetMax = new Vector2(0f, 0f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(right.transform, false);
            var sRt = scrollGo.GetComponent<RectTransform>();
            StretchFull(sRt);
            scrollGo.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.5f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            StretchFull(vpRt);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _skillsContent = content.GetComponent<RectTransform>();
            StretchFull(_skillsContent);
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(340f, 160f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.padding = new RectOffset(8, 8, 8, 8);

            var fit = content.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = vpRt;
            sr.content = _skillsContent;
            sr.horizontal = false;
            sr.vertical = true;

            // ✦ 底部分頁欄 (Pager)
            _pagerGo = new GameObject("Pager", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _pagerGo.transform.SetParent(right.transform, false);
            var pagerRt = _pagerGo.GetComponent<RectTransform>();
            pagerRt.anchorMin = new Vector2(0f, 0f);
            pagerRt.anchorMax = new Vector2(1f, 0f);
            pagerRt.pivot = new Vector2(0.5f, 0f);
            pagerRt.offsetMin = Vector2.zero;
            pagerRt.offsetMax = new Vector2(0f, 60f); // Height 60

            var phg = _pagerGo.GetComponent<HorizontalLayoutGroup>();
            phg.childAlignment = TextAnchor.MiddleCenter;
            phg.spacing = 30f;

            _prevBtn = CreatePagerButton(_pagerGo.transform, "上一頁", null);

            var txtGo = new GameObject("PageText", typeof(RectTransform), typeof(LayoutElement));
            txtGo.transform.SetParent(_pagerGo.transform, false);
            txtGo.GetComponent<LayoutElement>().preferredWidth = 180f;
            _pagerText = txtGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(_pagerText, "第 1 / 1 頁", 20, new Color(0.95f, 0.79f, 0.18f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _pagerText.verticalOverflow = VerticalWrapMode.Overflow;

            _nextBtn = CreatePagerButton(_pagerGo.transform, "下一頁", null);
        }

        void OnEnable()
        {
            _weapons = EquipmentSkillAggregator.LoadEquipmentDictionary();
            _armors = EquipmentSkillAggregator.LoadArmorDictionary();
            _skills = EquipmentSkillAggregator.LoadSkillsOrEmpty();
            LoadUpgradeRules();
            Refresh();
        }

        void LoadUpgradeRules()
        {
            _upgradeRules.Clear();
            if (DesignDataReader.TryLoadDesignDataText(out var json, "02_Equipment", "upgrade_rules.json"))
            {
                try
                {
                    var rows = JsonConvert.DeserializeObject<裝備升級規則列[]>(json);
                    if (rows != null)
                    {
                        foreach (var r in rows)
                        {
                            if (r != null && !string.IsNullOrEmpty(r.裝備編號))
                                _upgradeRules[r.裝備編號] = r;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Workshop] upgrade_rules.json：" + e.Message);
                }
            }

            if (DesignDataReader.TryLoadDesignDataText(out var armorJson, "02_Equipment", "upgrade_rules_armor.json"))
            {
                try
                {
                    var rows = JsonConvert.DeserializeObject<裝備升級規則列[]>(armorJson);
                    if (rows != null)
                    {
                        foreach (var r in rows)
                        {
                            if (r != null && !string.IsNullOrEmpty(r.裝備編號))
                                _upgradeRules[r.裝備編號] = r;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Workshop] upgrade_rules_armor.json：" + e.Message);
                }
            }
        }

        void SetTab(WorkshopTab t)
        {
            _tab = t;
            _currentPage = 0; // Reset page
            _statusLine = "";
            Refresh();
        }

        void Refresh()
        {
            if (_loadoutList == null) return;

            var ledger = LocalHunterLedger.LoadOrCreate();
            
            // 重新整理金幣顯示
            var zennyGo = transform.Find("Header/ZennyText");
            if (zennyGo != null)
            {
                var zt = zennyGo.GetComponent<Text>();
                if (zt != null)
                {
                    zt.text = $"金幣: <color=#F2C94C>{ledger.Zenny} z</color>";
                }
            }

            // 重新整理狀態行
            var statusGo = transform.Find("Header/StatusText");
            if (statusGo != null)
            {
                var st = statusGo.GetComponent<Text>();
                if (st != null)
                {
                    st.text = _statusLine ?? "";
                }
            }

            var loadoutColumn = transform.Find("Body/LoadoutColumn");
            var skillsArea = transform.Find("Body/SkillsArea") as RectTransform;
            var grid = _skillsContent.GetComponent<GridLayoutGroup>();
            var scrollGo = transform.Find("Body/SkillsArea/Scroll");
            var sRt = scrollGo != null ? scrollGo.GetComponent<RectTransform>() : null;
 
            if (_tab == WorkshopTab.LoadoutSkills)
            {
                if (loadoutColumn != null) loadoutColumn.gameObject.SetActive(true);
                if (skillsArea != null)
                {
                    skillsArea.anchorMin = new Vector2(0.35f, 0f);
                    skillsArea.offsetMin = new Vector2(8f, 0f);
                }
                if (grid != null)
                {
                    grid.cellSize = new Vector2(340f, 160f);
                    grid.spacing = new Vector2(10f, 10f);
                    grid.constraintCount = 2;
                }
                if (_pagerGo != null) _pagerGo.SetActive(false);
                if (sRt != null) sRt.offsetMin = Vector2.zero; // Stretch full
 
                RebuildLoadoutSkills(ledger);
            }
            else
            {
                if (loadoutColumn != null) loadoutColumn.gameObject.SetActive(false);
                if (skillsArea != null)
                {
                    skillsArea.anchorMin = new Vector2(0f, 0f);
                    skillsArea.offsetMin = Vector2.zero;
                }
                if (grid != null)
                {
                    grid.cellSize = new Vector2(480f, 180f); // 2-column layout!
                    grid.spacing = new Vector2(16f, 16f);
                    grid.constraintCount = 2;
                }
                if (_pagerGo != null) _pagerGo.SetActive(true);
                if (sRt != null) sRt.offsetMin = new Vector2(0f, 60f); // Leave 60px space for pager!
 
                RebuildForgeList(ledger);
            }
        }

        void RebuildLoadoutSkills(LocalHunterLedger ledger)
        {
            for (var i = _loadoutList.childCount - 1; i >= 0; i--)
                Destroy(_loadoutList.GetChild(i).gameObject);

            for (var i = _skillsContent.childCount - 1; i >= 0; i--)
                Destroy(_skillsContent.GetChild(i).gameObject);

            ledger.NormalizeEquippedArmorSlots();

            var wId = string.IsNullOrWhiteSpace(ledger.EquippedWeaponEquipmentId)
                ? "WEP_001"
                : ledger.EquippedWeaponEquipmentId.Trim();

            CreateLoadoutRow(0, wId);

            for (var k = 0; k < 5; k++)
            {
                var aid = ledger.EquippedArmorSlotIds[k];
                CreateLoadoutRow(k + 1, aid);
            }

            var skillRows = EquipmentSkillAggregator.BuildDisplayList(wId, ledger.EquippedArmorSlotIds, _skills);
            foreach (var s in skillRows)
                CreateSkillCell(s);
        }

        void RebuildForgeList(LocalHunterLedger ledger)
        {
            for (var i = _skillsContent.childCount - 1; i >= 0; i--)
                Destroy(_skillsContent.GetChild(i).gameObject);
 
            List<裝備資料列> list = null;
            if (_tab == WorkshopTab.WeaponForge)
            {
                list = _weapons.Values.OrderBy(w => w.裝備編號).ToList();
            }
            else if (_tab == WorkshopTab.ArmorForge)
            {
                list = _armors.Values.OrderBy(a => a.裝備編號).ToList();
            }

            if (list == null) return;

            var totalItems = list.Count;
            var totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / ItemsPerPage));
            _currentPage = Mathf.Clamp(_currentPage, 0, totalPages - 1);

            // Update pager UI
            if (_pagerText != null)
            {
                _pagerText.text = $"第 <color=#F2C94C>{_currentPage + 1}</color> / {totalPages} 頁";
            }

            if (_prevBtn != null)
            {
                _prevBtn.interactable = _currentPage > 0;
                _prevBtn.onClick.RemoveAllListeners();
                if (_currentPage > 0)
                {
                    _prevBtn.onClick.AddListener(() =>
                    {
                        _currentPage--;
                        Refresh();
                    });
                }
                var pImg = _prevBtn.GetComponent<Image>();
                if (pImg != null) pImg.color = _currentPage > 0 ? new Color(0.12f, 0.13f, 0.16f, 0.95f) : new Color(0.16f, 0.18f, 0.22f, 0.4f);
            }

            if (_nextBtn != null)
            {
                _nextBtn.interactable = _currentPage < totalPages - 1;
                _nextBtn.onClick.RemoveAllListeners();
                if (_currentPage < totalPages - 1)
                {
                    _nextBtn.onClick.AddListener(() =>
                    {
                        _currentPage++;
                        Refresh();
                    });
                }
                var nImg = _nextBtn.GetComponent<Image>();
                if (nImg != null) nImg.color = _currentPage < totalPages - 1 ? new Color(0.12f, 0.13f, 0.16f, 0.95f) : new Color(0.16f, 0.18f, 0.22f, 0.4f);
            }

            // Render paginated items!
            var pageItems = list.Skip(_currentPage * ItemsPerPage).Take(ItemsPerPage);
            foreach (var item in pageItems)
            {
                if (item == null) continue;
                CreateForgeRow(item, ledger);
            }
        }

        bool CanCraft(裝備資料列 item, LocalHunterLedger ledger, out string reason)
        {
            reason = "";
            if (item.合成配方 == null)
            {
                reason = "此裝備無法直接生產";
                return false;
            }

            if (ledger.Zenny < item.合成配方.所需金幣)
            {
                reason = "金幣不足以生產此裝備";
                return false;
            }

            if (item.合成配方.需求素材 != null)
            {
                foreach (var mat in item.合成配方.需求素材)
                {
                    if (mat == null || string.IsNullOrEmpty(mat.素材編號)) continue;
                    var owned = ledger.GetWarehouseQuantity(mat.素材編號);
                    if (owned < mat.需求數量)
                    {
                        reason = $"生產材料不足 ({mat.素材名稱 ?? mat.素材編號}: {owned}/{mat.需求數量})";
                        return false;
                    }
                }
            }

            return true;
        }

        void CraftEquipment(裝備資料列 item)
        {
            var ledger = LocalHunterLedger.LoadOrCreate();
            if (!CanCraft(item, ledger, out var reason))
            {
                _statusLine = $"<color=#EB5757>⚠ 生產失敗：{reason}</color>";
                Refresh();
                return;
            }

            // 扣除金幣與素材
            ledger.Zenny -= item.合成配方.所需金幣;
            if (item.合成配方.需求素材 != null)
            {
                foreach (var mat in item.合成配方.需求素材)
                {
                    if (mat == null || string.IsNullOrEmpty(mat.素材編號)) continue;
                    if (ledger.Warehouse.ContainsKey(mat.素材編號))
                        ledger.Warehouse[mat.素材編號] -= mat.需求數量;
                }
            }

            // 新增擁有紀錄且初始等級為 1
            ledger.EquipmentLevels[item.裝備編號] = 1;
            
            // 自動幫玩家穿上
            EquipItemInternal(item.裝備編號, item.裝備類型, ledger);

            ledger.Save();
            _statusLine = $"<color=#27AE60>✔ 成功生產並裝備了：{item.名稱}！</color>";
            Refresh();
        }

        bool CanUpgrade(裝備資料列 item, int currentLevel, LocalHunterLedger ledger, out 裝備升級路徑項 nextPath, out string reason)
        {
            nextPath = null;
            reason = "";

            if (!_upgradeRules.TryGetValue(item.裝備編號, out var rule) || rule == null)
            {
                reason = "此裝備在升級資料庫中無強化規則";
                return false;
            }

            if (currentLevel >= rule.最高等級)
            {
                reason = "已達此裝備的強化上限等級";
                return false;
            }

            var nextLevel = currentLevel + 1;
            if (rule.升級路徑 != null)
            {
                nextPath = rule.升級路徑.FirstOrDefault(p => p != null && p.等級 == nextLevel);
            }

            if (nextPath == null || nextPath.升級花費 == null)
            {
                reason = $"未定義等級 {nextLevel} 的強化花費與配方";
                return false;
            }

            if (ledger.Zenny < nextPath.升級花費.金幣)
            {
                reason = $"金幣不足以進行等級 {nextLevel} 的強化";
                return false;
            }

            if (nextPath.升級花費.需求素材 != null)
            {
                foreach (var mat in nextPath.升級花費.需求素材)
                {
                    if (mat == null || string.IsNullOrEmpty(mat.素材編號)) continue;
                    var owned = ledger.GetWarehouseQuantity(mat.素材編號);
                    if (owned < mat.需求數量)
                    {
                        reason = $"強化材料不足 (需要 {mat.素材編號}: {owned}/{mat.需求數量})";
                        return false;
                    }
                }
            }

            return true;
        }

        void UpgradeEquipment(裝備資料列 item)
        {
            var ledger = LocalHunterLedger.LoadOrCreate();
            var currentLevel = ledger.GetEquipmentLevel(item.裝備編號);

            if (!CanUpgrade(item, currentLevel, ledger, out var nextPath, out var reason))
            {
                _statusLine = $"<color=#EB5757>⚠ 強化失敗：{reason}</color>";
                Refresh();
                return;
            }

            // 扣除金幣與素材
            ledger.Zenny -= nextPath.升級花費.金幣;
            if (nextPath.升級花費.需求素材 != null)
            {
                foreach (var mat in nextPath.升級花費.需求素材)
                {
                    if (mat == null || string.IsNullOrEmpty(mat.素材編號)) continue;
                    if (ledger.Warehouse.ContainsKey(mat.素材編號))
                        ledger.Warehouse[mat.素材編號] -= mat.需求數量;
                }
            }

            // 等級提升
            ledger.EquipmentLevels[item.裝備編號] = currentLevel + 1;
            ledger.Save();

            _statusLine = $"<color=#27AE60>✔ {item.名稱} 成功強化至 Lv {currentLevel + 1}！傷害與屬性已暴漲！</color>";
            Refresh();
        }

        void EquipEquipment(裝備資料列 item)
        {
            var ledger = LocalHunterLedger.LoadOrCreate();
            EquipItemInternal(item.裝備編號, item.裝備類型, ledger);
            ledger.Save();
            _statusLine = $"<color=#27AE60>✔ 成功裝備了：{item.名稱}</color>";
            Refresh();
        }

        void EquipItemInternal(string id, string type, LocalHunterLedger ledger)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type)) return;
            var t = type.Trim();
            if (t == "武器")
            {
                ledger.EquippedWeaponEquipmentId = id;
            }
            else
            {
                var idx = -1;
                if (t == "頭部") idx = 0;
                else if (t == "胸部") idx = 1;
                else if (t == "腕部") idx = 2;
                else if (t == "腰部") idx = 3;
                else if (t == "腳部") idx = 4;

                if (idx >= 0 && idx < 5)
                {
                    ledger.EquippedArmorSlotIds[idx] = id;
                }
            }
        }

        void CreateForgeRow(裝備資料列 item, LocalHunterLedger ledger)
        {
            var cell = new GameObject("Forge_" + item.裝備編號, typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            cell.transform.SetParent(_skillsContent, false);

            var currentLevel = ledger.GetEquipmentLevel(item.裝備編號);
            var isOwned = ledger.EquipmentLevels.ContainsKey(item.裝備編號);
            
            bool isEquipped = false;
            if (item.裝備類型 == "武器")
            {
                isEquipped = (ledger.EquippedWeaponEquipmentId ?? "").Trim() == item.裝備編號;
            }
            else
            {
                isEquipped = ledger.EquippedArmorSlotIds != null && ledger.EquippedArmorSlotIds.Contains(item.裝備編號);
            }

            var img = cell.GetComponent<Image>();
            img.color = isEquipped
                ? new Color(0.08f, 0.28f, 0.52f, 0.95f) // 裝備中：亮選定寶藍底色
                : (isOwned 
                    ? new Color(0.12f, 0.16f, 0.22f, 0.95f) // 已擁有已解鎖：高質量深鋼卡片
                    : new Color(0.07f, 0.08f, 0.1f, 0.9f)); // 未生產：精緻黑鋼底色

            var outl = cell.AddComponent<Outline>();
            if (isEquipped)
            {
                outl.effectColor = new Color(0.15f, 0.75f, 1f, 0.95f); // 亮藍高發光框
                outl.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else if (isOwned)
            {
                outl.effectColor = new Color(0.85f, 0.65f, 0.3f, 0.45f); // 黃金已解鎖框
                outl.effectDistance = new Vector2(1.2f, -1.2f);
            }
            else
            {
                outl.effectColor = new Color(1f, 1f, 1f, 0.08f); // 未解鎖極細邊框
                outl.effectDistance = new Vector2(1f, -1f);
            }

            var shad = cell.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            var hg = cell.GetComponent<HorizontalLayoutGroup>();
            hg.padding = new RectOffset(16, 16, 10, 10);
            hg.spacing = 16f;
            hg.childAlignment = TextAnchor.MiddleLeft;
            hg.childControlWidth = true;
            hg.childControlHeight = true;
            hg.childForceExpandWidth = false;
            hg.childForceExpandHeight = false;

            cell.GetComponent<LayoutElement>().minHeight = 180f;
 
            // 1. 左側圖示
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(cell.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(72f, 72f);
            var iconLe = iconGo.GetComponent<LayoutElement>();
            iconLe.preferredWidth = iconLe.preferredHeight = 72f;
            var iconImg = iconGo.GetComponent<Image>();
             
            var path = item.圖示路徑;
            if (string.IsNullOrWhiteSpace(path)) path = item.圖片路徑;
            if (!string.IsNullOrWhiteSpace(path))
            {
                path = path.Trim();
                if (path.Contains("Equipments/"))
                {
                    path = path.Replace("Equipments/", "Equipment/");
                }
            }
            var sp = !string.IsNullOrWhiteSpace(path) ? SafeSpriteLoader.TryLoadSprite(path) : null;
            iconImg.sprite = sp ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconGo.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.15f);


            // 2. 中間描述與配方文字
            var textCol = new GameObject("Texts", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            textCol.transform.SetParent(cell.transform, false);
            textCol.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var vg = textCol.GetComponent<VerticalLayoutGroup>();
            vg.spacing = 4f;
            vg.childAlignment = TextAnchor.MiddleLeft;
            vg.childControlWidth = true;
            vg.childControlHeight = true;
            vg.childForceExpandWidth = true;
            vg.childForceExpandHeight = false;

            var nameText = isEquipped 
                ? $"<color=#00EAFF><b>[已裝備] {item.名稱}</b></color>" 
                : $"<b>{item.名稱}</b>";
            var nameLbl = AddBareText(textCol.transform, nameText, 24, TextAnchor.MiddleLeft, new Color(0.96f, 0.97f, 1f));
            nameLbl.fontStyle = FontStyle.Bold;

            // 動態估算數值顯示（若有升級則套用乘數）
            var statStr = "";
            if (item.裝備類型 == "武器")
            {
                var dmg = item.基礎數值 != null ? item.基礎數值.物理傷害 : 0;
                var elem = item.基礎數值 != null ? item.基礎數值.屬性傷害 : 0;
                
                if (isOwned && currentLevel > 1 && _upgradeRules.TryGetValue(item.裝備編號, out var rule))
                {
                    var pathItem = rule.升級路徑?.FirstOrDefault(p => p != null && p.等級 == currentLevel);
                    var mul = pathItem != null ? pathItem.數值加成倍率 : 1f;
                    statStr = $"物理傷害: <color=#F2C94C>{Mathf.RoundToInt(dmg * mul)}</color> (基礎 {dmg}) ｜ 屬性傷害: <color=#A17FFF>{Mathf.RoundToInt(elem * mul)}</color> (基礎 {elem}) ｜ 屬性: {item.裝備屬性}";
                }
                else
                {
                    statStr = $"物理傷害: <color=#F2C94C>{dmg}</color> ｜ 屬性傷害: <color=#A17FFF>{elem}</color> ｜ 屬性: {item.裝備屬性}";
                }
            }
            else
            {
                var df = item.基礎數值 != null ? item.基礎數值.物理防御 : 0;
                var edf = item.基礎數值 != null ? item.基礎數值.屬性防御 : 0;
                
                if (isOwned && currentLevel > 1 && _upgradeRules.TryGetValue(item.裝備編號, out var rule))
                {
                    var pathItem = rule.升級路徑?.FirstOrDefault(p => p != null && p.等級 == currentLevel);
                    var mul = pathItem != null ? pathItem.數值加成倍率 : 1f;
                    statStr = $"物理防御: <color=#F2C94C>{Mathf.RoundToInt(df * mul)}</color> (基礎 {df}) ｜ 屬性防御: <color=#A17FFF>{Mathf.RoundToInt(edf * mul)}</color> (基礎 {edf}) ｜ 部位: {item.裝備類型}";
                }
                else
                {
                    statStr = $"物理防御: <color=#F2C94C>{df}</color> ｜ 屬性防御: <color=#A17FFF>{edf}</color> ｜ 部位: {item.裝備類型}";
                }
            }

            var subLbl = AddBareText(textCol.transform, statStr, 18, TextAnchor.MiddleLeft, new Color(0.75f, 0.78f, 0.85f));

            // 配方消耗或強化花費文字
            var costStr = "";
            if (!isOwned)
            {
                if (item.合成配方 != null)
                {
                    var mats = new List<string>();
                    if (item.合成配方.需求素材 != null)
                    {
                        foreach (var m in item.合成配方.需求素材)
                        {
                            if (m == null) continue;
                            var owned = ledger.GetWarehouseQuantity(m.素材編號);
                            var color = owned >= m.需求數量 ? "#27AE60" : "#EB5757";
                            mats.Add($"<color={color}>{m.素材名稱 ?? m.素材編號} ({owned}/{m.需求數量})</color>");
                        }
                    }
                    costStr = $"生產花費: <color=#F2C94C>{item.合成配方.所需金幣} z</color> ｜ 素材: {string.Join("，", mats)}";
                }
                else
                {
                    costStr = "<color=#EB5757>非生產裝備（解鎖限定）</color>";
                }
            }
            else
            {
                var nextLevel = currentLevel + 1;
                if (_upgradeRules.TryGetValue(item.裝備編號, out var rule) && rule != null)
                {
                    if (currentLevel >= rule.最高等級)
                    {
                        costStr = $"<color=#27AE60>當前等級: Lv {currentLevel} (已達滿級 ★★★★★)</color>";
                    }
                    else
                    {
                        var pathItem = rule.升級路徑?.FirstOrDefault(p => p != null && p.等級 == nextLevel);
                        if (pathItem != null && pathItem.升級花費 != null)
                        {
                            var mats = new List<string>();
                            if (pathItem.升級花費.需求素材 != null)
                            {
                                foreach (var m in pathItem.升級花費.需求素材)
                                {
                                    if (m == null) continue;
                                    var owned = ledger.GetWarehouseQuantity(m.素材編號);
                                    var color = owned >= m.需求數量 ? "#27AE60" : "#EB5757";
                                    mats.Add($"<color={color}>{m.素材編號} ({owned}/{m.需求數量})</color>");
                                }
                            }
                            costStr = $"強化至 Lv {nextLevel}: <color=#F2C94C>{pathItem.升級花費.金幣} z</color> ｜ 素材: {string.Join("，", mats)}";
                        }
                    }
                }
                else
                {
                    costStr = $"當前等級: Lv {currentLevel} ｜ <color=#888888>此裝備無法強化</color>";
                }
            }

            var costLbl = AddBareText(textCol.transform, costStr, 18, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));

            // 3. 右側按鈕列
            var btnCol = new GameObject("BtnCol", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            btnCol.transform.SetParent(cell.transform, false);
            btnCol.GetComponent<LayoutElement>().preferredWidth = 160f;
            var bvg = btnCol.GetComponent<VerticalLayoutGroup>();
            bvg.spacing = 6f;
            bvg.childAlignment = TextAnchor.MiddleCenter;
            bvg.childControlWidth = true;
            bvg.childControlHeight = true;
            bvg.childForceExpandWidth = true;
            bvg.childForceExpandHeight = false;

            if (!isOwned)
            {
                bool canForge = CanCraft(item, ledger, out _);
                CreateRowButton(btnCol.transform, canForge ? "生產裝備" : "材料不足", canForge ? () => CraftEquipment(item) : null);
            }
            else
            {
                if (!isEquipped)
                {
                    CreateRowButton(btnCol.transform, "裝備此件", () => EquipEquipment(item));
                }

                var nextLevel = currentLevel + 1;
                bool hasUpgrade = _upgradeRules.TryGetValue(item.裝備編號, out var rule) && rule != null && currentLevel < rule.最高等級;
                if (hasUpgrade)
                {
                    bool canUpgrade = CanUpgrade(item, currentLevel, ledger, out _, out _);
                    var upgradeBtn = CreateRowButton(btnCol.transform, canUpgrade ? $"強化 Lv{nextLevel}" : "材料不足", canUpgrade ? () => UpgradeEquipment(item) : null);
                    
                    if (canUpgrade)
                    {
                        var btnImg = upgradeBtn.GetComponent<Image>();
                        if (btnImg != null)
                        {
                            btnImg.color = new Color(0.85f, 0.45f, 0.08f, 0.95f); // 亮橘色強化按鈕
                            var btnOut = upgradeBtn.GetComponent<Outline>();
                            if (btnOut != null) btnOut.effectColor = new Color(1f, 0.7f, 0.3f, 0.8f);
                        }
                    }
                }
            }
        }

        Text AddBareText(Transform parent, string msg, int size, TextAnchor align, Color c)
        {
            var go = new GameObject("Lbl", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            VillageGameFlow.SetSharpText(t, msg, size, c, align, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 8;
            return t;
        }

        Button CreateRowButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("ActionBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 54f);
            
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 160f;
            le.preferredHeight = 54f;
            
            var img = go.GetComponent<Image>();
            
            if (onClick != null)
            {
                img.color = new Color(0.12f, 0.48f, 0.85f, 0.95f); // 亮藍色按鈕
                var outl = go.AddComponent<Outline>();
                outl.effectColor = new Color(0.5f, 0.78f, 1f, 0.8f);
                outl.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else
            {
                img.color = new Color(0.16f, 0.18f, 0.22f, 0.7f); // 禁用灰色
                var outl = go.AddComponent<Outline>();
                outl.effectColor = new Color(0.35f, 0.38f, 0.45f, 0.5f);
                outl.effectDistance = new Vector2(1.2f, -1.2f);
            }
            
            var shad = go.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shad.effectDistance = new Vector2(2f, -2f);

            var btn = go.GetComponent<Button>();
            btn.interactable = onClick != null;
            
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            btn.colors = colors;

            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var txt = txtGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(txt, label, 20, onClick != null ? new Color(0.96f, 0.98f, 1f) : new Color(0.7f, 0.72f, 0.76f), TextAnchor.MiddleCenter, FontStyle.Bold);

            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
            return btn;
        }

        void CreateLoadoutRow(int slotIndex, string equipId)
        {
            var row = new GameObject("Row_" + slotIndex, typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_loadoutList, false);
            
            var img = row.GetComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            
            var outl = row.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outl.effectDistance = new Vector2(1.2f, -1.2f);
            
            var shad = row.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 8, 8);
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            row.GetComponent<LayoutElement>().minHeight = 84f;

            var slotTxtGo = new GameObject("Slot", typeof(RectTransform));
            slotTxtGo.transform.SetParent(row.transform, false);
            var slotLe = slotTxtGo.AddComponent<LayoutElement>();
            slotLe.preferredWidth = 72f;
            var slotTxt = slotTxtGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(slotTxt, SlotLabels[Mathf.Clamp(slotIndex, 0, 5)], 22, new Color(0.95f, 0.79f, 0.18f), TextAnchor.MiddleCenter, FontStyle.Bold);
            
            var slotShad = slotTxtGo.AddComponent<Shadow>();
            slotShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
            slotShad.effectDistance = new Vector2(1f, -1f);

            裝備資料列 rowData = null;
            if (!string.IsNullOrEmpty(equipId))
            {
                if (slotIndex == 0) _weapons.TryGetValue(equipId, out rowData);
                else _armors.TryGetValue(equipId, out rowData);
            }

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = iconLe.preferredHeight = 68f;
            var icon = iconGo.GetComponent<Image>();
            var path = rowData != null ? rowData.圖示路徑 : null;
            if (string.IsNullOrWhiteSpace(path))
                path = rowData != null ? rowData.圖片路徑 : null;
            if (!string.IsNullOrWhiteSpace(path))
            {
                path = path.Trim();
                if (path.Contains("Equipments/"))
                {
                    path = path.Replace("Equipments/", "Equipment/");
                }
            }
            var sp = !string.IsNullOrWhiteSpace(path) ? SafeSpriteLoader.TryLoadSprite(path) : null;
            icon.sprite = sp ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
            icon.color = Color.white;
            icon.preserveAspect = true;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(row.transform, false);
            var nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1f;
            var name = nameGo.AddComponent<Text>();
            
            // 獲取並顯示當前強化等級！
            var levelStr = "";
            if (rowData != null)
            {
                var ledger = LocalHunterLedger.LoadOrCreate();
                var lv = ledger.GetEquipmentLevel(rowData.裝備編號);
                if (lv > 1) levelStr = $" <color=#F2C94C>Lv {lv}</color>";
            }

            var equipName = rowData != null ? (rowData.名稱 + levelStr) : (string.IsNullOrEmpty(equipId) ? "（未裝備）" : equipId + "（找不到資料）");
            VillageGameFlow.SetSharpText(name, equipName, 24, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            
            var nameShad = nameGo.AddComponent<Shadow>();
            nameShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
            nameShad.effectDistance = new Vector2(1.2f, -1.2f);
        }

        void CreateSkillCell(EquipmentSkillAggregator.技能顯示列 s)
        {
            var cell = new GameObject("Sk_" + s.定義?.技能編號, typeof(RectTransform), typeof(Image),
                typeof(LayoutElement));
            cell.transform.SetParent(_skillsContent, false);
            
            var img = cell.GetComponent<Image>();
            img.color = new Color(0.08f, 0.09f, 0.11f, 0.92f);
            
            var outl = cell.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, 0.12f);
            outl.effectDistance = new Vector2(1.2f, -1.2f);
            
            var shad = cell.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            cell.GetComponent<LayoutElement>().minHeight = 150f;

            var pad = new GameObject("Pad", typeof(RectTransform), typeof(VerticalLayoutGroup));
            pad.transform.SetParent(cell.transform, false);
            var pRt = pad.GetComponent<RectTransform>();
            StretchFull(pRt);
            pRt.offsetMin = new Vector2(12f, 10f);
            pRt.offsetMax = new Vector2(-12f, -10f);
            var v = pad.GetComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.childAlignment = TextAnchor.UpperLeft;

            var titleRow = new GameObject("TitleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            titleRow.transform.SetParent(pad.transform, false);
            var tr = titleRow.GetComponent<RectTransform>();
            tr.sizeDelta = new Vector2(320f, 32f);
            var th = titleRow.GetComponent<HorizontalLayoutGroup>();
            th.childAlignment = TextAnchor.MiddleLeft;
            th.spacing = 8f;

            var nmGo = new GameObject("Nm", typeof(RectTransform));
            nmGo.transform.SetParent(titleRow.transform, false);
            var nmLe = nmGo.AddComponent<LayoutElement>();
            nmLe.flexibleWidth = 1f;
            var nm = nmGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(nm, s.定義?.名稱 ?? "", 24, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            
            var nmShad = nmGo.AddComponent<Shadow>();
            nmShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
            nmShad.effectDistance = new Vector2(1.2f, -1.2f);

            var lvGo = new GameObject("Lv", typeof(RectTransform));
            lvGo.transform.SetParent(titleRow.transform, false);
            lvGo.AddComponent<LayoutElement>().preferredWidth = 48f;
            var lv = lvGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(lv, "Lv " + s.顯示等級.ToString(), 24, new Color(0.95f, 0.79f, 0.18f), TextAnchor.MiddleRight, FontStyle.Bold);
            
            var lvShad = lvGo.AddComponent<Shadow>();
            lvShad.effectColor = new Color(0f, 0f, 0f, 0.85f);
            lvShad.effectDistance = new Vector2(1.2f, -1.2f);

            CreateLevelBar(pad.transform, s.顯示等級, s.定義?.最高等級 ?? 5);

            var descGo = new GameObject("Desc", typeof(RectTransform));
            descGo.transform.SetParent(pad.transform, false);
            var dLe = descGo.AddComponent<LayoutElement>();
            dLe.preferredHeight = 56f;
            var desc = descGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(desc, s.效果說明文字 ?? "", 18, new Color(0.78f, 0.82f, 0.88f), TextAnchor.UpperLeft, FontStyle.Normal);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void CreateLevelBar(Transform parent, int level, int maxLevel)
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(parent, false);
            bar.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 14f);
            var hg = bar.GetComponent<HorizontalLayoutGroup>();
            hg.spacing = 4f;
            hg.childAlignment = TextAnchor.MiddleLeft;

            var cap = Mathf.Max(1, maxLevel);
            var filled = Mathf.Clamp(level, 0, cap);
            var segments = Mathf.Min(5, cap);
            var visualFilled = Mathf.CeilToInt((float)filled / cap * segments);

            for (var i = 0; i < segments; i++)
            {
                var seg = new GameObject("Seg" + i, typeof(RectTransform), typeof(Image));
                seg.transform.SetParent(bar.transform, false);
                var sle = seg.AddComponent<LayoutElement>();
                sle.preferredWidth = 40f;
                sle.preferredHeight = 12f;
                var img = seg.GetComponent<Image>();
                img.color = i < visualFilled
                    ? new Color(0.95f, 0.82f, 0.2f, 1f)
                    : new Color(0.22f, 0.24f, 0.28f, 1f);
            }
        }

        static Button CreateHeaderButton(
            Transform parent,
            string label,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            
            var outl = go.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outl.effectDistance = new Vector2(1.2f, -1.2f);
            
            var shad = go.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = colors;

            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            
            var txt = txtGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(txt, label, 24, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            
            var textShad = txtGo.AddComponent<Shadow>();
            textShad.effectColor = new Color(0f, 0f, 0f, 0.75f);
            textShad.effectDistance = new Vector2(1.2f, -1.2f);
            
            return btn;
        }

        void AddTabButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Tab_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 54f);
            
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            
            var outl = go.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outl.effectDistance = new Vector2(1.2f, -1.2f);
            
            var shad = go.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            var btn = go.GetComponent<Button>();
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 200f;
            le.preferredWidth = 240f;
            
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = colors;

            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var txt = txtGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(txt, label, 24, new Color(0.96f, 0.97f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            
            btn.onClick.AddListener(onClick);
        }

        Button CreatePagerButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("PagerBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140f, 44f);
            
            var img = go.GetComponent<Image>();
            img.color = onClick != null ? new Color(0.12f, 0.13f, 0.16f, 0.95f) : new Color(0.16f, 0.18f, 0.22f, 0.4f);
            
            var outl = go.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, onClick != null ? 0.15f : 0.05f);
            outl.effectDistance = new Vector2(1f, -1f);

            var btn = go.GetComponent<Button>();
            btn.interactable = onClick != null;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var txt = txtGo.AddComponent<Text>();
            VillageGameFlow.SetSharpText(txt, label, 18, onClick != null ? Color.white : new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold);
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            return btn;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }
    }
}
