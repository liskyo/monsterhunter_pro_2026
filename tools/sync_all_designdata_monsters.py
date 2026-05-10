# -*- coding: utf-8 -*-
"""
將 DesignData 內依魔物編號／MAT 前綴的 JSON 與 monsters.json（120）對齊。
- materials.json：魔物 MAT_###_## 全量依 drop_rates 重建；保留 ITM／MAT_COM 等。
- quests.json：QST_k ↔ MON_k（至多 120 筆）；不足則自 QST_100 模板補上。
- monster_traces.json：TRC_k ↔ MON_k（至多 120）。
- pets.json：依「對應魔物編號」更新顯示名（艾路／加爾克後綴分開）。
- canteen.json：依主題對應表重對 MAT 魔物編號、移除需求素材「名稱」、修正金幣錯字。
"""
from __future__ import annotations

import copy
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MON_PATH = ROOT / "DesignData" / "01_Monsters" / "monsters.json"
DROP_PATH = ROOT / "DesignData" / "01_Monsters" / "drop_rates.json"
MAT_PATH = ROOT / "DesignData" / "04_Items" / "materials.json"
QST_PATH = ROOT / "DesignData" / "05_Systems" / "quests.json"
TRC_PATH = ROOT / "DesignData" / "04_Items" / "monster_traces.json"
PET_PATH = ROOT / "DesignData" / "05_Systems" / "pets.json"
CAT_PATH = ROOT / "DesignData" / "05_Systems" / "canteen.json"

MAT_MON_RE = re.compile(r"^MAT_(\d{3})_(\d{2})$")

# 貓飯：料理編號 → 對應魔物編號（1-based，與 MON_XXX 數字一致）；僅列需魔物 MAT 的項目
CANTEEN_TARGET_MON = {
    "FD_003": 36,
    "FD_004": 41,
    "FD_006": 38,
    "FD_007": 33,
    "FD_008": 66,
    "FD_009": 43,
    "FD_010": 67,
    "FD_011": 53,
    "FD_012": 63,
    "FD_013": 65,
    "FD_015": 69,
    "FD_016": 12,
    "FD_018": 39,
    "FD_019": 118,
    "FD_020": 35,
    "FD_021": 21,
    "FD_022": 51,
    "FD_023": 56,
    "FD_024": 61,
    "FD_025": 60,
    "FD_026": 9,
    "FD_027": 54,
    "FD_028": 28,
    "FD_029": 13,
}


def short_name(name: str) -> str:
    base = name.split("(")[0].strip()
    if "：" in base:
        return base.split("：")[-1].strip()
    return base


def rarity_and_price(suf: str) -> tuple[int, int]:
    r = {"01": 1, "02": 2, "03": 3, "04": 3, "05": 5}[suf]
    p = {1: 120, 2: 250, 3: 500, 5: 5000}[r]
    return r, p


def build_material_rows(monsters: list, drops: list) -> list[dict]:
    mon_by_id = {m["魔物編號"]: m for m in monsters}
    rows = []
    for d in drops:
        mid = d["素材編號"]
        m = mon_by_id.get(d["魔物編號"])
        if not m:
            raise SystemExit(f"drop_rates 魔物編號無對應: {d['魔物編號']}")
        sn = short_name(m["名稱"])
        disp = d["素材名稱"]
        part = disp.split("的")[-1] if "的" in disp else "素材"
        suf = mid.split("_")[-1]
        r, price = rarity_and_price(suf)
        rows.append(
            {
                "素材編號": mid,
                "名稱": disp,
                "稀有度": r,
                "分類": "魔物素材",
                "描述": f"從{sn}身上取得的{part}，可用於鍛造與強化。",
                "出售價格": price,
                "攜帶上限": 99 if r == 5 else 999,
            }
        )
    return rows


