# -*- coding: utf-8 -*-
"""從 equipment.json 合成配方移除「素材名稱」，改由 drop_rates + MaterialIdDisplay 解析。"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EQ = ROOT / "DesignData" / "02_Equipment" / "equipment.json"


def main():
    data = json.loads(EQ.read_text(encoding="utf-8"))
    n = 0
    for item in data:
        recipe = item.get("合成配方")
        if not recipe:
            continue
        for mat in recipe.get("需求素材") or []:
            if isinstance(mat, dict) and "素材名稱" in mat:
                del mat["素材名稱"]
                n += 1
    EQ.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Removed 素材名稱 from", n, "recipe rows ->", EQ)


if __name__ == "__main__":
    main()
