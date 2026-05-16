# -*- coding: utf-8 -*-
"""依 equipment.json 重產 upgrade_rules.json：每把武器的升級素材對齊該魔物 MAT 前綴。"""
from __future__ import annotations

import copy
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EQ_PATH = ROOT / "DesignData" / "02_Equipment" / "equipment.json"
UP_PATH = ROOT / "DesignData" / "02_Equipment" / "upgrade_rules.json"


def monster_index_for_weapon(k: int) -> int:
    """WEP_k → 魔物序號 1–120：五把武器一組。"""
    return (k - 1) // 5 + 1


def remap_mat_ids(obj, mon: int) -> None:
    """將樹狀 JSON 內 MAT_{任意三位}_{尾} 改為 MAT_{mon:03d}_{尾}。"""
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
    wep_id: str,
    img_path: str,
    mon: int,
    star: int,
    craft_zenny: int,
) -> dict:
    rule = copy.deepcopy(template)
    rule["裝備編號"] = wep_id
    rule["圖片路徑"] = img_path
    remap_mat_ids(rule, mon)

    lv_steps = rule.get("升級路徑") or []
    # 金幣：與合成費、星級、魔物梯度的簡單比例（表內一致、可再調）
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
    equipment = json.loads(EQ_PATH.read_text(encoding="utf-8"))
    old_rules = json.loads(UP_PATH.read_text(encoding="utf-8"))
    by_id = {r["裝備編號"]: r for r in old_rules if isinstance(r, dict) and "裝備編號" in r}
    if "WEP_001" not in by_id:
        raise SystemExit("現有 upgrade_rules.json 缺少 WEP_001，無法取模板。")
    template = by_id["WEP_001"]

    out: list = []
    for row in equipment:
        wid = row.get("裝備編號")
        if not isinstance(wid, str) or not wid.startswith("WEP_"):
            continue
        k = int(wid.replace("WEP_", ""))
        mon = monster_index_for_weapon(k)
        star = int(row.get("星級") or 1)
        craft = row.get("合成配方") or {}
        craft_zenny = int(craft.get("所需金幣") or 0)
        img = row.get("圖片路徑") or f"Assets/Textures/Equipment/{wid}.png"
        out.append(
            build_rule_from_template(
                template,
                wep_id=wid,
                img_path=img,
                mon=mon,
                star=star,
                craft_zenny=craft_zenny,
            )
        )

    if len(out) != len(equipment):
        raise SystemExit(f"輸出筆數 {len(out)} 與 equipment {len(equipment)} 不一致")

    UP_PATH.write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"OK: {UP_PATH} → {len(out)} 筆（MAT 前綴已對齊魔物 {(len(out)-1)//5+1} 組）")


if __name__ == "__main__":
    main()
