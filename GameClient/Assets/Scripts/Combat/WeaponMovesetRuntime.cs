using System;
using System.Collections.Generic;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 從 weapon_movesets.json 解析：點擊連段／長按分段蓄力／專屬技（含段數）。
    /// 並保留相容用的「首招點擊」查詢。
    /// </summary>
    public static class WeaponMovesetRuntime
    {
        public sealed class TapComboStep
        {
            public float 動作倍率;
            public float 攻擊距離;
            public int 段數;

            internal TapComboStep CloneWithHits(int h) =>
                new TapComboStep { 動作倍率 = 動作倍率, 攻擊距離 = 攻擊距離, 段數 = Mathf.Max(1, h) };
        }

        public sealed class ChargeStages
        {
            public readonly List<float> 門檻秒 = new List<float>();
            public readonly List<float> 動作倍率 = new List<float>();
        }

        public sealed class SkillInfo
        {
            public float 動作倍率;
            public int 冷卻秒;
            public float 攻擊距離;
            public int 段數;
        }

        public sealed class ParsedMoveset
        {
            public TapComboStep[] TapChain { get; }
            public ChargeStages Charge { get; }
            public SkillInfo Skill { get; }

            internal ParsedMoveset(TapComboStep[] taps, ChargeStages ch, SkillInfo sk)
            {
                TapChain = taps ?? Array.Empty<TapComboStep>();
                Charge = ch;
                Skill = sk;
            }
        }

        public static bool TryParseMoveset(string weaponType, string jsonText, float tapFallbackReach,
            float singleChargeGateSeconds, float skillDefaultReach, out ParsedMoveset moveset)
            => TryParseMoveset(weaponType, jsonText, tapFallbackReach, singleChargeGateSeconds, skillDefaultReach, 5,
                out moveset);

        public static bool TryParseMoveset(string weaponType, string jsonText, float tapFallbackReach,
            float singleChargeGateSeconds, float skillDefaultReach, int weaponStar, out ParsedMoveset moveset)
            =>
            TryParseMovesetInner(weaponType, jsonText, tapFallbackReach, singleChargeGateSeconds, skillDefaultReach,
                weaponStar, out moveset);

        public static bool TryParseMoveset(string weaponType, TextAsset movesetsJson, float tapFallbackReach,
            float singleChargeGateSeconds, float skillDefaultReach, out ParsedMoveset moveset)
        {
            moveset = null;
            return movesetsJson != null &&
                   TryParseMovesetInner(weaponType, movesetsJson.text, tapFallbackReach, singleChargeGateSeconds,
                       skillDefaultReach, 5, out moveset);
        }

        public static bool TryParseMoveset(string weaponType, TextAsset movesetsJson, float tapFallbackReach,
            float singleChargeGateSeconds, float skillDefaultReach, int weaponStar, out ParsedMoveset moveset)
        {
            moveset = null;
            return movesetsJson != null &&
                   TryParseMovesetInner(weaponType, movesetsJson.text, tapFallbackReach, singleChargeGateSeconds,
                       skillDefaultReach, weaponStar, out moveset);
        }

        static bool TryParseMovesetInner(string weaponType, string jsonText, float tapFallbackReach,
            float singleChargeGateSeconds, float skillDefaultReach, int weaponStar, out ParsedMoveset moveset)
        {
            moveset = null;
            if (string.IsNullOrEmpty(jsonText) || string.IsNullOrEmpty(weaponType)) return false;
            武器招式表列[] rows;
            try
            {
                rows = JsonConvert.DeserializeObject<武器招式表列[]>(jsonText);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WeaponMovesetRuntime] " + e.Message);
                return false;
            }

            if (rows == null) return false;
            var star = Mathf.Clamp(weaponStar <= 0 ? 5 : weaponStar, 1, 10);
            武器招式表列 fallback = null;
            foreach (var row in rows)
            {
                if (row?.操作配置 == null || row.武器類型 != weaponType) continue;
                if (fallback == null) fallback = row;
                if (RowMatchesStar(row, star) && TryBuildMovesetFromRow(row, tapFallbackReach, singleChargeGateSeconds,
                        skillDefaultReach, out moveset))
                    return true;
            }

            if (fallback != null &&
                TryBuildMovesetFromRow(fallback, tapFallbackReach, singleChargeGateSeconds, skillDefaultReach,
                    out moveset))
                return true;

            return false;
        }

        /// <summary>星級欄位皆 ≤0 視為舊表「通配」列；否則須落在 [星級下限, 星級上限]。</summary>
        static bool RowMatchesStar(武器招式表列 row, int star)
        {
            if (row == null) return false;
            if (row.星級下限 <= 0 && row.星級上限 <= 0) return true;
            var lo = row.星級下限;
            var hi = row.星級上限 <= 0 ? lo : row.星級上限;
            if (lo > hi)
            {
                var tmp = lo;
                lo = hi;
                hi = tmp;
            }
            return star >= lo && star <= hi;
        }

        static bool TryBuildMovesetFromRow(武器招式表列 row, float tapFallbackReach, float singleChargeGateSeconds,
            float skillDefaultReach, out ParsedMoveset moveset)
        {
            moveset = null;
            var tapChain = ExtractTapChain(row.操作配置.點擊, tapFallbackReach);
            if (tapChain.Count == 0) return false;
            var skillRange = tapChain[0].攻擊距離;
            if (skillRange < 0.05f) skillRange = skillDefaultReach;
            var charge = ExtractChargeStages(row.操作配置.長按, singleChargeGateSeconds);
            var skill = ExtractSkill(row.操作配置.專屬技能, Mathf.Max(skillRange, skillDefaultReach));
            moveset = new ParsedMoveset(tapChain.ToArray(), charge, skill);
            return true;
        }

        static List<TapComboStep> ExtractTapChain(JToken tap, float fallbackR)
        {
            var list = new List<TapComboStep>();
            if (tap == null || tap.Type == JTokenType.Null) return list;
            if (tap.Type == JTokenType.Array)
            {
                foreach (var tok in tap)
                {
                    var s = TapFromToken(tok, fallbackR);
                    if (s != null) list.Add(s);
                }

                return list;
            }

            if (tap.Type == JTokenType.Object)
            {
                var one = TapFromToken(tap, fallbackR);
                if (one != null) list.Add(one);
            }

            return list;
        }

        static TapComboStep TapFromToken(JToken t, float fallbackRange)
        {
            if (t == null || t.Type != JTokenType.Object) return null;
            var mv = t.Value<float?>("動作倍率") ?? 0f;
            if (mv <= 0f) return null;
            var r = t.Value<float?>("攻擊距離") ?? 0f;
            if (r < 0.05f) r = Mathf.Max(fallbackRange, 0.5f);
            var hits = 1;
            var hitsTok = t["段數"];
            if (hitsTok != null && hitsTok.Type != JTokenType.Null)
            {
                var dv = hitsTok.Value<double?>();
                if (dv.HasValue)
                    hits = Mathf.Max(1, (int)Mathf.Round((float)dv.Value));
            }

            var step = new TapComboStep { 動作倍率 = mv, 攻擊距離 = r, 段數 = Mathf.Max(1, hits) };
            return step;
        }

        static ChargeStages ExtractChargeStages(武器長按招式 lc, float tuningSingleChargeGate)
        {
            if (lc == null) return null;
            var tiers = lc.分段倍率;
            var gate = Mathf.Clamp(tuningSingleChargeGate <= 1e-3f ? 0.28f : tuningSingleChargeGate, 0.1f, 2f);

            var st = new ChargeStages();
            if (tiers != null && lc.蓄力時間_秒 != null && lc.蓄力時間_秒.Length > 0)
            {
                var tArr = lc.蓄力時間_秒;
                if (tiers.一段 > 0f && tArr.Length > 0) AddTier(st, tArr[0], tiers.一段);
                if (tiers.二段 > 0f && tArr.Length > 1) AddTier(st, tArr[1], tiers.二段);
                if (tiers.三段 > 0f && tArr.Length > 2) AddTier(st, tArr[2], tiers.三段);
                return st.門檻秒.Count > 0 ? st : null;
            }

            if (lc.動作倍率 > 0f)
            {
                AddTier(st, gate * 2f, lc.動作倍率);
                return st;
            }

            return null;
        }

        static void AddTier(ChargeStages st, float threshSec, float mv)
        {
            if (threshSec <= 0f || mv <= 0f) return;
            st.門檻秒.Add(Mathf.Max(0.05f, threshSec));
            st.動作倍率.Add(mv);
        }

        static SkillInfo ExtractSkill(武器專屬技能招式 sk, float defaultRange)
        {
            if (sk == null) return null;
            var mv = sk.動作倍率 <= 0f ? 1.2f : sk.動作倍率;
            var cd = sk.冷卻時間 <= 0 ? 15 : sk.冷卻時間;
            var hits = sk.段數 <= 1 ? 1 : sk.段數;
            return new SkillInfo { 動作倍率 = mv, 冷卻秒 = cd, 攻擊距離 = defaultRange, 段數 = hits };
        }

        public static int ResolveChargeTierIndex(float secondsHeld, ChargeStages ch)
        {
            if (ch == null || ch.門檻秒.Count == 0 || secondsHeld <= 0f) return -1;
            var best = -1;
            for (var i = 0; i < ch.門檻秒.Count; i++)
            {
                if (secondsHeld + 1e-4f >= ch.門檻秒[i])
                    best = i;
            }

            return best;
        }

        public static float MvPerSwing(float fullMv, int hitsPerSwing) =>
            Mathf.Max(0.01f, fullMv / Mathf.Max(1, hitsPerSwing));

        // ─── 相容舊呼叫：首段點擊 ─────────────────────────────────────

        public static bool TryGetTapMoveStats(string weaponType, string jsonText, out float 動作倍率, out float 攻擊距離)
            => TryGetTapMoveStats(weaponType, jsonText, 5, out 動作倍率, out 攻擊距離);

        public static bool TryGetTapMoveStats(string weaponType, string jsonText, int weaponStar, out float 動作倍率,
            out float 攻擊距離)
        {
            動作倍率 = 0f;
            攻擊距離 = 0f;
            if (!TryParseMoveset(weaponType, jsonText, 4f, 0.28f, 5.5f, weaponStar, out var p) ||
                p.TapChain.Length == 0)
                return false;
            var t = p.TapChain[0];
            動作倍率 = t.動作倍率;
            攻擊距離 = t.攻擊距離;
            return 動作倍率 > 0f;
        }

        public static bool TryGetTapMoveStats(string weaponType, TextAsset movesetsJson, out float 動作倍率,
            out float 攻擊距離)
            => TryGetTapMoveStats(weaponType, movesetsJson, 5, out 動作倍率, out 攻擊距離);

        public static bool TryGetTapMoveStats(string weaponType, TextAsset movesetsJson, int weaponStar,
            out float 動作倍率, out float 攻擊距離)
        {
            動作倍率 = 0f;
            攻擊距離 = 0f;
            if (!TryParseMoveset(weaponType, movesetsJson, 4f, 0.28f, 5.5f, weaponStar, out var p) ||
                p.TapChain.Length == 0)
                return false;
            var t = p.TapChain[0];
            動作倍率 = t.動作倍率;
            攻擊距離 = t.攻擊距離;
            return 動作倍率 > 0f;
        }
    }
}
