# -*- coding: utf-8 -*-
"""依 DesignData/01_Monsters/monsters.json 產生對齊的 drop_rates.json。"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MONSTERS = ROOT / "DesignData" / "01_Monsters" / "monsters.json"
OUT = ROOT / "DesignData" / "01_Monsters" / "drop_rates.json"


def short_name(name: str) -> str:
    base = name.split("(")[0].strip()
    if "：" in base:
        return base.split("：")[-1].strip()
    return base


def main():
    rows = json.loads(MONSTERS.read_text(encoding="utf-8"))
    if len(rows) != 120:
        raise SystemExit(f"預期 120 隻魔物，實際 {len(rows)}")

    template = [
        ("01", "鱗", "基本擊殺", 0.65),
        ("02", "甲殼", "基本擊殺", 0.4),
        ("03", "尖爪", "破壞部位", 0.3),
        ("04", "尾巴", "切斷尾巴", 0.7),
        ("05", "逆鱗", "破壞部位", 0.03),
    ]

    out = []
    for m in rows:
        mid = m["魔物編號"]
        idx = mid.split("_")[1]
        sn = short_name(m["名稱"])
        for suf, part, cond, rate in template:
            out.append(
                {
                    "魔物編號": mid,
                    "素材編號": f"MAT_{idx}_{suf}",
                    "素材名稱": f"{sn}的{part}",
                    "掉落機率": rate,
                    "掉落條件": cond,
                }
            )

    OUT.write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Wrote", len(out), "rows ->", OUT)


if __name__ == "__main__":
    main()
