using System;
using System.Collections.Generic;
using MonsterHunter.Data;
using MonsterHunter.DataModels;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 從本機倉庫已擁有的 <c>PET_*</c> 隨機擇一，套用至 <see cref="BattleRuntimeModifiers"/>。
    /// </summary>
    public static class OwnedPetBattleBuffs
    {
        static readonly System.Random Rng = new System.Random();

        /// <summary>企劃未填寵物購買價時的推估金額（與 <see cref="VillageShopScreen"/> 一致）。</summary>
        public static int EffectivePetPrice(寵物資料列 p)
        {
            if (p == null) return 800;
            if (p.購買價格 > 0) return p.購買價格;
            var h = (p.寵物編號 ?? "").GetHashCode();
            return 600 + (Mathf.Abs(h) % 400);
        }

        public static 寵物資料列[] LoadAllPets()
        {
            if (!DesignDataReader.TryLoadDesignDataText(out var json, "05_Systems", "pets.json"))
                return Array.Empty<寵物資料列>();
            try
            {
                var rows = DesignDataJsonArrayUtility.Parse寵物資料(json);
                return rows ?? Array.Empty<寵物資料列>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OwnedPetBattleBuffs] pets.json：" + e.Message);
                return Array.Empty<寵物資料列>();
            }
        }

        /// <summary>自 <paramref name="ledger"/> 的 Warehouse 找出數量 &gt; 0 的寵物編號。</summary>
        public static List<string> ListOwnedPetIds(LocalHunterLedger ledger)
        {
            var list = new List<string>(8);
            if (ledger?.Warehouse == null) return list;
            foreach (var kv in ledger.Warehouse)
            {
                if (kv.Value <= 0) continue;
                var id = kv.Key ?? "";
                if (id.StartsWith("PET_", StringComparison.Ordinal)) list.Add(id);
            }

            return list;
        }

        /// <summary>隨機擇一隻已擁有寵物並套用被動／騎乘等簡化詞條。</summary>
        public static void ApplyRandomOwnedPet(ref BattleRuntimeModifiers m, LocalHunterLedger ledger)
        {
            var owned = ListOwnedPetIds(ledger);
            if (owned.Count == 0) return;

            var pick = owned[Rng.Next(owned.Count)];
            var row = FindPetRow(LoadAllPets(), pick);
            if (row == null)
            {
                Debug.LogWarning("[OwnedPetBattleBuffs] 找不到寵物資料：" + pick);
                return;
            }

            ApplyPetRow(row, ref m);
            Debug.Log($"[OwnedPetBattleBuffs] 本局隨行：{row.名稱}（{pick}）");
        }

        static 寵物資料列 FindPetRow(寵物資料列[] rows, string petId)
        {
            if (rows == null || string.IsNullOrEmpty(petId)) return null;
            foreach (var r in rows)
            {
                if (r != null && r.寵物編號 == petId)
                    return r;
            }

            return null;
        }

        static void ApplyPetRow(寵物資料列 row, ref BattleRuntimeModifiers m)
        {
            var stats = row.基礎數值;
            if (stats != null)
            {
                var atk = Mathf.Max(0, stats.攻擊力);
                if (atk > 0)
                    m.PlayerOutgoingDamageMultiplier *= 1f + atk / 500f;

                var hp = Mathf.Max(0, stats.體力);
                if (hp > 0)
                    m.PlayerMaxHpMultiplier *= 1f + hp / 8000f;

                if (stats.騎乘移動加成 >= 1.02f)
                    m.PlayerMoveSpeedMultiplier *= Mathf.Clamp(stats.騎乘移動加成 / 1.65f, 1.04f, 1.22f);
            }

            var passiveText = PetSkillTextForBuffs(row.技能組);
            ApplyPassiveKeywords(passiveText, ref m);

            if (!string.IsNullOrEmpty(row.定位))
                ApplyPassiveKeywords(row.定位, ref m);
        }

        static string PetSkillTextForBuffs(寵物技能組 s)
        {
            if (s == null) return "";
            if (!string.IsNullOrEmpty(s.被動技能)) return s.被動技能;
            if (!string.IsNullOrEmpty(s.特殊功能)) return s.特殊功能;
            if (!string.IsNullOrEmpty(s.同步攻擊)) return s.同步攻擊;
            return s.主動技能 ?? "";
        }

        static void ApplyPassiveKeywords(string text, ref BattleRuntimeModifiers m)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Contains("會心") || text.Contains("攻擊") || text.Contains("咆哮") || text.Contains("鎖鐮") ||
                text.Contains("爆破"))
                m.PlayerOutgoingDamageMultiplier *= 1.02f;
            if (text.Contains("體力") || text.Contains("恢復") || text.Contains("回復"))
                m.PlayerMaxHpMultiplier *= 1.02f;
            if (text.Contains("鐵傘") || text.Contains("守護") || text.Contains("坦") || text.Contains("防禦"))
                m.PlayerMaxHpMultiplier *= 1.015f;
        }
    }
}
