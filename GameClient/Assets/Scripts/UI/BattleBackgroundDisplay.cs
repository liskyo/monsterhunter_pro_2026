using System.Collections.Generic;
using MonsterHunter.Combat;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MonsterHunter.UI
{
    /// <summary>
    /// 依任務 <see cref="任務資料列.地圖"/> 載入戰鬥遠景圖（預設 <c>Assets/UI/Backgrounds/Battle/{地圖}_背景.png</c>），
    /// 或可選依「星級＋構圖序」載入：<c>CS{01~10}R{01~04}_背景.png</c>（詳見 TryApplyCombatStarBackdrop）。
    /// 與 <see cref="BattlePortraitLayout"/> 並用：攝影機僅佔螢幕下方時，背景仍填滿該視錐對應的世界範圍。
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class BattleBackgroundDisplay : MonoBehaviour
    {
        public enum BattleBackgroundFitMode
        {
            /// <summary>
            /// 等比 <c>Max(sx,sy)</c> 鋪滿遠景（維持原圖寬高比、不拉長 Sprite）。
            /// 超寬圖可另開 <see cref="_slidePanoramaUWithCameraX"/>，只平移主紋路 U（貼圖 Wrap 建議 Repeat），走位時仍能橫向閱讀長圖。
            /// </summary>
            CoverPortraitPanorama,
            /// <summary>寬度對齊視野（上下常留黑邊）；僅特殊構圖使用。</summary>
            FitFullWidthLetterbox,
            /// <summary><c>Min(sx,sy)</c>：整張入鏡、可能四面留邊。</summary>
            ContainWholeSprite
        }

        [SerializeField] SpriteRenderer _spriteRenderer;
        [SerializeField] Camera _camera;

        /// <summary>供 HUD／除錯顯示本場載入成功的遠景說明（地圖名或星級輪播標籤）。</summary>
        public string LastHudCaption { get; private set; } = "";

        [Tooltip("相對於 Assets/ 的路徑，需含副檔名；{0} 為地圖名（與 quests.json「地圖」一致）。")]
        [SerializeField] string _pathFormat = "UI/Backgrounds/Battle/{0}_背景.png";

        [Tooltip("{0}=星級1~10（兩位數）；{1}=構圖1~4（兩位數），預設：CS08R03_背景.png")]
        [SerializeField] string _combatStarPathFormat = "UI/Backgrounds/Battle/CS{0:00}R{1:00}_背景.png";

        [Tooltip("戰鬥總管未注入時，仍至少覆蓋可視範圍；大於 1 時多留邊（與 combat_tuning 戰場倍率對齊）。")]
        [SerializeField] Vector2 _playfieldCoverageXY = new Vector2(1f, 1f);

        [Tooltip("2D 時將背景畫在較低順序，避免遮住角色。")]
        [SerializeField] int _sortingOrder = -100;

        [Tooltip("直立＋超寬全景請用 CoverPortraitPanorama（全螢幕＋可橫向滑）。FitFullWidthLetterbox 易變成上下黑邊＋中間細條。")]
        [SerializeField] BattleBackgroundFitMode _fitMode = BattleBackgroundFitMode.CoverPortraitPanorama;

        [Tooltip("開啟後遠景固定在「世界錨點」，遠景鏡頭移動時才會掃過貼圖；若關閉則每幀貼齊鏡頭中心，走位時畫面上永遠是同一段構圖。")]
        [SerializeField] bool _useWorldFixedBackgroundPosition = true;
        [Tooltip("UseWorldFixed 時 Sprite 在世界座標的置中點；建議維持 0 對齊戰場中心。")]
        [SerializeField] Vector3 _worldBackgroundAnchor;

        [Header("全景（等比＋橫向閱讀）")]
        [Tooltip("CoverPortraitPanorama：維持等比時，依主戰鬥鏡頭 X 平移主紋路 U（不拉長貼圖）；建議貼圖 Wrap = Repeat。")]
        [SerializeField] bool _slidePanoramaUWithCameraX = true;
        [Tooltip("主鏡頭每移 1 世界單位時，主紋路 U 偏移量；約 0.018～0.035。與 Enable Material Uv Pan 互斥。")]
        [SerializeField] float _panoramaUvSlidePerWorldUnitX = 0.026f;

        [Header("遠景捲動（選用；Sprite 套用 _MainTex_ST 在部份管線會失真，預設關閉）")]
        [Tooltip("開啟才用 MaterialPropertyBlock 滑 UV（須 Repeat）；一般請靠走位＋鏡頭／視差。")]
        [SerializeField] bool _enableMaterialUvPan = false;
        [Tooltip("主戰鬥鏡頭每移 1 世界單位時的主紋路 U 偏移量。")]
        [SerializeField] float _uvPanPerWorldUnitX;
        [Tooltip("同上（垂直）。")]
        [SerializeField] float _uvPanPerWorldUnitY;

        /// <summary>Unity 對主紋路縮放／偏移：(tiling.xy, offset.xy)。</summary>
        static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        MaterialPropertyBlock _mpb;
        Vector4 _materialMainTexStBase = new Vector4(1f, 1f, 0f, 0f);
        bool _materialMainTexStCaptured;
        bool _uvMpbApplied;

        [Header("除錯：開場自動依任務載入")]
        [SerializeField] bool _applyOnStartFromQuest;
        [SerializeField] TextAsset _questsJson;
        [SerializeField] string _任務編號 = "QST_001";

        void Awake()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null) _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sortingOrder = _sortingOrder;
        }

        void Start()
        {
            var pending = HuntSessionContext.PendingQuest;
            if (pending != null)
            {
                HuntSessionContext.PendingQuest = null;
                ApplyFromQuest(pending);
                return;
            }

            if (_applyOnStartFromQuest && _questsJson != null && !string.IsNullOrWhiteSpace(_任務編號))
            {
                var row = FindQuestInAsset(_任務編號);
                if (row != null) ApplyFromQuest(row);
            }
        }

        void LateUpdate()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;
            FitSpriteToOrthographicCamera();

            var wantManualUv = _enableMaterialUvPan &&
                          (_uvPanPerWorldUnitX > 1e-5f || _uvPanPerWorldUnitY > 1e-5f);
            var wantPanoramaSlide = !wantManualUv &&
                                     _fitMode == BattleBackgroundFitMode.CoverPortraitPanorama &&
                                     _slidePanoramaUWithCameraX &&
                                     _panoramaUvSlidePerWorldUnitX > 1e-5f;

            if (wantManualUv)
            {
                ApplyUvPanFromBattleCamera();
                _uvMpbApplied = true;
            }
            else if (wantPanoramaSlide)
            {
                ApplyPanoramaUvSlideXOnly();
                _uvMpbApplied = true;
            }
            else if (_uvMpbApplied)
            {
                _spriteRenderer.SetPropertyBlock(null);
                _uvMpbApplied = false;
            }
        }

        void ApplyUvPanFromBattleCamera()
        {
            if (!_enableMaterialUvPan ||
                (_uvPanPerWorldUnitX <= 1e-5f && _uvPanPerWorldUnitY <= 1e-5f))
                return;

            var main = Camera.main;
            if (main == null) return;

            if (!_materialMainTexStCaptured)
                CaptureMaterialMainTexStBase();

            var ox = -main.transform.position.x * _uvPanPerWorldUnitX;
            var oy = -main.transform.position.y * _uvPanPerWorldUnitY;

            _mpb ??= new MaterialPropertyBlock();
            _spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(MainTexStId, new Vector4(
                _materialMainTexStBase.x,
                _materialMainTexStBase.y,
                _materialMainTexStBase.z + ox,
                _materialMainTexStBase.w + oy));
            _spriteRenderer.SetPropertyBlock(_mpb);
        }

        void ApplyPanoramaUvSlideXOnly()
        {
            var main = Camera.main;
            if (main == null) return;

            if (!_materialMainTexStCaptured)
                CaptureMaterialMainTexStBase();

            var ox = -main.transform.position.x * _panoramaUvSlidePerWorldUnitX;

            _mpb ??= new MaterialPropertyBlock();
            _spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(MainTexStId, new Vector4(
                _materialMainTexStBase.x,
                _materialMainTexStBase.y,
                _materialMainTexStBase.z + ox,
                _materialMainTexStBase.w));
            _spriteRenderer.SetPropertyBlock(_mpb);
        }

        void CaptureMaterialMainTexStBase()
        {
            _materialMainTexStCaptured = true;
            var m = _spriteRenderer.sharedMaterial;
            if (m != null && m.HasProperty(MainTexStId))
                _materialMainTexStBase = m.GetVector(MainTexStId);
            else
                _materialMainTexStBase = new Vector4(1f, 1f, 0f, 0f);
        }

        /// <summary>由 <see cref="BattleCombatManager"/> 依 combat_tuning 設定，讓遠景涵蓋略大於鏡頭的可走區。</summary>
        public void SetPlayfieldCoverage(float horizontalMul, float verticalMul)
        {
            _playfieldCoverageXY = new Vector2(
                Mathf.Max(1f, horizontalMul),
                Mathf.Max(1f, verticalMul));
        }

        /// <summary>由任務列載入背景。</summary>
        public void ApplyFromQuest(任務資料列 quest)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.地圖)) return;
            ApplyMapName(quest.地圖);
        }

        /// <summary>
        /// 星級 1～10 × 構圖 01～04 共 40 張；載入後 <see cref="LastHudCaption"/> 會設定。
        /// 若對應檔案不存在則不改 sprite 並回傳 false。由 Bootstrap 決定是否在回退前先呼叫。
        /// </summary>
        public bool TryApplyCombatStarBackdrop(int starLevel1To10, int rotationZeroToThree, string areaLabelForHud)
        {
            var tier = Mathf.Clamp(starLevel1To10, 1, 10);
            var rot = Mathf.Clamp(rotationZeroToThree, 0, 3);
            var r = rot + 1;
            var relative = string.Format(_combatStarPathFormat, tier, r).Replace('\\', '/').TrimStart('/');
            var path = relative.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? relative
                : "Assets/" + relative;

#if UNITY_EDITOR
            TryEditorRefreshImport(path);
#endif
            var sp = SafeSpriteLoader.TryLoadSprite(path);
            if (sp == null) return false;

            ApplyLoadedSprite(sp, path);
            var stem = System.IO.Path.GetFileNameWithoutExtension(path);
            var shortCode = $"CS{tier:00}R{r:00}";
            var fallback = string.IsNullOrEmpty(area)
                ? $"星級遠景 {tier}★ 構圖{r}/4 ({stem})"
                : $"{area} · 星級遠景 {tier}★ 構圖{r}/4";
            ApplyLastHudCaption(area, fallback, shortCode, stem);

            ResetUvOverrides();
            FitSpriteToOrthographicCamera();
            return true;
        }

        static void TryEditorRefreshImport(string path)
        {
#if UNITY_EDITOR
            var rel = path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? path["Assets/".Length..]
                : path;
            var fsPath = System.IO.Path.Combine(Application.dataPath,
                rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fsPath))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