def sync_materials(monsters: list, drops: list) -> None:
    data = json.loads(MAT_PATH.read_text(encoding="utf-8"))
    head = [x for x in data if not MAT_MON_RE.match(x.get("素材編號", ""))]
    tail = build_material_rows(monsters, drops)
    MAT_PATH.write_text(
        json.dumps(head + tail, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print("materials.json:", len(head), "non-monster +", len(tail), "monster mats")


def sync_quests(monsters: list) -> None:
    data = json.loads(QST_PATH.read_text(encoding="utf-8"))
    if len(data) == 100:
        tmpl = copy.deepcopy(data[99])
        ref_hp = monsters[99]["最大血量"]
        ref_coin = tmpl["報酬"]["金幣"]
        for k in range(101, 121):
            q = copy.deepcopy(tmpl)
            m = monsters[k - 1]
            q["任務編號"] = f"QST_{k:03d}"
            q["前置任務要求"] = f"QST_{k - 1:03d}"
            q["星級"] = m["星級"]
            sn = short_name(m["名稱"])
            q["標題"] = f"{m['星級']}星：{sn}的『生態調查』"
            for t in q.get("目標魔物") or []:
                t["魔物編號"] = f"MON_{k:03d}"
                t["魔物名稱"] = m["名稱"]
            q["描述"] = (
                f"觀測到{sn}活動加劇，已影響周邊生態。請獵人前往調查並依公會指示處置。"
            )
            ratio = m["最大血量"] / max(1, ref_hp)
            q["報酬"]["金幣"] = int(ref_coin * ratio)
            data.append(q)

    for i, q in enumerate(data[:120], start=1):
        m = monsters[i - 1]
        sn = short_name(m["名稱"])
        q["星級"] = m["星級"]
        q["標題"] = f"{m['星級']}星：{sn}的『生態調查』"
        for t in q.get("目標魔物") or []:
            t["魔物編號"] = f"MON_{i:03d}"
            t["魔物名稱"] = m["名稱"]
        q["描述"] = (
            f"觀測到{sn}活動加劇，已影響周邊生態。請獵人前往調查並依公會指示處置。"
        )
    QST_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("quests.json:", len(data), "rows")


def trace_title_variant(idx: int, sn: str) -> str:
    v = idx % 3
    if v == 0:
        return f"{sn}的清晰足跡"
    if v == 1:
        return f"{sn}的古老粘液"
    return f"{sn}的調查殘跡"


def sync_traces(monsters: list) -> None:
    data = json.loads(TRC_PATH.read_text(encoding="utf-8"))
    if len(data) == 100:
        tmpl = copy.deepcopy(data[99])
        for k in range(101, 121):
            tr = copy.deepcopy(tmpl)
            tr["痕跡編號"] = f"TRC_{k:03d}"
            data.append(tr)

    for i, tr in enumerate(data[:120], start=1):
        m = monsters[i - 1]
        sn = short_name(m["名稱"])
        tr["對應魔物編號"] = f"MON_{i:03d}"
        tr["魔物星級"] = m["星級"]
        tr["名稱"] = trace_title_variant(i, sn)
        tr["描述"] = (
            f"與{sn}相關的調查痕跡。配合適合星級的染色球可提高追蹤效率。"
        )
        tr["取得途徑"] = f"擊敗{sn}後有機率取得，或於任務結算獎勵中取得。"
    TRC_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("monster_traces.json:", len(data), "rows")


def sync_pets(monsters: list) -> None:
    data = json.loads(PET_PATH.read_text(encoding="utf-8"))
    for p in data:
        mid = p.get("對應魔物編號") or ""
        if not mid.startswith("MON_"):
            continue
        try:
            n = int(mid.split("_")[1])
        except (IndexError, ValueError):
            continue
        if not 1 <= n <= len(monsters):
            continue
        m = monsters[n - 1]
        sn = short_name(m["名稱"])
        suf = "風加爾克" if p.get("種類") == "加爾克" else "風艾路"
        p["對應魔物編號"] = f"MON_{n:03d}"
        p["名稱"] = f"{sn}{suf}"
    PET_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("pets.json:", len(data), "rows")


def remap_mat_mon(mat_id: str, new_mon: int) -> str:
    m = MAT_MON_RE.match(mat_id)
    if not m:
        return mat_id
    suf = m.group(2)
    return f"MAT_{new_mon:03d}_{suf}"


def sync_canteen(monsters: list) -> None:
    data = json.loads(CAT_PATH.read_text(encoding="utf-8"))
    for dish in data:
        fid = dish.get("料理編號")
        cost = dish.get("花費") or {}
        # 修正錯字「金號」
        if "金號" in cost and "金幣" not in cost:
            cost["金幣"] = cost.pop("金號")
        target_mon = CANTEEN_TARGET_MON.get(fid)
        for mat in cost.get("需求素材") or []:
            if not isinstance(mat, dict):
                continue
            mid = mat.get("素材編號", "")
            mat.pop("名稱", None)
            if target_mon is None:
                continue
            if not MAT_MON_RE.match(mid):
                continue
            mat["素材編號"] = remap_mat_mon(mid, target_mon)
    CAT_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("canteen.json:", len(data), "rows")


def sync_playerstate_samples(monsters: list) -> None:
    enc = ROOT / "PlayerState" / "player_encyclopedia_progress.json"
    if enc.exists():
        d = json.loads(enc.read_text(encoding="utf-8"))
        for x in d.get("discovered_monsters") or []:
            mid = x.get("monster_id", "")
            if mid.startswith("MON_"):
                try:
                    n = int(mid.split("_")[1])
                except (IndexError, ValueError):
                    continue
                if 1 <= n <= len(monsters):
                    m = monsters[n - 1]
                    x["monster_name"] = m["名稱"]
        enc.write_text(json.dumps(d, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("updated", enc.name)


def main() -> None:
    monsters = json.loads(MON_PATH.read_text(encoding="utf-8"))
    drops = json.loads(DROP_PATH.read_text(encoding="utf-8"))
    assert len(monsters) == 120
    assert len(drops) == 600

    sync_materials(monsters, drops)
    sync_quests(monsters)
    sync_traces(monsters)
    sync_pets(monsters)
    sync_canteen(monsters)
    sync_playerstate_samples(monsters)
    print("Done.")


if __name__ == "__main__":
    main()
