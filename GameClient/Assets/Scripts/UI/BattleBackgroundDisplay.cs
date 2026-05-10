using MonsterHunter.Combat;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 依任務 <see cref="任務資料列.地圖"/> 載入戰鬥遠景圖（預設 <c>Assets/UI/Backgrounds/Battle/{地圖}_背景.png</c>），
    /// 與 <see cref="BattlePortraitLayout"/> 並用：攝影機僅佔螢幕下方時，背景仍填滿該視錐對應的世界範圍。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleBackgroundDisplay : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _spriteRenderer;
        [SerializeField] Camera _camera;
        [Tooltip("相對於 Assets/ 的路徑，需含副檔名；{0} 為地圖名（與 quests.json「地圖」一致）。")]
        [SerializeField] string _pathFormat = "UI/Backgrounds/Battle/{0}_背景.png";

        [Tooltip("2D 時將背景畫在較低順序，避免遮住角色。")]
        [SerializeField] int _sortingOrder = -100;

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
            if (_spriteRenderer != null && _spriteRenderer.sprite != null) FitSpriteToOrthographicCamera();
        }

        /// <summary>由任務列載入背景。</summary>
        public void ApplyFromQuest(任務資料列 quest)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.地圖)) return;
            ApplyMapName(quest.地圖);
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

            var sp = SafeSpriteLoader.TryLoadSprite(path);
            if (_spriteRenderer == null) return;

            _spriteRenderer.sprite = sp;
            _spriteRenderer.enabled = sp != null;
            if (sp == null)
                Debug.LogWarning($"[BattleBackgroundDisplay] 找不到背景：{path}");

            FitSpriteToOrthographicCamera();
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
            t.position = new Vector3(cpos.x, cpos.y, 0f);

            var halfH = cam.orthographicSize;
            var halfW = halfH * cam.aspect;
            var b = _spriteRenderer.sprite.bounds;
            var sx = halfW * 2f / Mathf.Max(0.0001f, b.size.x);
            var sy = halfH * 2f / Mathf.Max(0.0001f, b.size.y);
            var s = Mathf.Max(sx, sy);
            t.localScale = new Vector3(s, s, 1f);
        }
    }
}
