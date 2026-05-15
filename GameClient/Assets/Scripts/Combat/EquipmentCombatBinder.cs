using System;
using System.IO;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 依本機 Ledger 的穿戴武器編號解析 equipment／upgrade_rules JSON，填入 <see cref="PlayerCombatLoadout"/>。
    /// </summary>
    public static class EquipmentCombatBinder
    {
        /// <summary>由已在記憶體的 JSON 字串綁定（WebGL／StreamingAssets）。</summary>
        public static bool TryBindFromJson(
            string equipmentJsonText,
            string upgradeRulesJsonText,
            LocalHunterLedger ledger,
            string fallbackMovesetWeaponType,
            PlayerCombatLoadout loadout)
        {
            if (loadout == null || ledger == null || string.IsNullOrEmpty(equipmentJsonText))
                return false;

            try
            {
                var rows = JsonConvert.DeserializeObject<裝備資料列[]>(equipmentJsonText);
                if (rows == null || rows.Length == 0)
                    return false;

                var id = ledger.EquippedWeaponEquipmentId ?? "";
                裝備資料列 weapon = null;
                foreach (var r in rows)
                {
                    if (r?.裝備編號 != id && r?.裝備編號?.Trim() != id?.Trim())
                        continue;
                    if (LooksLikeWeapon(r)) { weapon = r; break; }
                }

                if (weapon == null)
                {
                    foreach (var r in rows)
                    {
                        if (r?.裝備類型 != null &&
                            !string.IsNullOrEmpty(fallbackMovesetWeaponType) &&
                            r.裝備類型.Trim() == fallbackMovesetWeaponType.Trim() &&
                            LooksLikeWeapon(r))
                        {
                            weapon = r;
                            break;
                        }
                    }
                }

                if (weapon == null || string.IsNullOrEmpty(weapon.裝備類型))
                    return false;

                裝備升級規則列 upgrade = null;
                if (!string.IsNullOrEmpty(upgradeRulesJsonText))
                {
                    var upRows = JsonConvert.DeserializeObject<裝備升級規則列[]>(upgradeRulesJsonText);
                    if (upRows != null)
                    {
                        foreach (var u in upRows)
                        {
                            if (u?.裝備編號 == weapon.裝備編號) { upgrade = u; break; }
                        }
                    }
                }

                var level = Mathf.Max(1, ledger.GetEquipmentLevel(weapon.裝備編號));
                var statMul = ResolveUpgradeMultiplier(upgrade, level);
                loadout.武器類型 = weapon.裝備類型.Trim();
                loadout.武器基礎物理 = Mathf.Max(1f, weapon.基礎數值.物理傷害 * statMul);
                loadout.武器屬性 = Mathf.Max(0f, weapon.基礎數值.屬性傷害 * statMul);
                loadout.武器屬性標籤 =
                    string.IsNullOrWhiteSpace(weapon.裝備屬性) ? "無" : weapon.裝備屬性.Trim();
                loadout.BoundWeaponEquipmentId = weapon.裝備編號;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EquipmentCombatBinder] " + e.Message);
                return false;
            }
        }

        public static bool TryBindFromDisk(
            string equipmentJsonFullPath,
            string upgradeRulesJsonFullPath,
            LocalHunterLedger ledger,
            string fallbackMovesetWeaponType,
            PlayerCombatLoadout loadout)
        {
            if (loadout == null || ledger == null ||
                string.IsNullOrEmpty(equipmentJsonFullPath) ||
                !File.Exists(equipmentJsonFullPath))
                return false;

            var eqText = File.ReadAllText(equipmentJsonFullPath);
            string upText = null;
            if (!string.IsNullOrEmpty(upgradeRulesJsonFullPath) && File.Exists(upgradeRulesJsonFullPath))
                upText = File.ReadAllText(upgradeRulesJsonFullPath);
            return TryBindFromJson(eqText, upText ?? "", ledger, fallbackMovesetWeaponType, loadout);
        }

        static bool LooksLikeWeapon(裝備資料列 r)
        {
            // 資料表以「類型為武器招式表鍵」者視為武器
            var t = r?.裝備類型 ?? "";
            return !(t.Contains("頭") || t.Contains("胸") || t.Contains("腕") ||
                     t.Contains("腰") || t.Contains("腳"));
        }

        internal static float ResolveUpgradeMultiplier(裝備升級規則列 rule, int currentLevel)
        {
            var mul = 1f;
            if (rule?.升級路徑 == null || currentLevel <= 1)
                return mul > 0f ? mul : 1f;

            foreach (var p in rule.升級路徑)
            {
                if (p == null) continue;
                if (p.等級 > 0 && currentLevel >= p.等級 &&
                    p.數值加成倍率 > 0f)
                    mul = p.數值加成倍率;
            }

            return mul > 0f ? mul : 1f;
        }
    }
}
