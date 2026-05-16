# -*- coding: utf-8 -*-
"""產生 weapon_movesets.json：8 武器 × 4 星級區間（招式數 2/3/4/5）。"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "DesignData" / "03_Combat" / "weapon_movesets.json"

# 星級區間：(下限, 上限, 點擊段數, 是否長按, 是否專屬) → 招式數 = n_tap + int(ch) + int(sk)
TIERS = [
    (1, 3, 2, False, False),
    (4, 6, 2, True, False),
    (7, 9, 2, True, True),
    (10, 10, 3, True, True),
]


def tap_entry(name: str, mv: float, reach: float, **extra) -> dict:
    d = {"招式名稱": name, "動作倍率": mv, "攻擊距離": reach}
    d.update(extra)
    return d


def charge_standard(name: str, m1: float, m2: float, m3: float, t1: float, t2: float, t3: float) -> dict:
    return {
        "招式名稱": name,
        "分段倍率": {"一段": m1, "二段": m2, "三段": m3},
        "蓄力時間_秒": [t1, t2, t3],
        "震動強度": 0.75,
    }


def skill_entry(name: str, cd: int, mv: float, desc: str, **kw) -> dict:
    d = {"招式名稱": name, "冷卻時間": cd, "動作倍率": mv, "描述": desc}
    d.update(kw)
    return d


def row(wtype: str, desc: str, lo: int, hi: int, taps: list, charge: dict | None, skill: dict | None) -> dict:
    cfg = {"點擊": taps}
    if charge:
        cfg["長按"] = charge
    if skill:
        cfg["專屬技能"] = skill
    return {
        "武器類型": wtype,
        "核心機制": desc,
        "星級下限": lo,
        "星級上限": hi,
        "操作配置": cfg,
    }


def build_melee_gs(lo: int, hi: int, nt: int, has_ch: bool, has_sk: bool) -> dict:
    taps = [
        tap_entry("橫掃斬", 0.40 + 0.02 * (lo // 4), 4.8),
        tap_entry("上撈斬", 0.32 + 0.02 * (lo // 4), 4.6),
    ]
    if nt >= 3:
        taps.append(tap_entry("飛身縱斬", 0.38 + 0.02 * (lo // 4), 5.0))
    ch = charge_standard("蓄力斬", 0.65, 1.05, 1.60, 0.75, 1.45, 2.20) if has_ch else None
    sk = (
        skill_entry("真・蓄力斬", 15, 2.1 + 0.05 * (hi - 10), "單發極大威力，對弱點特別有效。", 段數=1)
        if has_sk
        else None
    )
    return row("大劍", "高單發傷害／蓄力爆發", lo, hi, taps, ch, sk)


def build_hammer(lo: int, hi: int, nt: int, has_ch: bool, has_sk: bool) -> dict:
    taps = [
        tap_entry("縱敲", 0.38, 4.2),
        tap_entry("橫掃本壘", 0.34, 4.4),
    ]
    if nt >= 3:
        taps.append(tap_entry("踏步大地擊", 0.42, 4.5))
    ch = (
        charge_standard("蓄力升龍", 0.55, 0.95, 1.45, 0.70, 1.35, 2.05)
        if has_ch
        else None
    )
    sk = (
        skill_entry("巨力旋風", 14, 0.09, "多段打擊並累積眩暈值。", 段數=8)
        if has_sk
        else None
    )
    return row("大錘", "擅長暈眩／打擊系控場", lo, hi, taps, ch, sk)


def build_ls(lo: int, hi: int, nt: int, has_ch: bool, has_sk: bool) -> dict:
    taps = [
        tap_entry("踏步斬", 0.26, 5.0, 氣刃增加=8),
        tap_entry("直刺", 0.18, 4.8, 氣刃增加=6),
    ]
    if nt >= 3:
        taps.append(tap_entry("氣刃二連", 0.22, 4.9, 氣刃增加=10))
    ch = (
        {
            "招式名稱": "居合撥刀・反擊斬",
            "動作倍率": 0.88 + 0.02 * (lo // 4),
            "特性": "反擊判定；成功可免疫當下傷害並回氣刃。",
            "消耗氣刃": 25,
        }
        if has_ch
        else None
    )
    sk = (
        skill_entry("氣刃大迴旋", 18, 1.35, "削減氣刃並提升刃色階，暫時提高攻擊面。", 段數=3)
        if has_sk
        else None
    )
    return row("太刀", "靈活連段／可反擊", lo, hi, taps, ch, sk)


def build_db(lo: int, hi: int, nt: int, has_ch: bool, has_sk: bool) -> dict:
    taps = [
        tap_entry("二連斬", 0.11, 4.0, 段數=2),
        tap_entry("車輪斬", 0.09, 4.0, 段數=3),
    ]
    if nt >= 3:
        taps.append(tap_entry("螺旋突進斬", 0.13, 4.3, 段數=2))
    ch = (
        {
            "招式名稱": "鬼人化",
            "動作倍率": 0.28,
            "特性": "持續消耗耐力；攻速上升，所有點擊 MV +8%。",
            "每秒耐力消耗": 7 + lo // 3,
        }
        if has_ch
        else None
    )
    sk = (
        skill_entry("鬼人亂舞", 12, 0.065, "原地極速多段斬擊。", 段數=12)
        if has_sk
        else None
    )
    return row("雙劍", "高速連擊／鬼人化", lo, hi, taps, ch, sk)


def build_ig(lo: int, hi: int, nt: int, has_ch: bool, has_sk: bool) -> dict:
    taps = [
        tap_entry("舞棍掃擊", 0.27, 4.6),
        tap_entry("印劍突刺", 0.22, 4.8),
    ]
    if nt >= 3:
        taps.append(tap_entry("空中舞踏斬", 0.30, 5.2))
    ch = (
        {
            "招式名稱": "起跳撩擊／舞空姿態",
            "動作倍率": 0.45,
            "特性": "長按蓄力後躍起；維持浮空期間可銜接輕攻擊。",
        }
        if has_ch
        else None
    )
    sk = (
        skill_entry("操蟲強化突刺", 17, 1.15, "獵蟲與本體同步一擊，偏重單發墜機點。", 段數=2)
        if has_sk
        else None
    )
    return row("操蟲棍", "空中戰鬥／立回", lo, hi, taps, ch, sk)


def build_lbg(lo: int, hi: int, nt: int, has_ch: bool, has_sk: bool) -> dict:
    taps = [
        tap_entry("速射三連", 0.11, 10.0, 段數=3, 最佳距離="6-14m"),
        tap_entry("斷屬性速射", 0.13, 10.5, 段數=2, 最佳距離="6-14m"),
    ]
    if nt >= 3:
        taps.append(tap_entry("異常徹甲彈", 0.12, 11.0, 段數=2))
    ch = (
        {
            "招式名稱": "精準瞄準",
            "動作倍率": 0.22,
            "特性": "縮放準星並小幅提升屬性／異常累積效率。",
            "移動速度降低": 0.45,
        }
        if has_ch
        else None
    )
    sk = (
        skill_entry("起爆龍彈佈雷", 22, 0.75, "地面陷阱類爆炸；適合引怪踩踏。", 段數=1)
        if has_sk
        else None
    )
    return row("輕弩", "機動射擊／屬性・異常彈", lo, hi, taps, ch, sk)


def build_hbg(lo: int, hi: int, nt: int, has_ch: bool, has_sk: bool) -> dict:
    taps = [
        tap_entry("高壓單發", 0.48, 12.0),
        tap_entry("裝填特殊彈", 0.35, 11.5, 段數=2),
    ]
    if nt >= 3:
        taps.append(tap_entry("龍擊彈預備射", 0.52, 13.0))
    ch = (
        charge_standard("蓄力穿甲炮", 0.70, 1.15, 1.70, 0.90, 1.70, 2.55)
        if has_ch
        else None
    )
    sk = (
        skill_entry("超解放齊射", 26, 0.85, "短時間全彈傾瀉；低機動懲罰。", 段數=5)
        if has_sk
        else None
    )
    return row("重弩", "高火力／特殊彈藥", lo, hi, taps, ch, sk)


def build_bow(lo: int, hi: int, nt: int, has_ch: bool, has_sk: bool) -> dict:
    taps = [
        tap_entry("迅射", 0.16, 9.5, 段數=2),
        tap_entry("滑步剛射", 0.20, 9.8),
    ]
    if nt >= 3:
        taps.append(tap_entry("剛連射", 0.18, 10.0, 段數=3))
    ch = (
        {
            "招式名稱": "曲射／高蓄力矢",
            "分段倍率": {"一段": 0.45, "二段": 0.75, "三段": 1.15},
            "蓄力時間_秒": [0.55, 1.05, 1.65],
            "震動強度": 0.5,
        }
        if has_ch
        else None
    )
    sk = (
        skill_entry("龍穿剛矢", 20, 1.45, "長距離高蓄一箭，對大型目標有效。", 段數=1)
        if has_sk
        else None
    )
    return row("弓", "中距離蓄力／曲射", lo, hi, taps, ch, sk)


BUILDERS = [
    build_melee_gs,
    build_hammer,
    build_ls,
    build_db,
    build_ig,
    build_lbg,
    build_hbg,
    build_bow,
]


def _insert_move_id_after_name(d: dict, move_id: str) -> None:
    """在「招式名稱」鍵之後插入「招式編號」，保留其餘鍵順序。"""
    if "招式名稱" not in d:
        return
    ordered = {}
    for k, v in d.items():
        ordered[k] = v
        if k == "招式名稱":
            ordered["招式編號"] = move_id
    d.clear()
    d.update(ordered)


def stamp_move_ids(rows: list) -> int:
    """依資料列順序：點擊陣列 → 長按 → 專屬技能，編 MOV_001 起迄。回傳最後編號（如 112）。"""
    n = 1
    for row in rows:
        cfg = row.get("操作配置") or {}
        taps = cfg.get("點擊")
        if isinstance(taps, list):
            for t in taps:
                if isinstance(t, dict) and "招式名稱" in t:
                    _insert_move_id_after_name(t, f"MOV_{n:03d}")
                    n += 1
        elif isinstance(taps, dict) and "招式名稱" in taps:
            _insert_move_id_after_name(taps, f"MOV_{n:03d}")
            n += 1
        hold = cfg.get("長按")
        if isinstance(hold, dict) and "招式名稱" in hold:
            _insert_move_id_after_name(hold, f"MOV_{n:03d}")
            n += 1
        sk = cfg.get("專屬技能")
        if isinstance(sk, dict) and "招式名稱" in sk:
            _insert_move_id_after_name(sk, f"MOV_{n:03d}")
            n += 1
    return n - 1


def main() -> None:
    rows: list = []
    for tier in TIERS:
        lo, hi, nt, has_ch, has_sk = tier
        for b in BUILDERS:
            rows.append(b(lo, hi, nt, has_ch, has_sk))
    last = stamp_move_ids(rows)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(rows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(OUT, len(rows), "rows", f"MOV_001–MOV_{last:03d}")


if __name__ == "__main__":
    main()
