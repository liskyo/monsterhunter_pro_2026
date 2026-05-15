# -*- coding: utf-8 -*-
"""
武器 vs 護甲／防具 素材分流（對齊 drop_rates）：
- 護甲 armor.json（新建）：優先消耗「較易得」掉落 — MAT_k_01／02（多半為「基本擊殺」）。
- 武器 equipment.json：改為消耗「較難取得」掉落 — MAT_k_03／04／05（部位／尾巴／稀）。
- upgrade_rules.json：武器強化內 MAT_k_01→MAT_k_03、MAT_k_02→MAT_k_04（其餘尾碼不改）。

對應 120 隻魔物 MON_k ↔ WEP_k；護甲編號 ARM_k。
沿用 sync_equipment_to_monsters.short_name、equire_attr。
"""
from __future__ import annotations

import copy
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MON = ROOT / "DesignData" / "01_Monsters" / "monsters.json"
EQ = ROOT / "DesignData" / "02_Equipment" / "equipment.json"
UP = ROOT / "DesignData" / "02_Equipment" / "upgrade_rules.json"
ARM = ROOT / "DesignData" / "02_Equipment" / "armor.json"


def short_name(name: str) -> str:
    base = name.split("(")[0].strip()
    if "：" in base:
        return base.split("：")[-1].strip()
    return base


def equip_attr(attrs: list | None) -> str:
    if not attrs:
        return "無"
    if len(attrs) > 1:
        return "龍"
    return attrs[0]


def phys_elem_damage(m: dict) -> tuple[int, int]:
    star = int(m["星級"])
    hp = float(m["最大血量"])
    phys = int(280 + star * 72 + hp / 2200)
    ea = equip_attr(m.get("屬性") or [])
    elem = 0 if ea == "無" else int(phys * 0.21 + star * 12)
    return max(phys, 200), elem


def craft_gold(m: dict) -> int:
    return int(400 + int(m["星級"]) * 780 + float(m["最大血量"]) / 180)


def parse_mat_suffix(mat_id: str) -> tuple[str, str] | None:
    """('001', '01') from MAT_001_01"""
    m = re.fullmatch(r"MAT_(\d{3})_(\d{2})", mat_id or "")
    if not m:
        return None
    return m.group(1), m.group(2)


def remap_upgrade_mat_id(mid: str) -> str:
    """武器強化表：稀釋易得尾碼→難取得尾碼。"""
    p = parse_mat_suffix(mid.strip())
    if not p:
        return mid
    k, suf = p
    if suf == "01":
        suf = "03"
    elif suf == "02":
        suf = "04"
    return f"MAT_{k}_{suf}"


def remap_weapon_upgrade_tree(obj):
    if isinstance(obj, dict):
        for kk, vv in obj.items():
            if kk == "素材編號" and isinstance(vv, str):
                obj[kk] = remap_upgrade_mat_id(vv)
            else:
                remap_weapon_upgrade_tree(vv)
    elif isinstance(obj, list):
        for x in obj:
            remap_weapon_upgrade_tree(x)


def merge_material_list(items: list | None) -> list:
    """合併同素材編號的需求數量（維持出現順序）。"""
    if not items:
        return []
    acc: dict[str, int] = {}
    order: list[str] = []
    for it in items:
        if not isinstance(it, dict):
            continue
        mid = str(it.get("素材編號", "")).strip()
        if not mid:
            continue
        q = int(it.get("需求數量", 0))
        if mid not in acc:
            order.append(mid)
            acc[mid] = 0
        acc[mid] += q
    return [{"素材編號": m, "需求數量": acc[m]} for m in order]


def merge_weapon_upgrade_duplicate_mats(rows: list):
    for rule in rows or []:
        for step in rule.get("升級路徑") or []:
            cost = step.get("升級花費")
            if not cost or not isinstance(cost, dict):
                continue
            ml = cost.get("需求素材")
            if isinstance(ml, list) and ml:
                cost["需求素材"] = merge_material_list(ml)


def build_weapon_recipe_easy_to_hard(monster_idx: int, old_mats: list) -> list[dict]:
    """
    舊配方（易）→新配方（難）。保留總量感：以 _03/_04/_05 承接。
    """
    k = monster_idx
    q01 = q02 = q05 = 0
    already_hard = False
    for it in old_mats or []:
        if not isinstance(it, dict):
            continue
        mid = str(it.get("素材編號", "")).strip()
        qty = int(it.get("需求數量", 0))
        parsed = parse_mat_suffix(mid)
        if not parsed:
            continue
        if parsed[1] in ("03", "04", "05"):
            already_hard = True
        if parsed[1] == "01":
            q01 = qty
        elif parsed[1] == "02":
            q02 = qty
        elif parsed[1] == "05":
            q05 = qty
    if already_hard and q01 == 0 and q02 == 0:
        return [
            {key: val for key, val in dict(it).items() if key in ("素材編號", "需求數量")}
            for it in (old_mats or [])
            if isinstance(it, dict) and it.get("素材編號")
        ]
    total = max(1, q01 + q02)
    star = monster_idx_to_star.get(k, 1)
    w3 = max(8, min(38, int(0.52 * total) + star * 2))
    w4 = max(5, min(28, int(0.38 * total) + max(1, star)))
    w5 = max(q05, 1)
    return [
        {"素材編號": f"MAT_{k:03d}_03", "需求數量": w3},
        {"素材編號": f"MAT_{k:03d}_04", "需求數量": w4},
        {"素材編號": f"MAT_{k:03d}_05", "需求數量": w5},
    ]


