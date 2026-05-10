using UnityEngine;
using UnityEngine.UI;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 依 image_path 設定 Image；若檔案不存在或載入失敗，改用 256×256 半透明灰底，
    /// 並在中心顯示 displayName 的縮寫（不拋錯、不中斷流程）。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class SafeImageDisplay : MonoBehaviour
    {
        [SerializeField] Image _image;
        [SerializeField] Text _abbrevLabel;

        [Tooltip("對應 Textures 下相對路徑，例如 Textures/Items/potion 或 Assets/Textures/Items/potion.png")]
        public string imagePath;

        [Tooltip("用於縮寫顯示，例如道具中文名")]
        public string displayName;

        void Awake()
        {
            if (_image == null) _image = GetComponent<Image>();
            EnsureAbbrevLabel();
        }

        void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            if (_image == null) _image = GetComponent<Image>();
            EnsureAbbrevLabel();

            var sprite = SafeSpriteLoader.TryLoadSprite(imagePath);
            if (sprite != null)
            {
                _image.sprite = sprite;
                _image.color = Color.white;
                if (_abbrevLabel != null) _abbrevLabel.gameObject.SetActive(false);
                return;
            }

            _image.sprite = PlaceholderSpriteFactory.GetSharedPlaceholder();
            _image.color = Color.white;
            if (_abbrevLabel != null)
            {
                _abbrevLabel.gameObject.SetActive(true);
                _abbrevLabel.text = Abbreviate(displayName);
            }
        }

        /// <summary>執行時更新路徑與名稱並套用。</summary>
        public void SetPaths(string newImagePath, string newDisplayName)
        {
            imagePath = newImagePath;
            displayName = newDisplayName;
            Apply();
        }

        void EnsureAbbrevLabel()
        {
            if (_abbrevLabel != null) return;

            var t = transform.Find("AbbrevLabel");
            if (t != null)
            {
                _abbrevLabel = t.GetComponent<Text>();
                if (_abbrevLabel != null) return;
            }

            var go = new GameObject("AbbrevLabel", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240, 80);
            rt.anchoredPosition = Vector2.zero;

            _abbrevLabel = go.GetComponent<Text>();
            _abbrevLabel.alignment = TextAnchor.MiddleCenter;
            _abbrevLabel.color = Color.white;
            _abbrevLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_abbrevLabel.font == null)
                _abbrevLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _abbrevLabel.fontSize = 28;
            _abbrevLabel.resizeTextForBestFit = true;
            _abbrevLabel.resizeTextMinSize = 12;
            _abbrevLabel.resizeTextMaxSize = 36;
            _abbrevLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _abbrevLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _abbrevLabel.raycastTarget = false;
        }

        /// <summary>名稱縮寫：取前兩個字元（適合中文道具名）。</summary>
        public static string Abbreviate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var t = name.Trim();
            if (t.Length <= 2) return t;
            return t.Substring(0, 2);
        }
    }
}
