using System;
using System.IO;
using MonsterHunter.Controllers;
using MonsterHunter.DataModels;
using MonsterHunter.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 執行期戰鬥總管：
    ///   1. 在 BattleMonsterPortrait / HunterPreview 上掛載物理與戰鬥元件
    ///   2. 注入所有 JSON（從磁碟讀取，不依賴 TextAsset Inspector）
    ///   3. 即時更新 HUD 血條、顯示勝負結果畫面
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class BattleCombatManager : MonoBehaviour
    {
        // ── 由 BattlePreviewBootstrap 設定 ──
        public GameObject HunterGo;
        public GameObject MonsterGo;
        public 魔物資料列 MonsterDataRow;
        public Canvas HudCanvas;

        // ── 內部 ──
        PlayerController _playerCtrl;
        MonsterAiController _monsterAi;
        CombatTuningStore _tuningStore;

        Image _playerHpFill;
        Image _monsterHpFill;
        Text _playerHpText;
        Text _monsterHpText;
        Text _floatingDmgText;
        float _floatingDmgTimer;
        bool _battleOver;

        // ── 地圖限制（防止衝出鏡頭）──
        float _halfW;
        float _halfH;

        void Start()
        {
            if (HunterGo == null || MonsterGo == null || MonsterDataRow == null)
            {
                Debug.LogError("[BattleCombatManager] 缺少必要參考，戰鬥未啟動。");
                return;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                _halfH = cam.orthographicSize;
                _halfW = _halfH * cam.aspect;
            }

            SetupPhysics();
            SetupCombatComponents();
            BuildCombatHud();
        }

        // ────────────────────────────────────────────────────
        //  物理設定
        // ────────────────────────────────────────────────────

        void SetupPhysics()
        {
            SetupRigidbody(HunterGo);
            SetupRigidbody(MonsterGo);

            var monsterCol = MonsterGo.AddComponent<CircleCollider2D>();
            monsterCol.radius = 0.6f;
            monsterCol.isTrigger = false;
        }

        static void SetupRigidbody(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody2D>() ?? go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // ────────────────────────────────────────────────────
        //  戰鬥元件設定
        // ────────────────────────────────────────────────────

        void SetupCombatComponents()
        {
            var tuningJson = LoadDesignDataJson("03_Combat", "combat_tuning.json");
            var weaponJson = LoadDesignDataJson("03_Combat", "weapon_movesets.json");

            // CombatTuningStore
            var tsGo = new GameObject("CombatTuningStore");
            _tuningStore = tsGo.AddComponent<CombatTuningStore>();
            if (!string.IsNullOrEmpty(tuningJson)) _tuningStore.InjectJson(tuningJson);

            // PlayerCombatLoadout（預設大劍）
            var loadoutGo = new GameObject("PlayerCombatLoadout");
            var loadout = loadoutGo.AddComponent<PlayerCombatLoadout>();
            loadout.武器類型 = "大劍";
            loadout.武器基礎物理 = 230f;
            loadout.武器屬性 = 0f;
            loadout.武器屬性標籤 = "無";

            // Hitbox on hunter child
            var hitboxGo = new GameObject("AttackHitbox");
            hitboxGo.transform.SetParent(HunterGo.transform, false);
            var hbCol = hitboxGo.AddComponent<CircleCollider2D>();
            hbCol.radius = 2.8f;
            hbCol.isTrigger = true;
            hbCol.enabled = false;
            var hitbox = hitboxGo.AddComponent<Hitbox>();

            // PlayerController on hunter
            _playerCtrl = HunterGo.AddComponent<PlayerController>();
            _playerCtrl.Inject(_tuningStore, loadout, weaponJson);
            // 開啟攻擊 hitbox 序列化欄位無法直接設定；透過 reflection 備用
            TrySetPrivateField(_playerCtrl, "_attackHitbox", hitbox);

            // MonsterAiController on monster
            _monsterAi = MonsterGo.AddComponent<MonsterAiController>();
            _monsterAi.InjectData(MonsterDataRow, HunterGo.transform);
            // 告知 ai 結算服務為空（開發期先不結算雲端）
            TrySetPrivateField(_monsterAi, "_settlement", null);

            // 玩家直接知道目標，不走 Physics2D 掃描
            _playerCtrl.SetDirectTarget(_monsterAi);

            // PortraitCombatTouchInput
            var inputGo = new GameObject("CombatTouchInput");
            var touchInput = inputGo.AddComponent<PortraitCombatTouchInput>();
            touchInput.Inject(_tuningStore, _playerCtrl);

            // 訂閱事件
            _monsterAi.OnDamageReceived += OnMonsterDamaged;
            _monsterAi.OnDefeated       += OnMonsterDefeated;
            _playerCtrl.OnDamageReceived += OnPlayerDamaged;
            _playerCtrl.OnDefeated       += OnPlayerDefeated;
        }

        // ────────────────────────────────────────────────────
        //  HUD 血條（覆蓋在現有 Canvas 上方）
        // ────────────────────────────────────────────────────

        void BuildCombatHud()
        {
            if (HudCanvas == null) return;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Arial" }, 14);

            // 魔物血條（上方 HUD 右半）
            _monsterHpFill = BuildTopBar(HudCanvas.transform, font,
                new Color(0.9f, 0.2f, 0.15f), new Vector2(0.52f, 1f), new Vector2(1f, 1f),
                out _monsterHpText, "魔物");

            // 獵人血條（上方 HUD 左半）
            _playerHpFill = BuildTopBar(HudCanvas.transform, font,
                new Color(0.15f, 0.75f, 0.25f), new Vector2(0f, 1f), new Vector2(0.48f, 1f),
                out _playerHpText, "獵");

            // 即時傷害浮字
            var fltGo = new GameObject("FloatDmg", typeof(RectTransform));
            fltGo.transform.SetParent(HudCanvas.transform, false);
            var frt = fltGo.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.5f, 0.88f);
            frt.anchorMax = new Vector2(0.5f, 0.88f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(300f, 50f);
            _floatingDmgText = fltGo.AddComponent<Text>();
            _floatingDmgText.font = font;
            _floatingDmgText.fontSize = 28;
            _floatingDmgText.fontStyle = FontStyle.Bold;
            _floatingDmgText.alignment = TextAnchor.MiddleCenter;
            _floatingDmgText.supportRichText = true;
            _floatingDmgText.color = new Color(1f, 1f, 0f, 0f);
        }

        static Image BuildTopBar(Transform parent, Font font, Color fillColor,
            Vector2 anchorMin, Vector2 anchorMax, out Text label, string tag)
        {
            var root = new GameObject("CombatBar_" + tag, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rr = root.GetComponent<RectTransform>();
            rr.anchorMin = anchorMin + new Vector2(0.01f, 0f);
            rr.anchorMax = anchorMax + new Vector2(-0.01f, 0f);
            rr.offsetMin = new Vector2(0f, -32f);
            rr.offsetMax = new Vector2(0f, -6f);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(root.transform, false);
            var trk = track.GetComponent<RectTransform>();
            trk.anchorMin = new Vector2(0f, 0f);
            trk.anchorMax = new Vector2(1f, 1f);
            trk.offsetMin = new Vector2(26f, 0f);
            trk.offsetMax = Vector2.zero;
            var trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            trackImg.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            var fr = fill.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            var fi = fill.AddComponent<Image>();
            fi.color = fillColor;
            fi.raycastTarget = false;

            var capGo = new GameObject("Label", typeof(RectTransform));
            capGo.transform.SetParent(root.transform, false);
            var crt = capGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(26f, 0f);
            crt.anchoredPosition = Vector2.zero;
            label = capGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = tag;

            return fi;
        }

        // ────────────────────────────────────────────────────
        //  每幀更新
        // ────────────────────────────────────────────────────

        void Update()
        {
            if (_battleOver) return;

            UpdateHpBar(_playerHpFill, _playerHpText,
                _playerCtrl != null ? _playerCtrl.CurrentHp : 0f,
                _playerCtrl != null ? _playerCtrl.MaxHp : 150f);

            UpdateHpBar(_monsterHpFill, _monsterHpText,
                _monsterAi != null ? _monsterAi.CurrentHp : 0f,
                _monsterAi != null ? Mathf.Max(1f, _monsterAi.MaxHp) : 1f);

            // 邊界鉗制（防止角色跑出鏡頭）
            if (_playerCtrl != null) ClampToCamera(HunterGo);
            if (_monsterAi  != null) ClampToCamera(MonsterGo);

            // 浮字淡出
            if (_floatingDmgTimer > 0f)
            {
                _floatingDmgTimer -= Time.deltaTime;
                if (_floatingDmgText != null)
                {
                    var c = _floatingDmgText.color;
                    c.a = Mathf.Clamp01(_floatingDmgTimer / 0.8f);
                    _floatingDmgText.color = c;
                }
            }
        }

        static void UpdateHpBar(Image fill, Text label, float cur, float max)
        {
            if (fill == null) return;
            var ratio = Mathf.Clamp01(cur / Mathf.Max(1f, max));
            var fr = fill.GetComponent<RectTransform>();
            fr.anchorMax = new Vector2(ratio, 1f);
            if (label != null)
                label.text = $"{Mathf.CeilToInt(cur)}";
        }

        void ClampToCamera(GameObject go)
        {
            if (go == null || _halfW <= 0f) return;
            var p = go.transform.position;
            p.x = Mathf.Clamp(p.x, -_halfW * 0.9f, _halfW * 0.9f);
            p.y = Mathf.Clamp(p.y, -_halfH * 0.85f, _halfH * 0.85f);
            go.transform.position = p;
        }

        // ────────────────────────────────────────────────────
        //  事件回調
        // ────────────────────────────────────────────────────

        void OnMonsterDamaged(float dmg, bool crit)
        {
            if (_floatingDmgText == null) return;
            _floatingDmgText.text = crit
                ? $"<color=#FF4400><b>暴！{dmg:F0}</b></color>"
                : $"<color=#FFE84A>{dmg:F0}</color>";
            var c = _floatingDmgText.color; c.a = 1f; _floatingDmgText.color = c;
            _floatingDmgTimer = 1f;
        }

        void OnPlayerDamaged(float dmg, bool crit) { }

        void OnMonsterDefeated() => ShowResult(won: true);
        void OnPlayerDefeated() => ShowResult(won: false);

        void ShowResult(bool won)
        {
            _battleOver = true;
            if (HudCanvas == null) return;

            var overlay = new GameObject("ResultOverlay", typeof(RectTransform));
            overlay.transform.SetParent(HudCanvas.transform, false);
            var ort = overlay.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;

            var bg = overlay.AddComponent<Image>();
            bg.color = won
                ? new Color(0f, 0.1f, 0.4f, 0.75f)
                : new Color(0.4f, 0f, 0f, 0.75f);

            var txtGo = new GameObject("ResultText", typeof(RectTransform));
            txtGo.transform.SetParent(overlay.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.1f, 0.35f); trt.anchorMax = new Vector2(0.9f, 0.65f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var t = txtGo.AddComponent<Text>();
            t.font = font;
            t.fontSize = 52;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = true;
            t.color = Color.white;
            t.text = won
                ? "<color=#FFEE44>任務成功！</color>\n<size=28>魔物已被討伐</size>"
                : "<color=#FF4444>任務失敗</color>\n<size=28>獵人倒下了……</size>";
        }

        // ────────────────────────────────────────────────────
        //  工具
        // ────────────────────────────────────────────────────

        static string LoadDesignDataJson(params string[] relativeUnderDesignData)
        {
            var rel = Path.Combine(relativeUnderDesignData);
            var dir = new System.IO.DirectoryInfo(Application.dataPath);
            for (var i = 0; i < 6 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "DesignData", rel);
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
            Debug.LogWarning($"[BattleCombatManager] 找不到 DesignData/{rel}");
            return null;
        }

        static void TrySetPrivateField(object target, string fieldName, object value)
        {
            try
            {
                var fi = target.GetType().GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fi?.SetValue(target, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BattleCombatManager] SetField {fieldName}: {e.Message}");
            }
        }
    }
}
