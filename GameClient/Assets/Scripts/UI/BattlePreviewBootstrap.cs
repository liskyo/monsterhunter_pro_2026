using System.IO;
using MonsterHunter.Combat;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterHunter.UI
{
    /// <summary>
    /// Play 時組戰鬥預覽：全畫面遠景（獨立背景相機）+ 主戰鬥視口（預設約螢幕 90%，上方約 10% HUD）、
    /// 魔物／獵人 Sprite、可選白底去背、HUD 血量與操作說明。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class BattlePreviewBootstrap : MonoBehaviour
    {
        const int BackdropCullingLayer = 9;

        [SerializeField] string _previewQuestId = "QST_001";
        [SerializeField] string _fallbackMapName = "古代樹森林";
        [SerializeField] float _orthographicSize = 5f;
        [SerializeField] [Range(0.5f, 0.95f)] float _battlefieldViewportHeight = 0.9f;
        [SerializeField] bool _showHudLabel = true;
        [SerializeField] bool _showMonsterWorldPortrait = true;
        [SerializeField] string _fallbackMonsterId = "MON_001";
        [SerializeField] bool _chromaKeyWhiteBackground = true;
        [SerializeField] [Range(0.02f, 0.2f)] float _chromaThreshold = 0.08f;
        [SerializeField] bool _showHunterPlaceholder = true;
        [SerializeField] int _demoPlayerHp = 100;
        [SerializeField] int _demoPlayerHpMax = 100;

        BattleBackgroundDisplay _background;
        Camera _bgCamera;

        // 戰鬥用：記錄精靈物件與魔物資料
        GameObject _monsterGo;
        GameObject _hunterGo;
        魔物資料列 _currentMonsterRow;
        Canvas _hudCanvas;

        void Awake()
        {
            // 強制覆寫：不論場景序列化舊值為何，預覽一律用 90% 戰鬥區 / 10% HUD
            _battlefieldViewportHeight = 0.9f;

            var main = Camera.main;
            if (main == null)
            {
                Debug.LogError("[BattlePreviewBootstrap] 找不到 MainCamera。");
                return;
            }

            main.orthographic = true;
            main.orthographicSize = Mathf.Max(0.1f, _orthographicSize);
            main.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

            BuildDualCameraStack(main);

            var layout = main.GetComponent<BattlePortraitLayout>();
            if (layout == null) layout = main.gameObject.AddComponent<BattlePortraitLayout>();
            layout.Configure(main, _battlefieldViewportHeight);

            var bgGo = new GameObject("BattleFarBackground");
            bgGo.transform.SetParent(null);
            bgGo.layer = BackdropCullingLayer;
            _background = bgGo.AddComponent<BattleBackgroundDisplay>();
            _background.SetWorldCamera(_bgCamera);
            foreach (Transform t in bgGo.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = BackdropCullingLayer;

            var quest = LoadPreviewQuest();
            if (quest != null && !string.IsNullOrWhiteSpace(quest.地圖))
                _background.ApplyFromQuest(quest);
            else if (!string.IsNullOrWhiteSpace(_fallbackMapName))
            {
                Debug.LogWarning("[BattlePreviewBootstrap] 使用 fallback 地圖：" + _fallbackMapName);
                _background.ApplyMapName(_fallbackMapName);
            }

            var monsterId = ResolveTargetMonsterId(quest);
            var monsterRow = LoadMonsterRow(monsterId);
            _currentMonsterRow = monsterRow;

            if (_showMonsterWorldPortrait && monsterRow != null)
                CreateMonsterWorldPortrait(main, monsterRow);

            if (_showHunterPlaceholder)
                CreateHunterPlaceholder(main);

            if (_showHudLabel)
                CreateHudAndBars(quest, monsterRow);

            // ── 啟動正式戰鬥 ──
            LaunchCombat();
        }

        void LaunchCombat()
        {
            if (_monsterGo == null || _hunterGo == null || _currentMonsterRow == null) return;

            var mgrGo = new GameObject("BattleCombatManager");
            var mgr = mgrGo.AddComponent<BattleCombatManager>();
            mgr.HunterGo      = _hunterGo;
            mgr.MonsterGo     = _monsterGo;
            mgr.MonsterDataRow = _currentMonsterRow;
            mgr.HudCanvas     = _hudCanvas;
        }

        void BuildDualCameraStack(Camera main)
        {
            var bgGo = new GameObject("BattleBackdropCamera");
            _bgCamera = bgGo.AddComponent<Camera>();
            _bgCamera.orthographic = true;
            _bgCamera.orthographicSize = Mathf.Max(0.1f, _orthographicSize);
            _bgCamera.transform.SetPositionAndRotation(main.transform.position, Quaternion.identity);
            _bgCamera.clearFlags = CameraClearFlags.SolidColor;
            _bgCamera.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            _bgCamera.depth = 0;
            _bgCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _bgCamera.cullingMask = 1 << BackdropCullingLayer;
            _bgCamera.allowHDR = false;
            _bgCamera.allowMSAA = false;

            main.depth = 1;
            main.clearFlags = CameraClearFlags.Depth;
            main.rect = new Rect(0f, 0f, 1f, _battlefieldViewportHeight);
            main.cullingMask = main.cullingMask & ~(1 << BackdropCullingLayer);
        }

        任務資料列 LoadPreviewQuest()
        {
            var path = ResolveDesignDataFile("05_Systems", "quests.json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning(
                    "[BattlePreviewBootstrap] 找不到 quests.json。預期：…/DesignData/05_Systems/quests.json");
                return null;
            }

            try
            {
                var text = File.ReadAllText(path);
                var rows = JsonConvert.DeserializeObject<任務資料列[]>(text);
                if (rows == null) return null;
                foreach (var r in rows)
                {
                    if (r != null && r.任務編號 == _previewQuestId)
                        return r;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattlePreviewBootstrap] 讀取任務失敗：" + e.Message);
            }

            return null;
        }

        static string ResolveDesignDataFile(params string[] relativeUnderDesignData)
        {
            var rel = Path.Combine(relativeUnderDesignData);
            var dir = new DirectoryInfo(Application.dataPath);
            for (var i = 0; i < 6 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "DesignData", rel);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            return null;
        }

        string ResolveTargetMonsterId(任務資料列 quest)
        {
            if (quest?.目標魔物 != null)
            {
                foreach (var t in quest.目標魔物)
                {
                    if (t != null && !string.IsNullOrWhiteSpace(t.魔物編號))
                        return t.魔物編號.Trim();
                }
            }

            return string.IsNullOrWhiteSpace(_fallbackMonsterId) ? "MON_001" : _fallbackMonsterId.Trim();
        }

        魔物資料列 LoadMonsterRow(string 魔物編號)
        {
            var path = ResolveDesignDataFile("01_Monsters", "monsters.json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning("[BattlePreviewBootstrap] 找不到 monsters.json。");
                return null;
            }

            try
            {
                var text = File.ReadAllText(path);
                var rows = JsonConvert.DeserializeObject<魔物資料列[]>(text);
                if (rows == null) return null;
                foreach (var r in rows)
                {
                    if (r != null && r.魔物編號 == 魔物編號)
                        return r;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattlePreviewBootstrap] 讀取魔物資料失敗：" + e.Message);
            }

            return null;
        }

        Material CreateChromaMaterialIfPossible()
        {
            var sh = Shader.Find("MonsterHunter/ChromaKeyWhiteSprite");
            if (sh == null) return null;
            var m = new Material(sh);
            m.SetColor("_KeyColor", Color.white);
            m.SetFloat("_Threshold", _chromaThreshold);
            return m;
        }

        void CreateMonsterWorldPortrait(Camera battleCam, 魔物資料列 row)
        {
            var sp = TryLoadMonsterFullBodySprite(row);
            var go = new GameObject("BattleMonsterPortrait");
            _monsterGo = go;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;

            var halfH = battleCam.orthographicSize;
            var halfW = halfH * battleCam.aspect;

            // 魔物目標高度 = 可見高度的 25%（halfH*0.5 = 可見全高的 25%）
            const float monsterTargetRatio = 0.5f;

            if (sp != null)
            {
                sr.sprite = sp;
                sr.color = Color.white;
                if (_chromaKeyWhiteBackground)
                {
                    var mat = CreateChromaMaterialIfPossible();
                    if (mat != null)
                        sr.sharedMaterial = mat;
                }
                var targetH = halfH * monsterTargetRatio;
                var bh = Mathf.Max(sp.bounds.size.y, 0.01f);
                var scale = targetH / bh;
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                sr.sprite = PlaceholderSpriteFactory.GetSharedPlaceholder();
                sr.color = new Color(0.9f, 0.45f, 0.1f, 0.85f);
                go.transform.localScale = new Vector3(halfW * 0.22f, halfH * monsterTargetRatio, 1f);
            }

            // 魔物：畫面右側 80% 處，偏上方（y = +halfH*0.4 ≈ 可見高度上方 70%）
            go.transform.position = new Vector3(
                battleCam.transform.position.x + halfW * 0.6f,
                battleCam.transform.position.y + halfH * 0.4f,
                0f);
        }

        /// <summary>
        /// 先走 JSON「圖片路徑」→ fallback {編號}.png → null（呼叫端會改用色塊）。
        /// </summary>
        static Sprite TryLoadMonsterFullBodySprite(魔物資料列 row)
        {
            if (row == null) return null;

            if (!string.IsNullOrWhiteSpace(row.圖片路徑))
            {
                var sp = SafeSpriteLoader.TryLoadSprite(row.圖片路徑);
                if (sp != null) return sp;
            }

            if (!string.IsNullOrWhiteSpace(row.魔物編號))
            {
                var id = row.魔物編號.Trim();
                var sp2 = SafeSpriteLoader.TryLoadSprite($"Assets/Textures/Monsters/{id}.png");
                if (sp2 != null) return sp2;
            }

            return null;
        }

        void CreateHunterPlaceholder(Camera battleCam)
        {
            var go = new GameObject("HunterPreview");
            _hunterGo = go;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 11;

            var halfH = battleCam.orthographicSize;
            var halfW = halfH * battleCam.aspect;

            // 獵人目標高度 = 魔物 (halfH*0.5) 的 1/3
            const float monsterTargetRatio = 0.5f;
            var hunterTargetH = halfH * monsterTargetRatio / 3f;

            var hunterSp = SafeSpriteLoader.TryLoadSprite("Assets/Textures/Hunter_placeholder.png");
            if (hunterSp != null)
            {
                sr.sprite = hunterSp;
                sr.color = Color.white;
                var bh = Mathf.Max(hunterSp.bounds.size.y, 0.01f);
                var s = hunterTargetH / bh;
                go.transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                sr.sprite = PlaceholderSpriteFactory.GetSharedPlaceholder();
                sr.color = new Color(0.35f, 0.5f, 0.95f, 0.85f);
                // 色塊寬 = 高 * 0.5（人形比例）
                go.transform.localScale = new Vector3(hunterTargetH * 0.5f, hunterTargetH, 1f);
            }

            // 獵人：左側，遠離魔物，相同地面高度
            go.transform.position = new Vector3(
                battleCam.transform.position.x - halfW * 0.62f,
                battleCam.transform.position.y - halfH * 0.28f,
                0f);
        }

        void CreateHudAndBars(任務資料列 quest, 魔物資料列 monster)
        {
            if (!TryEnsureEventSystem())
                return;

            var canvasGo = new GameObject("BattlePreviewHUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            _hudCanvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var topH = 1f - _battlefieldViewportHeight;
            var panel = new GameObject("TopHud", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f - topH);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Microsoft JhengHei", "Arial" }, 16);

            if (monster != null && !string.IsNullOrWhiteSpace(monster.圖示路徑))
            {
                var iconGo = new GameObject("MonsterIcon", typeof(RectTransform));
                iconGo.transform.SetParent(panel.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(1f, 0.5f);
                irt.anchorMax = new Vector2(1f, 0.5f);
                irt.pivot = new Vector2(1f, 0.5f);
                irt.sizeDelta = new Vector2(56f, 56f);
                irt.anchoredPosition = new Vector2(-12f, 0f);

                var iconImg = iconGo.AddComponent<Image>();
                iconImg.raycastTarget = false;
                var iconSprite = SafeSpriteLoader.TryLoadSprite(monster.圖示路徑);
                if (iconSprite != null)
                    iconImg.sprite = iconSprite;
                else
                    iconImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            }

            float textRightPad = monster != null && !string.IsNullOrWhiteSpace(monster.圖示路徑) ? 68f : 16f;

            var title = quest != null ? quest.標題 : "（無任務資料）";
            var map = quest != null ? quest.地圖 : _fallbackMapName;
            var mName = monster != null ? monster.名稱 : "（無魔物資料）";
            var mid = monster != null ? monster.魔物編號 : ResolveTargetMonsterId(quest);
            var mMaxHp = monster != null ? Mathf.Max(1, monster.最大血量) : 100;

            CreateHudSection(panel.transform, font, monster, quest, textRightPad, mMaxHp, mid, title, map, mName);

            BuildHintText(canvasGo.transform, font);
        }

        void CreateHudSection(Transform panelRoot, Font font, 魔物資料列 monster, 任務資料列 quest, float textRightPad,
            int mMaxHp, string mid, string title, string map, string mName)
        {
            BuildCompactDualHpRow(panelRoot, font,
                _demoPlayerHp / (float)Mathf.Max(1, _demoPlayerHpMax), 1f);

            var textGo = new GameObject("TitleBlock", typeof(RectTransform));
            textGo.transform.SetParent(panelRoot, false);
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(12f, 4f);
            tr.offsetMax = new Vector2(-textRightPad, -34f);

            var label = textGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 15;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 0.9f;
            label.text =
                $"<b>戰鬥預覽</b> · {title}\n{map} · <b>{mName}</b> ({mid})　HP上限 {mMaxHp}　獵人 {_demoPlayerHp}/{_demoPlayerHpMax}";
        }

        static void BuildCompactDualHpRow(Transform parent, Font font, float playerFill, float monsterFill)
        {
            var row = new GameObject("HpDualRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.04f, 1f);
            rr.anchorMax = new Vector2(0.96f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = new Vector2(0f, 26f);
            rr.anchoredPosition = new Vector2(0f, -6f);

            BuildMiniBar(row.transform, font, "獵", playerFill, new Color(0.2f, 0.7f, 0.3f), 0f, 0.48f);
            BuildMiniBar(row.transform, font, "魔", monsterFill, new Color(0.85f, 0.25f, 0.18f), 0.52f, 0.48f);
        }

        static void BuildMiniBar(Transform rowParent, Font font, string tag, float fill01, Color fillRgb,
            float anchorXMin, float widthFrac)
        {
            var wrap = new GameObject("Bar_" + tag, typeof(RectTransform));
            wrap.transform.SetParent(rowParent, false);
            var wr = wrap.GetComponent<RectTransform>();
            wr.anchorMin = new Vector2(anchorXMin, 0f);
            wr.anchorMax = new Vector2(anchorXMin + widthFrac, 1f);
            wr.offsetMin = Vector2.zero;
            wr.offsetMax = Vector2.zero;

            var cap = new GameObject("Tag");
            cap.transform.SetParent(wrap.transform, false);
            var crt = cap.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0.5f);
            crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(22f, 20f);
            crt.anchoredPosition = new Vector2(0f, 0f);
            var ct = cap.AddComponent<Text>();
            ct.font = font;
            ct.fontSize = 14;
            ct.color = Color.white;
            ct.alignment = TextAnchor.MiddleLeft;
            ct.text = tag;

            var track = new GameObject("Track");
            track.transform.SetParent(wrap.transform, false);
            var trk = track.AddComponent<RectTransform>();
            trk.anchorMin = new Vector2(0f, 0.15f);
            trk.anchorMax = new Vector2(1f, 0.85f);
            trk.offsetMin = new Vector2(24f, 0f);
            trk.offsetMax = new Vector2(-4f, 0f);
            var trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            trackImg.raycastTarget = false;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(track.transform, false);
            var fr = fill.AddComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            var fi = fill.AddComponent<Image>();
            fi.color = fillRgb;
            fi.raycastTarget = false;
        }

        void BuildHintText(Transform canvasRoot, Font font)
        {
            var hint = new GameObject("CombatHint", typeof(RectTransform));
            hint.transform.SetParent(canvasRoot, false);
            var hrt = hint.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0f, 8f);
            hrt.sizeDelta = new Vector2(-16f, 88f);

            var t = hint.AddComponent<Text>();
            t.font = font;
            t.fontSize = 13;
            t.color = new Color(1f, 1f, 1f, 0.85f);
            t.alignment = TextAnchor.LowerCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.text =
                "【操作（預覽）】正式戰鬥請接 <b>PortraitCombatTouchInput</b> + <b>PlayerController</b> 與戰鬥場景。\n" +
                "編輯器：在螢幕「下方戰鬥區」按住滑鼠左鍵拖曳＝虛擬搖桿移動；放開後可觸發普攻流程（需已配置 CombatTuningStore、武器 JSON）。\n" +
                "手機：單指在下半部拖曳＝移動。\n" +
                "去背：PNG 建議直接存 Alpha；白底可用 Shader 「ChromaKeyWhiteSprite」（魔物頭像 UI 仍為原圖裁切）。";
        }

        bool TryEnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return true;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            return true;
        }
    }
}
