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

        static Sprite _orbSprite;

        /// <summary>
        /// 實心圓形占位（weapon 繞身攻擊等），多處共用一張貼圖。</summary>
        public static Sprite GetSharedOrbSprite()
        {
            if (_orbSprite != null) return _orbSprite;

            const int dim = 40;
            const float rOuter = dim * 0.48f;

            var tex = new Texture2D(dim, dim, TextureFormat.RGBA32, false);
            tex.name = "Placeholder_OrbCircle_Tex";
            var ctr = new Vector2(dim * 0.5f - 0.5f, dim * 0.5f - 0.5f);

            for (var y = 0; y < dim; y++)
            for (var x = 0; x < dim; x++)
            {
                var dx = x - ctr.x;
                var dy = y - ctr.y;
                var hit = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y,
                    hit <= rOuter
                        ? new Color(0.92f, 0.94f, 1f, 1f)
                        : Color.clear);
            }

            tex.Apply(false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;

            _orbSprite = Sprite.Create(tex, new Rect(0f, 0f, dim, dim), new Vector2(0.5f, 0.5f),
                Mathf.Max(dim * 0.5f, 1f));

            _orbSprite.name = "Placeholder_OrbCircle";
            return _orbSprite;
        }

        static Sprite _blockSprite;

        /// <summary>實心方塊占位（魔物方塊彈）。</summary>
        public static Sprite GetSharedBlockSprite()
        {
            if (_blockSprite != null) return _blockSprite;

            const int dim = 32;
            var tex = new Texture2D(dim, dim, TextureFormat.RGBA32, false);
            tex.name = "Placeholder_Block_Tex";
            for (var y = 0; y < dim; y++)
            for (var x = 0; x < dim; x++)
                tex.SetPixel(x, y, new Color(0.88f, 0.95f, 1f, 1f));

            tex.Apply(false, true);
            tex.filterMode = FilterMode.Trilinear;

            _blockSprite = Sprite.Create(tex, new Rect(0f, 0f, dim, dim), new Vector2(0.5f, 0.5f),
                dim * 0.5f);
            _blockSprite.name = "Placeholder_Block";
            return _blockSprite;
        }

        static Sprite _boltSprite;

        /// <summary>橫向閃電條占位。</summary>
        public static Sprite GetSharedBoltSprite()
        {
            if (_boltSprite != null) return _boltSprite;

            const int w = 48;
            const int h = 10;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "Placeholder_Bolt_Tex";
            var cy = h * 0.5f;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var d = Mathf.Abs(y + 0.5f - cy);
                var a = d < 2.2f ? 1f : d < 3.4f ? 0.45f : 0f;
                tex.SetPixel(x, y, new Color(1f, 0.98f, 0.45f, a));
            }

            tex.Apply(false, true);
            tex.filterMode = FilterMode.Trilinear;

            _boltSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), w * 0.5f);
            _boltSprite.name = "Placeholder_Bolt";
            return _boltSprite;
        }
    }
}
