using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MonsterHunter.UI
{
    /// <summary>
    /// 依 image_path 嘗試載入 Sprite；失敗時回傳 null，不拋例外。
    /// Editor Play 模式下，若為 <c>Assets/...</c> 的 png/jpg，會<strong>優先</strong>自磁碟讀取以避免 AssetDatabase／已載入貼圖快取仍是舊像素；
    /// 其餘情境順序為：AssetDatabase → Resources → StreamingAssets → 絕對／相對檔案路徑。
    /// </summary>
    public static class SafeSpriteLoader
    {
        /// <param name="imagePath">例如 "Textures/Items/potion"、或 "Assets/Textures/Items/potion.png"</param>
        public static Sprite TryLoadSprite(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            var normalized = imagePath.Trim().Replace('\\', '/');

            try
            {
#if UNITY_EDITOR
                // Play 模式下 AssetDatabase 可能仍握有已載入的 Texture2D，外覆寫 PNG 後仍像舊圖；先讀磁碟最穩。
                if (EditorApplication.isPlaying && LooksLikeAssetsProjectImagePath(normalized))
                {
                    var diskSp = TryLoadUnderAssetsDataPath(normalized);
                    if (diskSp != null) return diskSp;
                }
                var editorSprite = TryLoadFromAssetDatabase(normalized);
                if (editorSprite != null) return editorSprite;
#endif
                var res = TryLoadFromResources(normalized);
                if (res != null) return res;

                var assetsFile = TryLoadUnderAssetsDataPath(normalized);
                if (assetsFile != null) return assetsFile;

                var stream = TryLoadFromStreamingAssets(normalized);
                if (stream != null) return stream;

                var file = TryLoadFromFileSystem(normalized);
                if (file != null) return file;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SafeSpriteLoader] 載入略過（已吞錯）: {normalized}\n{e.Message}");
            }

            return null;
        }

#if UNITY_EDITOR
        static bool LooksLikeAssetsProjectImagePath(string normalized)
        {
            var p = normalized.TrimStart('/').Trim();
            if (!p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;
            var lower = p.ToLowerInvariant();
            return lower.EndsWith(".png", StringComparison.Ordinal) ||
                   lower.EndsWith(".jpg", StringComparison.Ordinal) ||
                   lower.EndsWith(".jpeg", StringComparison.Ordinal);
        }

        static Sprite TryLoadFromAssetDatabase(string path)
        {
            var p = path;
            if (!p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                p = "Assets/" + p.TrimStart('/');
            if (p.IndexOf('.') < 0)
            {
                if (File.Exists(p + ".png")) p += ".png";
                else if (File.Exists(p + ".jpg")) p += ".jpg";
                else if (File.Exists(p + ".jpeg")) p += ".jpeg";
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (sprite != null) return sprite;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
#endif

        static Sprite TryLoadFromResources(string path)
        {
            var withoutExt = path;
            if (withoutExt.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && withoutExt.Length > 4)
                withoutExt = withoutExt[..^4];
            else if (withoutExt.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && withoutExt.Length > 4)
                withoutExt = withoutExt[..^4];
            else if (withoutExt.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) && withoutExt.Length > 5)
                withoutExt = withoutExt[..^5];

            if (withoutExt.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                withoutExt = withoutExt["Assets/".Length..];
            if (withoutExt.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
                withoutExt = withoutExt["Resources/".Length..];

            var sp = Resources.Load<Sprite>(withoutExt);
            if (sp != null) return sp;

            var tex = Resources.Load<Texture2D>(withoutExt);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>編輯器或本機開發：直接讀 Assets 下檔案（例如 Assets/Textures/Items/foo.png）。</summary>
        static Sprite TryLoadUnderAssetsDataPath(string path)
        {
            if (string.IsNullOrEmpty(Application.dataPath)) return null;

            var rel = path.Replace('\\', '/').TrimStart('/');
            if (rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                rel = rel["Assets/".Length..];

            if (rel.IndexOf('.') < 0)
            {
                var baseDir = Path.Combine(Application.dataPath, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(baseDir + ".png")) rel = rel + ".png";
                else if (File.Exists(baseDir + ".jpg")) rel = rel + ".jpg";
                else if (File.Exists(baseDir + ".jpeg")) rel = rel + ".jpeg";
                else return null;
            }

            var full = Path.Combine(Application.dataPath, rel.Replace('/', Path.DirectorySeparatorChar));
            return TextureFileToSprite(full);
        }

        static Sprite TryLoadFromStreamingAssets(string path)
        {
            var sa = Application.streamingAssetsPath;
            if (string.IsNullOrEmpty(sa)) return null;

            var rel = path;
            if (rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                rel = rel["Assets/".Length..];

            var full = Path.Combine(sa, rel.Replace('/', Path.DirectorySeparatorChar));
            return TextureFileToSprite(full);
        }

        static Sprite TryLoadFromFileSystem(string path)
        {
            if (!Path.IsPathRooted(path)) return null;
            return TextureFileToSprite(path);
        }

        static Sprite TextureFileToSprite(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;

            var bytes = File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;

            tex.name = Path.GetFileNameWithoutExtension(fullPath);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
