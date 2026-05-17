using System;
using System.Collections.Generic;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Data
{
    /// <summary>
    /// 讀取 <c>DesignData/hunter.json</c>：五件防具齊穿時對應獵人圖；否則自 <see cref="獵人未齊套裝預設"/> 隨機擇一。
    /// </summary>
    public static class HunterAppearanceResolver
    {
        static readonly string[] FallbackRandomPaths =
        {
            "Assets/Textures/Hunters/HUNTER_000_1.png",
            "Assets/Textures/Hunters/HUNTER_000_2.png",
            "Assets/Textures/Hunters/HUNTER_000_3.png",
            "Assets/Textures/Hunters/HUNTER_000_4.png",
        };

        static 獵人企劃根 _cachedRoot;
        static bool _cacheLoaded;

        /// <param name="equippedArmorIds">
        /// 目前身上已穿之五件防具 <c>ARM_*</c>（可超過 5 筆，會取集合判定）；null 或無法對齊任一套裝則走未齊套裝隨機圖。
        /// </param>
        public static string PickBattlePortraitPath(IReadOnlyList<string> equippedArmorIds)
        {
            var root = LoadOrCacheRoot();
            if (root != null && equippedArmorIds != null && equippedArmorIds.Count > 0 &&
                TryGetPortraitForWornSet(root, equippedArmorIds, out var wornPath) &&
                !string.IsNullOrWhiteSpace(wornPath))
                return wornPath.Trim();

            return PickRandomDefaultPortraitPath(root);
        }

        public static bool TryLoadRoot(out 獵人企劃根 root)
        {
            root = null;
            if (!DesignDataReader.TryLoadDesignDataText(out var json, "hunter.json"))
                return false;
            try
            {
                root = JsonConvert.DeserializeObject<獵人企劃根>(json);
                return root != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HunterAppearanceResolver] hunter.json 解析失敗：" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 僅當身上恰好穿滿五件 <c>ARM_*</c> 且與某列 <see cref="獵人企劃列.套裝防具編號"/> 集合完全一致時，回傳該獵人出場圖。
        /// </summary>
        public static bool TryGetPortraitForWornSet(
            獵人企劃根 root,
            IReadOnlyList<string> equippedArmorIds,
            out string portraitPath)
        {
            portraitPath = null;
            if (root?.獵人 == null || equippedArmorIds == null || equippedArmorIds.Count == 0)
                return false;

            var worn = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in equippedArmorIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                var t = id.Trim();
                if (t.StartsWith("ARM_", StringComparison.Ordinal))
                    worn.Add(t);
            }

            if (worn.Count != 5)
                return false;

            foreach (var h in root.獵人)
            {
                if (h?.套裝防具編號 == null || h.套裝防具編號.Length != 5)
                    continue;
                var need = new HashSet<string>(StringComparer.Ordinal);
                foreach (var a in h.套裝防具編號)
                {
                    if (!string.IsNullOrWhiteSpace(a))
                        need.Add(a.Trim());
                }

                if (need.Count != 5)
                    continue;
                if (!need.SetEquals(worn))
                    continue;

                portraitPath = h.獵人出場圖片路徑;
                return !string.IsNullOrWhiteSpace(portraitPath);
            }

            return false;
        }

        public static string PickRandomDefaultPortraitPath(獵人企劃根 root)
        {
            var paths = root?.未齊套裝預設?.隨機出場圖片路徑;
            if (paths != null && paths.Length > 0)
            {
                var list = new List<string>(paths.Length);
                foreach (var p in paths)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        list.Add(p.Trim());
                }

                if (list.Count > 0)
                    return list[UnityEngine.Random.Range(0, list.Count)];
            }

            return FallbackRandomPaths[UnityEngine.Random.Range(0, FallbackRandomPaths.Length)];
        }

        static 獵人企劃根 LoadOrCacheRoot()
        {
            if (_cacheLoaded)
                return _cachedRoot;
            _cacheLoaded = true;
            TryLoadRoot(out _cachedRoot);
            return _cachedRoot;
        }
    }
}
