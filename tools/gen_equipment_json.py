# -*- coding: utf-8 -*-
"""一次性：由 monsters + drop_rates 邏輯生成 equipment.json。"""
import json
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# 與 DesignData/03_Combat/weapon_movesets.json 的「武器類型」欄位一致（勿用「輕弩槍」）
CANON = ["大劍", "大錘", "太刀", "雙劍", "操蟲棍", "輕弩", "重弩", "弓"]
# 用於補滿「屬性種類」數；魔物本身有的異常（如毒）優先保留於戰鬥標籤
ELEMENT_POOL = ["火", "水", "雷", "冰", "龍"]


def elem_count(mi: int) -> int:
    """魔物編號 1–60 一種、61–100 兩種、101–120 三種（依編號分段，非星級）。"""
    if mi <= 60:
        return 1
    if mi <= 100:
        return 2
    return 3


def elements_for(mrow: dict, mi: int, n: int) -> list:
    attrs = [a for a in (mrow.get("屬性") or []) if a and str(a).strip() != "無"]
    out = []
    for a in attrs:
        if a not in out:
            out.append(a)
        if len(out) >= n:
            break
    k = 0
    while len(out) < n:
        e = ELEMENT_POOL[(mi * 3 + k) % len(ELEMENT_POOL)]
        if e not in out:
            out.append(e)
        k += 1
    return out[:n]


def type_phys_mul(t: str) -> float:
    return {
        "大劍": 1.18,
        "大錘": 1.12,
        "太刀": 1.0,
        "雙劍": 0.82,
        "操蟲棍": 0.88,
        "輕弩": 0.72,
        "重弩": 1.22,
        "弓": 0.92,
    }[t]


def recipe(mid3: str, star: int, with_rare: bool) -> list:
    ms = [f"MAT_{mid3}_{j:02d}" for j in range(1, 6)]
    a, b, c, d, e = ms
    if with_rare:
        return [
            {"素材編號": c, "需求數量": 8 + star},
            {"素材編號": d, "需求數量": 6 + star // 2},
            {"素材編號": e, "需求數量": 2 if star >= 9 else 1},
        ]
    return [
        {"素材編號": a, "需求數量": 5 + star // 2},
        {"素材編號": c, "需求數量": 6 + star},
        {"素材編號": d, "需求數量": 5 + star // 2},
    ]


def gold(star: int, nqty: int, rare: bool) -> int:
    return int(900 + star * 140 + nqty * 22 + (200 if rare else 0))


def main():
    monsters = json.loads(
        (ROOT / "DesignData" / "01_Monsters" / "monsters.json").read_text(encoding="utf-8")
    )
    mon_by_id = {m["魔物編號"]: m for m in monsters}

    out = []
    wid = 0
    for mi in range(1, 121):
        mid = f"MON_{mi:03d}"
        mrow = mon_by_id[mid]
        name = mrow["名稱"]
        star = int(mrow["星級"])
        ec = elem_count(mi)
        els = elements_for(mrow, mi, ec)
        attr_str = "、".join(els)
        rng = random.Random(424242 + mi * 10007)
        pick = rng.sample(CANON, 5)
        pick.sort(key=lambda t: CANON.index(t))
        rare_idx = rng.randrange(5)
        ne = len(els)
        base_elem = int(28 + star * 11)
        elem_dmg = int(base_elem * (1.0 + 0.4 * max(0, ne - 1)))

        for j, t in enumerate(pick):
            wid += 1
            rare = j == rare_idx
            mats = recipe(f"{mi:03d}", star, rare)
            nqty = sum(x["需求數量"] for x in mats)
            phys = int((210 + star * 48) * type_phys_mul(t))
            ed = elem_dmg if ne and attr_str else 0
            out.append(
                {
                    "裝備編號": f"WEP_{wid:03d}",
                    "名稱": f"{name}{t}",
                    "裝備類型": t,
                    "星級": star,
                    "裝備屬性": attr_str,
                    "基礎數值": {"物理傷害": phys, "屬性傷害": ed},
                    "圖片路徑": f"Assets/Textures/Equipment/WEP_{wid:03d}.png",
                    "圖示路徑": f"Assets/Textures/Equipment/WEP_{wid:03d}.png",
                    "合成配方": {"所需金幣": gold(star, nqty, rare), "需求素材": mats},
                }
            )

    path = ROOT / "DesignData" / "02_Equipment" / "equipment.json"
    path.write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
    rare_ct = sum(
        1
        for r in out
        if any(m["素材編號"].endswith("_05") for m in r["合成配方"]["需求素材"])
    )
    print("wrote", path)
    print("rows", len(out), "_05 recipes", rare_ct)
    for mi in [1, 60, 61, 100, 101, 120]:
        print("MON sample", mi, out[(mi - 1) * 5]["裝備屬性"])


if __name__ == "__main__":
    main()
