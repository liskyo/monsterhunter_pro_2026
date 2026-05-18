using System;
using System.Collections.Generic;
using System.Linq;
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
        public GameObject PetGo { get; set; }
        public 寵物資料列 PetDataRow { get; set; }
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

        // ✦ MH NOW 75秒時間倒數與 SP 大招能值按鈕參考
        private float _battleTimer = 75f;
        private Text _timerText;
        private Image _spGaugeFill;
        private Text _spBtnText;
        bool _battleOver;

        // ✦ 寵物與自選攜帶道具系統
        private CompanionPetController _petCtrl;
        private Image _petHpFill;
        private Text _petHpText;

        public List<string> SelectedBattleItemIds { get; set; } = new List<string>();
        private Dictionary<string, int> _battleItemQuantities = new Dictionary<string, int>();
        private Dictionary<string, float> _battleItemCooldowns = new Dictionary<string, float>();
        private Dictionary<string, float> _battleItemActiveDurations = new Dictionary<string, float>();
        private List<Text> _itemCooldownTexts = new List<Text>();
        private List<Image> _itemCooldownCovers = new List<Image>();
        private bool _inEntranceCountdown = true;

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
            _inEntranceCountdown = true;

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
            
            // ✦ 啟動開場慢動作倒數！
            StartCoroutine(EntranceCountdownRoutine());
        }

        System.Collections.IEnumerator EntranceCountdownRoutine()
        {
            // 1. 凍結雙方控制權
            if (_playerCtrl != null) _playerCtrl.enabled = false;
            if (_monsterAi != null) _monsterAi.enabled = false;
            var touch = UnityEngine.Object.FindAnyObjectByType<PortraitCombatTouchInput>();
            if (touch != null) touch.enabled = false;

            // 2. 進入極致慢動作 (0.25倍速)
            Time.timeScale = 0.25f;

            // 3. 建立倒數 UI
            var overlay = new GameObject("CountdownOverlay", typeof(RectTransform));
            if (HudCanvas != null)
                overlay.transform.SetParent(HudCanvas.transform, false);
            var ort = overlay.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;

            var txtGo = new GameObject("CountdownText", typeof(RectTransform));
            txtGo.transform.SetParent(overlay.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Segoe UI", "Arial" }, 72)
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var t = txtGo.AddComponent<Text>();
            t.font = font;
            t.fontSize = 140;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = true;
            t.color = new Color(1f, 0.85f, 0.1f, 1f); // 經典閃亮金
            
            var shadow = txtGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(4f, -4f);

            // 3... 2... 1...
            string[] steps = { "3", "2", "1" };
            foreach (var step in steps)
            {
                t.text = step;
                t.transform.localScale = Vector3.one * 1.5f;
                t.color = new Color(1f, 0.85f, 0.1f, 1f);
                
                // 動畫：使用真實時間 (unscaledDeltaTime) 以確保不受慢動作影響
                float elapsed = 0f;
                while (elapsed < 1f) 
                {
                    elapsed += Time.unscaledDeltaTime;
                    t.transform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one * 0.9f, elapsed / 0.3f);
                    t.color = Color.Lerp(new Color(1f, 0.85f, 0.1f, 1f), new Color(1f, 0.5f, 0f, 0.5f), (elapsed - 0.5f) / 0.5f);
                    yield return null;
                }
            }

            // 恢復正常時間
            Time.timeScale = 1f;

            _inEntranceCountdown = false;

            // START!
            t.text = "<color=#FF2222>開始狩獵！</color>";
            t.transform.localScale = Vector3.one * 1.5f;
            t.color = Color.white;

            // 解除凍結
            if (_playerCtrl != null) _playerCtrl.enabled = true;
            if (_monsterAi != null) _monsterAi.enabled = true;
            if (touch != null) touch.enabled = true;

            // START 字樣短暫停留並漸隱
            float fade = 0f;
            while (fade < 0.6f) 
            {
                fade += Time.deltaTime;
                t.transform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one * 2f, fade / 0.6f);
                t.color = new Color(1f, 1f, 1f, 1f - (fade / 0.6f));
                yield return null;
            }

            Destroy(overlay);
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
            _battleTimer = 75f;
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

            // 玩家 HP 使用原始的 session.MonsterMaxHpMultiplier 計算，保證血量大、極具安全感！
            var rawMonsterHpScale = Mathf.Max(0.05f, session.MonsterMaxHpMultiplier);
            var monsterBaseHp =
                MonsterDataRow != null ? Mathf.Max(1f, MonsterDataRow.最大血量) : 2000f;

            var playerMaxHp =
                Mathf.Max(300f, monsterBaseHp * rawMonsterHpScale * 0.15f *
                                       session.PlayerMaxHpMultiplier); // ✦ 下修獵人血量倍率，拒絕無腦站樁

            // 魔物實際 HP 乘上星級折扣，大幅提高擊殺效率，爽快通關！
            var star = MonsterDataRow != null ? MonsterDataRow.星級 : 1;
            float starHpScale = 1.0f;
            if (star == 1) starHpScale = 0.105f;       // ✦ 再下修 1/2（約 472 HP）
            else if (star == 2) starHpScale = 0.155f;  // ✦ 再下修 1/2
            else if (star == 3) starHpScale = 0.215f;  // ✦ 再下修 1/2
            else if (star == 4) starHpScale = 0.28f;   // ✦ 再下修 1/2
            else starHpScale = 0.435f;                 // ✦ 再下修 1/2

            var monsterHpScale = Mathf.Max(0.05f, rawMonsterHpScale * starHpScale);

            // PlayerController on hunter
            var touchGo = new GameObject("CombatTouchInput");
            var touchInput = touchGo.AddComponent<PortraitCombatTouchInput>();

            _playerCtrl = HunterGo.AddComponent<PlayerController>();

            touchInput.Inject(_tuningStore, _playerCtrl);

            // ✦ 移除無條件的 1.5 倍增傷，要求玩家依賴暴擊與閃避
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

            // ✦ 設置隨行寵物 AI
            if (PetGo != null && PetDataRow != null)
            {
                var petCtrl = PetGo.GetComponent<CompanionPetController>();
                if (petCtrl != null)
                {
                    petCtrl.Setup(HunterGo.transform, MonsterGo.transform, _monsterAi, _playerCtrl, PetDataRow);
                    Debug.Log($"[BattleCombatManager] 成功初始化隨行寵物 AI：{PetDataRow.名稱}");
                    _petCtrl = petCtrl; // ✦ 保存隨行寵物控制器引用！
                }
            }
        }

        // ────────────────────────────────────────────────────
        //  HUD 血條（覆蓋在現有 Canvas 上方）
        // ────────────────────────────────────────────────────

        void BuildCombatHud()
        {
            if (HudCanvas == null) return;

            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Segoe UI", "Arial" }, 20)
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 魔物血條（上方 HUD 右半）
            _monsterHpFill = BuildTopBar(HudCanvas.transform, font,
                new Color(0.9f, 0.2f, 0.15f), new Vector2(0.52f, 1f), new Vector2(1f, 1f),
                out _monsterHpText, "魔物");

            // 獵人血條（上方 HUD 左半）
            _playerHpFill = BuildTopBar(HudCanvas.transform, font,
                new Color(0.15f, 0.75f, 0.25f), new Vector2(0f, 1f), new Vector2(0.48f, 1f),
                out _playerHpText, "獵");

            // ✦ MH NOW 75秒倒數計時器
            var timerGo = new GameObject("CombatTimer", typeof(RectTransform));
            timerGo.transform.SetParent(HudCanvas.transform, false);
            var trt = timerGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -8f);
            trt.sizeDelta = new Vector2(160f, 48f);

            var bgImgGo = new GameObject("Bg", typeof(RectTransform));
            bgImgGo.transform.SetParent(timerGo.transform, false);
            var brt = bgImgGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bgImg = bgImgGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            var timerOut = bgImgGo.AddComponent<Outline>();
            timerOut.effectColor = new Color(1f, 1f, 1f, 0.35f);
            timerOut.effectDistance = new Vector2(1f, -1f);

            var txtGo = new GameObject("TimeText", typeof(RectTransform));
            txtGo.transform.SetParent(timerGo.transform, false);
            var txrt = txtGo.GetComponent<RectTransform>();
            txrt.anchorMin = Vector2.zero;
            txrt.anchorMax = Vector2.one;
            txrt.offsetMin = Vector2.zero;
            txrt.offsetMax = Vector2.zero;

            _timerText = txtGo.AddComponent<Text>();
            _timerText.font = font;
            _timerText.fontSize = 24;
            _timerText.fontStyle = FontStyle.Bold;
            _timerText.alignment = TextAnchor.MiddleCenter;
            _timerText.color = new Color(0.95f, 0.98f, 1f, 1f);
            _timerText.text = "75.00s";
            var timerShadow = txtGo.AddComponent<Shadow>();
            timerShadow.effectColor = Color.black;
            timerShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // ✦ MH NOW SP大招能值按鈕
            BuildSpButton(HudCanvas.transform, font);

            // ✦ 隨行寵物血條（位於獵人血條下方，高 44px，Y: -104f 到 -60f）
            if (PetGo != null)
            {
                _petHpFill = BuildTopBarWithOffset(HudCanvas.transform, font,
                    new Color(0.1f, 0.6f, 0.8f), new Vector2(0f, 1f), new Vector2(0.48f, 1f),
                    out _petHpText, "寵物", -104f, -60f);
            }

            // ✦ 攜帶道具按鈕（位於左下角）
            BuildBattleItemButtons(HudCanvas.transform, font);

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
            // 增加血條高度：原為 -32f 到 -6f (26px)，改為 -54f 到 -10f (44px)
            rr.offsetMin = new Vector2(0f, -54f);
            rr.offsetMax = new Vector2(0f, -10f);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(root.transform, false);
            var trk = track.GetComponent<RectTransform>();
            trk.anchorMin = new Vector2(0f, 0f);
            trk.anchorMax = new Vector2(1f, 1f);
            trk.offsetMin = Vector2.zero; // 置中填滿，不需留左側空間
            trk.offsetMax = Vector2.zero;
            var trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(0.12f, 0.13f, 0.16f, 0.95f); // MHN 現代極簡深灰卡片底色
            trackImg.raycastTarget = false;

            var outl = track.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, 0.25f); // 細緻白銀外框
            outl.effectDistance = new Vector2(1f, -1f);
            
            var shad = track.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            var fr = fill.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            // 讓血條稍微內縮一點點，露出外框的精緻感
            fr.offsetMin = new Vector2(1.5f, 1.5f);
            fr.offsetMax = new Vector2(-1.5f, -1.5f);
            var fi = fill.AddComponent<Image>();
            fi.color = fillColor;
            fi.raycastTarget = false;

            // 置中文字標籤，覆蓋在血條正上方
            var capGo = new GameObject("Label", typeof(RectTransform));
            capGo.transform.SetParent(track.transform, false);
            var crt = capGo.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            
            label = capGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 20; // 顯著放大字體
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter; // 水平垂直置中
            label.supportRichText = true;
            label.text = tag;

            // 增加黑陰影以確保背景亮色時文字依然清晰
            var shadow = capGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return fi;
        }

        static Image BuildTopBarWithOffset(Transform parent, Font font, Color fillColor,
            Vector2 anchorMin, Vector2 anchorMax, out Text label, string tag, float offsetMinY, float offsetMaxY)
        {
            var root = new GameObject("CombatBar_" + tag, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rr = root.GetComponent<RectTransform>();
            rr.anchorMin = anchorMin + new Vector2(0.01f, 0f);
            rr.anchorMax = anchorMax + new Vector2(-0.01f, 0f);
            rr.offsetMin = new Vector2(0f, offsetMinY);
            rr.offsetMax = new Vector2(0f, offsetMaxY);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(root.transform, false);
            var trk = track.GetComponent<RectTransform>();
            trk.anchorMin = new Vector2(0f, 0f);
            trk.anchorMax = new Vector2(1f, 1f);
            trk.offsetMin = Vector2.zero;
            trk.offsetMax = Vector2.zero;
            var trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            trackImg.raycastTarget = false;

            var outl = track.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, 0.25f);
            outl.effectDistance = new Vector2(1f, -1f);
            
            var shad = track.AddComponent<Shadow>();
            shad.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shad.effectDistance = new Vector2(2f, -2f);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            var fr = fill.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(1.5f, 1.5f);
            fr.offsetMax = new Vector2(-1.5f, -1.5f);
            var fi = fill.AddComponent<Image>();
            fi.color = fillColor;
            fi.raycastTarget = false;

            var capGo = new GameObject("Label", typeof(RectTransform));
            capGo.transform.SetParent(track.transform, false);
            var crt = capGo.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            
            label = capGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.supportRichText = true;
            label.text = tag;

            var shadow = capGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return fi;
        }

        private void BuildBattleItemButtons(Transform parent, Font font)
        {
            _itemCooldownTexts.Clear();
            _itemCooldownCovers.Clear();
            _battleItemCooldowns.Clear();
            _battleItemActiveDurations.Clear();
            _battleItemQuantities.Clear();

            var ledger = LocalHunterLedger.LoadOrCreate();
            var allItems = VillageGameFlow.LoadAllItems();

            // 如果 SelectedBattleItemIds 沒有項目，為防禦性起見，我們自動加載 3 個預設道具（回復藥、回復藥【大】、鬼人藥）
            if (SelectedBattleItemIds == null || SelectedBattleItemIds.Count == 0)
            {
                SelectedBattleItemIds = new List<string> { "ITM_001", "ITM_002", "ITM_017" };
            }

            for (int i = 0; i < SelectedBattleItemIds.Count; i++)
            {
                var itemId = SelectedBattleItemIds[i];
                if (string.IsNullOrEmpty(itemId)) continue;

                var itemRow = allItems.FirstOrDefault(x => x.素材編號 == itemId);
                if (itemRow == null) continue;

                int qty = ledger.GetWarehouseQuantity(itemId);
                _battleItemQuantities[itemId] = qty;
                _battleItemCooldowns[itemId] = 0f;
                _battleItemActiveDurations[itemId] = 0f;

                int currentIdx = i;
                string currentId = itemId;

                // 建立按鈕主物件
                var btnGo = new GameObject($"ItemButton_{i}", typeof(RectTransform));
                btnGo.transform.SetParent(parent, false);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                
                // 置於左下角，依序排列：X: 25, 115, 205 (每個按鈕寬 76f，間隔 14f)
                rt.anchoredPosition = new Vector2(25f + i * 90f, 25f);
                rt.sizeDelta = new Vector2(76f, 76f);

                // 背景圓形
                var bgGo = new GameObject("Bg", typeof(RectTransform));
                bgGo.transform.SetParent(btnGo.transform, false);
                var bgr = bgGo.GetComponent<RectTransform>();
                bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
                bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
                var bgImg = bgGo.AddComponent<Image>();
                bgImg.sprite = CreateCircleSprite(new Color(0.08f, 0.1f, 0.14f, 0.95f));
                
                var bgOutl = bgGo.AddComponent<Outline>();
                bgOutl.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.6f);
                bgOutl.effectDistance = new Vector2(1f, -1f);

                // 道具圖示 (Image)
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(btnGo.transform, false);
                var icr = iconGo.GetComponent<RectTransform>();
                icr.anchorMin = new Vector2(0.12f, 0.12f);
                icr.anchorMax = new Vector2(0.88f, 0.88f);
                icr.offsetMin = Vector2.zero; icr.offsetMax = Vector2.zero;
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.preserveAspect = true;
                
                var iconSprite = SafeSpriteLoader.TryLoadSprite(itemRow.圖片路徑);
                if (iconSprite != null)
                {
                    iconImg.sprite = iconSprite;
                    iconImg.color = Color.white;
                }
                else
                {
                    if (itemRow.分類 == "消耗品")
                        iconImg.color = new Color(0.1f, 0.65f, 0.25f, 0.7f);
                    else
                        iconImg.color = new Color(0.85f, 0.5f, 0.05f, 0.7f);
                }

                // 道具數量標記 (Text)
                var qtyGo = new GameObject("Qty", typeof(RectTransform));
                qtyGo.transform.SetParent(btnGo.transform, false);
                var qr = qtyGo.GetComponent<RectTransform>();
                qr.anchorMin = new Vector2(0f, 0f);
                qr.anchorMax = new Vector2(1f, 0.35f);
                qr.offsetMin = Vector2.zero; qr.offsetMax = Vector2.zero;
                var qtyTxt = qtyGo.AddComponent<Text>();
                qtyTxt.font = font;
                qtyTxt.fontSize = 15;
                qtyTxt.fontStyle = FontStyle.Bold;
                qtyTxt.alignment = TextAnchor.MiddleCenter;
                qtyTxt.color = Color.yellow;
                qtyTxt.text = $"x{qty}";
                
                var qtyShad = qtyGo.AddComponent<Shadow>();
                qtyShad.effectColor = Color.black;
                qtyShad.effectDistance = new Vector2(1f, -1f);

                // CD 遮罩 (Cooldown Cover)
                var cdGo = new GameObject("CdCover", typeof(RectTransform));
                cdGo.transform.SetParent(btnGo.transform, false);
                var cdr = cdGo.GetComponent<RectTransform>();
                cdr.anchorMin = Vector2.zero; cdr.anchorMax = Vector2.one;
                cdr.offsetMin = new Vector2(2f, 2f); cdr.offsetMax = new Vector2(-2f, -2f);
                var cdImg = cdGo.AddComponent<Image>();
                cdImg.sprite = CreateCircleSprite(new Color(0f, 0f, 0f, 0.75f));
                cdImg.type = Image.Type.Filled;
                cdImg.fillMethod = Image.FillMethod.Radial360;
                cdImg.fillOrigin = (int)Image.Origin360.Top;
                cdImg.fillClockwise = true;
                cdImg.fillAmount = 0f;
                _itemCooldownCovers.Add(cdImg);

                // CD 剩餘秒數文字
                var cdTxtGo = new GameObject("CdText", typeof(RectTransform));
                cdTxtGo.transform.SetParent(btnGo.transform, false);
                var cdtr = cdTxtGo.GetComponent<RectTransform>();
                cdtr.anchorMin = Vector2.zero; cdtr.anchorMax = Vector2.one;
                cdtr.offsetMin = Vector2.zero; cdtr.offsetMax = Vector2.zero;
                var cdTxt = cdTxtGo.AddComponent<Text>();
                cdTxt.font = font;
                cdTxt.fontSize = 20;
                cdTxt.fontStyle = FontStyle.Bold;
                cdTxt.alignment = TextAnchor.MiddleCenter;
                cdTxt.color = Color.white;
                cdTxt.text = "";
                _itemCooldownTexts.Add(cdTxt);
                
                var cdTxtShad = cdTxtGo.AddComponent<Shadow>();
                cdTxtShad.effectColor = Color.black;
                cdTxtShad.effectDistance = new Vector2(1f, -1f);

                // 按鈕點擊監聽
                var btn = btnGo.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    UseBattleItem(currentId, currentIdx, qtyTxt);
                });
            }
        }

        private void UseBattleItem(string itemId, int index, Text qtyText)
        {
            if (_battleOver) return;

            // 1. 檢查是否在冷卻中
            if (_battleItemCooldowns.TryGetValue(itemId, out var cd) && cd > 0f)
            {
                return;
            }

            // 2. 檢查數量
            if (!_battleItemQuantities.TryGetValue(itemId, out var qty) || qty <= 0)
            {
                ShowPetFloatingText("❌ 道具數量不足！", Color.red);
                return;
            }

            // 3. 觸發道具效果
            bool usedSuccess = false;
            float cdDuration = 10f; // 預設 CD

            switch (itemId)
            {
                case "ITM_001": // 回復藥
                    if (_playerCtrl != null)
                    {
                        float heal = _playerCtrl.MaxHp * 0.25f;
                        _playerCtrl.Heal(heal);
                        ShowPetFloatingText($"💚 使用回復藥！回復 {Mathf.CeilToInt(heal)} HP", new Color(0.12f, 0.95f, 0.2f));
                        usedSuccess = true;
                        cdDuration = 10f;
                    }
                    break;

                case "ITM_002": // 回復藥【大】
                    if (_playerCtrl != null)
                    {
                        float heal = _playerCtrl.MaxHp * 0.5f;
                        _playerCtrl.Heal(heal);
                        ShowPetFloatingText($"💚 使用回復藥【大】！回復 {Mathf.CeilToInt(heal)} HP", new Color(0.12f, 0.95f, 0.2f));
                        usedSuccess = true;
                        cdDuration = 15f;
                    }
                    break;

                case "ITM_017": // 鬼人藥
                    if (_playerCtrl != null)
                    {
                        _playerCtrl.SetOutgoingDamageMultiplier(1.20f);
                        _battleItemActiveDurations[itemId] = 15f;
                        ShowPetFloatingText("🔥 使用鬼人藥！15秒內物理攻擊提升 20%！", new Color(1f, 0.85f, 0f));
                        usedSuccess = true;
                        cdDuration = 20f;
                    }
                    break;

                case "ITM_018": // 硬化藥
                    if (_playerCtrl != null)
                    {
                        _playerCtrl.SetIncomingDamageMultiplier(0.80f); // 減少 20% 受傷
                        _battleItemActiveDurations[itemId] = 15f;
                        ShowPetFloatingText("🛡️ 使用硬化藥！15秒內承受傷害減少 20%！", new Color(0.2f, 0.6f, 1f));
                        usedSuccess = true;
                        cdDuration = 20f;
                    }
                    break;

                case "ITM_026": // 閃光彈
                    if (_monsterAi != null && _monsterAi.CurrentHp > 0f)
                    {
                        _monsterAi.SetOverrideTarget(null, 3f); // 眩暈斷仇恨 3s
                        ShowPetFloatingText("💫 投擲閃光彈！魔物眩暈 3 秒！", new Color(1f, 0.95f, 0.4f));
                        usedSuccess = true;
                        cdDuration = 25f;
                    }
                    break;

                case "ITM_030": // 小爆彈桶
                    if (_monsterAi != null && _monsterAi.CurrentHp > 0f)
                    {
                        _monsterAi.ApplyDamage(120f, false);
                        ShowPetFloatingText("💥 引爆小爆彈桶！造成 120 點爆破傷害！", new Color(1f, 0.35f, 0f));
                        usedSuccess = true;
                        cdDuration = 15f;
                    }
                    break;

                case "ITM_031": // 大爆彈桶
                    if (_monsterAi != null && _monsterAi.CurrentHp > 0f)
                    {
                        _monsterAi.ApplyDamage(380f, false);
                        ShowPetFloatingText("💥 引爆大爆彈桶！造成 380 點巨大爆破傷害！", new Color(1f, 0.2f, 0f));
                        usedSuccess = true;
                        cdDuration = 30f;
                    }
                    break;

                default: // 預設道具效果（回復 20% HP）
                    if (_playerCtrl != null)
                    {
                        float heal = _playerCtrl.MaxHp * 0.20f;
                        _playerCtrl.Heal(heal);
                        ShowPetFloatingText($"💚 使用道具！回復 {Mathf.CeilToInt(heal)} HP", new Color(0.12f, 0.95f, 0.2f));
                        usedSuccess = true;
                        cdDuration = 10f;
                    }
                    break;
            }

            if (usedSuccess)
            {
                // 4. 減扣本戰數量
                qty--;
                _battleItemQuantities[itemId] = qty;
                if (qtyText != null)
                {
                    qtyText.text = $"x{qty}";
                }

                // 5. 扣除磁碟存檔中的道具數量並存檔
                var led = LocalHunterLedger.LoadOrCreate();
                if (led.Warehouse != null && led.Warehouse.ContainsKey(itemId))
                {
                    led.Warehouse[itemId] = Mathf.Max(0, led.Warehouse[itemId] - 1);
                    led.Save();
                }

                // 6. 設定冷卻計時器
                _battleItemCooldowns[itemId] = cdDuration;
            }
        }

        private void ShowPetFloatingText(string text, Color color)
        {
            if (HudCanvas == null) return;
            
            var go = new GameObject("ItemUsePop", typeof(RectTransform));
            go.transform.SetParent(HudCanvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.45f);
            rt.anchorMax = new Vector2(0.5f, 0.45f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(800f, 60f);

            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Segoe UI", "Arial" }, 22)
                        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 28;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.text = text;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            StartCoroutine(FloatAndFadeItemText(go, rt, txt));
        }

        private System.Collections.IEnumerator FloatAndFadeItemText(GameObject go, RectTransform rt, Text txt)
        {
            float elapsed = 0f;
            float duration = 1.6f;
            Vector2 startPos = rt.anchoredPosition;

            while (elapsed < duration)
            {
                if (go == null || rt == null || txt == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float lift = Mathf.Sin(t * Mathf.PI * 0.5f) * 140f;
                rt.anchoredPosition = startPos + new Vector2(0f, lift);

                var c = txt.color;
                c.a = Mathf.Clamp01(1f - t);
                txt.color = c;

                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ────────────────────────────────────────────────────
        //  每幀更新
        // ────────────────────────────────────────────────────

        void Update()
        {
            if (_battleOver) return;

            // ✦ 75秒倒數計時器更新
            _battleTimer -= Time.deltaTime;
            if (_timerText != null)
            {
                _timerText.text = Mathf.Max(0f, _battleTimer).ToString("F2") + "s";
                if (_battleTimer <= 10f)
                {
                    _timerText.color = Color.Lerp(Color.white, Color.red, (Mathf.Sin(Time.time * 15f) + 1f) * 0.5f);
                    float pulse = 1f + (Mathf.Sin(Time.time * 15f) + 1f) * 0.1f;
                    _timerText.transform.localScale = new Vector3(pulse, pulse, 1f);
                }
                else
                {
                    _timerText.color = new Color(0.95f, 0.98f, 1f, 1f);
                    _timerText.transform.localScale = Vector3.one;
                }
            }

            if (_battleTimer <= 0f)
            {
                OnTimeUp();
                return;
            }

            // ✦ SP大招能值更新
            if (_playerCtrl != null)
            {
                float spPct = _playerCtrl.SpGauge;
                if (_spGaugeFill != null)
                {
                    _spGaugeFill.fillAmount = spPct / 100f;
                }
                if (_spBtnText != null)
                {
                    if (spPct >= 99.9f)
                    {
                        _spBtnText.text = "<b><color=#00E5FF>SP\nREADY</color></b>";
                        float pulse = 1f + (Mathf.Sin(Time.time * 8f) + 1f) * 0.05f;
                        _spBtnText.transform.localScale = new Vector3(pulse, pulse, 1f);
                    }
                    else
                    {
                        _spBtnText.text = $"SP\n{Mathf.FloorToInt(spPct)}%";
                        _spBtnText.transform.localScale = Vector3.one;
                    }
                }
            }

            string ailmentSuffix = null;
            if (_playerCtrl != null)
            {
                var ailments = _playerCtrl.GetActiveAilments();
                if (ailments != null && ailments.Count > 0)
                {
                    var activeTexts = new System.Collections.Generic.List<string>();
                    var now = Time.time;
                    foreach (var a in ailments)
                    {
                        var secLeft = Mathf.Max(0f, a.結束時間 - now);
                        // ✦ 未連結圖片時以文字替代，有圖片時以圖片加文字，並精準計秒顯示
                        if (!string.IsNullOrEmpty(a.圖片路徑))
                        {
                            var filename = a.圖片路徑.Contains("/") 
                                ? a.圖片路徑.Substring(a.圖片路徑.LastIndexOf('/') + 1) 
                                : a.圖片路徑;
                            activeTexts.Add($"🖼️({filename}) {a.異常名稱} ({secLeft:F1}s)");
                        }
                        else
                        {
                            activeTexts.Add($"{a.異常名稱} ({secLeft:F1}s)");
                        }
                    }
                    ailmentSuffix = $"<color=#FF3B30><b>[{string.Join("] [", activeTexts)}]</b></color>";
                }
            }

            UpdateHpBar(_playerHpFill, _playerHpText,
                _playerCtrl != null ? _playerCtrl.CurrentHp : 0f,
                _playerCtrl != null ? _playerCtrl.MaxHp : 150f,
                "獵人",
                ailmentSuffix);

            UpdateHpBar(_monsterHpFill, _monsterHpText,
                _monsterAi != null ? _monsterAi.CurrentHp : 0f,
                _monsterAi != null ? Mathf.Max(1f, _monsterAi.MaxHp) : 1f,
                "魔物");

            // ✦ 隨行寵物血條與力竭休息計時更新
            if (_petCtrl != null)
            {
                if (_petCtrl.IsResting)
                {
                    UpdateHpBar(_petHpFill, _petHpText,
                        0f,
                        _petCtrl.MaxHp,
                        $"🐾 {PetDataRow.名稱} [休息中]",
                        $"<color=#FF3B30><b>({Mathf.Max(0f, _petCtrl.RestTimer):F1}s)</b></color>");
                    
                    if (_petHpFill != null)
                    {
                        _petHpFill.color = new Color(0.8f, 0.2f, 0.2f);
                    }
                }
                else
                {
                    UpdateHpBar(_petHpFill, _petHpText,
                        _petCtrl.CurrentHp,
                        _petCtrl.MaxHp,
                        $"🐾 {PetDataRow.名稱}");

                    if (_petHpFill != null)
                    {
                        _petHpFill.color = new Color(0.1f, 0.6f, 0.8f);
                    }
                }
            }

            // ✦ 戰鬥自選攜帶道具冷卻與效果持續時間計時更新
            for (int i = 0; i < SelectedBattleItemIds.Count; i++)
            {
                var itemId = SelectedBattleItemIds[i];
                if (string.IsNullOrEmpty(itemId)) continue;

                // 1. Cooldown
                if (_battleItemCooldowns.TryGetValue(itemId, out var cd) && cd > 0f)
                {
                    cd = Mathf.Max(0f, cd - Time.deltaTime);
                    _battleItemCooldowns[itemId] = cd;

                    if (i < _itemCooldownTexts.Count && _itemCooldownTexts[i] != null)
                    {
                        _itemCooldownTexts[i].text = cd > 0f ? $"{cd:F1}s" : "";
                    }

                    if (i < _itemCooldownCovers.Count && _itemCooldownCovers[i] != null)
                    {
                        float maxCd = 10f;
                        if (itemId == "ITM_002") maxCd = 15f;
                        else if (itemId == "ITM_017" || itemId == "ITM_018") maxCd = 20f;
                        else if (itemId == "ITM_026") maxCd = 25f;
                        else if (itemId == "ITM_030") maxCd = 15f;
                        else if (itemId == "ITM_031") maxCd = 30f;

                        _itemCooldownCovers[i].fillAmount = Mathf.Clamp01(cd / maxCd);
                    }
                }
                else
                {
                    if (i < _itemCooldownTexts.Count && _itemCooldownTexts[i] != null)
                        _itemCooldownTexts[i].text = "";
                    if (i < _itemCooldownCovers.Count && _itemCooldownCovers[i] != null)
                        _itemCooldownCovers[i].fillAmount = 0f;
                }

                // 2. Active Buff Duration
                if (_battleItemActiveDurations.TryGetValue(itemId, out var dur) && dur > 0f)
                {
                    dur = Mathf.Max(0f, dur - Time.deltaTime);
                    _battleItemActiveDurations[itemId] = dur;

                    if (dur <= 0f)
                    {
                        if (itemId == "ITM_017" && _playerCtrl != null)
                        {
                            _playerCtrl.SetOutgoingDamageMultiplier(1.0f);
                            ShowPetFloatingText("🔥 鬼人藥效果結束！", Color.gray);
                        }
                        else if (itemId == "ITM_018" && _playerCtrl != null)
                        {
                            _playerCtrl.SetIncomingDamageMultiplier(1.0f);
                            ShowPetFloatingText("🛡️ 硬化藥效果結束！", Color.gray);
                        }
                    }
                }
            }

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

            if (_inEntranceCountdown && MonsterGo != null && HunterGo != null)
            {
                // ✦ 配合使用者要求，戰鬥倒數時魔物與獵人要完美呈現在同一個手機畫面上！
                // 計算兩者中心點，並平滑拉遠鏡頭視野，使雙方角色在直屏下皆能全彩同屏展現！
                var p1 = HunterGo.transform.position;
                var p2 = MonsterGo.transform.position;
                var center = Vector3.Lerp(p1, p2, 0.5f);

                // 直屏下將 Size 平滑過渡放大至 1.48 倍
                var targetSize = _halfH * 1.48f;
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.unscaledDeltaTime * 4.5f);

                var cur = cam.transform.position;
                var tgt = new Vector3(center.x, center.y + 0.5f, cur.z);
                cam.transform.position = Vector3.Lerp(cur, tgt, Time.unscaledDeltaTime * 6f);

                if (_backdropCamera != null)
                {
                    var bc = _backdropCamera.transform.position;
                    var bx = cam.transform.position.x * Mathf.Clamp01(_backdropParallaxX);
                    var by = cam.transform.position.y * Mathf.Clamp01(_backdropParallaxY);
                    _backdropCamera.transform.position = new Vector3(bx, by, bc.z);
                }
                return;
            }

            // 戰鬥開始後，鏡頭平滑拉近還原到標準戰鬥視野
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _halfH, Time.deltaTime * 3.5f);

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

        static void UpdateHpBar(Image fill, Text label, float cur, float max, string prefix, string suffix = null)
        {
            if (fill == null) return;
            var ratio = Mathf.Clamp01(cur / Mathf.Max(1f, max));
            var fr = fill.GetComponent<RectTransform>();
            fr.anchorMax = new Vector2(ratio, 1f);
            if (label != null)
            {
                var hpStr = $"{Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}";
                label.text = suffix != null 
                    ? $"{prefix}  {hpStr} {suffix}" 
                    : $"{prefix}  {hpStr}";
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
            if (HudCanvas == null || MonsterGo == null) return;

            // 建立一個漂浮傷害數字物件
            var go = new GameObject("DmgPop", typeof(RectTransform));
            go.transform.SetParent(HudCanvas.transform, false);
            
            var rt = go.GetComponent<RectTransform>();
            
            // 將魔物世界座標轉為螢幕 Canvas 局部座標
            Vector2 screenPos = Camera.main.WorldToScreenPoint(MonsterGo.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                HudCanvas.transform as RectTransform, 
                screenPos, 
                HudCanvas.worldCamera, 
                out Vector2 localPos
            );
            
            // 隨機微調初始位置，避免多段傷害重疊
            localPos.x += UnityEngine.Random.Range(-35f, 35f);
            localPos.y += UnityEngine.Random.Range(30f, 75f);
            rt.anchoredPosition = localPos;

            // 字型處理，若無自訂字型，則自動回退使用系統內建或大體字
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Arial" }, 22);

            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.supportRichText = true;
            
            // ✦ MH NOW 風格各類傷害樣式與特大數據格式
            if (PlayerController.NextHitIsPerfectCounter)
            {
                txt.text = $"<color=#FFD700><b>⚡ PERFECT {dmg:F0}</b></color>";
                txt.fontSize = 110;
            }
            else if (PlayerController.NextHitIsSP)
            {
                txt.text = $"<color=#00E5FF><b>💥 SP ULTIMATE {dmg:F0}</b></color>";
                txt.fontSize = 105;
            }
            else if (PlayerController.NextHitIsWeakness)
            {
                txt.text = $"<color=#FF8000><b>🎯 WEAKNESS {dmg:F0}</b></color>";
                txt.fontSize = 88;
            }
            else
            {
                txt.text = crit 
                    ? $"<color=#FF3B30><b>💥 CRIT {dmg:F0}</b></color>" 
                    : $"<color=#FFCC00><b>{dmg:F0}</b></color>";
                txt.fontSize = crit ? 98 : 72;
            }

            // 重設全域標記，防止污染後續傷害
            PlayerController.NextHitIsPerfectCounter = false;
            PlayerController.NextHitIsSP = false;
            PlayerController.NextHitIsWeakness = false;

            // 加上高對比黑陰影（Shadow）元件，讓數字在多變的戰鬥背景下依然極度耀眼清晰
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            // 啟動漂浮並漸變消失協程
            StartCoroutine(FloatAndFadeRoutine(go, rt, txt));
        }

        System.Collections.IEnumerator FloatAndFadeRoutine(GameObject go, RectTransform rt, Text txt)
        {
            float elapsed = 0f;
            float duration = 0.8f;
            Vector2 startPos = rt.anchoredPosition;
            
            while (elapsed < duration)
            {
                if (go == null || rt == null || txt == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 順滑向上漂移，前期快、後期慢
                float lift = Mathf.Sin(t * Mathf.PI * 0.5f) * 90f;
                rt.anchoredPosition = startPos + new Vector2(0f, lift);
                
                // 漸變淡出
                var c = txt.color;
                c.a = Mathf.Clamp01(1f - t);
                txt.color = c;
                
                yield return null;
            }
            
            if (go != null) Destroy(go);
        }

        void OnPlayerDamaged(float dmg, bool crit)
        {
            if (HudCanvas == null || HunterGo == null) return;

            // 建立一個漂浮傷害數字物件
            var go = new GameObject("PlayerDmgPop", typeof(RectTransform));
            go.transform.SetParent(HudCanvas.transform, false);
            
            var rt = go.GetComponent<RectTransform>();
            
            // 將獵人世界座標轉為螢幕 Canvas 局部座標
            Vector2 screenPos = Camera.main.WorldToScreenPoint(HunterGo.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                HudCanvas.transform as RectTransform, 
                screenPos, 
                HudCanvas.worldCamera, 
                out Vector2 localPos
            );
            
            // 隨機微調初始位置，避免多段傷害重疊
            localPos.x += UnityEngine.Random.Range(-25f, 25f);
            localPos.y += UnityEngine.Random.Range(40f, 85f);
            rt.anchoredPosition = localPos;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Arial" }, 22);

            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 48; // ✦ 獵人受傷數字超級清晰！
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.supportRichText = true;
            
            // 鮮紅色受擊數字
            txt.text = $"<color=#FF1E1E><b>-{dmg:F0}</b></color>";

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            shadow.effectDistance = new Vector2(2f, -2f);

            StartCoroutine(FloatAndFadeRoutine(go, rt, txt));
        }

        void OnMonsterDefeated()
        {
            StartCoroutine(WaitAndShowSuccessResult());
        }

        void OnPlayerDefeated() => ShowResult(won: false, rewards: null);

        System.Collections.IEnumerator WaitAndShowSuccessResult()
        {
            // 等待直到 _monsterAi 的 SettlementRewards 不是 null（限時最多 2.5 秒，保證流暢）
            float elapsed = 0f;
            while (_monsterAi != null && _monsterAi.SettlementRewards == null && elapsed < 2.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            var rewards = _monsterAi != null ? _monsterAi.SettlementRewards : null;
            
            // 任務成功！把素材噴在戰鬥畫面上！
            SpawnPhysicalLootDrops(rewards);

            // ✦ 新增互動：先等待玩家點擊畫面，將素材吸入玩家身上後，才跳出結算藍色畫面！
            yield return StartCoroutine(WaitForPlayerClickToCollect());

            ShowResult(won: true, rewards: rewards);
        }

        System.Collections.IEnumerator WaitForPlayerClickToCollect()
        {
            // 在 HUD 上加一個暫時的提示字體：「✦ 點擊畫面收集素材 ✦」
            GameObject hintGo = null;
            if (HudCanvas != null)
            {
                hintGo = new GameObject("CollectHint", typeof(RectTransform));
                hintGo.transform.SetParent(HudCanvas.transform, false);
                var rt = hintGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.22f);
                rt.anchorMax = new Vector2(1f, 0.32f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var txt = hintGo.AddComponent<Text>();
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Segoe UI", "Arial" }, 28)
                           ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.font = font;
                txt.fontSize = 32;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(1f, 0.92f, 0.25f, 1f); // 亮金色
                txt.text = "✦ 點擊畫面任意處收集掉落素材 ✦";

                var shadow = hintGo.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
                shadow.effectDistance = new Vector2(2f, -2f);
            }

            // 等待玩家點擊 (滑鼠左鍵或觸控)
            // 首 0.6 秒不接受點擊，給予素材完美的噴出散開動畫時間
            yield return new WaitForSeconds(0.6f);

            bool clicked = false;
            while (!clicked)
            {
                if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
                {
                    clicked = true;
                }
                yield return null;
            }

            // 點擊後，播放動感的素材「向玩家中心飛入吸收」的特效！
            var drops = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
            var dropList = new System.Collections.Generic.List<GameObject>();
            var startScales = new System.Collections.Generic.Dictionary<GameObject, Vector3>();
            foreach (var d in drops)
            {
                if (d != null && d.name.StartsWith("PhysicalDrop_"))
                {
                    dropList.Add(d);
                    startScales[d] = d.transform.localScale;
                }
            }

            float animDur = 0.5f;
            float animElapsed = 0f;
            Vector3 targetPos = Vector3.zero;
            if (HunterGo != null) 
            {
                targetPos = HunterGo.transform.position;
            }
            else if (MonsterGo != null)
            {
                targetPos = MonsterGo.transform.position;
            }

            // 關閉所有掉落物的物理碰撞，準備用飛入內插動畫
            foreach (var d in dropList)
            {
                if (d != null)
                {
                    var rb = d.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.simulated = false; // 停用物理
                    var col = d.GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                }
            }

            // 內插飛入動畫：飛向獵人並縮小
            while (animElapsed < animDur)
            {
                animElapsed += Time.deltaTime;
                float t = animElapsed / animDur;
                float tEase = t * t; // 速度由慢變快，極有吸力感
                
                // 動態追蹤獵人此時的最新位置
                if (HunterGo != null) targetPos = HunterGo.transform.position;

                foreach (var d in dropList)
                {
                    if (d != null)
                    {
                        d.transform.position = Vector3.Lerp(d.transform.position, targetPos, tEase);
                        if (startScales.TryGetValue(d, out var startScale))
                        {
                            d.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                        }
                    }
                }
                yield return null;
            }

            // 清理掉落物
            foreach (var d in dropList)
            {
                if (d != null) Destroy(d);
            }

            if (hintGo != null) Destroy(hintGo);
        }

        void SpawnPhysicalLootDrops(System.Collections.Generic.IReadOnlyList<MonsterHunter.Combat.SettlementRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0) return;

            Vector2 spawnCenter = Vector2.zero;
            if (MonsterGo != null)
                spawnCenter = MonsterGo.transform.position;
            else if (HunterGo != null)
                spawnCenter = HunterGo.transform.position;

            foreach (var r in rewards)
            {
                for (int i = 0; i < r.數量; i++)
                {
                    var dropGo = new GameObject("PhysicalDrop_" + r.素材編號);
                    dropGo.transform.position = spawnCenter + new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.3f, 0.3f));
                    dropGo.transform.localScale = new Vector3(1f, 1f, 1f); // 預設基礎大小

                    var sr = dropGo.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 3000; // 顯示在最上層
                    
                    if (!string.IsNullOrEmpty(r.圖片路徑))
                    {
                        var spr = MonsterHunter.UI.SafeSpriteLoader.TryLoadSprite(r.圖片路徑);
                        if (spr != null)
                        {
                            sr.sprite = spr;
                            // ✦ 智慧尺寸標準化防線：不論圖片解析度多高，強制將 2D 世界空間寬高限制在 0.8f ~ 1.0f 單位左右！
                            float maxDim = Mathf.Max(spr.rect.width, spr.rect.height) / spr.pixelsPerUnit;
                            if (maxDim > 0f)
                            {
                                float targetSize = 0.8f; // 理想的 2D 物理掉落大小 (約地圖格子的 80%，非常精緻！)
                                float scale = targetSize / maxDim;
                                dropGo.transform.localScale = new Vector3(scale, scale, scale);
                            }
                        }
                    }

                    // 針對 2D Top-Down 俯視地圖的無重力噴出與摩擦力減速設計！
                    var rb = dropGo.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f; // 頂部視角，無重力！
                    rb.mass = 1f;
                    rb.linearDamping = 3.5f; // 阻尼減速
                    rb.angularDamping = 2.5f;

#pragma warning disable CS0618
                    rb.drag = 3.5f; // 相容舊版 Unity 屬性
#pragma warning restore CS0618

                    var col = dropGo.AddComponent<CircleCollider2D>();
                    col.radius = 0.3f;
                    col.isTrigger = true; // 設為 Trigger 避免擋住人物或互推

                    // 向 360 度任意方向大角度噴射！
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float speed = UnityEngine.Random.Range(6f, 10f); // 噴發力度
                    var vel = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
                    
                    rb.linearVelocity = vel;
                    rb.angularVelocity = UnityEngine.Random.Range(-540f, 540f); // 旋轉效果
                    
                    Destroy(dropGo, 12f); // 防呆銷毀
                }
            }
        }

        void ShowResult(bool won, System.Collections.Generic.IReadOnlyList<MonsterHunter.Combat.SettlementRewardEntry> rewards)
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
            // 使用更具質感的深色半透明背景
            bg.color = won
                ? new Color(0.05f, 0.08f, 0.22f, 0.88f) // 勝利：深藍金屬光澤
                : new Color(0.22f, 0.04f, 0.04f, 0.88f); // 失敗：深暗紅警戒光澤

            var txtGo = new GameObject("ResultText", typeof(RectTransform));
            txtGo.transform.SetParent(overlay.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            // 拓寬垂直顯示範圍，保證大量素材列表完美呈現在中央！
            trt.anchorMin = new Vector2(0.05f, 0.12f); 
            trt.anchorMax = new Vector2(0.95f, 0.88f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            // 載入高品質中文字型，絕不顯示豆腐塊
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Segoe UI", "Arial" }, 32)
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var t = txtGo.AddComponent<Text>();
            t.font = font;
            t.fontSize = 52;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = true;
            t.color = Color.white;
            t.lineSpacing = 1.15f; // 字行間距稍微加寬，極具大氣感

            // 加上高對比黑陰影
            var shadow = txtGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            shadow.effectDistance = new Vector2(2f, -2f);

            var dropsText = "";
            if (won)
            {
                dropsText = "\n\n<color=#FFDD44><size=30><b>✦ 獲得討伐掉落素材 ✦</b></size></color>\n\n";
                if (rewards != null && rewards.Count > 0)
                {
                    foreach (var r in rewards)
                    {
                        dropsText += $"<color=#FFFFFF><size=26>✨  {r.素材名稱}  x  {r.數量}</size></color>\n";
                    }
                }
                else
                {
                    dropsText += "<color=#AAAAAA><size=24>無素材掉落</size></color>\n";
                }
            }

            t.text = won
                ? $"<color=#FFEE44><size=56><b>任務成功！</b></size></color>\n<size=28>魔物已被討伐</size>{dropsText}"
                : "<color=#FF4444><size=56><b>任務失敗</b></size></color>\n<size=28>獵人倒下了……</size>";

            // ✦ 新增「重新挑戰 / 下一隻魔物」按鈕，讓玩家不用回 Editor 狂按 Play！
            var btnGo = new GameObject("RestartButton", typeof(RectTransform));
            btnGo.transform.SetParent(overlay.transform, false);
            var brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(320f, 80f);
            brt.anchoredPosition = new Vector2(0f, 150f); // 螢幕下方

            var btnImg = btnGo.AddComponent<Image>();
            // 勝利顯示璀璨黃金返回色，失敗顯示熱血警戒紅重試色
            btnImg.color = won 
                ? new Color(0.85f, 0.65f, 0.15f, 0.95f) // 璀璨金黃
                : new Color(0.68f, 0.15f, 0.12f, 0.95f);  // 警戒猩紅

            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => {
                if (won)
                {
                    // ✦ 贏了取得素材，返回主入口村莊場景
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Bootstrap");
                }
                else
                {
                    // ✦ 輸了，重新載入當前戰鬥場景以重複進行戰鬥挑戰
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
            });

            var btnTxtGo = new GameObject("Text", typeof(RectTransform));
            btnTxtGo.transform.SetParent(btnGo.transform, false);
            var btnTrt = btnTxtGo.GetComponent<RectTransform>();
            btnTrt.anchorMin = Vector2.zero; btnTrt.anchorMax = Vector2.one;
            btnTrt.offsetMin = Vector2.zero; btnTrt.offsetMax = Vector2.zero;
            
            var btnTxt = btnTxtGo.AddComponent<Text>();
            btnTxt.font = font;
            btnTxt.fontSize = 32;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.color = Color.white;
            btnTxt.text = won ? "返回村莊" : "重新挑戰 (Retry)";

            var btnShadow = btnTxtGo.AddComponent<Shadow>();
            btnShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            btnShadow.effectDistance = new Vector2(2f, -2f);
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

        // ✦ MH NOW 75秒限時超時判定、SP按鈕動態生成與點擊綁定
        private void OnTimeUp()
        {
            if (_battleOver) return;
            _battleOver = true;
            IsBattleConcluded = true;
            Debug.Log("[BattleCombatManager] 時間到！任務失敗！");
            
            // 任務失敗，不發放獎勵，直接顯示失敗畫面
            ShowResult(won: false, rewards: null);
        }

        private void BuildSpButton(Transform parent, Font font)
        {
            var btnGo = new GameObject("SP_Button", typeof(RectTransform));
            btnGo.transform.SetParent(parent, false);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-25f, 25f);
            rt.sizeDelta = new Vector2(100f, 100f);

            var bgGo = new GameObject("Bg", typeof(RectTransform));
            bgGo.transform.SetParent(btnGo.transform, false);
            var bgr = bgGo.GetComponent<RectTransform>();
            bgr.anchorMin = Vector2.zero;
            bgr.anchorMax = Vector2.one;
            bgr.offsetMin = Vector2.zero;
            bgr.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = CreateCircleSprite(new Color(0.08f, 0.1f, 0.14f, 0.95f));
            bgImg.color = Color.white;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(btnGo.transform, false);
            var fr = fillGo.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(4f, 4f);
            fr.offsetMax = new Vector2(-4f, -4f);
            _spGaugeFill = fillGo.AddComponent<Image>();
            _spGaugeFill.sprite = CreateCircleSprite(new Color(0f, 0.85f, 1f, 1f));
            _spGaugeFill.type = Image.Type.Filled;
            _spGaugeFill.fillMethod = Image.FillMethod.Radial360;
            _spGaugeFill.fillOrigin = (int)Image.Origin360.Top;
            _spGaugeFill.fillClockwise = true;
            _spGaugeFill.fillAmount = 0f;

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(btnGo.transform, false);
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            _spBtnText = txtGo.AddComponent<Text>();
            _spBtnText.font = font;
            _spBtnText.fontSize = 22;
            _spBtnText.fontStyle = FontStyle.Bold;
            _spBtnText.alignment = TextAnchor.MiddleCenter;
            _spBtnText.color = new Color(0.92f, 0.96f, 1f, 0.85f);
            _spBtnText.text = "SP\n0%";

            var sh = txtGo.AddComponent<Shadow>();
            sh.effectColor = Color.black;
            sh.effectDistance = new Vector2(1.5f, -1.5f);

            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (_playerCtrl != null)
                {
                    _playerCtrl.TriggerSpUltimate();
                }
            });
        }

        static Sprite CreateCircleSprite(Color c)
        {
            var tex = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 31.5f;
                    float dy = y - 31.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= 31.5f)
                    {
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 12f);
        }
    }
}
