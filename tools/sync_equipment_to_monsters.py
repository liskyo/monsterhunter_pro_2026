# -*- coding: utf-8 -*-
"""
將 equipment.json / upgrade_rules.json 與 monsters.json（120 隻）對齊：
- WEP_k ↔ MON_k、合成／升級素材 MAT_k_*。
- 1–100：保留原有武器類型與素材列結構，更新名稱、星級、屬性、數值、金幣、圖檔路徑。
- 101–120：以 WEP_{((k-1)%100)+1} 為模板複製並改編號與 MAT 前綴。
"""
from __future__ import annotations

import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MON = ROOT / "DesignData" / "01_Monsters" / "monsters.json"
EQ = ROOT / "DesignData" / "02_Equipment" / "equipment.json"
UP = ROOT / "DesignData" / "02_Equipment" / "upgrade_rules.json"


def short_name(name: str) -> str:
    base = name.split("(")[0].strip()
    if "：" in base:
        return base.split("：")[-1].strip()
    return base


def equip_attr(attrs: list) -> str:
    if not attrs:
        return "無"
    if len(attrs) > 1:
        return "龍"
    return attrs[0]


def remap_mat_ids(obj, src: int, dst: int) -> None:
    pre_s = f"MAT_{src:03d}_"
    pre_d = f"MAT_{dst:03d}_"
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k == "素材編號" and isinstance(v, str) and v.startswith(pre_s):
                obj[k] = pre_d + v[len(pre_s) :]
            else:
                remap_mat_ids(v, src, dst)
    elif isinstance(obj, list):
        for x in obj:
            remap_mat_ids(x, src, dst)


def phys_elem(m: dict) -> tuple[int, int]:
    star = m["星級"]
    hp = m["最大血量"]
    phys = int(280 + star * 72 + hp / 2200)
    ea = equip_attr(m["屬性"])
    elem = 0 if ea == "無" else int(phys * 0.21 + star * 12)
    return max(phys, 200), elem


def craft_gold(m: dict) -> int:
    return int(400 + m["星級"] * 780 + m["最大血量"] / 180)


def scale_upgrade_gold(rule: dict, m: dict, ref: dict) -> None:
    """依合成金幣比例調整升級花費金幣（複製模板時避免高星武器升級過便宜）。"""
    r = craft_gold(m) / max(1, craft_gold(ref))
    for step in rule.get("升級路徑") or []:
        cost = step.get("升級花費")
        if not cost or "金幣" not in cost:
            continue
        cost["金幣"] = max(100, int(cost["金幣"] * r))


def main() -> None:
    monsters = json.loads(MON.read_text(encoding="utf-8"))
    equip_raw = json.loads(EQ.read_text(encoding="utf-8"))
    up_raw = json.loads(UP.read_text(encoding="utf-8"))

    assert len(monsters) == 120, len(monsters)
    # 以 WEP_001–100 為武器類型／配方結構模板（檔案若已 120 筆仍只取前 100）
    equip = equip_raw[:100]
    up = up_raw[:100]
    assert len(equip) == 100, len(equip)
    assert len(up) == 100, len(up)

    by_wep = {e["裝備編號"]: e for e in equip}
    by_rule = {u["裝備編號"]: u for u in up}

    new_equip: list = []
    new_up: list = []

    for k in range(1, 121):
        src = ((k - 1) % 100) + 1
        m = monsters[k - 1]
        assert m["魔物編號"] == f"MON_{k:03d}", m["魔物編號"]

        if k <= 100:
            e = copy.deepcopy(equip[k - 1])
            u = copy.deepcopy(up[k - 1])
        else:
            e = copy.deepcopy(by_wep[f"WEP_{src:03d}"])
            u = copy.deepcopy(by_rule[f"WEP_{src:03d}"])
            remap_mat_ids(e, src, k)
            remap_mat_ids(u, src, k)

        wtype = e["裝備類型"]
        e["裝備編號"] = f"WEP_{k:03d}"
        e["名稱"] = short_name(m["名稱"]) + wtype
        e["星級"] = m["星級"]
        e["裝備屬性"] = equip_attr(m["屬性"])
        p, el = phys_elem(m)
        e["基礎數值"]["物理傷害"] = p
        e["基礎數值"]["屬性傷害"] = el
        e["圖片路徑"] = f"Assets/Textures/Equipment/WEP_{k:03d}.png"
        e["圖示路徑"] = f"Assets/Textures/Equipment/WEP_{k:03d}.png"
        e["合成配方"]["所需金幣"] = craft_gold(m)
        for mat in e["合成配方"].get("需求素材") or []:
            if isinstance(mat, dict):
                mat.pop("素材名稱", None)

        u["裝備編號"] = f"WEP_{k:03d}"
        ref_m = monsters[src - 1]
        scale_upgrade_gold(u, m, ref_m)

        new_equip.append(e)
        new_up.append(u)

    EQ.write_text(json.dumps(new_equip, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    UP.write_text(json.dumps(new_up, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Wrote", len(new_equip), "equipment,", len(new_up), "upgrade blocks")


if __name__ == "__main__":
    main()
