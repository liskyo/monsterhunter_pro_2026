using System;
using MonsterHunter.Controllers;
using MonsterHunter.Data;
using MonsterHunter.DataModels;
using MonsterHunter.UI;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 執行期戰鬥總管：
    ///   1. 在 BattleMonsterPortrait / HunterPreview 上掛載物理與戰鬥元件
    ///   2. 注入預設企劃 JSON（<see cref="DesignDataReader"/>／StreamingAssets，不依賴 TextAsset Inspector）
    ///   3. 即時更新 HUD 血條、顯示勝負結果畫面
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class BattleCombatManager : MonoBehaviour
    {
        /// <summary>分出勝負並顯示結算後為 true，全戰鬥行為須停用。</summary>
        public static bool IsBattleConcluded { get; private set; }

        // ── 由 BattlePreviewBootstrap 設定 ──
        public GameObject HunterGo;
        public GameObject MonsterGo;
        public 魔物資料列 MonsterDataRow;
        public Canvas HudCanvas;

        /// <summary>試玩／除錯用：對應 weapon_movesets.json 的「武器類型」欄。</summary>
        public string DemoWeaponType = "大劍";

        /// <summary>武器基礎物理（equipment 綁定失敗時的後備）。</summary>
        public float DemoWeaponBasePhysical = 230f;

        /// <summary>Bootstrap／任務詞條等：本場戰鬥暫態倍率。</summary>
        public BattleRuntimeModifiers SessionModifiers = BattleRuntimeModifiers.Neutral;
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

        Camera _backdropCamera;
        Vector3 _battleCameraSmoothVel;

        [Header("遠景：世界座標固定全景時，係數愈接近 1，遠景鏡頭愈跟主鏡頭，走位時『滑過』全景愈明顯")]
        [SerializeField] [Range(0f, 1f)] float _backdropParallaxX = 1f;
        [SerializeField] [Range(0f, 1f)] float _backdropParallaxY = 1f;
        [Tooltip("主戰鬥鏡頭 SmoothDamp 平滑秒數；略小則跟獵人更貼、遠景滑行更即時。")]
        [SerializeField] [Range(0.035f, 0.22f)] float _cameraFollowSmoothTime = 0.045f;

        // ── 地圖限制（防止衝出鏡頭）──
        float _halfW;
        float _halfH;
        float _arenaHalfWMultiplier = 1.38f;
        float _arenaHalfHMultiplier = 1.28f;

        // 物理前置在 Awake 完成（HunterGo/MonsterGo 由 LaunchCombat 在 BattlePreviewBootstrap.Awake 設好後立刻設定，
        // BattleCombatManager 是在 BattlePreviewBootstrap.Awake 裡 new 出來的，
        // 所以 BattleCombatManager.Awake 不會執行（Unity 不在 Awake 期間遞迴呼叫新物件 Awake），
        // 改成 Start 確保所有 Awake 完成後才跑。
        // 但 PlayerController / MonsterAiController 的 Awake 在 AddComponent 瞬間執行，
        // 因此先在 SetupPhysics 加好 Rigidbody2D，彼等 Awake 自行確保也安全。

        void Start()
        {
            IsBattleConcluded = false;

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

            EnsureBackdropParallaxSerializedDefaults();

            SetupPhysics();        // 先給 Rigidbody2D
            SetupCombatComponents(); // 再加 PlayerController / MonsterAiController
            RefreshArenaBoundsFromTuning();
            ApplyBattlefieldBoundsToBackground();

            var bdGo = GameObject.Find("BattleBackdropCamera");
            if (bdGo != null)
                _backdropCamera = bdGo.GetComponent<Camera>();
            else
                Debug.LogWarning("[BattleCombatManager] 找不到 BattleBackdropCamera：遠景相機將無法跟隨／視差。");

            BuildCombatHud();
        }

        /// <summary>
        /// 執行期 <c>AddComponent</c> 時，Inspector 序列化會把部份欄位寫成 0，導致遠景相機乘上 0 永遠杵在原點。
        /// </summary>
        void EnsureBackdropParallaxSerializedDefaults()
        {
            // 僅在「皆為 0」時補值；避免 Unity 對執行期 AddComponent 把欄位序列成 0 導致遠景杵死。
            if (_backdropParallaxX > 1e-4f || _backdropParallaxY > 1e-4f)
                return;
            _backdropParallaxX = 1f;
            _backdropParallaxY = 1f;
        }

        /// <summary>
        /// 直行超寬全景預覽：遠景鏡頭與主鏡頭 XY 對齊、加快跟獵人（由 Bootstrap 呼叫）。
        /// </summary>
        public void ApplyStrongPanoramaBackdropFeel()
        {
            _backdropParallaxX = 1f;
            _backdropParallaxY = 1f;
            _cameraFollowSmoothTime = 0.045f;
        }

        // ────────────────────────────────────────────────────
        //  物理設定
        // ────────────────────────────────────────────────────

        void SetupPhysics()
        {
            SetupRigidbody(HunterGo);
            SetupRigidbody(MonsterGo);

            // 魔物實體碰撞體（阻擋獵人穿入）
            var monsterCol = MonsterGo.AddComponent<CircleCollider2D>();
            monsterCol.radius = 0.8f;
            monsterCol.isTrigger = false;

            // 獵人實體碰撞體（防止與魔物重疊）
            var hunterCol = HunterGo.AddComponent<CircleCollider2D>();
            hunterCol.radius = 0.4f;
            hunterCol.isTrigger = false;
        }

        static void SetupRigidbody(GameObject go)
        {
            // 不用 ?? ：Unity 的 GetComponent 回傳「假 null」，?? 無法正確偵測
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        void RefreshArenaBoundsFromTuning()
        {
            var row = _tuningStore != null ? _tuningStore.Active : null;
            if (row == null) return;
            _arenaHalfWMultiplier = Mathf.Max(1f, row.戰場水平可行走倍率);
            _arenaHalfHMultiplier = Mathf.Max(1f, row.戰場垂直可行走倍率);
        }

        void ApplyBattlefieldBoundsToBackground()
        {
            var bg = FindAnyObjectByType<BattleBackgroundDisplay>();
            if (bg == null) return;
            bg.SetPlayfieldCoverage(_arenaHalfWMultiplier, _arenaHalfHMultiplier);
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

            var session = SessionModifiers.Clamp();

            var ledger = LocalHunterLedger.LoadOrCreate();

            // PlayerCombatLoadout：優先綁 equipment.json，失敗則用試玩數值。
            var loadoutGo = new GameObject("PlayerCombatLoadout");
            var loadout = loadoutGo.AddComponent<PlayerCombatLoadout>();

            loadout.武器類型 =
                string.IsNullOrWhiteSpace(DemoWeaponType) ? "大劍" : DemoWeaponType.Trim();
            loadout.武器基礎物理 =
                DemoWeaponBasePhysical > 0f ? DemoWeaponBasePhysical : 230f;
            loadout.武器屬性 = 0f;
            loadout.武器屬性標籤 = "無";

            string equipJsonText = null;
            if (DesignDataReader.TryLoadDesignDataText(out equipJsonText, "02_Equipment",
                    "equipment.json"))
            {
                DesignDataReader.TryLoadDesignDataText(out var upgradeJson, "02_Equipment",
                    "upgrade_rules.json");

                EquipmentCombatBinder.TryBindFromJson(
                    equipJsonText, upgradeJson ?? "", ledger,
                    DemoWeaponType, loadout);

                DemoWeaponType = loadout.武器類型;

                DemoWeaponBasePhysical = loadout.武器基礎物理;
            }

            // 割草軌道：軌道半徑略大以利刃口靠近魔物本體；碰撞半徑隨資料放大
            var starGuess = Mathf.Clamp(loadout.武器星級 > 0 ? loadout.武器星級 : 5, 1, 10);
            var atkRangeGuess = GuessWeaponReach(loadout.武器類型, weaponJson, starGuess);
            // 直徑再加大 1.5 倍：乘數調高至 1.32x，最小半徑提升至 2.18f，最大至 5.25f，預設值提升至 2.48f，直徑極大化！
            var orbitRadius   = atkRangeGuess > 0.05f
                ? Mathf.Clamp(atkRangeGuess * 1.32f, 2.18f, 5.25f)
                : 2.48f;

            var orbitPivotGo = new GameObject("MeleeOrbitPivot");
            orbitPivotGo.transform.SetParent(HunterGo.transform, false);
            orbitPivotGo.transform.localPosition = Vector3.zero;
            orbitPivotGo.AddComponent<MeleeOrbitPivot>();

            var hitboxGo = new GameObject("AttackHitbox");
            hitboxGo.transform.SetParent(orbitPivotGo.transform, false);
            hitboxGo.transform.localPosition = new Vector3(orbitRadius, 0f, 0f);

            var hbCol = hitboxGo.AddComponent<CircleCollider2D>();
            hbCol.radius = atkRangeGuess > 0.05f
                ? Mathf.Clamp(atkRangeGuess * 0.28f, 0.38f, 1.45f)
                : 0.52f;
            hbCol.isTrigger = true;
            hbCol.enabled = false;
            var hitbox = hitboxGo.AddComponent<Hitbox>();

            // 占位視覺：改為武器圖片繞獵人轉（外圍軌道）
            var orbSr = hitboxGo.AddComponent<SpriteRenderer>();
            orbSr.sortingLayerName = "Default";
            orbSr.sortingOrder     = 50;
            orbSr.transform.localRotation = Quaternion.identity;

            string weaponImgPath = null;
            if (!string.IsNullOrEmpty(equipJsonText) && !string.IsNullOrEmpty(loadout.BoundWeaponEquipmentId))
            {
                try
                {
                    var eqRows = Newtonsoft.Json.JsonConvert.DeserializeObject<裝備資料列[]>(equipJsonText);
                    if (eqRows != null)
                    {
                        foreach (var row in eqRows)
                        {
                            if (row != null && row.裝備編號 == loadout.BoundWeaponEquipmentId)
                            {
                                weaponImgPath = row.圖片路徑;
                                break;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[BattleCombatManager] 解析裝備圖片路徑失敗：" + ex.Message);
                }
            }

            Sprite weaponSp = null;
            if (!string.IsNullOrEmpty(weaponImgPath))
                weaponSp = SafeSpriteLoader.TryLoadSprite(weaponImgPath.Trim());
            if (weaponSp == null)
                weaponSp = SafeSpriteLoader.TryLoadSprite("Assets/Textures/Equipment/WEP_001.png");

            if (weaponSp != null)
            {
                orbSr.sprite = weaponSp;
                var bh = Mathf.Max(weaponSp.bounds.size.y, weaponSp.bounds.size.x, 0.01f);
                var scale = (hbCol.radius * 2f) / bh;
                orbSr.transform.localScale = Vector3.one * scale;
            }
            else
            {
                orbSr.sprite = PlaceholderSpriteFactory.GetSharedOrbSprite();
                orbSr.transform.localScale = Vector3.one * Mathf.Clamp(hbCol.radius * 0.92f + 0.045f, 0.17f, 0.62f);
            }

            // 玩家 HP = 魔物最大血量 × 魔物詞條倍率 × 0.25 × 獵人體魄詞條
            var monsterHpScale =
                Mathf.Max(0.05f, session.MonsterMaxHpMultiplier);
            var monsterBaseHp =
                MonsterDataRow != null ? Mathf.Max(1f, MonsterDataRow.最大血量) : 2000f;

            var playerMaxHp =
                Mathf.Max(500f, monsterBaseHp * monsterHpScale * 0.25f *
                                       session.PlayerMaxHpMultiplier);

            // PlayerController on hunter
            var touchGo = new GameObject("CombatTouchInput");
            var touchInput = touchGo.AddComponent<PortraitCombatTouchInput>();

            _playerCtrl = HunterGo.AddComponent<PlayerController>();

            touchInput.Inject(_tuningStore, _playerCtrl);

            _playerCtrl.Inject(_tuningStore, loadout, weaponJson, playerMaxHp, touchInput,
                session.PlayerOutgoingDamageMultiplier, session.PlayerMoveSpeedMultiplier);

            TrySetPrivateField(_playerCtrl, "_attackHitbox", hitbox);

            _playerCtrl.SetMeleeOrbitRadius(orbitRadius);

            // MonsterAiController on monster
            _monsterAi = MonsterGo.AddComponent<MonsterAiController>();
            // 同時注入 tuningStore，AI 的 Update 才會執行追擊邏輯
            _monsterAi.InjectData(MonsterDataRow, HunterGo.transform, _tuningStore, monsterHpScale);
            TrySetPrivateField(_monsterAi, "_settlement", null);

            // 玩家直接知道目標，不走 Physics2D 掃描
            _playerCtrl.SetDirectTarget(_monsterAi);

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

            var backdrop = UnityEngine.Object.FindAnyObjectByType<BattleBackgroundDisplay>();
            var cap = backdrop != null ? backdrop.LastHudCaption.Trim() : "";
            if (!string.IsNullOrEmpty(cap))
            {
                var capGo = new GameObject("BackdropCaption", typeof(RectTransform));
                capGo.transform.SetParent(HudCanvas.transform, false);
                var crt = capGo.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0f, 0f);
                crt.anchorMax = new Vector2(0f, 0f);
                crt.pivot = new Vector2(0f, 0f);
                crt.anchoredPosition = new Vector2(14f, 10f);
                crt.sizeDelta = new Vector2(880f, 36f);

                var t = capGo.AddComponent<Text>();
                t.font = font;
                t.fontSize = 12;
                t.color = new Color(0.92f, 0.95f, 1f, 0.82f);
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.text = $"遠景　{cap}";
            }
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

            string ailmentSuffix = null;
            if (_playerCtrl != null)
            {
                var list = _playerCtrl.GetActiveAilmentNames();
                if (list != null && list.Count > 0)
                {
                    ailmentSuffix = $"<color=#FF3B30><b>[{string.Join("中！][", list)}中！]</b></color>";
                }
            }

            UpdateHpBar(_playerHpFill, _playerHpText,
                _playerCtrl != null ? _playerCtrl.CurrentHp : 0f,
                _playerCtrl != null ? _playerCtrl.MaxHp : 150f,
                ailmentSuffix);

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

        void LateUpdate()
        {
            if (_battleOver || HunterGo == null || _halfW <= 0f) return;
            UpdateBattleCameraFollow();
        }

        void UpdateBattleCameraFollow()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var Ax = _halfW * Mathf.Max(1f, _arenaHalfWMultiplier);
            var Ay = _halfH * Mathf.Max(1f, _arenaHalfHMultiplier);
            var p = HunterGo.transform.position;

            float cx;
            if (Ax <= _halfW + 1e-4f)
                cx = 0f;
            else
                cx = Mathf.Clamp(p.x, -Ax + _halfW, Ax - _halfW);

            float cy;
            if (Ay <= _halfH + 1e-4f)
                cy = 0f;
            else
                cy = Mathf.Clamp(p.y, -Ay + _halfH, Ay - _halfH);

            var current = cam.transform.position;
            var target = new Vector3(cx, cy, current.z);
            var smoothSec = Mathf.Clamp(_cameraFollowSmoothTime, 0.035f, 0.22f);
            var smoothed =
                Vector3.SmoothDamp(current, target, ref _battleCameraSmoothVel,
                    Mathf.Max(0.03f, smoothSec));

            smoothed.z = current.z;
            cam.transform.position = smoothed;

            if (_backdropCamera != null)
            {
                var bc = _backdropCamera.transform.position;
                var bx = smoothed.x * Mathf.Clamp01(_backdropParallaxX);
                var by = smoothed.y * Mathf.Clamp01(_backdropParallaxY);
                _backdropCamera.transform.position = new Vector3(bx, by, bc.z);
            }
        }

        static void UpdateHpBar(Image fill, Text label, float cur, float max, string suffix = null)
        {
            if (fill == null) return;
            var ratio = Mathf.Clamp01(cur / Mathf.Max(1f, max));
            var fr = fill.GetComponent<RectTransform>();
            fr.anchorMax = new Vector2(ratio, 1f);
            if (label != null)
            {
                label.text = suffix != null 
                    ? $"{Mathf.CeilToInt(cur)} {suffix}" 
                    : $"{Mathf.CeilToInt(cur)}";
            }
        }

        void ClampToCamera(GameObject go)
        {
            if (go == null || _halfW <= 0f) return;
            var limX = _halfW * _arenaHalfWMultiplier;
            var limY = _halfH * _arenaHalfHMultiplier;
            var p = go.transform.position;
            p.x = Mathf.Clamp(p.x, -limX, limX);
            p.y = Mathf.Clamp(p.y, -limY, limY);
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
            if (IsBattleConcluded)
                return;

            _battleOver = true;
            IsBattleConcluded = true;
            HaltAllCombatActors();

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

        /// <summary>勝負已分：停用輸入與 AI、清投射物、停軌道刃口。</summary>
        void HaltAllCombatActors()
        {
            if (_playerCtrl != null)
                _playerCtrl.MoveInput = Vector2.zero;

            if (HunterGo != null)
            {
                var hrb = HunterGo.GetComponent<Rigidbody2D>();
                if (hrb != null) hrb.linearVelocity = Vector2.zero;

                foreach (var piv in HunterGo.GetComponentsInChildren<MeleeOrbitPivot>(true))
                    if (piv != null) piv.enabled = false;

                var hb = HunterGo.GetComponentInChildren<Hitbox>(true);
                if (hb != null) hb.SetEnabled(false);

                if (_playerCtrl != null)
                    _playerCtrl.enabled = false;
            }

            if (MonsterGo != null)
            {
                var mrb = MonsterGo.GetComponent<Rigidbody2D>();
                if (mrb != null) mrb.linearVelocity = Vector2.zero;

                if (_monsterAi != null)
                    _monsterAi.enabled = false;
            }

            var touch = UnityEngine.Object.FindAnyObjectByType<PortraitCombatTouchInput>();
            if (touch != null)
                touch.enabled = false;

            var projs =
                UnityEngine.Object.FindObjectsByType<MonsterProjectile2D>(
                    FindObjectsInactive.Exclude);

            foreach (var p in projs)
                if (p != null)
                    Destroy(p.gameObject);
        }

        // ────────────────────────────────────────────────────
        //  工具
        // ────────────────────────────────────────────────────

        static float GuessWeaponReach(string weaponType, string weaponJsonText, int weaponStar)
        {
            if (string.IsNullOrEmpty(weaponType) ||
                string.IsNullOrEmpty(weaponJsonText) ||
                !WeaponMovesetRuntime.TryGetTapMoveStats(weaponType, weaponJsonText, weaponStar, out _, out var reach))
                return 0f;
            return Mathf.Max(0f, reach);
        }

        static string LoadDesignDataJson(params string[] relativeUnderDesignData)
        {
            if (DesignDataReader.TryLoadDesignDataText(out var json, relativeUnderDesignData))
                return json;

            Debug.LogWarning(
                "[BattleCombatManager] 載入 DesignData 失敗：" +
                System.IO.Path.Combine(relativeUnderDesignData));
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
