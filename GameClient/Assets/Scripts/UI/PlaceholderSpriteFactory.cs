using UnityEngine;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 產生 256×256 半透明灰色占位 Sprite（不含文字；文字由 SafeImageDisplay 的 Text 子物件顯示）。
    /// </summary>
    public static class PlaceholderSpriteFactory
    {
        public const int Size = 256;

        static Sprite _cached;
        static Texture2D _cachedTex;

        /// <summary>取得可重複使用的占位 Sprite（同一張紋理，避免重複配置記憶體）。</summary>
        public static Sprite GetSharedPlaceholder()
        {
            if (_cached != null) return _cached;

            _cachedTex = CreatePlaceholderTexture();
            _cached = Sprite.Create(
                _cachedTex,
                new Rect(0, 0, Size, Size),
                new Vector2(0.5f, 0.5f),
                100f
            );
            _cached.name = "Placeholder_Gray256";
            return _cached;
        }

        public static Texture2D CreatePlaceholderTexture()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.name = "Placeholder_Gray256_Tex";
            var c = new Color32(128, 128, 128, 140);
            var pixels = new Color32[Size * Size];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = c;
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }
}