def build_armor_recipe_easy(monster_idx: int, old_mats: list) -> list[dict]:
    k = monster_idx
    q01 = 12
    q02 = 8
    for it in old_mats or []:
        if not isinstance(it, dict):
            continue
        mid = str(it.get("素材編號", "")).strip()
        qty = int(it.get("需求數量", 0))
        parsed = parse_mat_suffix(mid)
        if not parsed:
            continue
        if parsed[1] == "01":
            q01 = max(10, qty)
        elif parsed[1] == "02":
            q02 = max(6, qty)
    star = monster_idx_to_star.get(k, 1)
    a1 = max(10, q01 + star)
    a2 = max(6, q02 + star // 2)
    return [
        {"素材編號": f"MAT_{k:03d}_01", "需求數量": a1},
        {"素材編號": f"MAT_{k:03d}_02", "需求數量": a2},
    ]


monster_idx_to_star: dict[int, int] = {}


def main() -> None:
    monsters = json.loads(MON.read_text(encoding="utf-8"))
    equip_rows = json.loads(EQ.read_text(encoding="utf-8"))
    up_rows = json.loads(UP.read_text(encoding="utf-8"))

    assert len(monsters) >= len(equip_rows), (len(monsters), len(equip_rows))

    global monster_idx_to_star
    monster_idx_to_star.clear()
    for i, mon in enumerate(monsters, start=1):
        monster_idx_to_star[i] = int(mon["星級"])

    by_wep = {r["裝備編號"]: r for r in equip_rows}
    new_armor: list = []

    for k in range(1, len(equip_rows) + 1):
        wep_id = f"WEP_{k:03d}"
        row = copy.deepcopy(by_wep[wep_id])
        mats = row.get("合成配方", {}).get("需求素材") or []
        monster = monsters[k - 1]
        assert monster["魔物編號"] == f"MON_{k:03d}", monster["魔物編號"]

        # === 武器：難掉落 ===
        row["合成配方"]["需求素材"] = build_weapon_recipe_easy_to_hard(k, mats)
        for m in row["合成配方"]["需求素材"]:
            m.pop("素材名稱", None)

        by_wep[wep_id] = row

        # === 護甲：易掉落 ===
        p, el = phys_elem_damage(monster)
        d_phys = max(140, min(980, int(p * 0.32)))
        d_elem = max(0, min(320, int(el * 0.30)))
        armor_gold = max(260, int(craft_gold(monster) * 0.62))

        arm_row = {
            "裝備編號": f"ARM_{k:03d}",
            "名稱": short_name(monster["名稱"]) + "護甲",
            "裝備類型": "護甲",
            "星級": int(monster["星級"]),
            "裝備屬性": equip_attr(monster.get("屬性") or []),
            "基礎數值": {
                "物理傷害": 0,
                "屬性傷害": 0,
                "物理防御": d_phys,
                "屬性防御": d_elem,
            },
            "圖片路徑": f"Assets/Textures/Equipment/ARM_{k:03d}.png",
            "圖示路徑": f"Assets/Textures/Equipment/ARM_{k:03d}.png",
            "合成配方": {
                "所需金幣": armor_gold,
                "需求素材": build_armor_recipe_easy(k, mats),
            },
        }
        new_armor.append(arm_row)

    out_equip = [by_wep[f"WEP_{k:03d}"] for k in range(1, len(equip_rows) + 1)]
    EQ.write_text(json.dumps(out_equip, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")

    new_up = copy.deepcopy(up_rows)
    remap_weapon_upgrade_tree(new_up)
    merge_weapon_upgrade_duplicate_mats(new_up)
    UP.write_text(json.dumps(new_up, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")

    ARM.write_text(json.dumps(new_armor, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")
    print("wrote", EQ, "weapon recipes -> MAT *_03,*_04,*_05")
    print("wrote", UP, "weapon upgrade remap 01→03, 02→04")
    print("wrote", ARM, "armor x", len(new_armor), " MAT *_01,*_02")


if __name__ == "__main__":
    main()
