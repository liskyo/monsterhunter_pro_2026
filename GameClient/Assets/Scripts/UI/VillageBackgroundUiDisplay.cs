using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 村莊／大廳全畫面背景（uGUI <see cref="Image"/>）。
    /// 預設載入 <c>Assets/UI/Backgrounds/Village/{鍵}_背景.png</c>，並以「覆蓋」比例縮放（裁切多餘邊，不變形）。
    /// 請將本元件掛在已拉滿父層（例如全螢幕 Panel）底下的子物件，錨點置中。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class VillageBackgroundUiDisplay : UIBehaviour
    {
        [SerializeField] Image _image;
        [Tooltip("相對於 Assets/；{0} 為村莊／場景鍵，須與圖檔主檔名一致。")]
        [SerializeField] string _pathFormat = "UI/Backgrounds/Village/{0}_背景.png";

        [Header("開場套用")]
        [Tooltip("若 <see cref=\"VillageSessionContext.PendingBackgroundKey\"/> 有值，會優先於此欄位。")]
        [SerializeField] string _預設村莊鍵 = "";

        [Tooltip("覆蓋範圍；若未指定，使用父物件 RectTransform。")]
        [SerializeField] RectTransform _coverBounds;

        Sprite _sprite;

        Vector2 _lastBoundsSize;

        void Awake()
        {
            if (_image == null) _image = GetComponent<Image>();
            _image.type = Image.Type.Simple;
            _image.preserveAspect = false;
            _image.raycastTarget = false;
        }

        void Start()
        {
            var pending = VillageSessionContext.PendingBackgroundKey;
            if (!string.IsNullOrWhiteSpace(pending))
            {
                VillageSessionContext.PendingBackgroundKey = null;
                ApplyVillageKey(pending);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_預設村莊鍵))
                ApplyVillageKey(_預設村莊鍵);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            RefreshCoverLayout();
        }

        void LateUpdate()
        {
            var bounds = BoundsRect;
            if (bounds == null) return;
            var s = bounds.rect.size;
            if (s != _lastBoundsSize)
                RefreshCoverLayout();
        }

        RectTransform BoundsRect => _coverBounds != null ? _coverBounds : transform.parent as RectTransform;

        /// <summary>村莊鍵須與圖檔一致，例如「調查團營地」→ 調查團營地_背景.png。</summary>
        public void ApplyVillageKey(string 村莊鍵)
        {
            if (string.IsNullOrWhiteSpace(村莊鍵)) return;

            var key = 村莊鍵.Trim();
            var relative = string.Format(_pathFormat, key).Replace('\\', '/').TrimStart('/');
            var path = relative.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? relative
                : "Assets/" + relative;

            _sprite = SafeSpriteLoader.TryLoadSprite(path);
            if (_image == null) _image = GetComponent<Image>();
            if (_image != null)
            {
                _image.sprite = _sprite;
                _image.enabled = _sprite != null;
                _image.color = Color.white;
            }

            if (_sprite == null)
                Debug.LogWarning($"[VillageBackgroundUiDisplay] 找不到背景：{path}");

            RefreshCoverLayout();
        }

        void RefreshCoverLayout()
        {
            var rt = transform as RectTransform;
            var bounds = BoundsRect;
            if (rt == null || bounds == null || _sprite == null) return;

            var p = bounds.rect;
            _lastBoundsSize = p.size;
            if (p.width <= 1f || p.height <= 1f) return;

            var spr = _sprite.rect;
            if (spr.height <= 0.01f) return;
            var spriteAspect = spr.width / spr.height;

            var parentAspect = p.width / p.height;
            float w;
            float h;
            if (parentAspect > spriteAspect)
            {
                w = p.width;
                h = p.width / spriteAspect;
            }
            else
            {
                h = p.height;
                w = p.height * spriteAspect;
            }

            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one;
        }

        void OnValidate()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image != null) _image.raycastTarget = false;
        }
    }
}
