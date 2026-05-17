using System;
using System.Collections.Generic;
using MonsterHunter.Combat;
using MonsterHunter.Data;
using MonsterHunter.DataModels;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 加工屋：裝備摘要（1 武器＋5 防具）與依 <c>skills.json</c> 聚合的技能列表；背景為 <c>Assets/UI/Backgrounds/Village/加工屋_背景.png</c>。
    /// 資料來源 <see cref="LocalHunterLedger"/>＋<c>equipment.json</c>／<c>armor.json</c>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkshopEquipmentScreen : MonoBehaviour
    {
        const string BackgroundPath = "Assets/UI/Backgrounds/Village/加工屋_背景.png";

        static readonly string[] SlotLabels = { "武器", "頭", "身", "手", "腰", "腳" };

        System.Action _onClose;

        Font _font;
        RectTransform _loadoutList;
        RectTransform _skillsContent;
        Dictionary<string, 裝備資料列> _weapons = new Dictionary<string, 裝備資料列>(System.StringComparer.Ordinal);
        Dictionary<string, 裝備資料列> _armors = new Dictionary<string, 裝備資料列>(System.StringComparer.Ordinal);
        技能資料列[] _skills = System.Array.Empty<技能資料列>();

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
            titleRt.anchoredPosition = new Vector2(0f, -28f);
            titleRt.sizeDelta = new Vector2(900f, 52f);
            var title = titleGo.AddComponent<Text>();
            title.font = _font;
            title.text = "裝備與技能";
            title.fontSize = 36;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;

            var closeBtn = CreateHeaderButton(header.transform, "返回村莊",
                new Vector2(0f, -90f), new Vector2(260f, 54f), () =>
                {
                    gameObject.SetActive(false);
                    _onClose?.Invoke();
                });

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(root, false);
            var bRt = body.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.offsetMin = new Vector2(24f, 24f);
            bRt.offsetMax = new Vector2(-24f, -150f);

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
        }

        void OnEnable()
        {
            _weapons = EquipmentSkillAggregator.LoadEquipmentDictionary();
            _armors = EquipmentSkillAggregator.LoadArmorDictionary();
            _skills = EquipmentSkillAggregator.LoadSkillsOrEmpty();
            Refresh();
        }

        void Refresh()
        {
            if (_loadoutList == null) return;

            for (var i = _loadoutList.childCount - 1; i >= 0; i--)
                Destroy(_loadoutList.GetChild(i).gameObject);

            for (var i = _skillsContent.childCount - 1; i >= 0; i--)
                Destroy(_skillsContent.GetChild(i).gameObject);

            var ledger = LocalHunterLedger.LoadOrCreate();
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

        void CreateLoadoutRow(int slotIndex, string equipId)
        {
            var row = new GameObject("Row_" + slotIndex, typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_loadoutList, false);
            row.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 0.9f);
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
            slotTxt.font = _font;
            slotTxt.fontSize = 22;
            slotTxt.fontStyle = FontStyle.Bold;
            slotTxt.color = new Color(0.85f, 0.88f, 0.95f);
            slotTxt.alignment = TextAnchor.MiddleCenter;
            slotTxt.text = SlotLabels[Mathf.Clamp(slotIndex, 0, 5)];

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
            var sp = !string.IsNullOrWhiteSpace(path) ? SafeSpriteLoader.TryLoadSprite(path.Trim()) : null;
            icon.sprite = sp ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
            icon.color = Color.white;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(row.transform, false);
            var nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1f;
            var name = nameGo.AddComponent<Text>();
            name.font = _font;
            name.fontSize = 24;
            name.color = Color.white;
            name.alignment = TextAnchor.MiddleLeft;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.text = rowData != null
                ? rowData.名稱
                : (string.IsNullOrEmpty(equipId) ? "（未裝備）" : equipId + "（找不到資料）");
        }

        void CreateSkillCell(EquipmentSkillAggregator.技能顯示列 s)
        {
            var cell = new GameObject("Sk_" + s.定義?.技能編號, typeof(RectTransform), typeof(Image),
                typeof(LayoutElement));
            cell.transform.SetParent(_skillsContent, false);
            cell.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.92f);
            cell.GetComponent<LayoutElement>().minHeight = 150f;

            var pad = new GameObject("Pad", typeof(RectTransform), typeof(VerticalLayoutGroup));
            pad.transform.SetParent(cell.transform, false);
            var pRt = pad.GetComponent<RectTransform>();
            StretchFull(pRt);
            pRt.offsetMin = new Vector2(10f, 8f);
            pRt.offsetMax = new Vector2(-10f, -8f);
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
            nm.font = _font;
            nm.fontSize = 24;
            nm.fontStyle = FontStyle.Bold;
            nm.color = Color.white;
            nm.alignment = TextAnchor.MiddleLeft;
            nm.text = s.定義?.名稱 ?? "";

            var lvGo = new GameObject("Lv", typeof(RectTransform));
            lvGo.transform.SetParent(titleRow.transform, false);
            lvGo.AddComponent<LayoutElement>().preferredWidth = 48f;
            var lv = lvGo.AddComponent<Text>();
            lv.font = _font;
            lv.fontSize = 24;
            lv.fontStyle = FontStyle.Bold;
            lv.color = new Color(1f, 0.92f, 0.35f);
            lv.alignment = TextAnchor.MiddleRight;
            lv.text = s.顯示等級.ToString();

            CreateLevelBar(pad.transform, s.顯示等級, s.定義?.最高等級 ?? 5);

            var descGo = new GameObject("Desc", typeof(RectTransform));
            descGo.transform.SetParent(pad.transform, false);
            var dLe = descGo.AddComponent<LayoutElement>();
            dLe.preferredHeight = 56f;
            var desc = descGo.AddComponent<Text>();
            desc.font = _font;
            desc.fontSize = 18;
            desc.color = new Color(0.72f, 0.75f, 0.8f);
            desc.alignment = TextAnchor.UpperLeft;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;
            desc.text = s.效果說明文字 ?? "";
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
            img.color = new Color(0.3f, 0.35f, 0.42f, 1f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = label;
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
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