#endif
        }

        /// <summary>執行期指定戰鬥攝影機（可由 <see cref="BattlePreviewBootstrap"/> 呼叫）。</summary>
        public void SetWorldCamera(Camera cam) => _camera = cam;

        /// <summary>
        /// 直立戰鬥預覽預設：全螢幕覆蓋超寬全景、鏡頭移動時紋路可橫向捲動（不依賴 AddComponent 時的序列化 0）。
        /// </summary>
        public void ApplyPortraitPanoramaBootstrapDefaults()
        {
            _fitMode = BattleBackgroundFitMode.CoverPortraitPanorama;
            _useWorldFixedBackgroundPosition = true;
            _worldBackgroundAnchor = Vector3.zero;
            _slidePanoramaUWithCameraX = true;
            _panoramaUvSlidePerWorldUnitX = 0.026f;
            _enableMaterialUvPan = false;
            _uvPanPerWorldUnitX = 0f;
            _uvPanPerWorldUnitY = 0f;
            _materialMainTexStCaptured = false;
        }

        /// <summary>直接使用地圖名（須與圖檔命名一致，例如「古代樹森林」）。</summary>
        public void ApplyMapName(string 地圖名)
        {
            if (string.IsNullOrWhiteSpace(地圖名)) return;

            var map = 地圖名.Trim();
            var relative = string.Format(_pathFormat, map).Replace('\\', '/').TrimStart('/');
            var path = relative.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? relative
                : "Assets/" + relative;

#if UNITY_EDITOR
            TryEditorRefreshImport(path);
#endif

            var sp = SafeSpriteLoader.TryLoadSprite(path);
            ApplyLoadedSprite(sp, path);
            var fallback = sp != null
                ? map
                : $"{map}（未取得圖：{System.IO.Path.GetFileNameWithoutExtension(path)}）";
            ApplyLastHudCaption(null, fallback, map);

            ResetUvOverrides();

            FitSpriteToOrthographicCamera();
        }

        void ApplyLastHudCaption(string areaForHud, string fallbackCaption, params string[] lookupKeys)
        {
            var list = new List<string>();
            foreach (var k in lookupKeys)
            {
                if (!string.IsNullOrWhiteSpace(k)) list.Add(k.Trim());
            }

            if (BattleBackgroundLabelTable.TryPickDisplayName(list, out var label))
            {
                var a = string.IsNullOrWhiteSpace(areaForHud) ? "" : areaForHud.Trim();
                LastHudCaption = string.IsNullOrEmpty(a) ? label : $"{a} · {label}";
                return;
            }

            LastHudCaption = fallbackCaption;
        }

        void ApplyLoadedSprite(Sprite sp, string pathLogged)
        {
            if (_spriteRenderer == null) return;

            _spriteRenderer.sprite = sp;
            _spriteRenderer.enabled = sp != null;
            if (sp == null)
                Debug.LogWarning($"[BattleBackgroundDisplay] 找不到背景：{pathLogged}");
        }

        void ResetUvOverrides()
        {
            _materialMainTexStCaptured = false;
            if (_uvMpbApplied && _spriteRenderer != null)
            {
                _spriteRenderer.SetPropertyBlock(null);
                _uvMpbApplied = false;
            }
        }

        void OnValidate()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null) _spriteRenderer.sortingOrder = _sortingOrder;
        }

        任務資料列 FindQuestInAsset(string questId)
        {
            if (_questsJson == null || string.IsNullOrWhiteSpace(questId)) return null;
            try
            {
                var rows = JsonConvert.DeserializeObject<任務資料列[]>(_questsJson.text);
                if (rows == null) return null;
                foreach (var r in rows)
                {
                    if (r != null && r.任務編號 == questId) return r;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleBackgroundDisplay] 解析 quests 失敗：" + e.Message);
            }

            return null;
        }

        void FitSpriteToOrthographicCamera()
        {
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null || !cam.orthographic || _spriteRenderer == null || _spriteRenderer.sprite == null) return;

            var t = transform;
            var cpos = cam.transform.position;
            if (_useWorldFixedBackgroundPosition)
                t.position = _worldBackgroundAnchor;
            else
                t.position = new Vector3(cpos.x, cpos.y, 0f);

            // 可走區來自 BattleCombatManager（Camera.main 視口）；遠景相機為全螢幕。
            var main = Camera.main;
            var halfH = cam.orthographicSize;
            var aspectForArena = main != null && main != cam ? main.aspect : cam.aspect;
            var halfW = halfH * aspectForArena;
            var coverW = halfW * Mathf.Max(1f, _playfieldCoverageXY.x);
            var coverH = halfH * Mathf.Max(1f, _playfieldCoverageXY.y);
            var b = _spriteRenderer.sprite.bounds;
            var sx = coverW * 2f / Mathf.Max(0.0001f, b.size.x);
            var sy = coverH * 2f / Mathf.Max(0.0001f, b.size.y);
            switch (_fitMode)
            {
                case BattleBackgroundFitMode.CoverPortraitPanorama:
                {
                    var sUniform = Mathf.Max(sx, sy);
                    t.localScale = new Vector3(sUniform, sUniform, 1f);
                    break;
                }
                case BattleBackgroundFitMode.FitFullWidthLetterbox:
                    var sFw = sx;
                    t.localScale = new Vector3(sFw, sFw, 1f);
                    break;
                case BattleBackgroundFitMode.ContainWholeSprite:
                    var sCt = Mathf.Min(sx, sy);
                    t.localScale = new Vector3(sCt, sCt, 1f);
                    break;
                default:
                    var sCv = Mathf.Max(sx, sy);
                    t.localScale = new Vector3(sCv, sCv, 1f);
                    break;
            }
        }
    }
}
