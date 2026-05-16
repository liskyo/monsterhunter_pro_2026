# -*- coding: utf-8 -*-
"""依 armor.json 產生 upgrade_rules_armor.json：每個防具槽位 5 件一組對應魔物 MAT 前綴（與武器規則相同）。"""
from __future__ import annotations

import copy
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ARMOR_PATH = ROOT / "DesignData" / "02_Equipment" / "armor.json"
WEAPON_UP_PATH = ROOT / "DesignData" / "02_Equipment" / "upgrade_rules.json"
OUT_PATH = ROOT / "DesignData" / "02_Equipment" / "upgrade_rules_armor.json"


def monster_index_for_armor(k: int) -> int:
    """ARM_k：五件一組（頭胸腕腰腳）對應同一魔物，序號 1–120。"""
    return (k - 1) // 5 + 1


def remap_mat_ids(obj, mon: int) -> None:
    if isinstance(obj, dict):
        for key, val in obj.items():
            if key == "素材編號" and isinstance(val, str):
                m = re.match(r"^MAT_\d{3}_(.+)$", val)
                if m:
                    obj[key] = f"MAT_{mon:03d}_{m.group(1)}"
            else:
                remap_mat_ids(val, mon)
    elif isinstance(obj, list):
        for x in obj:
            remap_mat_ids(x, mon)


def build_rule_from_template(
    template: dict,
    *,
    arm_id: str,
    img_path: str,
    mon: int,
    star: int,
    craft_zenny: int,
) -> dict:
    rule = copy.deepcopy(template)
    rule["裝備編號"] = arm_id
    rule["圖片路徑"] = img_path
    remap_mat_ids(rule, mon)

    lv_steps = rule.get("升級路徑") or []
    g2 = max(500, int(craft_zenny * 6.8 * (1 + 0.07 * (star - 1)) * (1 + 0.018 * (mon - 1))))
    mult = [1.0, 1.5, 2.0, 2.5]
    for i, step in enumerate(lv_steps):
        if i >= len(mult):
            break
        spend = step.get("升級花費")
        if isinstance(spend, dict):
            spend["金幣"] = int(g2 * mult[i])
    return rule


def main() -> None:
    armor_rows = json.loads(ARMOR_PATH.read_text(encoding="utf-8"))
    up_rows = json.loads(WEAPON_UP_PATH.read_text(encoding="utf-8"))
    by_id = {r["裝備編號"]: r for r in up_rows if isinstance(r, dict) and "裝備編號" in r}
    if "WEP_001" not in by_id:
        raise SystemExit("需要 upgrade_rules.json 內 WEP_001 作為升級結構模板。")
    template = by_id["WEP_001"]

    out: list = []
    for row in armor_rows:
        aid = row.get("裝備編號")
        if not isinstance(aid, str) or not aid.startswith("ARM_"):
            continue
        k = int(aid.replace("ARM_", ""))
        mon = monster_index_for_armor(k)
        star = int(row.get("星級") or 1)
        craft = row.get("合成配方") or {}
        craft_zenny = int(craft.get("所需金幣") or 0)
        img = row.get("圖片路徑") or f"Assets/Textures/Equipment/{aid}.png"
        out.append(
            build_rule_from_template(
                template,
                arm_id=aid,
                img_path=img,
                mon=mon,
                star=star,
                craft_zenny=craft_zenny,
            )
        )

    if len(out) != len(armor_rows):
        raise SystemExit(f"輸出 {len(out)} 與 armor.json {len(armor_rows)} 不一致")

    OUT_PATH.write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"OK: {OUT_PATH} → {len(out)} 筆")


if __name__ == "__main__":
    main()
