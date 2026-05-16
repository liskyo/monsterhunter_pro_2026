using System;
using System.Collections;
using System.Collections.Generic;
using MonsterHunter.Combat;
using MonsterHunter.Core;
using MonsterHunter.Data;
using MonsterHunter.DataModels;
using MonsterHunter.Network;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 村莊商店：道具（materials）、染色球（paintballs）、寵物（pets）。
    /// 金幣：已登入走 Supabase <c>profiles.zeni</c>；否則 <see cref="LocalHunterLedger.Zenny"/>。
    /// 寵物／道具入庫：<c>warehouse_items</c> 與本機 ledger 併記，供戰鬥隨機寵物使用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillageShopScreen : MonoBehaviour
    {
        enum ShopTab
        {
            Materials,
            Paintballs,
            Pets,
        }

        const int MaxMaterialBuy = 99;

        Action _onReturnHub;
        SupabaseService _supabase;

        ShopTab _tab = ShopTab.Materials;

        readonly Dictionary<string, int> _warehouseQty =
            new Dictionary<string, int>(StringComparer.Ordinal);

        long _displayZenny;
        string _statusLine = "";

        Font _font;
        Text _zennyLabel;
        Text _statusText;
        RectTransform _listContent;

        素材資料列[] _materialRows;
        染色球資料列[] _paintRows;
        寵物資料列[] _petRows;

        public void Initialize(Action onReturnHub)
        {
            _onReturnHub = onReturnHub;
            BuildUi();
        }

        void OnEnable()
        {
            _supabase = FindFirstObjectByType<SupabaseService>();
            StartCoroutine(RefreshAll());
        }

        IEnumerator RefreshAll()
        {
            LoadDesignRows();
            _statusLine = "";
            if (_supabase != null && AuthSession.IsSignedIn)
            {
                string e0 = null;
                玩家狀態本機範例 st = null;
                yield return _supabase.LoadPlayerProfile(err => e0 = err, v => st = v);
                if (e0 != null)
                {
                    _statusLine = e0;
                    _displayZenny = LocalHunterLedger.LoadOrCreate().Zenny;
                }
                else if (st != null)
                    _displayZenny = st.zenny;

                string e1 = null;
                yield return _supabase.LoadWarehouseItemDictionary(
                    err => e1 = err,
                    d =>
                    {
                        _warehouseQty.Clear();
                        if (d != null)
                        {
                            foreach (var kv in d)
                                _warehouseQty[kv.Key] = kv.Value;
                        }

                    });
                if (e1 != null)
                {
                    _statusLine = e1;
                    CopyLedgerWarehouseToCache();
                }
            }
            else
            {
                var ledger = LocalHunterLedger.LoadOrCreate();
                _displayZenny = ledger.Zenny;
                CopyLedgerWarehouseToCache();
            }

            RebuildProductList();
            RefreshChrome();
        }

        void CopyLedgerWarehouseToCache()
        {
            _warehouseQty.Clear();
            var ledger = LocalHunterLedger.LoadOrCreate();
            if (ledger.Warehouse == null) return;
            foreach (var kv in ledger.Warehouse)
                _warehouseQty[kv.Key] = kv.Value;
        }

        void LoadDesignRows()
        {
            if (DesignDataReader.TryLoadDesignDataText(out var mj, "04_Items", "materials.json"))
                _materialRows = DesignDataJsonArrayUtility.Parse素材資料(mj);
            else
                _materialRows = Array.Empty<素材資料列>();

            if (DesignDataReader.TryLoadDesignDataText(out var pj, "04_Items", "paintballs.json"))
                try
                {
                    _paintRows = JsonConvert.DeserializeObject<染色球資料列[]>(pj) ?? Array.Empty<染色球資料列>();
                }
                catch
                {
                    _paintRows = Array.Empty<染色球資料列>();
                }
            else
                _paintRows = Array.Empty<染色球資料列>();

            _petRows = OwnedPetBattleBuffs.LoadAllPets();
        }

        bool IsOwned(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return _warehouseQty.TryGetValue(itemId, out var q) && q > 0;
        }

        int MaterialPrice(素材資料列 r)
        {
            if (r == null) return 0;
            if (r.購買價格 > 0) return r.購買價格;
            if (r.出售價格 > 0) return Mathf.Max(1, r.出售價格 * 2);
            return 100;
        }

        int PaintPrice(染色球資料列 r)
        {
            if (r == null) return 0;
            if (r.購買價格 > 0) return r.購買價格;
            return 500;
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
            bgImg.sprite = SafeSpriteLoader.TryLoadSprite("Assets/UI/Backgrounds/Village/商店.png");
            bgImg.type = bgImg.sprite != null ? Image.Type.Simple : Image.Type.SolidColor;
            bgImg.color = bgImg.sprite != null ? Color.white : new Color(0.06f, 0.07f, 0.1f, 0.97f);
            bgImg.raycastTarget = true;

            var header = AddChildPanel(root, "Header", 180f, TextAnchor.UpperCenter);
            var title = AddText(header, "Title", "商店", 40, TextAnchor.UpperCenter, new Vector2(0f, -24f));
            title.rectTransform.sizeDelta = new Vector2(900f, 72f);

            _zennyLabel = AddText(header, "Zenny", "金幣：--", 30, TextAnchor.UpperRight, new Vector2(-36f, -28f));
            _zennyLabel.rectTransform.sizeDelta = new Vector2(520f, 48f);
            _zennyLabel.color = new Color(1f, 0.92f, 0.55f);

            _statusText = AddText(header, "Status", "", 22, TextAnchor.LowerLeft, new Vector2(36f, 12f));
            _statusText.rectTransform.sizeDelta = new Vector2(980f, 56f);
            _statusText.color = new Color(1f, 0.65f, 0.55f);

            var tabBar = AddChildPanel(root, "Tabs", 100f, TextAnchor.UpperCenter);
            var tabBarHg = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabBarHg.childAlignment = TextAnchor.MiddleCenter;
            tabBarHg.spacing = 12f;
            tabBarHg.padding = new RectOffset(8, 8, 4, 4);
            var tabRt = tabBar.GetComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0f, 1f);
            tabRt.anchorMax = new Vector2(1f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.offsetMin = new Vector2(24f, -280f);
            tabRt.offsetMax = new Vector2(-24f, -200f);

            AddTabButton(tabBar.transform, "道具", () => SetTab(ShopTab.Materials));
            AddTabButton(tabBar.transform, "染色球", () => SetTab(ShopTab.Paintballs));
            AddTabButton(tabBar.transform, "寵物", () => SetTab(ShopTab.Pets));

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(root, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.offsetMin = new Vector2(24f, 120f);
            scrollRt.offsetMax = new Vector2(-24f, -300f);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0.12f, 0.13f, 0.16f, 0.55f);

            var viewport = AddChildImage(scrollRt, "Viewport", new Color(0f, 0f, 0f, 0.02f));
            StretchFull(viewport.rectTransform);
            viewport.rectTransform.offsetMin = Vector2.zero;
            viewport.rectTransform.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _listContent = content.GetComponent<RectTransform>();
            StretchFull(_listContent);
            var v = content.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.spacing = 10f;
            v.padding = new RectOffset(8, 8, 8, 8);
            v.childControlHeight = true;
            v.childForceExpandHeight = false;
            var fit = content.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = viewport.rectTransform;
            sr.content = _listContent;
            sr.horizontal = false;
            sr.vertical = true;

            var foot = AddChildPanel(root, "Footer", 100f, TextAnchor.LowerCenter);
            var footRt = foot.GetComponent<RectTransform>();
            footRt.anchorMin = new Vector2(0.5f, 0f);
            footRt.anchorMax = new Vector2(0.5f, 0f);
            footRt.pivot = new Vector2(0.5f, 0f);
            footRt.anchoredPosition = new Vector2(0f, 28f);
            footRt.sizeDelta = new Vector2(1020f, 88f);
            var footHg = foot.gameObject.AddComponent<HorizontalLayoutGroup>();
            footHg.childAlignment = TextAnchor.MiddleCenter;
            footHg.spacing = 20f;
            footHg.padding = new RectOffset(12, 12, 8, 8);
            footHg.childForceExpandHeight = true;
            footHg.childForceExpandWidth = true;

            CreateFooterSplitButton(foot.transform, "返回村莊", () => _onReturnHub?.Invoke());
        }

        void CreateFooterSplitButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("FooterBtn_" + label, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 240f;
            le.preferredHeight = 72f;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.85f, 0.45f, 0.18f, 1f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var txt = txtGo.AddComponent<Text>();
            txt.font = _font;
            txt.text = label;
            txt.fontSize = 28;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }

        void CreateFooterSplitButton(Transform parent, string label, Action onClick)
        {
            _tab = t;
            RebuildProductList();
        }

        void RefreshChrome()
        {
            if (_zennyLabel != null)
                _zennyLabel.text = "金幣：" + _displayZenny;
            if (_statusText != null)
                _statusText.text = _statusLine ?? "";
        }

        void RebuildProductList()
        {
            if (_listContent == null) return;
            for (var i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            switch (_tab)
            {
                case ShopTab.Materials:
                    if (_materialRows != null)
                    {
                        foreach (var r in _materialRows)
                        {
                            if (r == null) continue;
                            var price = MaterialPrice(r);
                            var owned = GetWarehouseQty(r.素材編號);
                            CreateRow(r.素材編號, r.名稱, r.圖片路徑, price,
                                canBuy: owned < MaxMaterialBuy,
                                extra: $"持有 {owned}",
                                () => StartCoroutine(CoBuyMaterial(r.素材編號, r.名稱, price)));
                        }
                    }

                    break;
                case ShopTab.Paintballs:
                    if (_paintRows != null)
                    {
                        foreach (var r in _paintRows)
                        {
                            if (r == null) continue;
                            var price = PaintPrice(r);
                            var owned = GetWarehouseQty(r.道具編號);
                            CreateRow(r.道具編號, r.名稱, r.圖片路徑, price,
                                canBuy: owned < MaxMaterialBuy,
                                extra: $"持有 {owned}",
                                () => StartCoroutine(CoBuyMaterial(r.道具編號, r.名稱, price)));
                        }
                    }

                    break;
                case ShopTab.Pets:
                    if (_petRows != null)
                    {
                        foreach (var r in _petRows)
                        {
                            if (r == null) continue;
                            var price = OwnedPetBattleBuffs.EffectivePetPrice(r);
                            var owned = IsOwned(r.寵物編號);
                            CreateRow(r.寵物編號, $"{r.名稱}（{r.種類}）", r.圖片路徑, price,
                                canBuy: !owned,
                                extra: owned ? "已取得" : "",
                                () => StartCoroutine(CoBuyPet(r.寵物編號, r.名稱, price)));
                        }
                    }

                    break;
            }
        }

        int GetWarehouseQty(string id)
        {
            return !string.IsNullOrEmpty(id) && _warehouseQty.TryGetValue(id, out var q) ? q : 0;
        }

        void CreateRow(
            string id,
            string displayName,
            string imagePath,
            int price,
            bool canBuy,
            string extra,
            Action onBuy)
        {
            var row = new GameObject("Row_" + id, typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_listContent, false);
            var img = row.GetComponent<Image>();
            img.color = new Color(0.18f, 0.2f, 0.24f, 0.92f);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 12, 12);
            h.spacing = 14f;
            h.childAlignment = TextAnchor.MiddleLeft;
            var le = row.GetComponent<LayoutElement>();
            le.minHeight = 100f;
            le.preferredHeight = 100f;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(88f, 88f);
            var icon = iconGo.GetComponent<Image>();
            var sp = SafeSpriteLoader.TryLoadSprite(imagePath);
            icon.sprite = sp ?? PlaceholderSpriteFactory.GetSharedPlaceholder();
            icon.color = Color.white;

            var textCol = new GameObject("Texts", typeof(RectTransform), typeof(VerticalLayoutGroup));
            textCol.transform.SetParent(row.transform, false);
            var textRt = textCol.GetComponent<RectTransform>();
            var ve = textCol.AddComponent<LayoutElement>();
            ve.flexibleWidth = 1f;
            var vg = textCol.GetComponent<VerticalLayoutGroup>();
            vg.childAlignment = TextAnchor.MiddleLeft;
            vg.spacing = 4f;

            AddBareText(textCol.transform, displayName, 26, TextAnchor.MiddleLeft,
                new Color(0.96f, 0.97f, 1f));
            var sub = string.IsNullOrEmpty(extra) ? $"{id} ｜ ${price}" : $"{id} ｜ ${price} ｜ {extra}";
            AddBareText(textCol.transform, sub, 20, TextAnchor.MiddleLeft,
                new Color(0.7f, 0.75f, 0.82f));

            var btn = CreateRowButton(row.transform, canBuy ? "購買" : (IsOwned(id) && id.StartsWith("PET_", StringComparison.Ordinal) ? "已取得" : "上限"),
                canBuy ? onBuy : null);
        }

        Text AddBareText(Transform parent, string msg, int size, TextAnchor align, Color c)
        {
            var go = new GameObject("Lbl", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = msg;
            t.fontSize = size;
            t.alignment = align;
            t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 8;
            return t;
        }

        Button CreateRowButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("BuyBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 64f);
            var img = go.GetComponent<Image>();
            img.color = onClick != null ? new Color(0.25f, 0.55f, 0.95f, 0.95f) : new Color(0.35f, 0.36f, 0.4f, 0.7f);
            var btn = go.GetComponent<Button>();
            btn.interactable = onClick != null;
            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var txt = txtGo.AddComponent<Text>();
            txt.font = _font;
            txt.text = label;
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
            return btn;
        }

        IEnumerator CoBuyMaterial(string itemId, string displayName, int price)
        {
            if (string.IsNullOrEmpty(itemId) || price <= 0) yield break;

            _statusLine = "處理中…";
            RefreshChrome();

            if (_supabase != null && AuthSession.IsSignedIn)
            {
                玩家狀態本機範例 st = null;
                string e0 = null;
                yield return _supabase.LoadPlayerProfile(e => e0 = e, v => st = v);
                if (e0 != null || st == null)
                {
                    _statusLine = e0 ?? "讀取檔案失敗";
                    RefreshChrome();
                    yield break;
                }

                if (st.zenny < price)
                {
                    _statusLine = "金幣不足";
                    RefreshChrome();
                    yield break;
                }

                var nextZeni = (long)st.zenny - price;
                string e1 = null;
                yield return _supabase.PatchProfileZeni(nextZeni, e => e1 = e, () => { });
                if (e1 != null)
                {
                    _statusLine = e1;
                    RefreshChrome();
                    yield break;
                }

                string e2 = null;
                yield return _supabase.UpdateWarehouse(itemId, 1, e => e2 = e, () => { });
                if (e2 != null)
                {
                    _statusLine = e2;
                    RefreshChrome();
                    yield break;
                }

                MirrorLedgerAfterCloudBuy(itemId, 1, nextZeni);
                _displayZenny = nextZeni;
                _warehouseQty[itemId] = GetWarehouseQty(itemId) + 1;
                _statusLine = $"已購買：{displayName}";
            }
            else
            {
                var ledger = LocalHunterLedger.LoadOrCreate();
                if (ledger.Zenny < price)
                {
                    _statusLine = "金幣不足";
                    RefreshChrome();
                    yield break;
                }

                ledger.Zenny -= price;
                if (ledger.Warehouse == null)
                    ledger.Warehouse = new Dictionary<string, int>(StringComparer.Ordinal);
                if (!ledger.Warehouse.TryGetValue(itemId, out var n))
                    n = 0;
                ledger.Warehouse[itemId] = Mathf.Min(MaxMaterialBuy, n + 1);
                ledger.Save();
                _displayZenny = ledger.Zenny;
                CopyLedgerWarehouseToCache();
                _statusLine = $"已購買：{displayName}";
            }

            RebuildProductList();
            RefreshChrome();
        }

        IEnumerator CoBuyPet(string petId, string displayName, int price)
        {
            if (string.IsNullOrEmpty(petId) || price <= 0) yield break;

            if (IsOwned(petId))
            {
                _statusLine = "已擁有此寵物";
                RefreshChrome();
                yield break;
            }

            _statusLine = "處理中…";
            RefreshChrome();

            if (_supabase != null && AuthSession.IsSignedIn)
            {
                玩家狀態本機範例 st = null;
                string e0 = null;
                yield return _supabase.LoadPlayerProfile(e => e0 = e, v => st = v);
                if (e0 != null || st == null)
                {
                    _statusLine = e0 ?? "讀取檔案失敗";
                    RefreshChrome();
                    yield break;
                }

                if (st.zenny < price)
                {
                    _statusLine = "金幣不足";
                    RefreshChrome();
                    yield break;
                }

                var nextZeni = (long)st.zenny - price;
                string e1 = null;
                yield return _supabase.PatchProfileZeni(nextZeni, e => e1 = e, () => { });
                if (e1 != null)
                {
                    _statusLine = e1;
                    RefreshChrome();
                    yield break;
                }

                string e2 = null;
                yield return _supabase.UpdateWarehouse(petId, 1, e => e2 = e, () => { });
                if (e2 != null)
                {
                    _statusLine = e2;
                    RefreshChrome();
                    yield break;
                }

                MirrorLedgerAfterCloudBuy(petId, 1, nextZeni);
                _displayZenny = nextZeni;
                _warehouseQty[petId] = GetWarehouseQty(petId) + 1;
                _statusLine = $"已取得寵物：{displayName}";
            }
            else
            {
                var ledger = LocalHunterLedger.LoadOrCreate();
                if (ledger.Zenny < price)
                {
                    _statusLine = "金幣不足";
                    RefreshChrome();
                    yield break;
                }

                ledger.Zenny -= price;
                ledger.Warehouse ??= new Dictionary<string, int>(StringComparer.Ordinal);
                ledger.Warehouse[petId] = 1;
                ledger.Save();
                _displayZenny = ledger.Zenny;
                CopyLedgerWarehouseToCache();
                _statusLine = $"已取得寵物：{displayName}";
            }

            RebuildProductList();
            RefreshChrome();
        }

        static void MirrorLedgerAfterCloudBuy(string itemId, int qty, long zennyAfter)
        {
            var ledger = LocalHunterLedger.LoadOrCreate();
            ledger.Zenny = zennyAfter;
            ledger.Warehouse ??= new Dictionary<string, int>(StringComparer.Ordinal);
            if (!ledger.Warehouse.TryGetValue(itemId, out var n))
                n = 0;
            ledger.Warehouse[itemId] = Mathf.Max(0, n + qty);
            ledger.Save();
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

        static Image AddChildImage(RectTransform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            StretchFull(go.GetComponent<RectTransform>());
            return img;
        }

        static RectTransform AddChildPanel(RectTransform parent, string name, float height, TextAnchor _)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);
            return rt;
        }

        Text AddText(RectTransform parent, string name, string msg, int size, TextAnchor align, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(800f, 64f);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = msg;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            return t;
        }

        void AddTabButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("Tab_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 64f);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.2f, 0.22f, 0.28f, 0.95f);
            var btn = go.GetComponent<Button>();
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 200f;
            le.preferredWidth = 240f;
            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var txt = txtGo.AddComponent<Text>();
            txt.font = _font;
            txt.text = label;
            txt.fontSize = 26;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            btn.onClick.AddListener(() => onClick());
        }

