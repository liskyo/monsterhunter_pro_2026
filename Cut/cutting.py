#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
將大圖依固定格數裁切成小 PNG，不需 JSON。

**MAT 類預設（格子、檔名列／行、裁切區間、前綴、預設輸入圖）請只改下方的**
``MAT = MatCropSettings(...)`` **一次改齊。** 命令列 `-r/-c/-p` 會覆寫格子與前綴；區間／列號起算仍以 `MAT` 為準（除非日後再加參數）。

用法範例：
  python Cut/cutting.py 素材總覽.png -r 5 -c 5 -p MAT
  python Cut/cutting.py *.png -r 8 -c 8 -p MAT -o Assets/UI/Materials/icons
  python Cut/cutting.py --folder ./原始圖 -r 5 -c 5 -p ICON
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


# ═══════════════════════════════════════════════════════════════
# 【MAT 整合區】★ 只改這一段：``MatCropSettings(...)`` ★
#
# rows／cols　：大圖切成幾橫列 × 幾直行（與視覺格線一致）。
# asset_prefix ：檔名前綴，例如 MAT → MAT_001_01.png（命令列 -p 可覆寫）。
# name_row_first_1／name_col_first_1
#              ：左上角「第一格」在企劃上的列號／行號（例 6、1 → MAT_006_01）。
# crop_*_0     ：要「跳過」的格子時，設 0-based 半開區間 [start, end)；
#               全輸出就通通 None。（例只想 _01〜_04：crop_col_end_excl_0 = 4）
# input_if_no_cli：未傳圖、未用 --folder 時自動裁這張（"" = 一定要先命令列給檔）。
#
# 例：MAT_006_01〜MAT_010_04（大圖 5×5、不要第 5 直行）
#     name_row_first_1=6, crop_col_end_excl_0=4，crop 其餘 None。
# ═══════════════════════════════════════════════════════════════


@dataclass(frozen=True)
class MatCropSettings:
    rows: int = 5
    cols: int = 5
    asset_prefix: str = "MAT"
    name_row_first_1: int = 1
    name_col_first_1: int = 1
    crop_row_start_0: int | None = None
    crop_row_end_excl_0: int | None = None
    crop_col_start_0: int | None = None
    crop_col_end_excl_0: int | None = None
    input_if_no_cli: str = ""


MAT = MatCropSettings(
    rows=5,
    cols=5,
    asset_prefix="MAT",
    name_row_first_1=6,
    name_col_first_1=1,
    crop_row_start_0=None,
    crop_row_end_excl_0=None,
    crop_col_start_0=None,
    crop_col_end_excl_0=None,
    input_if_no_cli=(
        r"C:\Users\gc\Desktop\MonsterHunter_PRO_2026\monsterhunter_pro_2026\Cut\掉落物6-10.png"
    ),
)


def build_output_filename(
    prefix: str,
    *,
    mat_row_num_1: int,
    mat_col_num_1: int,
) -> str:
    pfx = prefix.strip("_") or MAT.asset_prefix.strip("_") or "MAT"
    return f"{pfx}_{mat_row_num_1:03d}_{mat_col_num_1:02d}.png"


def _range_or_full(start: int | None, end_excl: int | None, max_n: int) -> range:
    """start=None 當 0；end_excl=None 當 max_n（用滿這一向度）。"""
    s = 0 if start is None else max(0, start)
    e = max_n if end_excl is None else min(max_n, end_excl)
    if e <= s:
        raise ValueError(f"裁切區間無效：start={start!r} end_excl={end_excl!r} max={max_n}")
    return range(s, e)


