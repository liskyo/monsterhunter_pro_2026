# -*- coding: utf-8 -*-
"""一次性腳本：依 monsterlist.json 產生 120 隻 monsters.json。"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# 每隻魔物主屬性（1-based 索引）；與 monsterlist 順序對齊
ELEM = (
    "無", "無", "無", "無", "無", "冰", "火", "火", "毒", "水",
    "無", "無", "無", "無", "無", "無", "水", "火", "水", "雷",
    "火", "毒", "無", "冰", "無", "無", "毒", "龍", "冰", "火",
    "毒", "冰", "無", "雷", "水", "無", "火", "火", "無", "火",
    "雷", "無", "火", "火", "無", "火", "無", "火", "火", "火",
    "無", "雷", "龍", "火", "火", "龍", "無", "冰", "水", "冰",
    "火", "龍", "雷", "龍", "雷", "龍", "冰", "龍", "龍", "雷",
    "龍", "龍", "水", "龍", "火", "火", "龍", "火", "龍", "火",
    "雷", "火", "火", "龍", "龍", "冰", "水", "龍", "無", "無",
    "龍", "無", "龍", "無", "無", "火", "無", "水", "雷", "冰",
    "龍", "龍", "冰", "火", "龍", "龍", "龍", "龍", "龍", "火",
    "龍", "火", "無", "無", "龍", "龍", "龍", "龍", "龍", "龍",
)

# 主題內異常（名稱, 每秒傷害, 觸發機率, 持續時間秒）
POOLS = {
    "火": [
        ("燃燒", 28, 0.25, 8.0),
        ("灼熱", 22, 0.2, 6.0),
        ("延燒", 18, 0.18, 7.0),
        ("熔蝕", 26, 0.22, 5.0),
    ],
    "水": [
        ("水纏", 14, 0.22, 7.0),
        ("溺壓", 18, 0.2, 6.0),
        ("潮蝕", 16, 0.18, 8.0),
        ("卷流", 20, 0.15, 5.0),
    ],
    "冰": [
        ("凍結", 0, 0.18, 3.5),
        ("冰結", 22, 0.2, 6.0),
        ("霜咬", 24, 0.22, 5.0),
        ("寒潮", 30, 0.2, 7.0),
    ],
    "雷": [
        ("麻痺", 0, 0.15, 3.5),
        ("感電", 24, 0.2, 6.0),
        ("落雷", 32, 0.12, 4.0),
        ("雷蝕", 20, 0.18, 5.5),
    ],
    "龍": [
        ("龍蝕", 26, 0.2, 8.0),
        ("龍壓", 0, 0.12, 3.0),
        ("蝕傷", 22, 0.22, 6.0),
        ("瘴蝕", 28, 0.18, 7.0),
    ],
    "毒": [
        ("中毒", 20, 0.25, 8.0),
        ("劇毒", 28, 0.2, 5.0),
        ("毒蝕", 24, 0.18, 6.0),
        ("猛毒", 32, 0.15, 4.0),
    ],
    "無": [
        ("裂傷", 26, 0.22, 8.0),
        ("震盪", 0, 0.15, 2.5),
        ("疲勞", 10, 0.2, 6.0),
        ("眩暈", 0, 0.1, 2.0),
    ],
}

WEAK = {"火": "水", "水": "雷", "冰": "火", "雷": "冰", "龍": "龍", "毒": "火", "無": "龍"}


def stats_one_based(i: int):
    if i <= 30:
        star = max(1, min(4, 1 + (i - 1) // 10))
        hp = 4200 + i * 300
        dmg = 75 + i * 7
    elif i <= 80:
        star = max(4, min(6, 4 + (i - 31) // 25))
        hp = 11500 + (i - 30) * 240
        dmg = 165 + (i - 30) * 6
    elif i <= 100:
        star = max(7, min(8, 7 + (i - 81) // 10))
        hp = 23500 + (i - 80) * 650
        dmg = 290 + (i - 80) * 10
    else:
        star = max(9, min(10, 9 + (i - 101) // 10))
        hp = 36000 + (i - 100) * 2200
        dmg = 420 + (i - 100) * 38
    return star, hp, dmg


def anomaly_count(i: int) -> int:
    if i <= 30:
        return 1
    if i <= 80:
        return 2
    if i <= 100:
        return 3
    return 4


def build_anomalies(primary: str, name: str, i: int):
    n = name
    pool = list(POOLS[primary])
    # 眠狗龍：在「無」主題下插入睡眠
    if "眠狗" in n and primary == "無":
        pool = [
            ("睡眠", 0, 0.2, 5.0),
            ("疲勞", 10, 0.2, 6.0),
            ("裂傷", 24, 0.22, 7.0),
            ("震盪", 0, 0.12, 2.5),
        ]
    # 煌黑龍：多屬性主題（仍各條目有持續時間）
    if "煌黑" in n:
        pool = [
            ("劫火", 30, 0.2, 6.0),
            ("霜蝕", 22, 0.18, 6.0),
            ("雷殛", 28, 0.18, 5.0),
            ("龍蝕", 26, 0.2, 8.0),
        ]
    k = anomaly_count(i)
    return [
        {
            "異常屬性": pool[j][0],
            "每秒傷害": pool[j][1],
            "觸發機率": pool[j][2],
            "持續時間秒": pool[j][3],
        }
        for j in range(k)
    ]


def main():
    assert len(ELEM) == 120, len(ELEM)
    raw = (ROOT / "monsterlist.json").read_text(encoding="utf-8")
    ml = json.loads(raw)
    assert len(ml) == 120

    out = []
    for idx, row in enumerate(ml, start=1):
        mid = row["魔物編號"]
        name = row["名稱"]
        assert mid == f"MON_{idx:03d}", (mid, idx)
        primary = ELEM[idx - 1]
        attrs = [primary]
        if "煌黑" in name:
            attrs = ["火", "冰", "雷"]
        star, hp, dmg = stats_one_based(idx)
        atk_range = 4.0 + min(10, star) * 0.35
        weak_elem = WEAK[primary]
        bonus = 1.35 + (star - 1) * 0.02
        area = round(26.0 + star * 1.15, 1)

        entry = {
            "魔物編號": mid,
            "名稱": name,
            "星級": star,
            "屬性": attrs,
            "弱點": [{"屬性": weak_elem, "傷害加成比例": round(bonus, 2)}],
            "身材面積": area,
            "最大血量": int(hp),
            "魔物攻擊內容": {
                "普通攻擊": {
                    "傷害": int(dmg),
                    "攻擊距離": round(atk_range, 1),
                },
                "特殊攻擊": build_anomalies(primary, name, idx),
            },
            "圖片路徑": f"Assets/Textures/Monsters/{mid}_全身圖.png",
            "圖示路徑": f"Assets/UI/Icons/Monsters/{mid}_圖示.png",
        }
        out.append(entry)

    path = ROOT / "DesignData" / "01_Monsters" / "monsters.json"
    path.write_text(
        json.dumps(out, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print("Wrote", path, "entries", len(out))


if __name__ == "__main__":
    main()
