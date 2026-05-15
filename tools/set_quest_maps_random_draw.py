#!/usr/bin/env python3
"""為 quests.json 每筆任務加上「地圖抽取」「依星級隨機」。
用法：在倉庫根執行  python tools/set_quest_maps_random_draw.py
"""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
QUESTS = ROOT / "DesignData" / "05_Systems" / "quests.json"
MODE = "依星級隨機"


def main() -> int:
    if not QUESTS.exists():
        print(f"找不到 {QUESTS}", file=sys.stderr)
        return 1
    txt = QUESTS.read_text(encoding="utf-8")
    data = json.loads(txt)
    if not isinstance(data, list):
        print("quests.json 頂層須為陣列。", file=sys.stderr)
        return 1
    n = 0
    for row in data:
        if isinstance(row, dict):
            row["地圖抽取"] = MODE
            n += 1
    tmp = QUESTS.with_suffix(".json.tmp")
    tmp.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    tmp.replace(QUESTS)
    print(f"已更新 {n} 筆任務：地圖抽取 = {MODE}（quests.json）。地圖池：DesignData/05_Systems/quest_map_pools_by_star.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