def slice_one(
    image_path: Path,
    *,
    cols: int,
    rows: int,
    prefix: str,
    output_dir: Path | None,
    profile: MatCropSettings = MAT,
) -> Path:
    """裁切單張圖，回傳實際輸出資料夾路徑。"""
    cols = max(1, cols)
    rows = max(1, rows)

    img = Image.open(image_path).convert("RGBA")
    w, h = img.size
    cell_w = w // cols
    cell_h = h // rows
    if cell_w <= 0 or cell_h <= 0:
        raise ValueError(f"圖片太小或格子數過多：{w}x{h} / {cols}x{rows}")

    out = output_dir if output_dir is not None else Path("Cut") / image_path.stem
    out.mkdir(parents=True, exist_ok=True)
    prefix = prefix.strip("_") or profile.asset_prefix.strip("_") or "MAT"

    row_iter = _range_or_full(
        profile.crop_row_start_0,
        profile.crop_row_end_excl_0,
        rows,
    )
    col_iter = _range_or_full(
        profile.crop_col_start_0,
        profile.crop_col_end_excl_0,
        cols,
    )

    n = 0
    for row in row_iter:
        for col in col_iter:
            left = col * cell_w
            upper = row * cell_h
            sprite = img.crop((left, upper, left + cell_w, upper + cell_h))
            fname = build_output_filename(
                prefix,
                mat_row_num_1=profile.name_row_first_1 + row,
                mat_col_num_1=profile.name_col_first_1 + col,
            )
            dest = out / fname
            sprite.save(dest)
            n += 1
            print(f"  [{n:3}] {dest}")

    print(f"完成：{image_path.name} → {n} 張，資料夾 {out.resolve()}")
    return out


def collect_inputs(paths: list[str], folder: str | None) -> list[Path]:
    ps: list[Path] = []
    for p in paths:
        pi = Path(p)
        if not pi.exists():
            print(f"[略過] 找不到檔案：{p}")
            continue
        ps.append(pi)

    if folder:
        fd = Path(folder)
        if not fd.is_dir():
            raise SystemExit(f"不是資料夾：{folder}")
        for ext in ("*.png", "*.PNG", "*.jpg", "*.jpeg", "*.webp"):
            ps.extend(sorted(fd.glob(ext)))

    seen = set()
    uniq: list[Path] = []
    for p in ps:
        if p.is_file() and p.resolve() not in seen:
            seen.add(p.resolve())
            uniq.append(p)
    return uniq


def main() -> None:
    ap = argparse.ArgumentParser(
        description="格狀裁圖並自動命名（不需 JSON）。區間／列號起算見程式內 MAT。",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    ap.add_argument(
        "images",
        nargs="*",
        help=f"要裁切的圖；皆空則試用程式內 MAT.input_if_no_cli 或搭配 --folder",
    )
    ap.add_argument(
        "-folder",
        "--folder",
        metavar="DIR",
        dest="folder",
        help="此資料夾內所有 png/jpg/webp 各別裁一份",
    )
    ap.add_argument(
        "-r",
        "--rows",
        type=int,
        default=MAT.rows,
        help=f"縱向格數（default: MAT.rows=%(default)s）",
    )
    ap.add_argument(
        "-c",
        "--cols",
        type=int,
        default=MAT.cols,
        help=f"橫向格數（default: MAT.cols=%(default)s）",
    )
    ap.add_argument(
        "-p",
        "--prefix",
        default=MAT.asset_prefix,
        help=f"檔名前綴（default: MAT.asset_prefix=%(default)s）",
    )
    ap.add_argument(
        "-o",
        "--output",
        metavar="DIR",
        dest="output",
        help="輸出根目錄（單檔時可省略，預設 Cut/<檔名不含副檔>）",
    )

    args = ap.parse_args()

    imgs: list[str] = list(args.images)
    if not imgs and not args.folder:
        d = (MAT.input_if_no_cli or "").strip()
        if d:
            imgs.append(d)

    targets = collect_inputs(imgs, args.folder)
    if not targets:
        ap.print_help()
        raise SystemExit(
            "請指定至少一張圖片、或 --folder，或在 MAT.input_if_no_cli 填入預設圖。"
        )

    root_out = Path(args.output) if args.output else None

    for tp in targets:
        if root_out is None:
            od = None
        elif len(targets) == 1:
            od = root_out
        else:
            od = root_out / tp.stem

        slice_one(
            tp,
            cols=args.cols,
            rows=args.rows,
            prefix=args.prefix,
            output_dir=od,
            profile=MAT,
        )


if __name__ == "__main__":
    main()
